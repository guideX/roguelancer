using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Roguelancer.Configuration;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Roguelancer
{
    /// <summary>
    /// Focused Phase 50 coverage for the bounded, mission-owned convoy escort
    /// contract. The lifecycle checks use the real mission world, NPC state,
    /// faction targeting, and TradelaneManager rather than a scripted fleet
    /// substitute.
    /// </summary>
    internal sealed class ConvoyEscortMissionSmokeTest
    {
        private readonly GraphicsDevice _graphicsDevice;

        public ConvoyEscortMissionSmokeTest(GraphicsDevice graphicsDevice = null)
        {
            _graphicsDevice = graphicsDevice;
        }

        public (int Passed, int Failed) Run()
        {
            int passed = 0;
            int failed = 0;
            Check("board exposes deterministic convoy escort offers", BoardExposesOffers, ref passed, ref failed);
            Check("offer identity is stable", OfferIdentityIsStable, ref passed, ref failed);
            Check("offer route is stable and usable", OfferRouteIsStable, ref passed, ref failed);
            Check("offer reward is deterministic", OfferRewardIsDeterministic, ref passed, ref failed);
            Check("easy convoy size and reward are bounded", EasyTermsAreBounded, ref passed, ref failed);
            Check("medium convoy size and reward are bounded", MediumTermsAreBounded, ref passed, ref failed);
            Check("hard convoy size and reward are bounded", HardTermsAreBounded, ref passed, ref failed);
            Check("employer is a lawful commercial faction", EmployerIsLawful, ref passed, ref failed);
            Check("hostile faction is Liberty Rogue", HostileFactionIsRogue, ref passed, ref failed);
            Check("offer presents convoy details", OfferPresentationIsSpecific, ref passed, ref failed);
            Check("convoy uses a commercial faction", ConvoyUsesCommercialFaction, ref passed, ref failed);
            Check("convoy uses an ordinary NPC loadout", ConvoyUsesOrdinaryLoadout, ref passed, ref failed);
            Check("acceptance creates an active convoy", AcceptanceCreatesConvoy, ref passed, ref failed);
            Check("convoy waits at rendezvous", ConvoyWaitsBeforeRendezvous, ref passed, ref failed);
            Check("rendezvous guidance is clear", RendezvousGuidanceIsClear, ref passed, ref failed);
            Check("player arrival activates escort", PlayerArrivalActivatesEscort, ref passed, ref failed);
            Check("activated convoy uses TraderRoute behavior", ActivatedConvoyUsesTraderRoute, ref passed, ref failed);
            Check("convoy spacing avoids overlap", ConvoySpacingAvoidsOverlap, ref passed, ref failed);
            Check("convoy enters the existing trade lane", ConvoyEntersTradeLane, ref passed, ref failed);
            Check("convoy advances real route progress", ConvoyAdvancesRoute, ref passed, ref failed);
            Check("convoy exits the existing trade lane", ConvoyExitsTradeLane, ref passed, ref failed);
            Check("Rogue encounter activates at deterministic point", EncounterActivatesOnce, ref passed, ref failed);
            Check("Rogue force respects difficulty bounds", RogueForceIsBounded, ref passed, ref failed);
            Check("Rogue force uses Liberty Rogue faction", RogueForceUsesRogueFaction, ref passed, ref failed);
            Check("Rogue force uses canonical loadouts", RogueForceUsesCanonicalLoadouts, ref passed, ref failed);
            Check("Rogues prioritize the convoy objective", RoguesPrioritizeConvoy, ref passed, ref failed);
            Check("Rogues retain normal retaliation state", RoguesRetainNormalRetaliation, ref passed, ref failed);
            Check("convoy receives ordinary shield and hull damage", ConvoyReceivesOrdinaryDamage, ref passed, ref failed);
            Check("one convoy loss does not fail the mission", PartialLossRemainsViable, ref passed, ref failed);
            Check("all convoy losses fail immediately", TotalLossFailsMission, ref passed, ref failed);
            Check("player final blow is not required", PlayerFinalBlowIsNotRequired, ref passed, ref failed);
            Check("unrelated Police can acquire a Rogue target", PoliceCanAssistNaturally, ref passed, ref failed);
            Check("convoy resumes after the encounter", ConvoyResumesAfterEncounter, ref passed, ref failed);
            Check("destination arrival registers survivors", DestinationArrivalRegistersSurvivors, ref passed, ref failed);
            Check("success pays exactly once", SuccessPaysExactlyOnce, ref passed, ref failed);
            Check("success grants reputation exactly once", SuccessRewardsReputationExactlyOnce, ref passed, ref failed);
            Check("failure pays nothing", FailurePaysNothing, ref passed, ref failed);
            Check("proximity warning starts after the threshold", ProximityWarningStarts, ref passed, ref failed);
            Check("proximity grace resets on return", ProximityGraceResets, ref passed, ref failed);
            Check("continuous abandonment fails", ContinuousAbandonmentFails, ref passed, ref failed);
            Check("save preserves route and stage", SavePreservesRouteAndStage, ref passed, ref failed);
            Check("save preserves convoy losses", SavePreservesConvoyLosses, ref passed, ref failed);
            Check("load does not duplicate convoy ships", LoadDoesNotDuplicateConvoy, ref passed, ref failed);
            Check("load does not duplicate attackers", LoadDoesNotDuplicateAttackers, ref passed, ref failed);
            Check("completed encounter does not respawn attackers", CompletedEncounterDoesNotRespawn, ref passed, ref failed);
            Check("reset clears convoy-owned state", ResetClearsConvoyState, ref passed, ref failed);
            Check("failure cleanup removes attackers", FailureCleanupRemovesAttackers, ref passed, ref failed);
            Check("ambient traffic remains present", AmbientTrafficRemainsActive, ref passed, ref failed);
            Check("reverse lane remains available", ReverseLaneRemainsAvailable, ref passed, ref failed);
            Check("convoy identity is separate from disruption missions", IdentityDoesNotCrossContaminate, ref passed, ref failed);
            Check("Rogue destruction keeps ordinary attribution", RogueDestructionKeepsAttribution, ref passed, ref failed);
            Check("HUD exposes staged escort guidance", HudExposesEscortGuidance, ref passed, ref failed);
            Check("manual lifecycle reaches destination", ManualLifecycleReachesDestination, ref passed, ref failed);

            Console.WriteLine($"[CONVOY ESCORT MISSION SMOKE] RESULT: {passed} passed, {failed} failed");
            return (passed, failed);
        }

        private static void Check(
            string label,
            Func<(bool Success, string FailureReason)> test,
            ref int passed,
            ref int failed)
        {
            try
            {
                (bool success, string reason) = test();
                if (success)
                {
                    passed++;
                    Console.WriteLine($"[CONVOY ESCORT MISSION SMOKE] PASS {label}");
                }
                else
                {
                    failed++;
                    Console.WriteLine($"[CONVOY ESCORT MISSION SMOKE] FAIL {label}: {reason}");
                }
            }
            catch (Exception ex)
            {
                failed++;
                Console.WriteLine($"[CONVOY ESCORT MISSION SMOKE] FAIL {label}: {ex.Message}");
            }
        }

        private (bool Success, string FailureReason) BoardExposesOffers()
        {
            Context context = CreateContext();
            List<Mission> offers = context.Manager.GenerateConvoyEscortMissions(context.Origin);
            return Result(offers.Count == 3 && offers.All(mission => mission.Type == MissionType.ConvoyEscort),
                "corporate board did not expose three convoy escort offers");
        }

        private (bool Success, string FailureReason) OfferIdentityIsStable()
        {
            Context first = CreateContext();
            Context second = CreateContext();
            Mission a = Offer(first, MissionDifficulty.Medium);
            Mission b = Offer(second, MissionDifficulty.Medium);
            return Result(a.ConvoyRouteId == b.ConvoyRouteId &&
                a.ConvoyRouteLaneId == b.ConvoyRouteLaneId &&
                a.ConvoyRouteSegmentId == b.ConvoyRouteSegmentId &&
                a.ConvoyRouteDirection == b.ConvoyRouteDirection &&
                a.DestinationStationId == b.DestinationStationId,
                "deterministic board changed convoy route identity");
        }

        private (bool Success, string FailureReason) OfferRouteIsStable()
        {
            Mission mission = Offer(CreateContext(), MissionDifficulty.Easy);
            return Result(!string.IsNullOrWhiteSpace(mission.ConvoyRouteLaneId) &&
                mission.ConvoyRouteRingIndex == -1 &&
                mission.ConvoyEncounterRingIndex >= 1 &&
                mission.ConvoyEncounterRingIndex < 5 &&
                mission.ConvoyRendezvousPosition.HasValue &&
                mission.ConvoyEncounterPosition.HasValue &&
                mission.ConvoyDestinationPosition.HasValue,
                "offer did not carry complete stable route metadata");
        }

        private (bool Success, string FailureReason) OfferRewardIsDeterministic()
        {
            Mission a = Offer(CreateContext(), MissionDifficulty.Hard);
            Mission b = Offer(CreateContext(), MissionDifficulty.Hard);
            return Result(a.Reward == b.Reward && a.Reward >= 11_000 && a.Reward <= 15_000,
                "hard reward was not deterministic or within the contract band");
        }

        private (bool Success, string FailureReason) EasyTermsAreBounded()
        {
            Mission mission = Offer(CreateContext(), MissionDifficulty.Easy);
            return Result(mission.ConvoyShipCount == 2 && mission.ConvoyAttackForceSize == 2 && mission.Reward == 5_000,
                "easy terms did not match the bounded policy");
        }

        private (bool Success, string FailureReason) MediumTermsAreBounded()
        {
            Mission mission = Offer(CreateContext(), MissionDifficulty.Medium);
            return Result(mission.ConvoyShipCount == 3 && mission.ConvoyAttackForceSize == 3 && mission.Reward == 8_500,
                "medium terms did not match the bounded policy");
        }

        private (bool Success, string FailureReason) HardTermsAreBounded()
        {
            Mission mission = Offer(CreateContext(), MissionDifficulty.Hard);
            return Result(mission.ConvoyShipCount == 4 && mission.ConvoyAttackForceSize == 5 && mission.Reward == 13_000,
                "hard terms did not match the bounded policy");
        }

        private (bool Success, string FailureReason) EmployerIsLawful()
        {
            Mission mission = Offer(CreateContext());
            return Result(mission.FactionId == FactionManager.LibertyCorporations ||
                mission.FactionId == FactionManager.LibertyPolice,
                "escort employer was not lawful");
        }

        private (bool Success, string FailureReason) HostileFactionIsRogue()
        {
            Mission mission = Offer(CreateContext());
            return Result(mission.ConvoyHostileFactionId == FactionManager.LibertyRogues,
                "escort hostile faction was not Liberty Rogue");
        }

        private (bool Success, string FailureReason) OfferPresentationIsSpecific()
        {
            Mission mission = Offer(CreateContext());
            return Result(mission.GetTypeLabel() == "CONVOY ESCORT" &&
                mission.Description.Contains("Rogue", StringComparison.OrdinalIgnoreCase) &&
                mission.Description.Contains(mission.Destination, StringComparison.OrdinalIgnoreCase) &&
                mission.GetObjectiveText().Contains(mission.ConvoyShipCount.ToString(), StringComparison.OrdinalIgnoreCase),
                "board text omitted convoy, destination, or Rogue threat details");
        }

        private (bool Success, string FailureReason) ConvoyUsesCommercialFaction()
        {
            Context context = AcceptedContext();
            return Result(context.World.GetConvoyShips(context.Mission).Count == context.Mission.ConvoyShipCount &&
                context.World.GetConvoyShips(context.Mission).All(ship => ship.FactionId == FactionManager.LibertyCorporations),
                "convoy ships did not use the commercial faction");
        }

        private (bool Success, string FailureReason) ConvoyUsesOrdinaryLoadout()
        {
            Context context = AcceptedContext();
            IReadOnlyList<NpcShip> ships = context.World.GetConvoyShips(context.Mission);
            return Result(ships.Count > 0 && ships.All(ship => ship.Loadout != null &&
                ship.TrafficBehavior == TrafficZoneBehaviorType.TraderRoute &&
                ship.Hull != null && ship.Shields != null && ship.WeaponEnergy != null),
                "convoy ships did not receive ordinary NPC runtime state");
        }

        private (bool Success, string FailureReason) AcceptanceCreatesConvoy()
        {
            Context context = AcceptedContext();
            return Result(context.Mission.ConvoyStage == ConvoyEscortStage.Rendezvous &&
                context.Npcs.Count == context.Mission.ConvoyShipCount,
                "acceptance did not create the bounded convoy at rendezvous");
        }

        private (bool Success, string FailureReason) ConvoyWaitsBeforeRendezvous()
        {
            Context context = AcceptedContext();
            Dictionary<NpcShip, Vector3> before = context.World.GetConvoyShips(context.Mission)
                .ToDictionary(ship => ship, ship => ship.Position);
            Tick(context, 3f, followConvoy: false);
            bool held = context.Mission.ConvoyStage == ConvoyEscortStage.Rendezvous &&
                context.World.GetConvoyShips(context.Mission).All(ship =>
                    Vector3.Distance(ship.Position, before[ship]) < 1f && ship.IsMissionHoldPosition);
            return Result(held, "convoy moved or left its hold before rendezvous");
        }

        private (bool Success, string FailureReason) RendezvousGuidanceIsClear()
        {
            Mission mission = AcceptedContext().Mission;
            return Result(mission.GetHudProgressLine().Contains("RENDEZVOUS", StringComparison.OrdinalIgnoreCase) &&
                mission.GetHudProgressLine().Contains("PROCEED", StringComparison.OrdinalIgnoreCase),
                "rendezvous HUD guidance was unclear");
        }

        private (bool Success, string FailureReason) PlayerArrivalActivatesEscort()
        {
            Context context = AcceptedContext();
            context.Player.Position = context.Mission.ConvoyRendezvousPosition.Value;
            context.World.Update(0.1f, null, 1);
            return Result(context.Mission.ConvoyStage == ConvoyEscortStage.Escorting &&
                context.Mission.ConvoyRouteStarted,
                "rendezvous did not activate escort travel");
        }

        private (bool Success, string FailureReason) ActivatedConvoyUsesTraderRoute()
        {
            Context context = ActivatedContext();
            return Result(context.World.GetConvoyShips(context.Mission).All(ship =>
                ship.TrafficBehavior == TrafficZoneBehaviorType.TraderRoute && ship.TrafficRouteStart.HasValue && ship.TrafficRouteEnd.HasValue),
                "activated convoy did not use normal route-following state");
        }

        private (bool Success, string FailureReason) ConvoySpacingAvoidsOverlap()
        {
            Context context = AcceptedContext();
            IReadOnlyList<NpcShip> ships = context.World.GetConvoyShips(context.Mission);
            float minimumDistance = float.MaxValue;
            for (int i = 0; i < ships.Count; i++)
            {
                for (int j = i + 1; j < ships.Count; j++)
                    minimumDistance = Math.Min(minimumDistance, Vector3.Distance(ships[i].Position, ships[j].Position));
            }

            return Result(minimumDistance >= 300f, "convoy formation offsets overlapped");
        }

        private (bool Success, string FailureReason) ConvoyEntersTradeLane()
        {
            Context context = ActivatedContext();
            bool entered = AdvanceUntil(context,
                () => context.World.GetConvoyShips(context.Mission).Any(ship => ship.IsTradeLaneTransit),
                40,
                0.25f);
            return Result(entered, "convoy did not enter the shared trade lane");
        }

        private (bool Success, string FailureReason) ConvoyAdvancesRoute()
        {
            Context context = ActivatedContext();
            bool advanced = AdvanceUntil(context, () => context.Mission.ConvoyRouteRingIndex > 0, 80, 0.25f);
            return Result(advanced && context.Mission.ConvoyRouteRingIndex >= 1,
                "convoy route progress did not advance through trade-lane rings");
        }

        private (bool Success, string FailureReason) ConvoyExitsTradeLane()
        {
            Context context = EncounterContext();
            DestroyAttackers(context, NpcDestructionSource.Npc);
            bool arrivedOrExited = AdvanceUntil(context,
                () => context.Mission.ConvoyArrivedCount > 0 ||
                    context.World.GetConvoyShips(context.Mission).Any(ship => !ship.IsTradeLaneTransit &&
                        ship.TrafficRouteEnd.HasValue),
                240,
                0.25f);
            return Result(arrivedOrExited, "surviving convoy never exited the trade lane");
        }

        private (bool Success, string FailureReason) EncounterActivatesOnce()
        {
            Context context = EncounterContext();
            int count = context.World.GetConvoyAttackers(context.Mission).Count;
            string[] names = context.World.GetConvoyAttackers(context.Mission).Select(attacker => attacker.Name).OrderBy(name => name).ToArray();
            context.World.Update(0.1f, null, 1);
            string[] namesAfter = context.World.GetConvoyAttackers(context.Mission).Select(attacker => attacker.Name).OrderBy(name => name).ToArray();
            return Result(context.Mission.ConvoyEncounterActivated && count > 0 && names.SequenceEqual(namesAfter),
                "interception was not activated exactly once");
        }

        private (bool Success, string FailureReason) RogueForceIsBounded()
        {
            Context context = EncounterContext();
            return Result(context.World.GetConvoyAttackers(context.Mission).Count == context.Mission.ConvoyAttackForceSize &&
                context.World.GetConvoyAttackers(context.Mission).Count is >= 2 and <= 6,
                "Rogue force exceeded the bounded contract range");
        }

        private (bool Success, string FailureReason) RogueForceUsesRogueFaction()
        {
            Context context = EncounterContext();
            return Result(context.World.GetConvoyAttackers(context.Mission).All(attacker =>
                attacker.FactionId == FactionManager.LibertyRogues && attacker.TrafficBehavior == TrafficZoneBehaviorType.PirateAmbush),
                "interceptors were not ordinary Liberty Rogue ambushers");
        }

        private (bool Success, string FailureReason) RogueForceUsesCanonicalLoadouts()
        {
            Context context = EncounterContext();
            return Result(context.World.GetConvoyAttackers(context.Mission).All(attacker =>
                attacker.Loadout != null && attacker.Loadout.GetMountedGuns().Any() &&
                attacker.Loadout.GetMountedGuns().All(NpcEquipmentLoadoutFactory.IsValidNpcWeapon)),
                "interceptors did not use canonical Rogue weapons");
        }

        private (bool Success, string FailureReason) RoguesPrioritizeConvoy()
        {
            Context context = EncounterContext();
            NpcShip attacker = context.World.GetConvoyAttackers(context.Mission).FirstOrDefault();
            NpcShip expected = context.World.GetConvoyCombatTarget(attacker);
            context.Traffic.Update(Frame(0.1f), context.Player, context.Reputation);
            return Result(expected != null && attacker.FactionCombatTarget == expected,
                "mission Rogue did not acquire a convoy target through TrafficManager");
        }

        private (bool Success, string FailureReason) RoguesRetainNormalRetaliation()
        {
            Context context = EncounterContext();
            NpcShip attacker = context.World.GetConvoyAttackers(context.Mission).First();
            attacker.SetPlayerTarget(context.Player.Position, NpcPlayerTargetReason.PlayerInitiatedAggression);
            attacker.Update(Frame(0.1f), null, context.Player, context.Reputation);
            return Result(attacker.HasPlayerTarget && attacker.EncounterState == TrafficEncounterState.AttackingPlayer,
                "Rogue retaliation state was bypassed by mission ownership");
        }

        private (bool Success, string FailureReason) ConvoyReceivesOrdinaryDamage()
        {
            Context context = EncounterContext();
            NpcShip convoy = context.World.GetConvoyShips(context.Mission).First();
            float shieldBefore = convoy.Shields.CurrentShields;
            float hullBefore = convoy.Hull.CurrentHull;
            convoy.ApplyCombatDamage(60f, NpcDestructionSource.Npc);
            return Result(convoy.Shields.CurrentShields < shieldBefore || convoy.Hull.CurrentHull < hullBefore,
                "ordinary combat damage did not affect convoy shield or hull state");
        }

        private (bool Success, string FailureReason) PartialLossRemainsViable()
        {
            Context context = ActivatedContext();
            NpcShip lost = context.World.GetConvoyShips(context.Mission).First();
            lost.ApplyDamage(100_000f, NpcDestructionSource.Npc);
            return Result(context.Manager.ActiveMission == context.Mission &&
                context.Mission.ConvoySurvivors >= context.Mission.ConvoyRequiredSurvivors &&
                context.Mission.ConvoyDestroyedCount == 1,
                "one convoy loss incorrectly failed the escort");
        }

        private (bool Success, string FailureReason) TotalLossFailsMission()
        {
            Context context = ActivatedContext();
            foreach (NpcShip ship in context.World.GetConvoyShips(context.Mission).ToList())
                ship.ApplyDamage(100_000f, NpcDestructionSource.Npc);
            return Result(context.Mission.Status == MissionStatus.Failed && context.Manager.ActiveMission == null,
                "loss of all convoy ships did not fail the mission");
        }

        private (bool Success, string FailureReason) PlayerFinalBlowIsNotRequired()
        {
            Context context = EncounterContext();
            DestroyAttackers(context, NpcDestructionSource.Npc);
            return Result(context.Mission.ConvoyEncounterResolved && context.Mission.Status == MissionStatus.InProgress,
                "mission required player-attributed final blows to resolve the encounter");
        }

        private (bool Success, string FailureReason) PoliceCanAssistNaturally()
        {
            Context context = EncounterContext();
            NpcShip police = new("Phase 50 Police Assist", context.Mission.ConvoyEncounterPosition.Value + new Vector3(500f, 0f, 500f),
                context.Mission.ConvoyEncounterPosition.Value, 500f, 0f, FactionManager.LibertyPolice);
            police.ConfigureTrafficBehavior(TrafficZoneBehaviorType.LawfulPatrol, "phase50-assist", police.Position, 500f, 180f, 12_000f);
            context.Npcs.Add(police);
            context.SpaceObjects.Add(police);
            context.Traffic.RegisterMissionNpc(police);
            context.Traffic.Update(Frame(0.1f), context.Player, context.Reputation);
            return Result(police.FactionCombatTarget != null &&
                context.World.GetConvoyAttackers(context.Mission).Contains(police.FactionCombatTarget),
                "unrelated lawful Police did not retain natural faction assistance");
        }

        private (bool Success, string FailureReason) ConvoyResumesAfterEncounter()
        {
            Context context = EncounterContext();
            DestroyAttackers(context, NpcDestructionSource.Npc);
            bool resumed = AdvanceUntil(context,
                () => context.Mission.ConvoyEncounterResolved && context.Mission.ConvoyArrivedCount > 0,
                240,
                0.25f);
            return Result(resumed, "convoy did not resume travel after the Rogue wave ended");
        }

        private (bool Success, string FailureReason) DestinationArrivalRegistersSurvivors()
        {
            Context context = CompletedContext();
            return Result(context.Mission.ConvoyArrivedCount == context.Mission.ConvoyShipCount - context.Mission.ConvoyDestroyedCount &&
                context.Mission.ConvoyArrivedCount >= context.Mission.ConvoyRequiredSurvivors,
                "destination did not register every surviving convoy ship");
        }

        private (bool Success, string FailureReason) SuccessPaysExactlyOnce()
        {
            Context context = CompletedContext();
            int before = context.Credits.Credits;
            bool first = context.Manager.TryClaimReward(context.Mission, context.Origin, out _);
            bool second = context.Manager.TryClaimReward(context.Mission, context.Origin, out _);
            return Result(first && !second && context.Credits.Credits - before == context.Mission.Reward,
                "escort reward was not an exactly-once transaction");
        }

        private (bool Success, string FailureReason) SuccessRewardsReputationExactlyOnce()
        {
            Context context = CompletedContext();
            float before = context.Reputation.GetStanding(context.Mission.FactionId);
            context.Manager.TryClaimReward(context.Mission, context.Origin, out _);
            float afterFirst = context.Reputation.GetStanding(context.Mission.FactionId);
            context.Manager.TryClaimReward(context.Mission, context.Origin, out _);
            return Result(afterFirst > before && context.Reputation.GetStanding(context.Mission.FactionId) == afterFirst,
                "escort reputation reward was not applied exactly once");
        }

        private (bool Success, string FailureReason) FailurePaysNothing()
        {
            Context context = ActivatedContext();
            int before = context.Credits.Credits;
            foreach (NpcShip ship in context.World.GetConvoyShips(context.Mission).ToList())
                ship.ApplyDamage(100_000f, NpcDestructionSource.Npc);
            bool claimed = context.Manager.TryClaimReward(context.Mission, context.Origin, out _);
            return Result(!claimed && before == context.Credits.Credits && context.Mission.Status == MissionStatus.Failed,
                "failed escort paid or remained claimable");
        }

        private (bool Success, string FailureReason) ProximityWarningStarts()
        {
            Context context = ActivatedContext();
            context.Player.Position = new Vector3(20_000f, 5_000f, 20_000f);
            context.World.Update(1f, null, 1);
            return Result(context.Mission.ConvoyAbandonmentProgressSeconds == 1f,
                "abandonment grace did not start after leaving the convoy range");
        }

        private (bool Success, string FailureReason) ProximityGraceResets()
        {
            Context context = ActivatedContext();
            context.Player.Position = new Vector3(20_000f, 5_000f, 20_000f);
            context.World.Update(2f, null, 1);
            NpcShip leader = context.World.GetConvoyShips(context.Mission).First();
            context.Player.Position = leader.Position;
            context.World.Update(0.1f, null, 1);
            return Result(context.Mission.ConvoyAbandonmentProgressSeconds == 0f,
                "returning to the convoy did not reset abandonment grace");
        }

        private (bool Success, string FailureReason) ContinuousAbandonmentFails()
        {
            Context context = ActivatedContext();
            context.Player.Position = new Vector3(20_000f, 5_000f, 20_000f);
            for (int i = 0; i < 30 && context.Manager.ActiveMission != null; i++)
                context.World.Update(1f, null, 1);
            return Result(context.Mission.Status == MissionStatus.Failed && context.Manager.ActiveMission == null,
                "continuous convoy abandonment did not fail the mission");
        }

        private (bool Success, string FailureReason) SavePreservesRouteAndStage()
        {
            Context context = EncounterContext();
            SaveMissionData data = Capture(context);
            Mission loaded = BuildSavedMission(data);
            return Result(loaded.Type == MissionType.ConvoyEscort &&
                loaded.ConvoyRouteId == context.Mission.ConvoyRouteId &&
                loaded.ConvoyRouteRingIndex == context.Mission.ConvoyRouteRingIndex &&
                loaded.ConvoyStage == ConvoyEscortStage.EncounterActive &&
                loaded.ConvoyEncounterActivated,
                "save/load lost escort route progress or encounter stage");
        }

        private (bool Success, string FailureReason) SavePreservesConvoyLosses()
        {
            Context context = ActivatedContext();
            context.World.GetConvoyShips(context.Mission).First().ApplyDamage(100_000f, NpcDestructionSource.Npc);
            SaveMissionData data = Capture(context);
            Mission loaded = BuildSavedMission(data);
            return Result(loaded.ConvoyDestroyedCount == 1 &&
                loaded.ConvoyDestroyedMask != 0 && loaded.ConvoySurvivors == context.Mission.ConvoySurvivors,
                "save/load lost convoy survivor state");
        }

        private (bool Success, string FailureReason) LoadDoesNotDuplicateConvoy()
        {
            Context source = AcceptedContext();
            SaveMissionData data = Capture(source);
            Context resumed = CreateContext();
            Mission loaded = BuildSavedMission(data);
            resumed.Manager.RestoreState(new[] { loaded }, null);
            resumed.World.RebindActiveMissions(resumed.Manager.ActiveMissions);
            int firstCount = resumed.Npcs.Count;
            resumed.World.RebindActiveMissions(resumed.Manager.ActiveMissions);
            return Result(firstCount == loaded.ConvoyShipCount && resumed.Npcs.Count == firstCount,
                "load/rebind duplicated transient convoy ships");
        }

        private (bool Success, string FailureReason) LoadDoesNotDuplicateAttackers()
        {
            Context source = EncounterContext();
            SaveMissionData data = Capture(source);
            Context resumed = CreateContext();
            Mission loaded = BuildSavedMission(data);
            resumed.Manager.RestoreState(new[] { loaded }, null);
            resumed.World.RebindActiveMissions(resumed.Manager.ActiveMissions);
            int firstCount = resumed.Npcs.Count;
            resumed.World.RebindActiveMissions(resumed.Manager.ActiveMissions);
            return Result(firstCount == loaded.ConvoyShipCount + loaded.ConvoyAttackForceSize &&
                resumed.Npcs.Count == firstCount,
                "load/rebind duplicated the interrupted Rogue wave");
        }

        private (bool Success, string FailureReason) CompletedEncounterDoesNotRespawn()
        {
            Context context = EncounterContext();
            DestroyAttackers(context, NpcDestructionSource.Npc);
            context.World.Update(0.1f, null, 1);
            int remaining = context.World.GetConvoyAttackers(context.Mission).Count;
            context.World.RebindActiveMissions(context.Manager.ActiveMissions);
            return Result(context.Mission.ConvoyEncounterResolved && remaining == 0 &&
                context.World.GetConvoyAttackers(context.Mission).Count == 0,
                "resolved encounter was reconstructed as an active wave");
        }

        private (bool Success, string FailureReason) ResetClearsConvoyState()
        {
            Context context = EncounterContext();
            context.Manager.ClearState();
            return Result(context.Npcs.Count == 0 && context.SpaceObjects.Count == 0 &&
                context.Manager.ActiveMission == null && context.World.GetConvoyShips(context.Mission).Count == 0,
                "reset retained mission-owned convoy state");
        }

        private (bool Success, string FailureReason) FailureCleanupRemovesAttackers()
        {
            Context context = ActivatedContext();
            context.Player.Position = new Vector3(20_000f, 5_000f, 20_000f);
            for (int i = 0; i < 30 && context.Manager.ActiveMission != null; i++)
                context.World.Update(1f, null, 1);
            return Result(context.Npcs.Count == 0 && context.SpaceObjects.Count == 0,
                "failure left transient convoy or attacker objects behind");
        }

        private (bool Success, string FailureReason) AmbientTrafficRemainsActive()
        {
            Context context = AcceptedContext();
            NpcShip ambient = new("Ambient Trader", new Vector3(-300f, 0f, 500f), Vector3.Zero, 500f, 0f,
                FactionManager.NeutralCivilians);
            ambient.ConfigureTrafficBehavior(TrafficZoneBehaviorType.TraderRoute, "ambient-route", ambient.Position, 500f, 100f, 12_000f,
                new Vector3(-1000f, 0f, 0f), new Vector3(6000f, 0f, 0f));
            context.Npcs.Add(ambient);
            context.SpaceObjects.Add(ambient);
            context.Tradelanes.Update(Frame(0.1f), context.Player, context.Npcs, default);
            return Result(context.Npcs.Contains(ambient) && context.World.GetConvoyShips(context.Mission).Count == context.Mission.ConvoyShipCount,
                "ambient traffic disappeared when the escort convoy was created");
        }

        private (bool Success, string FailureReason) ReverseLaneRemainsAvailable()
        {
            Context context = CreateContext();
            NpcShip reverse = new("Reverse Ambient Trader", new Vector3(5_700f, 0f, 0f), new Vector3(6_000f, 0f, 0f),
                500f, 0f, FactionManager.NeutralCivilians);
            reverse.ConfigureTrafficBehavior(TrafficZoneBehaviorType.TraderRoute, "reverse-route", reverse.Position, 500f, 100f, 12_000f,
                context.Lane.Config.EndPosition, context.Lane.Config.StartPosition);
            context.Npcs.Add(reverse);
            context.Tradelanes.Update(Frame(0.1f), context.Player, context.Npcs, default);
            bool entered = reverse.IsTradeLaneTransit && reverse.TradeLaneDirection == TradeLaneDirection.Reverse;
            return Result(entered && context.Lane.GetRouteRings(TradeLaneDirection.Reverse).Count == context.Lane.GetRouteRings(TradeLaneDirection.Forward).Count,
                "reverse traffic could not use the shared lane route");
        }

        private (bool Success, string FailureReason) IdentityDoesNotCrossContaminate()
        {
            Mission convoy = Offer(CreateContext());
            Mission disruption = Mission.CreateTradeLaneDisruption(
                convoy.ConvoyRouteLaneId,
                convoy.ConvoyRouteSegmentId,
                convoy.ConvoyEncounterRingIndex,
                "Phase 50 lane",
                convoy.ConvoyEncounterPosition.Value,
                convoy.TargetSystemIndex,
                MissionDifficulty.Easy,
                1_000,
                10f,
                "unrelated disruption",
                -0.01f);
            return Result(convoy.Type == MissionType.ConvoyEscort && disruption.Type == MissionType.TradeLaneDisruption &&
                convoy.Id != disruption.Id && convoy.ConvoyEncounterActivated == false,
                "convoy identity contaminated a lane-disruption mission");
        }

        private (bool Success, string FailureReason) RogueDestructionKeepsAttribution()
        {
            Context context = EncounterContext();
            NpcShip attacker = context.World.GetConvoyAttackers(context.Mission).First();
            attacker.ApplyDamage(100_000f, NpcDestructionSource.Npc);
            return Result(attacker.IsDestroyed && attacker.DestructionSource == NpcDestructionSource.Npc &&
                context.Mission.ConvoyAttackersRemaining == context.Mission.ConvoyAttackForceSize - 1,
                "Rogue destruction lost ordinary NPC attribution");
        }

        private (bool Success, string FailureReason) HudExposesEscortGuidance()
        {
            Context context = AcceptedContext();
            bool rendezvous = context.Mission.GetHudProgressLine().Contains("RENDEZVOUS", StringComparison.OrdinalIgnoreCase);
            context.Player.Position = context.Mission.ConvoyRendezvousPosition.Value;
            context.World.Update(0.1f, null, 1);
            bool escort = context.Mission.GetHudProgressLine().Contains("ESCORT", StringComparison.OrdinalIgnoreCase);
            return Result(rendezvous && escort && context.Mission.GetTypeLabel() == "CONVOY ESCORT",
                "HUD did not expose staged convoy escort guidance");
        }

        private (bool Success, string FailureReason) ManualLifecycleReachesDestination()
        {
            Context context = EncounterContext();
            int startingConvoy = context.Mission.ConvoyShipCount;
            DestroyAttackers(context, NpcDestructionSource.Npc);
            bool completed = AdvanceUntil(context, () => context.Mission.Status == MissionStatus.Completed, 300, 0.25f);
            bool success = completed && context.Mission.ConvoyArrivedCount == startingConvoy &&
                context.Mission.ObjectiveComplete && context.Npcs.Count == 0 && context.SpaceObjects.Count == 0;
            string npcNames = string.Join(",", context.Npcs.Select(npc => npc?.Name ?? "<null>"));
            return Result(success,
                $"real convoy lifecycle did not reach destination and clean up (completed={completed}, status={context.Mission.Status}, arrivals={context.Mission.ConvoyArrivedCount}/{startingConvoy}, objective={context.Mission.ObjectiveComplete}, npcs={context.Npcs.Count}, spaceObjects={context.SpaceObjects.Count}, npcNames={npcNames})");
        }

        private Context AcceptedContext(MissionDifficulty difficulty = MissionDifficulty.Easy)
        {
            Context context = CreateContext();
            context.Mission = Offer(context, difficulty);
            if (!context.Manager.AcceptMission(context.Mission, context.Origin))
                throw new InvalidOperationException(context.Manager.LastAcceptanceFailureReason);
            return context;
        }

        private Context ActivatedContext(MissionDifficulty difficulty = MissionDifficulty.Easy)
        {
            Context context = AcceptedContext(difficulty);
            context.Player.Position = context.Mission.ConvoyRendezvousPosition.Value;
            context.World.Update(0.1f, null, 1);
            return context;
        }

        private Context EncounterContext()
        {
            Context context = ActivatedContext();
            bool activated = AdvanceUntil(context, () => context.Mission.ConvoyEncounterActivated, 120, 0.25f);
            if (!activated)
                throw new InvalidOperationException("convoy did not reach deterministic interception");
            return context;
        }

        private Context CompletedContext()
        {
            Context context = EncounterContext();
            DestroyAttackers(context, NpcDestructionSource.Npc);
            float beforeTickX = context.World.GetConvoyShips(context.Mission).FirstOrDefault()?.Position.X ?? float.NaN;
            Tick(context, 0.25f, followConvoy: true);
            float afterTickX = context.World.GetConvoyShips(context.Mission).FirstOrDefault()?.Position.X ?? float.NaN;
            if (!AdvanceUntil(context, () => context.Mission.Status == MissionStatus.Completed, 300, 0.25f))
            {
                string positions = string.Join("; ", context.World.GetConvoyShips(context.Mission)
                    .Select(ship => $"{ship.Name}:{ship.Position}:transit={ship.IsTradeLaneTransit}:end={ship.TrafficRouteEnd}:state={ship.EncounterState}:speed={ship.Speed}:velocity={ship.Velocity}:forward={ship.Forward}:hold={ship.IsMissionHoldPosition}"));
                string route = string.Join(",", context.Lane.GetRouteRings(context.Mission.ConvoyRouteDirection).Select(ring => ring.Position.X.ToString("0")));
                throw new InvalidOperationException($"convoy did not reach destination (stage={context.Mission.ConvoyStage}, arrivals={context.Mission.ConvoyArrivedCount}, ring={context.Mission.ConvoyRouteRingIndex}, ships={context.World.GetConvoyShips(context.Mission).Count}, route={route}, firstTick={beforeTickX:0.##}->{afterTickX:0.##}, positions={positions})");
            }
            return context;
        }

        private Mission Offer(Context context, MissionDifficulty difficulty = MissionDifficulty.Easy) =>
            context.Manager.GenerateConvoyEscortMissions(context.Origin).First(mission => mission.Difficulty == difficulty);

        private static void DestroyAttackers(Context context, NpcDestructionSource source)
        {
            foreach (NpcShip attacker in context.World.GetConvoyAttackers(context.Mission).ToList())
                attacker.ApplyDamage(100_000f, source);
            context.World.Update(0.1f, null, 1);
        }

        private static bool AdvanceUntil(Context context, Func<bool> condition, int maxSteps, float seconds)
        {
            for (int i = 0; i < maxSteps; i++)
            {
                if (condition())
                    return true;
                NpcShip leader = GetActiveConvoyLeader(context);
                if (leader != null)
                    context.Player.Position = leader.Position;
                Tick(context, seconds, followConvoy: false);
            }

            return condition();
        }

        private static void Tick(Context context, float seconds, bool followConvoy)
        {
            if (followConvoy)
            {
                NpcShip leader = GetActiveConvoyLeader(context);
                if (leader != null)
                    context.Player.Position = leader.Position;
            }

            GameTime frame = Frame(seconds);
            context.Tradelanes.Update(frame, context.Player, context.Npcs, default);
            foreach (NpcShip npc in context.Npcs.ToList())
                npc.Update(frame, null, context.Player, context.Reputation);
            context.Traffic.Update(frame, context.Player, context.Reputation);
            context.World.Update(seconds, null, 1);
        }

        private static NpcShip GetActiveConvoyLeader(Context context)
        {
            IReadOnlyList<NpcShip> ships = context?.World?.GetConvoyShips(context.Mission);
            if (ships == null)
                return null;
            for (int i = 0; i < ships.Count; i++)
            {
                NpcShip ship = ships[i];
                if (ship != null && !ship.IsDestroyed && (context.Mission.ConvoyArrivedMask & (1 << i)) == 0)
                    return ship;
            }
            return null;
        }

        private static GameTime Frame(float seconds) => new(TimeSpan.Zero, TimeSpan.FromSeconds(seconds));

        private static (bool Success, string FailureReason) Result(bool success, string reason) =>
            success ? (true, string.Empty) : (false, reason);

        private static SaveMissionData Capture(Context context) =>
            new SaveGameManager(Path.Combine(Path.GetTempPath(), $"phase50-{Guid.NewGuid():N}.json"))
                .CaptureMissions(context.Manager.ActiveMissions).Single();

        private static Mission BuildSavedMission(SaveMissionData data) =>
            new SaveGameManager(Path.Combine(Path.GetTempPath(), $"phase50-load-{Guid.NewGuid():N}.json"))
                .BuildMissionList(new[] { data }, out _).Single();

        private static Context CreateContext()
        {
            Context context = new()
            {
                Credits = new PlayerCredits(10_000),
                Player = new Ship(new Vector3(0f, 5_000f, 0f)),
                Origin = CreateStation("Fort Bush Corporate", FactionManager.LibertyCorporations, 0f),
                Destination = CreateStation("Newark Station", FactionManager.LibertyCorporations, 6_000f),
                Lane = CreateLane()
            };
            context.Stations.Add(context.Origin);
            context.Stations.Add(context.Destination);
            context.Lanes.Add(context.Lane);
            context.Reputation = new ReputationManager(new FactionManager());
            context.Reputation.SetReputation(FactionManager.LibertyCorporations, 0f, "phase50 smoke setup");
            context.Reputation.SetReputation(FactionManager.LibertyPolice, 0f, "phase50 smoke setup");
            context.Reputation.SetReputation(FactionManager.LibertyRogues, -0.6f, "phase50 smoke setup");
            context.Manager = new MissionManager(context.Credits, null, context.Reputation);
            context.Waypoints = new MissionWaypointSystem();
            context.Tradelanes = new TradelaneManager(null, null);
            context.Tradelanes.SetNpcShips(context.Npcs);
            context.Tradelanes.SetLanesForTesting(context.Lanes);
            context.Traffic = new TrafficManager(null, context.Npcs, context.SpaceObjects,
                npc => context.World?.NotifyNpcDestroyed(npc));
            context.World = new MissionWorldManager(
                context.Manager,
                context.Waypoints,
                context.Player,
                context.Npcs,
                context.SpaceObjects,
                () => context.Stations,
                npc => context.World.NotifyNpcDestroyed(npc),
                null,
                null,
                () => context.Lanes,
                null,
                null,
                npc => context.Traffic.RegisterMissionNpc(npc),
                npc => context.Tradelanes.EjectNpcFromTransit(npc));
            context.Traffic.MissionTargetResolver = context.World.GetConvoyCombatTarget;
            context.Manager.SetWaypointSystem(context.Waypoints);
            context.Manager.SetWorldManager(context.World);
            return context;
        }

        private static TradeLane CreateLane() => new(null, new TradelaneConfig
        {
            Id = "phase50_liberty_lane",
            Name = "Fort Bush Trade Corridor",
            SystemIndex = 1,
            StartPositionX = 0f,
            EndPositionX = 6_000f,
            RingSpacing = 1_000f,
            TravelSpeed = 1_500f,
            DockingRange = 800f,
            ActivationRange = 1_000f,
            DisruptionRecoverySeconds = 20f,
            DisruptionDamageThreshold = 100f
        });

        private static Station CreateStation(string name, string factionId, float x) => new(new StationConfig
        {
            Description = name,
            SystemIndex = 1,
            FactionId = factionId,
            StartupPositionX = x,
            StartupPositionY = 0f,
            StartupPositionZ = 0f,
            Radius = 900f,
            DockingRange = 700f
        }, null);

        private sealed class Context
        {
            public PlayerCredits Credits { get; set; }
            public Ship Player { get; set; }
            public Station Origin { get; set; }
            public Station Destination { get; set; }
            public TradeLane Lane { get; set; }
            public Mission Mission { get; set; }
            public List<Station> Stations { get; } = new();
            public List<TradeLane> Lanes { get; } = new();
            public List<NpcShip> Npcs { get; } = new();
            public List<SpaceObject> SpaceObjects { get; } = new();
            public ReputationManager Reputation { get; set; }
            public MissionManager Manager { get; set; }
            public MissionWaypointSystem Waypoints { get; set; }
            public MissionWorldManager World { get; set; }
            public TradelaneManager Tradelanes { get; set; }
            public TrafficManager Traffic { get; set; }
        }
    }
}
