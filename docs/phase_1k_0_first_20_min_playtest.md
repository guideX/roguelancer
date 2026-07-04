# Phase 1K.0 First-20-Minute Playtest

Date: 2026-07-03

Run context:
- Fresh launch with no existing save present in `%LocalAppData%\Roguelancer\Saves\player_save.json`
- Game build target: `dotnet build Roguelancer.sln`
- Smoke baseline: `dotnet run --no-build --project Roguelancer.csproj -- --all-smoke`
- Smoke status: 11 suites passed, 0 failed

## Playtest Route Taken

1. Started a new game from a clean save state.
2. Confirmed the starting bankroll is 3,000 CR.
3. Opened the system map and used station targeting/GOTO on the New York system.
4. Sampled station target cycling with `F2` and route activation with `G`.
5. Confirmed hostile-target selection with `F1` on a visible Warthog heavy fighter.
6. Tested quicksave with `F6`.
7. Let the ship drift far enough for a visible state change.
8. Tested quickload with `F8` and verified the ship snapped back to the saved position/state.

## What Worked Well

- Starting credits are exactly 3,000 CR as intended.
- The system map opens cleanly and the target info box is readable.
- Dockability feedback is visible on station targets, including faction, standing, dock range, and distance.
- Hostile target selection works and can find nearby enemy traffic.
- Quick save/load works and restores the ship position cleanly.
- The save action produces clear feedback, and the load action visibly returns the player to the saved state.

## Prioritized Issues

### Blocking Bug

- I could not complete the station-docking part of the first pass in a practical amount of time because station target cycling plus `GOTO` did not converge to a dockable approach in a way that was obvious to a new player.
- The route kept selecting/maintaining long tradelane-style approaches, and the straight-line distance to the chosen station often increased instead of decreasing.
- Result: I was not able to reach the job board, market, equipment dealer, or trade route checkout during this pass.

### Confusing UX

- Station target cycling is not self-explanatory.
- `F2` moved through station targets, but the order was not intuitive and did not communicate which station was actually closest or most appropriate for docking.
- The current `GOTO` destination is easy to lose track of because the target box, autopilot text, and distance readouts can all point at different things at once.

### Balance Issue

- Could not fully validate early-market pricing or trade profitability because I never reached a station screen.
- Based on the flight leg alone, the early travel path felt longer than a new player would expect for a first dock.

### Visual Polish

- The target/dock info panels are functional, but the world-space labels and route overlays become crowded quickly.
- When multiple station and traffic labels overlap, the player has to work hard to understand what is selected.

### Missing Feedback

- Gun and missile firing were hard to validate in-flight because the hostile target was not naturally centered in a clean firing lane.
- I did not get strong hit confirmation from the brief combat test.
- Docking progress or approach guidance felt too indirect while the autopilot was routing.

### Nice-to-Have

- A direct "nearest dockable station" shortcut would make the first dock much more discoverable.
- A small hint for the station cycle order, or a way to sort stations by distance, would reduce early friction.
- A clearer combat-ready state or target-lock cue would make the first firefight easier to understand.

## Recommended Next Pass

1. Focus on the dock loop first and confirm a new player can reliably reach a station from the opening spawn without guesswork.
2. Add or refine a "nearest station" or "dock now" path if the current target cycling remains too opaque.
3. Once station access is reliable, rerun the same first-20-minute pass and complete:
   - job board clarity
   - market/trade route
   - equipment browse/purchase
   - contraband scan/jettison
   - mission objective targeting
4. After that, do a second combat-focused pass aimed at a scripted or mission-driven hostile so gun/missile/countermeasure feedback can be judged in a clean fight.
