# Phase 1K.4 - First Legal Trade Route Completion Test

Date: 2026-08-01

## Date / run context

- Replayed against the current `main` checkout after the Phase 1K.3 station-loop pass.
- Started from a clean client save. The pre-existing post-route save was preserved outside the repository before the replay.
- No gameplay code was changed during this validation pass. The current checkout already contains the earlier small GOTO steering fallback; it was not modified here.
- The complete live route was: fresh start -> Fort Bush -> buy Water -> Newark Station -> sell Water -> quicksave -> visible station-area change -> quickload.

## Validation commands

| Command | Result |
|---|---|
| `dotnet build Roguelancer.sln --no-restore` | PASS - 0 errors, 56 existing warnings |
| `dotnet run --no-build --project Roguelancer.csproj -- --market-smoke` | PASS - 7 passed, 0 failed |
| `dotnet run --no-build --project Roguelancer.csproj -- --dock-smoke` | PASS - 9 passed, 0 failed |
| `dotnet run --no-build --project Roguelancer.csproj -- --all-smoke` | PASS - 12 suites passed, 0 failed |

The smoke output still includes existing non-blocking missing optional-content/model and sun-effect warnings. None affected the trade route.

## Manual route and transaction

- System: New York System
- Origin station: Fort Bush
- Destination station: Newark Station
- Commodity: Water
- Quantity: 1 unit
- Cargo capacity: 50 units

| Checkpoint | Credits | Cargo hold | Market state |
|---|---:|---|---|
| Before purchase | 3,000 CR | 0/50 | Fort Bush buy price: 55 CR/unit; stock: 500 |
| After purchase | 2,945 CR | 1/50 Water | Fort Bush stock: 499 |
| Before sale | 2,945 CR | 1/50 Water | Newark sell price: 75 CR/unit; stock: 200 |
| After sale | 3,020 CR | 0/50 | Newark stock: 201 |

Profit/loss: **+20 CR** (`75 - 55`, one unit).

The route was legal and completed without developer-only market manipulation. Credits decreased on purchase, the Water stack appeared in the hold, Newark accepted the cargo, credits increased on sale, and the hold returned to empty.

## Travel and docking feel

- The fresh-start direct F3 dock-assist fallback reached Fort Bush reliably and felt practical for the first station.
- Newark was targeted from the system map and GOTO reached the Manhattan-Newark tradelane entry.
- The route displayed a clear F5 tradelane-entry prompt, but pressing F5 did not synchronize the GOTO state. GOTO returned to the entry prompt, so F3 dock assist was used to complete the Newark approach.
- Repeated docking at Fort Bush and Newark was practical with dock assist.
- Overall route feel: slightly long / somewhat indirect because of the tradelane handoff and final docking approach, but still reasonable for a first legal hop.

## Save/load check

After the sale, F6 quicksave succeeded. A visible station-area change was made, then F8 quickload returned the client to safe New York free flight and showed `Game Loaded`.

The written save payload contained:

- `player_credits: 3020`
- `current_system_index: 1`
- an empty cargo list
- Fort Bush Water stock `499`
- Newark Water stock `201`

No duplicate cargo or money appeared. The post-load state confirms that the sale, credits, cargo removal, market stock changes, and location state were preserved. The save/load path leaves the player in free flight rather than reopening the dealer screen, which is safe but not very explicit.

## What worked well

- A new player could get from a clean start to Fort Bush with the visible dock-assist path.
- Fort Bush station services and the commodity dealer were reachable.
- Water was clearly legal and had a positive Newark destination spread.
- Buy, cargo, sell, credit, and station-stock changes all matched the intended values.
- Newark docking and the second market screen were reachable without code or save editing.
- Quicksave/quickload preserved the completed sale and did not create an exploit.
- Build, market smoke, dock smoke, and all-smoke remained green without gameplay changes.

## Issue list

### Blocking bug

- None found. The legal route completed end-to-end.

### Confusing UX

- GOTO can stop at the Newark tradelane entry with an F5 prompt, but manual F5 entry does not advance the GOTO state; the route can return to the same entry point. Dock assist is a reliable workaround, but the behavior is confusing for a new player.
- The station navigation label `[3] Equipment` opens the dealer in Equipment mode. The player must press TAB to reach commodities.

### Travel/docking feel

- Fort Bush docking felt good. The Fort Bush -> Newark route felt slightly long because of the tradelane handoff and final approach.

### Market/balance issue

- No failure found. The intended legal Water route produced a modest +20 CR/unit profit.
- One unit validates correctness, but a 5-10 unit run is still needed to judge normal play reward and cargo feedback.

### Missing feedback

- `Game Loaded` is visible briefly, but free-flight HUD state does not prominently restate the restored credits or cargo result after quickload.
- The commodity/equipment lists extend below the fixed 1920x1080 viewport. Keyboard selection remains safe, so this is non-blocking.

### Nice-to-have

- Synchronize GOTO state with manual F5 tradelane entry, or make the handoff automatic when aligned.
- Show the expected buy/sell spread or destination profit in the market flow.
- Add paging/scrolling for the lower market and equipment rows.

## Recommended next pass

Run a focused navigation/persistence replay around GOTO plus tradelane-entry synchronization, then repeat the Water or Food route with 5-10 units to judge profit feel. Keep `dotnet build`, `--market-smoke`, `--dock-smoke`, and `--all-smoke` as the regression gate. No new trade or economy system is needed for this phase.
