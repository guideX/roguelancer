# Phase 1K.10 - First Deliberate Inter-System Round Trip

Date: 2026-08-08
Checkout: `main`, tracking `github/main`
Result: **Pass, qualified for minor navigation/visual UX limitations**

This report distinguishes live-client observation, automated proof, code inspection, inference, and untested behavior explicitly.

## 1. Date and checkout context

**Live observation / code inspection:** Testing was performed on 2026-08-08 from the `main` checkout at `D:\dev\Roguelancer\Roguelancer`. The checkout started clean and no commit was created.

## 2. Git status and preservation notes

**Automated proof:** Initial status was:

```text
## main...github/main
```

Existing Phase 1 reports were preserved. No reset, clean, discard, or checkout-over-local-work operation was used. The Phase 1K.9 Pirate Transport save was backed up before testing:

```text
C:\Users\guideX\AppData\Local\Roguelancer\Saves\phase_1k_10_backups\player_save_before_1k_10_20260808.json
SHA256 C7D1AD7DB0DD5FAFCC9AE4C39E70EC5E47A66CA1890662B35DA945F0110F3F90
```

The final Colorado save after the round trip was also backed up. The live save and that backup matched after the final regression runs:

```text
SHA256 B63490B5875D4D0285FD65D3B7DA92957D3823974E290066519C9E331E3DAAC3
```

External live logs and screenshots are under `C:\Users\guideX\AppData\Local\Roguelancer\Saves\phase_1k_10_backups`. They are outside the repository.

## 3. Phase 1K.9 starting-state verification

**Live observation / automated proof:** The preserved starting save contained the accepted Phase 1K.9 state:

- System index 3, Colorado.
- Credits: 627 CR.
- Current ship: Pirate Transport.
- Position: approximately `(25872.5, 1571.1, 8587.8)`.
- Cargo: Construction Materials x1, 3 volume.
- Cargo capacity: 200.
- Active missions: 0.
- Completed missions: 14.
- Liberty Pulse Cannon owned once and mounted on `PrimaryGunLeft`.

## 4. Original market-smoke reproduction

**Automated proof:** Running the requested build and smoke commands from the legitimate Colorado save reproduced the defect:

```text
[MARKET SMOKE] FAIL Newark Station: station 'Newark Station' was not found
[MARKET SMOKE] FAIL Rochester Base: station 'Rochester Base' was not found
[MARKET SMOKE] FAIL Buffalo Base: station 'Buffalo Base' was not found
[MARKET SMOKE] FAIL Fort Bush: station 'Fort Bush' was not found
[MARKET SMOKE] FAIL Detroit Munitions: station 'Detroit Munitions' was not found
[MARKET SMOKE] FAIL fallback station: fallback station 'Trenton Outpost' was not found
[MARKET SMOKE] FAIL early route balance: one or more early-route stations were not found
[MARKET SMOKE] RESULT: 0 passed, 7 failed
```

## 5. Root cause of 0/7 Colorado result

**Code inspection plus automated proof:** The live Colorado world loaded seven Colorado stations. `MarketSmokeTest` was resolving its New York assertions against the live-system station collection, so the New York fixtures were absent by design. The assertions themselves were meaningful; the locality assumption in the harness was wrong. No commodity price, station market, player credit, cargo, or ship state was changed to reproduce or repair this.

## 6. Exact market-smoke harness change

**Code inspection:** `MarketSmokeTest` now ignores the live station collection for fixture resolution and calls `LoadFixtureStations()` in its constructor. That helper:

- Scans `Configuration/stations/station_*.json`.
- Deserializes `StationConfig` values.
- Filters to the configured New York assertion stations plus `Trenton Outpost` for the fallback assertion.
- Constructs lightweight `Station(config, null)` instances.
- Leaves all existing market assertions unchanged.

This is a narrow harness-only fixture change. It does not mutate production station markets or player state.

## 7. New York market-smoke result

**Automated proof:** With the preserved New York save temporarily placed in the live-save slot, standalone market smoke passed:

```text
[MARKET SMOKE] RESULT: 7 passed, 0 failed
```

The New York save was restored byte-for-byte afterward.

## 8. Colorado market-smoke result

**Automated proof:** From the Colorado save, market smoke passed twice consecutively:

```text
[MARKET SMOKE] RESULT: 7 passed, 0 failed
[MARKET SMOKE] RESULT: 7 passed, 0 failed
```

The Colorado save hash stayed `C7D1AD7DB0DD5FAFCC9AE4C39E70EC5E47A66CA1890662B35DA945F0110F3F90` during those runs. Credits, cargo, system index, and ship remained unchanged.

## 9. Colorado starting world inventory

**Live observation / automated proof:** Colorado loaded seven stations:

- Battleship Rio Grande
- Pueblo Station
- Ouray Base
- Cheyenne Asteroid Field
- Silverton Asteroid Field
- Copperton Asteroid Field
- Mimosa Asteroid Field

It loaded four jump holes:

- Jump Hole to Galileo
- Jump Hole to Kepler
- Jump Hole to New York
- Sea of Shadows Jump Hole

The initial runtime world contained 12 NPC ships and three configured planets. The Colorado map had no active New York stations or New York jump holes after the system-load fix.

## 10. Starting ship, cargo, loadout, and reputation

**Live observation / automated proof:** The starting Pirate Transport runtime values were:

```text
Speed 180 | Hull 250 | Shields 100 | Energy 300 | Cargo 200
Credits 627 | Cargo 3/200
```

Cargo was Construction Materials x1. Owned equipment was exactly one each of:

```text
liberty_light_laser
rogue_blaster
basic_missile_launcher
civilian_shield_generator
light_thruster
basic_scanner
basic_countermeasure_dropper
liberty_pulse_cannon
```

Mounts were:

```text
PrimaryGunLeft       Liberty Pulse Cannon
PrimaryGunRight      Rogue Blaster
MissileRack          Basic Missile Launcher
CountermeasureRack   Basic Countermeasure Dropper
ShieldGenerator      Civilian Shield Generator
Thruster             Light Thruster
Scanner              Basic Scanner
```

Reputation was unchanged from Phase 1K.9: Liberty Police approximately `0.594`, Liberty Navy `0.846`, Liberty Rogues `-0.770`, Liberty Corporations `0.540`, Bounty Hunters `0.522`, Junkers `0.036`, and Neutral Civilians `0.738`.

## 11. Colorado station sanity check

**Live observation:** Cheyenne Asteroid Field was targeted with the normal station-target flow, reached with GOTO/dock assist, and docked normally. The station UI showed:

- Credits 627 CR.
- Cargo 3/200.
- Pirate Transport.
- Speed 180, hull 250, shields 100, cargo 200.
- Equipment dealer available.
- Commodity dealer available through the fallback market.
- Job board available.

The equipment view showed Liberty Pulse Cannon owned once and mounted once on `PrimaryGunLeft`. The Colorado job board listed Colorado-local destinations. No transaction was made.

**Inference:** The asteroid-field marker provided usable station services, although the later Pueblo Station check was the stronger ordinary-base validation.

## 12. Colorado map and jump-hole discoverability

**Live observation:** The normal system map title was `SYSTEM MAP - Colorado System`. The visible map contained a purple `Jump Hole to New York` marker and label, alongside the other Colorado jump holes and stations. The marker was targetable by ordinary map click. The route was understandable without injecting a developer target.

The map labels are crowded around the system center, but the New York connection was identifiable by name and position.

## 13. Colorado to New York approach

**Live observation:** The map marker was selected normally at approximately 32 km from the saved starting position. After the GOTO fix, the route was a direct one-node route to `Jump Hole to New York`. Cruise activated for the long leg, then disengaged for the final approach. The Pirate Transport approached more slowly and deliberately than the Scimitar but remained controllable.

**Code inspection:** A selected jump hole is now treated as the concrete GOTO destination. The autopilot no longer inserts a nearer unrelated jump hole as a shortcut.

## 14. F4 prompt and interaction

**Live observation:** At approximately 162 m, the HUD displayed:

```text
Press F4 to enter Jump Hole to New York
```

The target panel simultaneously showed `Jump Hole to New York` and 162 m. F4 initiated the intended transit through normal player input.

## 15. Transit effect observations

**Live observation / automated proof:** The Colorado-to-New York log contained exactly one initiation and one completion:

```text
[JUMP] Transit effect started - Duration: 4s, Target: System 1
[JUMPHOLES] Transit initiated: Jump Hole to New York -> System 1
[JUMP] Transit effect completed
[JUMPHOLES] Transit complete! Switching to system 1
```

The client did not freeze. The four-second effect completed, and the player arrived in the destination system without being embedded in geometry or immediately reversing through the hole.

## 16. New York arrival position

**Live observation:** The destination-side arrival position was approximately `(48000, 600, -11400)`, matching the configured New York-side jump-hole arrival offset. The HUD showed `New York System`, the Pirate Transport model, and the expected mounted Liberty Pulse Cannon profile.

## 17. New York world replacement validation

**Live observation / automated proof:** New York loaded:

- New York system visuals, traffic, planets, and tradelanes.
- Nine configured New York stations in the active system world.
- Four New York jump holes: California, Colorado, Magellan, and Texas.
- 33 active NPC ships in the loaded New York traffic world.

The New York map title changed to `SYSTEM MAP - New York System`, with the New York station and jump-hole set visible.

## 18. Stale-object checks

**Live observation / code inspection:** Colorado station names and Colorado jump holes were not present in the active New York map or active station list. On return, the New York-only set was removed and the Colorado set was rebuilt. The system-change path clears and repopulates the runtime world collections; the added jump-hole reload makes the active jump-hole collection explicit during system replacement and save/load.

Traffic objects were replaced with the destination system's traffic population. No stale Colorado mission marker or GOTO route node was observed after New York arrival. No unloaded-object target error occurred.

## 19. Ship, cargo, and equipment continuity

**Live observation:** Across the Colorado-to-New York jump:

- Pirate Transport remained current.
- Runtime stats remained 180/250/100/300/200.
- Credits remained 627 CR.
- Cargo remained Construction Materials x1, 3/200.
- Liberty Pulse Cannon remained owned exactly once.
- Liberty Pulse Cannon remained mounted on `PrimaryGunLeft`.
- HUD still showed the Liberty Pulse Cannon / BlueDonut profile.
- Reputation and 14 completed missions remained present.

**Live empty-space confirmation:** After the completed round trip, a short right-click empty-space burst produced a `BlueDonut DUAL FIRED` log entry. No neutral traffic was attacked and no missile was launched. The final save was not overwritten by this confirmation; its hash remained the post-round-trip Colorado hash.

## 20. New York station loop

**Live observation:** Fort Bush was reached with normal targeting, GOTO, and dock assist. Docking produced normal market and equipment service events. The hangar/ship dealer view showed:

```text
Your Ship: Pirate Transport
Speed 180 | Hull 250 | Shields 100 | Cargo 200
```

The equipment dealer showed Liberty Pulse Cannon `Owned: 1`, `Mounted: 1`, and the current loadout showed it on `PrimaryGunLeft`. Fort Bush's configured commodity market was readable, and the job board worked without accepting a contract.

No Scimitar refund or duplicate trade-in path appeared in the current-ship display, and no purchase was made.

## 21. New York save/load checkpoint

**Live observation / automated proof:** A quicksave at Fort Bush produced a separate external backup with:

- System index 1.
- Credits 627 CR.
- Pirate Transport.
- Fort Bush position approximately `(6149.5, 602.7, -4497.7)`.
- Construction Materials x1.
- 14 completed missions and zero active missions.
- The complete seven-mount loadout.

The New York quicksave backup hash was `3068F517DC9A5DB45D937CF2BD9106B788F2548FF7F7D4AAE66D2C6C18273698`.

After undocking and changing the live state, F8 quickload restored New York. The reload log showed system 1, four New York jump holes, New York stations, restored loadout, restored reputation, and restored mission history. Flight remained usable after loading.

## 22. New York to Colorado return

**Live observation:** The New York map visibly labeled `Jump Hole to Colorado`. A normal map click selected it at approximately 42.6 km from the post-load Fort Bush position. GOTO built a direct route, cruise activated, and the same visible F4 prompt appeared at approximately 162 m:

```text
Press F4 to enter Jump Hole to Colorado
```

F4 initiated the second live transit. The return log contained one initiation and one completion for target system 3.

## 23. Colorado world restoration

**Live observation / automated proof:** After the return transit:

- The HUD showed `Colorado System`.
- The arrival position was approximately `(-2000, -200, -6400)`.
- Colorado's four jump holes were active.
- Colorado stations were active again.
- New York-only stations and jump holes were absent.
- The Pirate Transport, cargo, credits, and equipment HUD remained intact.
- No duplicate NPC/world set or stale New York target was observed.

## 24. Colorado station loop after return

**Live observation:** Normal Ctrl+F2 nearest-station targeting selected Pueblo Station at approximately 1.36 km. GOTO/dock assist reached and docked at Pueblo Station. The station UI showed Pirate Transport, 627 CR, 3/200 cargo, equipment service, commodity service, and job-board service.

This was the ordinary-base confirmation after completing both live jumps.

## 25. Colorado save/load checkpoint

**Live observation / automated proof:** A quicksave at Pueblo Station after the round trip stored:

- System index 3.
- Credits 627 CR.
- Pirate Transport.
- Position approximately `(-2873.9, -32.1, -5573.6)`.
- Construction Materials x1.
- 14 completed missions, zero active missions.
- The same seven mounted equipment entries.

The final Colorado save was backed up externally. After F8 quickload, the log showed system 3, four Colorado jump holes, Colorado stations, restored loadout, restored reputation, and restored mission history. A second normal nearest-station/dock-assist flow reopened Pueblo Station services after the load.

## 26. Job-board locality observations

**Live observation:** The Fort Bush job board offered New York-local destinations, including Buffalo Base and West Point Military Academy. The post-return Pueblo Station job board offered Colorado-local destinations, including Silverton Asteroid Field, Battleship Rio Grande, and Cheyenne Asteroid Field.

No impossible same-system destination pointing to an unloaded previous-system station was observed. No mission was accepted and no cross-system mission support was added.

## 27. Duplicate and stale world-state checks

**Automated proof / live observation:** Across both system changes and both save/load checkpoints:

- Transit initiation occurred once per deliberate jump.
- Transit completion occurred once per deliberate jump.
- The active jump-hole lists were rebuilt for the new system.
- Station lists were replaced with the current system's stations.
- NPC counts changed to the destination world's configured population.
- No duplicate equipment, cargo, credits, or player ship appeared.
- No stale New York objects remained in Colorado after return.
- No stale Colorado objects remained in New York after arrival.

**Code inspection:** `HandleSystemChange` clears and repopulates the runtime world collections. The Phase 1K.10 change explicitly reloads jump holes after configuration reload, preventing a save/load system switch from retaining the previous system's jump-hole collection.

## 28. Round-trip duration

**Inference from live wall-clock observation:** The bounded player-facing traversal, including the two long GOTO approaches, two four-second transit effects, Fort Bush and Pueblo station loops, and the requested save/load checkpoints, took approximately 8-10 minutes. The jump effects themselves were four seconds each; the majority of time was Pirate Transport approach travel and station docking.

## 29. Inter-system usability verdict

**Live observation:** A player can identify the Colorado-to-New York connection from the normal map, target it, use GOTO, see the F4 prompt, and understand the four-second transit. The New York map and world visibly differ, and the return connection is similarly labeled and targetable. Arrival positions are safe and station-finding works through the normal map/target/GOTO flow.

Verdict: **Pass.** The loop is usable through player-facing controls after the narrow GOTO correction.

## 30. Freelancer-feel verdict

**Inference:** The map-target-GOTO-prompt-transit sequence feels substantially closer to Freelancer-style system travel than a debug teleport. The four-second effect communicates the transition and the destination-side arrival is spatially sensible. The Pirate Transport's slower handling makes station approaches lengthy, but it does not break navigation.

Verdict: **Qualified positive.** The main remaining gap is presentation polish: dense labels, placeholder/missing models, and limited transition feedback rather than persistent-world corruption.

## 31. Blocking bugs

**Resolved during this phase:**

1. Save/load system changes could retain the previous system's jump-hole collection. Fixed by refreshing jump holes during `HandleSystemChange`.
2. Save/load restored the live ship but left the ship dealer's current-ship field showing Scimitar. Fixed by calling `SetCurrentShip(shipDefinition)` during save application.
3. GOTO to a player-selected jump hole could insert a nearer unrelated jump hole and transit to the wrong system. Fixed by routing selected jump-hole destinations directly.
4. GOTO stopped at approximately 500 m while jump-hole activation was 180 m. Fixed by using the existing jump-hole activation range as the selected jump-hole route's arrival radius.

After these fixes, no remaining Phase 1K.10 blocker was observed.

## 32. Confusing UX

**Live observation:** The original GOTO shortcut behavior was materially confusing and unsafe because it chose Sea of Shadows when New York was selected; it was fixed before the accepted round trip. Station-target cycling and the crowded system map can make target selection less immediate. Long Pirate Transport approaches also make it easy to wonder whether GOTO is still progressing.

## 33. Missing feedback

**Live observation:** The transit effect is understandable, but the route approach could communicate the final interaction distance more prominently. The system map labels overlap heavily in dense regions. Station and NPC model fallback messages are noisy in the debug console and do not always translate into clear player-facing feedback.

## 34. Nice-to-have improvements

**Inference:** Future parity work could consider:

- Less-overlapping map labels and clearer jump-hole emphasis.
- A more visible GOTO final-approach indicator.
- A concise system-arrival notification naming the destination and arrival hole.
- Replacing missing station/NPC models and reducing placeholder geometry.
- Smoother heavy-transport station approach pacing.

These are not required for the Phase 1K.10 round-trip pass.

## 35. Files changed and why

**Code inspection:**

- `MarketSmokeTest.cs`: self-contained station fixtures for deterministic market smoke.
- `RoguelancerGame.cs`: refresh active jump holes on system replacement and synchronize the ship dealer's current ship during save application.
- `GotoAutopilot.cs`: preserve a player-selected jump-hole destination and use its configured activation range for final approach.
- `docs/phase_1k_10_first_inter_system_round_trip.md`: this report.

No commodity prices, station market configuration, player state, save format, mission architecture, station architecture, or unrelated gameplay behavior was changed.

## 36. Final regression counts

**Automated proof:** Final commands completed with exit code 0:

```text
dotnet build Roguelancer.sln --no-restore       Build succeeded, 0 warnings, 0 errors
--market-smoke                                 7 passed, 0 failed
--ship-smoke                                   4 passed, 0 failed
--save-smoke                                   4 passed, 0 failed
--mission-smoke                                14 passed, 0 failed
--nav-smoke                                    7 passed, 0 failed
--traffic-smoke                                9 passed, 0 failed
--all-smoke                                    12 suites passed, 0 failed
```

The all-smoke run also reported Contraband 6/6, Traffic 9/9, Loot 8/8, Mission 14/14, Navigation 7/7, Dock 9/9, and Ship 4/4. Missile smoke was not run because the only new weapon use was the permitted single empty-space Pulse Cannon confirmation; no missile or combat interaction was used.

## 37. Recommended next parity phase

**Inference:** Proceed to a focused Phase 1K.11 on player-facing navigation polish and system-map readability, using the now-working multi-system loop as the regression path. Keep the scope bounded: improve target/route feedback and label density only if the next phase needs it. Do not broaden into economy balancing, mission cross-system support, or general asset replacement without a separate parity objective.

## Concise result summary

- Market-smoke fix/result: self-contained station fixtures; 7/7 from New York and Colorado, twice consecutively from Colorado.
- Starting system: Colorado, system index 3.
- Starting ship: Pirate Transport.
- Colorado -> New York: normal map target, direct GOTO, visible F4, one successful transit.
- New York station result: Fort Bush services worked; Pirate Transport and loadout recognized.
- New York save/load result: New York restored correctly with stations, holes, cargo, ship, equipment, reputation, and missions.
- New York -> Colorado: normal map target, direct GOTO, visible F4, one successful return transit.
- Colorado post-return station result: Pueblo Station worked before and after final load.
- Colorado post-return save/load result: Colorado restored correctly.
- Cargo/loadout persistence: Construction Materials x1 and full mount set persisted; Pulse Cannon stayed on `PrimaryGunLeft`.
- Stale/duplicate world-state result: no stale system objects, duplicate player state, duplicate equipment, or cargo exploit observed.
- Round-trip usability verdict: pass, qualified for minor UX/presentation limitations.
- Freelancer-feel verdict: qualified positive; player-facing system travel is now coherent.
- Smoke-test counts: Market 7/7, Ship 4/4, Save 4/4, Mission 14/14, Nav 7/7, Traffic 9/9, All-smoke 12/12 suites.
- Gameplay code changed: yes, three minimal live blockers were repaired.
- Test-harness code changed: yes, market smoke now uses self-contained fixtures.
- Report path: `docs/phase_1k_10_first_inter_system_round_trip.md`.
- Commit created: no.
