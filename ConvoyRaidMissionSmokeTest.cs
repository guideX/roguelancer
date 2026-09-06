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
    /// Phase 51 coverage for cargo interdiction. The important path is real:
    /// mission-bound TraderRoute NPC -> destruction attribution -> physical
    /// CargoPod -> capacity-aware pickup -> mission reward claim.
    /// </summary>
    internal sealed class ConvoyRaidMissionSmokeTest
    {
        private readonly GraphicsDevice _graphicsDevice;

        public ConvoyRaidMissionSmokeTest(GraphicsDevice graphicsDevice = null)
        {
            _graphicsDevice = graphicsDevice;
        }

        public (int Passed, int Failed) Run()
        {
            int passed = 0;
            int failed = 0;
            Check("board exposes three cargo-interdiction offers", BoardExposesOffers, ref passed, ref failed);
            Check("offer identity is deterministic", OfferIdentityIsStable, ref passed, ref failed);
            Check("difficulty cargo allocation is bounded", DifficultyTermsAreBounded, ref passed, ref failed);
            Check("Rogue offer text names cargo and corridor", OfferPresentationIsSpecific, ref passed, ref failed);
            Check("pre-owned cargo does not satisfy the raid", PreOwnedCargoIsNotCredited, ref passed, ref failed);
            Check("acceptance creates real commercial transports", AcceptanceCreatesTransports, ref passed, ref failed);
            Check("raid activates at the interception point", InterceptionActivates, ref passed, ref failed);
            Check("transport destruction releases a physical mission pod", DestructionReleasesPhysicalCargo, ref passed, ref failed);
            Check("mission cargo pickup is capacity-aware", PickupBindsMissionCargo, ref passed, ref failed);
            Check("full cargo hold leaves the pod and reservation untouched", FullHoldBlocksPickup, ref passed, ref failed);
            Check("jettison reduces authoritative recovery progress", JettisonReducesProgress, ref passed, ref failed);
            Check("one lost transport still leaves a completable raid", PartialTransportLossRemainsCompletable, ref passed, ref failed);
            Check("expired cargo is counted as unrecoverable", ExpiredCargoCanFailRaid, ref passed, ref failed);
            Check("successful recovery pays once and consumes required cargo", SuccessfulRecoveryPaysOnce, ref passed, ref failed);
            Check("mission cargo pod save/rebind is durable", SaveRestoresPhysicalMissionPod, ref passed, ref failed);
            Check("raid identity does not mutate escort identity", IdentityIsSeparateFromEscort, ref passed, ref failed);

            Console.WriteLine($"[CONVOY RAID MISSION SMOKE] RESULT: {passed} passed, {failed} failed");
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
                    Console.WriteLine($"[CONVOY RAID MISSION SMOKE] PASS {label}");
                }
                else
                {
                    failed++;
                    Console.WriteLine($"[CONVOY RAID MISSION SMOKE] FAIL {label}: {reason}");
                }
            }
            catch (Exception ex)
            {
                failed++;
                Console.WriteLine($"[CONVOY RAID MISSION SMOKE] FAIL {label}: {ex.Message}");
            }
        }

        private (bool Success, string FailureReason) BoardExposesOffers()
        {
            Context context = CreateContext();
            List<Mission> offers = context.Manager.GenerateConvoyRaidMissions(context.Origin);
            return Result(offers.Count == 3 && offers.All(mission => mission.Type == MissionType.ConvoyRaid &&
                mission.FactionId == FactionManager.LibertyRogues), "Rogue board did not expose three bounded raid offers");
        }

        private (bool Success, string FailureReason) OfferIdentityIsStable()
        {
            Mission first = Offer(CreateContext(), MissionDifficulty.Medium);
            Mission second = Offer(CreateContext(), MissionDifficulty.Medium);
            return Result(first.RaidRouteId == second.RaidRouteId &&
                first.RaidRouteLaneId == second.RaidRouteLaneId &&
                first.RaidRouteSegmentId == second.RaidRouteSegmentId &&
                first.RaidRouteDirection == second.RaidRouteDirection &&
                first.RaidCommodityId == second.RaidCommodityId &&
                first.DestinationStationId == second.DestinationStationId &&
                first.Reward == second.Reward,
                "deterministic route or cargo identity changed between board reads");
        }

        private (bool Success, string FailureReason) DifficultyTermsAreBounded()
        {
            Mission easy = Offer(CreateContext(), MissionDifficulty.Easy);
            Mission medium = Offer(CreateContext(), MissionDifficulty.Medium);
            Mission hard = Offer(CreateContext(), MissionDifficulty.Hard);
            return Result(easy.RaidShipCount == 2 && easy.RaidRequiredQuantity == 3 && easy.RaidCargoAllocation.SequenceEqual(new[] { 3, 2 }) &&
                medium.RaidShipCount == 3 && medium.RaidRequiredQuantity == 5 && medium.RaidCargoAllocation.SequenceEqual(new[] { 3, 3, 2 }) &&
                hard.RaidShipCount == 4 && hard.RaidRequiredQuantity == 8 && hard.RaidCargoAllocation.SequenceEqual(new[] { 3, 3, 3, 2 }) &&
                easy.RaidTotalAllocatedQuantity >= easy.RaidRequiredQuantity && hard.Reward > medium.Reward && medium.Reward > easy.Reward,
                "difficulty terms did not match the bounded cargo policy");
        }

        private (bool Success, string FailureReason) OfferPresentationIsSpecific()
        {
            Mission mission = Offer(CreateContext(), MissionDifficulty.Medium);
            Commodity commodity = CommodityCatalog.GetById(mission.RaidCommodityId);
            return Result(mission.GetTypeLabel() == "CARGO INTERDICTION" &&
                commodity != null &&
                mission.Description.Contains(commodity.Name, StringComparison.OrdinalIgnoreCase) &&
                mission.Description.Contains(mission.TargetLocation, StringComparison.OrdinalIgnoreCase) &&
                mission.GetObjectiveText().Contains("recover", StringComparison.OrdinalIgnoreCase),
                "offer presentation omitted cargo or corridor details");
        }

        private (bool Success, string FailureReason) PreOwnedCargoIsNotCredited()
        {
            Context context = CreateContext();
            Mission mission = Offer(context);
            Commodity commodity = CommodityCatalog.GetById(mission.RaidCommodityId);
            bool ordinaryBeforeAcceptance = commodity != null && context.Player.CargoHold.AddCommodity(commodity, 1);
            context.Mission = mission;
            bool accepted = context.Manager.AcceptMission(mission, context.Origin);
            bool ordinaryAfterAcceptance = commodity != null && context.Player.CargoHold.AddCommodity(commodity, 1);
            context.World.Update(0.1f, null, 1);
            return Result(ordinaryBeforeAcceptance && accepted && ordinaryAfterAcceptance &&
                context.Player.CargoHold.GetMissionCargoQuantity(mission.Id) == 0 &&
                mission.CurrentProgress == 0 && !mission.ObjectiveComplete,
                "ordinary copies of the designated commodity were credited without a physical mission pickup");
        }

        private (bool Success, string FailureReason) AcceptanceCreatesTransports()
        {
            Context context = AcceptedContext();
            IReadOnlyList<NpcShip> ships = context.World.GetConvoyRaidShips(context.Mission);
            return Result(ships.Count == context.Mission.RaidShipCount &&
                ships.All(ship => ship.FactionId == FactionManager.LibertyCorporations &&
                    ship.TrafficBehavior == TrafficZoneBehaviorType.TraderRoute &&
                    ship.TrafficRouteStart.HasValue && ship.TrafficRouteEnd.HasValue && !ship.IsMissionHoldPosition &&
                    ship.Loadout != null && ship.Hull != null && ship.Shields != null && ship.WeaponEnergy != null) &&
                context.Player.CargoHold.GetMissionCargoQuantity(context.Mission.Id) == 0,
                "acceptance did not create a normal moving commercial convoy");
        }

        private (bool Success, string FailureReason) InterceptionActivates()
        {
            Context context = AcceptedContext();
            context.Player.Position = context.Mission.RaidInterceptionPosition.Value;
            context.World.Update(0.1f, null, 1);
            return Result(context.Mission.RaidInterceptionActivated &&
                context.Mission.RaidStage == ConvoyRaidStage.InterceptionActive &&
                context.Mission.GetHudProgressLine().Contains("INTERCEPT", StringComparison.OrdinalIgnoreCase),
                "player arrival did not activate the raid corridor");
        }

        private (bool Success, string FailureReason) DestructionReleasesPhysicalCargo()
        {
            Context context = AcceptedContext();
            NpcShip target = context.World.GetConvoyRaidShips(context.Mission).First();
            DestroyTransport(context, target);
            CargoPod pod = context.Loot.ActivePods.FirstOrDefault(candidate => candidate.IsMissionCargo);
            return Result(target.IsDestroyed && context.Mission.RaidDestroyedCount == 1 && pod != null &&
                pod.MissionId == context.Mission.Id && pod.Quantity == context.Mission.RaidCargoAllocation[0] &&
                context.Player.CargoHold.GetMissionCargoQuantity(context.Mission.Id) == 0,
                "destroyed transport did not produce an attributed physical mission pod");
        }

        private (bool Success, string FailureReason) PickupBindsMissionCargo()
        {
            Context context = AcceptedContext();
            NpcShip target = context.World.GetConvoyRaidShips(context.Mission).First();
            DestroyTransport(context, target);
            CargoPod pod = context.Loot.ActivePods.First(candidate => candidate.IsMissionCargo);
            pod.Velocity = Vector3.Zero;
            context.Player.Position = pod.Position;
            context.Loot.Update(Frame(0.1f), context.Player, false);
            context.World.Update(0.1f, null, 1);
            return Result(context.Player.CargoHold.GetMissionCargoQuantity(context.Mission.Id) == pod.InitialQuantity &&
                context.Mission.CurrentProgress == Math.Min(context.Mission.RaidRequiredQuantity, pod.InitialQuantity) &&
                !context.Loot.ActivePods.Contains(pod),
                "physical cargo pickup did not bind the expected mission quantity");
        }

        private (bool Success, string FailureReason) FullHoldBlocksPickup()
        {
            Context context = AcceptedContext();
            NpcShip target = context.World.GetConvoyRaidShips(context.Mission).First();
            DestroyTransport(context, target);
            CargoPod pod = context.Loot.ActivePods.First(candidate => candidate.IsMissionCargo);
            context.Player.CargoHold.SetMaxCapacity(0);
            context.Player.Position = pod.Position;
            context.Loot.Update(Frame(0.1f), context.Player, false);
            bool blocked = context.Loot.ActivePods.Contains(pod) &&
                context.Player.CargoHold.GetMissionCargoQuantity(context.Mission.Id) == 0;
            context.Player.CargoHold.SetMaxCapacity(50);
            return Result(blocked, "full hold consumed or removed mission cargo");
        }

        private (bool Success, string FailureReason) JettisonReducesProgress()
        {
            Context context = AcceptedContext();
            NpcShip target = context.World.GetConvoyRaidShips(context.Mission).First();
            DestroyTransport(context, target);
            CargoPod pod = context.Loot.ActivePods.First(candidate => candidate.IsMissionCargo);
            pod.Velocity = Vector3.Zero;
            context.Player.Position = pod.Position;
            context.Loot.Update(Frame(0.1f), context.Player, false);
            int before = context.Player.CargoHold.GetMissionCargoQuantity(context.Mission.Id);
            bool jettisoned = context.Loot.TryJettisonMissionCargo(
                context.Player.CargoHold,
                context.Mission.Id,
                CommodityCatalog.GetById(context.Mission.RaidCommodityId),
                1,
                context.Player.Position,
                Vector3.Zero,
                out CargoPod jettisonedPod);
            context.World.Update(0.1f, null, 1);
            bool reduced = before == 3 && jettisoned && jettisonedPod?.IsMissionCargo == true &&
                context.Mission.CurrentProgress == 2 &&
                context.Player.CargoHold.GetMissionCargoQuantity(context.Mission.Id) == 2;
            context.Loot.Update(Frame(0.1f), context.Player, false);
            context.World.Update(0.1f, null, 1);
            return Result(reduced && context.Mission.CurrentProgress == 3 &&
                context.Player.CargoHold.GetMissionCargoQuantity(context.Mission.Id) == 3,
                "jettison/re-pickup did not preserve authoritative mission attribution");
        }

        private (bool Success, string FailureReason) PartialTransportLossRemainsCompletable()
        {
            Context context = AcceptedContext();
            NpcShip target = context.World.GetConvoyRaidShips(context.Mission).Skip(1).First();
            DestroyTransport(context, target);
            CargoPod pod = context.Loot.ActivePods.First(candidate => candidate.IsMissionCargo &&
                candidate.MissionCargoSourceIndex == 1);
            pod.RestoreAge(pod.LifetimeSeconds);
            context.Loot.Update(Frame(0.1f), context.Player, false);
            context.World.Update(0.1f, null, 1);
            return Result(context.Mission.RaidCargoLostQuantity == context.Mission.RaidCargoAllocation[1] &&
                context.Mission.RaidRemainingPossibleQuantity >= context.Mission.RaidRequiredQuantity &&
                context.Mission.Status == MissionStatus.Active,
                "a bounded single-transport cargo loss caused premature mission failure");
        }

        private (bool Success, string FailureReason) ExpiredCargoCanFailRaid()
        {
            Context context = AcceptedContext();
            NpcShip target = context.World.GetConvoyRaidShips(context.Mission).First();
            DestroyTransport(context, target);
            CargoPod pod = context.Loot.ActivePods.First(candidate => candidate.IsMissionCargo);
            pod.RestoreAge(pod.LifetimeSeconds);
            context.Loot.Update(Frame(0.1f), context.Player, false);
            context.World.Update(0.1f, null, 1);
            return Result(context.Mission.RaidCargoLostQuantity == context.Mission.RaidCargoAllocation[0] &&
                context.Mission.Status == MissionStatus.Failed,
                "unrecoverable released cargo did not fail the impossible raid");
        }

        private (bool Success, string FailureReason) SuccessfulRecoveryPaysOnce()
        {
            Context context = AcceptedContext();
            foreach (NpcShip target in context.World.GetConvoyRaidShips(context.Mission).ToList())
                DestroyTransport(context, target);
            foreach (CargoPod pod in context.Loot.ActivePods.Where(candidate => candidate.IsMissionCargo).ToList())
            {
                pod.Velocity = Vector3.Zero;
                pod.Position = context.Player.Position;
                context.Loot.Update(Frame(0.1f), context.Player, false);
            }
            context.World.Update(0.1f, null, 1);
            int beforeCredits = context.Credits.Credits;
            bool completed = context.Mission.Status == MissionStatus.Completed &&
                context.Player.CargoHold.GetMissionCargoQuantity(context.Mission.Id) == context.Mission.RaidTotalAllocatedQuantity;
            bool claimed = context.Manager.TryClaimReward(context.Mission, context.Origin, out _);
            int afterCredits = context.Credits.Credits;
            bool claimedAgain = context.Manager.TryClaimReward(context.Mission, context.Origin, out _);
            return Result(completed && claimed && !claimedAgain && afterCredits - beforeCredits == context.Mission.Reward &&
                context.Player.CargoHold.GetMissionCargoQuantity(context.Mission.Id) == 0,
                "successful cargo recovery did not complete or pay exactly once");
        }

        private (bool Success, string FailureReason) SaveRestoresPhysicalMissionPod()
        {
            Context source = AcceptedContext();
            DestroyTransport(source, source.World.GetConvoyRaidShips(source.Mission).First());
            SaveGameManager saveManager = NewSaveManager("phase51-save");
            SaveMissionData missionData = saveManager.CaptureMissions(source.Manager.ActiveMissions).Single();
            SaveCargoPodData podData = source.Loot.CaptureMissionCargoPods().Single();

            Context restored = CreateContext();
            Mission restoredMission = saveManager.BuildMissionList(new[] { missionData }, out _).Single();
            restored.Manager.RestoreState(new[] { restoredMission }, null);
            restored.World.RebindActiveMissions(restored.Manager.ActiveMissions);
            int restoredPods = restored.Loot.RestoreMissionCargoPods(new[] { podData }, id => id == restoredMission.Id);
            CargoPod pod = restored.Loot.ActivePods.SingleOrDefault(candidate => candidate.IsMissionCargo);
            return Result(restoredPods == 1 && pod != null && pod.MissionId == restoredMission.Id &&
                pod.MissionCargoSourceIndex == 0 && pod.Quantity == podData.Quantity &&
                restoredMission.RaidDestroyedMask == source.Mission.RaidDestroyedMask,
                "save/rebind did not restore the physical mission pod and durable loss mask");
        }

        private (bool Success, string FailureReason) IdentityIsSeparateFromEscort()
        {
            Context context = CreateContext();
            Mission raid = Offer(context);
            Mission escort = Mission.CreateConvoyEscort(
                "escort-route", "phase51_liberty_lane", "phase51_liberty_lane:escort:forward",
                TradeLaneDirection.Forward, "Phase 51 lane", context.Origin, context.Destination,
                context.Lane.GetRingTravelPosition(context.Lane.GetRouteRings(TradeLaneDirection.Forward)[0], TradeLaneDirection.Forward),
                context.Lane.GetRingTravelPosition(context.Lane.GetRouteRings(TradeLaneDirection.Forward)[1], TradeLaneDirection.Forward),
                1, 2, 2, 1000, MissionDifficulty.Easy, "escort", "Corporate");
            return Result(raid.Type == MissionType.ConvoyRaid && escort != null &&
                raid.Id != escort.Id && raid.RaidRouteSegmentId.Contains(":raid:") &&
                escort.ConvoyRouteSegmentId.Contains(":escort:"),
                "raid and escort metadata shared an identity namespace");
        }

        private static void DestroyTransport(Context context, NpcShip transport)
        {
            transport.ApplyDamage(100_000f, NpcDestructionSource.Player);
            context.Loot.SpawnSalvageForDestroyedNpc(transport);
        }

        private Context AcceptedContext(MissionDifficulty difficulty = MissionDifficulty.Easy)
        {
            Context context = CreateContext();
            context.Mission = Offer(context, difficulty);
            if (!context.Manager.AcceptMission(context.Mission, context.Origin))
                throw new InvalidOperationException(context.Manager.LastAcceptanceFailureReason);
            return context;
        }

        private static Mission Offer(Context context, MissionDifficulty difficulty = MissionDifficulty.Easy) =>
            context.Manager.GenerateConvoyRaidMissions(context.Origin).First(mission => mission.Difficulty == difficulty);

        private static SaveGameManager NewSaveManager(string prefix) =>
            new SaveGameManager(Path.Combine(Path.GetTempPath(), $"{prefix}-{Guid.NewGuid():N}.json"));

        private static GameTime Frame(float seconds) => new(TimeSpan.Zero, TimeSpan.FromSeconds(seconds));

        private static (bool Success, string FailureReason) Result(bool success, string reason) =>
            success ? (true, string.Empty) : (false, reason);

        private static Context CreateContext()
        {
            Context context = new()
            {
                Credits = new PlayerCredits(10_000),
                Player = new Ship(new Vector3(0f, 5_000f, 0f)),
                Origin = CreateStation("Rogue Den", FactionManager.LibertyRogues, 0f),
                Destination = CreateStation("Newark Station", FactionManager.LibertyCorporations, 6_000f),
                Lane = CreateLane()
            };
            context.Stations.Add(context.Origin);
            context.Stations.Add(context.Destination);
            context.Lanes.Add(context.Lane);
            context.Reputation = new ReputationManager(new FactionManager());
            context.Reputation.SetReputation(FactionManager.LibertyRogues, 0f, "phase51 smoke setup");
            context.Reputation.SetReputation(FactionManager.LibertyCorporations, 0f, "phase51 smoke setup");
            context.Reputation.SetReputation(FactionManager.LibertyPolice, 0f, "phase51 smoke setup");
            context.Manager = new MissionManager(context.Credits, null, context.Reputation, null, context.Player.CargoHold);
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
                npc => context.World?.NotifyNpcDestroyed(npc),
                null,
                null,
                () => context.Lanes,
                null,
                null,
                npc => context.Traffic.RegisterMissionNpc(npc),
                npc => context.Tradelanes.EjectNpcFromTransit(npc));
            context.Loot = new LootManager(
                graphicsDevice: null,
                salvageService: new CombatSalvageService(),
                worldObjectsProvider: () => context.SpaceObjects);
            context.Loot.ConfigureMissionCargoCallbacks(
                npc => context.World.GetMissionCargoDrop(npc),
                pod => context.World.NotifyMissionCargoPodSpawned(pod),
                (pod, quantity) => context.World.NotifyMissionCargoPodCollected(pod, quantity),
                (pod, quantity) => context.World.NotifyMissionCargoPodExpired(pod, quantity),
                drop => context.World.NotifyMissionCargoDropUnavailable(drop));
            context.Traffic.MissionTargetResolver = context.World.GetConvoyCombatTarget;
            context.Manager.SetWaypointSystem(context.Waypoints);
            context.Manager.SetWorldManager(context.World);
            return context;
        }

        private static TradeLane CreateLane() => new(null, new TradelaneConfig
        {
            Id = "phase51_liberty_lane",
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
            public LootManager Loot { get; set; }
        }
    }
}
