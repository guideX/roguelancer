# Phase 85 — Pirate Demand Presentation State Cleanup

**Phase 85 separates active pirate-demand gameplay state from transient result presentation and creates no demand, cargo, combat, history, or persistence authority.**

## 1. Original `HudText` ownership

`NpcPirateCargoDemandService` (the Phase 81 gameplay authority) owned two
mutable presentation fields left over from the original Phase 81
implementation:

- `_presentationText` (`string`, mutable history slot)
- `_presentationRemainingSeconds` (`float`, visibility timer)

exposed through:

```csharp
public string HudText => _presentationRemainingSeconds > 0f ? _presentationText : string.Empty;
```

Every read returned retained text; nothing about the property distinguished
"a demand is happening right now" from "a demand happened seconds ago".

## 2. All discovered readers/writers

Writers (`NpcPirateCargoDemandService.cs`, pre-Phase-85):

1. `TryInitiateDemand` → `SetPresentation(demandText, ResponseWindowSeconds + 2f)`
   (live demand prompt, 7 s window).
2. `TryComply` → `SetPresentation($"Cargo surrendered: {totalRemoved} units", 4f)`
   (post-resolution history).
3. `TryRefuse` → `SetPresentation("PIRATE DEMAND REFUSED — ESCALATING", 4f)`
   (post-resolution history).
4. `ResolveRefusal` (timeout, demander state change, range exit, player attack)
   → **no write at all**; the initiation prompt was left in the slot.
5. `ResolveDestroyedOrInvalid` → cleared both fields.
6. `NotifyNpcDestroyed` → cleared both fields.
7. `CancelActiveDemand` → cleared both fields.
8. `Reset` → cleared both fields.
9. `Update` → ticked `_presentationRemainingSeconds` down every frame.

Readers:

1. `RoguelancerGame.DrawNpcPirateCargoDemandHUD` (`RoguelancerGame.cs:8184-8190`):
   legacy fallback, drawn only when neither the hail snapshot nor the Phase 84
   acknowledgement is available.
2. `Phase81NpcPirateCargoDemandSmokeTest.DemanderDeathRemovesPrompt`: asserts the
   prompt is gone after demander death.
3. Nothing else. `PirateDemandHailPresentation` never reads `HudText`; it reads
   structured state (`HasActiveDemand`, `ActiveDemander`,
   `ActiveDemandQuantities`, `ResponseRemainingSeconds`). Nothing parses the
   formatted string back into gameplay state.

## 3. Stale presentation behavior

Two distinct staleness defects:

1. **Retained post-resolution history.** After compliance or refusal the service
   kept `"Cargo surrendered: N units"` / `"PIRATE DEMAND REFUSED — ESCALATING"`
   for 4 s purely for future drawing, duplicating the Phase 84 acknowledgement
   beat plus the notification path. The service remembered an expired message.
2. **Retained live prompt after `ResolveRefusal`.** Timeout (and the other
   refusal-path resolutions) never wrote or cleared the slot, so the *active
   demand prompt* survived resolution. Timeline for a timeout: initiation sets a
   7 s window; the demand times out at 5 s; the Phase 84 acknowledgement covers
   seconds 5–9; the stale `"LIBERTY ROGUES DEMAND CARGO: …"` prompt was still
   alive until second 7 and reappeared underneath/after the acknowledgement
   with no active demand behind it.

## 4. Selected cleanup

Removed the mutable history entirely (Outcome A):

- Deleted `_presentationText` / `_presentationRemainingSeconds` and the
  `SetPresentation` helper.
- Deleted the per-frame presentation tick in `Update` and every clear site
  (`NotifyNpcDestroyed`, `CancelActiveDemand`, `Reset`,
  `ResolveDestroyedOrInvalid`).
- `HudText` is now a **read-only computed projection of current active demand
  state**: `HasActiveDemand ? FormatLiveDemandText() : string.Empty`.
  `FormatLiveDemandText` formats the authoritative `RequestedQuantities` through
  the single shared `FormatDemandText` site; it retains nothing and is empty
  whenever no demand is active.
- All `_notify` emissions are byte-for-byte unchanged (initiation demand text,
  compliance quantity line, refusal line), so no player-facing information was
  removed to remove the property.
- `RoguelancerGame.DrawNpcPirateCargoDemandHUD` code is unchanged; its fallback
  can now only render live demand text while the hail snapshot is momentarily
  unavailable, never a retained result. A comment records the new contract.
- The `AcknowledgementDurationSeconds` doc comment on
  `PirateDemandHailPresentation` was updated: it no longer references the
  deleted `SetPresentation(text, 4f)` window.

No replacement result string was introduced: reflection proves the service has
no `string`-typed instance field, no `presentation`-named field, no
string-valued `List<>`/`Queue<>`/`Stack<>`, and `HudText` is a getter-only
property.

## 5. Structured active-demand authority

Unchanged Phase 81 surface, still the only gameplay authority:

- `HasActiveDemand`, `ActiveDemander`, `ActiveDemandQuantities`,
  `ResponseRemainingSeconds`, `ComplyKey`/`RefuseKey`,
  `TryComply`/`TryRefuse`/`HandleInput`, cooldown, eligibility, haul,
  escalation, cancellation, `Reset`.

## 6. Resolution event authority

Unchanged: `DemandResolved` fires exactly once per resolution from `Complete`
with the full `NpcPirateCargoDemandResult` (state, demander, identity,
demanded quantities, surrendered quantity, reason). It drives the Phase 84
acknowledgement and remains the only resolution channel. No second
post-resolution state machine was added to the service.

## 7. Acknowledgement behavior

Unchanged Phase 84 beat, presentation-only, 4.0 s, non-modal:

- Comply → `Rogue: Smart choice. We're taking the cargo.`
- Explicit refuse → `Rogue: Then we'll take it ourselves.`
- Timeout → `Rogue: Time's up.`
- All other resolutions (destroyed/despawned/invalid/cancelled) → no beat.

## 8. Notification behavior

Unchanged Phase 81 convention, exactly as before:

- Initiation: one demand-text notification (also feeds the panel-era HUD).
- Compliance: `Pirate cargo demand complied: N units surrendered`.
- Refusal: `Pirate cargo demand refused`.
- Timeout/cancellation: no new notification (acknowledgement covers timeouts).

Per-response message counts are unchanged (proven by the Phase 84 arbitration
cases, all still passing).

## 9. Preserved dynamic cargo information

Exact surrendered quantities survive in two places: the compliance
notification (`… N units surrendered`) and `LastResult.SurrenderedQuantity` /
`DemandedQuantities`. The acknowledgement stays a short fixed beat; no dynamic
data was lost with the deleted `"Cargo surrendered: …"` service string.

## 10. Remaining compatibility projection

`HudText` itself, now live-only: current authoritative demand text while a
demand is active, `string.Empty` otherwise. It never retains a previous
resolution and is never parsed back. The two pre-existing readers
(draw fallback, Phase 81 death-prompt case) keep working unchanged.

## 11. Lifecycle cleanup

Because there is no retained text anymore, every boundary is trivially clean;
all are proven: demander destruction, demander despawn, player destruction,
docking, system transition, save/load reset, demand cancellation, failed
compliance, timeout, and repeated encounters. A new demand formats only its own
`RequestedQuantities`; the previous result cannot leak (proven, including an
explicit old-text/new-text discrimination case).

## 12. Save/load

No new persistence. `SaveGameData.CurrentSchemaVersion` remains 13; reflection
proves no `HudText`/`Presentation`/`Acknowledgement`/`Demander`/`PirateDemand`/
`Hail`/`Hover`/`Transient` member exists on `SaveGameData`. `Reset()` plus
`ClearTransientState()` (the production save/load and system-transition path)
leaves no active demand, no HUD text, no snapshot, no acknowledgement, and a
null `LastResult`.

## 13. Performance

Same or better than Phase 84: one fewer per-frame timer tick in the service
update, two fewer fields, no per-frame string retention. `HudText` formats on
read only, and production reads it at most once per frame on the fallback path
(which is unreachable while the hail panel is up). No scans, no logs, no queues,
no polling were added. Build warnings identical to baseline (128 → 128).

## 14. Testing

`Phase85PirateDemandPresentationCleanupSmokeTest` (`--phase85-smoke`,
registered in `--all-smoke`): **62 passed, 0 failed**, covering active
presentation (10), resolution behavior (14), presentation lifetime (6), input
regressions (9), lifecycle (9), and existing-authority regressions (14,
including two extra architecture probes: service owns no acknowledgement
state, presentation owns no gameplay state).

Regression validation, all green:

- Phase 85: 62/0.
- Phase 57: 19/0; Phase 66: 5/0; Phases 68–80: all pass; Phase 81: 70/0;
  Phase 82: 60/0; Phase 83: 60/0; Phase 84: 65/0.
- Player piracy demand: 61/0; player target scan: 25/0; loot/salvage,
  black market, contraband, faction combat (communication/escalation/
  disengagement/bounty), police enforcement/fugitive, docking, traffic,
  trade-lane hardening, mission, freight, weapon/thruster/cruise energy,
  missile/countermeasure/mine: all pass.
- `--all-smoke`: **85 suites passed, 0 failed** (includes the new suite).
- `dotnet build -c Debug`: 0 errors, 128 warnings — identical to the
  pre-change baseline; no new warnings.

## 15. Limitations

- `HudText` was kept (as a live-only projection) rather than deleted, because
  the production draw fallback and one Phase 81 case still read it; deleting it
  would have required unrelated UI/test restructuring for zero gameplay gain.
- `LastResult` still retains the last resolution record (pre-existing Phase 81
  API consumed by the acknowledgement and tests); it is authoritative result
  data, not presentation text, and `Reset()` clears it.
- The player-side `PirateCargoDemandService.HudText` (player-to-NPC piracy) was
  deliberately left untouched as out of scope.

## 16. Recommended Phase 86

Apply the same live-only treatment to the player-side
`PirateCargoDemandService.HudText` and the Police-stop stale demand line noted
in the Phase 84 doc (§15), now that the NPC-side pattern is proven. No new
piracy features are needed.

**Phase 85 separates active pirate-demand gameplay state from transient result presentation and creates no demand, cargo, combat, history, or persistence authority.**
