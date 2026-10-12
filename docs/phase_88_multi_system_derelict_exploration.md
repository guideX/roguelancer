# Phase 88 — Multi-System Derelict Exploration

**Phase 88 proves that the existing derelict exploration system supports multiple independent star systems and sites without duplicating world, recovery, loot, or inventory authority.**

## 1. Duplicate-feature audit

Before adding the second site, the repository was searched for
`derelict|wreck|abandoned|salvage-site` across production code and world
definitions:

- The only curated, persistent, discoverable wreck authority is Phase 87's
  `DerelictCatalog` / `DerelictRecoveryService` / `DerelictWreckObject`
  trio, with exactly one entry (`derelict_ny_manhattan_drift`).
- `RoguelancerGame._wrecks` (`Roguelancer/Wreck.cs`) is transient NPC-
  destruction debris with no proximity discovery, no finite cargo, and no
  durable anti-reloot guard — a different feature, not a second site.
- No California derelict, abandoned-ship, wreck-salvage, or persistent
  wreck identity existed anywhere in production world definitions.
- No second recovery ledger, loot pool, or exploration framework existed.

Conclusion: no second complete site existed (no Outcome C stop), and no
parallel authority needed removal. The correct move was a second
`DerelictCatalog` entry reusing all Phase 87 gameplay logic.

## 2. Phase 87 architecture reused (unchanged)

| Component | Phase 87 shape | Phase 88 change |
|---|---|---|
| `DerelictSite` / `DerelictCatalog` | Dictionary keyed by stable site id; `GetForSystem(systemIndex)` filters | **One new entry only** |
| `DerelictRecoveryService` | `HashSet<string>` of recovered ids; system gate in `GetPresentation`/`CanRecover`; edge-triggered `TryRecover` | None |
| `DerelictWreckObject : SpaceObject` | Plain world object, shared `MODELS/wreck`, never an `NpcShip` | None |
| `RoguelancerGame.RefreshDerelictWrecksForSystem` | Rebuilds system-local wrecks from `GetForSystem` | None |
| `RoguelancerGame.UpdateDerelictInteraction` / `DrawDerelictHUD` | Nearest-site resolution over system-local wrecks; `F` key | None |
| `LootManager.SpawnDerelictSalvage` | All-or-nothing physical pods; Clean provenance; rejects mission cargo | None |
| `SaveGameData.DerelictRecoveredSiteIds` | Additive `List<string>`, schema 13 | None |

No system-name conditionals were added to `RoguelancerGame`. Routing is
purely catalog-driven: `GetForSystem` partitions the two sites by
`SystemIndex`, and recovery is keyed by stable canonical id, so catalog
reordering cannot reassign recovery state (proven by test).

## 3. Second system selected: California

California (canonical id `california`, `SystemIndex = 2`) was verified
against live production configuration:

- Exists in `Configuration/systems/system_02_california.json`.
- Two-way jump topology proven from production `ConfigurationManager`:
  a New York → California hole and a California → New York hole both exist.
- 9 stations, 6 jump holes, 3 planets loaded from production config.
- **Zero trade lanes**: all six `Configuration/tradelanes/*.json` files
  declare `"system_index": 1`, so lane clearance holds by construction.

No new star system was created.

## 4. Curated site placement

- Site id: `derelict_ca_hesperia_verge` (nearest named landmark: Outpost Hesperia).
- System: California (`SystemIndex = 2`).
- Position: `(15000, 500, -18000)` — open north-eastern verge beyond Outpost Hesperia, inside the ±20000 ordinary-flight grid, off the Y=0 plane like Manhattan Drift.

Measured clearances from production geometry:

| Reference | Nearest | Distance |
|---|---|---|
| Stations (9 CA stations, live config) | Outpost Hesperia | **6,331.7 m** (next: Riverside 13,604.8 m, Sierra 14,008.9 m) |
| Docking volumes | Hesperia dock 500 m / radius 700 m | ~5 km of margin beyond body + dock range |
| Jump holes (6 CA holes, live config) | California → New York (arrival corridor) | **12,283.7 m** (next: Texas 20,115.7 m) |
| Planets (3 CA planets) | Mojave | **15,008.3 m** (LA 28,044.6 m, Minor 29,300.2 m) |
| Trade lanes | none in California | total clearance by construction |
| Spawn | NY→CA arrival corridor | 12,283.7 m (not at spawn) |
| Detection overlap | 6,000 m range vs 6,331.7 m station gap | no overlap with Hesperia docking |
| Other derelict | Manhattan Drift (other system) | 30,675.7 m and cross-system isolated |

Discovery stays intentional: ~12 km from the arrival corridor, ~6 km past
Hesperia along the natural exploration line, with a 6 km detection radius.

## 5. Salvage manifest

California carries a consumable/medical mix, economically distinct from
Manhattan Drift's industrial mix:

| Commodity (canonical id) | Qty | Volume |
|---|---|---|
| `food-rations` | 5 | 5 |
| `water` | 4 | 4 |
| `medical-supplies` | 3 | 3 |
| **Total** | **3 pods** | **12** |

All three exist in `CommodityCatalog`, are Clean, non-contraband,
non-mission cargo; quantities are positive and bounded (≤40); the manifest
fits the atomic-spawn rule (3 lines ≤ 6 offsets) and the starter
50-capacity hold (volume 12). No credits, no mission-bound items, no new
commodities. The two manifests are fully disjoint, so site identity
provably determines contents.

## 6. Discovery, interaction, and active-system routing

- Same ranges as Phase 87: detection 6,000 m / identification 3,000 m /
  investigation 600 m; same `F` key; same HUD strings.
- Discovery is system-local: in New York the California site presents
  `Hidden` with no HUD text and `CanRecover` fails with
  `derelict is in another system` (and symmetrically in California).
- `RoguelancerGame` resolves only the nearest wreck among the
  system-local `_derelictWrecks` (rebuilt per system by
  `RefreshDerelictWrecksForSystem`), so one nearby site yields one
  coherent HUD interaction. No cross-system sensor scanning.

## 7. Independent recovery state

One shared `DerelictRecoveryService` owns two independent bits keyed by
stable id. Verified matrix:

| NY | CA | Result |
|---|---|---|
| Unrecovered | Unrecovered | Both available |
| Recovered | Unrecovered | Only California available |
| Unrecovered | Recovered | Only New York available |
| Recovered | Recovered | Neither releases again |

Failed wrong-system or out-of-range attempts mutate neither bit and spawn
no pods. Travel NY→CA→NY and CA→NY→CA both preserve durable state;
`ResetTransient` on transitions clears only presentation, never recovery.

## 8. Physical loot semantics

Unchanged from Phase 87: each recovery releases one `CargoPod` per
salvage line through `LootManager.SpawnDerelictSalvage` (deterministic
±140 m offsets, capacity check, all-or-nothing staging). Shared global
limits (32 live pods, 120 s lifetime, 200 m pickup radius) are honored,
not raised. Normal pickup collects all 12 volume; capacity-11 holds take
11 and leave exactly 1 unit for retry; capacity-0 holds keep all 3 pods
until space frees. No teleportation into the hold; partial pickup follows
existing cargo semantics; repeated `F` cannot duplicate pods.

## 9. Save/load

Schema stays at **version 13**: the additive
`derelict_recovered_site_ids` list already supports multiple ids, holding
both (`["derelict_ca_hesperia_verge", "derelict_ny_manhattan_drift"]` in
deterministic sort order). Verified scenarios:

- A. Both unrecovered → both available after load.
- B. NY recovered only → NY depleted, CA available.
- C. CA recovered only → CA depleted, NY available.
- D. Both recovered → both depleted, no re-release.
- E. Partial pickup → outstanding pods round-trip via existing
  `CaptureCargoPods`/`RestoreCargoPods`; collected cargo stays
  player-owned via the existing cargo contract; neither site regenerates.
- F. Legacy saves (field missing or explicit null) load safely with both
  sites available and no cargo/credit loss.

No parallel recovery ledger was added.

## 10. Death and lost-pod semantics

Preserved from Phase 87: uncollected physical pods remain subject to the
existing `LootManager` lifecycle (they may be lost on death); the durable
recovered bit survives death, respawn, docking, and system transitions,
so a wreck can never be re-rolled for a second payout. No free-recovery
exception was added.

## 11. Performance bounds

Tiny fixed catalog (2 entries), `GetForSystem` filtering per transition
(not per frame), bounded proximity checks over ≤1 wreck per system, O(1)
`HashSet<string>` recovery lookup, O(salvage lines) recovery, no
per-frame catalog rebuild, no global world scan, no per-frame cargo
enumeration. No spatial index needed at this scale.

## 12. Test results

- **Phase 88 focused suite** (`Phase88MultiSystemDerelictSmokeTest`,
  `--phase88-smoke`): **66 passed, 0 failed**. Covers catalog/identity
  (8), placement (8, incl. live-config geometry proofs), discovery (8),
  salvage (11), recovery independence (8, incl. both travel orders),
  persistence (8, scenarios A–F), lifecycle/regressions (13), and
  architecture-specific routing/world-object proofs (2).
- **Phase 87 regression** (`--phase87-smoke`): **52 passed, 0 failed**.
  Three single-site assumptions were minimally updated for the curated
  pair (wrong-system gate now asserts the CA site is system's only
  entry; two `Sites.Count == 1` assertions became `== 2`); all New York
  behavior assertions are byte-identical.
- **Full matrix** (`--all-smoke`): **88 suites passed, 0 failed** (87
  pre-existing + 1 new). Includes Phase 57 (19), Phase 66 (5), Phases
  68–86, loot (8), combat salvage (13), equipment salvage (20), mission
  (9), freight (40), save (4), nav (7), traffic (9), trade-lane hardening
  (52) / disruption (66) / defense (29), piracy demand (61), police
  enforcement (30) / fugitive (65), docking (11).
- **Full non-incremental Debug build**: 137 warnings / 0 errors before
  and after — **zero new warnings** (no warning references the new or
  edited derelict files).

## 13. Files changed

- `DerelictSite.cs` — `SecondSiteId` const + California catalog entry; doc comment updated.
- `Phase87DiscoverableDerelictSmokeTest.cs` — 3 minimal two-site accommodation edits (no behavior change).
- `Phase88MultiSystemDerelictSmokeTest.cs` — new, 66 checks.
- `RoguelancerGame.cs` — `--phase88-smoke` flag, dispatch branch, `RunPhase88SmokeTest`, all-smoke registration.
- `docs/phase_88_multi_system_derelict_exploration.md` — this document.
- `.phase` — `last_completed=88`, `next_expected=89`.

## 14. Limitations

- Both wrecks share the display name "Abandoned spacecraft" and the
  `MODELS/wreck` asset; site identity is distinguished by
  signal location/system, not visuals (same as Phase 87's single-site
  presentation; no art pipeline exists for variants).
- Death still discards uncollected pods per existing loot lifecycle
  (documented consequence, not a defect).
- Only two curated sites exist; no procedural or third-site support was
  added (out of scope by design).

## 15. Recommended Phase 89

Player-facing discovery aid using existing authorities only: surface the
nearest *unrecovered* derelict hint through the existing nav/marker
presentation once its system has been visited, without changing detection
ranges, loot, recovery, or save schema. Alternatively, curate a third
site (Colorado is jump-connected to New York) to further qualify the
catalog — no infrastructure work required.

**Phase 88 extends finite derelict exploration across independent star systems using existing world, recovery, loot, and cargo authorities, without introducing a parallel exploration system.**
