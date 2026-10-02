# Phase 76 — Liberty Police Attention Transition Notifications

Outcome A: Liberty Police attention changes are now immediately
understandable at the moment they happen. Phase 76 adds **one bounded,
stateless observer** over the standing authority that already drives
enforcement. It creates no new criminal-state authority, no wanted level,
no warrants, no heat display, no persistent HUD meter, no criminal
history, and no save-schema change.

> **Phase 76 adds no enforcement authority and no persisted criminal
> state.**

The guiding rule: *when Liberty Police's attitude materially changes, tell
the player once — but never create another system whose job is to remember
that attitude.*

## Architecture discovered

- **Notification authority** — `NotificationManager` (`NotificationManager.cs`)
  is a transient centered toast queue with a single `ShowMessage(string,
  float duration = 3f)` entry point. It is strict FIFO, has no priority,
  no de-duplication key, no replacement, and no acknowledgement. It is
  created once in `RoguelancerGame.LoadContent` and persists across
  dock/undock, system change, and death. Phase 76 reuses it unchanged.
- **Reputation authority** — `ReputationManager` owns the durable
  `Dictionary<string,float>` standing and raises exactly one
  `OnReputationChanged(ReputationChangeResult)` event per committed
  mutation from `CommitChange` (`ReputationManager.cs:355`). The result
  carries `OldValue`, `NewValue`, `OldBand`, `NewBand`, `Reason`, and
  `IsSecondaryEffect`. `LoadStandings` and `ResetToNewGame` do **not**
  raise the event, so initialization is naturally silent.
- **Enforcement tier authority** — `PoliceEnforcementEscalationPolicy`
  owns the lawful thresholds (`-0.35` Elevated, `-0.50` Severe) and is
  the single source for `PoliceEnforcementTier`.
- **Hostility authority** — `ReputationManager.HostileThreshold`
  (`-0.60`) and `ReputationBand` own hostility. The existing
  `RoguelancerGame.HandleReputationChanged` already emits
  `"{FACTION} NOW {BAND}"` when a faction's band changes, and
  `HandleTemporaryHostilityChanged` emits the transient
  `TEMPORARILY HOSTILE` / `HOSTILITY CLEARED` lines.
- **Phase 75 projection** — `PoliceAttentionPresentation.GetLevel`
  derives `Standard / Elevated / Severe / Hostile` from the standing,
  resolving hostility first and otherwise delegating to the Phase 74
  policy. It owns no fields, timers, or history.

## Notification mechanism selected

The existing `NotificationManager.ShowMessage` toast, at the existing
default 3-second lifetime. No modal dialog, no blocking input, no
acknowledgement, no permanent alert, and no special priority. A Police
attention change is informative, not a modal event.

## Authoritative data path

```
ReputationManager.OnReputationChanged (one event per committed mutation)
        │  change.OldValue ─────────────────────────────┐
        │                                                ▼
        │                         PoliceAttentionPresentation.GetLevel(float)
        │                                    │
        │   ReputationManager.GetBandForStanding(standing) == Hostile
        │                                    │
        │   PoliceEnforcementEscalationPolicy.GetTier(standing)
        │                                    │
        └──────────────►  Standard / Elevated / Severe / Hostile
                                         │
                          compare previous vs current level
                                         │
                        emit at most one notification if changed
```

`PoliceAttentionPresentation.GetLevel(float)` is a new additive overload
that mirrors the existing `GetLevel(ReputationManager?)`; it re-encodes
no threshold. Hostility still comes from the canonical band authority and
the lawful bands still come from the Phase 74 policy. Phase 76 contains
no `-0.35`, `-0.50`, or `-0.60` literal.

## Transition observation design

`PoliceAttentionTransitionNotifier` (`PoliceAttentionTransitionNotifier.cs`)
is a single reusable observer. It exposes:

- `bool TryObserve(ReputationChangeResult? change)` — observes one
  authoritative mutation. It ignores non-Liberty-Police changes, derives
  the previous level from `change.OldValue` and the current level from the
  live `ReputationManager`, and returns `false` when the level did not
  change. On a genuine non-hostile change it emits exactly one message and
  returns `true`.
- `static string? BuildTransitionMessage(previous, current)` — the pure
  wording map, independently testable.

`RoguelancerGame.HandleReputationChanged` calls `TryObserve` first and
returns early when it handled the change, so the generic reputation line
is suppressed for an attention-band transition and the player is told
once. When `TryObserve` returns `false` the pre-existing generic
reputation feedback runs unchanged.

Because the change result already carries both endpoints, the notifier
needs **no transient cache at all**. There is no stored "last attention
level", no "already notified" bit, and no transition history.

## Transient state added

**None.** Phase 76 retains zero runtime observation state. The minimum
information required to detect a change is the `OldValue`/`NewValue`
already present on the authoritative `ReputationChangeResult`, so the
notifier is a pure function of one event. This makes new-game and
save/load initialization silent by construction and removes any
opportunity for a remembered value to become enforcement authority.

## Initialization / new game

New game begins with Liberty Police standing `-0.25` → Standard. No
reputation event is raised during construction or `ResetToNewGame`, so no
`Liberty Police attention normal` message appears. The observer is created
in `LoadContent` and emits nothing until a genuine mutation occurs.

## Live transitions

| Transition | Emitted text |
| ---------- | ------------ |
| Standard → Elevated | `Liberty Police attention increased — patrols are watching you more closely.` |
| Standard/Elevated → Severe | `Liberty Police attention increased — you are under elevated scrutiny.` |
| Severe → Elevated | `Liberty Police attention decreased, but patrols remain wary.` |
| Elevated/Severe → Standard | `Liberty Police attention has returned to normal.` |

Wording never exposes `Tier N`, raw standing, raw thresholds, or fugitive
heat.

## Direct multi-band jumps

Each `OnReputationChanged` event is one committed mutation, so a jump such
as `-0.55 → +0.10` (Severe → Standard) is compared once and emits only
the recovery line. `-0.25 → -0.65` (Standard → Hostile) emits only the
appropriate existing hostility feedback. Mathematically crossed
intermediate boundaries are never replayed. One underlying mutation
produces at most one Police-attention notification.

## Hostility handling

Hostility is already communicated by the existing band feedback
(`LIBERTY POLICE NOW HOSTILE`, or the transient hostility line). The
notifier therefore **defers** whenever either endpoint is `Hostile` and
emits nothing. This holds for a lawful → Hostile escalation, a
Hostile → Severe de-escalation, and a Hostile → Standard recovery. There
is no duplicate and no contradictory `attention increased` / `hostile`
pair. Hostility remains owned by existing faction/reputation mechanics.

## Recovery

- Severe → Elevated: de-escalation line that explicitly avoids claiming
  full normalization.
- Elevated/Severe → Standard: one clear recovery line.
- Hostile → Severe or Hostile → Standard: deferred to the existing band
  feedback, which is the single appropriate feedback event.

## Trigger sources

The observer reacts to *relationship state changes*, never to crimes or
event history. It is proven compatible with contraband compliance,
contraband refusal, mission-bound smuggling-cargo refusal, bribery,
mission reputation rewards, ordinary direct reputation adjustment, and
the ordinary recovery path. No source is special-cased; equal standing
produces equal feedback regardless of origin.

## Save / load

No save-schema change (`SaveGameData.SchemaVersion` stays `13`). No
persisted last-attention level, "already notified" bit, or transition
history exists. Proven: save at Severe → load → no notification, then a
genuine recovery emits exactly one; loading Standard/Elevated/Severe all
initialize silently; a compatible legacy-shaped save still loads.

## Lifecycle

The observer is created once and is stateless, so dock, undock, system
transition, death/reset, station UI open/close, and save/load cannot
invent a transition. `ResetToNewGame` and `LoadStandings` raise no event.
Even if the object were recreated, it would read the current authoritative
state rather than interpret recreation as a transition.

## Notification de-duplication / spam control

- Same-band changes (`-0.38 → -0.42`, `-0.52 → -0.58`) emit nothing.
- Repeated reads of `PoliceAttentionPresentation`,
  `ReputationPresentation`, dock overview, mission-board overview, and
  station NPC dialogue emit nothing — the Phase 75 surfaces remain
  side-effect free.
- The notifier only reacts to `OnReputationChanged`, never to a query, so
  opening UI cannot count as a transition.
- No unbounded queue growth: at most one message per genuine band
  transition.

## Performance / bounds

O(1) per reputation mutation: two band lookups, two tier derivations, one
enum comparison, and a constant string selection. No world/NPC/station
scans, no history traversal, no per-frame polling, and no per-NPC state.
Integration is at the existing mutation/event seam
(`HandleReputationChanged`), not a new update loop.

## Testing

`--phase76-smoke`: **40 passed, 0 failed**
(`Phase76PoliceAttentionTransitionNotificationsSmokeTest.cs`, wired in
`RoguelancerGame.cs` as flag, dispatch, runner, and `--all-smoke` entry).
Covers all 35 required items plus exact Phase 74/75 boundary cases around
`-0.35`, `-0.50`, and `-0.60`, and a non-Police-faction silence case.

Highlights: new-game and all three load states initialize silently;
Standard→Elevated, Elevated→Severe, Severe→Elevated, Elevated→Standard
each emit exactly one; Severe→Standard and Standard→Severe direct jumps
emit one final-state message; same-band and repeated reads are silent;
contraband compliance/refusal, mission-bound smuggling refusal, bribery,
and mission recovery each notify once; save/load does not replay and a
post-load genuine transition does notify; no persisted field/schema
member; the notification mutates neither reputation, fugitive heat,
lawful-stop state, nor hostility; hostility and hostile recovery produce
no duplicate; reset, dock/undock, and system transition invent nothing.

Regression suites, all green:

- Phase 68 (14), 69 (16), 70 (15), 71 (22), 72 (30), 73 (40), 74 (40),
  75 (20).
- Police enforcement (30), police fugitive (65), contraband 52/53 (23),
  mission (9), freight (40), reputation (102).
- `--all-smoke`: **76 suites passed, 0 failed** (includes Phase 76).
- `dotnet build -c Debug`: 0 errors, **128 warnings** (baseline
  unchanged; no new warnings).

## Limitations (out of scope for Phase 76)

No persistent HUD Police meter, wanted stars, Heat display, criminal
history, arrest/jail, bounty, warrants, Police radio/dispatch simulation,
new reputation schema, faction-wide UI redesign, station news, audio
alerts, achievements, tutorial framework, scan-frequency changes, fine
changes, or new enforcement tiers. The observer is presentation feedback
only. Because hostility feedback is owned elsewhere, Phase 76 intentionally
emits nothing for transitions into or out of `Hostile`.

## Recommended Phase 77

Extend the same stateless observer pattern to a small Liberty Navy /
Liberty Corporations attention readout, or add a bounded de-escalation
nudge when attention has been Elevated/Severe for a sustained play
session without further incidents — still reading only
`ReputationManager` + the Phase 74 policy, still with no persisted
criminal state and no new enforcement authority.
