# Phase 78 — Lawful Fugitive Surrender

Outcome A: a deliberate, edge-triggered **S-key** action lets a player who is
actively fugitive from Liberty Police stop running and surrender to a nearby
Liberty Police unit. The canonical `PoliceFugitiveManager` remains the single
fugitive authority before, during, and after surrender. The transaction is
atomic, applies only a bounded Heat-derived fee, confiscates no cargo, changes
no durable reputation, and creates no arrest record, warrant, criminal
history, detention state, Heat 3, or persisted surrender state. Phase 77
Liberty Police docking restrictions disappear naturally the moment
`PoliceFugitiveManager.IsActive` becomes false.

> **Phase 78 resolves existing fugitive state and adds no arrest record,
> warrant, criminal history, detention state, or persisted surrender state.**

The guiding rule: *Running from Liberty Police should be a choice the player
can stop making, but surrender must resolve the pursuit that actually
exists—not invent a record of crimes the game never stored.*

## Fugitive / enforcement architecture discovered

- **Single fugitive authority** — `PoliceFugitiveManager`
  (`PoliceFugitiveManager.cs:29`) owns the one bounded current-system fugitive
  incident. `PoliceFugitiveState` is `None/Pursued/Evading`;
  `PoliceHeatLevel` is `None/Pursuit/HotPursuit`. `IsActive`
  (`PoliceFugitiveManager.cs:71`) is `State != None`, so both `Pursued` and
  `Evading` are active. `BeginPursuit` opens/refreshes the incident and records
  fugitive-owned temporary hostility under the reason
  `"police fugitive pursuit"`. `NotifyPlayerDamage` escalates to Heat 2.
  `ResolveEscape` clears the incident, clears pursuit targets, releases
  fugitive-owned temporary hostility, and opens the Phase 74 post-escape
  contraband reacquisition grace. `Reset` is the destructive lifecycle clear
  (death/system transition/save-load).
- **Enforcement transaction** — `PoliceEnforcementService`
  (`PoliceEnforcementService.cs:130`) owns the first-pass law-enforcement
  rules: `Evaluate` produces a read-only offer (contraband findings, fine,
  tier, affordability) and `TryResolve` applies Comply (confiscate + fine +
  −0.03 standing) or Refuse (−0.20 standing + temporary hostility). The scan
  offer is single-owner and is cleared the moment the scan enters its
  `Enforcement` state, so **no live authoritative fine remains attached to an
  active fugitive incident**.
- **Scan / demand lifecycle** — `PoliceScanSystem` (`PoliceScanSystem.cs:22`)
  is the single scan/demand owner. `HandleInput` maps Enter (comply) / N
  (refuse) only while a `ContrabandDetected` demand is active. Refusal,
  timeout, flight, or scanner destruction converts the encounter to fugitive
  pursuit via `PoliceFugitiveManager.BeginPursuit`.
- **Encounter coordinator** — `PoliceContrabandStopCoordinator`
  (`PoliceContrabandStopCoordinator.cs:24`) connects Phase 70 interception to
  the scan/demand and fugitive systems. On escalation it converts
  `ContrabandEnforcement` interceptors to `FugitivePursuit` and releases
  hold-fire so normal pursuit/combat resumes.
- **Docking policy** — `FactionAccessService.EvaluateDocking`
  (`FactionAccess.cs:124`) is the single shared docking seam. Phase 77 wired
  the fugitive authority into it: Liberty Police docking is denied while
  `fugitivePursuitActive` is true. The resolver reads only
  `PoliceFugitiveManager.IsActive`.
- **Hostility model** — `ReputationManager.IsHostile` (permanent standing at
  or below −0.60) is the durable hostility boundary.
  `TemporaryHostilityManager` holds one transient entry per faction keyed by
  reason: `"police fugitive pursuit"` (fugitive-owned), `"police enforcement
  refusal"`, and `"player attack"` (player-caused). `Clear(factionId)` removes
  the single entry regardless of reason.
- **NPC targeting** — `NpcShip` (`NpcShip.cs`) exposes `SetPlayerTarget`,
  `ClearEncounterState`, `HasPlayerTarget`, `HasValidPlayerTarget`, and the
  `NpcPlayerTargetReason` enum (`None/FactionDisposition/PlayerInitiatedAggression/FugitivePursuit/ContrabandEnforcement`).
  `ClearEncounterState` returns an officer to ordinary cruising authority.
- **Game loop** — `RoguelancerGame.Update` runs the scan `HandleInput`, the
  J-key jettison, the scan update, and the coordinator update in the main
  flight path. The fugitive manager's per-frame `Update` runs in the
  jump/trade-lane transit paths; in the main path the incident is driven by
  `NotifyPlayerDamage`, `BeginPursuit`, the scan system, and the coordinator.

## Surrender interaction selected

A small contextual **S-key** action in the main flight path
(`RoguelancerGame.cs:3880`), guarded by
`_policeFugitiveManager?.IsActive == true` and edge-triggered against
`_prevKeys` (the same pattern as the J-key jettison). It calls
`PoliceFugitiveSurrenderService.TrySurrender` and shows one failure
notification when the transaction declines.

- The action is **intentional**: a discrete key press. Passive proximity to
  Police never surrenders automatically.
- It is **contextual**: the HUD shows `SURRENDER TO LIBERTY POLICE [Keys.S] —
  <fee> CR` only while `IsActive` (an O(1) query). The full eligibility check
  (Police presence, hostility, credits) runs only on the explicit key press,
  never per frame.
- No new input subsystem was added; no existing comply/refuse key was
  overloaded. The S key is otherwise unbound.

## Canonical eligibility rules

`PoliceFugitiveSurrenderService.Assess` evaluates, in order:

1. **Active fugitive** — `PoliceFugitiveManager.IsActive` must be true.
2. **Player survival** — the player ship must exist and not be destroyed.
3. **Hostility boundary** — permanent Liberty Police hostility
   (`ReputationManager.IsHostile`, standing ≤ −0.60) makes surrender
   unavailable. Temporary hostility does **not** block eligibility: the
   fugitive incident itself creates it, and any player-caused transient
   hostility is preserved (never cleared) by the resolution authority.
4. **Police presence** — `PoliceFugitiveManager.FindSurrenderPresence` must
   return a live Liberty Police actor (see below).
5. **Affordability** — `PlayerCredits.CanAfford(fee)` must be true.

The first failing check determines the player-facing reason. No check reads
cargo contents, mission state, reputation tier, or any history.

## Police-presence authority

`PoliceFugitiveManager.FindSurrenderPresence(npcs, playerShip)`
(`PoliceFugitiveManager.cs:309`) is the smallest authoritative local query. It
returns the **nearest live Liberty Police actor within the same acquisition
radius the pursuit itself uses**, reusing the exact candidate filter from
`AcquireNearbyPolice`:

- canonical faction id `liberty_police` (via `FactionManager.NormalizeFactionId`),
  never ship name or model text;
- not destroyed;
- not in trade-lane transit;
- not on a mission hold position;
- within `AcquisitionRadius` (10 000 at Heat 1, 14 000 at Heat 2).

It never searches beyond the world NPC list, never scans stations, and never
treats a non-Police faction as a surrender recipient. A `null` result means no
Liberty Police unit is available to accept surrender. The candidate cap is the
world NPC list (the same bounded list the fugitive manager's own `Update`
iterates every frame).

## Pursued / Evading behavior

- **Pursued** — surrender is allowed whenever a valid Police presence exists
  (the normal case while officers are in contact).
- **Evading** — if Police contact has been lost and no Liberty Police actor is
  within the acquisition radius, `FindSurrenderPresence` returns null and
  surrender is unavailable; it cannot magically resolve the pursuit from
  anywhere. If a Liberty Police actor is again within the radius while the
  state is still active, surrender is permitted.
- **Fully escaped** — `IsActive == false`, so surrender is unavailable. The
  post-escape reacquisition grace is a contraband re-detection suppressor, not
  a surrender eligibility state.

## Hostility boundary

- **Active fugitive + lawful/non-hostile Police relationship** → surrender
  available.
- **Active fugitive + independently hostile Liberty Police relationship**
  (permanent standing ≤ −0.60) → surrender unavailable; the existing
  hostility/combat authority is preserved. The standing is never reset.
- **Direct player aggression** — a player attack records temporary hostility
  under `"player attack"` through the existing
  `FactionCombatConsequenceService`. `ResolveSurrender` clears **only**
  fugitive-owned temporary hostility (reason `"police fugitive pursuit"`); any
  other transient hostility is preserved, never improperly cleared. A player
  who shoots an officer and then surrenders resolves the pursuit but keeps the
  combat consequences of the attack.

## Consequence model

The only consequence is a **bounded, deterministic surrender fee** derived
from the canonical current fugitive Heat:

| Fugitive Heat | Surrender fee |
| ------------- | ------------- |
| Heat 1 (`Pursuit`) | 1 000 CR |
| Heat 2 (`HotPursuit`) | 2 500 CR |

- The fee is a direct consequence of the live incident state, not a second
  enforcement authority. No Heat 3 exists or was added.
- The fee is **not** persisted, never becomes debt, and never creates a fine
  ledger.
- No cargo is confiscated and no cargo is fabricated. Surrender is not a
  scanner; the existing `PoliceEnforcementService` confiscation authority is
  not invoked. If the player is still carrying contraband, ordinary future
  scanning applies unchanged.
- **No reputation change.** The refusal/pursuit already imposed its durable
  penalty; surrender adds none and never improves standing.

## Credit / affordability behavior

- If `PlayerCredits.CanAfford(fee)` is false, surrender is unavailable and the
  player is told: `Unable to surrender: insufficient credits for the assessed
  penalty.`
- The fugitive state, credits, and cargo are left completely unchanged — no
  partial payment, no negative credits, no silent debt.
- Affordability is revalidated through the atomic `PlayerCredits.RemoveCredits`
  immediately before the pursuit resolves, so the commit is all-or-nothing.

## Reputation behavior

Surrender applies **no** reputation change. It never increases standing and
never resets durable standing. Running from Police and then surrendering is
not a reputation-reset exploit: the durable penalty from the original
refusal/pursuit remains exactly where the existing enforcement authority put
it.

## Contraband behavior

Surrender never inspects, confiscates, or fabricates cargo. The conservative
model is deliberate: during an active fugitive incident there is no live
authoritative confiscation context (the scan offer is already resolved), so
surrender must not reconstruct one. Proven by test: contraband is not
fabricated, legal cargo is never confiscated, and any contraband aboard
survives the surrender untouched.

## Smuggling-contract behavior

Mission-bound contraband follows the exact same cargo authority. Surrender
does not special-case smuggling missions and does not confiscate mission cargo,
so no mission failure is triggered by the surrender itself. If a future live
confiscation authority removes mission cargo, the existing mission/cargo
failure behavior would apply naturally; Phase 78 adds no such path.

## Direct-aggression behavior

A fugitive incident caused or accompanied by a direct attack on Police keeps
its existing combat semantics. `ResolveSurrender` clears only fugitive-owned
temporary hostility and the pursuit targets; player-caused transient hostility
(`"player attack"`) and any durable standing penalty from the attack are
preserved. The player cannot use surrender to erase the consequences of
shooting an officer.

## Fugitive-resolution mechanism

`PoliceFugitiveManager.ResolveSurrender(log, presentation)`
(`PoliceFugitiveManager.cs:375`) is the narrowly named authoritative
resolution method. It reuses the same internal invariant restoration as
`ResolveEscape` — `ClearPursuitTargets()` (officers return to ordinary
cruising authority and stop firing), incident timers cleared, fugitive-owned
temporary hostility released, observed player-damage attribution cleared —
with two deliberate differences:

1. **Surrender is not an escape**, so no post-escape contraband reacquisition
   grace is opened. Ordinary Police behavior resumes immediately.
2. **Only fugitive-owned temporary hostility is released.** Unrelated
   transient hostility is preserved.

Durable reputation is untouched, and no unrelated system is reset. The
optional `presentation` text is surfaced through the existing one-notification
`Present` surface, so the player receives exactly one bounded success message.

## Escape-grace distinction

- **Escape** (`ResolveEscape`) → existing Phase 74 post-escape contraband
  reacquisition grace.
- **Surrender** (`ResolveSurrender`) → **no** grace. After surrender the
  player is an ordinary non-fugitive vessel; if they still carry contraband,
  normal future scanning may occur according to the existing rules.

Proven by test: `IsContrabandReacquisitionGraceActive` is false after a
successful surrender.

## Phase 77 docking integration

Phase 77 requires no new special-case logic. Before surrender
`PoliceFugitiveManager.IsActive == true`, so Liberty Police docking is denied.
After a successful surrender `IsActive == false`, so the Phase 77 restriction
disappears naturally through the existing
`FactionAccessService.EvaluateDocking` policy — no Phase 77 code is called to
manually "unlock" stations. The docking resolver reads only `IsActive`.

## Atomicity / idempotency

A successful surrender performs one bounded transaction: eligibility
revalidated → Police presence revalidated → affordability revalidated → fee
charged atomically → fugitive pursuit resolved → one notification. No partial
success: if any required step fails before commit, the player remains fugitive
with no fee, no confiscation, and no reputation change.

Idempotency is guaranteed by the state authority itself: after a successful
surrender `IsActive == false`, so a second attempt is a harmless no-op — no
second fee, no second confiscation, no second reputation change, no duplicate
pursuit resolution.

## Death / reset behavior

Existing death/reset behavior is unchanged. `PoliceFugitiveManager.Update`
auto-resets when the player hull is destroyed, and the system-transition reset
clears local heat. After either, `IsActive == false` and surrender is
unavailable. No deferred Phase 78 transaction fires afterward.

## System-transition behavior

The existing fugitive lifecycle clears local heat on system transition.
Surrender availability follows that authority; no Phase 78 request is carried
across systems.

## Save / load behavior

No surrender state is persisted. The fee is transient, no "previously
surrendered" flag exists, and no surrender history is written. The existing
fugitive state is not persisted either (the fugitive-pursuit hostility is
filtered from saves and no fugitive field exists in `SaveGameData`). A
save/load cycle therefore creates no surrender prompt and replays no surrender
consequence. The save schema version remains **13**; no Phase 78 save field
exists (verified by reflection over `SaveGameData`).

## Performance / bounds

- O(1) fugitive query (`IsActive`).
- O(1) hostility/reputation checks (`IsHostile`).
- Bounded nearby-Police resolution: one pass over the world NPC list (the same
  bounded list the fugitive manager's own `Update` iterates), candidate cap =
  world NPC count.
- O(1) transaction work (one credit removal, one state resolution, one
  notification).
- No per-frame polling for surrender: the HUD prompt reads only `IsActive` and
  `Heat` (both O(1)); the full eligibility check runs only on the explicit
  S-key press. No world-wide searches, no per-Police surrender state, no
  unbounded request queue, no history maintenance.

## Player-facing messaging

All messages reuse the existing `NotificationManager.ShowMessage` / HUD
surfaces and expose no Heat enums, state-machine names, or reputation
thresholds:

| Context | Message |
| ------- | ------- |
| Available (HUD, while `IsActive`) | `SURRENDER TO LIBERTY POLICE [Keys.S] — 1,000 CR` |
| Success (Heat 1) | `Surrender accepted. 1,000 credits assessed. Liberty Police pursuit ended.` |
| Success (Heat 2) | `Surrender accepted. 2,500 credits assessed. Liberty Police pursuit ended.` |
| Insufficient credits | `Unable to surrender: insufficient credits for the assessed penalty.` |
| No Police nearby | `No Liberty Police unit is available to accept surrender.` |
| Permanently hostile | `Unable to surrender: Liberty Police are hostile.` |

The success message is produced exactly once (through the fugitive manager's
`Present` surface). Failure messages are produced once per explicit attempt.
Passive eligibility reads produce no notifications.

## Testing

`--phase78-smoke`: **55 passed, 0 failed**
(`Phase78LawfulFugitiveSurrenderSmokeTest.cs`, wired into `RoguelancerGame.cs`
as flag, dispatch, runner, and `--all-smoke` entry).

Covers all 50 required items plus architecture-specific proofs: canonical
faction-id usage (a ship named "Liberty Police" but factioned Navy is not
accepted); the accepting officer is always a Liberty Police actor; the fee is
derived only from the canonical current Heat; the success notification is
exactly one and matches the result message; the Phase 76 transition notifier is
not invoked; the Phase 75 presentation and Phase 77 docking policy remain
side-effect free; and no criminal-history authority exists (reflection over
the service's members).

Regression suites, all green:

- Phase 68 (14), 69 (16), 70 (15), 71 (22), 72 (30), 73 (40), 74 (40),
  75 (20), 76 (40), 77 (45).
- Police enforcement (30), police fugitive (65), contraband 52/53 (23),
  reputation (102), mission (9), freight (40).
- Dock (11), faction docking access (18), traffic (9).
- `--all-smoke`: **78 suites passed, 0 failed** (includes Phase 78).
- `dotnet build -c Debug`: 0 errors, **128 warnings** (baseline unchanged; no
  new warnings).

## Limitations (out of scope for Phase 78)

No jail, prison, arrest cutscene, station detention, forced landing, impound,
warrants, criminal history, bounty ledger, outstanding-fine ledger, debt, bail,
lawyers, court, Police dispatch redesign, Police radio, Heat 3, Police
attention tiers, permanent surrender flags, surrender reputation rewards,
broad hostility reconciliation, save-schema changes, or universal faction
surrender. The fee is a fixed Heat-derived amount, not a percentage of cargo
value or a reconstructed historical fine. Surrender is Liberty Police-specific
and requires a live Liberty Police actor; it is not a universal faction
mechanic. A player who is permanently hostile to Liberty Police (standing
≤ −0.60) cannot surrender and must resolve the hostility through the existing
reputation-recovery systems.

## Recommended Phase 79

Extend the same bounded pattern to a visible pre-surrender confirmation or a
short surrender approach: when the player presses S, the accepting officer
could briefly hold fire through the existing `NpcShip.SetLawfulStopHoldFire`
gate (reusing the bounded `PoliceContrabandStopCoordinator` hold-fire seam) so
the same-tick transaction cannot be interrupted by a stray weapon hit — still
with no new persistent state, no new criminal authority, and no schema change.
Alternatively, a bounded "surrender loiter" that keeps the autopilot holding
at a safe distance from Liberty Police units while the player decides, reading
only the existing fugitive authority.
