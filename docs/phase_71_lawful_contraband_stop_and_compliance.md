# Phase 71 — Lawful Contraband Stop and Compliance

Phase 71 connects Phase 70 detection to the existing police scan, demand,
confiscation, and fugitive systems so contraband encounters become coherent
player-facing law-enforcement interactions rather than immediately becoming
combat encounters.

Production proof:

`lawful detection (ContrabandEnforcement intercept, hold-fire) -> close to
ScanRange -> PoliceScanSystem scan/demand (single owner) -> comply via
PoliceEnforcementService + CargoHold.TryConfiscateCargo (resolve, no
fugitive) OR refuse/timeout/evade/attack -> PoliceFugitiveManager pursuit
(FugitivePursuit, normal weapons/combat)`

## Architecture and ownership

- `Commodity.IsContraband` remains the one canonical legality
  classification. No second list. Legal stolen goods alone never count for
  stop detection (scan demand may still cite stolen goods per existing
  `PoliceEnforcementService` semantics; the stop trigger itself is
  contraband-only).
- `CargoHold` (`Ship.CargoHold`) remains the sole cargo owner. No shadow
  state. No direct field mutation. Compliance uses
  `PoliceEnforcementService.TryResolve` plus
  `CargoHold.TryConfiscateCargo`. Jettison uses
  `LootManager.TryJettisonContraband` physical pods.
- `ContrabandEnforcementPolicy` remains the read-only detection policy
  (`HasPlayerContraband`, `IsValidPlayerTarget`). No duplicate scan logic.
- `TrafficManager.UpdateFactionCombatEngagements` /
  `UpdatePlayerContrabandEnforcement` remains the bounded detection owner
  (spatial cells, at most 64 candidates per tick, at most 16 enforcers,
  nearest/name/identity ordering). Acquisition calls
  `SetPlayerTarget(pos, ContrabandEnforcement)`.
- `NpcShip` owns encounter state, movement, retention, damage, and
  destruction. New transient `IsLawfulStopHoldFire` flag distinguishes
  interception from hostility. It is never save data and is cleared by
  `ClearEncounterState`, escalation conversion, or coordinator reset.
  Retention for `ContrabandEnforcement` remains cargo-backed.
- `PoliceScanSystem` remains the single scan/demand authority (ScanRange
  3200, CancelRange 3800, ScanDuration 3.5s, EnforcementDemandSeconds 8s,
  EnforcementEscapeRange 4200, RetryCooldown 25s). Phase 71 relaxes its
  eligibility only for `ContrabandEnforcement` interceptors: they may own a
  scan while in `AttackingPlayer` intercept movement. Other player targets
  (faction, retaliation, fugitive) are never claimed for a peaceful demand.
  Valid faction combat targets retain priority over a new scan.
- `PoliceContrabandStopCoordinator` is the one small transient encounter
  owner (at most 16 enforcers plus one scan owner per tick). It sets
  hold-fire, retains the deterministic single owner (live scan owner,
  otherwise nearest interceptor with name/identity tie-break), clears
  interception on compliance/clean scan, and converts interception to
  `FugitivePursuit` on escalation. It creates no cargo, weapons, or UI.
- `PoliceFugitiveManager` remains the fugitive authority (Heat 1/2,
  acquisition radii 10k/14k, escape 15s/25s, absolute bound 180s). Refusal,
  timeout, flight, trade-lane escape, explicit refuse input, scanner
  destroyed by hostile action, and player attack all funnel here.
- `NpcWeaponSystem` remains the only NPC projectile/damage path. New gate:
  `ContrabandEnforcement` plus hold-fire plus non-hostile reputation skips
  player fire. Hostile (faction disposition, temporary hostility/fugitive,
  retaliation) still fires. Phase 70 isolated harnesses never set hold-fire,
  so their firing proof is preserved; production stops hold fire pending
  demand.
- `SaveGameData` requires no new fields. Transient pursuit, validator,
  hold-fire, owner, scan, and fugitive state rebuild or discard at teardown
  like Phase 69/70.

## Detection versus confirmed enforcement

- Detection/suspicion: lawful NPC within `TrafficActivationRange`
  (default 6500) sees live contraband in the player hold and becomes a
  `ContrabandEnforcement` interceptor (hold-fire, closes via existing
  `NpcShip` engagement movement, no immediate contraband-only fire).
- Confirmed contraband: `PoliceScanSystem` completes its 3.5s inspection
  within 3200 and `PoliceEnforcementService.Evaluate` finds a violation.
  Only then does the existing surrender/demand open (Enter comply, N
  refuse, 8s timer).
- Escalation: refusal, timeout, leaving 4200, trade-lane/docking flight,
  scanner destroyed by hostile action, or player attack triggers the
  existing fugitive/aggression path. Combat follows because the player
  refused or evaded lawful enforcement, not merely because contraband was
  present at detection range.

## Scan trigger rules

- Reuses live constants: `ScanRange 3200`, `CancelRange 3800`,
  `ScanDurationSeconds 3.5`, `EnforcementDemandSeconds 8`,
  `EnforcementEscapeRange 4200`.
- New scan requires: lawful `LawfulPatrol` officer, live, not trade-lane,
  not mission hold, Police not currently hostile, within 3200, cruising or
  contraband-intercepting, no other valid player reason, no valid faction
  target.
- Active scan retention allows contraband intercept movement; other
  engaged states still invalidate. Out-of-range scan cancels with 5s
  cooldown; completed scan sets 25s retry cooldown.
- Scan operates against the real `CargoHold`. Legal cargo never produces a
  contraband demand (clean scan clears in 2s).

## Compliance behavior

- Uses `PoliceEnforcementService.TryResolve(Comply)` plus
  `CargoHold.TryConfiscateCargo` (or live equivalents).
- Removes only the demanded illegal quantities per existing rules;
  mission reservations reduce in the same operation and report via
  `MissionCargoConfiscation`.
- Preserves legal cargo. Cargo totals stay consistent (`UsedCapacity`,
  stolen aggregates, reservations).
- Resolves the demand (`Cleared`), clears interception targets immediately
  via the coordinator (plus cargo-backed traffic clearing next tick),
  creates no fugitive and no permanent hostility beyond the existing paid
  compliance standing penalty (-0.03).
- No shadow cargo. No direct hold mutation.

## Refusal and evasion (exact rules, no new UI)

Existing demand resolution only:

- explicit refuse input (`N`) calls `TryRefuseEnforcement`.
- demand timeout (8s) auto-refuses.
- leaves enforcement radius (4200) auto-refuses as flight.
- trade-lane transit or docking attempt during demand auto-refuses as
  flight (docking then resets the scan; fugitive persists per existing
  rules).
- scanner destroyed by hostile action (`WasDamagedByPlayer`) auto-refuses.
- player attacks the officer: monotonic `WasDamagedByPlayer` plus
  `TemporaryHostility` plus `PoliceFugitiveManager.NotifyPlayerDamage`
  begins/escalates pursuit; the coordinator converts interceptors to
  `FugitivePursuit` on the next tick.
- Player acceleration/flee without leaving 4200 does not by itself refuse;
  leaving the radius or timing out does. No new controls were added.

## Fugitive escalation behavior

- Uses `PoliceFugitiveManager.BeginPursuit` (already called by
  `PoliceScanSystem` on refuse paths).
- Coordinator converts every remaining `ContrabandEnforcement`
  interceptor to `FugitivePursuit` and releases hold-fire.
- Afterwards: NPC pursuit uses existing movement, firing uses
  `NpcWeaponSystem`, damage uses existing combat code, validity follows
  existing fugitive logic (`IsTemporarilyHostile`), disengagement uses the
  existing soft/hard leash (7.5k/10k, 30s stale). No parallel combat path.

## Multi-officer ownership policy

- One officer owns the active scan/demand (`PoliceScanSystem.ActiveScanner`).
- Other lawful officers may support/intercept but never open a second
  demand. Selection is deterministic: retain the live scan owner; otherwise
  nearest interceptor, then stable name, then stable NPC identity.
- Bounded to the existing enforcer list (16). No global coordination.
- Escalation may legitimately cause multiple Police NPCs to pursue through
  existing faction/fugitive acquisition.

## Jettison behavior

- `LootManager.TryJettisonContraband` remains the physical authority (also
  via `PoliceScanSystem.TryJettisonContraband` with `LootManager`).
- Cargo physically leaves the hold into real `CargoPod` objects; spawned
  pods are never deleted to simplify enforcement.
- The demand re-evaluates the live hold on resolution. `MatchesSnapshot`
  allows fewer units (jettison too late to invalidate the citation) but
  forbids new stacks. Compliance confiscates only remaining live violating
  quantities (possibly zero); phantom cargo is never confiscated.
- Post-detection jettison does not clear the demand (`Cargo dumped — Police
  demand remains active`). The fine path remains per existing service
  (insufficient credits cannot comply; must refuse/flee/time out).
- Police pod seizure remains owned by Phase 69 / `LootManager`
  (`TrySeizeContrabandPodForNpc`).

## Lifetime, reset, despawn, death, load behavior

- Compliant resolution: demand satisfied -> coordinator plus traffic clear
  interception; scan holds `Cleared` 2s then idles.
- Clean cargo before confirmation: scan completes to `Cleared`; no demand,
  no fugitive; enforcers clear via cargo-backed retention.
- Fugitive escalation: interception yields to `FugitivePursuit`; fugitive
  timers own lifetime; evasion clears pursuit via existing logic. If cargo
  remains after evasion, a new bounded interception may re-detect.
- Enforcer destroyed: removed from transient set via
  `NotifyNpcDestroyed`; hold-fire pruned; scan owner destroyed by hostile
  action refuses to fugitive (other officers pursue), otherwise clears
  safely. Remaining enforcers allow deterministic re-scan or safe
  termination.
- Enforcer despawn (`ReleaseShip`, `UnregisterMissionNpc`): removed from
  transient set, validator and hold-fire cleared.
- Player destroyed: traffic clears enforcers next pass; coordinator resets
  the scan demand safely (no new fugitive); fugitive resets via its own
  death path; normal `Ship` destruction remains authoritative.
- Player leaves world/system: `HandleSystemChange` / `LoadZonesForSystem`
  resets scan, fugitive, coordinator, enforcers, validators, seizures
  without touching market stock, active pods, or player cargo.
- Reset: `ResetContrabandEnforcement` plus scan/fugitive/coordinator resets
  clear all transient state without mutating cargo.
- Load: no pursuit, validator, hold-fire, owner, or counter is persisted;
  stale NPC references are never restored. Post-load state uses current
  cargo/fugitive standing and re-detects locally if smuggling.

## Performance bounds

- Reuses the existing spatial/traffic lookup and bounded enforcer list.
- Coordinator inspects at most 16 enforcers plus one scan owner per tick;
  no world-wide Police scans, no unbounded NPC enumeration, no per-frame
  allocations proportional to traffic, no duplicate scan state per NPC.
- Encounter-owner state is one nullable reference plus one small set
  (purely transient, cleared on reset/teardown.

## Intentionally deferred

No wanted levels, crime heat, jail, arrest cutscenes, docking denial,
bribery, fines redesign, value-based penalties beyond the existing fine
formula, reputation loss redesign, smuggling missions, contraband dealers,
hidden compartments, scanner-blocking equipment, scan minigames, new
contraband commodities, new Police UI framework, or voice/audio. Phase 71
only connects Phase 70 interception to the existing scan/demand and
fugitive systems.

## Smoke coverage

`--phase71-smoke` runs 22 focused checks: detection without
contraband-only fire, close via movement, scan only within range, legal
negative, contraband demand, compliant removal, legal survival, compliance
clears interception, compliance creates no fugitive, refusal/timeout/
evasion escalates and converts to fugitive, fugitive enables combat,
player attack causes hostility, already-hostile not suppressed, jettison
no double confiscation, clean-before-completion resolves, single scan
authority, no duplicates on repeated ticks, owner destruction safe,
despawn/reset/death clears, plus Phase 68, Phase 69, and Phase 70
regressions.
