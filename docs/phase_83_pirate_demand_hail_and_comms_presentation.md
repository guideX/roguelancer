# Phase 83 — Pirate Demand Hail and Comms Presentation

## Summary

Phase 83 makes Phase 81's NPC-to-player pirate cargo demand feel like an
in-world hail instead of a mechanical HUD line. When a Liberty Rogue demands
cargo, the demand is now presented through a bounded, non-modal hail panel
that names the demander and faction, lists the exact demanded commodities and
quantities, shows the authoritative response countdown, and offers explicit
COMPLY / REFUSE controls.

The Phase 81 `NpcPirateCargoDemandService` remains the sole authority for the
active demand, demander, demanded cargo, deadline, compliance, refusal,
timeout, lifecycle, and combat escalation. Phase 83 is presentation and
interaction integration only.

**Phase 83 presents the existing NPC pirate demand through existing UI
authority and adds no second demand, cargo, combat, or persistence authority.**

## Existing communications/UI architecture discovered

The project has several player-facing communication surfaces:

- `NotificationManager` — a single centered, fading, queued text banner at
  ~65% screen height. This is what the existing "combat radio"
  (`FactionCombatCommunicationService`) and many one-shot messages use. It is
  transient and cannot host persistent controls.
- `FactionCombatCommunicationService` — a presentation-only combat-radio queue
  that selects deterministic faction lines and throttles them. It surfaces
  each line through `NotificationManager`. It never owns combat state.
- `PoliceScanSystem` / `DrawPoliceScanStatus` — a persistent left-panel status
  line for Police contraband enforcement that includes
  `[Enter] Comply [N] Refuse` and a live countdown
  (`EnforcementDemandRemainingSeconds`). It is coupled to the Police
  scan/stop/enforcement lifecycle.
- In-flight HUD panels — `DrawActiveMissionsHUD`, `DrawFirstDockOnboardingHint`,
  `DrawActiveTradePlanHUD`, `DrawStatusPanel`, etc. These are bounded
  `_pixel`/`_font` panels drawn over the live simulation.
- `StationDialogue` / station UIs — modal, station-only surfaces that do not
  apply to live spaceflight.

No existing flight-space surface combined a persistent demander identity,
multi-line cargo list, countdown, and response buttons. The closest analogue
(Police enforcement) is intentionally bound to Police authority and must not be
generalized without touching Police systems, which Phase 83 is forbidden from
doing.

## Phase 81 presentation before Phase 83

Phase 81 exposed a single-line `NpcPirateCargoDemandService.HudText` (for
example `LIBERTY ROGUES DEMAND CARGO: Food Rations x4 | [Enter] Comply [N]
Refuse (5s)`) plus one-shot `NotificationManager` messages. `DrawNpcPirateCargoDemandHUD`
drew that one line at `(10, 64)` in IndianRed. There was no structured panel,
no per-line cargo list, and no clickable controls.

## Chosen reusable surface

Phase 83 uses a **small bounded presentation adapter**
(`PirateDemandHailPresentation`) rendered as an in-flight HUD panel. This is
preference-order option 5 ("a small dedicated pirate-demand presentation
adapter only if no reusable surface exists"). A reusable surface did not
cleanly exist:

- the combat-radio/notification surface is transient and has no controls;
- the Police enforcement surface is bound to Police authority and cannot be
  reused without violating the Phase 83 Police-independence rules.

The adapter is presentation-only and introduces no communications framework.

## Authoritative data path

The adapter reads only existing Phase 81 read-only members each frame:

- `NpcPirateCargoDemandService.HasActiveDemand`
- `NpcPirateCargoDemandService.ActiveDemander`
- `NpcPirateCargoDemandService.ActiveDemandQuantities`
- `NpcPirateCargoDemandService.ResponseRemainingSeconds`
- `NpcPirateCargoDemandService.ComplyKey` / `RefuseKey`

It builds an immutable `PirateDemandHailSnapshot` per frame. It caches no
demand, cargo, timer, or demander reference beyond the current frame and
persists nothing.

## Demander identity presentation

The panel shows the authoritative `ActiveDemander.Name` as the speaker. If the
name is blank it falls back to the canonical faction display label. No string
inference, no copied/stale name: the snapshot is rebuilt from the live service
and disappears with the demand.

## Faction presentation

The faction is rendered with `FactionManager.GetFactionDisplayName(demander.FactionId)`,
which yields `Liberty Rogues`. The raw internal id `liberty_rogues` is never
shown.

## Cargo-line presentation

The panel lists `ActiveDemandQuantities` verbatim as `Commodity xQuantity`
lines, sorted deterministically for stable rendering and bounded to
`NpcPirateCargoDemandService.MaximumCommodityTypes` (3). No cargo is selected,
re-rolled, re-priced, or filtered in the UI. Protected mission/freight cargo is
already excluded by Phase 81 and simply never appears.

## Countdown

`PirateDemandHailSnapshot.CountdownText` renders `Respond within X.X s` from
`ResponseRemainingSeconds`, which Phase 81 already clamps to `>= 0`. The UI
does not own the timeout: the service still resolves the deadline. The panel
never shows negative time and reaching zero does not execute a UI refusal.

## Input mapping

- COMPLY: `Enter` (Phase 81 `ComplyKey`)
- REFUSE: `N` (Phase 81 `RefuseKey`)
- Mouse: left-click on the panel's COMPLY / REFUSE buttons

All three converge on the Phase 81 authority. Keyboard continues to flow
through `NpcPirateCargoDemandService.HandleInput`; mouse clicks flow through
`PirateDemandHailPresentation.TryComply/TryRefuse`, which delegate directly to
`NpcPirateCargoDemandService.TryComply/TryRefuse`. There is exactly one
transaction path.

## Compliance/refusal routing

`UI input -> PirateDemandHailPresentation.TryComply/TryRefuse -> NpcPirateCargoDemandService.TryComply/TryRefuse`.

No cargo-removal, hostility, or escalation code exists in the presentation
layer. The adapter's methods are one-line delegations.

## Timeout

Timeout remains entirely owned by `NpcPirateCargoDemandService.Update`, which
resolves it as refusal and escalates once. The UI observes
`ResponseRemainingSeconds` only. When the service resolves, `HasActiveDemand`
becomes false and the panel disappears on the next draw.

## Combat behavior while visible

The simulation stays live. No invulnerability, hold-fire, weapon lock, or pause
is added. Nearby unrelated faction combat is untouched. If the player attacks
the demander, Phase 81's existing `WasDamagedByPlayer` revalidation resolves
the demand as refusal and the panel closes.

## Lifecycle cleanup

The panel has no lifetime of its own; it is visible iff `HasActiveDemand` is
true. It therefore closes immediately for every Phase 81 lifecycle outcome:
compliance, refusal, timeout, demander death, demander despawn, player death,
docking, and system transition. There is no clickable stale COMPLY control,
because the buttons only exist while the authoritative demand exists.

## Save/load behavior

No hail state is persisted. `NpcPirateCargoDemandService.Reset()` clears the
transient demand; the adapter has no save fields and no static state. Save/load
cannot recreate an open hail, countdown, pending buttons, or a demander
reference. The player save schema is unchanged (version 13).

## Duplicate-message handling

Phase 83 introduces no new result message. The pre-existing Phase 81
compliance/refusal HUD result line and notification remain authoritative.
While the demand is active the panel replaces the old single-line demand prompt
(which is suppressed) so the active demand is shown exactly once.

## Bounds/performance

- O(1) active-demand visibility query.
- O(number of demanded commodity lines) rendering, bounded to 3.
- No world scans, no NPC search, no cargo re-selection.
- The snapshot is rebuilt per frame but is tiny and allocation-bounded by the
  demand line count; no micro-optimization was added.

## Tests

`Phase83PirateDemandHailSmokeTest` (`--phase83-smoke`) covers 60 focused cases:
visibility, demander identity, faction label, exact commodity/quantity
display, multi-line display, no UI cargo selection, countdown authority and
non-negativity, no UI-owned timeout, comply/refuse routing, keyboard
compatibility, double-transaction protection, held-key protection,
compliance/refusal/timeout outcomes, demander death/despawn, player death,
dock/system transition, no delayed deduction, player-attack resolution, no
invulnerability, no hold-fire, live simulation, movement preservation,
unrelated combat, one-hail invariant, no stacked hails, ally behavior, cargo
revalidation, no commodity substitution, mission/freight reservation
protection, Police/fugitive/Phase 77-80/trade-lane independence, save/load,
schema, no persisted UI state, side-effect-free passive reads, and bounded
presentation.

## Limitations

- Mouse click support is added for the panel buttons; no controller-specific
  focus model is added because the reused surfaces did not provide one.
- The panel is drawn in the top-center HUD band; it does not implement dynamic
  reflow for extreme aspect ratios beyond the existing fixed-layout convention.
- Faction labels rely on `FactionManager.GetFactionDisplayName`; any future
  faction rename is automatically reflected.

## Recommended Phase 84

Add a small, optional controller/mouse focus cue for the hail controls and a
short "hail acknowledged" audio/visual beat, still without introducing a
generalized dialogue engine or any new demand/cargo authority. Alternatively,
consolidate the one-shot radio notification and panel into a single comms
presentation if the project later standardizes a persistent comms panel.
