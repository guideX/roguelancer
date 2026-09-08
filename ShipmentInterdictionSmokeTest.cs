using Microsoft.Xna.Framework;
using Roguelancer.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Roguelancer;

/// <summary>
/// Focused Phase 64 proof. Every target in this harness is the same live
/// EconomicShipmentManager trader that left real market stock; no synthetic
/// convoy or hidden mission inventory is used.
/// </summary>
internal sealed class ShipmentInterdictionSmokeTest
{
    private sealed class Context
    {
        public MarketManager Market { get; } = new();
        public Station Origin { get; }
        public Station Destination { get; }
        public Station Rogue { get; }
        public List<Station> Stations { get; }
        public EconomicShipmentManager Economy { get; }
        public TrafficZoneConfig Route { get; }
        public NpcShip Trader { get; }
        public List<NpcShip> Npcs { get; } = new();
        public List<SpaceObject> Objects { get; } = new();
        public Ship Player { get; }
        public PlayerCredits Credits { get; } = new(10_000);
        public ReputationManager Reputation { get; }
        public MissionManager Missions { get; }
        public MissionWorldManager World { get; }
        public TradeRouteRiskManager Risk { get; } = new();
        public MissionWaypointSystem Waypoints { get; } = new();
        public LootManager Loot { get; }
        public PirateCargoDemandService Demand { get; }

        public Context(string traderName = "Phase64 Interdiction Trader")
        {
            Origin = CreateStation("Fort Bush", FactionManager.LibertyCorporations, 1, new Vector3(6000f, 600f, -4500f));
            Destination = CreateStation("Newark Station", FactionManager.LibertyCorporations, 1, new Vector3(-7500f, -900f, 6000f));
            Rogue = CreateStation("Buffalo Base", FactionManager.LibertyRogues, 1, new Vector3(1200f, 600f, -1000f));
            Stations = new List<Station> { Origin, Destination, Rogue };
            Route = CreateRoute(Origin, Destination, "phase64-route");
            Trader = CreateTrader(traderName, Origin.Position, Route);
            Trader.Position = Origin.Position + new Vector3(1000f, 0f, 0f);
            Npcs.Add(Trader);
            Objects.Add(Trader);
            Player = new Ship(Trader.Position + new Vector3(250f, 0f, 0f));
            Reputation = new ReputationManager(new FactionManager());
            Economy = new EconomicShipmentManager(Market, () => Stations, _ => false, () => new[] { Route });
            Missions = new MissionManager(Credits, null, Reputation, Market, Player.CargoHold);
            World = new MissionWorldManager(
                Missions,
                Waypoints,
                Player,
                Npcs,
                Objects,
                () => Stations,
                marketManager: Market);
            World.SetEconomicShipmentManager(Economy);
            Economy.ConfigureRiskManager(Risk);
            Missions.SetWaypointSystem(Waypoints);
            Missions.SetWorldManager(World);
            Missions.SetEconomicShipmentManager(Economy);
            Loot = new LootManager(
                salvageService: new CombatSalvageService(),
                worldObjectsProvider: () => Objects);
            Loot.ConfigureMissionCargoCallbacks(
                _ => null,
                pod => World.NotifyMissionCargoPodSpawned(pod),
                (pod, quantity) => World.NotifyMissionCargoPodCollected(pod, quantity),
                (pod, quantity) => World.NotifyMissionCargoPodExpired(pod, quantity),
                drop => World.NotifyMissionCargoDropUnavailable(drop));
            Loot.ConfigureEconomicCargoCallbacks(
                trader => Economy.GetDestructionSalvage(trader),
                (trader, commodityId, quantity) => Economy.ConsumeDestructionSalvage(trader, commodityId, quantity),
                (trader, commodityId, quantity) => World.GetEconomicMissionCargoDrop(trader, commodityId, quantity),
                drop => World.NotifyMissionCargoDropUnavailable(drop));
            Demand = new PirateCargoDemandService(
                Npcs,
                Reputation,
                isMissionOwned: _ => true,
                spawnCargo: (trader, commodityId, quantity) =>
                    Loot.SpawnExtortionCargo(trader, commodityId, quantity, out _, null),
                manifestResolver: trader => Economy.GetManifest(trader),
                isAuthorizedMissionTarget: World.IsPiracyDemandAllowed,
                isMissionDemandCommodity: World.IsInterdictionDemandCommodity);
            Demand.DemandResolved += result => Economy.RecordPiracyDemandResult(result);
        }

        public EconomicShipment AttachShipment()
        {
            if (!Economy.TryAttachTrader(Trader, Route, out EconomicShipment shipment))
                throw new InvalidOperationException("real economic shipment could not be attached");
            return shipment;
        }

        public Mission AcceptOffer(bool makeEasyRequirementOne = false)
        {
            EconomicShipment shipment = Economy.TryGetShipment(Trader, out EconomicShipment active)
                ? active
                : AttachShipment();
            ForceCriticalShortage(shipment);
            Mission offer = Missions.GenerateEconomicInterdictionMissions(Rogue).FirstOrDefault();
            if (offer == null)
                throw new InvalidOperationException("Rogue board did not identify the real shipment");
            if (makeEasyRequirementOne)
                offer.RequiredQuantity = Math.Min(1, shipment.Manifest.Stacks.First(stack =>
                    string.Equals(stack.Commodity.Id, offer.CommodityId, StringComparison.OrdinalIgnoreCase)).Quantity);
            if (!Missions.AcceptMission(offer, Rogue))
                throw new InvalidOperationException(Missions.LastAcceptanceFailureReason);
            return offer;
        }

        internal void ForceCriticalShortage(EconomicShipment shipment)
        {
            TraderCargoStack stack = shipment.Manifest.Stacks.FirstOrDefault(candidate => candidate?.Commodity != null);
            StationMarketListing listing = stack == null ? null : Market.GetListingForCommodity(Destination, stack.Commodity);
            if (listing == null)
                return;

            int remove = Math.Max(0, listing.Stock - Math.Max(0, listing.BaselineStock / 4));
            if (remove > 0)
                Market.TryRemoveSupply(Destination, listing.Commodity, remove, 0, out _);
        }

        private static Station CreateStation(string name, string faction, int systemIndex, Vector3 position) =>
            new(new StationConfig
            {
                Description = name,
                FactionId = faction,
                SystemIndex = systemIndex,
                StartupPositionX = position.X,
                StartupPositionY = position.Y,
                StartupPositionZ = position.Z
            }, null);

        private static TrafficZoneConfig CreateRoute(Station origin, Station destination, string id) => new()
        {
            Id = id,
            Name = "Phase 64 route",
            BehaviorType = TrafficZoneBehaviorType.TraderRoute,
            SystemIndex = origin.Config?.SystemIndex ?? 1,
            OriginStationId = "fort_bush",
            DestinationStationId = "newark_station",
            RouteStartX = origin.Position.X,
            RouteStartY = origin.Position.Y,
            RouteStartZ = origin.Position.Z,
            RouteEndX = destination.Position.X,
            RouteEndY = destination.Position.Y,
            RouteEndZ = destination.Position.Z
        };

        private static NpcShip CreateTrader(string name, Vector3 position, TrafficZoneConfig route)
        {
            NpcShip trader = new(name, position, Vector3.Zero, 1000f, 0.1f, FactionManager.LibertyCorporations);
            trader.ConfigureTrafficBehavior(
                TrafficZoneBehaviorType.TraderRoute,
                route.Id,
                Vector3.Zero,
                1000f,
                190f,
                3500f,
                route.RouteStart,
                route.RouteEnd);
            return trader;
        }
    }

    public (int Passed, int Failed) Run()
    {
        int passed = 0;
        int failed = 0;
        Check("Rogue intelligence is bounded and deterministic", BoundedCandidateIntelligence, ref passed, ref failed);
        Check("lawful board does not generate Rogue interdiction", LawfulBoardIsEmpty, ref passed, ref failed);
        Check("offer binds the real shipment and no second debit", OfferBindsRealShipment, ref passed, ref failed);
        Check("acceptance locks opposing escort ownership", OpposingEscortIsLocked, ref passed, ref failed);
        Check("nonlethal extortion creates stolen physical mission cargo", NonlethalExtortionIsPhysical, ref passed, ref failed);
        Check("surviving target delivers remainder and raises route risk", NonlethalDeliverySettlesRemainder, ref passed, ref failed);
        Check("pickup and Rogue turn-in consume exact cargo once", PickupAndTurnInAreExact, ref passed, ref failed);
        Check("destruction exposes bounded real salvage only", DestructionUsesRealSalvage, ref passed, ref failed);
        Check("accepted target escape fails without clawing back delivery", TargetEscapeFailsWithoutClawback, ref passed, ref failed);
        Check("stale offer rejects after target delivery", StaleOfferRejects, ref passed, ref failed);
        Check("dynamic mission fields survive save reconstruction", SaveFieldsRoundTrip, ref passed, ref failed);
        Check("destroyed target and physical cargo survive save/load", SaveAfterDestructionResumesRecovery, ref passed, ref failed);
        Console.WriteLine($"[PHASE 64 SHIPMENT INTERDICTION SMOKE] RESULT: {passed} passed, {failed} failed");
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
                Console.WriteLine($"[PHASE 64 SHIPMENT INTERDICTION SMOKE] PASS {label}");
            }
            else
            {
                failed++;
                Console.WriteLine($"[PHASE 64 SHIPMENT INTERDICTION SMOKE] FAIL {label}: {reason}");
            }
        }
        catch (Exception ex)
        {
            failed++;
            Console.WriteLine($"[PHASE 64 SHIPMENT INTERDICTION SMOKE] FAIL {label}: {ex.Message}");
        }
    }

    private (bool Success, string FailureReason) BoundedCandidateIntelligence()
    {
        Context context = new("Phase64 Bounded Trader");
        EconomicShipment shipment = context.AttachShipment();
        context.ForceCriticalShortage(shipment);
        IReadOnlyList<EconomicShipment> first = context.Economy.GetInterdictionCandidates(context.Rogue, 99);
        IReadOnlyList<EconomicShipment> second = context.Economy.GetInterdictionCandidates(context.Rogue, 99);
        Station remote = CreateRemoteStation("New Rochester", 2);
        context.Stations.Add(remote);
        return first.Count <= EconomicShipmentManager.MaximumDynamicInterdictionCandidates &&
            first.Select(candidate => candidate.TraderIdentity).SequenceEqual(second.Select(candidate => candidate.TraderIdentity)) &&
            first.Contains(shipment) &&
            !context.Economy.IsInterdictionInScope(remote, shipment)
            ? Pass()
            : Fail($"candidate count={first.Count}, target={first.Contains(shipment)}, remoteScope={context.Economy.IsInterdictionInScope(remote, shipment)}");
    }

    private (bool Success, string FailureReason) LawfulBoardIsEmpty()
    {
        Context context = new();
        context.AttachShipment();
        List<Mission> offers = context.Missions.GenerateEconomicInterdictionMissions(context.Origin);
        return offers.Count == 0 ? Pass() : Fail("commercial origin received a Rogue interdiction offer");
    }

    private (bool Success, string FailureReason) OfferBindsRealShipment()
    {
        Context context = new();
        EconomicShipment shipment = context.AttachShipment();
        int initial = shipment.RemainingQuantity;
        Mission mission = context.AcceptOffer();
        bool acceptedTarget = mission.TargetSpaceObject == shipment.Trader &&
            mission.EconomicShipmentTraderIdentity == shipment.TraderIdentity &&
            context.Economy.GetRemainingCommodityQuantity(shipment, mission.CommodityId) >= mission.RequiredQuantity;
        return acceptedTarget && shipment.RemainingQuantity == initial &&
            shipment.InterdictionMissionId == mission.Id &&
            context.Missions.ActiveMission == mission
            ? Pass()
            : Fail("accepted mission did not retain the live shipment identity/manifest");
    }

    private (bool Success, string FailureReason) OpposingEscortIsLocked()
    {
        Context context = new();
        EconomicShipment shipment = context.AttachShipment();
        Mission mission = context.AcceptOffer();
        bool opposingAttach = context.Economy.TryAttachEconomicEscort(9001, shipment.TraderIdentity);
        bool candidateExcluded = !context.Economy.GetEscortCandidates(context.Origin).Contains(shipment);
        return mission.Id > 0 && !opposingAttach && candidateExcluded && shipment.InterdictionMissionId == mission.Id
            ? Pass()
            : Fail($"escort ownership was not excluded after interdiction acceptance (mission={mission.Id}, lock={shipment.InterdictionMissionId}, opposingAttach={opposingAttach}, candidateExcluded={candidateExcluded})");
    }

    private (bool Success, string FailureReason) NonlethalExtortionIsPhysical()
    {
        Context context = new();
        EconomicShipment shipment = context.AttachShipment();
        int initialQuantity = shipment.RemainingQuantity;
        Mission mission = context.AcceptOffer();
        context.Trader.MarkDamagedByPlayer(4f);
        context.Trader.ApplyDamage(4f, NpcDestructionSource.Player);
        if (!context.Demand.TryIssueDemand(context.Player, context.Trader, out string demandFailure))
            return Fail($"authorized Phase 56 demand was rejected: {demandFailure}");
        context.Demand.Update(PirateCargoDemandService.ResponseWindowSeconds + 0.1f, context.Player);
        PiracyDemandResult result = context.Demand.LastResult;
        CargoPod pod = context.Loot.ActivePods.FirstOrDefault(candidate => candidate.IsMissionCargo);
        return result?.State == PiracyDemandState.Complying &&
            result.SurrenderedQuantity > 0 && pod != null && pod.IsStolen &&
            pod.MissionId == mission.Id &&
            shipment.RemainingQuantity < initialQuantity &&
            mission.InterdictionReleasedQuantity >= result.SurrenderedQuantity
            ? Pass()
            : Fail($"extortion did not create attributed stolen cargo (state={result?.State}, surrendered={result?.SurrenderedQuantity}, pod={pod?.Quantity})");
    }

    private (bool Success, string FailureReason) PickupAndTurnInAreExact()
    {
        Context context = new();
        Mission mission = context.AcceptOffer(makeEasyRequirementOne: true);
        context.Trader.MarkDamagedByPlayer(4f);
        context.Trader.ApplyDamage(4f, NpcDestructionSource.Player);
        if (!context.Demand.TryIssueDemand(context.Player, context.Trader, out _))
            return Fail("demand could not be issued");
        context.Demand.Update(PirateCargoDemandService.ResponseWindowSeconds + 0.1f, context.Player);
        CargoPod pod = context.Loot.ActivePods.FirstOrDefault(candidate => candidate.MissionId == mission.Id);
        if (pod == null)
            return Fail("no physical attributed pod existed after compliance");
        pod.Velocity = Vector3.Zero;
        pod.Position = context.Player.Position;
        context.Loot.Update(new GameTime(TimeSpan.Zero, TimeSpan.FromSeconds(0.1)), context.Player, false);
        context.World.Update(0.1f, null, 1);
        int held = context.Player.CargoHold.GetMissionCargoQuantity(mission.Id);
        int beforeCredits = context.Credits.Credits;
        bool claimed = context.Missions.TryClaimReward(mission, context.Rogue, out _);
        bool claimedAgain = context.Missions.TryClaimReward(mission, context.Rogue, out _);
        return mission.Status == MissionStatus.Rewarded && held >= mission.RequiredQuantity && claimed && !claimedAgain &&
            context.Credits.Credits - beforeCredits == mission.Reward &&
            context.Player.CargoHold.GetMissionCargoQuantity(mission.Id) == 0
            ? Pass()
            : Fail($"pickup/turn-in mismatch (status={mission.Status}, held={held}, claimed={claimed}, repeat={claimedAgain})");
    }

    private (bool Success, string FailureReason) NonlethalDeliverySettlesRemainder()
    {
        Context context = new();
        Mission mission = context.AcceptOffer(makeEasyRequirementOne: true);
        EconomicShipment shipment = context.Economy.ActiveShipments.Single();
        Commodity commodity = CommodityCatalog.GetById(mission.CommodityId);
        int destinationBefore = context.Market.GetListingForCommodity(context.Destination, commodity).Stock;
        context.Trader.MarkDamagedByPlayer(4f);
        context.Trader.ApplyDamage(4f, NpcDestructionSource.Player);
        if (!context.Demand.TryIssueDemand(context.Player, context.Trader, out string demandFailure))
            return Fail($"authorized demand was rejected: {demandFailure}");
        context.Demand.Update(PirateCargoDemandService.ResponseWindowSeconds + 0.1f, context.Player);
        CargoPod pod = context.Loot.ActivePods.FirstOrDefault(candidate => candidate.MissionId == mission.Id);
        if (pod == null)
            return Fail("nonlethal demand did not release physical mission cargo");
        pod.Velocity = Vector3.Zero;
        pod.Position = context.Player.Position;
        context.Loot.Update(new GameTime(TimeSpan.Zero, TimeSpan.FromSeconds(0.1)), context.Player, false);
        int remainder = context.Economy.GetRemainingCommodityQuantity(shipment, mission.CommodityId);
        if (!context.Economy.TryDeliver(context.Trader) || remainder <= 0)
            return Fail("surviving target did not settle its remaining real manifest");
        int destinationAfter = context.Market.GetListingForCommodity(context.Destination, commodity).Stock;
        context.World.Update(0.1f, null, 1);
        bool claimed = context.Missions.TryClaimReward(mission, context.Rogue, out _);
        return destinationAfter - destinationBefore == remainder &&
            context.Risk.GetRisk(context.Route.Id, TradeLaneDirection.Forward) >= TradeRouteRiskManager.CargoExtortedRiskPoints &&
            claimed && mission.Status == MissionStatus.Rewarded
            ? Pass()
            : Fail($"remainder/risk settlement mismatch (remainder={remainder}, destinationDelta={destinationAfter - destinationBefore}, risk={context.Risk.GetRisk(context.Route.Id, TradeLaneDirection.Forward)}, claimed={claimed})");
    }

    private (bool Success, string FailureReason) DestructionUsesRealSalvage()
    {
        Context context = new();
        Mission mission = context.AcceptOffer(makeEasyRequirementOne: true);
        EconomicShipment shipment = context.Economy.ActiveShipments.Single();
        int destinationBefore = context.Market.GetListingForCommodity(
            context.Destination,
            CommodityCatalog.GetById(mission.CommodityId)).Stock;
        context.Trader.ApplyDamage(100_000f, NpcDestructionSource.Player);
        context.World.NotifyNpcDestroyed(context.Trader);
        context.Economy.NotifyTraderDestroyed(context.Trader);
        int spawned = context.Loot.SpawnLootForDestroyedNpc(context.Trader);
        context.Economy.FinalizeDestroyedTrader(context.Trader);
        CargoPod missionPod = context.Loot.ActivePods.FirstOrDefault(candidate => candidate.MissionId == mission.Id);
        int destinationAfter = context.Market.GetListingForCommodity(
            context.Destination,
            CommodityCatalog.GetById(mission.CommodityId)).Stock;
        return spawned > 0 && missionPod != null && missionPod.IsStolen &&
            !context.Economy.TryGetShipment(context.Trader, out _) &&
            destinationAfter == destinationBefore &&
            context.Loot.ActivePods.Count(candidate => candidate.MissionId == mission.Id) == 1
            ? Pass()
            : Fail($"destruction salvage was not authoritative (spawned={spawned}, missionPods={context.Loot.ActivePods.Count(candidate => candidate.MissionId == mission.Id)}, destinationDelta={destinationAfter - destinationBefore})");
    }

    private (bool Success, string FailureReason) TargetEscapeFailsWithoutClawback()
    {
        Context context = new();
        Mission mission = context.AcceptOffer(makeEasyRequirementOne: true);
        Commodity commodity = CommodityCatalog.GetById(mission.CommodityId);
        int destinationBefore = context.Market.GetListingForCommodity(context.Destination, commodity).Stock;
        int creditsBefore = context.Credits.Credits;
        if (!context.Economy.TryDeliver(context.Trader))
            return Fail("accepted target could not reach its destination");
        int destinationAfter = context.Market.GetListingForCommodity(context.Destination, commodity).Stock;
        context.World.Update(0.1f, null, 1);
        return mission.Status == MissionStatus.Failed && context.Missions.ActiveMission == null &&
            destinationAfter > destinationBefore && context.Credits.Credits == creditsBefore
            ? Pass()
            : Fail($"target escape did not fail cleanly (status={mission.Status}, destinationDelta={destinationAfter - destinationBefore}, creditsDelta={context.Credits.Credits - creditsBefore})");
    }

    private (bool Success, string FailureReason) StaleOfferRejects()
    {
        Context context = new();
        EconomicShipment shipment = context.AttachShipment();
        context.ForceCriticalShortage(shipment);
        Mission offer = context.Missions.GenerateEconomicInterdictionMissions(context.Rogue).FirstOrDefault();
        if (offer == null)
            return Fail("no offer existed for stale-target check");
        context.Economy.NotifyRouteEndpointReached(context.Trader, true);
        bool accepted = context.Missions.AcceptMission(offer, context.Rogue);
        return !accepted && context.Missions.ActiveMission == null ? Pass() : Fail("delivered target offer remained acceptable");
    }

    private (bool Success, string FailureReason) SaveFieldsRoundTrip()
    {
        Context context = new();
        Mission mission = context.AcceptOffer(makeEasyRequirementOne: true);
        mission.InterdictionReleasedQuantity = 2;
        mission.InterdictionCargoRecoveredQuantity = 1;
        mission.InterdictionRemainingPossibleQuantity = 4;
        SaveGameManager save = new(System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"phase64-{Guid.NewGuid():N}.json"));
        SaveMissionData data = save.CaptureMissions(context.Missions.ActiveMissions).Single();
        Mission restored = save.BuildMissionList(new[] { data }, out _).Single();
        return restored.IsShipmentInterdictionMission() &&
            restored.EconomicShipmentTraderIdentity == mission.EconomicShipmentTraderIdentity &&
            restored.CommodityId == mission.CommodityId &&
            restored.InterdictionReleasedQuantity == 2 &&
            restored.InterdictionCargoRecoveredQuantity == 1 &&
            restored.InterdictionRemainingPossibleQuantity == 4
            ? Pass()
            : Fail("dynamic mission fields were not serialized/reconstructed");
    }

    private (bool Success, string FailureReason) SaveAfterDestructionResumesRecovery()
    {
        Context context = new();
        Mission mission = context.AcceptOffer(makeEasyRequirementOne: true);
        context.Trader.ApplyDamage(100_000f, NpcDestructionSource.Player);
        context.World.NotifyNpcDestroyed(context.Trader);
        context.Economy.NotifyTraderDestroyed(context.Trader);
        context.Loot.SpawnLootForDestroyedNpc(context.Trader);
        context.Economy.FinalizeDestroyedTrader(context.Trader);

        SaveGameManager save = new(System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"phase64-load-{Guid.NewGuid():N}.json"));
        SaveMissionData missionData = save.CaptureMissions(context.Missions.ActiveMissions).Single();
        List<SaveCargoPodData> podData = context.Loot.CaptureCargoPods()
            .Where(pod => pod.MissionId == mission.Id)
            .ToList();
        if (podData.Count == 0)
            return Fail("destroyed shipment did not produce a saveable attributed pod");

        // Put the saved pod at the player for a deterministic post-load pickup;
        // the cargo identity, quantity, stolen provenance, and mission source
        // remain exactly the values emitted by the real destruction path.
        foreach (SaveCargoPodData pod in podData)
        {
            pod.Position = SaveVector3Data.From(context.Player.Position);
            pod.Velocity = SaveVector3Data.From(Vector3.Zero);
        }
        Mission restored = save.BuildMissionList(new[] { missionData }, out _).Single();
        context.Missions.RestoreState(new[] { restored }, null);
        context.World.RebindActiveMissions(context.Missions.ActiveMissions);
        context.Loot.Reset();
        int restoredPods = context.Loot.RestoreCargoPods(
            podData,
            id => context.Missions.ActiveMissions.Any(active => active.Id == id));
        CargoPod restoredPod = context.Loot.ActivePods.FirstOrDefault(pod => pod.MissionId == restored.Id);
        if (restoredPods != podData.Count || restoredPod == null ||
            context.Loot.ActivePods.Any(pod => pod.MissionId == restored.Id && !pod.IsStolen) ||
            restored.TargetSpaceObject != null)
            return Fail($"post-destruction save/load lost physical attribution (saved={podData.Count}, restored={restoredPods}, stolen={restoredPod?.IsStolen}, target={restored.TargetSpaceObject != null})");

        context.Loot.Update(new GameTime(TimeSpan.Zero, TimeSpan.FromSeconds(0.1)), context.Player, false);
        context.World.Update(0.1f, null, 1);
        return restored.Status == MissionStatus.Completed &&
            context.Player.CargoHold.GetMissionCargoQuantity(restored.Id) >= restored.RequiredQuantity
            ? Pass()
            : Fail($"post-load recovery did not complete (status={restored.Status}, held={context.Player.CargoHold.GetMissionCargoQuantity(restored.Id)})");
    }

    private static Station CreateRemoteStation(string name, int systemIndex) =>
        new(new StationConfig { Description = name, SystemIndex = systemIndex }, null);

    private static (bool Success, string FailureReason) Pass() => (true, string.Empty);
    private static (bool Success, string FailureReason) Fail(string reason) => (false, reason);
}
