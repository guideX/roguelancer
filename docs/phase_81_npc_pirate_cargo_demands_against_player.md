# Phase 81 — NPC Pirate Cargo Demands Against the Player

Phase 81 adds a bounded, emergent NPC-to-player piracy encounter. An eligible
Liberty Rogue NPC confronts the player and demands real player-owned cargo.
The player faces a bounded choice: **COMPLY** (surrender cargo) or **REFUSE**
(escalate to combat). This is the mirror image of the existing player-to-trader
`PirateCargoDemandService`, reusing the same cargo, combat, and lifecycle
authorities without creating any parallel system.

**Phase 81 adds a bounded NPC-to-player piracy encounter using existing cargo
and combat authorities; it adds no parallel inventory, mission, or
criminal-state authority.**

## Duplicate-Feature Audit

A comprehensive search of the codebase confirmed that no existing system lets
an NPC pirate/Rogue hail the player, demand player cargo, present a
comply/refuse choice, take player cargo on compliance, or escalate to combat
on refusal. The existing `PirateCargoDemandService` is the inverse direction
(player extorts NPC trader). The closest analog is `PoliceScanSystem` (lawful
contraband enforcement), which is a different authority with different rules.
No duplicate was found; Phase 81 is a new build.

## Existing Player-to-NPC Piracy Architecture

`PirateCargoDemandService` (player-to-trader extortion) provided the conceptual
mirror:

- **Target eligibility**: faction, behavior type, range, combat state
- **Response window**: bounded timer with deterministic resolution
- **Quantity selection**: stable-hash deterministic 25-75% of remaining cargo
- **Compliance**: removes cargo through existing authority, marks trader fleeing
- **Refusal**: marks trader fleeing, escalates through existing combat
- **Cooldown**: per-target identity set prevents repeat demands
- **Lifecycle**: transient state cleared on reset/save/load

Phase 81 reuses these concepts but reverses the direction and adapts them to
player cargo authority.

## Player Cargo Authority

`CargoHold` is the sole player cargo authority. Key properties:

- **Storage**: `_commodities` (total per name), `_stolenCommodities` (stolen
  subset), `_missionCargo` (mission reservations), `_missionReservationTargets`
  (freight contract targets)
- **Provenance**: `CargoProvenance.Clean` / `CargoProvenance.Stolen` tracked as
  bounded aggregates per commodity
- **Removal**: `RemoveSellableCommodity(commodity, quantity, preferStolen)`
  respects mission/freight reservations and preserves provenance
- **Eligibility**: `GetSellableCommodityQuantity(name)` excludes mission-reserved
  and freight-reserved quantities
- **Contraband**: ordinary commodity cargo; eligible if unreserved
- **Mission cargo**: `IsMissionCargo` commodities are excluded from demands

## Rogue Encounter Architecture

- **Faction**: `liberty_rogues` (canonical Liberty Rogue faction)
- **Spawn**: via `TrafficManager` in pirate ambush or lawful patrol zones
- **AI states**: `TrafficEncounterState` enum (Cruising, Fleeing, AttackingPlayer,
  etc.)
- **Combat targeting**: `SetPlayerTarget(position, reason)` through existing
  `NpcFactionCombatTargeting`
- **Flee behavior**: `TrafficManager.MarkNpcFleeing` or `SetEncounterState(Fleeing)`
- **Trade-lane transit**: `NpcShip.IsTradeLaneTransit` prevents demand initiation

## Demand Initiation Policy

1. Eligible Liberty Rogue is within `DemandRange` (3000 m) of the player
2. Player is valid (not destroyed, not docked, not in trade-lane transit, not
   in Police interaction, has eligible cargo)
3. Rogue is valid (not destroyed, not in trade-lane transit, not in combat,
   not mission-owned, not already resolved a demand)
4. Relationship gating: player must not be Allied or Friendly with Liberty Rogues
5. Player-global cooldown (30 s) must have expired
6. One active demander at a time
7. Deterministic selection: nearest eligible Rogue, stable-identity tiebreak

Cargo is enumerated only when a demand is created, not every frame.

## Rogue Eligibility

- Canonical faction `liberty_rogues`
- Live/not destroyed
- Not mission-held for unrelated authority
- Not in trade-lane transit
- Not already in incompatible combat (`IsTrafficEngaged`, `FactionCombatTarget`)
- Not already resolving another demand
- Within bounded local interaction range (3000 m)
- Not Allied/Friendly with the player under `ReputationManager`

## Player Eligibility

- Not destroyed
- Not docked
- Not in trade-lane transit
- Not already resolving a Police comply/refuse interaction
- Not already in another pirate-demand interaction
- Carries eligible cargo (ordinary non-mission commodity with sellable quantity)

## Relationship Gating

`ReputationManager.IsAllied` and `IsFriendly` suppress demands. A player with
standing >= 0.20 (Friendly) or >= 0.60 (Allied) with Liberty Rogues is not
extorted. Neutral, Unfriendly, and Hostile players are eligible. This uses
existing faction relationship authority without inventing a bespoke piracy
reputation threshold.

## Cargo Eligible

- Ordinary non-mission commodity cargo (`!IsMissionCargo`)
- Legal or stolen provenance
- Sellable quantity > 0 (excludes mission-reserved and freight-reserved)
- Contraband is eligible if unreserved (preserves commodity identity)
- Stolen cargo preserves provenance through the haul transfer

## Protected Mission/Freight Cargo Behavior

`GetSellableCommodityQuantity` excludes both mission-reserved cargo
(`_missionCargo`) and freight-reserved cargo (`_missionReservationTargets`
satisfied into `_missionCargo`). Pirates can never demand protected cargo.
Compliance removes only sellable quantities; mission and freight reservations
remain intact.

## Quantity and Commodity Selection

- 1-3 commodity types selected deterministically via stable hash of demander
  identity
- 25-75% of each selected commodity's sellable quantity
- Never exceeds eligible quantity
- Integral units
- Deterministic and testable

## Demand Presentation

```
LIBERTY ROGUES DEMAND CARGO: Food Rations x4, Water x2 | [Enter] Comply [N] Refuse (5s)
```

Presented via `NotificationManager.ShowMessage` and a dedicated HUD line.
Uses existing comply/refuse keys (`Enter` = Comply, `N` = Refuse), same as
`PoliceScanSystem`. No modal dialogue engine is created.

## Response Timeout

5-second response window. **Timeout = refusal**: the demand closes, no cargo
is removed, and the Rogue escalates through existing combat targeting. This is
documented and deterministic. The timeout cannot process twice.

## Compliance Behavior

1. Revalidate encounter (demander alive, in range, demand active)
2. Revalidate demander identity
3. Revalidate requested cargo availability
4. Remove exact quantities via `RemoveSellableCommodity` (prefers stolen)
5. Record removed quantities and provenance in bounded pirate haul
6. End demand interaction
7. Pirate flees via `TrafficManager.MarkNpcFleeing`
8. No combat escalation from the demand itself
9. One bounded notification
10. No reputation mutation, no Police interaction, no fugitive Heat

## Pirate Haul/Cargo Transfer Authority

Surrendered cargo enters a bounded `PirateDemandHaulEntry` list on the service,
keyed by the demander NPC. Each entry tracks `CleanQuantity` and
`StolenQuantity` separately. When the demander is destroyed, the haul is
dropped as physical stolen pods via `LootManager.SpawnStolenCargo`, enabling
natural recovery by the player. This is not a second player inventory — it is
a bounded NPC haul that uses existing loot authority.

## Refusal Behavior

- Removes no cargo
- Mutates no credits
- Preserves mission/freight cargo
- Closes demand interaction
- Escalates via `SetPlayerTarget(player.Position, FactionDisposition)`
- Rogue can pursue, fire, and damage through existing NPC combat systems

## Combat Escalation

Refusal and timeout use `NpcShip.SetPlayerTarget` with
`NpcPlayerTargetReason.FactionDisposition`. This routes through existing
`NpcFactionCombatTargeting` and `FactionCombatEscalationService`. No new
combat mode is created.

## Player Aggression During Demand

If the player fires on the demanding pirate (`WasDamagedByPlayer`), the demand
resolves as refusal through existing combat authority. The player cannot shoot
the demander and then comply after it is dead — compliance revalidates that
the demander is alive.

## Demander Death/Despawn Behavior

`NotifyNpcDestroyed` cancels the interaction, removes no cargo, drops the
pirate's haul as physical pods, and clears the presentation. No stale UI, no
delayed deduction, no refusal penalty.

## Police/Faction Interaction

This is a Rogue crime against the player, not a Police crime by the player.
No Police reputation change, no fugitive Heat, no docking restriction, no
surrender/docking flag. Nearby Liberty Police behave according to ordinary
faction-combat rules (hostile to Rogues naturally).

## Trade-Lane Interaction

No demand is initiated while either participant is in trade-lane transit.
Player trade-lane state (`Ship.IsTradeLaneTransit`) and NPC trade-lane state
(`NpcShip.IsTradeLaneTransit`) both block initiation. After lane exit, future
demand eligibility resumes.

## Dock/System Lifecycle

Active demand cancels safely on:
- Dock (`HandleDockingCompleted`)
- Player death (`Hull.IsDestroyed` check)
- System transition (`HandleSystemChange`)
- Save/load (`Reset`)

No cargo deduction after cancellation. No persistent encounter state.

## Save/Load/Schema Result

No demand state is persisted. `Reset()` clears all runtime state. Saving and
loading does not recreate a pending extortion prompt. No schema change is
required. `SaveGameData.CurrentSchemaVersion` remains 13. No Phase 81 save
field exists.

## Physical Cargo/Accounting Invariant

For compliance:

```
player eligible quantity before = player eligible quantity after + quantity transferred to pirate haul
```

No cargo duplication. No phantom cargo. The pirate haul is the only
representation of surrendered cargo outside the player's hold.

## Performance/Bounds

- Bounded local candidate query at existing traffic cadence (O(active NPCs))
- Cargo enumeration only at demand creation (O(eligible commodity rows))
- Compliance removal is O(requested lines)
- No global NPC search, no per-frame cargo scan, no demand history ledger
- Runtime-only cooldown and resolved-identity sets

## Tests

Smoke test file: `Phase81NpcPirateCargoDemandSmokeTest.cs`
70 test cases covering initiation, eligibility, cargo selection, compliance,
refusal, timeout, lifecycle, persistence, relationship gating, and regression
guards for all related systems.

Run: `dotnet run -c Debug -- --phase81-smoke`

## Limitations

- Only Liberty Rogues are eligible demanders in Phase 81
- The encounter is emergent, not mission-driven
- No credit ransom, debt, or boarding
- The pirate haul drops only stolen pods on destruction (clean cargo is
  consumed by the encounter)
- Compliance does not grant invulnerability or faction-wide truce

## Recommended Phase 82

Potential extensions:
- Additional pirate factions (Junkers, Bounty Hunters) as demanders
- Pirate demand frequency scaling with player notoriety
- Hail/comms UI integration for the demand presentation
- Pirate haul delivery to criminal receivers (black market stations)
