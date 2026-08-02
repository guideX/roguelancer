# Phase 1K.4 — First Legal Trade Route Completion Test

## Date and run context

Run date: 2026-08-01.

This was a clean-start manual client run in the New York System. No save file was present at the start. The run began with the Phase 1K.3 known-good fresh-start docking flow and reached Fort Bush.

The only gameplay-code change during the run was a tiny `GotoAutopilot.SteerToward` fallback for a nearly opposite heading. It snaps the ship to the desired heading when the current and desired vectors are almost 180 degrees apart. No new system was added.

## Validation commands

| Command | Result |
|---|---|
| `dotnet build Roguelancer.sln --no-restore` | PASS — 0 errors, 56 existing warnings |
| `dotnet run --no-build --project Roguelancer.csproj -- --dock-smoke` | PASS — 9/9 |
| `dotnet run --no-build --project Roguelancer.csproj -- --all-smoke` | PASS — 12 suites passed, 0 failed |

Manual validation used the normal client executable with the following flow: fresh start → `Ctrl+F2` nearest station → `F3` dock assist → `D3` dealer → `TAB` commodity market → buy/sell controls → map click and `G` targeting attempt → `F3` dock assist fallback → `F6` quicksave → visible station-area change → `F8` quickload.

## Route and transaction

- Origin station: Fort Bush
- Destination station: Newark Station
- System: New York System
- Commodity: Water
- Quantity: 1 unit

| Checkpoint | Credits | Cargo hold | Market observation |
|---|---:|---:|---|
| Before purchase | 3,000 CR | 0/50 | Fort Bush buy price: 55 CR/unit |
| After purchase | 2,945 CR | 1/50 Water | Fort Bush stock decreased from 500 to 499 |
| Before sale | 2,945 CR | 1/50 Water | Newark sell price: 75 CR/unit |
| After sale | 3,020 CR | 0/50 | Newark stock increased to 201 |

Profit/loss: **+20 CR** (`75 - 55`, one unit).

The transaction behaved correctly: credits decreased on purchase, cargo appeared in the hold, the cargo was accepted at the legal destination, credits increased on sale, and the hold returned to empty.

## Save/load check

After the sale, `F6` wrote the save file. The saved state contained:

- `player_credits: 3020`
- New York system index `1`
- Empty cargo list
- Newark Water market stock `201`

The run then changed station area and used `F8`. The game returned to free flight at the saved New York location with no duplicate cargo and no duplicate money. The save/load path safely exits the docked station UI rather than restoring the dealer screen; this is non-blocking but not especially explicit to the player.

## Travel and docking feel

Fresh-start docking at Fort Bush felt short and practical. The Fort Bush → Newark distance was reasonable, but the full route felt a little long because of the tradelane handoff and the final docking approach. Repeated docking was practical once using the visible dock-assist flow.

The GOTO route initially exposed a near-180-degree steering edge case and moved away from Newark. After the small steering fallback, the route converged on the Newark tradelane entry. The tradelane displayed a clear `Press F5` prompt, but manually entering it did not keep the GOTO node state synchronized; dock assist was used to complete the route reliably.

## What worked well

- Clean-start docking reached Fort Bush without dev-only setup.
- Fort Bush station services and commodity dealer were reachable.
- Water was clearly legal and had the intended Newark destination spread.
- Buy, hold, sell, credit, and market-stock changes were all correct.
- Newark docking and the commodity dealer were reachable through dock assist.
- Save/load preserved credits, empty cargo, market state, system, and safe location.
- Final build, dock smoke, and all-smoke gates remained green.

## Issue list

### Blocking bug

- **Resolved during this pass:** GOTO/dock assist could steer away from Newark when the initial heading was nearly opposite the target. The small `SteerToward` fallback fixed the edge case; the final run converged and all smoke suites stayed green.
- No remaining blocker prevented completion of the legal route when using dock assist.

### Confusing UX

- GOTO can stop at a tradelane entry with a visible F5 prompt, but manual F5 entry does not advance the GOTO state, so the route can return toward the entry ring. This is avoidable with dock assist but is confusing for a new player.
- The station menu opens the dealer in Equipment mode by default; the player must use `TAB` to reach commodities. The existing `[3] Equipment` label can make the intended commodity action unclear.

### Travel/docking feel

- Fort Bush docking felt good. Newark was reachable, but the tradelane handoff and final approach added noticeable waiting and made the route feel slightly indirect.

### Market/balance issue

- The legal route produced the intended modest profit: +20 CR for one Water. That is correct for the one-unit validation, though a larger-quantity pass is still needed to judge how rewarding the route feels in normal play.

### Missing feedback

- After quickload, the station UI is closed and the player is back in space. The brief load feedback is easy to miss, and free-flight HUD state does not prominently restate the restored credits/cargo result.
- The existing Phase 1K.3 market/equipment lower-row overflow at 1920×1080 remains non-blocking.

### Nice-to-have

- Keep the tradelane handoff prompt, but make it explicit whether F5 is a manual step or have GOTO synchronize/auto-enter when aligned.
- Show route buy/sell spread or expected per-unit profit in the market flow.
- Defer the existing dealer/equipment layout polish until the route loop is stable.

## Recommended next pass

Make the next pass a focused navigation/persistence replay: verify GOTO plus tradelane entry state synchronization, then run a 5–10 unit Water or Food transaction to judge profit feel and cargo feedback. Keep the current build, `--dock-smoke`, and `--all-smoke` gates unchanged. No additional trade or economy system is needed for this phase.
