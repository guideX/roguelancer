# Phase 74 — Repeat-Smuggler Enforcement Escalation

Outcome A: repeat smuggling becomes more dangerous **because the existing
durable Liberty Police relationship deteriorates** — not because a second
crime system was built. No wanted levels, offense logs, crime counters,
bribes-for-escalation, permits, bounties, or save-schema changes exist.

## Authority discovered (no new durable state)

- Standing ownership: `ReputationManager` owns the mutable player standing
  per faction (`ReputationManager.cs:63-90`); `FactionManager` owns only
  metadata/identity. Standings persist via `faction_reputation`
  (`SaveGameManager.cs: CaptureReputation/ApplyReputation`); transient
  aggression persists separately and the fugitive pursuit reason is
  deliberately **omitted** from saves
  (`SaveGameManager.cs:341,712`).
- Standing scale (`ReputationManager.cs:65-71`): clamped to [-1, +1];
  Hostile ≤ -0.60; Unfriendly (-0.60, -0.20); Neutral [-0.20, +0.20];
  Friendly/Allied above. New-game Liberty Police standing is **-0.25**
  (Unfriendly).
- Existing enforcement consequences, reused unchanged:
  compliance -0.03 (`PoliceEnforcementService.PaidComplianceReputationPenalty`),
  refusal -0.20 (`RefusalReputationPenalty`) plus bounded temporary
  hostility, both applied with `AdjustReputationDirect` (no ripple, so
  Liberty Rogues standing is untouched).
- Fine authority: `500 + 25% of violation value`, clamped to
  [500, 10 000] (`PoliceEnforcementService.CalculateFine`).
- Pursuit authority: `PoliceFugitiveManager` owns the one bounded
  incident with exactly two heats — Heat 1 Pursuit (10 000 acquisition,
  15 s escape) and Heat 2 HotPursuit (14 000 acquisition, 25 s escape) —
  plus the Phase 72 20 s post-escape contraband reacquisition grace.
- Lawful-stop gates, reused unchanged: `PoliceScanSystem` never starts
  or keeps a scan while `IsFactionCurrentlyHostile(LibertyPolice)`
  holds, and the `NpcWeaponSystem` hold-fire gate only suppresses fire
  while no valid hostility holds (`NpcWeaponSystem.cs:203-210`).
- Recovery authorities, reused unchanged: mission reputation rewards and
  `FactionBribeService` (ceiling +0.10, lawful-station rules), both
  committing through `ReputationManager`.

There was no durable criminal-history authority, and none was needed:
the standing itself already records repeated lawbreaking, because every
refusal/evasion/combat consequence moves it.

## Enforcement tiers (derived, never persisted)

`PoliceEnforcementEscalationPolicy.cs` is the one explicit policy.
`GetTier` reads the live Liberty Police standing; nothing caches it and
nothing serializes it:

| Tier | Standing | Meaning |
| ---- | -------- | ------- |
| 0 Standard | > -0.35 | New game (-0.25), neutral, first incident |
| 1 Elevated | (-0.50, -0.35] | One refusal from start (-0.45); repeated compliances |
| 2 Severe | ≤ -0.50 | Still lawful down to -0.60; hostile standings keep their tier value but the existing hostility gates prohibit peaceful stops |

Thresholds were chosen from the real scale: Tier 0 covers the starting
profile so a first incident is manageable; one -0.20 refusal step moves
exactly one tier; the lawful severe band (-0.60, -0.50] is reachable via
mixed compliance/combat/refusal histories (e.g. refuse at -0.36 →
-0.56). No double-penalty was added to force progression — the
unchanged -0.03/-0.20 consequences drive movement.

## Fine escalation (one central path)

- `Evaluate(faction, cargo, credits)` (3-arg) is unchanged: Tier 0,
  Phase 73-compatible fines (2 side-arms → 1 250).
- `Evaluate(faction, cargo, credits, reputationManager)` (new overload)
  computes the violation identically, then applies
  `ApplyFineEscalation` once: Tier 0 ×1.0, Tier 1 ×1.25 (ceiling),
  Tier 2 ×1.5 (ceiling), clamped to the existing [500, 10 000].
  Example: 2 side-arms → 1250 / 1563 / 1875. Cargo value is never
  mutated; each stack is counted once as before.
- The tier rides on `PoliceEnforcementOffer.EnforcementTier`, so the HUD
  demand and the resolution charge the same quoted fine; `TryResolve`
  still charges `offered.FineAmount` exactly once and re-validates only
  the cargo snapshot. `PoliceEnforcementResult` carries the tier for
  observability.
- Only `PoliceScanSystem.CompleteScan` uses the 4-arg overload (it
  already receives the reputation manager). All other callers keep
  Tier 0 behavior, which is why every pre-existing suite passes
  unmodified.
- Insufficient-credit semantics are unchanged: an unaffordable escalated
  fine offers only Refuse, comply fails with `Insufficient credits`,
  and credits/cargo/hostility stay consistent (no negative balances,
  no partial payment, no silent confiscation).

## Demand readability (existing UI only)

- Demand-open notice is unchanged; an elevated stop adds exactly one
  follow-up line from the same authoritative quote:
  `Liberty Police: Repeat smuggling offense — elevated fine` (Tier 1)
  or `Liberty Police: Severe contraband violation — elevated
  enforcement` (Tier 2). Tier 0 text is byte-identical to Phase 72.
- The live HUD status line appends `Repeat offense — elevated fine` /
  `Severe violation — elevated enforcement` from
  `offer.EnforcementTier`; standard stops keep their exact text.
- No wanted-level UI, no standing floats, no per-tick messages.

## Compliance and refusal

- Compliance remains fully possible at every tier while the
  relationship is lawful: scan completes, demand issues, cargo is
  confiscated live, the escalated fine is charged once, the stop
  clears, and no fugitive starts. Escalation is harsher law
  enforcement, not removed agency.
- Compliance keeps its durable -0.03 consequence, so future stops may
  stay elevated — intentional. Recovery through the existing bribe /
  mission paths lowers the derived tier again (proven: bribe from
  -0.55 → +0.10 restores Tier 0 and the standard fine path).
- Refusal resolves through the unchanged service path (cargo
  preserved, -0.20, temporary hostility) and enters the one existing
  fugitive incident at the tier heat:
  Tier 0/1 → Heat 1 Pursuit (existing behavior),
  Tier 2 → Heat 2 HotPursuit via the new
  `BeginPursuit(player, reason, log, initialHeat)` overload, which
  clamps to the two existing heats and can also advance a live Heat 1
  incident to Heat 2. No Heat 3, no second pursuit state; acquisition,
  timers, escape, and cleanup stay owned by the fugitive manager.
- Tier 1 pursuit pressure is expressed through the higher fine plus the
  shorter grace below; Heat 2 is reserved for severe refusals and
  officer-under-fire escalation, keeping the two existing heats
  semantically clean.

## Reacquisition grace (one authoritative calculation)

`ResolveEscape` derives the tier live from Liberty Police standing and
sets `GetReacquisitionGraceSeconds`: 20 s (Tier 0, Phase 72 value
preserved) / 12 s (Tier 1) / 6 s (Tier 2), floored at 6 s so
escape → same-tick immediate restop is impossible (a fresh stop still
needs detection plus a 3.5 s scan). `Reset` (death, system change,
save/load, docking) still clears the transient timer while durable
standing survives. The `TrafficManager` grace gate is untouched.

## Hostility boundaries

Contraband enforcement, fugitive pursuit, temporary hostility, faction
hostility, and retaliation remain distinct. At genuine Police hostility
no scan starts and no demand opens; `IsLawfulStopHoldFire` never
suppresses combat because the weapon gate already requires the absence
of valid hostility. Tier values never restore peaceful scanning.

## Mission / contraband interoperability

Mission contraband (`AddMissionCargo`) and ordinary contraband receive
the identical tier logic — the standing changes the response, never the
commodity. Legal (including mission-reserved legal) cargo is unaffected;
stolen/contraband classification and violation labels are unchanged;
escalation applies once per resolution, never per stack.

## Save / load / lifecycle

No schema change (`CurrentSchemaVersion` stays 13; proven by a
legacy-shaped save loading to standard enforcement). Standing
round-trips through the existing reputation capture; the tier is
recomputed after load and elevated fine/pursuit behavior applies.
Transient stop/demand/fugitive/grace state is never serialized.
Death, system change, docking/undocking, and save/load keep their
existing reset semantics; durable reputation (and therefore the
derived tier) survives them.

## Performance bounds

Tier derivation is O(1) at demand creation and at escape resolution —
no per-frame scans, no world-wide searches, no history lists, no
per-officer state, no duplicate fine snapshots. Detection and
interceptor bounds are untouched.

## Test results

- `--phase74-smoke`: 40 passed, 0 failed
  (`Phase74RepeatSmugglerEscalationSmokeTest.cs`, wired in
  `RoguelancerGame.cs` as flag, dispatch, runner, and all-smoke
  entry). Covers all 40 spec items: tier derivation (incl. hostile
  exclusion and no persisted tier field via reflection), fine
  monotonicity/bounds/central-path, demand/UI single-quote proofs,
  compliance exactness, insufficient-credit safety, Tier 0/1/2 refusal
  heats through the single manager, normal acquisition bounds,
  hold-fire set/clear via the live traffic coordinator, hostile-fire
  gate, tier graces incl. post-expiry re-detection, standing
  durability, bribery de-escalation, mission/ordinary/legal cargo
  parity, single-application over mixed stacks, classification
  stability, save/load tier reproduction without duplication,
  reset/death transient cleanup.
- Regressions, all green: phase 68 (14), 69 (16), 70 (15), 71 (22),
  72 (30), 73 (40), mission (9), freight (40), export (60),
  police-enforcement (30), police-fugitive (65), contraband 52/53
  (23).
- `--all-smoke`: 74 suites passed, 0 failed (includes phase 74).
- `dotnet build Roguelancer.csproj -c Debug`: 0 errors, 128 warnings
  (pre-existing count unchanged; no new warnings).

## Remaining limitations (future phases, out of scope)

No bribery-for-escalation, arrest/prison, impoundment, warrants,
bounties, permits, forged manifests, hidden compartments, criminal XP,
jurisdictions, reinforcements, checkpoints, new commodities, economy
rebalancing, audio, or mission types. Noted behaviors: two consecutive
refusals from the starting profile reach genuine hostility (-0.65)
without an intermediate severe lawful stop — the severe lawful band is
reached via mixed histories instead; Tier 1 refusals reuse Heat 1 by
design, with escalation expressed via fine and grace.
