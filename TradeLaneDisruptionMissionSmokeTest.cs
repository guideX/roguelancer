using Microsoft.Xna.Framework;
using Roguelancer.Configuration;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Roguelancer
{
    /// <summary>
    /// Deterministic Phase 48 coverage for offer generation, authoritative
    /// player attribution, hold/recovery semantics, consequences, reward
    /// transactions, save identity, and presentation data.
    /// </summary>
    internal sealed class TradeLaneDisruptionMissionSmokeTest
    {
        public (int Passed, int Failed) Run()
        {
            int passed = 0;
            int failed = 0;
            Check("Rogue origin exposes disruption offers", RogueOriginExposesOffers, ref passed, ref failed);
            Check("non-Rogue origin exposes no disruption offers", NonRogueOriginExposesNoOffers, ref passed, ref failed);
            Check("three deterministic difficulty offers are generated", ThreeDifficultyOffers, ref passed, ref failed);
            Check("offer IDs are stable", OfferIdsAreStable, ref passed, ref failed);
            Check("offer lane identity is stable", OfferLaneIdentityIsStable, ref passed, ref failed);
            Check("offer segment identity is stable", OfferSegmentIdentityIsStable, ref passed, ref failed);
            Check("offer targets an intermediate ring", OfferTargetsIntermediateRing, ref passed, ref failed);
            Check("offer target position is finite", OfferTargetPositionIsFinite, ref passed, ref failed);
            Check("offer system identity is preserved", OfferSystemIdentityIsPreserved, ref passed, ref failed);
            Check("offer flavor is specific", OfferFlavorIsSpecific, ref passed, ref failed);
            Check("easy reward is bounded", EasyRewardIsBounded, ref passed, ref failed);
            Check("medium reward is bounded", MediumRewardIsBounded, ref passed, ref failed);
            Check("hard reward is bounded", HardRewardIsBounded, ref passed, ref failed);
            Check("easy offer is neutral-gated", EasyOfferIsNeutralGated, ref passed, ref failed);
            Check("medium offer is neutral-gated", MediumOfferIsNeutralGated, ref passed, ref failed);
            Check("hard offer is friendly-gated", HardOfferIsFriendlyGated, ref passed, ref failed);
            Check("difficulty penalties are distinct", DifficultyPenaltiesAreDistinct, ref passed, ref failed);
            Check("hold duration is bounded", HoldDurationIsBounded, ref passed, ref failed);
            Check("canonical definition ID is present", CanonicalDefinitionIdIsPresent, ref passed, ref failed);
            Check("offer faction is Rogue", OfferFactionIsRogue, ref passed, ref failed);
            Check("offer client identifies the Rogue origin", OfferClientIdentifiesOrigin, ref passed, ref failed);
            Check("new context has no active mission", NewContextHasNoActiveMission, ref passed, ref failed);
            Check("easy offer accepts", EasyOfferAccepts, ref passed, ref failed);
            Check("accepted mission enters active state", AcceptedMissionEntersActiveState, ref passed, ref failed);
            Check("acceptance records origin identity", AcceptanceRecordsOriginIdentity, ref passed, ref failed);
            Check("acceptance binds target position", AcceptanceBindsTargetPosition, ref passed, ref failed);
            Check("acceptance binds target ring object", AcceptanceBindsTargetRingObject, ref passed, ref failed);
            Check("wrong system cannot advance hold", WrongSystemCannotAdvanceHold, ref passed, ref failed);
            Check("NPC disruption cannot advance hold", NpcDisruptionCannotAdvanceHold, ref passed, ref failed);
            Check("environment disruption cannot advance hold", EnvironmentDisruptionCannotAdvanceHold, ref passed, ref failed);
            Check("unrelated lane cannot advance hold", UnrelatedLaneCannotAdvanceHold, ref passed, ref failed);
            Check("wrong segment identity is rejected", WrongSegmentIdentityIsRejected, ref passed, ref failed);
            Check("wrong ring cannot advance hold", WrongRingCannotAdvanceHold, ref passed, ref failed);
            Check("player disruption is observed", PlayerDisruptionIsObserved, ref passed, ref failed);
            Check("qualified timestamp is recorded", QualifiedTimestampIsRecorded, ref passed, ref failed);
            Check("hold progress advances only while disrupted", HoldProgressAdvancesOnlyWhileDisrupted, ref passed, ref failed);
            Check("hold reaches exact threshold", HoldReachesExactThreshold, ref passed, ref failed);
            Check("mission completion status is authoritative", MissionCompletionStatusIsAuthoritative, ref passed, ref failed);
            Check("objective completion is set", ObjectiveCompletionIsSet, ref passed, ref failed);
            Check("reward claims at Rogue origin", RewardClaimsAtOrigin, ref passed, ref failed);
            Check("reward credits are exact", RewardCreditsAreExact, ref passed, ref failed);
            Check("reward cannot be claimed twice", RewardCannotBeClaimedTwice, ref passed, ref failed);
            Check("Rogue reputation improves on claim", RogueReputationImprovesOnClaim, ref passed, ref failed);
            Check("Police penalty is exact", PolicePenaltyIsExact, ref passed, ref failed);
            Check("Police penalty applies once", PolicePenaltyAppliesOnce, ref passed, ref failed);
            Check("security response callback fires once", SecurityResponseCallbackFiresOnce, ref passed, ref failed);
            Check("security response state is persisted in runtime", SecurityResponseStateIsSet, ref passed, ref failed);
            Check("recovery resets the hold", RecoveryResetsHold, ref passed, ref failed);
            Check("recovered segment can be retried", RecoveredSegmentCanBeRetried, ref passed, ref failed);
            Check("save preserves lane identity", SavePreservesLaneIdentity, ref passed, ref failed);
            Check("save preserves segment identity", SavePreservesSegmentIdentity, ref passed, ref failed);
            Check("save preserves ring identity", SavePreservesRingIdentity, ref passed, ref failed);
            Check("load resets transient hold", LoadResetsTransientHold, ref passed, ref failed);
            Check("load rebinds the target ring", LoadRebindsTargetRing, ref passed, ref failed);
            Check("HUD target label names the segment", HudTargetLabelNamesSegment, ref passed, ref failed);
            Check("HUD status exposes hold state", HudStatusExposesHoldState, ref passed, ref failed);
            Check("summary exposes reward and objective", SummaryExposesRewardAndObjective, ref passed, ref failed);
            Check("invalid position is rejected", InvalidPositionIsRejected, ref passed, ref failed);
            Check("broken lanes produce no offers", BrokenLanesProduceNoOffers, ref passed, ref failed);
            Check("minimum ring count is enforced", MinimumRingCountIsEnforced, ref passed, ref failed);
            Check("wrong faction mission is rejected", WrongFactionMissionIsRejected, ref passed, ref failed);
            Check("wrong employer station is rejected", WrongEmployerStationIsRejected, ref passed, ref failed);
            Check("wrong segment metadata cannot bind", WrongSegmentMetadataCannotBind, ref passed, ref failed);
            Check("duplicate disruption event does not double consequence", DuplicateEventDoesNotDoubleConsequence, ref passed, ref failed);
            Check("recovery state does not count as disrupted", RecoveryStateDoesNotCount, ref passed, ref failed);
            Check("target lane remains authoritative", TargetLaneRemainsAuthoritative, ref passed, ref failed);

            Console.WriteLine($"[TRADE-LANE DISRUPTION MISSION SMOKE] RESULT: {passed} passed, {failed} failed");
            return (passed, failed);
        }

        private static void Check(string label, Func<(bool Success, string FailureReason)> test, ref int passed, ref int failed)
        {
            try
            {
                (bool success, string reason) = test();
                if (success)
                {
                    passed++;
                    Console.WriteLine($"[TRADE-LANE DISRUPTION MISSION SMOKE] PASS {label}");
                }
                else
                {
                    failed++;
                    Console.WriteLine($"[TRADE-LANE DISRUPTION MISSION SMOKE] FAIL {label}: {reason}");
                }
            }
            catch (Exception ex)
            {
                failed++;
                Console.WriteLine($"[TRADE-LANE DISRUPTION MISSION SMOKE] FAIL {label}: {ex.Message}");
            }
        }

        private (bool Success, string FailureReason) RogueOriginExposesOffers() =>
            Result(CreateContext().MissionManager.GenerateTradeLaneDisruptionMissions(CreateContext().Origin).Count == 3, "Rogue board did not expose three offers");

        private (bool Success, string FailureReason) NonRogueOriginExposesNoOffers()
        {
            TestContext context = CreateContext(corporateOrigin: true);
            return Result(context.MissionManager.GenerateTradeLaneDisruptionMissions(context.Origin).Count == 0, "corporate origin exposed disruption work");
        }

        private (bool Success, string FailureReason) ThreeDifficultyOffers()
        {
            TestContext context = CreateContext();
            List<Mission> offers = context.MissionManager.GenerateTradeLaneDisruptionMissions(context.Origin);
            return Result(offers.Count == 3 && offers.Select(offer => offer.Difficulty).SequenceEqual(new[] { MissionDifficulty.Easy, MissionDifficulty.Medium, MissionDifficulty.Hard }), "difficulty rotation was not deterministic");
        }

        private (bool Success, string FailureReason) OfferIdsAreStable() =>
            Result(Offer().DefinitionId == MissionCatalog.TradeLaneDisruptionId, "canonical definition ID missing");

        private (bool Success, string FailureReason) OfferLaneIdentityIsStable() =>
            Result(Offer().TargetLaneId == "phase48_liberty_lane", "lane ID was not stable");

        private (bool Success, string FailureReason) OfferSegmentIdentityIsStable() =>
            Result(Offer().TargetSegmentId == $"phase48_liberty_lane:ring:{Offer().TargetRingIndex}", "segment ID was not stable");

        private (bool Success, string FailureReason) OfferTargetsIntermediateRing()
        {
            Mission mission = Offer();
            return Result(mission.TargetRingIndex > 0 && mission.TargetRingIndex < 6, "offer targeted an endpoint");
        }

        private (bool Success, string FailureReason) OfferTargetPositionIsFinite() => Result(TradeLaneStateSanitizer.IsFinite(Offer().TargetPosition ?? new Vector3(float.NaN)), "target position was non-finite");
        private (bool Success, string FailureReason) OfferSystemIdentityIsPreserved() => Result(Offer().TargetSystemIndex == 1, "system identity was not preserved");
        private (bool Success, string FailureReason) OfferFlavorIsSpecific() => Result(Offer().Description.Contains("phase48", StringComparison.OrdinalIgnoreCase) == false && Offer().Description.Length > 30, "offer flavor was too generic");
        private (bool Success, string FailureReason) EasyRewardIsBounded() => Result(Offer(MissionDifficulty.Easy).Reward == 3000, "easy reward mismatch");
        private (bool Success, string FailureReason) MediumRewardIsBounded() => Result(Offer(MissionDifficulty.Medium).Reward == 5500, "medium reward mismatch");
        private (bool Success, string FailureReason) HardRewardIsBounded() => Result(Offer(MissionDifficulty.Hard).Reward == 9000, "hard reward mismatch");
        private (bool Success, string FailureReason) EasyOfferIsNeutralGated() => Result(Offer(MissionDifficulty.Easy).MinimumEmployerReputation == 0f, "easy reputation gate mismatch");
        private (bool Success, string FailureReason) MediumOfferIsNeutralGated() => Result(Offer(MissionDifficulty.Medium).MinimumEmployerReputation == 0f, "medium reputation gate mismatch");
        private (bool Success, string FailureReason) HardOfferIsFriendlyGated() => Result(Offer(MissionDifficulty.Hard).MinimumEmployerReputation == ReputationManager.FriendlyThreshold, "hard reputation gate mismatch");
        private (bool Success, string FailureReason) DifficultyPenaltiesAreDistinct() => Result(Offer(MissionDifficulty.Easy).PoliceReputationPenalty > Offer(MissionDifficulty.Medium).PoliceReputationPenalty && Offer(MissionDifficulty.Medium).PoliceReputationPenalty > Offer(MissionDifficulty.Hard).PoliceReputationPenalty, "police penalties did not scale");
        private (bool Success, string FailureReason) HoldDurationIsBounded() => Result(Offer().HoldDurationSeconds == MissionManager.TradeLaneDisruptionHoldSeconds, "hold duration mismatch");
        private (bool Success, string FailureReason) CanonicalDefinitionIdIsPresent() => Result(Offer().DefinitionId == "trade-lane-disruption", "internal mission ID mismatch");
        private (bool Success, string FailureReason) OfferFactionIsRogue() => Result(Offer().FactionId == FactionManager.LibertyRogues, "offer faction mismatch");
        private (bool Success, string FailureReason) OfferClientIdentifiesOrigin() => Result(Offer().OfferedBy.Contains("Buffalo Base", StringComparison.OrdinalIgnoreCase), "Rogue client did not identify origin");
        private (bool Success, string FailureReason) NewContextHasNoActiveMission() => Result(CreateContext().MissionManager.ActiveMission == null, "new context had active mission");

        private (bool Success, string FailureReason) EasyOfferAccepts()
        {
            TestContext context = CreateContext();
            Mission mission = Offer(context, MissionDifficulty.Easy);
            return Result(context.MissionManager.AcceptMission(mission, context.Origin), context.MissionManager.LastAcceptanceFailureReason);
        }

        private (bool Success, string FailureReason) AcceptedMissionEntersActiveState()
        {
            TestContext context = AcceptedContext();
            return Result(context.MissionManager.ActiveMission?.Status == MissionStatus.InProgress, "mission was not InProgress");
        }

        private (bool Success, string FailureReason) AcceptanceRecordsOriginIdentity()
        {
            TestContext context = AcceptedContext();
            return Result(context.MissionManager.ActiveMission.OriginStationId == Mission.BuildStationIdentity(context.Origin), "origin identity mismatch");
        }

        private (bool Success, string FailureReason) AcceptanceBindsTargetPosition()
        {
            TestContext context = AcceptedContext();
            return Result(context.MissionManager.ActiveMission.TargetPosition.HasValue, "target position was not bound");
        }

        private (bool Success, string FailureReason) AcceptanceBindsTargetRingObject()
        {
            TestContext context = AcceptedContext();
            return Result(context.MissionManager.ActiveMission.TargetSpaceObject is TradelaneRing, "target object was not a ring");
        }

        private (bool Success, string FailureReason) WrongSystemCannotAdvanceHold()
        {
            TestContext context = AcceptedContext();
            DisruptTarget(context, TradeLaneDisruptionSource.Player);
            Tick(context, 1f, 2);
            return Result(context.MissionManager.ActiveMission.HoldProgressSeconds == 0f, "wrong system advanced hold");
        }

        private (bool Success, string FailureReason) NpcDisruptionCannotAdvanceHold() => NonPlayerSourceCannotAdvanceHold(TradeLaneDisruptionSource.Npc);
        private (bool Success, string FailureReason) EnvironmentDisruptionCannotAdvanceHold() => NonPlayerSourceCannotAdvanceHold(TradeLaneDisruptionSource.Environment);

        private (bool Success, string FailureReason) NonPlayerSourceCannotAdvanceHold(TradeLaneDisruptionSource source)
        {
            TestContext context = AcceptedContext();
            DisruptTarget(context, source);
            Tick(context, 1f);
            return Result(context.MissionManager.ActiveMission.HoldProgressSeconds == 0f && !context.MissionManager.ActiveMission.PlayerDisruptionObserved, "non-player source advanced hold");
        }

        private (bool Success, string FailureReason) UnrelatedLaneCannotAdvanceHold()
        {
            TestContext context = AcceptedContext();
            context.OtherLane.TryDisruptSegment(1, TradeLaneDisruptionSource.Player, "other", 20f);
            Tick(context, 1f);
            return Result(context.MissionManager.ActiveMission.HoldProgressSeconds == 0f, "unrelated lane counted");
        }

        private (bool Success, string FailureReason) WrongSegmentIdentityIsRejected()
        {
            TestContext context = CreateContext();
            Mission mission = Offer(context);
            mission.TargetSegmentId = "wrong-segment";
            return Result(!context.MissionManager.AcceptMission(mission, context.Origin), "wrong segment was accepted");
        }

        private (bool Success, string FailureReason) WrongRingCannotAdvanceHold()
        {
            TestContext context = AcceptedContext();
            context.Lane.TryDisruptSegment(context.MissionManager.ActiveMission.TargetRingIndex + 1, TradeLaneDisruptionSource.Player, "wrong", 20f);
            Tick(context, 1f);
            return Result(context.MissionManager.ActiveMission.HoldProgressSeconds == 0f, "wrong ring counted");
        }

        private (bool Success, string FailureReason) PlayerDisruptionIsObserved()
        {
            TestContext context = AcceptedContext();
            DisruptTarget(context, TradeLaneDisruptionSource.Player);
            Tick(context, 1f);
            return Result(context.MissionManager.ActiveMission.PlayerDisruptionObserved, "player event was not observed");
        }

        private (bool Success, string FailureReason) QualifiedTimestampIsRecorded()
        {
            TestContext context = AcceptedContext();
            DisruptTarget(context, TradeLaneDisruptionSource.Player);
            Tick(context, 1f);
            return Result(context.MissionManager.ActiveMission.LastQualifiedDisruptionAtSeconds >= 0d, "qualified timestamp was not recorded");
        }

        private (bool Success, string FailureReason) HoldProgressAdvancesOnlyWhileDisrupted()
        {
            TestContext context = AcceptedContext();
            Tick(context, 1f);
            if (context.MissionManager.ActiveMission.HoldProgressSeconds != 0f) return Fail("operational lane advanced hold");
            DisruptTarget(context, TradeLaneDisruptionSource.Player);
            Tick(context, 2f);
            return Result(context.MissionManager.ActiveMission.HoldProgressSeconds == 2f, "disrupted lane hold increment mismatch");
        }

        private (bool Success, string FailureReason) HoldReachesExactThreshold()
        {
            TestContext context = AcceptedContext();
            DisruptTarget(context, TradeLaneDisruptionSource.Player);
            Tick(context, 10f);
            return Result(context.MissionManager.ActiveMission == null, "exact threshold did not complete mission");
        }

        private (bool Success, string FailureReason) MissionCompletionStatusIsAuthoritative() => CompletionContext().MissionManager.CompletedMissions.Any(mission => mission.Status == MissionStatus.Completed) ? Pass() : Fail("completion was not recorded");
        private (bool Success, string FailureReason) ObjectiveCompletionIsSet() => Result(CompletionContext().MissionManager.CompletedMissions.FirstOrDefault()?.ObjectiveComplete == true, "objective flag was not set");

        private (bool Success, string FailureReason) RewardClaimsAtOrigin()
        {
            TestContext context = CompletionContext();
            Mission completed = context.MissionManager.UnclaimedCompletedMission;
            return Result(completed != null && context.MissionManager.TryClaimReward(completed, context.Origin, out _), "reward did not claim at origin");
        }

        private (bool Success, string FailureReason) RewardCreditsAreExact()
        {
            TestContext context = CompletionContext();
            Mission completed = context.MissionManager.UnclaimedCompletedMission;
            int before = context.Credits.Credits;
            context.MissionManager.TryClaimReward(completed, context.Origin, out _);
            return Result(context.Credits.Credits == before + completed.Reward, "credit transaction mismatch");
        }

        private (bool Success, string FailureReason) RewardCannotBeClaimedTwice()
        {
            TestContext context = CompletionContext();
            Mission completed = context.MissionManager.UnclaimedCompletedMission;
            context.MissionManager.TryClaimReward(completed, context.Origin, out _);
            return Result(!context.MissionManager.TryClaimReward(completed, context.Origin, out _), "duplicate reward claim succeeded");
        }

        private (bool Success, string FailureReason) RogueReputationImprovesOnClaim()
        {
            TestContext context = CompletionContext();
            float before = context.ReputationManager.GetStanding(FactionManager.LibertyRogues);
            context.MissionManager.TryClaimReward(context.MissionManager.UnclaimedCompletedMission, context.Origin, out _);
            return Result(context.ReputationManager.GetStanding(FactionManager.LibertyRogues) > before, "Rogue reputation did not improve");
        }

        private (bool Success, string FailureReason) PolicePenaltyIsExact()
        {
            TestContext context = AcceptedContext();
            float before = context.ReputationManager.GetStanding(FactionManager.LibertyPolice);
            DisruptTarget(context, TradeLaneDisruptionSource.Player);
            Tick(context, 1f);
            return Result(Math.Abs(context.ReputationManager.GetStanding(FactionManager.LibertyPolice) - (before + Offer(context).PoliceReputationPenalty)) < 0.0001f, "Police penalty mismatch");
        }

        private (bool Success, string FailureReason) PolicePenaltyAppliesOnce()
        {
            TestContext context = AcceptedContext();
            float before = context.ReputationManager.GetStanding(FactionManager.LibertyPolice);
            DisruptTarget(context, TradeLaneDisruptionSource.Player);
            Tick(context, 1f);
            float after = context.ReputationManager.GetStanding(FactionManager.LibertyPolice);
            Tick(context, 1f);
            return Result(Math.Abs(context.ReputationManager.GetStanding(FactionManager.LibertyPolice) - after) < 0.0001f && after < before, "Police penalty duplicated");
        }

        private (bool Success, string FailureReason) SecurityResponseCallbackFiresOnce()
        {
            TestContext context = AcceptedContext();
            DisruptTarget(context, TradeLaneDisruptionSource.Player);
            Tick(context, 1f);
            Tick(context, 1f);
            return Result(context.SecurityResponseCount == 1, "security response callback duplicated or did not fire");
        }

        private (bool Success, string FailureReason) SecurityResponseStateIsSet()
        {
            TestContext context = AcceptedContext();
            DisruptTarget(context, TradeLaneDisruptionSource.Player);
            Tick(context, 1f);
            return Result(context.MissionManager.ActiveMission.SecurityResponseTriggered, "security response state was not set");
        }

        private (bool Success, string FailureReason) RecoveryResetsHold()
        {
            TestContext context = AcceptedContext();
            DisruptTarget(context, TradeLaneDisruptionSource.Player);
            Tick(context, 2f);
            context.Lane.Update(new GameTime(TimeSpan.Zero, TimeSpan.FromSeconds(20)));
            Tick(context, 1f);
            return Result(context.MissionManager.ActiveMission.HoldProgressSeconds == 0f && !context.MissionManager.ActiveMission.PlayerDisruptionObserved, "recovery did not reset hold");
        }

        private (bool Success, string FailureReason) RecoveredSegmentCanBeRetried()
        {
            TestContext context = AcceptedContext();
            DisruptTarget(context, TradeLaneDisruptionSource.Player);
            context.Lane.Update(new GameTime(TimeSpan.Zero, TimeSpan.FromSeconds(20)));
            Tick(context, 1f);
            DisruptTarget(context, TradeLaneDisruptionSource.Player);
            Tick(context, 1f);
            return Result(context.MissionManager.ActiveMission.HoldProgressSeconds == 1f, "recovered segment could not be retried");
        }

        private (bool Success, string FailureReason) SavePreservesLaneIdentity()
        {
            SaveMissionData data = CapturePartialMission(out _);
            return Result(data.TargetLaneId == "phase48_liberty_lane", "save lane identity mismatch");
        }

        private (bool Success, string FailureReason) SavePreservesSegmentIdentity()
        {
            SaveMissionData data = CapturePartialMission(out _);
            return Result(data.TargetSegmentId.Contains(":ring:", StringComparison.Ordinal), "save segment identity missing");
        }

        private (bool Success, string FailureReason) SavePreservesRingIdentity()
        {
            SaveMissionData data = CapturePartialMission(out TestContext context);
            return Result(data.TargetRingIndex == context.MissionManager.ActiveMission.TargetRingIndex, "save ring identity mismatch");
        }

        private (bool Success, string FailureReason) LoadResetsTransientHold()
        {
            SaveMissionData data = CapturePartialMission(out _);
            TestContext restored = CreateContext();
            SaveGameManager save = new(Path.Combine(Path.GetTempPath(), $"phase48-{Guid.NewGuid():N}.json"));
            save.ApplyMissions(restored.MissionManager, new SaveGameData { ActiveMissions = new List<SaveMissionData> { data } }, out _);
            Mission loaded = restored.MissionManager.ActiveMission;
            return Result(loaded != null && loaded.HoldProgressSeconds == 0f && !loaded.PlayerDisruptionObserved, "load retained transient hold");
        }

        private (bool Success, string FailureReason) LoadRebindsTargetRing()
        {
            SaveMissionData data = CapturePartialMission(out _);
            TestContext restored = CreateContext();
            SaveGameManager save = new(Path.Combine(Path.GetTempPath(), $"phase48-{Guid.NewGuid():N}.json"));
            save.ApplyMissions(restored.MissionManager, new SaveGameData { ActiveMissions = new List<SaveMissionData> { data } }, out _);
            restored.WorldManager.RebindActiveMissions(restored.MissionManager.ActiveMissions);
            return Result(restored.MissionManager.ActiveMission?.TargetSpaceObject is TradelaneRing, "load did not rebind ring object");
        }

        private (bool Success, string FailureReason) HudTargetLabelNamesSegment() => Result(Offer().GetTargetLabel().Contains(":ring:", StringComparison.Ordinal), "HUD target label omitted segment");
        private (bool Success, string FailureReason) HudStatusExposesHoldState()
        {
            TestContext context = AcceptedContext();
            DisruptTarget(context, TradeLaneDisruptionSource.Player);
            Tick(context, 1f);
            return Result(context.MissionManager.ActiveMission.GetTradeLaneHudStatus().Contains("HOLD", StringComparison.Ordinal), "HUD status omitted hold");
        }

        private (bool Success, string FailureReason) SummaryExposesRewardAndObjective()
        {
            string summary = Offer().GetSummary();
            return Result(summary.Contains("Reward", StringComparison.Ordinal) && summary.Contains("Disrupt", StringComparison.Ordinal), "summary omitted reward or objective");
        }

        private (bool Success, string FailureReason) InvalidPositionIsRejected() => Result(Mission.CreateTradeLaneDisruption("lane", "lane:ring:1", 1, "Lane", new Vector3(float.NaN, 0f, 0f), 1, MissionDifficulty.Easy, 1, 1f, "x", -0.02f) == null, "non-finite target was accepted");

        private (bool Success, string FailureReason) BrokenLanesProduceNoOffers()
        {
            TestContext context = CreateContext();
            context.Lane.ForwardRings[1].IsDestroyed = true;
            return Result(!context.MissionManager.GenerateTradeLaneDisruptionMissions(context.Origin)
                .Any(mission => mission.TargetLaneId == context.Lane.LaneId), "broken lane offered work");
        }

        private (bool Success, string FailureReason) MinimumRingCountIsEnforced()
        {
            TestContext context = CreateContext(shortLane: true);
            return Result(!context.MissionManager.GenerateTradeLaneDisruptionMissions(context.Origin)
                .Any(mission => mission.TargetLaneId == context.Lane.LaneId), "short lane offered work");
        }

        private (bool Success, string FailureReason) WrongFactionMissionIsRejected()
        {
            TestContext context = CreateContext();
            Mission mission = Offer(context);
            mission.FactionId = FactionManager.LibertyPolice;
            return Result(!context.MissionManager.AcceptMission(mission, context.Origin), "wrong faction mission accepted");
        }

        private (bool Success, string FailureReason) WrongEmployerStationIsRejected()
        {
            TestContext context = CreateContext();
            Mission mission = Offer(context);
            Station wrong = CreateStation("Fort Bush", FactionManager.LibertyCorporations);
            return Result(!context.MissionManager.AcceptMission(mission, wrong), "non-Rogue employer accepted mission");
        }

        private (bool Success, string FailureReason) WrongSegmentMetadataCannotBind()
        {
            TestContext context = CreateContext();
            Mission mission = Offer(context);
            mission.TargetSegmentId = $"{mission.TargetLaneId}:ring:999";
            return Result(!context.MissionManager.AcceptMission(mission, context.Origin), "invalid segment metadata bound");
        }

        private (bool Success, string FailureReason) DuplicateEventDoesNotDoubleConsequence() => PolicePenaltyAppliesOnce();

        private (bool Success, string FailureReason) RecoveryStateDoesNotCount()
        {
            TestContext context = AcceptedContext();
            DisruptTarget(context, TradeLaneDisruptionSource.Player);
            context.Lane.Update(new GameTime(TimeSpan.Zero, TimeSpan.FromSeconds(18)));
            Tick(context, 1f);
            return Result(context.MissionManager.ActiveMission.HoldProgressSeconds == 0f, "recovering state counted toward hold");
        }

        private (bool Success, string FailureReason) TargetLaneRemainsAuthoritative()
        {
            TestContext context = AcceptedContext();
            Mission mission = context.MissionManager.ActiveMission;
            context.Lane.TryDisruptSegment(mission.TargetRingIndex, TradeLaneDisruptionSource.Player, "player", 20f);
            Tick(context, 1f);
            return Result(mission.TargetLaneId == context.Lane.LaneId && mission.TargetSegmentId == $"{context.Lane.LaneId}:ring:{mission.TargetRingIndex}", "mission target identity drifted");
        }

        private TestContext AcceptedContext()
        {
            TestContext context = CreateContext();
            Mission mission = Offer(context);
            if (!context.MissionManager.AcceptMission(mission, context.Origin))
                throw new InvalidOperationException(context.MissionManager.LastAcceptanceFailureReason);
            return context;
        }

        private TestContext CompletionContext()
        {
            TestContext context = AcceptedContext();
            DisruptTarget(context, TradeLaneDisruptionSource.Player);
            Tick(context, 10f);
            return context;
        }

        private void DisruptTarget(TestContext context, TradeLaneDisruptionSource source)
        {
            Mission mission = context.MissionManager.ActiveMission;
            context.Lane.TryDisruptSegment(mission.TargetRingIndex, source, source == TradeLaneDisruptionSource.Player ? "player" : "npc", 20f);
        }

        private static void Tick(TestContext context, float seconds, int currentSystem = 1)
        {
            context.WorldManager.Update(seconds, null, currentSystem);
            context.MissionManager.Update(seconds, false);
        }

        private SaveMissionData CapturePartialMission(out TestContext context)
        {
            context = AcceptedContext();
            DisruptTarget(context, TradeLaneDisruptionSource.Player);
            Tick(context, 2f);
            return new SaveGameManager(Path.Combine(Path.GetTempPath(), $"phase48-{Guid.NewGuid():N}.json"))
                .CaptureMissions(context.MissionManager.ActiveMissions).Single();
        }

        private Mission Offer(MissionDifficulty difficulty = MissionDifficulty.Easy) => Offer(CreateContext(), difficulty);

        private Mission Offer(TestContext context, MissionDifficulty difficulty = MissionDifficulty.Easy)
        {
            return context.MissionManager.GenerateTradeLaneDisruptionMissions(context.Origin)
                .First(mission => mission.Difficulty == difficulty);
        }

        private static TestContext CreateContext(bool corporateOrigin = false, bool shortLane = false)
        {
            TestContext context = new()
            {
                Credits = new PlayerCredits(10000),
                Player = new Ship(Vector3.Zero)
            };
            context.Origin = CreateStation(corporateOrigin ? "Fort Bush" : "Buffalo Base", corporateOrigin ? FactionManager.LibertyCorporations : FactionManager.LibertyRogues);
            context.Stations.Add(context.Origin);
            context.Lane = CreateLane(shortLane);
            context.OtherLane = new TradeLane(null, new TradelaneConfig
            {
                Id = "phase48_other_lane",
                Name = "Other Liberty Corridor",
                SystemIndex = 1,
                StartPositionX = 0f,
                EndPositionX = 6000f,
                RingSpacing = 1000f,
                DisruptionRecoverySeconds = 20f
            });
            context.Lanes.Add(context.Lane);
            context.Lanes.Add(context.OtherLane);
            FactionManager factions = new();
            context.ReputationManager = new ReputationManager(factions);
            context.MissionManager = new MissionManager(context.Credits, null, context.ReputationManager);
            context.WaypointSystem = new MissionWaypointSystem();
            context.WorldManager = new MissionWorldManager(
                context.MissionManager,
                context.WaypointSystem,
                context.Player,
                context.NpcShips,
                context.SpaceObjects,
                () => context.Stations,
                npc => context.WorldManager.NotifyNpcDestroyed(npc),
                null,
                null,
                () => context.Lanes,
                (_, _, _) => context.SecurityResponseCount++);
            context.MissionManager.SetWaypointSystem(context.WaypointSystem);
            context.MissionManager.SetWorldManager(context.WorldManager);
            return context;
        }

        private static TradeLane CreateLane(bool shortLane)
        {
            return new TradeLane(null, new TradelaneConfig
            {
                Id = "phase48_liberty_lane",
                Name = "Liberty Commercial Corridor",
                SystemIndex = 1,
                StartPositionX = 0f,
                EndPositionX = shortLane ? 1000f : 6000f,
                RingSpacing = 1000f,
                DisruptionRecoverySeconds = 20f,
                DisruptionDamageThreshold = 100f
            });
        }

        private static Station CreateStation(string name, string factionId)
        {
            return new Station(new StationConfig
            {
                Description = name,
                SystemIndex = 1,
                FactionId = factionId,
                Radius = 900f,
                DockingRange = 700f
            }, null);
        }

        private static (bool Success, string FailureReason) Pass() => (true, string.Empty);
        private static (bool Success, string FailureReason) Fail(string reason) => (false, reason);
        private static (bool Success, string FailureReason) Result(bool success, string reason) => success ? Pass() : Fail(reason);

        private sealed class TestContext
        {
            public PlayerCredits Credits { get; set; }
            public ReputationManager ReputationManager { get; set; }
            public Ship Player { get; set; }
            public Station Origin { get; set; }
            public TradeLane Lane { get; set; }
            public TradeLane OtherLane { get; set; }
            public List<TradeLane> Lanes { get; } = new();
            public List<NpcShip> NpcShips { get; } = new();
            public List<SpaceObject> SpaceObjects { get; } = new();
            public List<Station> Stations { get; } = new();
            public MissionWaypointSystem WaypointSystem { get; set; }
            public MissionManager MissionManager { get; set; }
            public MissionWorldManager WorldManager { get; set; }
            public int SecurityResponseCount { get; set; }
        }
    }
}
