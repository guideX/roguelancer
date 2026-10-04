# Phase 82 — Pirate Haul Recovery and Provenance

Phase 82 closes the cargo-conservation gap left by Phase 81. Phase 81 lets an
eligible Liberty Rogue NPC demand real player cargo, but its haul tracked a
clean/stolen split and its destruction drop only emitted the stolen portion.
Cargo that was clean when the player owned it was therefore consumed by the
encounter: the pirate took it, and when the pirate died, nothing physical was
released for it.

Phase 82 makes the result physically coherent. Once a pirate takes cargo from
the player, that cargo is stolen property in the pirate's possession and stays
part of the authoritative pirate haul. If the pirate is destroyed before
disposing of it, the haul drops back into space through the existing
`LootManager` / `CargoPod` authority.

**Phase 82 preserves player-surrendered cargo inside the existing pirate
haul/loot authority and adds no ownership-history, inventory, or
criminal-state system.**

## Duplicate-Feature Audit

The stale/duplicate-prompt gate inspected the production code and the Phase 81
documentation. Phase 81's own limitations section states:

> The pirate haul drops only stolen pods on destruction (clean cargo is
> consumed by the encounter).

The production representation confirmed this. `PirateDemandHaulEntry` stored
`CleanQuantity` and `StolenQuantity` separately, and `DropPirateHaul` only
called `_spawnStolenCargo` for entries with `StolenQuantity > 0`. Clean-origin
surrendered cargo was recorded but never released. The feature was **not**
already implemented; Phase 82 is a focused repair, not a duplicate build.

## Phase 81 Pirate Haul Architecture (as discovered)

- `NpcPirateCargoDemandService` owns a transient
  `Dictionary<NpcShip, List<PirateDemandHaulEntry>> _pirateHauls`.
- On compliance, the service removes the exact requested quantity through
  `CargoHold.RemoveSellableCommodity(commodity, quantity, preferStolen: true)`
  and recorded the removed quantity split into `CleanQuantity` /
  `StolenQuantity`.
- On destruction, `NotifyNpcDestroyed` called `DropPirateHaul`, which iterated
  the haul and invoked the injected `_spawnStolenCargo` delegate only for
  `StolenQuantity`.
- The game wires `_spawnStolenCargo` to
  `LootManager.SpawnStolenCargo(demander, commodityId, quantity, ...)`, which
  creates a canonical `CargoPod` with `CargoProvenance.Stolen`.
- The haul is transient: `Reset()` clears `_pirateHauls`, and no haul state is
  saved.

## Exact Clean-Cargo Loss Mechanism

1. Player owns `Food x6` clean.
2. The pirate demands `Food x3`; the player complies.
3. `RemoveSellableCommodity(..., preferStolen: true)` removes 3 clean units.
   The diff produced `cleanRemoved = 3`, `stolenRemoved = 0`.
4. The haul entry was `{ CleanQuantity = 3, StolenQuantity = 0 }`.
5. When the pirate was destroyed, `DropPirateHaul` skipped the entry because
   `StolenQuantity == 0`.
6. No `CargoPod` was created. Three crates vanished solely because they began
   clean.

Already-stolen surrendered cargo did not hit this bug, because it was recorded
in `StolenQuantity` and was dropped.

## Core Design Rule

**The provenance of cargo changes when the pirate steals it, but the commodity
identity and quantity do not.**

- Clean player `Food x3` → pirate haul `Food x3` stolen.
- Already-stolen player `Food x3` → pirate haul `Food x3` stolen.
- Contraband/legal classification is untouched.

## Transfer Semantics

On Phase 81 compliance the service now:

1. Revalidates the player cargo (`GetSellableCommodityQuantity`).
2. Removes the exact selected quantity through `CargoHold` authority
   (`RemoveSellableCommodity`, preferring stolen units so remaining buckets stay
   correct).
3. Preserves mission and freight reservations (sellable quantity already
   excludes them).
4. Adds the exact quantity to the pirate's existing bounded haul.
5. Records that pirate-held quantity as stolen (the haul is stolen by
   definition).
6. Completes the demand.
7. Keeps the existing flee/disengage behavior.

No `Phase82PirateCargo` type, shadow manifest, or second container was added.

## Provenance Transition

The pirate haul representation was normalized. `PirateDemandHaulEntry` now
carries only:

- `CommodityId`
- `CommodityName`
- `Quantity`
- `Provenance => CargoProvenance.Stolen` (a fact, not a legality flag)

The `CleanQuantity` / `StolenQuantity` split was removed.

- **Clean → Stolen**: the main fix. Clean player cargo becomes stolen pirate
  haul.
- **Stolen → Stolen**: already-stolen player cargo stays stolen. No
  `DoubleStolen` category is created and no provenance multiplication occurs.

## Contraband Independence

Provenance and commodity legality remain separate dimensions:

| Commodity | Player ownership | After pirate theft |
| --- | --- | --- |
| Legal | Clean | legal commodity, stolen provenance |
| Contraband | Clean | contraband commodity, stolen provenance |
| Contraband | Stolen | contraband, stolen |

`Commodity.IsContraband` is never mutated. The Phase 82 suite proves both a
legal commodity (`food-rations`) and a contraband commodity (`side-arms`) keep
their classification after theft and pod drop.

## Pirate Haul Authority

The existing Phase 81 haul is reused. Because it now stores commodity +
quantity and is stolen by definition, all successful player-demand transfers
route through the canonical stolen-haul path. The haul is bounded by the demand
policy (1-3 commodity lines per demand). No unbounded per-pirate history is
created. `TryGetHaulSnapshot` now reports `isStolen: true` for every stack, so
the player-target scanner shows the stolen provenance.

## Destruction Behavior

`NotifyNpcDestroyed` performs a single idempotent `DropPirateHaul`. For each
haul line it calls the injected `_spawnStolenCargo`, which is wired to
`LootManager.SpawnStolenCargo`. That creates an ordinary physical `CargoPod`
with:

- exact commodity identity
- exact quantity (subject to the existing bounded drop policy)
- `CargoProvenance.Stolen`

`DropPirateHaul` removes the haul entry before spawning, so a repeated
destruction notification cannot duplicate pods. The redundant first drop call
that existed in Phase 81 (which could double-drop) was removed.

## Existing Haul Drop Policy

`LootManager.SpawnStolenCargo` is shared with Phase 66 ambient raids and Phase
67 delivery. Phase 82 does **not** modify it. It creates one pod per commodity
line and clamps the pod quantity to the existing `1..40` bound. This is the
legitimate bounded-loss policy of the existing loot authority and is preserved
unchanged.

The distinction Phase 82 fixes is:

- **Legitimate drop policy**: a bounded pod count/quantity from an
  authoritative haul (preserved).
- **Cargo not represented in the haul at all**: clean-origin surrendered cargo
  (fixed).

Recovery is not guaranteed beyond what the existing haul/drop semantics already
guarantee. What Phase 82 guarantees is that clean stolen-from-player cargo
enters the same authoritative haul/drop pipeline as any other stolen pirate
cargo.

## Recovery Behavior

If the player kills the pirate and recovers the pod, the cargo remains stolen.
Phase 82 does not restore the player's former clean ownership merely because
the commodity originally came from them. The game retains no previous-owner
identity and Phase 82 does not add one:

```
clean player Food → pirate steals Food → pirate destroyed → player recovers pod
→ player now owns stolen Food
```

This is intentional. A future insurance/ownership system could behave
differently, but Phase 82 does not invent one.

## Phase 57 Fencing Integration

Because recovered haul is stolen, existing Phase 57 behavior applies naturally:

- A lawful dealer refuses stolen units
  (`"N units flagged as stolen property cannot be sold to a lawful dealer."`).
- A Rogue/Junker black market can fence them.
- The existing 60% fence price
  (`MarketManager.GetFenceListing`, `floor(cleanSellPrice * 0.60)`) is
  unchanged.

Phase 82 modifies no fence pricing and adds no black-market authority. The
suite proves lawful refusal, black-market acceptance, and that the fence price
is exactly the existing 60% derivation.

## Phase 66 Interaction

Phase 66 ambient raids and Phase 81 NPC demands share
`LootManager.SpawnStolenCargo` but keep separate haul ownership:
`AmbientPirateRaidManager` owns autonomous raider hauls;
`NpcPirateCargoDemandService` owns player-demand hauls. Phase 82 did not
refactor either into the other; it converged Phase 81 onto the canonical stolen
representation without touching Phase 66. The `--phase66-smoke` and
`--phase67-smoke` suites pass unchanged.

## Immediate-Death Race

Compliance is atomic. The sequence:

1. compliance commits
2. exact cargo leaves the player
3. cargo enters the pirate haul
4. pirate is destroyed before the next ordinary update

is handled because `NotifyNpcDestroyed` processes the already-committed haul.
The suite proves that a pirate destroyed immediately after compliance still
drops the committed quantity. If the pirate dies before compliance commits, no
haul exists, the player keeps cargo, and no pod is spawned.

## Despawn Behavior

A pirate that successfully flees/despawns with haul leaves the local
simulation through the existing NPC lifecycle. `Reset()` (used on save/load and
system transition) clears the transient haul without spawning cargo. No "refund
on despawn" is added — that would defeat successful piracy. The suite proves
despawn and system transition spawn no pods.

## Lifecycle

- Active demand cancels safely on dock, player death, and system transition.
- A committed haul is unaffected by cancel calls; it remains until destruction
  or reset.
- `NotifyNpcDestroyed` drops once and clears the entry.

## Persistence / Schema

The Phase 81 pirate haul remains transient with NPC lifetime. No new
persistence was added. No demand state and no haul state is saved, and no
duplicate save ledger exists. `SaveGameData.CurrentSchemaVersion` remains `13`.

Provenance that is physically present in space is still governed by the
existing Phase 57 `LootManager.CaptureCargoPods` / `RestoreCargoPods` seam,
which round-trips `IsStolen`. The suite proves a dropped Phase 82 pod
round-trips as stolen.

## Accounting Invariants

For a successful transfer of `Q` units:

```
player quantity decrease = Q
pirate authoritative haul increase = Q
```

No quantity exists in both places. On destruction, the existing haul-drop
policy applies to those `Q` units exactly once. No duplicate pods are emitted
for one haul line, quantities never go negative, and commodity totals do not
duplicate.

## Performance / Bounds

- Compliance cost is `O(number of demanded cargo lines)` (1-3).
- Destruction cost is `O(haul lines)` (1-3).
- No world scans, inventory histories, previous-owner lookups, global
  stolen-cargo maps, or per-frame reconciliation were introduced.

## Tests

Smoke test file: `Phase82PirateHaulRecoverySmokeTest.cs` — 60 cases.

Run:

```
dotnet run -c Debug -- --phase82-smoke
```

Coverage includes: Phase 81 clean compliance, exact clean removal, clean-origin
haul entry and stolen provenance, already-stolen entry, no double-stolen state,
legal/contraband independence, mixed commodity lines, mixed player provenance
buckets, mission/freight reservation protection, refusal/timeout/failed
compliance/dead-demander no-haul, immediate-death retention, destruction
routing through `LootManager`, clean-origin non-consumption, `CargoPod` identity
and stolen provenance, drop-policy quantity, no duplicate pod, pickup
preserving stolen provenance, no clean restoration, lawful refusal, black-market
acceptance, unchanged 60% fence price, no victim/timestamp/criminal-state
history, player→trader piracy unchanged, Phase 66 unchanged, ordinary salvage
unchanged, ordinary destruction unchanged, despawn/transition/player-death/
dock lifecycle, conservation, bounds, and Phase 57 save round-trip.

Regression gates run clean: Phase 57, 66, 68–81, black market, piracy demand,
cargo, loot, salvage, freight, shipment, mission, contraband, reputation, Police
enforcement, Police fugitive, faction combat, traffic, trade-lane, and
`--all-smoke` (82 suites passed, 0 failed). Build warning baseline unchanged at
128 unique warnings (0 new).

## Limitations

- The physical drop still uses the shared bounded pod policy (`1..40` per
  commodity line, one pod per line). Quantities above that bound follow the
  existing loot authority's bounded-loss behavior.
- Recovery is not guaranteed; only the same authoritative pipeline as other
  stolen pirate cargo is guaranteed.
- Recovered cargo stays stolen; there is no rightful-owner reclamation or clean
  restoration.
- Only Liberty Rogues are eligible demanders (unchanged from Phase 81).

## Recommended Phase 83

- Hail/comms UI integration for the demand presentation.
- Pirate haul delivery to criminal receivers for player-demand hauls, if
  desired, reusing the Phase 67 receiver authority.
- Optional player insurance or rightful-owner reclamation as an explicit future
  system (deliberately out of scope here).

## Scope Exclusions (unchanged)

No pirate receiver stations, pirate cargo delivery economy, dynamic fences,
cargo insurance, rightful-owner reclamation, clean-cargo restoration, victim
identity tracking, theft chain of custody, crime history, warrants, Police
response redesign, additional pirate factions, demand frequency/notoriety,
credit ransom, boarding, ship capture, mission system, or new loot engine were
added.

## Outcome

**Outcome A.** All cargo successfully taken from the player through the Phase 81
interaction enters the existing pirate haul with stolen provenance, clean-origin
cargo is no longer silently lost from haul recovery, destruction uses the
existing physical loot authority, accounting remains exact, and validation
passes.
