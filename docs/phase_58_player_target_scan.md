# Phase 58 — Player Cargo Scanner & Target Intelligence

Phase 58 adds a read-only `I` scan for the currently selected NPC. It gives the
player a bounded information action without changing cargo, markets,
reputation, faction state, mission state, or target behavior.

## Runtime model

`PlayerTargetScanService` owns one transient scan at a time. A scan is valid
only for a live NPC in the shared traffic/mission target set, within 4,000 m,
and outside trade-lane transit. It completes after three seconds of continuous
validity. Target changes, range loss, destruction/despawn, invalid player
state, and trade-lane entry cancel the active scan. Completed results remain
last-known HUD data until the target changes or the world is reset.

Target identity uses the same stable NPC identity used by Phase 56 manifests.
The result is not serialized and never creates a second manifest or cargo
ledger.

## Cargo and intelligence policy

The scanner reads the authoritative Phase 56 trader manifest through a
read-only snapshot seam. Mission-owned targets are resolved by
`MissionWorldManager`: convoy-raid cargo exposes only the real allocated
commodity and remaining quantity, while escorts, bounty targets, and other
mission NPCs report no registered cargo unless their mission data explicitly
provides it. Non-trader NPCs do not receive fabricated cargo.

The HUD shows target name, faction, ship type, hull/shield percentages,
canonical commodity names, quantities, canonical contraband metadata, and
estimated base-price value. The estimate is informational only and does not
pretend to be a market quote or grant credits.

## Verification

The focused suite covers range, duration, cancellation, one-active-scan
enforcement, target identity, lane restrictions, read-only behavior, canonical
metadata/value output, rescan behavior, demand/scan manifest sharing, stolen
pod flow, destroyed targets, and reset behavior:

```text
[PLAYER TARGET SCAN SMOKE] RESULT: 23 passed, 0 failed
```

The `I` binding is documented in `docs/controls.md`.
