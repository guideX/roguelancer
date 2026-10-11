# Phase 86 — Spaceflight Encounter HUD State Cleanup

**Phase 86 removes stale encounter presentation ownership without introducing new piracy, Police, cargo, combat, or persistence authority.**

## 1. Gate and starting position

- Repository: `D:\dev\Roguelancer\Roguelancer`, branch `main`.
- Starting HEAD: `d8aa3c03bd890269b0c346ab3e21854a29898446` (Phase 85).
- Starting `.phase`: `last_completed=85`, `next_expected=86` — prompt gate passed.
- Worktree was clean; no Phase 86 completion existed in history; Phase 85 commit and doc inspected before any edit.

## 2. Player piracy HUD architecture (before)

`PirateCargoDemandService` (player-to-NPC piracy) owned two mutable
presentation fields, exactly the pattern Phase 85 removed NPC-side:

- `_presentationText` (`string`), `_presentationRemainingSeconds` (`float`),
  exposed through `HudText => _presentationRemainingSeconds > 0f ? _presentationText : string.Empty`
  (`PirateCargoDemandService.cs`, pre-Phase-86).

Writers:

1. `TryIssueDemand` → `SetPresentation("CARGO DEMAND SENT", 3f)` — a static
   sentence with no target identity and no countdown, shown for exactly the
   3 s response window.
2. `ResolveCompliance` → `SetPresentation($"Cargo surrendered: {surrendered} units", 4f)`
   — post-resolution history, written *after* `Complete()` nulled the active
   demand.
3. `ResolveRefusal` → `SetPresentation("TRADER REFUSED — FLEEING", 4f)` —
   same post-resolution retention.
4. `ResolveDestroyedOrInvalid` / `CancelActiveDemand` / `Reset` → cleared
   both fields.
5. `Update` → ticked the presentation timer every frame, even with no
   active demand.

Readers: exactly one production reader,
`RoguelancerGame.DrawPiracyDemandHUD` (`RoguelancerGame.cs:8171`), plus two
`PiracyDemandSmokeTest` cases that asserted the stale strings verbatim.

## 3. Actual discovered stale-state defects (player piracy)

Two genuine defects, proven by code inspection and by the repo's own tests:

1. **Retained post-resolution history.** After compliance or refusal,
   `HasActiveDemand == false` while `HudText` stayed non-empty for 4 s, so
   the HUD drew an outcome with no active encounter behind it. The old
   `DemandHudShowsBoundedOutcome` case proved it: after `Advance()`
   resolved the demand, `HudText` still showed the result line.
2. **Static active prompt.** During the demand the HUD showed
   `"CARGO DEMAND SENT"` — no live target, no authoritative countdown —
   even though `ActiveTarget` and `ResponseRemainingSeconds` existed.

Additionally, the dynamic surrender quantity lived **only** in the mutable
string (plus the log). The service had no notification seam, so §11
required migrating that information before removing the string.

## 4. Selected changes (player piracy, Outcome A)

Mirroring the Phase 85 treatment, smallest compatible approach:

- Deleted `_presentationText` / `_presentationRemainingSeconds`, the
  `SetPresentation` helper, the per-frame presentation tick, and every
  clear site (`CancelActiveDemand`, `Reset`, `ResolveDestroyedOrInvalid`).
- `HudText` is now a **read-only computed projection of the current active
  demand only**: `HasActiveDemand ? FormatLiveDemandText() : string.Empty`.
  `FormatLiveDemandText` reads only the authoritative target and remaining
  window (`CARGO DEMAND — {name} | awaiting response {n:0.0}s`, name
  bounded to 40 chars); it retains nothing and is empty whenever no
  demand is active.
- Resolution feedback migrated to a new optional trailing constructor
  seam `Action<string> notify` (source-compatible; existing harnesses
  unchanged): compliance emits exactly one
  `Trader complied: {n} units surrendered`; refusal emits exactly one
  `Trader refused the cargo demand — fleeing`. Destroyed/invalid,
  cancellation, and reset stay silent, as before. Production passes
  `message => _notificationManager?.ShowMessage(message, 3f)`, matching
  the NPC-side convention.
- `DemandResolved` still fires exactly once per resolution from
  `Complete()`; `LastResult` (authoritative result data, not presentation
  text) is unchanged; all cargo, timing, reputation, distress, flee, and
  eligibility mechanics are byte-for-byte untouched.
- `DrawPiracyDemandHUD` code is unchanged apart from a contract comment;
  it can now only render the live prompt.
- The two stale-asserting `PiracyDemandSmokeTest` cases were updated to
  the new contract (live prompt during demand; empty HUD plus exactly one
  notification after resolution). No other existing suite needed changes.

## 5. Police stop HUD architecture (already correct, unchanged)

`PoliceScanSystem.StatusText` (`PoliceScanSystem.cs:60-68`) was already a
computed switch over **live state** — no retained string exists:

- `Scanning` → live progress line from `_scanTimer`.
- `ContrabandDetected` → `BuildDemandStatusText()` from the live
  `_enforcementOffer` plus the live `_enforcementTimer`; controls, quoted
  fine, affordability, and tier suffix all derive from the authoritative
  quote.
- `Cleared` / `Enforcement` → bounded 2 s state-derived result hold
  (`ResultHoldSeconds`), then `ClearResultState()` → `Idle` → empty.
- Resolution (`TryResolveEnforcement`) atomically nulls the offer **and**
  transitions state, so no stale fine or comply/refuse prompt can survive;
  despawn/destroy/dock/transit paths all clear to `Idle` deterministically.

Supporting systems hold no presentation: `PoliceEnforcementService` is
fully stateless (verified: no non-backing-field instance state);
`PoliceContrabandStopCoordinator` owns only `_activeOwner`, hold-fire
membership, and a one-shot announcement flag (no strings, no timers);
`PoliceFugitiveManager.StatusText` is state-derived with a bounded
presentation beat owned by the fugitive authority (Phases 77–80 territory,
deliberately untouched). Per §6, legitimate live state was not mistaken
for stale text — **zero Police files were modified**.

## 6. All relevant readers/writers

- `PirateCargoDemandService.HudText`: writers removed (3 sites + tick);
  sole production reader `DrawPiracyDemandHUD` unchanged in behavior.
- New writer-equivalent: the `notify` seam, invoked exactly once per
  comply/refuse resolution; nothing is invoked on read.
- Police: no `StatusText`/`CurrentOffer`/`IsEnforcementDemandActive`
  semantics changed; no reader touched.

## 7. Live-state projection

- Piracy active demand: target name + authoritative countdown, recomputed
  per read, O(1), bounded formatting.
- Piracy after resolution: empty; feedback via the one notification plus
  the existing `DemandResolved`/`LastResult` channel (route-risk recording
  untouched).
- Police: unchanged live projection, proven across scan, demand, hold,
  cancellation, death, despawn, dock, transit, and reset.

## 8. Resolution/notification authority

- Piracy: one authoritative resolution (`Complete` → `DemandResolved`) →
  exactly one bounded notification for comply/refuse; silent for
  destroyed/invalid/cancel/reset. No notification on read, no replay
  after load, no duplicate from old+new paths (old path deleted).
- Police: existing bounded notification behavior fully preserved
  (detection notice, tier label, result + compliance summary); no new
  messages added.

## 9. Preserved dynamic feedback

- Surrendered quantity survives in the compliance notification
  (`Trader complied: {n} units surrendered`) and in
  `LastResult.SurrenderedQuantity`.
- Refusal outcome survives in the refusal notification.
- Police fine/confiscation quantities survive in the existing result and
  compliance-summary messages (unchanged).

## 10. Input behavior

- Y-key initiation, 3,500 m range, one-demand guard, and one-shot victim
  bounds unchanged. Player piracy has no response keys (trader
  auto-responds); Police `Enter`/`N` handling, same-frame idempotency, and
  timeout-before-input precedence all proven unchanged.
- Reading `HudText`/`StatusText` performs no transaction (proven by
  20-read stability cases).

## 11. Lifecycle

Proven clean (empty HUD, no active demand, no replay) across normal
completion, refusal, timeout, invalid target, NPC destruction, NPC
despawn, player death, cancellation, system transition (`Reset`), and
save/load (`Reset` + world teardown). A new demand formats only its own
target; the previous result cannot leak.

## 12. Save/load

No new save fields. `SaveGameData.CurrentSchemaVersion` remains **13**;
reflection proves no `HudText`/`Presentation`/`Acknowledgement`/`Demander`/
`PirateDemand`/`Hail`/`Hover`/`Transient`/`PoliceStatus`/`PoliceDemand`/
`ScanText` member exists on `SaveGameData`.

## 13. Performance

One fewer per-frame timer tick, two fewer fields, format-on-read only
(once per frame on the production draw path). No scans, no queues, no
history traversal, no new searches. O(1) state queries throughout.

## 14. Testing

`Phase86SpaceflightEncounterHudStateCleanupSmokeTest` (`--phase86-smoke`,
registered in `--all-smoke`): **45 passed, 0 failed**, covering player
piracy (15), Liberty Police (20), and shared invariants (10), including
reflection guards against new string state, new events, new notification
authority, and new save members.

Regression validation, all green:

- Phase 86: 45/0.
- Phase 57: 19/0; Phase 66: 5/0; Phases 68–80: all pass (14–61 each);
  Phase 81: 70/0; Phase 82: 60/0; Phase 83: 60/0; Phase 84: 65/0;
  Phase 85: 62/0.
- Player piracy demand: 61/0; player target scan: 25/0; police
  enforcement: 30/0; police fugitive: 65/0; contraband: 23/0;
  reputation: 102/0.
- `--all-smoke`: **86 suites passed, 0 failed** (85 prior + new suite).
- `dotnet build -c Debug` (full, `--no-incremental`): 0 errors,
  **128 warnings — identical to the pre-change baseline**; no new warnings.

## 15. Limitations

- `HudText` was kept (as a live-only projection) rather than deleted,
  because the production draw path reads it; deleting it would require
  unrelated UI restructuring for zero gameplay gain (same rationale as
  Phase 85).
- `LastResult` still retains the last resolution record (pre-existing
  API consumed by route-risk recording and tests); it is authoritative
  result data, not presentation text, and `Reset()` clears it.
- Police presentation was audited and proven correct, so no Police
  production file changed; any future Police acknowledgement-style beat
  would be a new feature, not cleanup.
- The fugitive pursuit beat (`PoliceFugitiveManager` presentation timer)
  was treated as presentation-owned transient state under Phases 77–80
  ownership and left untouched.

## 16. Recommended Phase 87

With NPC piracy (Phase 85) and player piracy (Phase 86) both live-only
and Police stops proven clean, the remaining single-line HUD conventions
are done. A good next target: verify the same live-only ownership for
any other retained world-space status strings (e.g. target-scan or
trade-plan presentation), or close out the encounter-HUD series and
return to gameplay phases. No new piracy features are needed.

**Phase 86 removes stale encounter presentation ownership without introducing new piracy, Police, cargo, combat, or persistence authority.**
