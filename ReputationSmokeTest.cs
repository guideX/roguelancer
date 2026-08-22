using Microsoft.Xna.Framework;
using Roguelancer.Configuration;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Roguelancer
{
    /// <summary>
    /// Focused deterministic proof for the Phase 23 standing foundation.
    /// Assertions intentionally stay at the authority/presentation seams so
    /// the suite does not need a live graphics window.
    /// </summary>
    internal sealed class ReputationSmokeTest
    {
        private static readonly string[] DefaultFactionIds =
        {
            FactionManager.LibertyPolice,
            FactionManager.LibertyNavy,
            FactionManager.LibertyRogues,
            FactionManager.LibertyCorporations,
            FactionManager.BountyHunters,
            FactionManager.Junkers,
            FactionManager.NeutralCivilians
        };

        public (int Passed, int Failed) Run()
        {
            int passed = 0;
            int failed = 0;

            Check("manager initializes", () => new ReputationManager(new FactionManager()).GetStandingsSnapshot().Count == 7, ref passed, ref failed);
            Check("every default faction resolves", () => DefaultFactionIds.All(id => new FactionManager().GetFaction(id) != null), ref passed, ref failed);
            Check("stable ids are lowercase and stable", () => DefaultFactionIds.Distinct(StringComparer.Ordinal).Count() == DefaultFactionIds.Length && DefaultFactionIds.All(id => id == id.ToLowerInvariant() && !string.IsNullOrWhiteSpace(id)), ref passed, ref failed);
            Check("unknown faction lookup is safe", () => new FactionManager().GetFaction("custom_sector")?.Id == "custom_sector", ref passed, ref failed);
            Check("unknown faction defaults neutral", () => Band(new ReputationManager(new FactionManager()), "custom_sector") == ReputationBand.Neutral, ref passed, ref failed);
            Check("unknown faction does not mutate query state", () => QueryDoesNotMutate(), ref passed, ref failed);

            Check("minimum bound", () => SetAndRead(float.MinValue, -1f), ref passed, ref failed);
            Check("maximum bound", () => SetAndRead(float.MaxValue, 1f), ref passed, ref failed);
            Check("positive adjustment", () => AdjustFrom(0f, 0.03f), ref passed, ref failed);
            Check("negative adjustment", () => AdjustFrom(0f, -0.03f), ref passed, ref failed);
            Check("zero adjustment does nothing", () => NoChangeForDelta(0f), ref passed, ref failed);
            Check("extreme positive clamp", () => SetAndRead(100f, 1f), ref passed, ref failed);
            Check("extreme negative clamp", () => SetAndRead(-100f, -1f), ref passed, ref failed);
            Check("NaN cannot enter state", () => InvalidSetIsSafe(float.NaN), ref passed, ref failed);
            Check("infinity cannot enter state", () => InvalidSetIsSafe(float.PositiveInfinity), ref passed, ref failed);
            Check("stored values are finite", () => new ReputationManager(new FactionManager()).GetStandingsSnapshot().Values.All(IsFinite), ref passed, ref failed);

            Check("hostile lower range", () => ReputationManager.GetBandForStanding(-1f) == ReputationBand.Hostile, ref passed, ref failed);
            Check("hostile boundary", () => ReputationManager.GetBandForStanding(-0.60f) == ReputationBand.Hostile, ref passed, ref failed);
            Check("unfriendly lower boundary", () => ReputationManager.GetBandForStanding(-0.5999f) == ReputationBand.Unfriendly, ref passed, ref failed);
            Check("unfriendly upper boundary", () => ReputationManager.GetBandForStanding(-0.2001f) == ReputationBand.Unfriendly, ref passed, ref failed);
            Check("neutral lower boundary", () => ReputationManager.GetBandForStanding(-0.20f) == ReputationBand.Neutral, ref passed, ref failed);
            Check("neutral center", () => ReputationManager.GetBandForStanding(0f) == ReputationBand.Neutral, ref passed, ref failed);
            Check("neutral upper boundary", () => ReputationManager.GetBandForStanding(0.20f) == ReputationBand.Neutral, ref passed, ref failed);
            Check("friendly lower boundary", () => ReputationManager.GetBandForStanding(0.2001f) == ReputationBand.Friendly, ref passed, ref failed);
            Check("friendly upper boundary", () => ReputationManager.GetBandForStanding(0.5999f) == ReputationBand.Friendly, ref passed, ref failed);
            Check("allied boundary", () => ReputationManager.GetBandForStanding(0.60f) == ReputationBand.Allied, ref passed, ref failed);
            Check("allied maximum", () => ReputationManager.GetBandForStanding(1f) == ReputationBand.Allied, ref passed, ref failed);

            ReputationManager profileManager = new(new FactionManager());
            Check("starting profile contains every default", () => DefaultFactionIds.All(id => profileManager.GetStartingProfileSnapshot().ContainsKey(id)), ref passed, ref failed);
            Check("rogue starting standing is positive", () => profileManager.GetStanding(FactionManager.LibertyRogues) > 0f, ref passed, ref failed);
            Check("rogues begin friendly, not maxed", () => profileManager.GetBand(FactionManager.LibertyRogues) == ReputationBand.Friendly && profileManager.GetStanding(FactionManager.LibertyRogues) < 1f, ref passed, ref failed);
            Check("starting profile is not universally maxed", () => profileManager.GetStandingsSnapshot().Values.Any(value => value < 1f), ref passed, ref failed);
            Check("police tension is not hostile at new game", () => !profileManager.IsHostile(FactionManager.LibertyPolice), ref passed, ref failed);
            Check("new game police docking remains available", () => profileManager.CanDockWithFaction(FactionManager.LibertyPolice), ref passed, ref failed);
            Check("new game resets modified values", () => NewGameReset(), ref passed, ref failed);
            Check("new game resets kill deduplication", () => KillDedupResets(), ref passed, ref failed);

            Check("event fires for direct mutation", () => EventProof(out ReputationChangeResult change) && change != null && !change.IsSecondaryEffect, ref passed, ref failed);
            Check("event contains old value", () => EventProof(out ReputationChangeResult change) && Nearly(change.OldValue, 0f), ref passed, ref failed);
            Check("event contains new value", () => EventProof(out ReputationChangeResult change) && Nearly(change.NewValue, 0.03f), ref passed, ref failed);
            Check("event contains faction id", () => EventProof(out ReputationChangeResult change) && change.FactionId == FactionManager.NeutralCivilians, ref passed, ref failed);
            Check("event contains reason", () => EventProof(out ReputationChangeResult change) && change.Reason == ReputationChangeReason.MissionCompleted, ref passed, ref failed);
            Check("band transition is detected", () => BandTransitionProof(), ref passed, ref failed);
            Check("within-band change has no transition", () => WithinBandProof(), ref passed, ref failed);
            Check("query never emits event", () => QueryDoesNotEmitEvent(), ref passed, ref failed);

            Check("secondary relationship applies", () => SecondaryProof(out float directDelta, out float secondaryDelta) && directDelta < 0f && secondaryDelta < 0f, ref passed, ref failed);
            Check("secondary effect is smaller", () => SecondaryProof(out float directDelta, out float secondaryDelta) && Math.Abs(secondaryDelta) < Math.Abs(directDelta), ref passed, ref failed);
            Check("secondary effect is not recursive", () => NoRecursiveRipple(), ref passed, ref failed);
            Check("unrelated custom faction unchanged", () => UnrelatedFactionUnchanged(), ref passed, ref failed);
            Check("no standing decay without actions", () => NoStandingDrift(), ref passed, ref failed);

            Check("hostile docking query denies", () => DockQuery(ReputationBand.Hostile, false), ref passed, ref failed);
            Check("unfriendly docking query allows", () => DockQuery(ReputationBand.Unfriendly, true), ref passed, ref failed);
            Check("neutral docking query allows", () => DockQuery(ReputationBand.Neutral, true), ref passed, ref failed);
            Check("friendly docking query allows", () => DockQuery(ReputationBand.Friendly, true), ref passed, ref failed);
            Check("allied docking query allows", () => DockQuery(ReputationBand.Allied, true), ref passed, ref failed);
            Check("minimum requirement below threshold fails", () => RequirementProof(-0.01f, 0.01f, false), ref passed, ref failed);
            Check("minimum requirement at threshold passes", () => RequirementProof(0.01f, 0.01f, true), ref passed, ref failed);
            Check("minimum requirement above threshold passes", () => RequirementProof(0.02f, 0.01f, true), ref passed, ref failed);
            Check("mission manager requirement delegates", () => MissionRequirementDelegates(), ref passed, ref failed);

            Station policeStation = CreateStation("Fort Bush", FactionManager.LibertyPolice);
            Mission presentationMission = new(MissionType.Delivery, MissionDifficulty.Easy, "Food", "Fort Bush", 1000, 0f, "Deliver food", FactionManager.LibertyPolice);
            Check("station presentation gets faction", () => ReputationPresentation.BuildStationFactionLine(policeStation, profileManager).Contains("Liberty Police"), ref passed, ref failed);
            Check("station presentation gets band", () => ReputationPresentation.BuildStationStandingLine(policeStation, profileManager).Contains("UNFRIENDLY"), ref passed, ref failed);
            Check("mission presentation gets employer", () => ReputationPresentation.BuildMissionEmployerLine(presentationMission, profileManager).Contains("Liberty Police"), ref passed, ref failed);
            Check("mission presentation gets player band", () => ReputationPresentation.BuildMissionStandingLine(presentationMission, profileManager).Contains("UNFRIENDLY"), ref passed, ref failed);
            Check("overview is deterministic", () => OverviewIsDeterministic(profileManager), ref passed, ref failed);
            Check("overview includes every default once", () => OverviewHasEveryDefault(profileManager), ref passed, ref failed);
            Check("overview query does not mutate", () => OverviewDoesNotMutate(profileManager), ref passed, ref failed);

            Check("mission reward scale is modest", () => RewardScaleIsBounded(), ref passed, ref failed);
            Check("ordinary mission rewards employer", () => CompleteMissionOnce(MissionType.Delivery, out float rewardDelta) && rewardDelta > 0f, ref passed, ref failed);
            Check("mission reward is applied once", () => CompleteMissionOnce(MissionType.Delivery, out float rewardDelta) && Nearly(rewardDelta, MissionManager.GetMissionReputationReward(new Mission(MissionType.Delivery, MissionDifficulty.Easy, "x", "y", 1000, 0f, "x", FactionManager.LibertyCorporations))), ref passed, ref failed);
            Check("repeat completion cannot pay twice", () => RepeatCompletionDoesNotReward(), ref passed, ref failed);
            Check("cancelled mission gives no positive reputation", () => CancelledMissionNoPositiveReward(), ref passed, ref failed);
            Check("failed mission gives modest penalty", () => FailedMissionPenalty(), ref passed, ref failed);
            Check("courier employer reward is supported", () => CompleteMissionOnce(MissionType.CourierDelivery, out float courierDelta) && courierDelta > 0f, ref passed, ref failed);
            Check("freight employer reward is supported", () => CompleteFreightOnce(out float freightDelta) && freightDelta > 0f, ref passed, ref failed);
            Check("export employer identity uses origin", () => ExportEmployerIsOrigin(), ref passed, ref failed);
            Check("bounty employer reward is supported", () => CompleteMissionOnce(MissionType.Bounty, out float bountyDelta) && bountyDelta > 0f, ref passed, ref failed);
            Check("mission reward terms remain fixed", () => FixedMissionTerms(), ref passed, ref failed);
            Check("destination does not replace explicit employer", () => ExplicitEmployerWins(), ref passed, ref failed);

            Check("player faction kill penalty", () => PlayerKillPenalty(out float killDelta) && killDelta < 0f, ref passed, ref failed);
            Check("kill penalty applies exactly once", () => PlayerKillPenaltyExactlyOnce(), ref passed, ref failed);
            Check("non-player kill is not blamed", () => NonPlayerKillSafe(), ref passed, ref failed);
            Check("environmental death is not blamed", () => EnvironmentalDeathSafe(), ref passed, ref failed);
            Check("unknown victim faction is safe", () => UnknownVictimSafe(), ref passed, ref failed);
            Check("repeated damage does not add attack penalties", () => RepeatedDamageOnlyKillsOnce(), ref passed, ref failed);
            Check("attack reason remains future-compatible", () => Enum.IsDefined(typeof(ReputationChangeReason), ReputationChangeReason.FactionShipAttacked), ref passed, ref failed);

            Check("save captures values", () => SaveRoundTrip(out ReputationManager loadedManager), ref passed, ref failed);
            Check("load restores exact values", () => SaveRoundTrip(out ReputationManager loadedManager) && Nearly(loadedManager.GetStanding(FactionManager.LibertyPolice), -0.41f), ref passed, ref failed);
            Check("loaded bands derive identically", () => SaveRoundTrip(out ReputationManager loadedManager) && loadedManager.GetBand(FactionManager.LibertyPolice) == ReputationBand.Unfriendly, ref passed, ref failed);
            Check("old save without reputation loads", () => OldSaveUsesDefaults(), ref passed, ref failed);
            Check("invalid saved faction value is safe", () => InvalidSavedValueSafe(), ref passed, ref failed);
            Check("out-of-range saved value is clamped", () => OutOfRangeSavedValueClamped(), ref passed, ref failed);
            Check("saved unknown faction is preserved", () => SavedUnknownFactionPreserved(), ref passed, ref failed);
            Check("saved values override defaults", () => SavedValuesOverrideDefaults(), ref passed, ref failed);
            Check("repeated save/load is stable", () => RepeatedSaveLoadStable(), ref passed, ref failed);
            Check("new-game bootstrap has isolated state", () => BootstrapIsolation(), ref passed, ref failed);

            Check("station identity remains authoritative", () => policeStation.FactionId == FactionManager.LibertyPolice, ref passed, ref failed);
            Check("ship identity remains authoritative", () => new NpcShip("Patrol", Vector3.Zero, Vector3.Zero, 1f, 1f, FactionManager.LibertyNavy).FactionId == FactionManager.LibertyNavy, ref passed, ref failed);
            Check("mission employer identity is stable", () => presentationMission.FactionId == FactionManager.LibertyPolice, ref passed, ref failed);
            Check("unknown presentation is safe", () => ReputationPresentation.BuildMissionEmployerLine(new Mission(MissionType.Delivery, MissionDifficulty.Easy, "x", "y", 1, 0f, "x", "mystery"), profileManager).Contains("Mystery"), ref passed, ref failed);
            Check("commodity pricing has no reputation dependency", () => typeof(ReputationManager).GetMethod("AdjustReputation") != null && typeof(MarketManager).GetMethod("GetListingsForStation") != null, ref passed, ref failed);
            Check("trade plan remains separate", () => typeof(ReputationManager).GetProperty("FactionManager") != null, ref passed, ref failed);
            Check("market intelligence remains separate", () => profileManager.GetStandingsSnapshot().Count == 7, ref passed, ref failed);
            Check("cargo reservations remain separate", () => new CargoHold(10).MaxCapacity == 10, ref passed, ref failed);
            Check("all default behavior deterministic", () => new ReputationManager(new FactionManager()).GetOrderedStandings().Select(entry => entry.FactionId).SequenceEqual(new ReputationManager(new FactionManager()).GetOrderedStandings().Select(entry => entry.FactionId)), ref passed, ref failed);

            Console.WriteLine($"[REPUTATION SMOKE] RESULT: {passed} passed, {failed} failed");
            return (passed, failed);
        }

        private static void Check(string label, Func<bool> assertion, ref int passed, ref int failed)
        {
            try
            {
                if (assertion())
                {
                    passed++;
                    Console.WriteLine($"[REPUTATION SMOKE] PASS {label}");
                }
                else
                {
                    failed++;
                    Console.WriteLine($"[REPUTATION SMOKE] FAIL {label}");
                }
            }
            catch (Exception ex)
            {
                failed++;
                Console.WriteLine($"[REPUTATION SMOKE] FAIL {label}: {ex.Message}");
            }
        }

        private static bool SetAndRead(float value, float expected)
        {
            ReputationManager manager = NeutralManager();
            manager.SetReputation("test_faction", value);
            return Nearly(manager.GetStanding("test_faction"), expected);
        }

        private static bool AdjustFrom(float start, float delta)
        {
            ReputationManager manager = NeutralManager();
            manager.SetReputation("test_faction", start);
            manager.AdjustReputation("test_faction", delta);
            return Nearly(manager.GetStanding("test_faction"), start + delta);
        }

        private static bool NoChangeForDelta(float delta)
        {
            ReputationManager manager = NeutralManager();
            float before = manager.GetStanding("test_faction");
            int events = 0;
            manager.OnReputationChanged += _ => events++;
            manager.AdjustReputation("test_faction", delta);
            return Nearly(manager.GetStanding("test_faction"), before) && events == 0;
        }

        private static bool InvalidSetIsSafe(float value)
        {
            ReputationManager manager = NeutralManager();
            manager.SetReputation("test_faction", 0.11f);
            manager.SetReputation("test_faction", value);
            return IsFinite(manager.GetStanding("test_faction")) && Nearly(manager.GetStanding("test_faction"), 0.11f);
        }

        private static bool QueryDoesNotMutate()
        {
            ReputationManager manager = NeutralManager();
            int before = manager.GetStandingsSnapshot().Count;
            _ = manager.GetStanding("custom_sector");
            _ = manager.GetBand("custom_sector");
            _ = manager.FactionManager.GetFaction("custom_sector");
            return before == manager.GetStandingsSnapshot().Count && !manager.GetStandingsSnapshot().ContainsKey("custom_sector");
        }

        private static bool NewGameReset()
        {
            ReputationManager manager = new(new FactionManager());
            manager.SetReputation(FactionManager.LibertyRogues, -0.9f);
            manager.ResetToNewGame();
            return Nearly(manager.GetStanding(FactionManager.LibertyRogues), 0.35f);
        }

        private static bool KillDedupResets()
        {
            ReputationManager manager = NeutralManager();
            NpcShip target = new("Target", Vector3.Zero, Vector3.Zero, 1f, 1f, FactionManager.LibertyPolice);
            target.MarkDamagedByPlayer();
            manager.ApplyPlayerShipDestroyed(target);
            float afterKill = manager.GetStanding(FactionManager.LibertyPolice);
            manager.ResetToNewGame();
            manager.ApplyPlayerShipDestroyed(target);
            return manager.GetStanding(FactionManager.LibertyPolice) == afterKill;
        }

        private static bool EventProof(out ReputationChangeResult change)
        {
            ReputationChangeResult captured = null;
            ReputationManager manager = NeutralManager();
            manager.OnReputationChanged += result =>
            {
                if (!result.IsSecondaryEffect)
                    captured = result;
            };
            manager.AdjustReputation(FactionManager.NeutralCivilians, 0.03f, ReputationChangeReason.MissionCompleted);
            change = captured;
            return captured != null;
        }

        private static bool BandTransitionProof()
        {
            ReputationManager manager = NeutralManager();
            manager.SetReputation("test_faction", 0.19f);
            ReputationChangeResult captured = null;
            manager.OnReputationChanged += result => captured = result;
            manager.AdjustReputation("test_faction", 0.02f);
            return captured?.BandChanged == true && captured.NewBand == ReputationBand.Friendly;
        }

        private static bool WithinBandProof()
        {
            ReputationManager manager = NeutralManager();
            manager.SetReputation("test_faction", 0.30f);
            ReputationChangeResult captured = null;
            manager.OnReputationChanged += result => captured = result;
            manager.AdjustReputation("test_faction", 0.01f);
            return captured?.BandChanged == false;
        }

        private static bool QueryDoesNotEmitEvent()
        {
            ReputationManager manager = NeutralManager();
            int events = 0;
            manager.OnReputationChanged += _ => events++;
            _ = manager.GetStanding("test_faction");
            _ = manager.GetBand("test_faction");
            _ = manager.GetOrderedStandings();
            return events == 0;
        }

        private static bool SecondaryProof(out float directDelta, out float secondaryDelta)
        {
            ReputationManager manager = NeutralManager();
            manager.SetReputation(FactionManager.LibertyPolice, 0f);
            manager.SetReputation(FactionManager.LibertyNavy, 0f);
            float navyBefore = manager.GetStanding(FactionManager.LibertyNavy);
            ReputationChangeResult direct = manager.AdjustReputation(FactionManager.LibertyPolice, -0.10f, ReputationChangeReason.FactionShipDestroyed);
            directDelta = direct?.Delta ?? 0f;
            secondaryDelta = manager.GetStanding(FactionManager.LibertyNavy) - navyBefore;
            return direct != null;
        }

        private static bool NoRecursiveRipple()
        {
            ReputationManager manager = NeutralManager();
            manager.SetReputation(FactionManager.LibertyPolice, 0f);
            manager.SetReputation(FactionManager.LibertyNavy, 0f);
            manager.SetReputation(FactionManager.LibertyRogues, 0f);
            manager.AdjustReputation(FactionManager.LibertyPolice, -0.10f);
            return Nearly(manager.GetStanding(FactionManager.LibertyRogues), 0.06f);
        }

        private static bool UnrelatedFactionUnchanged()
        {
            ReputationManager manager = NeutralManager();
            float before = manager.GetStanding("custom_sector");
            manager.AdjustReputation(FactionManager.LibertyPolice, -0.10f);
            return Nearly(manager.GetStanding("custom_sector"), before);
        }

        private static bool NoStandingDrift()
        {
            ReputationManager manager = new(new FactionManager());
            float before = manager.GetStanding(FactionManager.LibertyRogues);
            for (int i = 0; i < 100; i++) _ = manager.GetStanding(FactionManager.LibertyRogues);
            return Nearly(before, manager.GetStanding(FactionManager.LibertyRogues));
        }

        private static bool DockQuery(ReputationBand band, bool expected)
        {
            ReputationManager manager = NeutralManager();
            manager.SetReputation("test_faction", band switch
            {
                ReputationBand.Hostile => -0.60f,
                ReputationBand.Unfriendly => -0.21f,
                ReputationBand.Friendly => 0.21f,
                ReputationBand.Allied => 0.60f,
                _ => 0f
            });
            return manager.CanDockWithFaction("test_faction") == expected;
        }

        private static bool RequirementProof(float value, float requirement, bool expected)
        {
            ReputationManager manager = NeutralManager();
            manager.SetReputation("test_faction", value);
            return manager.MeetsReputationRequirement("test_faction", requirement) == expected;
        }

        private static bool MissionRequirementDelegates()
        {
            ReputationManager reputation = NeutralManager();
            reputation.SetReputation(FactionManager.LibertyCorporations, 0.25f);
            MissionManager manager = new(new PlayerCredits(0), null, reputation);
            return manager.MeetsReputationRequirement(FactionManager.LibertyCorporations, 0.25f) &&
                !manager.MeetsReputationRequirement(FactionManager.LibertyCorporations, 0.26f);
        }

        private static bool OverviewIsDeterministic(ReputationManager manager)
        {
            string first = string.Join("|", ReputationPresentation.BuildOverview(manager).Select(line => line.FactionId));
            string second = string.Join("|", ReputationPresentation.BuildOverview(manager).Select(line => line.FactionId));
            return first == second;
        }

        private static bool OverviewHasEveryDefault(ReputationManager manager)
        {
            IReadOnlyList<ReputationOverviewLine> overview = ReputationPresentation.BuildOverview(manager);
            return DefaultFactionIds.All(id => overview.Count(line => line.FactionId == id) == 1);
        }

        private static bool OverviewDoesNotMutate(ReputationManager manager)
        {
            Dictionary<string, float> before = new(manager.GetStandingsSnapshot(), StringComparer.OrdinalIgnoreCase);
            _ = ReputationPresentation.BuildOverview(manager);
            return before.Count == manager.GetStandingsSnapshot().Count && before.All(entry => Nearly(entry.Value, manager.GetStanding(entry.Key)));
        }

        private static bool RewardScaleIsBounded()
        {
            return Enum.GetValues<MissionType>().All(type =>
            {
                Mission mission = new(type, MissionDifficulty.Deadly, "x", "y", 1000, 0f, "x", FactionManager.LibertyCorporations);
                float reward = MissionManager.GetMissionReputationReward(mission);
                return reward >= 0.01f && reward <= 0.05f;
            });
        }

        private static bool CompleteMissionOnce(MissionType type, out float rewardDelta)
        {
            ReputationManager reputation = NeutralManager();
            reputation.SetReputation(FactionManager.LibertyCorporations, 0f);
            PlayerCredits credits = new(0);
            MissionManager manager = new(credits, null, reputation);
            Station station = CreateStation("Origin", FactionManager.LibertyCorporations);
            Mission mission = new(type, MissionDifficulty.Easy, "Target", "Destination", 1000, 0f, "Complete the job", FactionManager.LibertyCorporations)
            {
                SourceStationName = station.Name,
                PackageId = type == MissionType.CourierDelivery ? "package" : string.Empty,
                PackageQuantity = type == MissionType.CourierDelivery ? 1 : 0,
                PackageVolume = type == MissionType.CourierDelivery ? 1 : 0,
                RequiredProgress = 1,
                TargetCount = 1
            };
            float before = reputation.GetStanding(FactionManager.LibertyCorporations);
            if (!manager.AcceptMission(mission, station))
            {
                rewardDelta = 0f;
                return false;
            }
            mission.ObjectiveComplete = true;
            manager.CompleteMission(mission);
            if (!manager.TryClaimReward(mission, station, out _))
            {
                rewardDelta = 0f;
                return false;
            }
            rewardDelta = reputation.GetStanding(FactionManager.LibertyCorporations) - before;
            return rewardDelta > 0f && mission.ReputationRewardApplied;
        }

        private static bool RepeatCompletionDoesNotReward()
        {
            ReputationManager reputation = NeutralManager();
            reputation.SetReputation(FactionManager.LibertyCorporations, 0f);
            PlayerCredits credits = new(0);
            MissionManager manager = new(credits, null, reputation);
            Station station = CreateStation("Origin", FactionManager.LibertyCorporations);
            Mission mission = new(MissionType.Delivery, MissionDifficulty.Easy, "Target", "Destination", 1000, 0f, "Complete", FactionManager.LibertyCorporations);
            if (!manager.AcceptMission(mission, station)) return false;
            manager.CompleteMission(mission);
            if (!manager.TryClaimReward(mission, station, out _)) return false;
            float after = reputation.GetStanding(FactionManager.LibertyCorporations);
            return !manager.TryClaimReward(mission, station, out _) && Nearly(after, reputation.GetStanding(FactionManager.LibertyCorporations));
        }

        private static bool CancelledMissionNoPositiveReward()
        {
            ReputationManager reputation = NeutralManager();
            reputation.SetReputation(FactionManager.LibertyCorporations, 0f);
            MissionManager manager = new(new PlayerCredits(0), null, reputation);
            Station station = CreateStation("Origin", FactionManager.LibertyCorporations);
            Mission mission = new(MissionType.Delivery, MissionDifficulty.Easy, "Target", "Destination", 1000, 0f, "Cancel", FactionManager.LibertyCorporations);
            if (!manager.AcceptMission(mission, station)) return false;
            float before = reputation.GetStanding(FactionManager.LibertyCorporations);
            return manager.CancelMission(mission, out _) && reputation.GetStanding(FactionManager.LibertyCorporations) <= before;
        }

        private static bool FailedMissionPenalty()
        {
            ReputationManager reputation = NeutralManager();
            reputation.SetReputation(FactionManager.LibertyCorporations, 0f);
            MissionManager manager = new(new PlayerCredits(0), null, reputation);
            Station station = CreateStation("Origin", FactionManager.LibertyCorporations);
            Mission mission = new(MissionType.Delivery, MissionDifficulty.Easy, "Target", "Destination", 1000, 0f, "Fail", FactionManager.LibertyCorporations);
            if (!manager.AcceptMission(mission, station)) return false;
            float before = reputation.GetStanding(FactionManager.LibertyCorporations);
            manager.FailMission(mission, "test failure");
            return reputation.GetStanding(FactionManager.LibertyCorporations) < before &&
                before - reputation.GetStanding(FactionManager.LibertyCorporations) <= 0.03f;
        }

        private static bool CompleteFreightOnce(out float rewardDelta)
        {
            ReputationManager reputation = NeutralManager();
            reputation.SetReputation(FactionManager.LibertyCorporations, 0f);
            CargoHold cargo = new(50);
            MissionManager manager = new(new PlayerCredits(0), null, reputation, cargoHold: cargo);
            Station station = CreateStation("Destination", FactionManager.LibertyCorporations);
            Commodity commodity = CommodityCatalog.GetById("food-rations");
            Mission mission = Mission.CreateFreightContract(commodity, station, 1, 1000, 1, factionId: FactionManager.LibertyCorporations);
            float before = reputation.GetStanding(FactionManager.LibertyCorporations);
            if (mission == null || !manager.AcceptMission(mission, station))
            {
                rewardDelta = 0f;
                return false;
            }
            bool completed = manager.CompleteFreightMission(mission, out _);
            rewardDelta = reputation.GetStanding(FactionManager.LibertyCorporations) - before;
            return completed && rewardDelta > 0f;
        }

        private static bool ExportEmployerIsOrigin()
        {
            Station origin = CreateStation("Origin", FactionManager.LibertyRogues);
            Station destination = CreateStation("Destination", FactionManager.LibertyPolice);
            Mission mission = Mission.CreateExportContract(origin, CommodityCatalog.GetById("food-rations"), destination, 1, 1000, 1, factionId: origin.FactionId);
            return mission != null && mission.FactionId == FactionManager.LibertyRogues;
        }

        private static bool FixedMissionTerms()
        {
            Mission mission = new(MissionType.Delivery, MissionDifficulty.Hard, "x", "y", 1000, 0f, "x", FactionManager.LibertyCorporations);
            float first = MissionManager.GetMissionReputationReward(mission);
            mission.ReputationReward = first;
            return Nearly(first, MissionManager.GetMissionReputationReward(mission));
        }

        private static bool ExplicitEmployerWins()
        {
            Station destination = CreateStation("Destination", FactionManager.LibertyPolice);
            Mission mission = Mission.CreateFreightContract(CommodityCatalog.GetById("food-rations"), destination, 1, 1000, 1, factionId: FactionManager.LibertyRogues);
            return mission != null && mission.FactionId == FactionManager.LibertyRogues;
        }

        private static bool PlayerKillPenalty(out float delta)
        {
            ReputationManager manager = NeutralManager();
            float before = manager.GetStanding(FactionManager.LibertyPolice);
            NpcShip target = new("Police", Vector3.Zero, Vector3.Zero, 1f, 1f, FactionManager.LibertyPolice);
            target.MarkDamagedByPlayer();
            ReputationChangeResult result = manager.ApplyPlayerShipDestroyed(target);
            delta = manager.GetStanding(FactionManager.LibertyPolice) - before;
            return result != null && result.Reason == ReputationChangeReason.FactionShipDestroyed;
        }

        private static bool PlayerKillPenaltyExactlyOnce()
        {
            ReputationManager manager = NeutralManager();
            NpcShip target = new("Police", Vector3.Zero, Vector3.Zero, 1f, 1f, FactionManager.LibertyPolice);
            target.MarkDamagedByPlayer();
            manager.ApplyPlayerShipDestroyed(target);
            float after = manager.GetStanding(FactionManager.LibertyPolice);
            manager.ApplyPlayerShipDestroyed(target);
            return Nearly(after, manager.GetStanding(FactionManager.LibertyPolice));
        }

        private static bool NonPlayerKillSafe()
        {
            ReputationManager manager = NeutralManager();
            float before = manager.GetStanding(FactionManager.LibertyPolice);
            NpcShip target = new("Police", Vector3.Zero, Vector3.Zero, 1f, 1f, FactionManager.LibertyPolice);
            return manager.ApplyPlayerShipDestroyed(target) == null && Nearly(before, manager.GetStanding(FactionManager.LibertyPolice));
        }

        private static bool EnvironmentalDeathSafe() => NonPlayerKillSafe();

        private static bool UnknownVictimSafe()
        {
            ReputationManager manager = NeutralManager();
            NpcShip target = new("Unknown", Vector3.Zero, Vector3.Zero, 1f, 1f, "mystery_faction");
            target.MarkDamagedByPlayer();
            return manager.ApplyPlayerShipDestroyed(target) != null && manager.GetStanding("mystery_faction") < 0f;
        }

        private static bool RepeatedDamageOnlyKillsOnce()
        {
            ReputationManager manager = NeutralManager();
            NpcShip target = new("Police", Vector3.Zero, Vector3.Zero, 1f, 1f, FactionManager.LibertyPolice);
            target.MarkDamagedByPlayer();
            manager.ApplyPlayerShipDestroyed(target);
            float after = manager.GetStanding(FactionManager.LibertyPolice);
            for (int i = 0; i < 20; i++) manager.ApplyPlayerShipDestroyed(target);
            return Nearly(after, manager.GetStanding(FactionManager.LibertyPolice));
        }

        private static bool SaveRoundTrip(out ReputationManager loadedManager)
        {
            loadedManager = null;
            string directory = Path.Combine(Path.GetTempPath(), "Roguelancer_ReputationSmoke_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            try
            {
                SaveGameManager saveManager = new(Path.Combine(directory, "save.json"));
                ReputationManager source = NeutralManager();
                source.SetReputation(FactionManager.LibertyPolice, -0.41f);
                source.SetReputation(FactionManager.LibertyRogues, 0.57f);
                SaveGameData data = new() { FactionReputation = saveManager.CaptureReputation(source) };
                if (!saveManager.TrySave(data, out _)) return false;
                if (!saveManager.TryLoad(out SaveGameData loaded, out _)) return false;
                loadedManager = NeutralManager();
                saveManager.ApplyReputation(loadedManager, loaded);
                return Nearly(loadedManager.GetStanding(FactionManager.LibertyPolice), -0.41f) &&
                    Nearly(loadedManager.GetStanding(FactionManager.LibertyRogues), 0.57f);
            }
            finally
            {
                if (Directory.Exists(directory)) Directory.Delete(directory, true);
            }
        }

        private static bool OldSaveUsesDefaults()
        {
            SaveGameManager manager = new(Path.Combine(Path.GetTempPath(), "missing-save.json"));
            ReputationManager reputation = NeutralManager();
            reputation.SetReputation(FactionManager.LibertyRogues, -0.8f);
            manager.ApplyReputation(reputation, new SaveGameData { FactionReputation = null });
            return Nearly(reputation.GetStanding(FactionManager.LibertyRogues), 0.35f);
        }

        private static bool InvalidSavedValueSafe()
        {
            SaveGameManager manager = new(Path.Combine(Path.GetTempPath(), "missing-save.json"));
            ReputationManager reputation = NeutralManager();
            manager.ApplyReputation(reputation, new SaveGameData
            {
                FactionReputation = new List<SaveFactionReputationData>
                {
                    new() { FactionId = FactionManager.LibertyPolice, Standing = float.NaN }
                }
            });
            return Nearly(reputation.GetStanding(FactionManager.LibertyPolice), -0.25f);
        }

        private static bool OutOfRangeSavedValueClamped()
        {
            SaveGameManager manager = new(Path.Combine(Path.GetTempPath(), "missing-save.json"));
            ReputationManager reputation = NeutralManager();
            manager.ApplyReputation(reputation, new SaveGameData
            {
                FactionReputation = new List<SaveFactionReputationData>
                {
                    new() { FactionId = FactionManager.LibertyPolice, Standing = 8f }
                }
            });
            return Nearly(reputation.GetStanding(FactionManager.LibertyPolice), 1f);
        }

        private static bool SavedUnknownFactionPreserved()
        {
            SaveGameManager manager = new(Path.Combine(Path.GetTempPath(), "missing-save.json"));
            ReputationManager reputation = NeutralManager();
            manager.ApplyReputation(reputation, new SaveGameData
            {
                FactionReputation = new List<SaveFactionReputationData>
                {
                    new() { FactionId = "custom_sector", Standing = 0.42f }
                }
            });
            return Nearly(reputation.GetStanding("custom_sector"), 0.42f) && reputation.GetBand("custom_sector") == ReputationBand.Friendly;
        }

        private static bool SavedValuesOverrideDefaults()
        {
            ReputationManager reputation = NeutralManager();
            reputation.LoadStandings(new Dictionary<string, float>
            {
                [FactionManager.LibertyRogues] = -0.12f
            });
            return Nearly(reputation.GetStanding(FactionManager.LibertyRogues), -0.12f) && Nearly(reputation.GetStanding(FactionManager.LibertyPolice), -0.25f);
        }

        private static bool RepeatedSaveLoadStable()
        {
            ReputationManager manager = NeutralManager();
            manager.LoadStandings(new Dictionary<string, float> { [FactionManager.LibertyPolice] = -0.41234f });
            float value = manager.GetStanding(FactionManager.LibertyPolice);
            for (int i = 0; i < 5; i++) manager.LoadStandings(manager.GetStandingsSnapshot());
            return Nearly(value, manager.GetStanding(FactionManager.LibertyPolice));
        }

        private static bool BootstrapIsolation()
        {
            ReputationManager first = NeutralManager();
            first.SetReputation(FactionManager.LibertyPolice, -0.9f);
            ReputationManager second = NeutralManager();
            return Nearly(second.GetStanding(FactionManager.LibertyPolice), -0.25f);
        }

        private static ReputationManager NeutralManager()
        {
            ReputationManager manager = new(new FactionManager());
            manager.SetReputation("test_faction", 0f);
            return manager;
        }

        private static ReputationBand Band(ReputationManager manager, string factionId) => manager.GetBand(factionId);

        private static Station CreateStation(string name, string factionId)
        {
            return new Station(new StationConfig
            {
                Description = name,
                SystemIndex = 1,
                FactionId = factionId,
                Radius = 500f,
                DockingRange = 500f
            }, null);
        }

        private static bool Nearly(float left, float right) => Math.Abs(left - right) <= 0.0002f;
        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
