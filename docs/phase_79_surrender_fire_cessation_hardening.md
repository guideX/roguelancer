# Phase 79 — Accepted Surrender Fire-Cessation Hardening

Outcome A: once Liberty Police have **accepted a valid surrender**, officers
whose player targeting came only from the fugitive-pursuit authority cannot
start a **new** weapon discharge because of same-tick/update-order lag. The
canonical Phase 78 transaction remains the sole surrender authority. Phase 79
adds no invulnerability, deletes no projectile, cancels no damage, and creates
no persisted or criminal state. It is a combat/update-order correctness phase,
not a new surrender feature.

> **Phase 79 changes only post-acceptance firing authority. It does not make
> surrender an invulnerability mechanic and adds no criminal-state authority.**

The guiding rule: *Once Liberty Police accept the surrender, they should stop
pulling the trigger because of the pursuit that just ended — but bullets
already in the air and fights the player started are still real.*

## Exact stale-fire defect discovered

The defect is a real same-update re-arm, not a hypothetical:

1. A player who refuses a Liberty Police contraband stop becomes fugitive.
   `PoliceScanSystem.TryResolveEnforcement` sets `PoliceScanState.Enforcement`
   and opens the incident via `PoliceFugitiveManager.BeginPursuit`
   (`PoliceScanSystem.cs:444-466`). The resolved enforcement **result hold**
   persists for `ResultHoldSeconds = 2f` purely for presentation.
2. The player presses **S**. `PoliceFugitiveSurrenderService.TrySurrender`
   commits the Phase 78 transaction: fee charged, `ResolveSurrender` clears the
   incident, `IsActive` becomes false.
3. Later in the **same frame**, in production order, the
   `PoliceContrabandStopCoordinator` runs
   (`PoliceContrabandStopCoordinator.cs:172-194`). Its escalation condition is
   `scan.State == Enforcement || fugitive.IsActive`. The stale `Enforcement`
   result hold is still true, so the coordinator calls
   `fugitive.BeginPursuit(...)` and converts any `ContrabandEnforcement`
   officer to `FugitivePursuit` again.
4. `NpcWeaponSystem.Update` runs after the coordinator in the same frame and
   emits a new fugitive-authorized shot.

A focused reproduction against the production systems confirmed the defect:
before hardening, a successful surrender was followed by **6 new Police
projectiles in the same production-equivalent update** (`--phase79-smoke`
diagnostic). The surrender was also undone on the next tick because the stale
result hold re-opened the incident every frame for its remaining lifetime.

## Relevant game-loop order

Normal flight path in `RoguelancerGame.Update`:

1. `_playerShip.Update`
2. **S-key surrender** (`RoguelancerGame.cs:3900`) → `TrySurrender`
3. `_trafficManager.Update` → `PoliceFugitiveManager.Update` (target acquisition)
4. `NpcShip.Update` for every NPC (disposition/encounter reconciliation)
5. `_policeScanSystem.Update`
6. `_policeContrabandStopCoordinator.Update`
7. `_npcWeaponSystem.Update` (projectile update + new fire)

The surrender commit at step 2 precedes the weapon pass at step 7, so any
system between them that re-creates a fugitive target can still authorize fire
in the same tick.

## NPC targeting / fire architecture

- `NpcShip.HasPlayerTarget` requires `EncounterState == AttackingPlayer` and a
  non-`None` `NpcPlayerTargetReason`. `HasValidPlayerTarget` gates each reason:
  `FugitivePursuit` is valid while `TemporaryHostility.IsTemporarilyHostile`;
  `FactionDisposition` / `PlayerInitiatedAggression` are valid while
  `IsFactionCurrentlyHostile`; `ContrabandEnforcement` is cargo-backed.
- `NpcWeaponSystem.Update` is the only NPC projectile/damage path. It resolves
  a faction target first, then a valid player target, then a trade-lane target,
  and emits one projectile per mounted gun.
- Because a fugitive target is only valid while temporary hostility exists,
  clearing the fugitive reason normally invalidates the target. Phase 79 closes
  the gap where a later system re-creates that reason (or a target) **after**
  surrender resolution but **before** the weapon pass.

## Existing lawful-stop hold-fire architecture

Phase 71 introduced `NpcShip.IsLawfulStopHoldFire` (`NpcShip.cs`), consulted by
`NpcWeaponSystem` only for `ContrabandEnforcement` targets and only while the
faction is not currently hostile. It is set by
`PoliceContrabandStopCoordinator`, and cleared by `ClearEncounterState`,
escalation, and coordinator reset.

Reuse was evaluated and rejected for surrender:

- `ClearEncounterState` (invoked by `PoliceFugitiveManager.ClearPursuitTargets`
  during surrender) clears `_lawfulStopHoldFire`, so a Phase 71 hold cannot
  survive the very cleanup it must outlive for one weapon pass.
- The Phase 71 flag is only honoured for `ContrabandEnforcement`, so it cannot
  suppress a re-created `FugitivePursuit` shot.
- Reusing it would make surrender look like an active contraband stop, which
  Phase 79 must avoid.

## Implementation seam chosen

A dedicated, transient, self-consuming hold on `NpcShip`:

- `NpcShip.SetSurrenderFireHold(bool)` / `HasSurrenderFireHold` /
  `ConsumeSurrenderFireHold()` (`NpcShip.cs`). It is deliberately **not**
  cleared by `ClearEncounterState`, so it survives the pursuit-target cleanup in
  the same resolution, and it is consumed by exactly one weapon pass.
- `NpcWeaponSystem.Update` consumes the hold for every NPC before any early
  `continue`, then suppresses a `FugitivePursuit` player target for that pass
  unless an independent combat authority still exists. It never touches
  projectiles or damage resolution.
- `PoliceFugitiveManager.ApplySurrenderFireHold` arms the hold on the bounded
  `_pursuitTargets` set plus the accepting officer.
- `PoliceScanSystem.ClearResolvedResultHold` drops a completed
  `Cleared`/`Enforcement` result hold. `TrySurrender` calls it only after the
  transaction commits, so the stale presentational result cannot re-open the
  surrendered incident on the next coordinator pass.

## Successful-surrender transaction ordering

1. Validate eligibility (`Assess`) — unchanged from Phase 78.
2. Revalidate affordability and atomically charge the Heat-derived fee.
3. `ResolveSurrender`: arm the one-pass fire hold on the participants, clear
   pursuit targets, release fugitive-owned temporary hostility, clear timers,
   emit exactly one bounded success notification.
4. `ClearResolvedResultHold`: drop the resolved enforcement result hold so the
   incident cannot be re-armed later in the same tick or the next.

Nothing is armed before the transaction is known to succeed.

## New shot vs existing projectile

- **Existing projectiles are physical.** A projectile fired before surrender is
  never deleted, never despawned, never re-owned, and its damage is never
  zeroed. It continues to travel and can still hit and damage the player, any
  NPC, a trade-lane ring, or the environment.
- **New emission is gated.** The hold suppresses only the *emission* of a new
  fugitive-authorized shot for one weapon pass. It does not run during damage
  resolution.

## Independent-hostility behavior

`NpcWeaponSystem.HasIndependentPlayerCombatAuthority` is the smallest
authoritative query: durable faction hostility (`IsHostile`) or any surviving
**non-fugitive** temporary hostility
(`IsTemporarilyHostile && !HasReason("police fugitive pursuit")`). If either is
true, the officer is free to fire even during the hold. The surrendered
fugitive reason itself is not an independent authority.

## "player attack" behavior

Phase 78 preserves a surviving `"player attack"` temporary hostility. Phase 79
honours that: if the current transient reason is not the fugitive reason, it
counts as independent authority and Police combat continues. If the player
independently attacked Police, surrender does not pacify them.

## Failed-surrender behavior

If surrender is refused (no Police present, insufficient credits, permanent
hostility, destroyed player, or not fugitive), **nothing** is armed: no
hold-fire, no target clearing, no cease-fire, no fee beyond existing semantics.
The fugitive pursuit continues and Police remain free to fire. Only a committed
surrender arms the hold or clears the result hold.

## Participant / officer scope

The hold is bounded to the existing `_pursuitTargets` participant set plus the
single accepting officer — the same set `PoliceFugitiveManager` already uses for
pursuit cleanup. There is no world scan, no unbounded officer list, no
per-officer criminal record, and no persistent participant history. A distant
Liberty Police unit that was never a participant receives no hold.

## Lifecycle cleanup

- The hold is a single bool on each NPC and is consumed by one weapon pass, so
  it cannot accumulate.
- Officer death, despawn, reset, or world teardown drops the NPC (and its
  bool). The weapon pass consumes the hold even for a destroyed NPC, so a dead
  officer cannot retain stale state.
- `Reset` (death / system transition / save-load) clears the manager incident;
  no Phase 79 state is retained by the manager.
- System transition and save-load recreate NPCs, so no hold survives.

## Save / load behavior

No save-schema change. Schema remains `13`. The hold is a transient bool on
`NpcShip` and is never persisted. No `SaveGameData` member references surrender,
hold-fire, or cease-fire. Loading does not recreate a fire hold, does not replay
the surrender notification, and leaves a non-fugitive player in ordinary combat
state.

## Performance / bounds

- The hold is O(1) to set and O(1) to consume.
- Arming is bounded to the existing pursuit participant set plus the accepting
  officer; no allocation, no projectile search, no faction-wide log.
- No per-frame world-wide Police scan is added.
- `ClearResolvedResultHold` is an O(1) state reset.

## Focused proof (`--phase79-smoke`, 58 checks, all green)

The suite drives production systems in production order and proves: Heat 1/2
pursuit firing before surrender; same-tick cessation after Heat 1/2 surrender;
no new projectile from stale fugitive targeting; pre-surrender projectiles stay
alive, keep their owner and damage, and still damage the player; no player
invulnerability; Rogue/Navy/environmental damage unaffected; pursuit-target and
stale-authority cleanup; preservation of unrelated NPC-vs-NPC and
`"player attack"` authority; permanent-hostility boundary unchanged; every
failed-surrender path arms nothing; lawful-stop and refusal escalation
regressions; bounded input and passive updates; officer death/despawn/reset/
death/transition safety; save/schema checks; Phase 76/77 non-interference; and
the one-pass self-releasing hold.

Regression gates, all green: Phase 68 (14), 69 (16), 70 (15), 71 (22), 72 (30),
73 (40), 74 (40), 75 (20), 76 (40), 77 (45), 78 (55), 79 (58); police
enforcement (30), police fugitive (65), contraband 52/53 (23), reputation (102),
mission (9), freight (40), dock (11), faction docking (18), traffic (9),
missile (4), mine (5), weapon energy (27). `--all-smoke`: **79 suites passed, 0
failed**. `dotnet build -c Debug`: 0 errors, **128 warnings** (baseline
unchanged).

## Limitations

The hold suppresses emission for one weapon pass only; it is not a truce and
does not persist. It does not prevent a genuinely new incident from starting on
a later tick (for example, ordinary Police scanning may re-detect contraband
later under the existing retry cooldown). Surrender remains Liberty
Police-specific and requires a live Liberty Police actor. No jail, arrest,
warrant, bounty ledger, Heat 3, dispatch redesign, or global cease-fire is
added.

## Recommended Phase 80

Consider a bounded, read-only surrender approach/confirmation surface that keeps
the autopilot holding at a safe distance from Liberty Police while the player
decides, reading only the existing fugitive authority — still with no new
persistent state, no new criminal authority, and no schema change. A separate
candidate is making the resolved enforcement result hold self-expire the moment
its owning incident resolves, generalizing the Phase 79 cleanup without adding
new state.
