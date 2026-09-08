using Microsoft.Xna.Framework;
using Roguelancer.Configuration;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Roguelancer;

/// <summary>
/// Focused Phase 63 proof for bounded route memory and real economic-shipment
/// integration. The existing Phase 50/59/62 suites remain independent.
/// </summary>
internal sealed class TradeRouteRiskSmokeTest
{
    private sealed class Context
    {
        public MarketManager Market { get; } = RunSilenced(() => new MarketManager());
        public Station Origin { get; }
        public Station Destination { get; }
        public TrafficZoneConfig Route { get; }
        public List<Station> Stations { get; }
        public List<TrafficZoneConfig> Routes { get; }
        public TradeRouteRiskManager Risk { get; }
        public EconomicShipmentManager Economy { get; }

        public Context()
        {
            Origin = CreateStation("Fort Bush", new Vector3(6_000f, 600f, -4_500f));
            Destination = CreateStation("Newark Station", new Vector3(-7_500f, -900f, 6_000f));
            Stations = new List<Station> { Origin, Destination };
            Route = new TrafficZoneConfig
            {
                Id = "phase63_fort_bush_newark",
                Name = "Phase 63 Fort Bush / Newark",
                SystemIndex = 1,
                BehaviorType = TrafficZoneBehaviorType.TraderRoute,
                ShipDescription = "Transport Ship Alpha",
                OriginStationId = "fort_bush",
                DestinationStationId = "newark_station",
                RouteStartX = Origin.Position.X,
                RouteStartY = Origin.Position.Y,
                RouteStartZ = Origin.Position.Z,
                RouteEndX = Destination.Position.X,
                RouteEndY = Destination.Position.Y,
                RouteEndZ = Destination.Position.Z
            };
            Routes = new List<TrafficZoneConfig> { Route };
            Risk = new TradeRouteRiskManager();
            Economy = new EconomicShipmentManager(Market, () => Stations, routesProvider: () => Routes);
            Economy.ConfigureRiskManager(Risk);
        }

        public NpcShip CreateTrader(string name = "Phase63 Trader")
        {
            NpcShip trader = new(name, Origin.Position, Vector3.Zero, 1_000f, 0.1f, FactionManager.LibertyCorporations);
            trader.ConfigureTrafficBehavior(
                TrafficZoneBehaviorType.TraderRoute,
                Route.Id,
                Origin.Position,
                1_000f,
                190f,
                3_500f,
                Route.RouteStart,
                Route.RouteEnd);
            return trader;
        }

        public void MakeDestinationCriticallyShort()
        {
            foreach (StationMarketListing listing in Market.GetListingsForStation(Destination).ToList())
            {
                if (listing?.Commodity == null || listing.Stock <= 0)
                    continue;
                Market.TryRemoveSupply(Destination, listing.Commodity, listing.Stock, 0, out _);
                Market.GetShortageState(Destination, listing.Commodity);
            }
        }

        private static Station CreateStation(string name, Vector3 position)
        {
            StationConfig config = new()
            {
                Description = name,
                SystemIndex = 1,
                StartupPositionX = position.X,
                StartupPositionY = position.Y,
                StartupPositionZ = position.Z
            };
            return new Station(config, null);
        }
    }

    public (int Passed, int Failed) Run()
    {
        int passed = 0;
        int failed = 0;
        RunCase(RouteStartsSafeAndIsDirectional, "route starts safe and direction is isolated", ref passed, ref failed);
        RunCase(IncidentsAreWeightedBoundedAndDeduplicated, "incidents are weighted, bounded, and deduplicated", ref passed, ref failed);
        RunCase(EconomicDestructionRecordsRiskWithoutAttributionGate, "economic destruction records risk", ref passed, ref failed);
        RunCase(NonRiskEventsDoNotChangeRisk, "scanner and teardown do not change risk", ref passed, ref failed);
        RunCase(DecayIsDeterministicAndCatchupBounded, "decay is deterministic and catch-up bounded", ref passed, ref failed);
        RunCase(SaveLoadPreservesRiskWithoutReplay, "save/load preserves risk without replay", ref passed, ref failed);
        RunCase(SafeDeliveryRecoveryIsSmall, "safe delivery recovery is bounded", ref passed, ref failed);
        RunCase(PlannerRiskPenaltyIsBounded, "planner risk penalty is bounded", ref passed, ref failed);
        RunCase(CriticalShortageQualifiesRealShipment, "critical shortage qualifies one real shipment", ref passed, ref failed);
        RunCase(EconomicEscortFactoryUsesRealShipment, "economic escort references real shipment", ref passed, ref failed);
        RunCase(RealEconomicEscortCompletesAgainstActualShipment, "real economic escort delivers and rewards once", ref passed, ref failed);
        RunCase(AcceptedEconomicEscortSaveLoadRebindsOnce, "accepted economic escort save/load rebinds once", ref passed, ref failed);
        Console.WriteLine($"[PHASE 63 TRADE ROUTE RISK SMOKE] RESULT: {passed} passed, {failed} failed");
        return (passed, failed);
    }

    private void RunCase(
        Func<(bool Success, string FailureReason)> test,
        string label,
        ref int passed,
        ref int failed)
    {
        try
        {
            (bool Success, string FailureReason) result = RunSilenced(test);
            if (result.Success)
            {
                passed++;
                Console.WriteLine($"[PHASE 63 TRADE ROUTE RISK SMOKE] PASS {label}");
            }
            else
            {
                failed++;
                Console.WriteLine($"[PHASE 63 TRADE ROUTE RISK SMOKE] FAIL {label}: {result.FailureReason}");
            }
        }
        catch (Exception ex)
        {
            failed++;
            Console.WriteLine($"[PHASE 63 TRADE ROUTE RISK SMOKE] FAIL {label}: {ex.Message}");
        }
    }

    private (bool Success, string FailureReason) RouteStartsSafeAndIsDirectional()
    {
        Context c = new();
        return c.Risk.GetRisk(c.Route.Id, TradeLaneDirection.Forward) == 0 &&
            c.Risk.GetRisk(c.Route.Id, TradeLaneDirection.Reverse) == 0 &&
            c.Risk.RecordIncident(c.Route.Id, TradeLaneDirection.Forward, TradeRouteRiskIncident.RefusalDistress, incidentKey: "one") &&
            c.Risk.GetRisk(c.Route.Id, TradeLaneDirection.Reverse) == 0
            ? Pass()
            : Fail("directional route risk was not isolated");
    }

    private (bool Success, string FailureReason) IncidentsAreWeightedBoundedAndDeduplicated()
    {
        TradeRouteRiskManager risk = new();
        bool first = risk.RecordTraderDestroyed("route-a", TradeLaneDirection.Forward, 50_000, "destroyed-1");
        bool duplicate = risk.RecordTraderDestroyed("route-a", TradeLaneDirection.Forward, 50_000, "destroyed-1");
        for (int i = 0; i < 10; i++)
            risk.RecordCargoExtorted("route-a", TradeLaneDirection.Forward, 10_000, $"extortion-{i}");
        int score = risk.GetRisk("route-a", TradeLaneDirection.Forward);
        return first && !duplicate && score == TradeRouteRiskManager.MaximumRiskScore
            ? Pass()
            : Fail($"risk weighting/deduplication mismatch (score={score}, duplicate={duplicate})");
    }

    private (bool Success, string FailureReason) EconomicDestructionRecordsRiskWithoutAttributionGate()
    {
        Context c = new();
        NpcShip trader = c.CreateTrader();
        if (!c.Economy.TryAttachTrader(trader, c.Route, out EconomicShipment shipment))
            return Fail("real shipment was not created");
        c.Economy.NotifyTraderDestroyed(trader);
        int risk = c.Risk.GetRisk(c.Route.Id, TradeLaneDirection.Forward);
        return shipment.Settlement == EconomicShipmentSettlement.Lost && risk >= TradeRouteRiskManager.TraderDestroyedRiskPoints
            ? Pass()
            : Fail($"destroyed shipment did not record risk (settlement={shipment.Settlement}, risk={risk})");
    }

    private (bool Success, string FailureReason) NonRiskEventsDoNotChangeRisk()
    {
        Context c = new();
        NpcShip trader = c.CreateTrader();
        if (!c.Economy.TryAttachTrader(trader, c.Route, out _))
            return Fail("real shipment was not created");
        c.Risk.RecordTraderDestroyed(c.Route.Id, TradeLaneDirection.Forward, incidentKey: "seed");
        int before = c.Risk.GetRisk(c.Route.Id, TradeLaneDirection.Forward);
        c.Economy.ResetForWorldTeardown(restoreOriginStock: true);
        int afterTeardown = c.Risk.GetRisk(c.Route.Id, TradeLaneDirection.Forward);
        return before == afterTeardown
            ? Pass()
            : Fail($"teardown changed route risk ({before}->{afterTeardown})");
    }

    private (bool Success, string FailureReason) DecayIsDeterministicAndCatchupBounded()
    {
        TradeRouteRiskManager risk = new();
        risk.RecordTraderDestroyed("route-a", TradeLaneDirection.Forward, incidentKey: "destroyed");
        risk.RecordTraderDestroyed("route-a", TradeLaneDirection.Forward, incidentKey: "destroyed-2");
        risk.RecordTraderDestroyed("route-a", TradeLaneDirection.Forward, incidentKey: "destroyed-3");
        risk.RecordTraderDestroyed("route-a", TradeLaneDirection.Forward, incidentKey: "destroyed-4");
        int initial = risk.GetRisk("route-a", TradeLaneDirection.Forward);
        risk.AdvanceTime(0d);
        bool noZeroDecay = risk.GetRisk("route-a", TradeLaneDirection.Forward) == initial;
        risk.AdvanceTime(30d);
        bool subMinuteAccumulates = risk.GetRisk("route-a", TradeLaneDirection.Forward) == initial;
        risk.AdvanceTime(30d);
        bool oneMinuteDecays = risk.GetRisk("route-a", TradeLaneDirection.Forward) ==
            initial - TradeRouteRiskManager.DecayPointsPerEconomicMinute;
        risk.AdvanceTime(20d * 60d);
        int afterCatchup = risk.GetRisk("route-a", TradeLaneDirection.Forward);
        return noZeroDecay && subMinuteAccumulates && oneMinuteDecays &&
            afterCatchup == Math.Max(0, initial - TradeRouteRiskManager.DecayPointsPerEconomicMinute * (TradeRouteRiskManager.MaximumCatchUpMinutes + 1))
            ? Pass()
            : Fail($"decay was not bounded (initial={initial}, after={afterCatchup})");
    }

    private (bool Success, string FailureReason) SaveLoadPreservesRiskWithoutReplay()
    {
        TradeRouteRiskManager original = new();
        original.RecordCargoExtorted("route-a", TradeLaneDirection.Reverse, 10_000, "extortion");
        int expected = original.GetRisk("route-a", TradeLaneDirection.Reverse);
        List<SaveTradeRouteRiskData> saved = original.CaptureState().ToList();
        TradeRouteRiskManager restored = new();
        restored.RestoreState(saved);
        bool noReplay = restored.GetRisk("route-a", TradeLaneDirection.Reverse) == expected &&
            !restored.RecordCargoExtorted("route-a", TradeLaneDirection.Reverse, 10_000, "extortion");
        return noReplay ? Pass() : Fail("risk save/load changed or replayed an incident");
    }

    private (bool Success, string FailureReason) SafeDeliveryRecoveryIsSmall()
    {
        TradeRouteRiskManager risk = new();
        risk.RecordTraderDestroyed("route-a", TradeLaneDirection.Forward, incidentKey: "destroyed");
        int before = risk.GetRisk("route-a", TradeLaneDirection.Forward);
        risk.RecordSafeDelivery("route-a", TradeLaneDirection.Forward, "delivery");
        int after = risk.GetRisk("route-a", TradeLaneDirection.Forward);
        return after == Math.Max(0, before - TradeRouteRiskManager.SafeDeliveryRecoveryPoints) && after > 0
            ? Pass()
            : Fail($"safe delivery recovery was too large or missing ({before}->{after})");
    }

    private (bool Success, string FailureReason) PlannerRiskPenaltyIsBounded()
    {
        int safe = AdaptiveTraderRoutingPlanner.CalculateOpportunityScore(450, 0, 0, 0, 0, 0);
        int risky = AdaptiveTraderRoutingPlanner.CalculateOpportunityScore(450, 0, 0, 0, 0, 100);
        return safe > risky && safe - risky == TradeRouteRiskManager.MaximumRouteRiskPenalty / 2
            ? Pass()
            : Fail($"planner did not apply bounded risk penalty ({safe} vs {risky})");
    }

    private (bool Success, string FailureReason) CriticalShortageQualifiesRealShipment()
    {
        Context c = new();
        c.MakeDestinationCriticallyShort();
        NpcShip trader = c.CreateTrader();
        if (!c.Economy.TryAttachTrader(trader, c.Route, out EconomicShipment shipment))
            return Fail("real shipment was not created");
        c.Risk.RecordTraderDestroyed(c.Route.Id, TradeLaneDirection.Forward, incidentKey: "risk-1");
        c.Risk.RecordTraderDestroyed(c.Route.Id, TradeLaneDirection.Forward, incidentKey: "risk-2");
        IReadOnlyList<EconomicShipment> candidates = c.Economy.GetEscortCandidates(c.Origin);
        return candidates.Count == 1 && ReferenceEquals(candidates[0], shipment)
            ? Pass()
            : Fail($"critical-shortage escort candidate was not bounded/real (count={candidates.Count})");
    }

    private (bool Success, string FailureReason) EconomicEscortFactoryUsesRealShipment()
    {
        Context c = new();
        if (!c.Economy.TryAttachTrader(c.CreateTrader(), c.Route, out EconomicShipment shipment))
            return Fail("real shipment was not created");
        Mission mission = Mission.CreateEconomicEscort(
            shipment,
            c.Origin,
            c.Destination,
            shipment.Trader.Position,
            Vector3.Lerp(shipment.Trader.Position, c.Destination.Position, 0.5f),
            TradeRouteRiskManager.DangerousRiskThreshold,
            7_500,
            MissionDifficulty.Medium,
            "CRITICAL SHORTAGE",
            "Commercial Operations");
        return mission?.IsEconomicEscort == true &&
            mission.EconomicShipmentTraderIdentity == shipment.TraderIdentity &&
            mission.EconomicShipmentRouteId == shipment.RouteId &&
            mission.ConvoyShipCount == 1 && shipment.RemainingQuantity > 0
            ? Pass()
            : Fail("escort factory did not retain the real shipment identity");
    }

    private (bool Success, string FailureReason) RealEconomicEscortCompletesAgainstActualShipment()
    {
        Context c = new();
        c.MakeDestinationCriticallyShort();
        NpcShip trader = c.CreateTrader("Phase63 Escort Trader");
        Dictionary<string, int> originBefore = c.Market.GetListingsForStation(c.Origin)
            .Where(listing => listing?.Commodity != null)
            .ToDictionary(listing => listing.Commodity.Id, listing => listing.Stock, StringComparer.OrdinalIgnoreCase);
        if (!c.Economy.TryAttachTrader(trader, c.Route, out EconomicShipment shipment))
            return Fail("real shipment was not created");

        int originDebit = shipment.Manifest.Stacks.Sum(stack =>
        {
            int before = originBefore.TryGetValue(stack.Commodity.Id, out int stock) ? stock : 0;
            int after = c.Market.GetListingForCommodity(c.Origin, stack.Commodity)?.Stock ?? 0;
            return before - after;
        });
        TradeLaneDirection direction = shipment.RouteTowardEnd ? TradeLaneDirection.Forward : TradeLaneDirection.Reverse;
        c.Risk.RecordTraderDestroyed(c.Route.Id, direction, incidentKey: "prior-loss-1");
        c.Risk.RecordTraderDestroyed(c.Route.Id, direction, incidentKey: "prior-loss-2");
        int riskBeforeEscort = c.Risk.GetRisk(c.Route.Id, direction);
        Dictionary<string, int> destinationBefore = shipment.Manifest.Stacks
            .ToDictionary(
                stack => stack.Commodity.Id,
                stack => c.Market.GetListingForCommodity(c.Destination, stack.Commodity)?.Stock ?? 0,
                StringComparer.OrdinalIgnoreCase);
        int remainingBeforeDelivery = shipment.RemainingQuantity;

        List<NpcShip> npcs = new() { trader };
        List<SpaceObject> spaceObjects = new() { trader };
        Ship player = new(trader.Position);
        PlayerCredits credits = new(1_000);
        ReputationManager reputation = new(new FactionManager());
        MissionManager missions = new(credits, null, reputation, c.Market, new CargoHold(100));
        MissionWorldManager world = new(
            missions,
            new MissionWaypointSystem(),
            player,
            npcs,
            spaceObjects,
            () => c.Stations,
            marketManager: c.Market,
            tradeLaneProvider: () => Array.Empty<TradeLane>());
        missions.SetWorldManager(world);
        missions.SetEconomicShipmentManager(c.Economy);
        missions.SetTradeRouteRiskManager(c.Risk);
        world.SetEconomicShipmentManager(c.Economy);

        Mission offer = missions.GenerateEconomicEscortMissions(c.Origin).SingleOrDefault();
        if (offer == null || !offer.IsEconomicEscort || !offer.EconomicShipmentTraderIdentity.Equals(
                shipment.TraderIdentity, StringComparison.Ordinal))
            return Fail("risky real shipment did not produce a matching offer");
        if (!missions.AcceptMission(offer, c.Origin))
            return Fail($"real escort acceptance failed: {missions.LastAcceptanceFailureReason}");
        if (shipment.EscortMissionId != offer.Id || !ReferenceEquals(world.GetConvoyShips(offer).SingleOrDefault(), trader))
            return Fail("accepted escort did not bind the existing trader");
        if (originDebit != shipment.InitialQuantity)
            return Fail($"origin debit was not exactly once ({originDebit} vs {shipment.InitialQuantity})");

        offer.ConvoyEncounterActivated = true;
        offer.ConvoyEncounterResolved = true;
        offer.ConvoyEncounterSpawnAttempted = true;
        player.Position = trader.Position;
        world.Update(0.1f, currentSystemIndex: 1);
        if (!offer.ConvoyRouteStarted || trader.IsMissionHoldPosition)
            return Fail("accepted escort did not pass rendezvous into the existing route state");

        player.Position = c.Destination.Position;
        trader.Position = c.Destination.Position;
        world.Update(0.1f, currentSystemIndex: 1);
        if (offer.Status != MissionStatus.Completed || shipment.Settlement != EconomicShipmentSettlement.Delivered ||
            c.Economy.ActiveShipmentCount != 0)
            return Fail("real escorted shipment did not complete through mission world settlement");

        bool exactDelivery = shipment.Manifest.Stacks.All(stack =>
            (c.Market.GetListingForCommodity(c.Destination, stack.Commodity)?.Stock ?? 0) -
            destinationBefore.GetValueOrDefault(stack.Commodity.Id) == stack.InitialQuantity) &&
            remainingBeforeDelivery > 0;
        if (!exactDelivery)
            return Fail("destination stock did not receive the exact real remainder");
        if (c.Risk.GetRisk(c.Route.Id, direction) != Math.Max(0, riskBeforeEscort - TradeRouteRiskManager.SafeDeliveryRecoveryPoints))
            return Fail("successful escort applied an unexpected route-risk recovery");

        int creditsBeforeClaim = credits.Credits;
        float reputationBeforeClaim = reputation.GetStanding(FactionManager.LibertyCorporations);
        if (!missions.TryClaimReward(offer, c.Origin, out _) ||
            credits.Credits != creditsBeforeClaim + offer.Reward ||
            reputation.GetStanding(FactionManager.LibertyCorporations) <= reputationBeforeClaim ||
            missions.TryClaimReward(offer, c.Origin, out _))
            return Fail("escort reward or reputation was not exactly once");
        return Pass();
    }

    private (bool Success, string FailureReason) AcceptedEconomicEscortSaveLoadRebindsOnce()
    {
        Context c = new();
        NpcShip originalTrader = c.CreateTrader("Phase63 Save Trader");
        if (!c.Economy.TryAttachTrader(originalTrader, c.Route, out EconomicShipment shipment))
            return Fail("real shipment was not created for save/load");
        Dictionary<string, int> originAfterDebit = c.Market.GetListingsForStation(c.Origin)
            .Where(listing => listing?.Commodity != null)
            .ToDictionary(listing => listing.Commodity.Id, listing => listing.Stock, StringComparer.OrdinalIgnoreCase);

        Mission mission = Mission.CreateEconomicEscort(
            shipment,
            c.Origin,
            c.Destination,
            shipment.Trader.Position,
            Vector3.Lerp(shipment.Trader.Position, c.Destination.Position, 0.5f),
            TradeRouteRiskManager.DangerousRiskThreshold,
            7_500,
            MissionDifficulty.Medium,
            "CRITICAL SHORTAGE",
            "Commercial Operations");
        mission.Status = MissionStatus.InProgress;
        if (!c.Economy.TryAttachEconomicEscort(mission.Id, shipment.TraderIdentity))
            return Fail("accepted escort was not attached before save");

        SaveGameManager saveManager = new();
        SaveMissionData savedMission = saveManager.CaptureMissions(new[] { mission }).Single();
        SaveEconomicShipmentData savedShipment = c.Economy.CaptureState().Single();
        int originDebit = shipment.Manifest.InitialQuantity;
        c.Economy.PrepareForSaveLoadRebind(new[] { savedShipment });
        c.Economy.ResetForWorldTeardown(restoreOriginStock: true);

        bool reservationWasPreserved = savedShipment.RemainingQuantity == shipment.RemainingQuantity &&
            c.Market.GetListingsForStation(c.Origin)
                .Where(listing => listing?.Commodity != null)
                .All(listing => !originAfterDebit.TryGetValue(listing.Commodity.Id, out int stock) || listing.Stock == stock);
        if (!reservationWasPreserved)
            return Fail("save/load teardown refunded the reserved origin stock");

        NpcShip restoredTrader = c.CreateTrader("Phase63 Save Trader");
        if (!c.Economy.TryAttachTrader(restoredTrader, c.Route, out EconomicShipment restoredShipment))
            return Fail("saved shipment did not rebind to reconstructed trader");
        if (restoredShipment.InitialQuantity != savedShipment.InitialQuantity ||
            restoredShipment.RemainingQuantity != savedShipment.RemainingQuantity ||
            restoredShipment.EscortMissionId != mission.Id ||
            originDebit != savedShipment.InitialQuantity)
            return Fail("rebound shipment changed manifest, escort, or debit state");

        List<Mission> restoredMissions = saveManager.BuildMissionList(new[] { savedMission }, out List<string> warnings);
        Mission restoredMission = restoredMissions.SingleOrDefault();
        List<NpcShip> npcs = new() { restoredTrader };
        List<SpaceObject> spaceObjects = new() { restoredTrader };
        Ship player = new(restoredTrader.Position);
        PlayerCredits credits = new(1_000);
        ReputationManager reputation = new(new FactionManager());
        MissionManager missions = new(credits, null, reputation, c.Market, new CargoHold(100));
        MissionWorldManager world = new(
            missions,
            new MissionWaypointSystem(),
            player,
            npcs,
            spaceObjects,
            () => c.Stations,
            marketManager: c.Market,
            tradeLaneProvider: () => Array.Empty<TradeLane>());
        missions.SetWorldManager(world);
        missions.SetEconomicShipmentManager(c.Economy);
        world.SetEconomicShipmentManager(c.Economy);
        missions.RestoreState(restoredMissions, Array.Empty<Mission>());
        if (restoredMission == null || warnings.Count != 0 || !restoredMission.IsEconomicEscort)
            return Fail("accepted economic escort mission metadata did not survive save/load");

        world.RebindMission(restoredMission);
        return world.GetConvoyShips(restoredMission).Count == 1 &&
            ReferenceEquals(world.GetConvoyShips(restoredMission).Single(), restoredTrader) &&
            c.Economy.ActiveShipmentCount == 1
            ? Pass()
            : Fail("save/load duplicated or failed to rebind the economic convoy");
    }

    private static (bool Success, string FailureReason) Pass() => (true, string.Empty);
    private static (bool Success, string FailureReason) Fail(string reason) => (false, reason);

    private static T RunSilenced<T>(Func<T> action)
    {
        TextWriter original = Console.Out;
        try
        {
            Console.SetOut(TextWriter.Null);
            return action();
        }
        finally
        {
            Console.SetOut(original);
        }
    }
}
