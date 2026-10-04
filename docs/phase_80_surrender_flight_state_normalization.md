# Phase 80 — Surrender Flight-State Normalization

**Phase 80 normalizes incompatible player flight automation after accepted surrender. It adds no detention state, movement authority, or criminal-state authority.**

## Outcome

**Outcome A.** A successful surrender cleanly releases incompatible player
flight automation through existing authorities, preserves physical momentum
and manual control, retains Phase 79 combat correctness, creates no new
persistent authority, and all required validation passes.

## Actual production flight-state architecture

Player flight state is distributed; there is no single movement-mode enum:

- `Ship` owns player throttle (`_throttle`, W/S/scroll), the
  automation-owned speed override (`_autopilotTargetSpeed`, written only via
  `SetAutopilotTargetSpeed`), cruise (`CruiseDrive`), afterburner, engines
  killed, Newtonian mode, free-flight mouse mode, and the trade-lane transit
  flag (`IsTradeLaneTransit`).
- `GotoAutopilot` owns GOTO navigation, including dock-assist mode
  (`_preferDirectStationApproach`), route planning (tradelane / jumphole /
  docking nodes), steering (`SteerToward`), and speed management
  (`ManageSpeed` → `SetAutopilotTargetSpeed`). `Ship.CancelGoto` is the
  cancellation authority: it clears `_gotoActive`, `_gotoTarget`,
  dock-assist state, resets `_autopilotTargetSpeed` to `-1f`, and calls
  `GotoAutopilot.Cancel()`.
- `CruiseDrive` owns cruise (Inactive/Charging/Active/Cooldown).
  `Ship.CancelCruise(CruiseCancellationReason)` is the cancellation authority;
  `CruiseCancellationReason.IncompatibleFlight` already exists.
- `TradelaneManager` owns trade-lane transit; `JumpHoleManager` owns jump
  transit. While either owns the ship, `RoguelancerGame.Update` returns
  early (RoguelancerGame.cs:3604 and :3659) **before** the S-key surrender
  handler at RoguelancerGame.cs:3901 is reached.
- The cinematic `DockingAutopilot.StartDocking` is never called from
  production code; the live dock-assist path is `TryStartDockAssist` →
  `Ship.ActivateDockAssist` → `GotoAutopilot.Activate(target, true)`.

## Real stale-flight risk discovered

A successful Phase 78/79 surrender resolved fugitive state and suppressed
stale Police fire but left the player's own flight automation untouched.
Production-reachable defects:

1. **GOTO autopilot survives surrender.** A player fleeing via GOTO (e.g.
   toward a jumphole or a station) who presses S remained under autopilot
   authority: the route, destination, steering, and the automation-owned
   `_autopilotTargetSpeed` override all survived, so the ship kept flying
   the escape route with no player input.
2. **Dock assist survives surrender.** Phase 77 denies Liberty Police docking
   while fugitive, but dock assist to a **non-Liberty** station is allowed
   while fugitive (the fugitive check in `FactionAccessService.EvaluateDocking`
   is scoped to the Liberty Police faction id). A player with dock assist to
   a Rogue/Corporate station who surrendered would have continued on the
   automatic approach and could have completed a station transition.
3. **Cruise survives surrender.** A player fleeing under cruise (3.5x speed)
   who surrendered kept cruising at high speed with no player input.

Modes inspected and intentionally **not** reset:

- **Trade-lane transit / jump-hole transit:** the surrender input is
  unreachable while either transit owns the ship (update-loop early return
  precedes the S-key handler), so the transaction cannot commit in that
  state. Phase 80 does not touch lane state; the surrender transaction is
  verified movement-neutral with respect to an active transit flag.
- **Afterburner:** player-activated, timed, self-terminating boost. It owns
  no navigation and expires on its own; resetting it would be a blanket
  movement-system reset, which the phase rules forbid.
- **Player-owned throttle (`_throttle`):** the player's own input, preserved
  verbatim. Only the automation-owned override
  (`_autopilotTargetSpeed`) is normalized, and `CancelGoto` already resets
  it to `-1f`.
- **Newtonian mode, free-flight mouse mode, engines killed:** control/physics
  postures owned by the player, not escape automation.

## Surrender transaction ordering

Phase 78/79 semantics remain authoritative. The committed order in
`PoliceFugitiveSurrenderService.TrySurrender` is now:

1. validate surrender (Phase 78 `Assess`)
2. validate Police presence (Phase 78 `FindSurrenderPresence`)
3. validate affordability (Phase 78, revalidated via atomic `RemoveCredits`)
4. charge the canonical Heat-derived fee
5. resolve fugitive state (`PoliceFugitiveManager.ResolveSurrender`)
6. Phase 79 immediate stale-fire cessation (`ApplySurrenderFireHold`
   inside `ResolveSurrender`; `scan?.ClearResolvedResultHold()`)
7. **Phase 80 flight-state normalization**
   (`NormalizePlayerFlightAutomation`: `CancelGoto(showNotification:
   false)` + `CancelCruise(IncompatibleFlight)`)
8. the existing single surrender-success notification (presented inside
   `ResolveSurrender`; unchanged)

The S-key handler in `RoguelancerGame.Update` runs after `Ship.Update`
within the tick, so the automation is cancelled synchronously inside the
transaction, before the next frame can run the autopilot or cruise update.
No deferred callback, timer, or cancellation token exists.

## Normalization seam selected

`PoliceFugitiveSurrenderService.NormalizePlayerFlightAutomation(Ship?)` —
the same seam that owns the surrender transaction. It calls only the two
existing public cancellation authorities:

- `player.CancelGoto(showNotification: false)` — cancels GOTO **and** dock
  assist (dock assist is a GOTO mode), clears the route/destination, and
  releases the automation-owned speed override. `showNotification: false`
  keeps the cancellation silent so one surrender still produces exactly one
  notification.
- `player.CancelCruise(CruiseCancellationReason.IncompatibleFlight)` —
  disengages cruise through its existing path (applies the standard
  cooldown; a no-op when cruise is not charging/active).

## Cancellation vs physical momentum

Surrender cancels **automation**, not physics. Phase 80 never writes
`Position`, `Velocity`, `Speed`, or coordinates, never zeroes momentum,
never disables collisions, and never grants invulnerability. Existing
velocity is untouched by the transaction and remains governed by normal
ship physics; after the automation is released, the ship's ordinary
thrust/drag model behaves exactly as it does after any ordinary ESC/GOTO
cancellation.

## Manual-control result

After a successful surrender:

- `_gotoActive == false`, `GotoAutopilot.State == Cancelled`,
  `_autopilotTargetSpeed == -1f` (automation-owned override released)
- cruise is not charging or active
- `EnginesKilled == false`
- player-owned `_throttle` is byte-identical to its pre-surrender value

The gates that block manual input in `Ship.Update` (goto active, cruise
active, engines killed) are all open, so ordinary WASD/throttle control
resumes on the next frame. The player is not detained.

## Phase 79 interaction

Flight normalization and fire-authority normalization are separate
concerns and both run on commit:

- no new Police shot from stale fugitive authority after accepted surrender
  (Phase 79 fire hold + target clearing; re-verified with GOTO active)
- already-fired projectiles remain alive, keep owner/velocity/damage, and
  can still strike the moving player
- `"player attack"` temporary hostility is preserved
- unrelated factions keep their combat authority

## Failed-surrender atomicity

Every failure path in `TrySurrender` returns **before**
`ResolveSurrender`, so it also returns before `NormalizePlayerFlightAutomation`:

- **Insufficient credits:** GOTO and cruise remain exactly as ordinary
  simulation dictates; no free escape-navigation reset.
- **No Police presence:** automated flight untouched.
- **Permanent hostility:** automated flight untouched.
- **Not fugitive:** automated flight untouched.
- **Destroyed player:** automated flight untouched (existing destruction
  lifecycle only).

## Docking interaction

Phase 77 is unchanged. While fugitive, Liberty Police docking is denied by
the shared access policy; after surrender `PoliceFugitiveManager.IsActive
== false`, so Liberty Police docking eligibility returns naturally.
Phase 80 never auto-docks, never forces an approach, never teleports to a
station, and never mutates docking-access state (verified by reflection:
`FactionAccessService` gained no surrender field). A previously active
dock assist to a non-Liberty station is cancelled with the rest of GOTO,
and the cancelled approach cannot complete a station transition.

## Trade-lane behavior

The S-key surrender handler is unreachable while trade-lane or jump-hole
transit owns the ship (the update loop returns before the handler), so
surrender cannot commit mid-transit. Phase 80 therefore does not duplicate
trade-lane state management. The focused suite proves the transaction is
movement-neutral with respect to an active transit flag: transit state,
velocity, and position are all unchanged by the surrender, so no corrupt
half-transit state can be produced by this path.

## Lifecycle

- **Death/reset:** normalization is two synchronous cancellation calls with
  no transient state; `PoliceFugitiveManager.Reset` and the existing
  destruction lifecycle clear everything else. No Phase 80 state survives.
- **System transition:** no Phase 80 state crosses systems; the existing
  system-transition reset path is unchanged.
- **Dock/undock:** normal docking/undocking keep their existing
  flight-state setup/teardown (`RestoreFlightState` etc.).
- **Save/load:** no Phase 80 state persists. Schema remains 13; reflection
  proves `SaveGameData` gained no surrender/flight field.

## Persistence

No schema change. `SaveGameData.CurrentSchemaVersion == 13`. Nothing is
persisted: no surrendered flight state, no cancelled-autopilot marker, no
surrender posture, no throttle override, no cease-movement timer.

## Performance

Work occurs only on a successful surrender: O(1) null check plus O(1)
cancellation calls on the two known player flight controllers. No NPC,
station, projectile, or traffic scan is introduced; Phase 78's bounded
Police presence query is not duplicated; no per-frame "has surrendered"
poll; no history of previous flight modes is maintained. The service
holds no mutable state (only the two const fee fields).

## Tests

`Phase80SurrenderFlightStateNormalizationSmokeTest` (61 cases, all green):
baseline manual-flight surrender, fee/reputation/cargo/grace invariants,
GOTO cancellation and no-resume, dock-assist cancellation with no station
transition, cruise disengagement with no reactivation, trade-lane
movement-neutrality, no teleport/velocity rewrite, manual-control gates,
engine/steering lock absence, no forced docking, no surrender autopilot,
Phase 77 restoration without direct mutation, Phase 79 fire/projectile/
hostility interaction, failed-surrender atomicity (insufficient credits,
no Police, permanent hostility, not fugitive, destroyed player),
idempotency, single-notification, lifecycle, save/schema, contraband and
lawful-stop preservation, Phase 76/77/78/79 behavioral regression probes,
no duplicate presence scan, O(1) normalization, and a production-equivalent
order proof (fugitive + active automation + Police presence → transaction
commits → fire hardening → flight normalization → next update does not
resume automation → manual control available).

Run with `--phase80-smoke`; included in `--all-smoke`.

## Limitations

- Surrender remains reachable only where the S-key handler runs: not
  during trade-lane or jump-hole transit (by existing architecture), not
  while docked/in station interiors, and not while the player is destroyed.
- Afterburner is deliberately left running if active at surrender; it is
  player-controlled and self-terminating, not escape automation.
- The player's own throttle setting is preserved, so a ship at full
  throttle keeps moving at the player's chosen speed after surrender —
  ordinary physics, not automation.

## Recommended Phase 81

No required follow-up for this seam. If a future phase adds a new
player-owned automated movement mode (e.g. formation flight, escort
autopilot), extend `NormalizePlayerFlightAutomation` with that mode's
existing cancellation authority and add focused coverage proving the same
post-surrender invariants.
