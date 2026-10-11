# Phase 87 — Discoverable Derelicts and Wreck Salvage

**Phase 87 adds discoverable finite wreck salvage using existing world, loot, and cargo authorities without creating parallel mission, economy, or inventory systems.**

## 1. Duplicate-feature audit

Searched for `derelict|wreck|abandoned-ship|hidden-cache|discoverable-salvage` across the repo before editing:

- `Roguelancer/Wreck.cs` — a drifting visual for destroyed ships only. Created in
  `RoguelancerGame` on NPC destruction, held in a transient `_wrecks` list that is
  **cleared on every system change**, never persisted, never discoverable, and
  carries no cargo.
- `CombatSalvageService` + `LootManager` + `CargoPod` — NPC-destruction salvage,
  extortion/raider-haul pods, and tractor/pickup. No wreck-anchored release.
- No `DerelictInventory`, no persistent static wrecks, no proximity discovery of
  wrecks, no finite wreck cargo, no anti-rel... no repeated-looting guard for any
  world site.

Result for the four spec questions:

- **A. Persistent static wrecks in explorable space** — did not exist.
- **B. Proximity discovery of wrecks** — did not exist.
- **C. Finite player-recoverable wreck cargo** — did not exist.
- **D. Durable prevention of repeated looting** — did not exist for wrecks.

No complete production implementation existed, so Phase 87 was implemented by
extension, not duplication.

## 2. Existing world-object architecture

- `SpaceObject` (name/position/radius/optional model) is the targetable world
  registry (`_spaceObjects` in `RoguelancerGame`): stations, planets, sun,
  jump holes, trade-lane rings. Rendered with brackets, listed on the system
  map, resolvable by GOTO/targeting.
- `NpcShip : SpaceObject` adds AI, weapons, shields, trade-lane transit,
  missions, and Police behavior. Deliberately **not** reused for the wreck.
- `LootManager` owns all physical pods (`CargoPod.TryCreate`), the 32-live-object
  cap, spawn-availability checks, tractor/pickup, capacity/partial-pickup
  semantics, and `CaptureCargoPods`/`RestoreCargoPods` persistence.
- `SaveGameData` is at schema version 13. Additive optional fields (precedent:
  `AmbientPirateRaids` kept version 12 compatibility) need no version bump.
- Sensor scales: NPC scan 4000 m, pod notice 900 m, tractor 1200 m, pickup
  radius 200 m, station docking ~500–900 m, map extent 25000 m.

## 3. Selected star system and location

- **System:** New York (index 1), the established starting/early system.
- **Site:** `derelict_ny_manhattan_drift` at `(-14000, 500, -8000)`.
- **Rationale:** open western drift, verified against all six New York
  trade-lane segments (min ~15.1 km), all nine stations (min ~15.5 km, far
  outside docking ranges ≤ 900 m and the largest 3000 m station radius), the
  Manhattan spawn `(500, 200, -500)` (~16.3 km: not on spawn, reachable by
  normal flight/cruise), and all four jump holes (±36–48 km). One
  early candidate `(8000, 0, 9000)` was rejected during testing because it sat
  707 m from the Manhattan–Pittsburgh lane (the focused suite caught it).
- Additional curated wrecks later are new `DerelictCatalog` entries only.

## 4. Wreck visual representation

`DerelictWreckObject : SpaceObject` with `ModelPath = "MODELS/wreck"` — the same
wreck asset the game already loads for destruction visuals. It is never an
`NpcShip`, so it cannot pursue, fire, regenerate, enter trade lanes, dock,
take missions, or trigger Police scans. Missing-asset fallback keeps a null
model (brackets/targeting still work).

## 5. Detection and identification

Pure functions of distance in `DerelictRecoveryService.GetPresentation`:

| Distance | Presentation |
|---|---|
| > 6000 m | Hidden (ordinary distant object, no announcement) |
| ≤ 6000 m | `Unidentified signal detected` |
| ≤ 3000 m | `Abandoned spacecraft` |
| ≤ 600 m, unrecovered | `Abandoned spacecraft - Press F to investigate` |
| ≤ 3000 m, recovered | `Abandoned spacecraft (salvaged)` |

Ranges sit above NPC-scan/pod scales but far below system scale, so discovery
requires travel without any global scanner subsystem or system-wide broadcast.
Edge-triggered notifications fire once per transition; the HUD line is a
live-only projection like the Phase 85/86 encounter panels.

## 6. Interaction mechanism

- **Key `F`** (`DerelictRecoveryService.InvestigateKey`), verified unused in
  flight (no `Keys.F` gameplay binding; `Enter`/`N` stay reserved for
  piracy/Police modals, weapons stay on `Q`/`E`/`C`). Edge-triggered in
  `UpdateDerelictInteraction`, mirroring existing `IsKeyDown && _prevKeys.IsKeyUp`
  conventions.
- Gates in `CanRecover`: correct system, live player, not docked, not in
  trade-lane transit, within 600 m, site unrecovered, no reentrant recovery.
- A same-frame reentrancy guard plus the durable recovered set make holding the
  key or double-dispatching idempotent.

## 7. Canonical salvage contents

Deterministic, from the real catalog (all Clean, non-contraband):

- `boron` × 6, `consumer-goods` × 4, `construction-materials` × 2
- Total volume 16, inside the starter 50-capacity hold; no fabricated item IDs;
  no rerolling; no `DerelictInventory`.

## 8. Physical loot authority

`LootManager.SpawnDerelictSalvage` stages one `CargoPod` per salvage line at
deterministic offsets around the wreck through the existing `CargoPod.TryCreate`
path, honoring the 32-live-object cap and spawn-availability checks. Nothing is
teleported into the hold; collection uses normal tractor/pickup with capacity
and partial-pickup semantics. Provenance is always `Clean` (`MissionId = 0`, not
stolen, not contraband, never mission-reserved).

## 9. Recovered-state representation

`DerelictRecoveryService` holds a `HashSet<string>` of stable site IDs —
O(1) lookup, O(salvage lines) work only during actual recovery, no per-frame
inventory rebuild, per-site evaluation only (one site in scope).

## 10. Save/load commit semantics

- **Commit point:** the site is marked recovered **only after**
  `SpawnDerelictSalvage` reports full success for every line. Spawn failure
  (saturated live cap or blocked coordinates) is all-or-nothing — no pod is
  added and the site stays unrecovered for a later retry.
- **New field:** `SaveGameData.DerelictRecoveredSiteIds` (`derelict_recovered_site_ids`),
  additive; **schema stays 13**. Legacy files (field absent/null) restore as
  fully unrecovered with cargo and missions untouched.
- **Ordering:** outstanding derelict pods persist through the existing
  `PhysicalCargoPods` capture/restore seam, so marking recovered at spawn time
  is safe: pods survive reload under normal loot rules.
- Scenarios proven in the suite: A (undiscovered survives), B (discovered,
  unrecovered, no duplicates), C (released pods persist without re-release),
  D (collected cargo kept, wreck depleted), E (legacy loads cleanly).

## 11. Lifecycle

Death clears transient pods per existing loot semantics but the durable
recovery bit prevents re-release (no duplication on respawn). Dock/undock,
system transitions (`ResetTransient` only), save/load, partial pickup, full
hold, and consecutive-frame interaction all preserve exactly-once release with
no stale references or stuck prompts.

## 12. Gameplay preservation

Environmental salvage only: no touch on trade-lane traffic, disruption/defense
missions, Police scans/enforcement/fugitive state, smuggling, piracy demands,
dealers, black-market pricing, mission reservations, NPC equipment salvage, or
destruction attribution. The suite asserts unchanged keys, catalog entries,
salvage determinism, extortion provenance, and (via reflection) that the
recovery service holds no mission/lane/Police/economy state.

## 13. Performance

One curated site, system-local objects, per-site distance checks only,
`HashSet<string>` recovery lookup, spawn work bounded by 3 salvage lines. No
global scan, no per-frame rebuild.

## 14. Test results

- **Focused suite** `Phase87DiscoverableDerelictSmokeTest` (`--phase87-smoke`):
  **57 passed, 0 failed**, covering all 52 spec gates plus architecture cases
  (world-object-not-NPC, key documentation, HUD conventions, O(1) lookup,
  commit point) and embedded green runs of loot, Phase 57, and combat-salvage
  suites.
- **Regression:** `--all-smoke` → **87 suites passed, 0 failed** (86 existing +
  Phase 87), covering Phases 57, 66, 68–86, cargo/loot/salvage, freight,
  missions, trading/black-market, navigation/traffic/lanes, targeting, piracy,
  faction combat, Police enforcement/fugitive, save/load, and UI/HUD.
- **Build:** `dotnet build -c Debug` → **0 errors, 128 warnings** — identical to
  the pre-change baseline (128/0).

## 15. Known limitations

- One site only, by design (vertical slice, not a wreck field).
- Death discards uncollected derelict pods like all transient loot; the player
  keeps the depleted-site state but must accept the lost pods (existing loot
  semantics, documented in-game behavior unchanged).
- No boarding/capture/repair/towing (explicit scope exclusions).

## 16. Recommended Phase 88

Add a second curated site in another accessible system (e.g., California) via a
catalog-only entry plus placement/geometry cases, exercising multi-site
`GetForSystem` routing and cross-system recovery independence without new
gameplay systems.
