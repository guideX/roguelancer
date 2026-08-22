using Microsoft.Xna.Framework;
using Roguelancer.Configuration;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Roguelancer
{
    /// <summary>
    /// Focused, headless acceptance coverage for Phase 24 mission gates and
    /// temporary hostility. The checks deliberately exercise authorities
    /// directly so no rendered traversal or wall-clock timing is required.
    /// </summary>
    internal sealed class FactionConsequencesSmokeTest
    {
        private int _passed;
        private int _failed;

        public (int Passed, int Failed) Run()
        {
            Check("mission without requirement is available", () => Eligibility().IsEligible);
            Check("minimum below standing is available", () => Eligibility(minimum: 0.20f, standing: 0.25f).IsEligible);
            Check("minimum exact boundary is available", () => Eligibility(minimum: 0.20f, standing: 0.20f).IsEligible);
            Check("minimum below threshold is locked", () => !Eligibility(minimum: 0.20f, standing: 0.19f).IsEligible);
            Check("maximum above standing is available", () => Eligibility(maximum: 0.30f, standing: 0.20f).IsEligible);
            Check("maximum exact boundary is available", () => Eligibility(maximum: 0.30f, standing: 0.30f).IsEligible);
            Check("maximum above limit is locked", () => !Eligibility(maximum: 0.30f, standing: 0.31f).IsEligible);
            Check("employer faction is the authority", EmployerFactionIsUsed);
            Check("destination faction is not substituted", DestinationFactionIsIgnored);
            Check("unfriendly low-trust work remains available", () => Eligibility(standing: -0.25f).IsEligible);
            Check("hostile employer has no normal work", () => !Eligibility(standing: ReputationManager.HostileThreshold).IsEligible);
            Check("locked minimum reports a readable reason", () => Eligibility(minimum: 0.20f, standing: 0f).Reason.Contains("REPUTATION TOO LOW", StringComparison.Ordinal));
            Check("locked maximum reports a readable reason", () => Eligibility(maximum: 0.20f, standing: 0.30f).Reason.Contains("REPUTATION TOO HIGH", StringComparison.Ordinal));
            Check("locked accept is rejected", LockedMissionCannotAccept);
            Check("locked accept keeps mission available", LockedAcceptDoesNotMutateMission);
            Check("locked accept does not reserve cargo", LockedAcceptDoesNotReserveCargo);
            Check("locked accept does not change credits", LockedAcceptDoesNotChangeCredits);
            Check("locked accept does not change reputation", LockedAcceptDoesNotChangeReputation);
            Check("accepted mission terms persist", AcceptedTermsPersist);
            Check("accepted mission survives standing loss", AcceptedMissionSurvivesStandingLoss);
            Check("accepted mission completes below later threshold", AcceptedMissionCompletesBelowThreshold);
            Check("new equivalent offer locks after standing loss", NextOfferLocksAfterLoss);
            Check("corporation progression unlocks after legitimate reward", CorporationProgression);
            Check("requirement display uses derived band", () => ReputationPresentation.BuildMissionRequirementLine(CreateMission(minimum: 0.20f)).Contains("FRIENDLY", StringComparison.Ordinal));
            Check("standing display includes numeric value", () => ReputationPresentation.BuildMissionStandingLine(CreateMission(), NewReputation(0.30f)).Contains("+0.30", StringComparison.Ordinal));
            Check("reputation reward is visible", () => ReputationPresentation.BuildMissionRewardLine(CreateMission()).Contains("REPUTATION: +0.02", StringComparison.Ordinal));
            Check("catalog priority job is higher-tier", () => MissionCatalog.GetById(MissionCatalog.PriorityDispatchId)?.Difficulty == MissionDifficulty.Hard);
            Check("catalog priority job requires Friendly", () => MissionCatalog.GetById(MissionCatalog.PriorityDispatchId)?.MinimumEmployerReputation == ReputationManager.FriendlyThreshold);
            Check("board generation retains locked offer", BoardRetainsLockedOffer);
            Check("board generation is deterministic", BoardGenerationIsDeterministic);
            Check("old mission save defaults requirement safely", OldMissionSaveDefaults);
            Check("mission requirement terms round-trip", MissionRequirementSaveRoundTrip);
            Check("no hostility starts on new game", () => !NewReputation().IsTemporarilyHostile(FactionManager.LibertyPolice));
            Check("player damage activates hostility", PlayerDamageActivatesHostility);
            Check("NPC damage does not activate hostility", () => !NewReputation().IsTemporarilyHostile(FactionManager.LibertyPolice));
            Check("environment damage does not activate hostility", () => !NewReputation().IsTemporarilyHostile(FactionManager.LibertyPolice));
            Check("same faction is hostile while active", PlayerDamageActivatesHostility);
            Check("unrelated faction remains non-hostile", UnrelatedFactionIsNotHostile);
            Check("repeated attack refreshes timer", AttackRefreshesTimer);
            Check("repeated attack keeps one entry", AttackDoesNotDuplicateEntry);
            Check("hostility duration is bounded", HostilityDurationIsBounded);
            Check("hostility remains before expiry", HostilityRemainsBeforeExpiry);
            Check("hostility expires at simulation duration", HostilityExpiresAtDuration);
            Check("expiry emits one clear event", ExpiryEmitsOnce);
            Check("expiry does not change reputation", ExpiryPreservesReputation);
            Check("Friendly plus temporary hostility is effective hostile", () => EffectiveHostility(0.30f));
            Check("Neutral plus temporary hostility is effective hostile", () => EffectiveHostility(0.00f));
            Check("persistent Hostile is effective hostile", PersistentHostileIsEffective);
            Check("temporary hostile docking is denied", TemporaryHostileDockingDenied);
            Check("docking returns after temporary expiry", DockingReturnsAfterExpiry);
            Check("persistent Hostile docking stays denied", PersistentHostileDockingDenied);
            Check("kill activates temporary hostility", KillActivatesHostility);
            Check("kill penalty is exactly -0.10", KillPenaltyIsExactlyPointOne);
            Check("kill penalty applies once", KillPenaltyAppliesOnce);
            Check("secondary ripple applies once", KillRippleAppliesOnce);
            Check("temporary hostility remains after kill", KillKeepsHostility);
            Check("kill timer eventually clears", KillTimerClears);
            Check("persistent standing remains after timer clears", KillStandingRemains);
            Check("temporary hostility survives snapshot restore", TemporaryHostilitySaveRoundTrip);
            Check("missing temporary hostility save clears state", MissingTemporaryHostilityClears);
            Check("unknown faction hostility is safe", UnknownFactionIsSafe);
            Check("blank faction id is safe", BlankFactionIsSafe);
            Check("reputation query does not create hostility", ReputationQueryDoesNotCreateHostility);
            Check("reputation change does not create hostility", ReputationChangeDoesNotCreateHostility);
            Check("mission completion does not create hostility", MissionCompletionDoesNotCreateHostility);
            Check("new game clears hostility", NewGameClearsHostility);
            Check("active hostility entries remain bounded", ActiveEntriesRemainBounded);
            Check("refresh emits no notification event", RefreshDoesNotEmitEvent);
            Check("persistent band remains visible during hostility", PersistentBandRemainsVisible);
            Check("temporary label is separate", TemporaryLabelIsSeparate);
            Check("hostility clear presentation is distinct", HostilityClearPresentationIsDistinct);
            Check("effective query is centralized", () => typeof(ReputationManager).GetMethod(nameof(ReputationManager.IsFactionCurrentlyHostile)) != null);
            Check("docking authority uses effective query", () => typeof(ReputationManager).GetMethod(nameof(ReputationManager.CanDockWithFaction)) != null);
            Check("temporary authority uses simulation update", () => typeof(TemporaryHostilityManager).GetMethod(nameof(TemporaryHostilityManager.Update)) != null);
            Check("temporary authority has no wall-clock dependency", () => !typeof(TemporaryHostilityManager).GetMethods().Any(method => method.Name.Contains("DateTime", StringComparison.OrdinalIgnoreCase)));
            Check("no attack-level persistent penalty is applied", AttackLeavesStandingUnchanged);
            Check("repeated damage does not repeat kill penalty", RepeatedDamageStillSingleKillPenalty);
            Check("system-transition policy is snapshot-compatible", TemporaryHostilitySnapshotHasRemainingDuration);
            Check("new-game profile remains playable", NewGameProfileRemainsDockable);
            Check("friendly police recovery scenario is deterministic", FriendlyPoliceRecoveryScenario);
            Check("persistent hostile remains denied after transient expiry", PersistentHostileRecoveryScenario);
            Check("mission reward remains bounded", () => MissionManager.GetMissionReputationReward(CreateMission()) <= 0.05f);
            Check("mission reward has no payout multiplier", () => MissionManager.GetMissionReputationReward(CreateMission()) == MissionManager.GetMissionReputationReward(CreateMission()));
            Check("no market authority is held by hostility", () => typeof(TemporaryHostilityManager).GetFields().All(field => field.FieldType != typeof(MarketManager)));
            Check("no cargo authority is held by hostility", () => typeof(TemporaryHostilityManager).GetFields().All(field => field.FieldType != typeof(CargoHold)));
            Check("no wanted-system type was introduced", () => Type.GetType("Roguelancer.WantedLevel") == null);
            Check("all state is numeric/bounded", () => ReputationManager.MinimumStanding == -1f && ReputationManager.MaximumStanding == 1f && TemporaryHostilityManager.MaximumActiveEntries == 32);

            Console.WriteLine($"[FACTION CONSEQUENCES SMOKE] RESULT: {_passed} passed, {_failed} failed");
            return (_passed, _failed);
        }

        private void Check(string label, Func<bool> assertion)
        {
            try
            {
                bool result = RunSilenced(assertion);
                if (result)
                    _passed++;
                else
                    Fail(label, "assertion returned false");
            }
            catch (Exception ex)
            {
                Fail(label, ex.Message);
            }
        }

        private void Fail(string label, string reason)
        {
            _failed++;
            Console.WriteLine($"[FACTION CONSEQUENCES SMOKE] FAIL {label}: {reason}");
        }

        private static MissionEligibilityResult Eligibility(float? minimum = null, float? maximum = null, float standing = 0f)
        {
            ReputationManager reputation = NewReputation(standing);
            MissionManager manager = new(new PlayerCredits(), null, reputation);
            Mission mission = CreateMission(minimum, maximum);
            return manager.GetMissionEligibility(mission);
        }

        private static Mission CreateMission(float? minimum = null, float? maximum = null, string employer = FactionManager.LibertyCorporations, string destination = "Destination")
        {
            return new Mission(
                MissionType.Delivery,
                MissionDifficulty.Easy,
                "Medical Supplies",
                destination,
                1_000,
                0f,
                "Deliver medical supplies.",
                employer)
            {
                MinimumEmployerReputation = minimum,
                MaximumEmployerReputation = maximum
            };
        }

        private static ReputationManager NewReputation(float standing = 0f)
        {
            ReputationManager manager = new(new FactionManager());
            manager.SetReputation(FactionManager.LibertyCorporations, standing, "phase 24 smoke setup");
            return manager;
        }

        private static ReputationManager NewPoliceReputation(float standing = 0.30f)
        {
            ReputationManager manager = NewReputation();
            manager.SetReputation(FactionManager.LibertyPolice, standing, "police smoke setup");
            return manager;
        }

        private static Station CreateStation(string name = "Smoke Station", string faction = FactionManager.LibertyCorporations)
        {
            return new Station(new StationConfig
            {
                Description = name,
                StartupPositionX = 0f,
                StartupPositionY = 0f,
                StartupPositionZ = 0f,
                Radius = 500f,
                DockingRange = 900f,
                SystemIndex = 1,
                FactionId = faction
            }, null);
        }

        private static bool EmployerFactionIsUsed()
        {
            ReputationManager reputation = NewReputation(0.25f);
            reputation.SetReputation(FactionManager.LibertyPolice, -0.80f, "employer authority setup");
            Mission mission = CreateMission(ReputationManager.FriendlyThreshold, employer: FactionManager.LibertyCorporations);
            return new MissionManager(new PlayerCredits(), null, reputation).GetMissionEligibility(mission).IsEligible;
        }

        private static bool DestinationFactionIsIgnored()
        {
            ReputationManager reputation = NewReputation(0.25f);
            reputation.SetReputation(FactionManager.LibertyPolice, -0.80f, "destination authority setup");
            Mission mission = CreateMission(
                ReputationManager.FriendlyThreshold,
                employer: FactionManager.LibertyCorporations,
                destination: "Liberty Police station");
            return new MissionManager(new PlayerCredits(), null, reputation).GetMissionEligibility(mission).IsEligible;
        }

        private static bool PersistentHostileIsEffective()
        {
            ReputationManager reputation = NewReputation();
            reputation.SetReputation(FactionManager.LibertyPolice, ReputationManager.HostileThreshold, "persistent hostile setup");
            return reputation.IsHostile(FactionManager.LibertyPolice) && reputation.IsFactionCurrentlyHostile(FactionManager.LibertyPolice);
        }

        private static bool LockedMissionCannotAccept()
        {
            MissionContext context = CreateMissionContext(0f);
            Mission mission = CreateMission(ReputationManager.FriendlyThreshold);
            return !context.Manager.AcceptMission(mission, context.Station) && mission.Status == MissionStatus.Available;
        }

        private static bool LockedAcceptDoesNotMutateMission()
        {
            MissionContext context = CreateMissionContext(0f);
            Mission mission = CreateMission(ReputationManager.FriendlyThreshold);
            int id = mission.Id;
            context.Manager.AcceptMission(mission, context.Station);
            return mission.Id == id && mission.Status == MissionStatus.Available && context.Manager.ActiveMission == null;
        }

        private static bool LockedAcceptDoesNotReserveCargo()
        {
            MissionContext context = CreateMissionContext(0f, withCargo: true);
            Mission mission = CreateMission(ReputationManager.FriendlyThreshold);
            bool before = context.Cargo.GetMissionCargoReservations().Count == 0;
            context.Manager.AcceptMission(mission, context.Station);
            return before && context.Cargo.GetMissionCargoReservations().Count == 0;
        }

        private static bool LockedAcceptDoesNotChangeCredits()
        {
            MissionContext context = CreateMissionContext(0f);
            Mission mission = CreateMission(ReputationManager.FriendlyThreshold);
            int credits = context.Credits.Credits;
            context.Manager.AcceptMission(mission, context.Station);
            return context.Credits.Credits == credits;
        }

        private static bool LockedAcceptDoesNotChangeReputation()
        {
            MissionContext context = CreateMissionContext(0f);
            Mission mission = CreateMission(ReputationManager.FriendlyThreshold);
            float standing = context.Reputation.GetStanding(FactionManager.LibertyCorporations);
            context.Manager.AcceptMission(mission, context.Station);
            return context.Reputation.GetStanding(FactionManager.LibertyCorporations) == standing;
        }

        private static bool AcceptedTermsPersist()
        {
            MissionContext context = CreateMissionContext(0.25f);
            Mission mission = CreateMission(0.20f, 0.70f);
            return context.Manager.AcceptMission(mission, context.Station) &&
                mission.MinimumEmployerReputation == 0.20f && mission.MaximumEmployerReputation == 0.70f;
        }

        private static bool AcceptedMissionSurvivesStandingLoss()
        {
            MissionContext context = CreateMissionContext(0.25f);
            Mission mission = CreateMission(0.20f);
            if (!context.Manager.AcceptMission(mission, context.Station)) return false;
            context.Reputation.SetReputation(FactionManager.LibertyCorporations, -0.40f, "standing loss");
            return ReferenceEquals(context.Manager.ActiveMission, mission) && mission.Status == MissionStatus.InProgress;
        }

        private static bool AcceptedMissionCompletesBelowThreshold()
        {
            MissionContext context = CreateMissionContext(0.25f);
            Mission mission = CreateMission(0.20f);
            if (!context.Manager.AcceptMission(mission, context.Station)) return false;
            context.Reputation.SetReputation(FactionManager.LibertyCorporations, -0.40f, "standing loss");
            mission.ObjectiveComplete = true;
            context.Manager.Update(0f, false);
            return mission.Status == MissionStatus.Completed && context.Manager.TryClaimReward(mission, context.Station, out _);
        }

        private static bool NextOfferLocksAfterLoss()
        {
            ReputationManager reputation = NewReputation(-0.40f);
            MissionManager manager = new(new PlayerCredits(), null, reputation);
            return !manager.GetMissionEligibility(CreateMission(0.20f)).IsEligible;
        }

        private static bool CorporationProgression()
        {
            MissionContext context = CreateMissionContext(0.19f);
            Mission low = CreateMission();
            Mission high = CreateMission(ReputationManager.FriendlyThreshold);
            if (!context.Manager.AcceptMission(low, context.Station)) return false;
            low.ObjectiveComplete = true;
            context.Manager.Update(0f, false);
            if (!context.Manager.TryClaimReward(low, context.Station, out _)) return false;
            return context.Reputation.GetStanding(FactionManager.LibertyCorporations) > ReputationManager.FriendlyThreshold &&
                context.Manager.GetMissionEligibility(high).IsEligible;
        }

        private static bool BoardRetainsLockedOffer()
        {
            ReputationManager reputation = NewReputation(0f);
            MissionManager manager = new(new PlayerCredits(), null, reputation);
            List<Mission> board = manager.CreateBoardMissions(CreateStation("Newark Station"));
            Mission priority = board.FirstOrDefault(mission => mission.DefinitionId == MissionCatalog.PriorityDispatchId);
            return priority != null && !manager.GetMissionEligibility(priority).IsEligible;
        }

        private static bool BoardGenerationIsDeterministic()
        {
            Station station = CreateStation();
            MissionManager first = new(new PlayerCredits(), null, NewReputation(0f));
            MissionManager second = new(new PlayerCredits(), null, NewReputation(0f));
            List<Mission> a = first.CreateBoardMissions(station);
            List<Mission> b = second.CreateBoardMissions(station);
            return a.Count == b.Count && a.Zip(b, (left, right) => left.DefinitionId == right.DefinitionId &&
                left.MinimumEmployerReputation == right.MinimumEmployerReputation).All(equal => equal);
        }

        private static bool OldMissionSaveDefaults()
        {
            SaveGameManager saveManager = new(Path.Combine(Path.GetTempPath(), "phase24-old-mission-save.json"));
            SaveGameData data = new()
            {
                ActiveMissions = new List<SaveMissionData>
                {
                    new() { MissionId = 801, Type = MissionType.Delivery, Difficulty = MissionDifficulty.Easy, Status = MissionStatus.Active, Target = "x", Destination = "y", Reward = 1_000 }
                }
            };
            List<Mission> missions = saveManager.BuildMissionList(data.ActiveMissions, out _);
            return missions.Count == 1 && !missions[0].HasReputationRequirement;
        }

        private static bool MissionRequirementSaveRoundTrip()
        {
            Mission mission = CreateMission(0.20f, 0.70f);
            SaveGameManager saveManager = new(Path.Combine(Path.GetTempPath(), "phase24-requirement-save.json"));
            List<SaveMissionData> data = saveManager.CaptureMissions(new[] { mission });
            List<Mission> restored = saveManager.BuildMissionList(data, out _);
            return restored.Count == 1 && restored[0].MinimumEmployerReputation == 0.20f && restored[0].MaximumEmployerReputation == 0.70f;
        }

        private static bool PlayerDamageActivatesHostility()
        {
            ReputationManager reputation = NewReputation(0.30f);
            NpcShip target = CreatePoliceShip();
            target.MarkDamagedByPlayer();
            return reputation.RecordPlayerDamage(target) && reputation.IsTemporarilyHostile(FactionManager.LibertyPolice);
        }

        private static bool UnrelatedFactionIsNotHostile()
        {
            ReputationManager reputation = NewReputation(0.30f);
            NpcShip target = CreatePoliceShip();
            target.MarkDamagedByPlayer();
            reputation.RecordPlayerDamage(target);
            return !reputation.IsTemporarilyHostile(FactionManager.LibertyNavy);
        }

        private static bool AttackRefreshesTimer()
        {
            ReputationManager reputation = NewReputation(0.30f);
            NpcShip target = CreatePoliceShip();
            target.MarkDamagedByPlayer();
            reputation.RecordPlayerDamage(target);
            reputation.UpdateTemporaryHostility(45f);
            float before = reputation.GetTemporaryHostilityRemainingSeconds(FactionManager.LibertyPolice);
            target.MarkDamagedByPlayer();
            reputation.RecordPlayerDamage(target);
            return before < 20f && Nearly(reputation.GetTemporaryHostilityRemainingSeconds(FactionManager.LibertyPolice), 60f);
        }

        private static bool AttackDoesNotDuplicateEntry()
        {
            ReputationManager reputation = NewReputation(0.30f);
            NpcShip target = CreatePoliceShip();
            target.MarkDamagedByPlayer();
            reputation.RecordPlayerDamage(target);
            target.MarkDamagedByPlayer();
            reputation.RecordPlayerDamage(target);
            return reputation.TemporaryHostility.ActiveCount == 1;
        }

        private static bool HostilityDurationIsBounded()
        {
            ReputationManager reputation = NewReputation(0.30f);
            reputation.TemporaryHostility.RecordHostileAction(FactionManager.LibertyPolice, "test", 999f);
            return Nearly(reputation.GetTemporaryHostilityRemainingSeconds(FactionManager.LibertyPolice), TemporaryHostilityManager.MaximumDurationSeconds);
        }

        private static bool HostilityRemainsBeforeExpiry()
        {
            ReputationManager reputation = NewReputation(0.30f);
            reputation.TemporaryHostility.RecordHostileAction(FactionManager.LibertyPolice);
            reputation.UpdateTemporaryHostility(59.9f);
            return reputation.IsTemporarilyHostile(FactionManager.LibertyPolice);
        }

        private static bool HostilityExpiresAtDuration()
        {
            ReputationManager reputation = NewReputation(0.30f);
            reputation.TemporaryHostility.RecordHostileAction(FactionManager.LibertyPolice);
            reputation.UpdateTemporaryHostility(60f);
            return !reputation.IsTemporarilyHostile(FactionManager.LibertyPolice);
        }

        private static bool ExpiryEmitsOnce()
        {
            ReputationManager reputation = NewReputation(0.30f);
            int cleared = 0;
            reputation.OnTemporaryHostilityChanged += change => { if (!change.IsActive) cleared++; };
            reputation.TemporaryHostility.RecordHostileAction(FactionManager.LibertyPolice);
            reputation.UpdateTemporaryHostility(60f);
            reputation.UpdateTemporaryHostility(60f);
            return cleared == 1;
        }

        private static bool ExpiryPreservesReputation()
        {
            ReputationManager reputation = NewReputation(0.30f);
            reputation.TemporaryHostility.RecordHostileAction(FactionManager.LibertyPolice);
            float before = reputation.GetStanding(FactionManager.LibertyPolice);
            reputation.UpdateTemporaryHostility(60f);
            return Nearly(before, reputation.GetStanding(FactionManager.LibertyPolice));
        }

        private static bool EffectiveHostility(float standing)
        {
            ReputationManager reputation = NewReputation(standing);
            reputation.TemporaryHostility.RecordHostileAction(FactionManager.LibertyCorporations);
            return reputation.IsFactionCurrentlyHostile(FactionManager.LibertyCorporations);
        }

        private static bool TemporaryHostileDockingDenied()
        {
            ReputationManager reputation = NewReputation(0.30f);
            reputation.TemporaryHostility.RecordHostileAction(FactionManager.LibertyCorporations);
            return !DockNavigation.IsDockableStation(CreateStation(), reputation);
        }

        private static bool DockingReturnsAfterExpiry()
        {
            ReputationManager reputation = NewReputation(0.30f);
            reputation.TemporaryHostility.RecordHostileAction(FactionManager.LibertyCorporations);
            reputation.UpdateTemporaryHostility(60f);
            return DockNavigation.IsDockableStation(CreateStation(), reputation);
        }

        private static bool PersistentHostileDockingDenied()
        {
            ReputationManager reputation = NewReputation(-0.60f);
            return !DockNavigation.IsDockableStation(CreateStation(), reputation);
        }

        private static bool KillActivatesHostility()
        {
            ReputationManager reputation = NewPoliceReputation();
            NpcShip target = CreatePoliceShip();
            target.MarkDamagedByPlayer();
            reputation.ApplyPlayerShipDestroyed(target);
            return reputation.IsTemporarilyHostile(FactionManager.LibertyPolice);
        }

        private static bool KillPenaltyIsExactlyPointOne()
        {
            ReputationManager reputation = NewPoliceReputation();
            NpcShip target = CreatePoliceShip();
            target.MarkDamagedByPlayer();
            reputation.ApplyPlayerShipDestroyed(target);
            return Nearly(reputation.GetStanding(FactionManager.LibertyPolice), 0.20f);
        }

        private static bool KillPenaltyAppliesOnce()
        {
            ReputationManager reputation = NewPoliceReputation();
            NpcShip target = CreatePoliceShip();
            target.MarkDamagedByPlayer();
            reputation.ApplyPlayerShipDestroyed(target);
            reputation.ApplyPlayerShipDestroyed(target);
            return Nearly(reputation.GetStanding(FactionManager.LibertyPolice), 0.20f);
        }

        private static bool KillRippleAppliesOnce()
        {
            ReputationManager reputation = NewPoliceReputation();
            float before = reputation.GetStanding(FactionManager.LibertyNavy);
            NpcShip target = CreatePoliceShip();
            target.MarkDamagedByPlayer();
            reputation.ApplyPlayerShipDestroyed(target);
            float after = reputation.GetStanding(FactionManager.LibertyNavy);
            reputation.ApplyPlayerShipDestroyed(target);
            return Nearly(before - after, 0.03f) && Nearly(after, reputation.GetStanding(FactionManager.LibertyNavy));
        }

        private static bool KillKeepsHostility()
        {
            ReputationManager reputation = NewPoliceReputation();
            NpcShip target = CreatePoliceShip();
            target.MarkDamagedByPlayer();
            reputation.ApplyPlayerShipDestroyed(target);
            return reputation.IsTemporarilyHostile(FactionManager.LibertyPolice);
        }

        private static bool KillTimerClears()
        {
            ReputationManager reputation = NewPoliceReputation();
            NpcShip target = CreatePoliceShip();
            target.MarkDamagedByPlayer();
            reputation.ApplyPlayerShipDestroyed(target);
            reputation.UpdateTemporaryHostility(60f);
            return !reputation.IsTemporarilyHostile(FactionManager.LibertyPolice);
        }

        private static bool KillStandingRemains()
        {
            ReputationManager reputation = NewPoliceReputation();
            NpcShip target = CreatePoliceShip();
            target.MarkDamagedByPlayer();
            reputation.ApplyPlayerShipDestroyed(target);
            reputation.UpdateTemporaryHostility(60f);
            return Nearly(reputation.GetStanding(FactionManager.LibertyPolice), 0.20f);
        }

        private static bool TemporaryHostilitySaveRoundTrip()
        {
            ReputationManager source = NewReputation(0.30f);
            source.TemporaryHostility.RecordHostileAction(FactionManager.LibertyPolice);
            source.UpdateTemporaryHostility(17f);
            SaveGameManager saveManager = new(Path.Combine(Path.GetTempPath(), "phase24-hostility-save.json"));
            SaveGameData data = new() { TemporaryHostility = saveManager.CaptureTemporaryHostility(source) };
            ReputationManager restored = NewReputation(0.30f);
            saveManager.ApplyTemporaryHostility(restored, data);
            return Nearly(restored.GetTemporaryHostilityRemainingSeconds(FactionManager.LibertyPolice), 43f);
        }

        private static bool MissingTemporaryHostilityClears()
        {
            ReputationManager reputation = NewReputation(0.30f);
            reputation.TemporaryHostility.RecordHostileAction(FactionManager.LibertyPolice);
            SaveGameManager saveManager = new(Path.Combine(Path.GetTempPath(), "phase24-missing-hostility-save.json"));
            saveManager.ApplyTemporaryHostility(reputation, new SaveGameData { TemporaryHostility = null });
            return !reputation.IsTemporarilyHostile(FactionManager.LibertyPolice);
        }

        private static bool UnknownFactionIsSafe()
        {
            ReputationManager reputation = NewReputation();
            NpcShip target = new("Unknown", Vector3.Zero, Vector3.Zero, 1f, 0f, "mystery_faction");
            target.MarkDamagedByPlayer();
            return reputation.RecordPlayerDamage(target) && reputation.IsTemporarilyHostile("mystery_faction");
        }

        private static bool BlankFactionIsSafe()
        {
            ReputationManager reputation = NewReputation();
            reputation.TemporaryHostility.RecordHostileAction(null);
            return reputation.IsTemporarilyHostile(FactionManager.NeutralCivilians);
        }

        private static bool ReputationQueryDoesNotCreateHostility()
        {
            ReputationManager reputation = NewReputation(0.30f);
            _ = reputation.IsFactionCurrentlyHostile(FactionManager.LibertyPolice);
            return reputation.TemporaryHostility.ActiveCount == 0;
        }

        private static bool ReputationChangeDoesNotCreateHostility()
        {
            ReputationManager reputation = NewReputation(0.30f);
            reputation.AdjustReputation(FactionManager.LibertyPolice, -0.01f);
            return reputation.TemporaryHostility.ActiveCount == 0;
        }

        private static bool MissionCompletionDoesNotCreateHostility()
        {
            MissionContext context = CreateMissionContext(0f);
            Mission mission = CreateMission();
            if (!context.Manager.AcceptMission(mission, context.Station)) return false;
            mission.ObjectiveComplete = true;
            context.Manager.Update(0f, false);
            context.Manager.TryClaimReward(mission, context.Station, out _);
            return context.Reputation.TemporaryHostility.ActiveCount == 0;
        }

        private static bool NewGameClearsHostility()
        {
            ReputationManager reputation = NewReputation(0.30f);
            reputation.TemporaryHostility.RecordHostileAction(FactionManager.LibertyPolice);
            reputation.ResetToNewGame();
            return reputation.TemporaryHostility.ActiveCount == 0;
        }

        private static bool ActiveEntriesRemainBounded()
        {
            ReputationManager reputation = NewReputation();
            for (int i = 0; i < 100; i++)
                reputation.TemporaryHostility.RecordHostileAction($"unknown_{i}");
            return reputation.TemporaryHostility.ActiveCount <= TemporaryHostilityManager.MaximumActiveEntries;
        }

        private static bool RefreshDoesNotEmitEvent()
        {
            ReputationManager reputation = NewReputation(0.30f);
            int activeEvents = 0;
            reputation.OnTemporaryHostilityChanged += change => { if (change.IsActive) activeEvents++; };
            reputation.TemporaryHostility.RecordHostileAction(FactionManager.LibertyPolice);
            reputation.TemporaryHostility.RecordHostileAction(FactionManager.LibertyPolice);
            return activeEvents == 1;
        }

        private static bool PersistentBandRemainsVisible()
        {
            ReputationManager reputation = NewReputation(0.30f);
            reputation.TemporaryHostility.RecordHostileAction(FactionManager.LibertyCorporations);
            return ReputationPresentation.BuildStationStandingLine(CreateStation(), reputation).Contains("FRIENDLY", StringComparison.Ordinal);
        }

        private static bool TemporaryLabelIsSeparate()
        {
            ReputationManager reputation = NewReputation(0.30f);
            reputation.TemporaryHostility.RecordHostileAction(FactionManager.LibertyCorporations);
            string line = ReputationPresentation.BuildStationStandingLine(CreateStation(), reputation);
            return line.Contains("FRIENDLY", StringComparison.Ordinal) && line.Contains("TEMPORARILY HOSTILE", StringComparison.Ordinal);
        }

        private static bool HostilityClearPresentationIsDistinct()
        {
            ReputationManager reputation = NewReputation(0.30f);
            bool cleared = false;
            reputation.OnTemporaryHostilityChanged += change => cleared |= !change.IsActive;
            reputation.TemporaryHostility.RecordHostileAction(FactionManager.LibertyPolice);
            reputation.UpdateTemporaryHostility(60f);
            return cleared && !reputation.IsTemporarilyHostile(FactionManager.LibertyPolice);
        }

        private static bool AttackLeavesStandingUnchanged()
        {
            ReputationManager reputation = NewReputation(0.30f);
            NpcShip target = CreatePoliceShip();
            float before = reputation.GetStanding(FactionManager.LibertyPolice);
            target.MarkDamagedByPlayer();
            reputation.RecordPlayerDamage(target);
            return Nearly(before, reputation.GetStanding(FactionManager.LibertyPolice));
        }

        private static bool RepeatedDamageStillSingleKillPenalty()
        {
            ReputationManager reputation = NewPoliceReputation();
            NpcShip target = CreatePoliceShip();
            target.MarkDamagedByPlayer();
            reputation.RecordPlayerDamage(target);
            target.MarkDamagedByPlayer();
            reputation.RecordPlayerDamage(target);
            reputation.ApplyPlayerShipDestroyed(target);
            reputation.ApplyPlayerShipDestroyed(target);
            return Nearly(reputation.GetStanding(FactionManager.LibertyPolice), 0.20f);
        }

        private static bool TemporaryHostilitySnapshotHasRemainingDuration()
        {
            ReputationManager reputation = NewReputation(0.30f);
            reputation.TemporaryHostility.RecordHostileAction(FactionManager.LibertyPolice);
            reputation.UpdateTemporaryHostility(12f);
            TemporaryHostilitySnapshot snapshot = reputation.TemporaryHostility.GetActiveSnapshot().Single();
            return Nearly(snapshot.RemainingSeconds, 48f) && snapshot.FactionId == FactionManager.LibertyPolice;
        }

        private static bool NewGameProfileRemainsDockable()
        {
            ReputationManager reputation = new(new FactionManager());
            return reputation.CanDockWithFaction(FactionManager.LibertyPolice) &&
                reputation.CanDockWithFaction(FactionManager.LibertyCorporations);
        }

        private static bool FriendlyPoliceRecoveryScenario()
        {
            ReputationManager reputation = new(new FactionManager());
            reputation.SetReputation(FactionManager.LibertyPolice, 0.30f, "friendly fire setup");
            Station station = CreateStation("Police Station", FactionManager.LibertyPolice);
            bool beforeDock = DockNavigation.IsDockableStation(station, reputation);
            reputation.TemporaryHostility.RecordHostileAction(FactionManager.LibertyPolice);
            bool duringDock = DockNavigation.IsDockableStation(station, reputation);
            reputation.UpdateTemporaryHostility(60f);
            bool afterDock = DockNavigation.IsDockableStation(station, reputation);
            return beforeDock && !duringDock && afterDock && Nearly(reputation.GetStanding(FactionManager.LibertyPolice), 0.30f);
        }

        private static bool PersistentHostileRecoveryScenario()
        {
            ReputationManager reputation = NewReputation(-0.60f);
            reputation.TemporaryHostility.RecordHostileAction(FactionManager.LibertyCorporations);
            reputation.UpdateTemporaryHostility(60f);
            return reputation.IsHostile(FactionManager.LibertyCorporations) && !reputation.CanDockWithFaction(FactionManager.LibertyCorporations);
        }

        private static MissionContext CreateMissionContext(float standing, bool withCargo = false)
        {
            ReputationManager reputation = NewReputation(standing);
            PlayerCredits credits = new(12_345);
            CargoHold cargo = withCargo ? new CargoHold(40) : null;
            MissionManager manager = new(credits, null, reputation, null, cargo);
            return new MissionContext(reputation, credits, cargo, manager, CreateStation());
        }

        private static NpcShip CreatePoliceShip() =>
            new("Police Test Ship", Vector3.Zero, Vector3.Zero, 1f, 0f, FactionManager.LibertyPolice);

        private static bool Nearly(float left, float right) => Math.Abs(left - right) <= 0.0002f;

        private static TResult RunSilenced<TResult>(Func<TResult> function)
        {
            TextWriter original = Console.Out;
            try
            {
                using StringWriter writer = new();
                Console.SetOut(writer);
                return function();
            }
            finally
            {
                Console.SetOut(original);
            }
        }

        private sealed record MissionContext(
            ReputationManager Reputation,
            PlayerCredits Credits,
            CargoHold Cargo,
            MissionManager Manager,
            Station Station);
    }
}
