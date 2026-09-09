# Phase 69 — Lawful Contraband Interdiction & Seizure

Phase 69 adds a bounded lawful-enforcement layer to the existing local NPC
combat and physical salvage systems. Liberty Police can now recognize a nearby
NPC with actual canonical contraband in its authoritative manifest, acquire it
through the ordinary faction target path, damage it with normal NPC weapons,
and pursue the resulting physical contraband pods for removal from circulation.

The production proof is a real Phase 68 Rogue smuggling carrier:

`Rogue smuggler → manifest-backed Police detection → normal NPC combat → destruction → physical contraband pods → proximate Police seizure`

## Architecture and ownership

- `Commodity.IsContraband` remains the canonical legality classification.
  Contraband enforcement never infers legality from faction, route, ship name,
  stolen provenance, or the mere presence of cargo.
- `RogueSmugglingManager` remains the Phase 68 shipment and manifest authority.
  It reserves criminal-market stock, exposes the live manifest snapshot, marks
  a carrier lost on destruction, and supplies the remaining manifest to the
  existing destruction salvage callback.
- `EconomicShipmentManager` remains the lawful trader-manifest authority. Its
  legal manifests are available to the same enforcement query, but ordinary
  legal cargo is not a violation.
- `TrafficManager.UpdateFactionCombatEngagements` remains the bounded local
  NPC target-acquisition owner. It uses the existing spatial cells and source
  activation range, then gives a manifest-backed contraband target the explicit
  `FactionCombatTargetOrigin.ContrabandEnforcement` origin. Existing hostile
  faction acquisition remains a separate fallback.
- `NpcShip` still owns encounter state, movement, target lifetime, damage, and
  destruction. The contraband validator is a transient callback supplied by
  `TrafficManager`; it is not save data.
- `NpcWeaponSystem` remains the only NPC projectile/damage path. Police do not
  receive scripted damage or instant-confiscation behavior.
- `LootManager` remains the only `CargoPod` authority. Its new seizure method
  removes an eligible physical pod from the same active list used by player
  pickup and marks transient seizure provenance on that pod before removal.
- Markets receive no seizure stock. There is no Police warehouse or hidden
  inventory.
- `SaveGameData` requires no new Phase 69 fields. Active Phase 68 shipments and
  physical pods already have authoritative save paths; transient target,
  pursuit, and seizure records are rebuilt or discarded at world teardown.

## Legality and enforcement policy

`ContrabandEnforcementPolicy` is read-only. It requires all of the following:

- the source is the configured lawful enforcement faction (`liberty_police`),
- source and target are distinct, live NPCs from different factions,
- the target is not in trade-lane transit,
- the target is within the source's existing activation/detection range, and
- the resolved live manifest contains at least one positive-quantity
  commodity whose canonical definition has `IsContraband == true`.

Stolen provenance is intentionally ignored by this detector. A legal commodity
marked `[STOLEN]` remains a separate Phase 57/67 fence-only concern. A cleanly
purchased contraband commodity remains contraband. A neutral/non-Rogue carrier
with a real contraband manifest is representable by the policy.

The explicit policy distinctions are:

- hostile faction target: authorized by `FactionRelationshipMatrix`;
- contraband violation: authorized by actual manifest legality;
- ordinary legal ship: not eligible for this detector;
- neutral ship without contraband: not eligible for this detector.

Police may still attack a Rogue because Police/Rogue faction hostility is
already hostile. Phase 69 proves the additional contraband origin separately;
it is not implemented as `target faction == Rogue`.

## Detection, targeting, and combat

Detection is local. The traffic manager first builds the existing 3D spatial
cell neighborhood, then the policy inspects at most 64 local candidates per
lawful source. Candidate selection is nearest-first with stable name and NPC
identity tie-breakers. The default source range is 6,500 world units, while a
configured NPC's existing `TrafficActivationRange` remains authoritative.

When a valid carrier is found, Police acquire it through
`SetFactionCombatTarget` with the explicit `ContrabandEnforcement` origin.
Movement continues through `NpcShip` engagement movement and weapon selection
continues through `NpcWeaponSystem`. If a manifest becomes empty or the target
leaves the bounded policy range, the target becomes invalid and ordinary
targeting/disengagement rules apply.

No second scanner, police AI, or combat system was added. Player scanner output
continues to resolve through the same Phase 68 manifest snapshot and therefore
reports the actual contraband without converting it into stolen provenance.

## Destruction and physical seizure

Destruction enters the existing Phase 68 loss path:

1. `RogueSmugglingManager.NotifyCarrierDestroyed` marks the shipment lost.
2. `LootManager.SpawnLootForDestroyedNpc` requests the remaining manifest from
   the existing economic salvage seam.
3. Each remaining contraband stack becomes a real physical `CargoPod`.
4. The normal Phase 68 finalizer consumes the manifest after the pods are made.
5. The destination criminal market is never credited.

Police do not teleport cargo out of a living ship. After destruction, a nearby
Police NPC selects one eligible physical contraband pod at a time, enters the
ordinary NPC movement state toward its position, and can seize it only inside
the 260-unit seizure radius. `LootManager.TrySeizeContrabandPodForNpc` checks
that the pod is still active, live, commodity cargo, and canonically
contraband; it drains the pod and removes it from the active pod list. The
seizer records only bounded transient diagnostic provenance: pod runtime
identity, commodity, quantity, Police identity, and faction.

Seizure is not a market transaction and does not award cargo or credits to
Police. It removes contraband from circulation exactly once.

## Player-versus-Police pickup race

The player and Police use the same active `CargoPod` object. The player pickup
path can run first, in which case the pod is removed into the player's
canonical `CargoHold` and Police find no active pod to seize. If Police reach
the pod first, the pod is removed and the player cannot receive a duplicate.
Partial player pickup is supported by the existing cargo-capacity path; any
remaining physical quantity remains eligible for later Police seizure.

## Commodity conservation

For a Phase 68 shipment with initial remaining quantity `N`:

```text
source criminal stock - N
carrier manifest = N
carrier destroyed = 0 carrier units
physical pods = N
fully seized pods = 0 physical units
destination criminal stock increase = 0
```

For a mixed recovery:

```text
player recovered + Police seized = destroyed carrier cargo
```

The shipment is terminally lost before salvage and cannot later settle at the
destination. `LootManager` removes each pod from one shared active list, and
the bounded seizure identity set prevents duplicate seizure accounting.

## Save/load

No Police target, pending pod pursuit, or seizure counter is persisted. These
are transient world-AI state. Phase 68 active shipment identity, route, and
manifest state continue to use `SaveRogueSmugglingShipmentData`. Physical pods
continue to use `SaveCargoPodData`.

Consequently:

- an active shipment still rebinds by its Phase 68 identity;
- a destroyed shipment is not saved as active and cannot settle later;
- a seized pod is absent from the pod save list and cannot be restored;
- an unseized pod remains an ordinary physical pod and can still be saved;
- no seizure-only market or Police inventory is reconstructed on load.

## Reset and teardown

`TrafficManager.ResetContrabandEnforcement` clears pending Police pod targets,
bounded seizure identities, and transient seizure diagnostics without touching
market stock, player cargo, or destination stock. Traffic world reload calls
this before existing Phase 68 reset/rebind behavior. Existing Phase 68 active
shipment rollback remains authoritative for world teardown: it restores
committed source stock according to the Phase 68 transient-world semantics and
does not deliver or award cargo.

## Bounds and determinism

- detection uses the existing local spatial-cell pass;
- at most 64 candidates are inspected for one enforcement source;
- at most 16 Police pod pursuits are tracked at once;
- physical pod candidates are capped by the existing 32 live salvage-object
  limit;
- candidate choice is nearest-first, then stable name/identity order;
- seizure records are bounded by the live salvage-object limit;
- all enforcement state is transient and stale NPC/pod references are removed
  when the object is destroyed, consumed, or the world is reset.

## Smoke coverage and regressions

`--phase69-smoke` runs one live configured TrafficManager proof followed by 16
focused checks covering canonical legality, nearby Phase 68 detection, the
Rogue-only negative, legal cargo, non-Rogue contraband, normal NPC weapon
damage, Phase 68 loss/destination semantics, exact physical drops, delayed
seizure, exactly-once removal, player pickup competition, full and mixed
conservation, save/load, and reset.

The suite explicitly keeps legal stolen cargo separate from contraband. It does
not implement player fines, surrender, bribery, arrest, jettison demands,
docking denial, evidence storage, prison gameplay, or a mission system; those
remain future player-crime phases.
