# Phase 84 — Pirate Hail Input and Response Polish

**Phase 84 improves input and response feedback for the existing pirate hail and adds no demand, cargo, combat, or persistence authority.**

## 1. Input architecture discovered

Roguelancer polls XNA input directly with no input-manager abstraction:

- `Keyboard.GetState()` / `Mouse.GetState()` once per frame in `RoguelancerGame.UpdateCore`
  (`RoguelancerGame.cs:3597-3598`), with manual edge detection via `_prevKeys`
  and `_prevMouseState`.
- Phase 81 keyboard handling: `NpcPirateCargoDemandService.HandleInput(current,
  previous, player)` consumes edge-triggered `Enter` (comply) / `N` (refuse).
- Phase 83 mouse handling: `HandlePirateDemandHailClick` hit-tests the click
  against the shared `PirateDemandHailLayout` and routes through
  `PirateDemandHailPresentation.TryComply/TryRefuse` into the Phase 81 service.
- Station UIs follow the same convention: direct per-action keys, no shared
  focus framework (`StationCommodityTraderUI`, `StationDockUI`, etc.).

## 2. Controller UI architecture discovered

**None exists.** The assembly contains no `GamePad`/`Joystick` type and no
method that produces or consumes `GamePadState`/`GamePadButtons`; input is
keyboard/mouse only. Per the phase rules, no general controller framework was
built. The controller portion is a documented limitation (see §13).

## 3. Phase 83 interaction baseline

- Non-modal top-center hail panel (`DrawPirateDemandHailPanel`): demander
  identity, `Liberty Rogues` faction label, exact cargo lines, countdown,
  `[Enter] COMPLY` / `[N] REFUSE` buttons.
- Keyboard `Enter`/`N` direct actions; clickable mouse actions; live
  combat/simulation; authoritative Phase 81 transaction routing.
- No hover state, no focus state, no pressed/acknowledged state: the panel
  vanished instantly on resolution with zero hail-level feedback.

## 4. Focus model

Focus is a presentation-only selection state on `PirateDemandHailPresentation`
(`FocusedResponse`, enum `PirateHailResponse.None/Comply/Refuse`):

- `COMPLY` when the pointer hovers the COMPLY hit region, `REFUSE` when it
  hovers REFUSE, otherwise the deterministic default (COMPLY).
- Focus never activates an action, never alters demand state, and never gates
  the direct keys. Each response's UI-confirm action is its existing direct
  key (`Enter` for COMPLY, `N` for REFUSE) — the same direct-per-action
  convention the game uses for the Police contraband stop.
- A focused response means: the response the presentation shows as selected.
  The direct keys still invoke their matching authoritative actions regardless
  of focus (proven by smoke cases 5–6).

## 5. Default focus behavior

`DefaultResponseFocus = PirateHailResponse.Comply`, reset on every new
authoritative demand (detected by demander object identity; cleared through
the no-demand transition). Rationale:

1. COMPLY is the first (left) control in the Phase 83 layout.
2. `Enter` is the existing universal confirm key and already means comply in
   Phase 81/83, so the default focus mirrors existing behavior exactly.
3. Compliance is the non-combat resolution.
4. REFUSE was rejected: no game safety convention favors it here (both
   outcomes cost the player — cargo vs. combat), and defaulting to REFUSE
   would divorce the visual default from the existing `Enter` semantics.

The default focus never triggers an action.

## 6. Mouse hover/pressed behavior

- Hover is recomputed every frame in `PirateDemandHailPresentation.Update`
  against the same shared layout the renderer draws, so a click can only land
  on the control that was highlighted.
- COMPLY highlights only inside its hit region, REFUSE only inside its; no
  hover outside; the 12 px inter-button gap is neutral (proven). Hover never
  activates and never alters demand state. Hit regions are disjoint at every
  cargo-line count (proven).
- Focused button: full accent perimeter border, brightened fill, white label.
  Hovered button: accent tint overlay plus brightening. No label text changes,
  so the 150 px button bounds are never overflowed.
- No keyboard navigation keys were added: the in-flight arrow keys are bound
  to turret aiming (`RoguelancerGame.cs:3961-3964`), and inventing a
  pirate-specific side binding would conflict with combat controls. The
  presentation declares no `KeyboardState` member at all (proven).

## 7. Controller behavior

Not supported: no production controller/gamepad UI navigation exists to
integrate with (proven by `NoControllerUiAbstractionExists`). No controller
framework was built and no controller tests were fabricated. When controller
support lands, the presentation's `PirateHailResponse` focus state is ready to
receive navigation input and the single authoritative action seam
(`TryComply`/`TryRefuse`) is already the only commit path.

## 8. Acknowledgement design

A bounded, presentation-only beat observed from the authoritative
`NpcPirateCargoDemandService.DemandResolved` event (no Phase 81 change was
needed — the event and `LastResult` already existed):

- Player-complied → `Rogue: Smart choice. We're taking the cargo.`
- Player-refused → `Rogue: Then we'll take it ourselves.`
- Timeout (`response window expired`) → `Rogue: Time's up.` (visually distinct
  from any button-pressed state; no fake selection shown)
- Every other resolution (demander destroyed/despawned, state change, player
  attack, range exit, invalid player) → no beat; the existing
  failure/cancellation behavior stands.
- Duration `4.0 s`, matching the service's existing result-presentation window
  (`SetPresentation(text, 4f)`), so the beat fully replaces the old
  single-line result text instead of overlapping it. Non-modal throughout.
- Drawn as a bounded single-line panel (470 × 48) in the hail's style; the
  old `HudText` result line is suppressed while the beat is active.

## 9. Result-message arbitration

Per committed player response the transient messaging is bounded:

1. One hail acknowledgement beat (Phase 84, replaces the redundant
   `HudText` result line while active).
2. One existing `NotificationManager` gameplay message (Phase 81 convention;
   kept for clarity, unchanged).
3. The existing `HudText` result line may reappear only after the beat
   expires, inside the service's own window (never simultaneously).

No message is shown twice at once; the acknowledgement itself adds zero
notifications and performs zero transactions (proven).

## 10. Input races

- Service idempotency is the final guard: the second same-frame response
  (`TryComply`/`TryRefuse`/`HandleInput`) finds no active demand and is a
  no-op (proven for held keys, repeated clicks, comply+refuse, and
  mouse+keyboard orders).
- Timeout/input same-frame race: production `UpdateCore` ordering is
  `NpcPirateCargoDemandService.Update` (timeout, line ~4026) → hail click
  (line ~4319) → `HandleInput` (line ~4586). The timeout update runs first, so
  the timeout wins deterministically and the player's same-frame keypress
  observes a resolved demand. Documented and proven.

## 11. Non-modal combat behavior

No changes to flight, weapons, damage, NPC combat, Police behavior, targeting,
docking, trade lanes, or surrender systems. The hail keeps the simulation
live; focus visuals are not combat focus (reflection probes for combat,
weapon, lock, pause, fugitive, surrender, fire-cessation, flight-state, and
trade-lane authority all pass).

## 12. Lifecycle cleanup and persistence

`PirateDemandHailPresentation.ClearTransientState()` clears focus, hover,
acknowledgement timer, and acknowledged text. The game calls it adjacent to
every demand-cancelling boundary: player death, dock completion, system
transition, and save/load reset. The acknowledgement additionally clears
itself when its demander is destroyed, when a new demand begins, and when its
bounded timer expires. Nothing is persisted; `SaveGameData.CurrentSchemaVersion`
remains 13 and no Phase 84 save member exists.

## 13. Tests

`Phase84PirateHailInputResponseSmokeTest` (`--phase84-smoke`, registered in
`--all-smoke`): **65 passed, 0 failed**, covering default focus, direct-key
preservation, hover hit-testing, single-transaction routing, input races,
result-driven acknowledgements, message arbitration, lifecycle cleanup,
schema stability, and all Phase 77–83 regressions.

## 14. Limitations

- No controller/gamepad UI navigation: the repository has no input
  abstraction for it, and the in-flight arrow keys are turret-bound.
- No keyboard focus-navigation keys (same turret-binding reason); keyboard
  players use the existing direct keys, which always work.
- The acknowledgement carries no dynamic quantities (the surviving
  notification and result line keep that data); it is a short in-world beat,
  not a dialogue system.

## 15. Recommended Phase 85

Leave the hail interaction alone unless controller UI navigation is built
game-wide first. A better next target: the `HudText` single-line convention
itself — several systems (pirate demand, Police stop) still show a stale
demand line for seconds after their encounter resolves.

**Phase 84 improves interaction with the existing pirate hail and creates no parallel demand, cargo, combat, or persistence authority.**
