# Phase 75 — Player-Facing Law-Enforcement Reputation Readability

Outcome A: the player can now **feel and understand** Liberty Police's
changing attitude through existing game-world surfaces. Phase 75 adds
**presentation only** — it creates no new criminal-state authority, no
wanted level, no warrants, no notoriety, no suspicion, no heat, no
offense history, and no save-schema change.

The core design rule: *the player should be able to feel and understand
Liberty Police's changing attitude without Roguelancer inventing a second
system to remember why they are angry.*

## Existing UI / dialogue architecture discovered

- **Station walk-around social NPCs** (`StationBarSocial.cs`,
  `StationNpc.cs`, `StationInteraction.cs`): the bartender, rogue pilot,
  dockhand, and smuggler each carry one `StationDialogue`. Interaction is
  composed at runtime in `RoguelancerGame.TalkToStationNpc`
  (`RoguelancerGame.cs:6802`), which already layers mission-specific
  lines (`StationBarSocial.GetBartenderMissionLine`) and bribe-offer
  lines on top of the base payload. This is the existing contextual
  dialogue seam.
- **Docked reputation overview** (`ReputationPresentation.cs`,
  `StationDockUI.DrawReputationOverview`,
  `StationMissionBoardUI.DrawReputationOverview`): a pure, read-only
  presentation layer (`ReputationPresentation`) shared by the station
  dock UI and the mission-board UI. `BuildStationFactionLine`,
  `BuildStationStandingLine`, and `BuildOverview` already project the
  authoritative `ReputationManager` into UI strings.
- **Dock header** (`StationDockUI.DrawHeader`) and mission-board header
  reuse the same `ReputationPresentation` lines.
- **Notification manager** (`NotificationManager.cs`) is a transient
  centered toast queue — suitable for events, not for a persistent
  relationship readout.
- No news/rumor system exists; `StationBarSocial` is the closest existing
  ambient-text surface.

## Chosen player-facing surfaces and why

Two existing surfaces were reused, both reading the **same** single
authority — no new dialogue engine, no new screen, no new state:

1. **Station social NPC dialogue** (preference 1: existing bar/NPC
   dialogue). At a Liberty-affiliated station
   (`LibertyPolice` / `LibertyNavy` / `LibertyCorporations`), the
   existing `TalkToStationNpc` path appends one bounded ambient line to
   the NPC's normal line when attention is Elevated or worse. This is
   the diegetic "rumor" delivery and matches the requested tone.
2. **Docked reputation overview** (preference 2: existing
   reputation/faction UI). The dock reputation screen and the
   mission-board reputation overview each render one always-present
   `LIBERTY POLICE ATTENTION:` line, so the relationship also reads as
   ordinary at Standard rather than being blank.

The dock header and mission-board header already show the *station's*
faction standing; Phase 75 deliberately does **not** duplicate the
Police readout there, keeping the change bounded.

## Authoritative data path

`PoliceAttentionPresentation.cs` is a pure static projection:

```
ReputationManager.GetStanding(LibertyPolice)
        │
        ├── ReputationManager.IsHostile(LibertyPolice)  // band <= -0.60
        │
        └── PoliceEnforcementEscalationPolicy.GetTier(reputationManager)
                    │  (Phase 74, the one definition of the bands)
                    └── Standard / Elevated / Severe
```

- Hostility is resolved **first**, so a genuinely hostile relationship is
  never described as mere inspection attention. The Phase 74 tier stays
  standing-only (it still returns `Severe` at `-0.60`), and the
  presentation layers the existing hostile band on top without
  redefining it.
- The Phase 74 thresholds are **not** reproduced in the presentation
  code; `GetTier` is the single source. `ReputationPresentation` and the
  two UI call sites call only this projection.
- No fields, timers, cached previous tiers, permanent smuggler flags, or
  incident history exist anywhere in Phase 75.

## Exact tier-to-presentation mapping

| Standing | Authority | Level | Overview line | Ambient NPC line |
| -------- | --------- | ----- | ------------- | ---------------- |
| > -0.35 | Tier 0 | Standard | `LIBERTY POLICE ATTENTION: NORMAL — no unusual attention.` | *(none)* |
| (-0.50, -0.35] | Tier 1 | Elevated | `LIBERTY POLICE ATTENTION: RECENT ACTIVITY NOTED — patrols are watching more closely.` | `You've been attracting some attention from Liberty patrols lately.` |
| (-0.60, -0.50] | Tier 2 | Severe | `LIBERTY POLICE ATTENTION: ELEVATED SCRUTINY — another incident could have serious consequences.` | `Word is Liberty Police have your ship flagged for extra scrutiny.` |
| ≤ -0.60 | Hostile band | Hostile | `LIBERTY POLICE ATTENTION: HOSTILE — patrols will treat you as an enemy.` | `Liberty Police regard you as hostile.` |

Wording is natural-language and never exposes raw thresholds, enum names,
"Tier N", heat values, or mechanics that do not exist. No "permanent
watch list", "all stations know your record", "three strikes", or
"bounty issued" claims are made.

## Hostile behavior

At or below the existing `ReputationManager.HostileThreshold` (-0.60),
the presentation reports hostility and does **not** describe increased
inspection. Existing faction-hostility presentation
(`ReputationPresentation.BuildStationStandingLine`, the transient
`TEMPORARILY HOSTILE` overview label, docking access, weapon gates)
remains authoritative. Phase 75 adds no hostile-state mechanics and does
not restore peaceful scan behavior.

## Recovery / de-escalation

The level is derived live on every read. There is no stale remembered
"criminal history", no forgiveness timer, and no stored previous tier.
A bribe/mission recovery that raises Police standing from Severe to
`+0.10` immediately returns the readout to Standard semantics (proven).
Temporary hostility from a refusal is deliberately not treated as
durable hostility — it remains owned by the existing transient
presentation.

## Smuggling / contraband interoperability

Ordinary contraband incidents and Phase 68+ smuggling contracts both
commit through the same `ReputationManager` standing. Phase 75 never
inspects mission history and never classifies the player as a
"smuggler"; it describes the Police relationship only. Equal standing
produces equal messaging regardless of how it was reached (proven).

## Persistence

No save-schema change (`SaveGameData.SchemaVersion` stays 13). The
readout is reconstructed entirely from the durable `faction_reputation`
standing after load. Proven: save while Severe → reload → Severe
semantics; save while Elevated → reload → Elevated; save while Standard
→ reload → Standard; and a legacy-shaped save with no Phase 75 fields
loads and reconstructs Standard.

## Lifecycle

- **Dock/undock**: the reputation overview and bar dialogue are rebuilt
  from live reputation on each open; no transient UI state becomes
  authoritative.
- **System transition / death / reset**: no Phase 75 state exists to
  clear or leak; durable standing survives and the readout follows.
- **Repeated open/close and repeated reads**: deterministic (proven 100
  reads and 25 open/close cycles).
- Presentation does not mutate reputation, fugitive heat, or
  lawful-stop state (proven with an active pursuit and a live
  `ContrabandDetected` demand).

## Performance / bounds

O(1) per read: one dictionary lookup (`GetStanding`), one band check, one
tier derivation, and a bounded `switch` selecting a constant string. No
world scans, NPC searches, history traversal, unbounded dialogue logs,
per-officer records, or per-frame allocations. The ambient line is only
computed on an explicit NPC interaction.

## Testing

`--phase75-smoke`: **20 passed, 0 failed**
(`Phase75LawEnforcementReputationReadabilitySmokeTest.cs`, wired in
`RoguelancerGame.cs` as flag, dispatch, runner, and `--all-smoke`
entry). Covers all 20 spec items, boundary-focused on the exact Phase 74
thresholds:

1. new-game `-0.25` → Standard; 2. just above `-0.35` → Standard;
3. exactly `-0.35` → Elevated; 4. mid-Elevated → Elevated; 5. exactly
`-0.50` → Severe; 6. lawful `(-0.50, -0.60)` → Severe; 7. hostile
`-0.60/-0.75/-1.00` → Hostile without peaceful wording; 8–11. immediate
de-escalation and escalation on standing change; 12. save/load tier
reproduction + legacy save; 13. no persisted Phase 75 field/schema
member (reflection + schema 13); 14. ordinary contraband refusal
reflected; 15. smuggling-contract (mission-bound) cargo refusal
reflected and source-independent; 16. reputation not mutated; 17.
fugitive heat not mutated (active pursuit held); 18. lawful-stop demand
not altered (live `ContrabandDetected`); 19. deterministic repeated
reads; 20. existing hostility rules unchanged.

Regression suites, all green:

- Phase 68 (15), 69 (17), 70 (15), 71 (22), 72 (30), 73 (40), 74 (40).
- Police enforcement (30), police fugitive (65), contraband 52/53 (23),
  mission (9), freight (40), reputation (102), bar social (7).
- `--all-smoke`: **75 suites passed, 0 failed** (includes Phase 75).
- `dotnet build -c Debug`: 0 errors, **128 warnings** (baseline
  unchanged; no new warnings).

## Limitations (out of scope for Phase 75)

No bounty system, arrest/jail, permits/licenses, bribery redesign,
criminal record, wanted level, Heat 3, additional fugitive states, new
contraband types, scan-frequency escalation, reputation overhaul,
faction-wide ripple, persistent incident history, achievements, tutorial
framework, full codex redesign, or extensive new station NPC system.
Context sensitivity is intentionally bounded to Liberty-affiliated
station social dialogue plus the reputation overview; non-Liberty
stations show no Police-specific ambient line.

## Recommended Phase 76

Extend the same single-authority projection to a small **Liberty Navy /
Corporations** attention readout (they share the Liberty enforcement
world), or add contextual bar dialogue variants keyed to the existing
`ReputationBand` for other lawful factions — still presentation-only,
still reading `ReputationManager` + the Phase 74 policy, with no new
durable state.
