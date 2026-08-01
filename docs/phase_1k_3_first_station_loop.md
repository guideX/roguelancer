# Phase 1K.3 First Station Loop

Date: 2026-08-01

## Date / run context

- Continued from the Phase 1K.2 fresh-save retest. The live route to a station was already verified from a clean start: onboarding hint, `Ctrl+F2`, `F3`, dock assist, and docking.
- This pass reran the current build and the station-loop smoke coverage. No gameplay code was changed.
- The station reached was **Fort Bush** in the New York system.
- The available station UI path was checked in the current build; the remaining service, mission, trade, loadout, and persistence assertions were exercised by the in-process smoke suites.

## Validation commands

| Command | Result |
| --- | --- |
| `dotnet build Roguelancer.sln` | PASS - 0 errors; existing compiler/content warnings only |
| `dotnet run --no-build --project Roguelancer.csproj -- --dock-smoke` | PASS - 9 checks, 0 failures |
| `dotnet run --no-build --project Roguelancer.csproj -- --all-smoke` | PASS - 12 suites, 0 failures |

Relevant all-smoke results included:

- Market: 7 passed, including Fort Bush buy/sell and early legal-route balance.
- Mission: 14 passed, including bounty, delivery, escort, UI strings, HUD fallback text, and mission save/load.
- Save: 4 passed, including round-trip persistence and missing-content safety.
- Navigation/docking: 7 navigation checks and 9 dock checks passed.
- Ship/economy: 4 ship purchase and affordability checks passed.

## Station services tested

- **Hangar / launch:** reachable after docking; the hangar exposes `[Press U] UNDOCK`. The existing dock log records market and equipment station context being attached at Fort Bush.
- **Job board:** reachable through `[5] Jobs`; refresh and selection are safe. Bounty, delivery, and escort display data includes objective, target or destination, reward, risk, client, and faction.
- **Commodity dealer / market:** Fort Bush exposes 12 configured listings. The market smoke bought and sold one safe legal commodity and verified credits, cargo quantity, and stock changes.
- **Equipment dealer:** reachable from the dealer screen with `TAB`; the UI shows price, owned/mounted counts, compatible hardpoints, mounted hardpoints, and current loadout.
- **Ship dealer:** reachable through `[4] Ships`; the ship smoke suite passed purchase and affordability guards.
- **Active missions:** the docked station UI and in-flight HUD render an active-mission panel. Mission smoke passed active mission acceptance, objective/HUD text, and save/load state restoration.

## Mission accepted

- **PASS in mission smoke:** one each of bounty, delivery, and escort missions can be generated and accepted; mission world binding, rewards, objective text, and failure/completion paths passed.
- The job-board text is clear in the tested strings. Delivery uses destination text, bounty uses target text, and escort explicitly uses "Protect", destination, reward, risk, client, and faction.

## Cargo / equipment actions

- Fort Bush legal cargo is reachable with the 3,000 CR starting bankroll: water is 55 CR/unit, food rations are 85 CR/unit, and H-fuel is 220 CR/unit. The market smoke verified a one-unit legal buy reduces credits and increases cargo, then verified the sell path.
- Cargo capacity is 50 units on the starter ship, and cargo is included in save/load state. The save and mission suites passed round-trip persistence.
- Equipment prices are visible and bounded: Basic Mine Dropper is 2,600 CR, Basic Countermeasure Dropper is 1,800 CR, and Basic Scanner is 1,200 CR; larger upgrades begin at 3,200 CR and above. The catalog is reachable and the loadout display remains data-backed and readable. No equipment purchase was made in this pass; the 2,600 CR mine dropper is an affordable candidate, but no live equipment transaction trace was captured.

## What worked well

- The fresh-spawn docking flow now reliably gets the player to Fort Bush without station-cycle or GOTO ambiguity.
- Station services are wired into a compact, understandable loop: hangar, bar/jobs, market, equipment, ships, and job board.
- Market transactions give the expected credit/cargo/stock changes, and legal early-route balance remains positive without same-station arbitrage.
- Mission text is explicit about the information a new player needs, and unresolved targets/destinations fall back safely instead of producing blank HUD fields.
- Quicksave/quickload and mission/cargo/loadout persistence remain covered by green smoke tests.

## Issue list

### Blocking bug

- None found. The success path from fresh spawn to docked Fort Bush and station-loop state is green.

### Confusing UX

- The bottom navigation label `[3] Equipment` opens the commodity dealer first; equipment browsing requires `TAB`. Rename the button to "Market / Equipment" or split the entry in a later pass.

### HUD/UI spacing

- At the fixed 1920x1080 viewport, the market draws all 12 listings in a single vertical stack and the equipment catalog draws nine rows without paging. The lower entries are likely below the viewport even though keyboard selection wraps safely. Confirm visually and add paging/scrolling in a later UI pass if reproduced.

### Economy/balance issue

- No failing balance result. The 3,000 CR start supports a small legal cargo purchase, while new equipment upgrades are close enough to the starting bankroll to be meaningful. Recheck whether a 2,600 CR mine purchase leaving 400 CR is the intended first upgrade decision.

### Missing feedback

- No station-loop feedback failure was found in the smoke coverage. Keep the existing transaction notifications and "Game Saved" / "Game Loaded" notifications in the next manual pass.

### Nice-to-have

- Add one focused end-to-end station-loop smoke case that drives the exact UI sequence: dock, inspect each service, accept a mission, buy one legal unit with exactly 3,000 CR, buy or mount one affordable equipment item, undock, quicksave, mutate state, and quickload.
- Clean up unrelated startup noise seen during smoke runs (missing optional visual assets and malformed legacy tradelane config files) so station-loop failures stand out more clearly.

## Recommended next pass

1. Do a short focused live-client replay from clean save using the exact checklist above, with special attention to focus/input ownership after docking.
2. Confirm the market and equipment lower rows at 1920x1080 and decide whether paging is needed.
3. Rename or clarify the `[3] Equipment` navigation entry and make the `TAB` market/equipment relationship explicit.
4. After the live replay, keep `dotnet build`, `--dock-smoke`, and `--all-smoke` as the regression gate.
