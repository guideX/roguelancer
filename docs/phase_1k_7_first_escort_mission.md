# Phase 1K.7 — First Live Escort Mission Completion Test

## 1. Date and checkout context

- Test date: 2026-08-02.
- Repository: `D:\dev\Roguelancer\Roguelancer`.
- Checkout: `main`, tracking `github/main`.
- Scope: normal live client, normal station job board, normal launch/follow/arrival flow, active save/load, completed save/load.
- Phase 1K.0 through Phase 1K.6 reports were read before testing. Phase 1K.6 was treated as accepted and complete.

## 2. Git status and preservation notes

Initial status before testing:

```text
## main...github/main
?? docs/phase_1k_6_first_bounty_mission.md
```

The uncommitted Phase 1K.6 report was pre-existing user work and was preserved. The live save was backed up outside the repository before testing:

`C:\Users\guideX\AppData\Local\Roguelancer\Saves\phase_1k_7_backups\player_save_before_1k_7_20260802_192951.json`

Additional outside-repository backups were made at acceptance, active route progress, and completion:

- `player_save_escort_active_acceptance.json`
- `player_save_escort_active_progress.json`
- `player_save_escort_completed.json`

No reset, clean, checkout-over, or unrelated-file overwrite was performed. No gameplay code was changed. No commit was created.

## 3. Baseline validation

The required baseline commands all exited 0:

| Command | Exact result |
|---|---|
| `dotnet build Roguelancer.sln --no-restore` | `Build succeeded. 0 Warning(s), 0 Error(s)` |
| `dotnet run --no-build --project Roguelancer.csproj -- --mission-smoke` | `MISSION SMOKE RESULT: 14 passed, 0 failed` |
| `dotnet run --no-build --project Roguelancer.csproj -- --nav-smoke` | `NAV SMOKE RESULT: 7 passed, 0 failed` |
| `dotnet run --no-build --project Roguelancer.csproj -- --traffic-smoke` | `TRAFFIC SMOKE RESULT: 9 passed, 0 failed` |
| `dotnet run --no-build --project Roguelancer.csproj -- --save-smoke` | `SAVE SMOKE RESULT: 4 passed, 0 failed` |
| `dotnet run --no-build --project Roguelancer.csproj -- --all-smoke` | `ALL SMOKE RESULT: 12 suites passed, 0 failed` |

The all-smoke run included missile smoke at `4 passed, 0 failed`. No separate missile smoke run was required during the live test because no natural combat occurred and no combat code changed.

Existing non-blocking runtime/content warnings remained present: missing planet model and texture assets, missing fallback station/wreck models for several stations, sun-effect load errors, malformed legacy tradelane configuration files 1 and 2, and invalid Warthog model index/model-path fallback warnings. These did not change the smoke outcomes or block the escort route.

## 4. Origin station and full contract details

The normal client loaded the existing completed-bounty save at 6,547 CR, then docked normally at Newark Station. The first board contained practical escort options; a normal board refresh was used to obtain a clearer Easy/Low Risk contract. No mission generation or save editing was used to force the selection.

Accepted contract:

| Field | Live value |
|---|---|
| Origin station | Newark Station, New York system |
| Mission ID | 128 |
| Type | Escort |
| Offered by/client faction | Neutral Civilians |
| Escort name/type | Research Vessel 128 / Research Vessel NPC |
| Escort faction | Neutral Civilians (`neutral_civilians`) |
| Destination | Rochester Base |
| Difficulty/risk | Easy / Low Risk |
| Reward | 1,336 CR |
| Time limit | None (`time_limit: 0`) |
| Contract text | `Escort Research Vessel to Rochester Base` |
| Active objective text | `[ESCORT] Protect [ESCORT] Research Vessel 128` |
| Credits before acceptance | 6,547 CR |
| Neutral Civilians reputation before acceptance | 0.24 |
| Player ship | Scimitar |
| Player starting hull/shields | 100% / 100% |
| Mounted weapons/equipment | Liberty Light Laser, Rogue Blaster, Basic Missile Launcher, Civilian Shield Generator, Light Thruster, Basic Scanner, Basic Countermeasure Dropper |

The destination was a real loaded station in the current New York gameplay area.

## 5. Escort spawn and identification observations

After acceptance, the station mission panel showed exactly one active escort objective and the expected Rochester Base destination. The acceptance save recorded one active mission with mission ID 128, target `Research Vessel`, destination `Rochester Base`, reward 1,336, and status `Active`.

After normal undocking, the escort was visible near the origin area with a visible ship model, green friendly marker, and `[ESCORT] Research Vessel 128` label. It was a real `NpcShip`, friendly/non-hostile to the player, and showed 100% hull and 100% shields. It was not marked as a bounty target or enemy. The escort began moving immediately rather than waiting for the player.

The mission HUD resolved the escort objective and the destination. Normal F5 mission targeting selected the escort and exposed mission, faction, standing, hull, shields, and distance data. No duplicate mission escort was observed at acceptance, during the route, after active load, at completion, or after completed load.

## 6. HUD, targeting, and reacquisition results

Live-client observations:

- The objective panel identified the escort and Rochester Base and displayed distance information.
- The escort’s green world marker and target bracket made it visually identifiable when it was in range/view.
- F5 successfully selected the mission escort and set the target HUD to the correct friendly NPC.
- F7 did not initially provide a clear rebinding while a prior station target remained selected; the practical recovery was F5, followed by G to set GOTO to the escort.
- GOTO sometimes stopped near dense station, tradelane, or traffic geometry. The player had to reapply F5/G several times while following.
- The escort was never intentionally abandoned for an excessive period. It became approximately 10–14 km ahead during some GOTO interruptions, but it remained reacquirable through the mission target control and marker.
- The HUD answered which ship was being protected, its destination, approximate distance, and whether it remained alive. No live under-attack state occurred, so that feedback was not exercised.

The targeting behavior resembles the accepted Phase 1K.6 bounty target-acquisition/aiming UX concern and the accepted F5/GOTO tradelane synchronization concern. These were confusing but non-blocking and were not changed in this phase.

## 7. Route and pacing observations

The escort followed normal NPC/trader-route behavior through New York traffic and tradelane reference areas. No special formation behavior was observed. It started moving immediately, did not wait for the player, and required active follow-up to remain nearby.

The saved mission `elapsed_time` at completion was `610.632` seconds, approximately 10.18 minutes from acceptance through arrival. This includes normal station handling, target reacquisition, and repeated GOTO reapplication; a separate stopwatch for pure escort movement time was not taken.

The escort remained alive and did not visibly collide with the player, station, rings, or traffic. It did not become stuck or circle indefinitely. The route was practical but somewhat tedious because the player had to recover target/GOTO state around dense navigation objects. The escort continued route movement without waiting for the player. Whether it would complete safely after a prolonged player absence was not tested.

## 8. Natural combat observations

No natural hostile encounter occurred during the successful run. The escort remained at 100% hull and 100% shields, and no attacker, under-attack message, escort evasive behavior, escort return fire, or player damage was observed.

Therefore, combat protection was not exercised live. This run functioned primarily as a follow-the-transport mission. Automated coverage still passed the escort-destruction failure assertion and missile smoke remained green.

## 9. Active save/load checkpoint

The active checkpoint was taken after the escort had been identified, was moving, had made visible route progress, and remained well short of Rochester Base.

Before the active quicksave:

- Player position: approximately `{"x":19140.436,"y":5108.3296,"z":19383.615}`.
- Escort distance: approximately 21.9 km.
- Destination distance: approximately 43.2 km.
- Escort hull/shields: 100% / 100%.
- Engagement state: not engaged; no combat.
- Mission: active, mission ID 128.
- Credits/reputation: 6,547 CR / Neutral Civilians 0.24.
- Matching escort count: exactly one observed live.

The client quicksaved normally, a visible temporary station-target/GOTO change was made, and the client quickloaded normally. After loading:

- Mission 128 remained active.
- One logical escort was reacquirable and remained friendly.
- The escort remained bound to Research Vessel 128 and Rochester Base.
- The reward remained 1,336 CR.
- No reward or reputation was awarded prematurely.
- No duplicate escort appeared.
- F5 followed by G restored a usable escort target/GOTO route.
- The mission continued normally and completed later.

## 10. Exact versus logical escort continuity

The active save payload persisted the logical mission contract, including mission ID, target name, destination, reward, status, and elapsed time. It did not contain the escort NPC’s runtime position, velocity, hull, shields, or object identity.

Continuity classification: **logical mission reconstruction / coherent route continuation**.

Exact in-memory object identity and exact runtime position continuity were not proven. A duplicate spawn, corrupt state, failed mission, or uncompletable mission was not observed. The loaded state had exactly one logical escort, the correct destination, a coherent route, and a successful single completion. This is acceptable for the bounded Phase 1K.7 parity objective, with the persistence-fidelity gap recorded below.

## 11. Route-progress and damage persistence

Exact escort route progress and runtime position persistence were not directly observable from the save payload. No escort damage had occurred before the checkpoint, so the post-load 100% hull/shields observation cannot prove damage-state persistence. No exact position, damage, encounter state, or runtime object identity persistence is claimed.

The escort resumed valid route behavior after the load and remained completable. Whether its route resumed at the exact prior point or was reconstructed/restarted is unproven. GOTO state also required practical rebinding after load.

## 12. Destination arrival behavior

The escort reached the Rochester Base vicinity and completion triggered while the player was still in free flight. The player did not need to dock. The escort was observed approaching the destination area; it did not produce a clearly observable docking animation before the mission completed. The exact arrival radius was not directly measured.

The escort was still visibly selected immediately after completion, but the active objective panel disappeared/collapsed to a generic mission-objective state. The persisted save state was definitive: mission 128 changed from `Active` to `Completed`, `objective_complete` became true, active mission count became zero, and the completed mission count increased by exactly one.

Completion occurred exactly once. No duplicate completion, stale active mission in the saved state, or second award was observed.

## 13–15. Credits, reward, and reputation

| Measure | Before completion | After completion | Delta |
|---|---:|---:|---:|
| Credits | 6,547 CR | 7,883 CR | +1,336 CR |
| Neutral Civilians reputation | 0.24 | 0.36 (0.35999998 in save) | +0.12 |

Expected reward: 1,336 CR. Actual reward: 1,336 CR. The credit delta exactly matched the stated contract reward with no unexplained modifier.

Expected direct Neutral Civilians reputation change: +0.12. Actual change: +0.12. The final saved faction standings also showed the expected faction-reputation state without a duplicate escort completion or second reputation application.

## 16. Completed save/load checkpoint

Immediately after completion, the client quicksaved and the save was backed up outside the repository. A visible station-target/GOTO change was made, followed by a normal quickload.

After completed load:

- Mission 128 remained completed, not active and not failed.
- Credits remained 7,883 CR.
- Neutral Civilians reputation remained 0.35999998.
- Active mission count remained zero.
- Completed mission IDs were exactly `[67, 115, 128]`.
- No mission escort respawned.
- The old escort did not restart its route as an active objective.
- F5 did not resolve a nonexistent active escort; the normal station target remained selectable.
- No stale active escort marker remained after reload.
- Remaining briefly in/revisiting the Rochester Base area produced no second award or completion.

## 17. Duplicate escort/reward/reputation checks

No duplicate escort was observed at acceptance, after active load, during arrival, after completion, or after completed load. Mission 128 appeared exactly once in the completed mission list. The reward was paid once: 6,547 CR to 7,883 CR. Neutral Civilians reputation changed once: 0.24 to 0.36. A post-load/post-arrival save remained at 7,883 CR with zero active missions and one completed mission 128.

## 18. Automated failure proof versus live failure testing

Automated proof: `--mission-smoke` passed `escort failure on destroy` as part of 14/14 mission tests. The all-smoke run also passed the same mission suite.

Live destructive failure test: **not attempted**. The successful active and completed saves were preserved, and the primary successful escort route was not compromised. Escort-destruction failure is therefore automated-only and live-client untested in Phase 1K.7.

## 19. Blocking bugs

None found. The escort contract was accepted normally, bound to one friendly NPC, followed to a real station, completed once, paid the correct reward, applied the correct reputation change, survived active save/load, and remained completed after completed save/load.

## 20. Confusing UX

- F7 did not initially make the escort target state obvious while the prior station target was still selected.
- GOTO could stop near stations, tradelane geometry, and traffic, requiring repeated F5/G reapplication.
- Dense traffic/tradelane labels reduced visual separation between the escort and ambient NPCs.
- The escort moved immediately after launch, so the player had to understand quickly that the mission was a follow mission rather than a wait-for-player mission.
- These concerns overlap the accepted bounty target-acquisition/aiming and F5/GOTO synchronization issues. They were non-blocking and remained out of scope.

## 21. Escort AI or pacing concerns

The escort’s normal trader-route movement was functional and safe but not especially escort-like: it did not wait for the player, use a visible formation, or communicate a preferred follow distance. The player’s presence mattered because the player had to follow and would be expected to respond to threats, but the absence of natural combat meant the protection loop was not exercised. No collision, route stall, or endless circling was observed.

## 22. Missing feedback

Completion feedback was weak in the live HUD. The active objective disappeared and the saved credits/reputation changed, but no strong escort-arrived/completed message, sound, or captured reward toast made the transition unambiguous. The selected escort HUD also remained stale in runtime immediately after completion, still displaying the escort as active until the completed quickload cleared it.

Under-attack, player-too-far, escort-fleeing, and escort-damaged feedback were not exercised because no natural hostile encounter occurred.

## 23. Persistence-fidelity gaps

- Exact escort object identity across a full reload was not proven.
- Escort position, velocity, route node, hull, shields, and encounter state were not stored in the observed save payload.
- Exact route progress and exact damage persistence therefore remain untested/unclaimed.
- GOTO/target state required normal rebind after active load.

These gaps did not cause duplication, exploitation, failure, or inability to complete the mission. They are fidelity concerns, not Phase 1K.7 blockers.

## 24. Nice-to-have improvements

- Add an explicit escort-arrived/completion toast, sound, and mission-panel transition.
- Clear or convert the selected escort HUD immediately when completion occurs.
- Make F7 target rebinding and mission GOTO state more explicit.
- Reduce marker/label clutter near traffic and tradelane objects.
- If future parity phases require stronger continuity, persist escort route progress and runtime damage state.
- Add a later live hostile-encounter run to exercise under-attack feedback and protection pacing.

No improvement was implemented in this phase.

## 25. Files changed and why

Repository change created by this phase:

- `docs/phase_1k_7_first_escort_mission.md` — this live validation report.

The pre-existing uncommitted `docs/phase_1k_6_first_bounty_mission.md` was preserved. No gameplay, navigation, HUD, AI, save-schema, or content files were changed. Save backups were stored outside the repository.

## 26. Final regression results

The required post-run regression gate passed:

| Command | Exact result |
|---|---|
| `dotnet build Roguelancer.sln --no-restore` | `Build succeeded. 0 Warning(s), 0 Error(s)` |
| `dotnet run --no-build --project Roguelancer.csproj -- --mission-smoke` | `MISSION SMOKE RESULT: 14 passed, 0 failed` |
| `dotnet run --no-build --project Roguelancer.csproj -- --nav-smoke` | `NAV SMOKE RESULT: 7 passed, 0 failed` |
| `dotnet run --no-build --project Roguelancer.csproj -- --traffic-smoke` | `TRAFFIC SMOKE RESULT: 9 passed, 0 failed` |
| `dotnet run --no-build --project Roguelancer.csproj -- --save-smoke` | `SAVE SMOKE RESULT: 4 passed, 0 failed` |
| `dotnet run --no-build --project Roguelancer.csproj -- --all-smoke` | `ALL SMOKE RESULT: 12 suites passed, 0 failed` |

The all-smoke suite counts were: save 4/4, market 7/7, missile 4/4, countermeasure 4/4, mine 5/5, contraband 6/6, traffic 9/9, loot 8/8, mission 14/14, nav 7/7, dock 9/9, and ship 4/4.

Final repository status after report creation is expected to contain only the preserved uncommitted Phase 1K.6 report and this new Phase 1K.7 report. No commit was created.

## 27. Recommended next parity phase

Phase 1K.8 should validate a second live escort or a safely backed-up live escort failure/hostile-encounter path, with emphasis on natural attacker behavior, under-attack feedback, escort damage, route resumption, and failure-state save/load. Keep the targeting/GOTO and completion-feedback concerns triaged as bounded UX work; do not expand into a general navigation, escort, or HUD redesign unless a future route is blocked or corrupted.

## Final summary

- Contract route: Newark Station → Rochester Base.
- Escort: Research Vessel 128, friendly Neutral Civilians `NpcShip`.
- Destination: Rochester Base.
- Reward/credit delta: 1,336 CR; 6,547 → 7,883 CR, paid exactly once.
- Reputation delta: Neutral Civilians 0.24 → 0.36, +0.12, applied exactly once.
- Route duration: approximately 610.632 seconds / 10.18 minutes from acceptance to completion.
- Natural combat: none; escort protection and under-attack feedback were not exercised live.
- Active save/load: passed; mission remained active, one logical escort remained bound, no duplicate or premature reward, and the route remained completable.
- Escort continuity: logical mission reconstruction/coherent route continuation; exact runtime position, damage, and object identity persistence unproven.
- Completed save/load: passed; completed mission remained completed with no active escort, duplicate reward, duplicate reputation, or stale active marker after reload.
- Escort-gameplay verdict: qualified pass / accepted for bounded Phase 1K.7. The normal escort loop completed successfully with non-blocking targeting/GOTO and completion-feedback UX concerns.
- Smoke counts: mission 14/14, nav 7/7, traffic 9/9, save 4/4, all-smoke 12/12 suites; all-smoke missile 4/4.
- Gameplay code changed: no.
- Report: `docs/phase_1k_7_first_escort_mission.md`.
- Commit created: no.
