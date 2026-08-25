using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Roguelancer.Configuration;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Roguelancer
{
    /// <summary>
    /// Focused Phase 49 coverage for the lawful trade-lane defense contract.
    /// The harness uses the same mission world, lane, NPC loadout, projectile,
    /// reward, and save authorities as the game runtime.
    /// </summary>
    internal sealed class TradeLaneDefenseMissionSmokeTest
    {
        private readonly GraphicsDevice _graphicsDevice;

        public TradeLaneDefenseMissionSmokeTest(GraphicsDevice graphicsDevice = null)
        {
            _graphicsDevice = graphicsDevice;
        }

        public (int Passed, int Failed) Run()
        {
            int passed = 0;
            int failed = 0;
            Check("Police board exposes defense offers", PoliceBoardExposesOffers, ref passed, ref failed);
            Check("non-Police board exposes no defense offers", NonPoliceBoardExposesNoOffers, ref passed, ref failed);
            Check("defense target identity is stable", StableTargetIdentity, ref passed, ref failed);
            Check("defense offer describes Rogue attack", OfferDescriptionIsSpecific, ref passed, ref failed);
            Check("difficulty scales force and reward", DifficultyScalesForceAndReward, ref passed, ref failed);
            Check("defense acceptance starts en route", AcceptanceStartsEnRoute, ref passed, ref failed);
            Check("wrong employer is rejected", WrongEmployerRejected, ref passed, ref failed);
            Check("activation requires target area", ActivationRequiresTargetArea, ref passed, ref failed);
            Check("activation spawns bounded Rogue force", ActivationSpawnsBoundedForce, ref passed, ref failed);
            Check("attackers use Rogue faction and loadouts", AttackersUseRogueLoadouts, ref passed, ref failed);
            Check("attackers expose the real lane objective", AttackersExposeLaneObjective, ref passed, ref failed);
            Check("NPC projectile disrupts the real lane", NpcProjectileDisruptsRealLane, ref passed, ref failed);
            Check("brief disruption does not fail", BriefDisruptionDoesNotFail, ref passed, ref failed);
            Check("continuous hostile disruption fails", ContinuousDisruptionFails, ref passed, ref failed);
            Check("recovery resets failure progress", RecoveryResetsFailureProgress, ref passed, ref failed);
            Check("destroying attackers prevents further attack", DestroyedAttackersPreventFurtherAttack, ref passed, ref failed);
            Check("success waits for lane recovery", SuccessWaitsForRecovery, ref passed, ref failed);
            Check("success does not require final blows", SuccessDoesNotRequireFinalBlows, ref passed, ref failed);
            Check("successful defense pays exactly once", SuccessfulDefensePaysExactlyOnce, ref passed, ref failed);
            Check("successful defense rewards Police reputation once", SuccessfulDefenseRewardsReputationOnce, ref passed, ref failed);
            Check("failed defense pays nothing", FailedDefensePaysNothing, ref passed, ref failed);
            Check("pre-existing disruption does not immediately fail", PreExistingDisruptionIsDeferred, ref passed, ref failed);
            Check("save preserves defense target and stage", SavePreservesDefenseState, ref passed, ref failed);
            Check("load does not duplicate attackers", LoadDoesNotDuplicateAttackers, ref passed, ref failed);
            Check("load reconstructs an interrupted defense encounter", LoadReconstructsInterruptedEncounter, ref passed, ref failed);
            Check("completion cleanup removes encounter", CompletionCleanupRemovesEncounter, ref passed, ref failed);
            Check("failure cleanup removes encounter", FailureCleanupRemovesEncounter, ref passed, ref failed);
            Check("reset clears defense encounter state", ResetClearsDefenseEncounter, ref passed, ref failed);
            Check("HUD exposes bounded defense guidance", HudExposesDefenseGuidance, ref passed, ref failed);

            Console.WriteLine($"[TRADE-LANE DEFENSE MISSION SMOKE] RESULT: {passed} passed, {failed} failed");
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
                    Console.WriteLine($"[TRADE-LANE DEFENSE MISSION SMOKE] PASS {label}");
                }
                else
                {
                    failed++;
                    Console.WriteLine($"[TRADE-LANE DEFENSE MISSION SMOKE] FAIL {label}: {reason}");
                }
            }
            catch (Exception ex)
            {
                failed++;
                Console.WriteLine($"[TRADE-LANE DEFENSE MISSION SMOKE] FAIL {label}: {ex.Message}");
            }
        }

        private (bool Success, string FailureReason) PoliceBoardExposesOffers()
        {
            TestContext context = CreateContext();
            List<Mission> offers = context.Manager.GenerateTradeLaneDefenseMissions(context.Origin);
            return Result(offers.Count == 3 && offers.All(mission => mission.FactionId == FactionManager.LibertyPolice), "Police board did not expose three Police offers");
        }

        private (bool Success, string FailureReason) NonPoliceBoardExposesNoOffers()
        {
            TestContext context = CreateContext(FactionManager.LibertyCorporations);
            return Result(context.Manager.GenerateTradeLaneDefenseMissions(context.Origin).Count == 0, "corporate board exposed defense work");
        }

        private (bool Success, string FailureReason) StableTargetIdentity()
        {
            TestContext first = CreateContext();
            TestContext second = CreateContext();
            Mission a = Offer(first);
            Mission b = Offer(second);
            return Result(a.TargetLaneId == b.TargetLaneId && a.TargetSegmentId == b.TargetSegmentId && a.TargetRingIndex == b.TargetRingIndex,
                "target identity changed between deterministic boards");
        }

        private (bool Success, string FailureReason) OfferDescriptionIsSpecific()
        {
            Mission mission = Offer(CreateContext());
            return Result(mission.Description.Contains("Rogue", StringComparison.OrdinalIgnoreCase) &&
                mission.Description.Contains(mission.TargetLocation, StringComparison.OrdinalIgnoreCase), "offer did not identify the Rogue threat and lane");
        }

        private (bool Success, string FailureReason) DifficultyScalesForceAndReward()
        {
            TestContext context = CreateContext();
            Mission easy = Offer(context, MissionDifficulty.Easy);
            Mission medium = Offer(context, MissionDifficulty.Medium);
            Mission hard = Offer(context, MissionDifficulty.Hard);
            return Result(easy.DefenseAttackForceSize == 2 && medium.DefenseAttackForceSize == 3 && hard.DefenseAttackForceSize == 4 &&
                easy.Reward < medium.Reward && medium.Reward < hard.Reward, "difficulty conventions did not scale defense terms");
        }

        private (bool Success, string FailureReason) AcceptanceStartsEnRoute()
        {
            TestContext context = AcceptedContext();
            return Result(context.Manager.ActiveMission.DefenseStage == TradeLaneDefenseStage.EnRoute &&
                context.Npcs.Count == 0, "acceptance spawned the encounter too early");
        }

        private (bool Success, string FailureReason) WrongEmployerRejected()
        {
            TestContext context = CreateContext();
            Mission mission = Offer(context);
            mission.FactionId = FactionManager.LibertyRogues;
            return Result(!context.Manager.AcceptMission(mission, context.Origin), "non-Police defense employer was accepted");
        }

        private (bool Success, string FailureReason) ActivationRequiresTargetArea()
        {
            TestContext context = AcceptedContext();
            Tick(context, 1f);
            return Result(context.Manager.ActiveMission.DefenseStage == TradeLaneDefenseStage.EnRoute && context.Npcs.Count == 0,
                "defense activated before the player reached the segment");
        }

        private (bool Success, string FailureReason) ActivationSpawnsBoundedForce()
        {
            TestContext context = ActivatedContext();
            return Result(context.Manager.ActiveMission.DefenseStage == TradeLaneDefenseStage.AttackActive &&
                context.Npcs.Count == context.Manager.ActiveMission.DefenseAttackForceSize &&
                context.Npcs.Count >= 2 && context.Npcs.Count <= 4, "defense force was not bounded or activation-gated");
        }

        private (bool Success, string FailureReason) AttackersUseRogueLoadouts()
        {
            TestContext context = ActivatedContext();
            bool valid = context.Npcs.Count > 0 && context.Npcs.All(attacker =>
                attacker.FactionId == FactionManager.LibertyRogues &&
                attacker.Loadout != null &&
                attacker.Loadout.GetMountedGuns().Any() &&
                attacker.Loadout.GetMountedGuns().All(NpcEquipmentLoadoutFactory.IsValidNpcWeapon));
            return Result(valid, "defense attackers did not use ordinary Rogue loadouts");
        }

        private (bool Success, string FailureReason) AttackersExposeLaneObjective()
        {
            TestContext context = ActivatedContext();
            NpcShip attacker = context.Npcs[0];
            TradeLaneAttackTarget objective = context.World.GetTradeLaneAttackTarget(attacker);
            return Result(objective != null && objective.Lane == context.Lane && objective.RingIndex == context.Manager.ActiveMission.TargetRingIndex,
                "active attacker had no authoritative lane objective");
        }

        private (bool Success, string FailureReason) NpcProjectileDisruptsRealLane()
        {
            if (_graphicsDevice == null)
                return Fail("graphics device was unavailable for projectile coverage");

            TestContext context = ActivatedContext();
            NpcWeaponSystem weapons = new(_graphicsDevice, context.Reputation);
            weapons.TradeLaneTargetResolver = context.World.GetTradeLaneAttackTarget;
            bool fired = false;
            weapons.NpcWeaponFired += (_, target, _, _) => fired |= target == null;

            for (int i = 0; i < 160 && context.Lane.GetDisruptionState(context.Manager.ActiveMission.TargetRingIndex) == TradeLaneDisruptionState.Operational; i++)
            {
                GameTime frame = Frame(0.1f);
                weapons.Update(frame, context.Npcs, context.Player);
                context.Lane.Update(frame);
            }

            return Result(fired && context.Lane.GetDisruptionState(context.Manager.ActiveMission.TargetRingIndex) == TradeLaneDisruptionState.Disrupted,
                "ordinary NPC projectile path did not disrupt the assigned lane ring");
        }

        private (bool Success, string FailureReason) BriefDisruptionDoesNotFail()
        {
            TestContext context = ActivatedContext();
            NpcShip attacker = context.Npcs[0];
            context.Lane.TryDisruptSegment(context.Manager.ActiveMission.TargetRingIndex, TradeLaneDisruptionSource.Npc, attacker.Name, 20f);
            Tick(context, 1f);
            return Result(context.Manager.ActiveMission != null && context.Manager.ActiveMission.DefenseFailureHoldProgressSeconds == 1f,
                "brief hostile disruption did not record bounded progress");
        }

        private (bool Success, string FailureReason) ContinuousDisruptionFails()
        {
            TestContext context = ActivatedContext();
            NpcShip attacker = context.Npcs[0];
            Mission mission = context.Manager.ActiveMission;
            context.Lane.TryDisruptSegment(mission.TargetRingIndex, TradeLaneDisruptionSource.Npc, attacker.Name, 30f);
            context.World.Update(mission.DefenseFailureHoldSeconds, null, 1);
            return Result(context.Manager.CompletedMissions.Any(candidate => candidate.Status == MissionStatus.Failed) &&
                mission.DefenseStage == TradeLaneDefenseStage.Failed, "continuous hostile hold did not fail defense");
        }

        private (bool Success, string FailureReason) RecoveryResetsFailureProgress()
        {
            TestContext context = ActivatedContext();
            NpcShip attacker = context.Npcs[0];
            Mission mission = context.Manager.ActiveMission;
            context.Lane.TryDisruptSegment(mission.TargetRingIndex, TradeLaneDisruptionSource.Npc, attacker.Name, 2f);
            Tick(context, 1f);
            context.Lane.Update(Frame(2f));
            context.World.Update(0.1f, null, 1);
            return Result(context.Manager.ActiveMission != null && mission.DefenseFailureHoldProgressSeconds == 0f &&
                mission.DefenseStage == TradeLaneDefenseStage.AttackActive, "lane recovery did not reset defense hold");
        }

        private (bool Success, string FailureReason) DestroyedAttackersPreventFurtherAttack()
        {
            TestContext context = ActivatedContext();
            Mission mission = context.Manager.ActiveMission;
            DestroyAttackers(context);
            context.World.Update(0.1f, null, 1);
            return Result(context.Manager.CompletedMissions.Contains(mission) && context.Npcs.Count == 0 &&
                context.World.GetTradeLaneAttackTarget(null) == null, "destroyed attackers retained mission attack state");
        }

        private (bool Success, string FailureReason) SuccessWaitsForRecovery()
        {
            TestContext context = ActivatedContext();
            Mission mission = context.Manager.ActiveMission;
            NpcShip attacker = context.Npcs[0];
            context.Lane.TryDisruptSegment(mission.TargetRingIndex, TradeLaneDisruptionSource.Npc, attacker.Name, 2f);
            DestroyAttackers(context);
            context.World.Update(0.1f, null, 1);
            bool waiting = context.Manager.ActiveMission == mission && mission.DefenseStage == TradeLaneDefenseStage.AwaitingRecovery;
            context.Lane.Update(Frame(2f));
            context.World.Update(0.1f, null, 1);
            return Result(waiting && context.Manager.CompletedMissions.Contains(mission), "success did not wait for authoritative lane recovery");
        }

        private (bool Success, string FailureReason) SuccessDoesNotRequireFinalBlows()
        {
            TestContext context = ActivatedContext();
            DestroyAttackers(context);
            return Result(context.Manager.ActiveMission == null && context.Manager.CompletedMissions.Count == 1,
                "defense completion depended on player final-blow attribution");
        }

        private (bool Success, string FailureReason) SuccessfulDefensePaysExactlyOnce()
        {
            TestContext context = ActivatedContext();
            Mission mission = context.Manager.ActiveMission;
            DestroyAttackers(context);
            int before = context.Credits.Credits;
            bool first = context.Manager.TryClaimReward(mission, context.Origin, out _);
            bool second = context.Manager.TryClaimReward(mission, context.Origin, out _);
            return Result(first && !second && context.Credits.Credits - before == mission.Reward, "defense reward transaction was not exactly once");
        }

        private (bool Success, string FailureReason) SuccessfulDefenseRewardsReputationOnce()
        {
            TestContext context = ActivatedContext();
            Mission mission = context.Manager.ActiveMission;
            float before = context.Reputation.GetStanding(FactionManager.LibertyPolice);
            DestroyAttackers(context);
            context.Manager.TryClaimReward(mission, context.Origin, out _);
            float afterFirst = context.Reputation.GetStanding(FactionManager.LibertyPolice);
            context.Manager.TryClaimReward(mission, context.Origin, out _);
            return Result(afterFirst > before && context.Reputation.GetStanding(FactionManager.LibertyPolice) == afterFirst,
                "Police reputation reward was not applied exactly once");
        }

        private (bool Success, string FailureReason) FailedDefensePaysNothing()
        {
            TestContext context = ActivatedContext();
            Mission mission = context.Manager.ActiveMission;
            NpcShip attacker = context.Npcs[0];
            context.Lane.TryDisruptSegment(mission.TargetRingIndex, TradeLaneDisruptionSource.Npc, attacker.Name, 30f);
            int before = context.Credits.Credits;
            context.World.Update(mission.DefenseFailureHoldSeconds, null, 1);
            bool claimed = context.Manager.TryClaimReward(mission, context.Origin, out _);
            return Result(!claimed && context.Credits.Credits == before && mission.Status == MissionStatus.Failed,
                "failed defense paid or remained claimable");
        }

        private (bool Success, string FailureReason) PreExistingDisruptionIsDeferred()
        {
            TestContext context = CreateContext();
            Mission mission = Offer(context);
            context.Lane.TryDisruptSegment(mission.TargetRingIndex, TradeLaneDisruptionSource.Environment, "pre-existing", 20f);
            context.Manager.AcceptMission(mission, context.Origin);
            context.Player.Position = mission.TargetPosition.Value;
            context.World.Update(1f, null, 1);
            bool deferred = context.Manager.ActiveMission == mission && mission.DefenseStage == TradeLaneDefenseStage.EnRoute && context.Npcs.Count == 0;
            context.Lane.Update(Frame(20f));
            context.World.Update(0.1f, null, 1);
            return Result(deferred && mission.DefenseStage == TradeLaneDefenseStage.AttackActive, "pre-existing disruption invalidated the mission");
        }

        private (bool Success, string FailureReason) SavePreservesDefenseState()
        {
            TestContext context = ActivatedContext();
            SaveMissionData data = new SaveGameManager(SavePath()).CaptureMissions(context.Manager.ActiveMissions).Single();
            List<Mission> restored = new SaveGameManager(SavePath()).BuildMissionList(new[] { data }, out _);
            Mission loaded = restored.Single();
            return Result(data.DefenseStage == TradeLaneDefenseStage.AttackActive &&
                loaded.Type == MissionType.TradeLaneDefense && loaded.TargetLaneId == context.Manager.ActiveMission.TargetLaneId &&
                loaded.TargetSegmentId == context.Manager.ActiveMission.TargetSegmentId && loaded.DefenseStage == TradeLaneDefenseStage.AttackActive,
                "save/load lost defense target or stage");
        }

        private (bool Success, string FailureReason) LoadDoesNotDuplicateAttackers()
        {
            TestContext source = ActivatedContext();
            SaveMissionData data = new SaveGameManager(SavePath()).CaptureMissions(source.Manager.ActiveMissions).Single();
            TestContext resumed = CreateContext();
            Mission loaded = new SaveGameManager(SavePath()).BuildMissionList(new[] { data }, out _).Single();
            resumed.Manager.RestoreState(new[] { loaded }, null);
            resumed.World.RebindActiveMissions(resumed.Manager.ActiveMissions);
            int firstCount = resumed.Npcs.Count;
            resumed.World.RebindActiveMissions(resumed.Manager.ActiveMissions);
            return Result(firstCount >= 2 && firstCount <= 4 && resumed.Npcs.Count == firstCount,
                "load/rebind duplicated the transient Rogue group");
        }

        private (bool Success, string FailureReason) LoadReconstructsInterruptedEncounter()
        {
            TestContext source = ActivatedContext();
            Mission sourceMission = source.Manager.ActiveMission;
            NpcShip attacker = source.Npcs[0];
            source.Lane.TryDisruptSegment(sourceMission.TargetRingIndex, TradeLaneDisruptionSource.Npc, attacker.Name, 20f);
            source.World.Update(0.1f, null, 1);

            SaveMissionData data = new SaveGameManager(SavePath()).CaptureMissions(source.Manager.ActiveMissions).Single();
            TestContext resumed = CreateContext();
            Mission loaded = new SaveGameManager(SavePath()).BuildMissionList(new[] { data }, out _).Single();
            resumed.Manager.RestoreState(new[] { loaded }, null);
            resumed.World.RebindActiveMissions(resumed.Manager.ActiveMissions);
            return Result(resumed.Manager.ActiveMission == loaded &&
                loaded.Status == MissionStatus.InProgress &&
                loaded.DefenseStage == TradeLaneDefenseStage.AttackActive &&
                resumed.Npcs.Count >= 2 && resumed.Npcs.Count <= 4 &&
                resumed.Manager.CompletedMissions.Count == 0,
                "load completed or failed an interrupted defense instead of rebuilding its bounded attack");
        }

        private (bool Success, string FailureReason) CompletionCleanupRemovesEncounter()
        {
            TestContext context = ActivatedContext();
            DestroyAttackers(context);
            return Result(context.Npcs.Count == 0 && context.SpaceObjects.Count == 0 && context.Manager.ActiveMission == null,
                "completion left mission NPC or space-object references");
        }

        private (bool Success, string FailureReason) FailureCleanupRemovesEncounter()
        {
            TestContext context = ActivatedContext();
            Mission mission = context.Manager.ActiveMission;
            NpcShip attacker = context.Npcs[0];
            context.Lane.TryDisruptSegment(mission.TargetRingIndex, TradeLaneDisruptionSource.Npc, attacker.Name, 30f);
            context.World.Update(mission.DefenseFailureHoldSeconds, null, 1);
            return Result(context.Npcs.Count == 0 && context.SpaceObjects.Count == 0 && context.Manager.ActiveMission == null,
                "failure left mission NPC or space-object references");
        }

        private (bool Success, string FailureReason) ResetClearsDefenseEncounter()
        {
            TestContext context = ActivatedContext();
            context.Manager.ClearState();
            return Result(context.Npcs.Count == 0 && context.SpaceObjects.Count == 0 && context.Manager.ActiveMission == null,
                "world reset retained defense encounter state");
        }

        private (bool Success, string FailureReason) HudExposesDefenseGuidance()
        {
            Mission mission = AcceptedContext().Manager.ActiveMission;
            return Result(mission.GetHudProgressLine().Contains("PROCEED", StringComparison.OrdinalIgnoreCase) &&
                mission.GetTypeLabel() == "TRADE-LANE DEFENSE", "HUD did not expose defense guidance");
        }

        private TestContext AcceptedContext()
        {
            TestContext context = CreateContext();
            Mission mission = Offer(context);
            if (!context.Manager.AcceptMission(mission, context.Origin))
                throw new InvalidOperationException(context.Manager.LastAcceptanceFailureReason);
            return context;
        }

        private TestContext ActivatedContext()
        {
            TestContext context = AcceptedContext();
            context.Player.Position = context.Manager.ActiveMission.TargetPosition.Value;
            context.World.Update(0.1f, null, 1);
            return context;
        }

        private Mission Offer(TestContext context, MissionDifficulty difficulty = MissionDifficulty.Easy) =>
            context.Manager.GenerateTradeLaneDefenseMissions(context.Origin).First(mission => mission.Difficulty == difficulty);

        private static void DestroyAttackers(TestContext context)
        {
            foreach (NpcShip attacker in context.Npcs.ToList())
                attacker.ApplyDamage(100_000f, NpcDestructionSource.Player);

            context.World.Update(0.1f, null, 1);
        }

        private static void Tick(TestContext context, float seconds)
        {
            context.Lane.Update(Frame(seconds));
            context.World.Update(seconds, null, 1);
        }

        private static GameTime Frame(float seconds) => new(TimeSpan.Zero, TimeSpan.FromSeconds(seconds));

        private static string SavePath() => Path.Combine(Path.GetTempPath(), $"phase49-{Guid.NewGuid():N}.json");

        private static (bool Success, string FailureReason) Result(bool success, string failureReason) =>
            success ? (true, string.Empty) : (false, failureReason);

        private static (bool Success, string FailureReason) Fail(string reason) => (false, reason);

        private static TestContext CreateContext(string factionId = FactionManager.LibertyPolice)
        {
            TestContext context = new()
            {
                Credits = new PlayerCredits(10_000),
                Player = new Ship(Vector3.Zero),
                Origin = CreateStation(factionId),
                Lane = CreateLane()
            };
            context.Stations.Add(context.Origin);
            context.Lanes.Add(context.Lane);
            context.Reputation = new ReputationManager(new FactionManager());
            // The normal new-game profile starts Police at -0.25, while this
            // focused contract fixture represents an eligible Neutral pilot.
            context.Reputation.SetReputation(FactionManager.LibertyPolice, 0f, "phase49 smoke setup");
            context.Manager = new MissionManager(context.Credits, null, context.Reputation);
            context.Waypoints = new MissionWaypointSystem();
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
                () => context.Lanes);
            context.Manager.SetWaypointSystem(context.Waypoints);
            context.Manager.SetWorldManager(context.World);
            return context;
        }

        private static TradeLane CreateLane() => new(null, new TradelaneConfig
        {
            Id = "phase49_liberty_lane",
            Name = "Fort Bush Trade Corridor",
            SystemIndex = 1,
            StartPositionX = 0f,
            EndPositionX = 6000f,
            RingSpacing = 1000f,
            DisruptionRecoverySeconds = 20f,
            DisruptionDamageThreshold = 100f
        });

        private static Station CreateStation(string factionId) => new(new StationConfig
        {
            Description = factionId == FactionManager.LibertyPolice ? "Fort Bush Police" : "Fort Bush Corporate",
            SystemIndex = 1,
            FactionId = factionId,
            Radius = 900f,
            DockingRange = 700f
        }, null);

        private sealed class TestContext
        {
            public PlayerCredits Credits { get; set; }
            public Ship Player { get; set; }
            public Station Origin { get; set; }
            public TradeLane Lane { get; set; }
            public List<Station> Stations { get; } = new();
            public List<TradeLane> Lanes { get; } = new();
            public List<NpcShip> Npcs { get; } = new();
            public List<SpaceObject> SpaceObjects { get; } = new();
            public ReputationManager Reputation { get; set; }
            public MissionManager Manager { get; set; }
            public MissionWaypointSystem Waypoints { get; set; }
            public MissionWorldManager World { get; set; }
        }
    }
}
