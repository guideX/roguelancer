# Phase 77 — Fugitive Docking Restrictions

Outcome A: Liberty Police-controlled facilities now refuse docking clearance
while the player is an active Liberty Police fugitive, and ordinary docking
returns the moment the canonical fugitive state clears. Phase 77 extends the
one shared docking access policy that already existed
(`FactionAccessService.EvaluateDocking`) and wires the existing fugitive
authority into the two autopilot call sites that were not yet fugitive-aware.
It creates no new crime state, wanted state, station blacklist, or persisted
docking restriction.

> **Phase 77 adds no new crime state, wanted state, station blacklist, or
> persisted docking restriction.**

The guiding rule: *Liberty authorities may refuse you entry while they are
actively chasing you, but the station must never invent its own memory of why
you are wanted.*

## Docking architecture discovered

- **Input flow** — F3 (`RoguelancerGame.Update`, `RoguelancerGame.cs:3632`)
  resolves a dock target (`ResolveDockAssistTarget`,
  `RoguelancerGame.cs:4542`) and calls `TryStartDockAssist`
  (`RoguelancerGame.cs:4563`), which delegates to
  `Ship.ActivateDockAssist` → `GotoAutopilot.Activate`.
- **Central access policy** — `FactionAccessService.EvaluateDocking`
  (`FactionAccess.cs:124`) is the single shared docking-eligibility seam. It
  already had a fugitive-aware overload
  (`FactionAccess.cs:132-165`) that denies Liberty Police docking while
  `fugitivePursuitActive` is true, and `StationDockUI.DockAtStation`
  (`StationDockUI.cs:115-119`) already passed a fugitive resolver into it.
- **The Phase 77 gap** — `GotoAutopilot` evaluated docking at two call
  sites without fugitive state: `Activate` (`GotoAutopilot.cs:159-162`) and
  the approach-phase revalidation `ExecuteDockingApproach`
  (`GotoAutopilot.cs:731-734`). A fugitive player could therefore be denied
  only at the final physical-docking boundary, after the autopilot had already
  committed to the approach.
- **Final authority** — `RoguelancerGame.HandleDockingCompleted`
  (`RoguelancerGame.cs:4604`) calls `StationDockUI.DockAtStation`, which
  re-evaluates the shared policy with the fugitive resolver before any
  station transition. This path was already correct and is unchanged.

## Station/faction ownership authority

- `Station.FactionId` (`Station.cs:28`) is the canonical, normalized
  ownership field, set from `StationConfig.FactionId` at construction
  (`Station.cs:61`). No station-name matching exists anywhere in the
  docking path.
- `FactionManager` (`FactionManager.cs:13-19`) defines the faction constants.
  `liberty_police` is a **separate faction** from `liberty_navy`,
  `liberty_rogues`, and `liberty_corporations`; there is no broad "Liberty"
  government faction. The covered set is therefore exactly the Liberty Police
  faction id, resolved from canonical data.

## Authoritative fugitive query

`PoliceFugitiveManager.IsActive` (`PoliceFugitiveManager.cs:71`,
`State != PoliceFugitiveState.None`) is the canonical current-state query.
Phase 77 derives nothing from Police reputation tier, current Police
standing, contraband possession, mission history, previous refusals, or
notification history. The resolver wired into both `StationDockUI`
(`RoguelancerGame.cs:1351-1353`) and, new in Phase 77, `GotoAutopilot`
(`RoguelancerGame.cs:1480-1482`) is:

```csharp
factionId => string.Equals(
        FactionManager.NormalizeFactionId(factionId),
        FactionManager.LibertyPolice,
        StringComparison.OrdinalIgnoreCase)
    && _policeFugitiveManager?.IsActive == true
```

## Covered facility policy

| Facility ownership | While actively fugitive |
| ------------------- | ----------------------- |
| Liberty Police (`liberty_police`) | **Denied** |
| Liberty Navy / Liberty Corporations | Unchanged (existing rules) |
| Liberty Rogues / criminal facilities | Unchanged (existing rules) |
| Neutral civilians | Unchanged |
| Any other faction | Unchanged |

The policy is the narrowest defensible one: the repository has no
Liberty-government ownership broader than the per-faction ids, so only the
Police faction itself is covered. Control is proven to come from canonical
faction data, not station names (two stations sharing the name "Fort Bush"
but owned by different factions evaluate differently).

## Heat behavior

Both existing heats behave identically — Heat 1 (`Pursuit`) and Heat 2
(`HotPursuit`) both satisfy `IsActive`, so both deny covered docking. No
Heat 3 exists or was added, and no per-heat docking rules were created.

## Grace-period interpretation

Discovered semantics of `PoliceFugitiveManager`:

- **Active pursuit** (`State == Pursued`, either heat): docking denied.
- **Escape interval** (`State == Evading`): the manager still reports
  `IsActive == true` while the escape countdown runs, so docking **remains
  denied**. The 0.25 s contact-loss grace and the Heat 1/Heat 2 escape
  durations are part of the active incident, not a separate non-fugitive
  state.
- **Post-escape reacquisition grace**
  (`ContrabandReacquisitionGraceSeconds`, `PoliceFugitiveManager.cs:48`):
  after `ResolveEscape` the manager reports `IsActive == false`; the grace
  only suppresses fresh contraband re-detection. It is **not** treated as
  criminality — docking follows the fugitive authority and is allowed.
- **Cleared** (`ResolveEscape` or `Reset`): docking is immediately
  available again, subject only to the pre-existing docking rules.

## Denial precedence

Inside the single shared policy (`FactionAccess.cs:142-155`):

1. `reputationManager == null` → allowed (test convenience).
2. Active Police fugitive at a Liberty Police facility → fugitive denial.
3. Temporary hostility → existing temporary-hostility denial.
4. Permanent hostility → existing hostile denial.
5. Otherwise allowed.

Because the fugitive check is evaluated first, a Liberty Police station that
is both hostile and fugitive produces **one** coherent denial — the fugitive
message — never two messages and never a contradictory pair. When the player
is not fugitive, the existing hostility behavior is authoritative and
unchanged.

## Reputation-tier independence

Phase 74 tiers and Phase 76 notifications are not docking authority. Proven
by the smoke suite:

- Standard / Elevated / Severe lawful standing **with** active fugitive →
  denied (the fugitive authority decides).
- Severe lawful standing **without** fugitive → allowed (Phase 77 does not
  deny merely because standing is poor).
- Elevated without fugitive → allowed.
- Reputation worsening alone (Standard → Severe, never fugitive) → allowed.
- Bribery that improves standing while the fugitive authority remains active
  → still denied.

## Contraband / smuggling independence

- Contraband aboard, never detected → ordinary docking.
- Detected, compliant resolution, fugitive state never begins → ordinary
  docking.
- Refusal that creates an active fugitive state → covered docking denied.
- Smuggling-contract (mission-bound) contraband refusal → same denial,
  because the same `PoliceFugitiveManager` authority is active. The policy
  never inspects mission type, cargo flags, or smuggler identity.
- Fugitive state later clears while contraband remains aboard → docking
  follows the fugitive state, not the cargo.

Stations do not become contraband scanners; Phase 77 reacts only to the
current fugitive authority.

## Pursuit-clear / recovery behavior

When `ResolveEscape` (or `Reset`) clears the canonical state, the restriction
disappears immediately: no remembered denial, no Phase 77 cooldown, no
additional reputation requirement, no persisted station blacklist. Proven
for both Heat 1 → cleared and Heat 2 → cleared, and the post-clear docking
attempt uses the ordinary pre-existing path (`GotoAutopilot.Activate` →
`StationDockUI.DockAtStation`).

## Lifecycle behavior

- **Death/reset** — `PoliceFugitiveManager.Update` auto-resets when the
  player hull is destroyed (`PoliceFugitiveManager.cs:231-234`); the
  docking restriction clears with it. No Phase 77 flag exists to survive.
- **System transition** — the existing fugitive lifecycle clears local heat
  on transition; Phase 77 follows that authority and invents nothing.
- **Already docked** — Phase 77 governs entry only. A player who is docked
  when fugitive state is somehow established is not ejected, arrested, or
  impounded; the existing no-forced-undocking behavior is preserved and
  proven by test.

## Save / load behavior

Active fugitive state is **not** persisted (Phase 54 architecture, unchanged):
`SaveGameManager.CaptureTemporaryHostility` filters the fugitive-pursuit
hostility, and no fugitive field exists in `SaveGameData`. Consequently, after
a save/load cycle the loaded game has no active fugitive state, and the
Phase 77 restriction is cleared with it — the desired behavior, achieved
without any Phase 77 persistence. Proven by test: save while fugitive →
load → Liberty Police docking allowed.

No save-schema change (`SaveGameData.SchemaVersion` stays `13`). No Phase 77
save field exists (verified by reflection over `SaveGameData`).

## UI / refusal messaging

The refusal reuses the existing docking status surface — no new UI:

```
Docking denied — Liberty Police are actively pursuing this vessel at Fort Bush.
```

(`FactionAccess.cs:150`, station suffix appended when a station name is
known.) The message is surfaced through the existing
`NotificationManager.ShowMessage` path used by every pre-existing docking
denial: `GotoAutopilot.RejectDocking` (activation and approach-phase
denials) and the F3 handler / `HandleDockingCompleted` for the final
boundary. Wording exposes no Heat level, enum names, raw reputation values,
or implementation details.

## Notification-spam behavior

- Messages occur on explicit player docking attempts (F3 key transition) or
  on the autopilot's bounded rejection path — never on passive eligibility
  polling. `FactionAccessService.EvaluateDocking` and
  `DockNavigation.IsDockableStation` are pure functions with no
  notification side effects (proven over 1000 repeated evaluations).
- Each explicit denied attempt produces at most one autopilot rejection
  message; repeated attempts remain bounded and never mutate fugitive state.
- Hostile + fugitive produces a single coherent denial, not two.

## Performance / bounds

O(1) per docking attempt: one faction-id normalization, one dictionary
standing lookup, one boolean fugitive query against
`PoliceFugitiveManager.IsActive`. No world searches, Police NPC scans,
station scans, traffic scans, reputation-history lookup, mission-history
traversal, per-frame fugitive polling, or per-station fugitive caches. The
integration extends the existing policy call sites; it adds no update loop.

## Atomicity / no side effects

A denied attempt is observational except for the bounded refusal message.
Proven unchanged by the suite: player position, velocity, docking/animation
state, station transition, current station, credits, reputation, fugitive
Heat, ordinary cargo, and mission cargo. `RejectDocking` cancels the
autopilot route before any station transition can begin, so no partial
docking state remains.

## NPC behavior

Phase 77 applies only to player docking access. The shared policy is
evaluated against the player's fugitive authority and the target station's
faction; it takes no NPC or traffic input, and NPCs never route through
`GotoAutopilot`. NPC docking/traffic behavior is unchanged (proven by test).

## Testing

`--phase77-smoke`: **45 passed, 0 failed**
(`Phase77FugitiveDockingRestrictionsSmokeTest.cs`, wired in
`RoguelancerGame.cs` as flag, dispatch, runner, and `--all-smoke` entry).

Covers all 40 required items plus architecture-specific proofs: evading
state (contact lost, escape timer running) still denies; post-escape
reacquisition grace is not treated as criminality; the approach-phase
revalidation denies mid-approach; the `StationDockUI` final authority
denies while fugitive; an already-docked player is not ejected; and the
Phase 75 presentation / Phase 76 transition observer remain side-effect
free.

Regression suites, all green:

- Phase 68 (14), 69 (16), 70 (15), 71 (22), 72 (30), 73 (40), 74 (40),
  75 (20), 76 (40).
- Police enforcement (30), police fugitive (65), contraband 52/53 (23),
  reputation (102), mission (9), freight (40).
- Dock (11), traffic (9), faction docking access (18).
- `--all-smoke`: **77 suites passed, 0 failed** (includes Phase 77).
- `dotnet build -c Debug`: 0 errors, **128 warnings** (baseline
  unchanged; no new warnings).

## Limitations (out of scope for Phase 77)

No arrests, jail, surrender at stations, bounty payments, fines at
docking, station bribes used to bypass the restriction, forged credentials,
docking permits, station security scans, Police radio, dispatch
reinforcement, launch lockdown, forced undocking, impound, cargo seizure,
reputation redesign, new Heat levels, new Police attention tiers, a
universal faction access framework, or persisted warrants. The restriction
covers only the Liberty Police faction because the repository's canonical
ownership data has no broader Liberty-government faction; if such an
ownership tier is ever introduced, the same policy seam can be extended
without new criminal state. Fugitive state remains transient across
save/load by existing design, so a loaded game is never fugitive and never
docking-restricted.

## Recommended Phase 78

Extend the same bounded pattern to a visible "wanted" indicator in the
dock-assist HUD (e.g., a red clearance line on Liberty Police stations while
`PoliceFugitiveManager.IsActive`), reading only the existing fugitive
authority — still with no persisted criminal state, no new wanted level, and
no station-side memory. Alternatively, a bounded approach-hold behavior
that keeps the autopilot loitering at a safe distance from covered
facilities while the pursuit is active, reusing the existing
`GotoAutopilot` cancellation path.
