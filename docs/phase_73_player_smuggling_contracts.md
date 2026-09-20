# Phase 73 — Player Smuggling Contracts

One coherent loop: **accept illegal freight → physically carry it →
risk existing Police enforcement → deliver successfully or lose the
shipment.** No Police, cargo, mission, economy, or faction system was
rewritten. Smuggling contracts place real contraband in the real cargo
hold, and the world systems built in Phases 68–72 react naturally.

## Existing mission architecture reused

- Mission identity, lifecycle, and board live in `Mission.cs` /
  `MissionManager.cs`. The contract is `MissionType.ContrabandSmuggling`
  (`Mission.cs:26`) with `ContrabandSmugglingStage`
  (`Mission.cs:66-72`) and the standard
  `Available → Accepted/InProgress → Rewarded/Failed` statuses.
- Offers ride the existing board: `CreateBoardMissions`
  (`MissionManager.cs:493`) aggregates
  `GenerateContrabandSmugglingMissions` alongside freight, export,
  disruption, defense, escort, raid, and interdiction offers. There is
  no second mission board.
- Acceptance is the single existing authority:
  `CanPlayerAcceptMission` (`MissionManager.cs:318`) then
  `AcceptMission` (`MissionManager.cs:2157`), with the smuggling branch
  at `MissionManager.cs:2321-2343` and cargo issuance at
  `TryIssueSmugglingCargo` (`MissionManager.cs:2581`).
- Completion flows through the existing docking path:
  `MissionWorldManager.NotifyStationDocked` →
  `TryCompleteSmugglingDelivery` (`MissionWorldManager.cs:3697`) →
  `MissionManager.CompleteSmugglingMission`
  (`MissionManager.cs:2850`).
- Failure flows through the existing `FailMission`
  (`MissionManager.cs:3053`), which sends one notification and applies
  the standard −0.02 reputation penalty.

## Smuggling-offer generation

`GenerateContrabandSmugglingMissions` (`MissionManager.cs:518`):

- Rogue-origin stations only (faction gate, normalized comparison).
- Destinations exclude the origin and other Rogue stations, ordered
  deterministically by distance, then name, then station identity.
- Commodities are the canonical catalog contraband
  (`IsContraband && !IsMissionCargo && VolumePerUnit > 0`, ordered by
  Id): `side-arms` and `alien-organisms`. No fake mission contraband.
- Exactly three bounded offers (Easy/Medium/Hard, quantities 3/5/8
  capped by `FreightMaximumCargoVolume`), selected by stable hash, so
  refreshes are deterministic and never accumulate duplicates.
- Offer creation validates everything again in
  `Mission.CreateContrabandSmuggling` (`Mission.cs:614-660`).
- Generation creates `Mission` objects only. No cargo exists before
  acceptance (proven by `board refresh issues no cargo`).

## Faction / access policy

No independent criminal reputation system was built. Smuggling reuses
the standard gates:

- Origin must be Liberty Rogues (offer generation + acceptance
  re-validation).
- `GetMissionEligibility` (`MissionManager.cs:231`) applies before any
  mutation: hostile Rogue standing locks the offer
  (`NO WORK AVAILABLE — HOSTILE STANDING`); neutral/positive standings
  follow normal board conventions.
- Smuggling offers carry no minimum-standing requirement beyond the
  hostile lock: Rogues take anyone who is not their enemy. This small
  rule is intentional and documented here rather than expanded into
  reputation infrastructure.

## Cargo / reservation ownership

- `CargoHold` remains authoritative. The mission tracks the obligation
  (`CommodityId`, `RequiredQuantity`, `IssuedCargoQuantity`).
- Acceptance issues real mission-attributed cargo via
  `CargoHold.AddMissionCargo` — the same mechanism as export
  contracts. No hidden cargo count exists.
- Progress derives from `GetMissionCargoQuantity` /
  `HasMissionCargo`, so Police confiscation, jettison, destruction,
  pods, docking, and save/load all act on one truth.

## Acceptance transaction behavior

Acceptance is all-or-nothing, reusing the freight/export pattern:

- `CanPlayerAcceptMission` checks availability, single-active-mission,
  reward validity, reputation eligibility, Rogue origin/identity,
  canonical contraband metadata, quantity bounds, destination
  resolvability, and `CargoHold.CanFit` — before any mutation.
- `TryIssueSmugglingCargo` re-checks capacity and rolls back through
  `TryRemoveIssuedSmugglingCargo` if world binding fails.
- A capacity rejection leaves the offer `Available`, creates no
  mission, creates no cargo, and changes no used capacity (proven by
  the transactional-acceptance smoke cases).

## Police interoperability (no special-casing)

Mission cargo is ordinary mission-attributed contraband, so the
production chain reacts with zero smuggling-specific logic:

- Detection: `ContrabandEnforcementPolicy.HasPlayerContraband` counts
  mission-reserved units; `TrafficManager` intercepts with reason
  `ContrabandEnforcement`.
- Scan/demand: `PoliceScanSystem` scans, detects the exact mission
  quantity, and opens the bounded comply/refuse demand.
- Compliance: `PoliceEnforcementService.TryResolve` confiscates via
  `CargoHold.TryConfiscateCargo` (reporting
  `MissionCargoConfiscation`), charges the quoted fine, and notifies
  through `NotifyPoliceConfiscation` +
  `NotifyPoliceEnforcementCompliance`.
- Refusal/timeout/flight: existing fugitive pursuit
  (`PoliceFugitiveManager.BeginPursuit` + temporary hostility); the
  cargo and contract survive, so delivery after evasion still works.

## Confiscation consequences

`NotifyPoliceConfiscation` (`MissionManager.cs:166-210`) refreshes the
authoritative quantity and fails the contract:
`Liberty Police confiscated the smuggling cargo`. The compliance
follow-up (`MissionManager.cs:218-226`) also fails the contract when
post-detection jettison left it short, so the mission can never revive
without legitimately restored cargo. Failed contracts pay nothing and
keep no phantom quantity or stale reservation. This mirrors the
established loss semantics: freight/export missions likewise stay bound
to authoritative cargo reality rather than inventing recovery state.

## Jettison / loss behavior

- Jettison uses the physical-pod path
  (`LootManager.TryJettisonMissionCargo` / `TryJettisonContraband`):
  pods keep mission attribution (`MissionId`, provenance).
- The hold/reservation updates immediately; a short-loaded contract
  cannot complete at the destination.
- Legitimate pod recovery (`LootManager.Update` →
  `CargoHold.TryAddMissionCommodityPartial`) restores attribution, and
  the contract remains completable — proven end-to-end by
  `jettison recovery keeps the contract completable`.
- Cancellation converts remaining mission cargo to ordinary cargo and
  marks the mission failed (established `ReleaseSmugglingCargo`
  behavior); the player keeps the physical goods and their Police
  exposure.

## Delivery / completion behavior

- Destination identity must match exactly
  (`CompleteSmugglingMission`, `MissionManager.cs:2860-2867`); wrong
  stations leave the live contract untouched.
- Completion requires the complete contracted quantity
  (`IssuedCargoQuantity == RequiredQuantity` plus exact
  `HasMissionCargo`); partial cargo cannot complete.
- Success removes the exact cargo once, pays the exact reward once,
  sets `Rewarded`/`RewardPaid`, awards the standard reputation reward,
  and unregisters the waypoint. Repeat docking cannot duplicate payout.

## Reward policy (exact calculation)

`MissionManager.CalculateSmugglingReward` (public static, tested
formula-exact):

```
base      = 4000 (Easy) / 6500 (Medium) / 10000 (Hard, Deadly)
premium   = commodity.BasePrice * quantity / 4   (25% seizure-risk premium,
            mirrors the 0.25 Police fine rate)
distance  = clamp((int)(origin↔destination distance / 1000), 0, 4000)
reward    = clamp(base + premium + distance, 3000, 30000)
```

No market, reputation, or procedural economy state participates, so
identical routes always offer identical rewards. Typical contracts pay
roughly 2–5× equivalent lawful freight, reflecting interdiction risk.
Phase 73 is not an economy rebalance: bounds match the existing
mission-reward conventions.

## Player-facing text (existing UI only)

- Board type label: `CONTRABAND SMUGGLING`; objective:
  `Deliver {cargo} to {destination} without a police seizure`.
- Offer description ends with:
  `Illegal cargo — Liberty Police may confiscate this shipment.`
  (`Mission.cs:639`).
- The mission-board detail panel shows origin, destination, contraband
  type/quantity/space, loaded state, reward, and the same illegal-cargo
  warning (`StationMissionBoardUI.cs:419-434`).
- Success feedback: `Smuggling delivered — +{reward} CR` /
  `Smuggling reward received: {reward} CR` from the authoritative
  completion result. Failure feedback: one `Mission failed: {reason}`
  notification (confiscation, pre-compliance loss, timeout, or ship
  destruction).

## Save / load behavior

No schema change was needed: `SaveMissionData` already serializes the
smuggling lifecycle fields and `SaveCargoItemData` persists
mission-bound cargo attribution. Round-trip proof (real file
save/load via `SaveGameManager.TrySave`/`TryLoad`, then
`ApplyCargo`/`ApplyMissions`/`SetCredits`/`RebindActiveMissions`):

1. accept → save → load: identity, commodity, quantity, destination,
   reward, cargo, reservation, and status all survive;
2. loaded cargo is Police-detectable with no duplicated cargo or
   mission;
3. completion after load pays exactly once; repeat docking is inert;
4. confiscation after load fails the mission, clears the reservation,
   and blocks reward without restored cargo.

This closes the loop with the Phase 72 transient-stop policy: the stop
itself is never persisted, but the durable mission + contraband
re-detect deterministically after load.

## Performance bounds

- Offer generation is refresh-time only: 3 offers, no per-frame work,
  no world-wide route search (destinations come from the known-station
  list), no per-NPC mission scans, no cargo snapshots, no second
  contraband inventory.
- Mission updates ride the existing docking/delivery events and the
  bounded Police detection paths.

## Test results

- `--phase73-smoke`: 40 passed, 0 failed
  (`Phase73PlayerSmugglingContractsSmokeTest.cs`,
  wired in `RoguelancerGame.cs` as flag, dispatch, runner, and
  all-smoke entry).
- Regressions, all green: phase 68 (14), 69 (16), 70 (15), 71 (22),
  72 (30), mission (9), freight (40), export (60), police-enforcement
  (30), police-fugitive (65), contraband 52/53 (23).
- `--all-smoke`: 73 suites passed, 0 failed (includes the new phase 73
  suite).
- `dotnet build Roguelancer.csproj -c Debug`: 0 errors, no new
  warnings (128 pre-existing warnings untouched).

## Remaining limitations (future phases, out of scope)

No bribing, forged manifests, cloaked cargo, corruption, customs
permits, dynamic jurisdictions, criminal reputation progression,
smuggling skills, black-market purchasing, procedural contacts, timed
pursuit spawns, smuggler NPC factions, arrest gameplay, scanner
equipment, secret compartments, Police audio, or formation AI. Also
noted: smuggling cargo is issued by the Rogue contact (no market stock
movement, unlike export); contracts have time limits per difficulty
(Easy 900s / Medium 750s / Hard 600s) enforced by the standard expiry
path; only one mission (any type) may be active at a time.
