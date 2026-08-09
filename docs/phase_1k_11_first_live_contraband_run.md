# Phase 1K.11 - First Live Contraband Run and Police Scan

Date: 2026-08-09  
Checkout used: `C:\dev\Roguelancer`  
Requested checkout: `D:\dev\Roguelancer\Roguelancer` (not present in this environment)  
Result: **Blocked; automated proof passes, live-client proof not established**  
Commit created: **No**

This report distinguishes live-client observation, automated proof, code/config inspection, inference, and untested behavior. No gameplay code was changed in this phase.

## 1. Git status and preservation

Initial status contained only the pre-existing setup change:

```text
 M .gitignore
```

The Phase 1K.10 report and its gameplay/test-harness changes were preserved. No reset, clean, discard, restore, or commit was performed. The requested D: checkout could not be opened; the available C: checkout was used instead. No successful Phase 1K.10 save was found under `C:\Users\guidex\AppData\Local\Roguelancer`, so no destructive scan-test backup could be made.

## 2. Phase 1K.10 fix preservation

**Code inspection:** The four Phase 1K.10 changes described by the accepted report remain present:

- `RoguelancerGame.cs`: jump-hole refresh during system replacement and ship-dealer current-ship synchronization during save application.
- `GotoAutopilot.cs`: preserve selected jump-hole destinations and use their configured activation range.
- `MarketSmokeTest.cs`: self-contained station fixtures for cross-system market assertions.
- `docs/phase_1k_10_first_inter_system_round_trip.md`: preserved unchanged.

No regression was demonstrated against those changes.

## 3. Baseline validation

The normal client build initially could not run because this checkout lacks the ignored `Content\Content.mgcb` and content assets. A temporary empty MGCB file was used only to compile/run the smoke harness, then removed. This is not a valid game-content build.

**Automated proof using the repo-local .NET 9 SDK:**

```text
dotnet build Roguelancer.csproj --no-restore: succeeded with 56 existing warnings, 0 errors (temporary empty MGCB only)
--contraband-smoke: 6 passed, 0 failed
--market-smoke: 7 passed, 0 failed
--traffic-smoke: 9 passed, 0 failed
--save-smoke: 4 passed, 0 failed
--all-smoke: 12 suites passed, 0 failed
```

The all-smoke run also reported the existing missile 4/4, countermeasure 4/4, mine 5/5, loot 8/8, mission 14/14, navigation 7/7, dock 9/9, and ship 4/4 suites. The requested `dotnet run --no-build` commands without the repo-local SDK initially failed because no client binary existed; after the temporary harness build they executed successfully.

## 4. Starting career state

**Live observation:** Not performed. No current Phase 1K.10 save was available in this environment.

**Historical reference only:** The accepted Phase 1K.10 report records Colorado / Pueblo Station, 627 CR, Pirate Transport, 200 cargo capacity, Construction Materials x1, 14 completed missions, Liberty Pulse Cannon mounted on `PrimaryGunLeft`, and the seven-item loadout. This was not treated as a current live save.

System, location, credits, cargo, ship, equipment, mounts, hull, shields, energy, reputations, and active missions are therefore **untested for the current checkout**.

## 5. Contraband configuration inspection

**Code/config inspection:** The catalog marks these commodities as contraband:

- Side Arms, id `side-arms`, volume 1, base price 1,500 CR.
- Alien Organisms, id `alien-organisms`, volume 5, base price 3,000 CR.

Both are shown with a `CONTRABAND` label in the station market UI before purchase (`StationDockUI.cs`). The purchase path is normal market trading; no spawn or save editing was used.

## 6. Selected route and expected economics

The current configuration identifies Buffalo Base -> Rochester Base as the intended contraband lane.

| Commodity | Source buy | Destination sell | Unit margin | Quantity planned | Investment | Expected profit |
|---|---:|---:|---:|---:|---:|---:|
| Side Arms | 1,050 CR | 1,280 CR | +230 CR | 1 | 1,050 CR | +230 CR |
| Alien Organisms | 2,300 CR | 2,700 CR | +400 CR | 1 | 2,300 CR | +400 CR |

**Inference:** Side Arms is the practical first run because it uses one cargo unit and has the lower entry cost. The historical 627 CR checkpoint could afford neither, so a current save with a higher balance is required for live acquisition. No market prices were changed.

## 7. Lawful traffic and scan path

**Code/config inspection:** Lawful scanners are restricted to `liberty_police` and `liberty_navy`. New York config contains 10 Liberty Navy patrol ships; Colorado contains 9 Liberty Navy patrol ships. The Fort Bush traffic zone contains 1-3 Liberty Police ships, with a 22-second spawn interval. Scan range is 2,800 units and cancellation range is 3,400 units.

The player-facing scan states are:

- `Police Scan: NN%`
- `Contraband detected - jettison or pay fine (N.Ns)`
- `Scan cleared`
- `Police hostile`

`J` is the normal jettison control. The implementation removes all contraband stacks, leaves legal cargo in place, and clears the detected state when no contraband remains. The configured fine is 1,500 CR. Paid fine reputation penalty is -0.08 to the scanning faction; fleeing after detection is -0.50; enforcement is -1.0.

## 8. Live clean scan

**Live observation:** Not performed. The available checkout lacks the real content pipeline/assets and no current career save was available. No police NPC was spawned for a primary proof.

**Automated proof:** Clean cargo scan passed in the contraband smoke suite without credit or reputation changes. This is harness evidence, not live-client evidence.

## 9. Live contraband acquisition and carriage

**Live observation:** Not performed. No current save could be loaded, and no live market transaction was made. No contraband, police, money, or scan state was injected.

Expected UI behavior is supported by code inspection: Side Arms and Alien Organisms should be visibly labeled `CONTRABAND` in the market listing before purchase.

## 10. Contraband save/load checkpoint

**Live observation:** Not performed because the successful Pirate Transport save was unavailable.

**Automated proof:** Save smoke passed 4/4, and the save schema serializes cargo by commodity id, credits, ship, mounted equipment, reputation, missions, and market state. The dedicated contraband smoke suite does not prove a real contraband save/load cycle; that remains untested.

## 11. Smuggling route and natural police encounters

**Live observation:** Not performed. No route duration, patrol count, natural scan count, avoidance behavior, or Pirate Transport handling risk was measured.

**Code/config inference:** Buffalo Base and Rochester Base are configured for the intended route, while lawful patrol traffic exists naturally in New York. This establishes availability in configuration, not actual encounter frequency in the live client.

## 12. Contraband detection and resolution

**Automated proof:** Contraband detection, the grace state, 1,500 CR fine/reputation consequence, all-contraband jettison, flee/escalation, and non-lawful scanner rejection passed 6/6.

**Live observation:** Detection, HUD clarity, scanner identification, clean scan, jettison, fine, and enforcement were all untested. No live credits, cargo, or reputation deltas exist to report.

## 13. Fine and reputation result

**Code inspection:** Fine amount is 1,500 CR. The automated fine case deducted exactly 1,500 CR and applied the configured police penalty plus the existing reputation ripple behavior.

**Live observation:** Not performed. The historical 627 CR balance is below the fine, and no money was injected or earned solely to force this test.

## 14. Scan save/load exploit check

**Live observation:** Not performed because no live contraband save or content-complete client was available.

**Code inspection:** Police scan state itself is not represented in `SaveGameData`; transient scanning/detection timers are therefore not persisted. Cargo, credits, reputation, and equipment are persisted. Whether reload gives a practical immunity window in the real world remains untested.

## 15. Optional flee/enforcement test

**Live observation:** Not performed. The destructive branch was not attempted.

**Automated proof:** Flee/cancel/escalation passed. **Code inspection:** The implementation changes scan state and reputation and displays `Police hostile`; this does not by itself prove that the actual NPC becomes hostile or attacks.

## 16. Player-experience evaluation

The following are **not live verdicts** because the client could not be exercised from the requested checkout:

- Illegal cargo label before purchase: supported by UI code; live clarity untested.
- Profit versus risk: route economics support a modest Side Arms margin; live risk/reward untested.
- Patrol availability: configured; live encounter rate untested.
- Scan start/progress/detection: explicit HUD strings exist; live readability untested.
- Scanner identity: log includes ship name/faction, but the player-facing target presentation is untested.
- Jettison discoverability: `J` binding exists, but live discoverability is untested.
- Fine and reputation feedback: notification strings exist; live clarity untested.
- Enforcement: automated state transition exists; actual hostile gameplay is untested.
- Freelancer-style smuggling tension: cannot be judged without live route and scan evidence.

## 17. Blocking issues

1. **Environment blocker:** `D:\dev\Roguelancer\Roguelancer` is absent; only `C:\dev\Roguelancer` is available.
2. **Repository/content blocker:** `Content\Content.mgcb` and the real MonoGame content tree are absent from the available checkout. A temporary empty MGCB can compile the harness but cannot support honest live gameplay.
3. **Save-state blocker:** No Phase 1K.10 Pirate Transport save exists in the available user's local save path, so it cannot be loaded or backed up safely.

No reproducible gameplay blocker was fixed. No gameplay files were changed.

## 18. Files changed

- `docs/phase_1k_11_first_live_contraband_run.md`: this report.
- No production gameplay code changed.
- The pre-existing `.gitignore` change remains untouched by this phase.

## 19. Final regression counts

```text
Contraband: 6/6
Market: 7/7
Traffic: 9/9
Save: 4/4
All-smoke: 12/12 suites
Build: 0 errors; 56 existing warnings, using temporary empty MGCB
```

## 20. Recommended next parity phase

Resume Phase 1K.11 from the real `D:\dev\Roguelancer\Roguelancer` checkout with its actual `Content` tree and the backed-up Phase 1K.10 Pirate Transport save. Do not treat this environment-blocked run as a qualified gameplay pass.

## Concise summary

- Starting system/location: unavailable; historical 1K.10 state was Colorado / Pueblo Station only.
- Contraband commodity: Side Arms selected by configuration; live quantity 0 tested.
- Quantity: untested; planned 1.
- Source -> destination: Buffalo Base -> Rochester Base.
- Purchase cost: 1,050 CR planned; live purchase not completed.
- Sale/profit: 1,280 CR / +230 CR planned; not completed.
- Clean scan result: automated pass; live untested.
- Contraband scan result: automated pass; live untested.
- Jettison result: automated pass; live untested.
- Fine result: automated 1,500 CR deduction; live untested.
- Reputation delta: automated paid-fine case passed configured penalty; live untested.
- Scan save/load/exploit: live untested; transient scan state is not in save schema by inspection.
- Enforcement result: automated state transition passed; actual hostile gameplay untested.
- Smuggling gameplay verdict: not established; blocked by checkout/assets/save availability.
- Freelancer-feel verdict: not established.
- Smoke-test counts: Contraband 6/6, Market 7/7, Traffic 9/9, Save 4/4, All-smoke 12/12.
- Gameplay code changed: no.
- Report path: `docs/phase_1k_11_first_live_contraband_run.md`.
- Commit created: no.
