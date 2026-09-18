# Phase 70 — Player Contraband Interdiction

Phase 70 makes contraband law apply to the player through the existing
faction/traffic/combat architecture. A nearby lawful enforcement NPC that
physically sees canonical contraband in the live player hold acquires the
player through the ordinary NPC AI and weapon pipeline with an explicit,
distinguishable contraband origin.

The production proof is a real player hold:

`player CargoHold with contraband → bounded Police detection → ContrabandEnforcement player target → normal NPC weapons/damage → jettison/confiscation/destruction clears pursuit`

## Architecture and ownership

- `Commodity.IsContraband` remains the one canonical legality
  classification. No second hard-coded illegal list was added. Legal stolen
  goods alone never count.
- `CargoHold` (`Ship.CargoHold`) remains the sole authoritative player cargo
  owner. No shadow contraband inventory exists. Mission-reserved units are
  physical hold contents and therefore count, matching the existing police
  scan semantics.
- `ContrabandEnforcementPolicy` remains the read-only detection policy. It
  now exposes `HasPlayerContraband(CargoHold)` and
  `IsValidPlayerTarget(enforcer, playerShip, maxDistance)` alongside the
  unchanged NPC-to-NPC path.
- `TrafficManager.UpdateFactionCombatEngagements` remains the bounded local
  target-acquisition owner. It reuses the same 3D spatial cells for NPC and
  player enforcement, then gives a player-contraband target the explicit
  `NpcPlayerTargetReason.ContrabandEnforcement` reason. Existing hostile
  faction, retaliation, fugitive, mission, security, pirate, and NPC
  contraband acquisition remain separate and retain priority where already
  valid.
- `NpcShip` still owns encounter state, movement, target lifetime, damage,
  and destruction. The player-contraband validator is a transient
  `Func<bool>` callback supplied by `TrafficManager`; it is not save data.
  Acquisition is traffic-owned; retention is cargo-backed.
- `NpcWeaponSystem` remains the only NPC projectile/damage path. Police
  receive no scripted damage or instant-confiscation behavior.
- `PoliceScanSystem` / `PoliceEnforcementService` / `CargoHold`
  confiscation, `LootManager.TryJettisonContraband` physical pods, and normal
  player destruction remain the only cargo-removal authorities. Phase 70 adds
  no dialog, fine, bribe, jail, or seizure UI.
- `SaveGameData` requires no new Phase 70 fields. Active Phase 68 shipments
  and physical pods keep their authoritative save paths; transient player
  pursuit state is rebuilt or discarded at world teardown like Phase 69.

## Legality and enforcement policy

`ContrabandEnforcementPolicy.IsValidPlayerTarget` requires all of:

- the source is the configured lawful enforcement faction
  (`liberty_police` via `IsLawfulEnforcementFaction`),
- source is a distinct live NPC, not in trade-lane transit and not in
  mission hold,
- player is live (`Hull.IsDestroyed != true`) and not in trade-lane transit,
- the live `CargoHold` contains at least one positive-quantity commodity
  whose canonical definition has `IsContraband == true`,
- the player is within the source's existing `TrafficActivationRange`
  (default `DefaultDetectionRange = 6,500`, clamped minimum 100).

Stolen provenance is ignored. A cleanly purchased `side-arms` unit is
contraband; a stolen `water` unit is not.

Only `liberty_police` enforces. Rogues, neutrals, corporations, and other
factions never acquire `ContrabandEnforcement` player targets even when
close to a smuggling player.

## Detection, targeting, and combat

Detection is local and deterministic with no RNG:

- the traffic pass builds the existing 3D spatial-cell neighborhood,
- the player cell plus 26 neighbors is inspected, at most 64 candidates per
  tick (`MaximumCandidatesPerSource`),
- candidates are ordered nearest-first, then stable name, then stable NPC
  identity,
- at most 16 concurrent player enforcers are tracked
  (`MaximumPlayerContrabandEnforcers`), matching the existing seizure-pursuit
  bound,
- a new acquisition requires a cruising (non-engaged) lawful NPC with no
  other valid faction or player target and no disengagement suppression;
  existing valid NPC faction targets and other valid player reasons retain
  priority and are never preempted,
- acquisition calls the existing `SetPlayerTarget(pos,
  ContrabandEnforcement)`; refresh of an existing enforcer only moves the
  encounter anchor and creates no duplicate incident state,
- movement continues through `NpcShip` engagement movement and weapon
  selection continues through `NpcWeaponSystem` via the existing
  `HasValidPlayerTarget` gate, which for this reason is cargo-backed.

The reason is distinguishable from `FactionDisposition`,
`PlayerInitiatedAggression`, and `FugitivePursuit`. No permanent or
temporary faction standing is changed by detection alone.

## Enforcement lifetime

cargo-clean clears immediately. This mirrors the Phase 69 NPC rule where an
emptied manifest invalidates the target and ordinary rules apply:

- contraband no longer present: every `ContrabandEnforcement` player target
  clears on the next traffic pass and via `NpcShip` retention; if ordinary
  disposition separately justifies a target, that later pass re-acquires
  with its own reason,
- enforcing NPC destroyed: removed from the transient set via
  `NotifyNpcDestroyed`; no resurrection,
- player destroyed: all transient pursuit clears on the next traffic pass;
  normal player destruction remains authoritative and is not duplicated,
- traffic entity despawn / `UnregisterMissionNpc` / `ReleaseShip`: removed
  from the transient set and validator cleared,
- player/world reset / `ResetContrabandEnforcement` /
  `ResetTransientDistressState` / `LoadZonesForSystem`: clears enforcers,
  validators, and seizure bookkeeping without touching market stock, active
  pods, or player cargo,
- save/load: no pursuit, validator, or counter is persisted; a clean hold
  cannot resurrect pursuit and a smuggling hold re-detects locally after
  load through the normal bounded pass.

Distance leash beyond acquisition uses the existing disengagement radii
(soft 7,500 / hard 10,000 / 30s stale). Leaving the 6,500 activation range
prevents new detection; an already-triggered pursuit disengages through the
normal leash, not through a second range check.

## Cargo-loss / confiscation behavior

Phase 70 does not teleport cargo out of a living ship, matching Phase 69:

- player destruction: existing `Ship` destruction is authoritative; no
  duplicate handling and no new pod-drop path was added,
- voluntary jettison: the existing `LootManager.TryJettisonContraband`
  physical-pod seam removes real hold quantities into real `CargoPod`
  objects; a fully jettisoned hold becomes clean and clears pursuit per the
  lifetime rule above,
- voluntary compliance: the existing `PoliceScanSystem` demand plus
  `PoliceEnforcementService.TryResolve` plus `CargoHold.TryConfiscateCargo`
  remains the architecture-consistent fine/confiscation path; Phase 70 does
  not duplicate its UI,
- no fake seizure exists: clearing an enforcement flag never removes cargo;
  only the cargo authorities above mutate the hold.

Automatic seizure from a living player hold without using one of those
authorities is intentionally deferred as the natural Phase 71 follow-up.

## Performance / bounding strategy

- reuses the existing faction-combat spatial cells; no unbounded per-frame
  scan over every NPC,
- at most 64 local candidates inspected per tick for player enforcement,
- at most 16 concurrent player enforcers,
- deterministic nearest/name/identity ordering; repeated checks refresh
  position without duplicating state,
- all enforcement state transient; stale NPC references pruned when
  destroyed, removed, despawned, or cleared.

## Interactions with Phase 68 and Phase 69

- Phase 68 ambient smuggling is untouched. `RogueSmugglingManager` still
  reserves criminal stock, owns manifests, marks loss, and supplies
  destruction salvage; carriers still deliver, drop exact pods, save/load,
  and reset per Phase 68 semantics.
- Phase 69 NPC interdiction is untouched. `SelectNearestTarget`,
  `ContrabandEnforcement` NPC origin, NPC weapon damage, pod pursuit,
  exactly-once seizure, conservation, save/load, and reset all still pass.
  Valid NPC faction targets retain priority over new player acquisition.
- Player interdiction is an extension of the same world logic, not a
  replacement: the same lawful faction, same activation range concept, same
  canonical legality, same weapon/disengagement pipeline, and a parallel
  distinguishable origin for the player target type.

## Smoke coverage

`--phase70-smoke` runs 15 focused checks: legal negative (including legal
stolen), nearby detection, outside-boundary negative plus inside
re-acquire, non-enforcer negative, explicit pursuit-state validity,
existing weapon fire/damage, mixed cargo, authoritative removal clears,
reset/enforcer-destroyed/player-destroyed/despawn clears, Phase 69 NPC
proof, Phase 68 shipment proof, and no-duplicate on repeated checks.

## Intentionally deferred

No smuggling missions, contraband reputation trees, jail, police stations,
wanted levels, bribery, scan minigames, hidden compartments, stealth
scanners, contraband dealers, new commodities, broad UI redesigns, or
procedural crime systems. No new RNG, stealth equipment, cloaking, forged
manifests, or hidden holds. Automatic live-hold seizure without the
existing demand/jettison/destruction authorities remains Phase 71.
