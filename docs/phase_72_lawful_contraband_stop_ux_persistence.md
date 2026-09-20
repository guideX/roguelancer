# Phase 72 — Lawful Contraband Stop UX, Pacing, and Persistence Hardening

Phase 72 is a production-hardening pass over the Phase 71 lawful
contraband-stop path. No new crime framework was built. All behavior reuses
the Phase 68–71 authorities:

`TrafficManager.UpdatePlayerContrabandEnforcement` (bounded detection) ->
`NpcShip` movement/retention -> `PoliceScanSystem` (single scan/demand
owner) -> `PoliceEnforcementService` + `CargoHold.TryConfiscateCargo`
(comply) or `PoliceFugitiveManager` (refuse/timeout/evade/attack) ->
`NpcWeaponSystem` (only projectile/damage path).

Production proof:

`distant detection (hold-fire intercept, stop order) -> close to ScanRange
-> scan progress -> demand with Enter/N + 8s timer -> comply (live-hold
confiscation + exact summary) or refuse/flee (fugitive) -> escape ->
20s reacquisition grace -> legitimate new encounter may re-detect`

## Production behavior traced (Phase 71 baseline)

- Initial detection: lawful NPC within `TrafficActivationRange` (default
  6500) with live canonical contraband becomes a
  `ContrabandEnforcement` interceptor. Console-only log; **no player-facing
  notice** (defect 1).
- Interceptor approach: `AttackingPlayer` engagement movement toward the
  live player anchor at `max(cruise*1.2, 160)` (~216 for LawfulPatrol).
  HUD shows nothing until scan range (defect 2: 6500→3200 gap is silent).
- Scanner acquisition: `PoliceScanSystem` claims the nearest lawful
  `LawfulPatrol` officer within `ScanRange` 3200. Tie-break hashed faction
  + name + **position** (defect 3: position in the key makes equal-distance
  ownership flicker as ships move).
- Scan progress: 3.5s within `CancelRange` 3800; `AttackingPlayer` movement
  already accepted for `ContrabandEnforcement` interceptors (Phase 71,
  preserved). Start message was the generic "Liberty Police inspection
  initiated".
- Demand: `ContrabandDetected` + `PoliceEnforcementOffer`, 8s timer, Enter
  comply / N refuse. The notice did not name the keys; the countdown lived
  only on the HUD status line (correct placement, kept).
- Supporters: every interceptor chased the exact player anchor — visible
  stacking with 2+ officers (defect 4).
- Jettison during interception/scan: physical pods via
  `LootManager.TryJettisonContraband`; cargo-backed retention clears the
  stop if the hold goes clean before confirmation.
- Jettison during demand: `MatchesSnapshot` allows fewer units; compliance
  confiscates only remaining live quantities (possibly zero). The
  compliance notice was generic ("Fine received. Cargo confiscated.")
  and never reported actual quantities (defect 5).
- Compliance: `TryResolve(Comply)` + live `TryConfiscateCargo`, `Cleared`,
  coordinator clears interception, no fugitive. Legal cargo survives.
- Refusal/timeout/leaving 4200/trade-lane/docking/scanner destroyed by
  hostile action/player attack: `Enforcement` + `BeginPursuit`, interceptors
  converted to `FugitivePursuit`, hold-fire released.
- Scanner destruction (non-hostile): demand cleared, no fugitive. A
  **despawned** (removed, not destroyed) scanner kept a stale
  `ActiveScanner` reference in scan state (defect 6).
- Fugitive escape: incident resolves, cargo retained, and the next traffic
  tick could instantly re-detect with the same officer — a mechanical
  same-tick loop with no separation chance (defect 7).
- Save: F6 quick-save is permitted in every stop state (no gating).
  `CaptureSaveData` persists cargo, credits, reputation, non-fugitive
  hostility, missions, pods, markets — never NPC references, scan offers,
  hold-fire, or fugitive incidents. `ApplySaveData` resets scan,
  coordinator, and fugitive, then reloads the system (which resets traffic
  enforcement). Load therefore always discards the transient stop.
- System transition / docking / undocking: scan + coordinator + fugitive
  reset; docking during demand auto-refuses first (flight), then resets.

## UX state transitions (all one-shot, never per-tick)

1. **Ordered stop / interception** (new, coordinator-owned):
   "Liberty Police stop order — hold position for inspection", announced
   once per encounter while the stop is a pure interception (scan `Idle`,
   no fugitive). The scan-start message already covers the case where the
   officer begins inside scan range, so no duplicate is emitted.
2. **Scanning** (existing system, reworded):
   "Liberty Police cargo scan in progress — hold position". HUD status line
   shows live `Inspection x% (t/3.5s)` in light sky blue.
3. **Contraband detected / demand** (existing system, extended):
   "Contraband detected — [Enter] Comply / [N] Refuse (8s)". HUD status
   line renders the authoritative
   `PoliceScanSystem.EnforcementDemandRemainingSeconds` countdown plus an
   insufficient-credits note; no duplicate timer variable exists.
4. **Cleared**: "Inspection complete: cargo clean", or the new compliance
   summary (below), in lime.
5. **Refusal / pursuit** (unchanged): "Suspect refuses inspection order.
   Weapons free." / "Suspect is fleeing. Pursue." plus the fugitive
   `LIBERTY POLICE PURSUIT` HUD line.

## Interception pacing

- One bounded change in `NpcShip.UpdateEngagementBehavior`: a
  `ContrabandEnforcement` interceptor uses a 240 minimum closing speed
  through the existing `MoveTowardTarget` pipeline (turn rate, afterburner,
  cruise, arrival deceleration untouched). No bespoke flight model.
- The Phase 71 hold-fire policy is unchanged: `ContrabandEnforcement` +
  hold-fire + non-hostile reputation skips player fire; any independent
  hostility (faction disposition, temporary hostility/fugitive,
  retaliation) still fires.
- Scanner validity still accepts `ContrabandEnforcement` intercept
  movement; all other engaged states still invalidate the lock.
- Ownership flicker fix: the deterministic scanner tie-break now hashes
  faction + name + stable identity only (position removed), so two
  equidistant officers keep a stable owner while closing. Distance remains
  the primary key; the live scan owner is always retained.

## Supporter-spacing policy

- Owner keeps the exact player anchor (`Vector3.Zero` offset) so scan
  range is reached without detour.
- Every other live `ContrabandEnforcement` interceptor chases a ring
  standoff at `SupportStandoffRadius` 700 with a ±120 identity-derived
  vertical spread — near enough to stay useful and inside scan/detection
  ranges, far enough to avoid stacking.
- Angles are evenly spaced (`basePhase + 2π·i/N`) over the
  StableIdentity-ordered enforcer list with a stable-identity base phase,
  so N supporters never share an anchor and the layout is deterministic
  across runs.
- Owned by `PoliceContrabandStopCoordinator` (the existing encounter
  owner), applied through `NpcShip` retention onto the live player anchor
  via the existing engagement movement. No formation AI, no squad
  framework, no world-wide search, no per-frame allocation (indexed loops
  over the ≤16 enforcer list + one scan owner).
- The offset is transient runtime state on `NpcShip` (like hold-fire):
  assigned every coordinator tick, applied only while the
  `ContrabandEnforcement` reason is retained, cleared by
  `SetPlayerTarget`/`ClearEncounterState`/prune/escalation. Never save
  data.

## Demand UI behavior

- Controls remain exactly `Enter` = comply, `N` = refuse (no alternates).
- The HUD status line is the continuous countdown surface (per-frame
  `DrawString` of existing `StatusText`, not notification spam).
- The one-shot demand notice now names both keys and the 8s window.

## Live-hold compliance behavior

- Resolution still re-evaluates the live hold (`TryResolve` +
  `TryConfiscateCargo`); economic policy untouched.
- The compliance notice now reports the authoritative result:
  "Surrendered {ConfiscatedQuantity} illegal units — fine {CreditsCharged}
  CR paid. You're free to go.", or "No illegal cargo remaining — fine
  {CreditsCharged} CR paid. You're free to go." when everything was
  jettisoned after detection.
- Truthful special cases preserved: partial quantities confiscate only
  what remains; zero-remaining charges only the fine; legal cargo
  untouched; mission reservations flow through the existing confiscation
  authority and mission notifications; insufficient credits still cannot
  comply (must refuse/flee/time out).

## Evasion/reacquisition policy

- Refusal/timeout/flight/attack still become fugitive pursuit immediately.
- The fugitive incident remains authoritative while active.
- `PoliceFugitiveManager.ResolveEscape` now opens a bounded 20s
  `ContrabandReacquisitionGraceSeconds` window.
  `TrafficManager.UpdatePlayerContrabandEnforcement` skips only *fresh*
  `ContrabandEnforcement` acquisition while the grace is active; pruning
  of stale targets continues.
- The grace never blocks a new incident: `BeginPursuit` clears it, so a
  refusal, attack, or flight during the window still escalates at once.
- A system transition clears the grace through the existing fugitive
  reset, so a new system naturally constitutes a new encounter.
- No NPC reference is serialized; the timer is transient and never
  persisted. There is no permanent immunity: after the window, remaining
  contraband re-detects as a legitimate new encounter.

## Save/load policy (discovered, then proven)

- Roguelancer permits saving during interception, active scan, demand,
  and fugitive pursuit (F6 has no state gating).
- Load behavior is policy **C (safely restart/clear the transient scan
  portion)** with **B-style deterministic reselection** for the next
  encounter: `ApplySaveData` resets scan/coordinator/fugitive and the
  system reload resets traffic enforcement. Durable cargo, credits,
  reputation standings, and non-fugitive hostility are restored; fugitive
  temp hostility is intentionally not persisted (capture and apply both
  filter `FugitivePursuitReason`), matching the transient-incident design.
- Post-load guarantees: no stale `NpcShip` reference, no dead scan owner
  (scan `Idle`, offer null), no duplicate enforcement offer, no duplicate
  fine/confiscation (resolution without an offer fails closed), no phantom
  demand, no crash, no permanent hold-fire (cleared with encounter
  state), no accidental hostility loss beyond the designed transient
  fugitive drop (standing penalties persist).
- Owner reconstruction: the exact pre-save officer is never restored;
  the next tick deterministically reselects among live officers (live scan
  owner, otherwise nearest interceptor, name/identity tie-break, bounded
  to the enforcer list). A missing pre-save scanner therefore degrades to
  a clean idle stop that re-detects normally.
- New scan hardening: a scanner that left the world list (despawn) now
  clears transient scan state without a fugitive, in both `Scanning` and
  `ContrabandDetected` (world cleanup is never hostile action).

## Persistence boundaries

- No schema change (`CurrentSchemaVersion` stays 13). Persisted: cargo
  stacks (incl. mission reservations/stolen provenance), credits,
  reputation, non-fugitive hostility, missions, pods, markets, shipments.
- Transient (never persisted): `NpcShip` references, scan owner/offer/
  timers, supporter list and spacing offsets, hold-fire flags, fugitive
  incident and reacquisition grace, HUD/notification state, cached
  distances.

## Performance bounds

- Detection: unchanged spatial cells, ≤64 candidates/tick, ≤16 enforcers.
- Coordinator: ≤16 enforcers + 1 scan owner per tick; spacing adds O(16)
  arithmetic with no allocation.
- Scan: one O(n) world-membership loop only while a scan/demand is live.
- No world-wide NPC scans, no per-NPC stop state beyond one transient
  offset vector, no per-frame string generation for HUD (status text is
  read on draw; notifications are edge-triggered).

## Test results

`--phase72-smoke`: 30/30 (detection, hold-fire, hostile fire, approach to
scan range, scan progress, single notifications, demand + controls +
authoritative timer, single owner, no duplicate demand, deterministic
non-stacking supporters, live confiscation, legal survival, no phantom
jettison cargo, exact compliance feedback, refusal/flee pursuit,
acceleration-not-refusal, attack escalation, escape grace, new-encounter
redetection, reset, owner destruction, player death, pre-demand save/load,
demand save/load single-charge, deterministic reselection, missing
scanner, no persisted runtime refs).

Regression matrix (all green, build `dotnet build Roguelancer.csproj -c
Debug` with 0 errors and no new warnings — 128 pre-existing warnings
unchanged): `--phase68-smoke`, `--phase69-smoke`, `--phase70-smoke`,
`--phase71-smoke`, `--phase72-smoke`, `--police-enforcement-smoke`,
`--police-fugitive-smoke`, `--contraband-smoke`, `--all-smoke`.

## Remaining limitations

- The ordered-stop notice is text-only (existing notification queue); no
  audio/directional indicator.
- Supporters hold a flat ring, not terrain/formation-aware offsets.
- The 20s grace is a fixed constant, not reputation- or heat-scaled.
- A demand that outlives its scanner via timeout still escalates to a
  fugitive incident with zero local contacts (resolves via normal evasion
  timers); this is safe but produces a brief pursuit notice with no
  pursuers.
