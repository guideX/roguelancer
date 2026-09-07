using Microsoft.Xna.Framework;
using Roguelancer.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Roguelancer;

/// <summary>
/// Focused Phase 62 proof for bounded live-market routing and the lifecycle
/// seams that make inbound supply a truthful view of physical manifests.
/// </summary>
internal sealed class AdaptiveTraderRoutingSmokeTest
{
    private sealed class Context
    {
        public MarketManager Market { get; } = RunSilenced(() => new MarketManager());
        public Station Origin { get; }
        public Station HealthyDestination { get; }
        public Station ShortDestination { get; }
        public List<Station> Stations { get; }
        public List<TrafficZoneConfig> Routes { get; } = new();
        public TrafficZoneConfig DefaultRoute { get; }
        public TrafficZoneConfig ShortRoute { get; }
        public EconomicShipmentManager Economy { get; }
        public Commodity ConsumerGoods { get; } = CommodityCatalog.GetById("consumer-goods");

        public Context(bool missionOwned = false, bool includeShortRoute = true)
        {
            Origin = CreateStation("Fort Bush", "fort_bush", new Vector3(6_000f, 600f, -4_500f));
            HealthyDestination = CreateStation("Newark Station", "newark_station", new Vector3(-7_500f, -900f, 6_000f));
            ShortDestination = CreateStation("Rochester Base", "rochester_base", new Vector3(42_000f, -600f, 30_000f));
            Stations = new List<Station> { Origin, HealthyDestination, ShortDestination };

            DefaultRoute = CreateRoute(
                "fort_bush_newark",
                Origin,
                HealthyDestination);
            ShortRoute = CreateRoute(
                "fort_bush_rochester",
                Origin,
                ShortDestination);
            Routes.Add(DefaultRoute);
            if (includeShortRoute)
                Routes.Add(ShortRoute);

            Economy = new EconomicShipmentManager(
                Market,
                () => Stations,
                _ => missionOwned,
                () => Routes);
        }

        public NpcShip CreateTrader(string name = "Phase62 Trader")
        {
            NpcShip trader = new(name, Origin.Position, Vector3.Zero, 1_000f, 0.1f, FactionManager.NeutralCivilians);
            trader.ConfigureTrafficBehavior(
                TrafficZoneBehaviorType.TraderRoute,
                DefaultRoute.Id,
                Vector3.Zero,
                1_000f,
                190f,
                3_500f,
                DefaultRoute.RouteStart,
                DefaultRoute.RouteEnd);
            return trader;
        }

        public void KeepOnlyConsumerGoodsAtOrigin()
        {
            foreach (StationMarketListing listing in Market.GetListingsForStation(Origin)
                .Where(listing => listing?.Commodity != null && listing.Commodity != ConsumerGoods &&
                    !listing.Commodity.IsContraband && !listing.Commodity.IsMissionCargo))
            {
                int exportable = Math.Max(0, listing.Stock - listing.MinimumStock);
                if (exportable > 0)
                    Market.TryRemoveSupply(Origin, listing.Commodity, exportable, 0, out _);
            }
        }

        public void CreateShortage()
        {
            StationMarketListing listing = Market.GetListingForCommodity(ShortDestination, ConsumerGoods);
            if (listing != null && listing.Stock > 0)
                Market.TryRemoveSupply(ShortDestination, ConsumerGoods, listing.Stock, 0, out _);

            // Calling the live shortage API latches Phase 60's real-stock
            // shortage metadata; no synthetic demand is introduced here.
            Market.GetShortageState(ShortDestination, ConsumerGoods);
        }

        private static Station CreateStation(string name, string id, Vector3 position)
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

        private static TrafficZoneConfig CreateRoute(string id, Station origin, Station destination)
        {
            return new TrafficZoneConfig
            {
                Id = id,
                Name = id,
                SystemIndex = 1,
                BehaviorType = TrafficZoneBehaviorType.TraderRoute,
                ShipDescription = "Transport Ship Alpha",
                OriginStationId = origin == null ? string.Empty : origin.Name == "Fort Bush" ? "fort_bush" : "rochester_base",
                DestinationStationId = destination == null ? string.Empty : destination.Name == "Newark Station" ? "newark_station" : "rochester_base",
                RouteStartX = origin?.Position.X,
                RouteStartY = origin?.Position.Y,
                RouteStartZ = origin?.Position.Z,
                RouteEndX = destination?.Position.X,
                RouteEndY = destination?.Position.Y,
                RouteEndZ = destination?.Position.Z
            };
        }
    }

    public (int Passed, int Failed) Run()
    {
        int passed = 0;
        int failed = 0;
        RunCase(PlannerUsesLiveShortageAndPrice, "planner uses live price and shortage", ref passed, ref failed);
        RunCase(TopologyIsBoundedAndInvalidCandidatesAreExcluded, "topology is bounded and validated", ref passed, ref failed);
        RunCase(SelectedRouteIsPhysicalAndDebitsOnce, "selected route is physical and debits once", ref passed, ref failed);
        RunCase(ContrabandAndMissionTrafficStayOut, "contraband and mission traffic stay out", ref passed, ref failed);
        RunCase(NoOpportunityFallsBackWithoutDebit, "no opportunity falls back without debit", ref passed, ref failed);
        RunCase(InboundTracksManifestPiracyAndLoss, "inbound tracks manifest piracy and loss", ref passed, ref failed);
        RunCase(DestroyedReliefShipmentAllowsReplacementPlanning, "lost relief shipment allows replacement planning", ref passed, ref failed);
        RunCase(SaveLoadRestoresDynamicRouteAndInboundOnce, "save/load restores route and inbound once", ref passed, ref failed);
        RunCase(DeliveryIsExactAndScannerShowsRoute, "delivery is exact and scanner shows route", ref passed, ref failed);
        RunCase(ScoreIsBoundedAndDeterministic, "score is bounded and deterministic", ref passed, ref failed);
        Console.WriteLine($"[PHASE 62 ADAPTIVE ROUTING SMOKE] RESULT: {passed} passed, {failed} failed");
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
                Console.WriteLine($"[PHASE 62 ADAPTIVE ROUTING SMOKE] PASS {label}");
            }
            else
            {
                failed++;
                Console.WriteLine($"[PHASE 62 ADAPTIVE ROUTING SMOKE] FAIL {label}: {result.FailureReason}");
            }
        }
        catch (Exception ex)
        {
            failed++;
            Console.WriteLine($"[PHASE 62 ADAPTIVE ROUTING SMOKE] FAIL {label}: {ex.Message}");
        }
    }

    private (bool Success, string FailureReason) PlannerUsesLiveShortageAndPrice()
    {
        Context c = new();
        c.KeepOnlyConsumerGoodsAtOrigin();
        c.CreateShortage();
        NpcShip trader = c.CreateTrader();

        IReadOnlyList<AdaptiveTraderRoutePlan> plans = c.Economy.RoutingPlanner.GetCandidateDestinations(trader, c.DefaultRoute);
        AdaptiveTraderRoutePlan selected = plans.FirstOrDefault(plan => !plan.IsDefaultRoute);
        AdaptiveTraderCommodityOpportunity opportunity = selected?.BestOpportunity;
        return plans.Count == 2 && selected?.Destination == c.ShortDestination && opportunity?.Commodity == c.ConsumerGoods &&
            opportunity.EffectiveDeficit > 0 && opportunity.ShortageBonus is >= 50 and <= 250 &&
            opportunity.DestinationBuyPrice > opportunity.OriginBuyPrice && selected.OpportunityScore > plans.First(plan => plan.IsDefaultRoute).OpportunityScore
            ? Pass()
            : Fail("short destination did not win from current price/shortage state");
    }

    private (bool Success, string FailureReason) TopologyIsBoundedAndInvalidCandidatesAreExcluded()
    {
        Context c = new();
        c.Routes.Add(new TrafficZoneConfig
        {
            Id = "invalid-no-market",
            Name = "Invalid No Market",
            BehaviorType = TrafficZoneBehaviorType.TraderRoute,
            OriginStationId = "fort_bush",
            DestinationStationId = "missing-market"
        });
        c.Routes.Add(new TrafficZoneConfig
        {
            Id = "invalid-self",
            Name = "Invalid Self",
            BehaviorType = TrafficZoneBehaviorType.TraderRoute,
            OriginStationId = "fort_bush",
            DestinationStationId = "fort_bush",
            RouteStartX = c.Origin.Position.X,
            RouteStartY = c.Origin.Position.Y,
            RouteStartZ = c.Origin.Position.Z,
            RouteEndX = c.Origin.Position.X + 500f,
            RouteEndY = c.Origin.Position.Y,
            RouteEndZ = c.Origin.Position.Z
        });
        NpcShip trader = c.CreateTrader();
        IReadOnlyList<AdaptiveTraderRoutePlan> plans = c.Economy.RoutingPlanner.GetCandidateDestinations(trader, c.DefaultRoute);
        bool bounded = plans.Count <= AdaptiveTraderRoutingPlanner.MaximumCandidateDestinations;
        bool onlyKnown = plans.All(plan => c.Routes.Any(route => string.Equals(route.Id, plan.Route.Id, StringComparison.OrdinalIgnoreCase)));
        bool noSelf = plans.All(plan => !string.Equals(c.Market.GetStationId(plan.Destination), c.Market.GetStationId(c.Origin), StringComparison.OrdinalIgnoreCase));
        return bounded && onlyKnown && noSelf && !plans.Any(plan => plan.Route.Id.StartsWith("invalid", StringComparison.OrdinalIgnoreCase))
            ? Pass()
            : Fail("invalid or unbounded route candidates were exposed");
    }

    private (bool Success, string FailureReason) SelectedRouteIsPhysicalAndDebitsOnce()
    {
        Context c = new();
        c.KeepOnlyConsumerGoodsAtOrigin();
        c.CreateShortage();
        NpcShip trader = c.CreateTrader();
        int before = c.Market.GetListingForCommodity(c.Origin, c.ConsumerGoods).Stock;
        if (!c.Economy.TryAttachTrader(trader, c.DefaultRoute, out EconomicShipment shipment))
            return Fail("adaptive shipment was not created");

        int exported = before - c.Market.GetListingForCommodity(c.Origin, c.ConsumerGoods).Stock;
        bool physical = shipment.DestinationStationId == "rochester_base" &&
            trader.TrafficRouteEnd.HasValue &&
            Vector3.DistanceSquared(trader.TrafficRouteEnd.Value, c.ShortDestination.Position) < 1f;
        return physical && exported == shipment.InitialQuantity && shipment.InitialQuantity is >= 2 and <= 12
            ? Pass()
            : Fail($"dynamic route/debit mismatch (destination={shipment.DestinationStationId}, exported={exported}, initial={shipment.InitialQuantity})");
    }

    private (bool Success, string FailureReason) ContrabandAndMissionTrafficStayOut()
    {
        Context mission = new(missionOwned: true);
        int before = mission.Market.GetListingForCommodity(mission.Origin, mission.ConsumerGoods).Stock;
        bool missionAttached = mission.Economy.TryAttachTrader(mission.CreateTrader(), mission.DefaultRoute, out _);

        Context legal = new();
        legal.CreateShortage();
        legal.KeepOnlyConsumerGoodsAtOrigin();
        if (!legal.Economy.TryAttachTrader(legal.CreateTrader(), legal.DefaultRoute, out EconomicShipment shipment))
            return Fail("legal adaptive shipment was not created");

        bool noContraband = shipment.Manifest.Stacks.All(stack => !stack.Commodity.IsContraband && !stack.Commodity.IsMissionCargo);
        bool missionNoDebit = !missionAttached && mission.Economy.ActiveShipmentCount == 0 &&
            mission.Market.GetListingForCommodity(mission.Origin, mission.ConsumerGoods).Stock == before;
        return missionNoDebit && noContraband ? Pass() : Fail("mission traffic or contraband entered adaptive commerce");
    }

    private (bool Success, string FailureReason) NoOpportunityFallsBackWithoutDebit()
    {
        Context c = new(includeShortRoute: false);
        c.KeepOnlyConsumerGoodsAtOrigin();
        foreach (StationMarketListing listing in c.Market.GetListingsForStation(c.Origin)
            .Where(listing => listing?.Commodity != null))
        {
            int exportable = Math.Max(0, listing.Stock - listing.MinimumStock);
            if (exportable > 0)
                c.Market.TryRemoveSupply(c.Origin, listing.Commodity, exportable, 0, out _);
        }

        int before = c.Market.GetListingsForStation(c.Origin).Sum(listing => Math.Max(0, listing.Stock));
        NpcShip trader = c.CreateTrader();
        bool attached = c.Economy.TryAttachTrader(trader, c.DefaultRoute, out _);
        bool defaultRouteUnchanged = trader.TrafficRouteStart == c.DefaultRoute.RouteStart && trader.TrafficRouteEnd == c.DefaultRoute.RouteEnd;
        int after = c.Market.GetListingsForStation(c.Origin).Sum(listing => Math.Max(0, listing.Stock));
        return !attached && c.Economy.ActiveShipmentCount == 0 && before == after && defaultRouteUnchanged
            ? Pass()
            : Fail($"no-opportunity fallback mutated stock or route (attached={attached}, active={c.Economy.ActiveShipmentCount}, before={before}, after={after}, routeUnchanged={defaultRouteUnchanged})");
    }

    private (bool Success, string FailureReason) InboundTracksManifestPiracyAndLoss()
    {
        Context c = new();
        c.KeepOnlyConsumerGoodsAtOrigin();
        c.CreateShortage();
        NpcShip trader = c.CreateTrader();
        if (!c.Economy.TryAttachTrader(trader, c.DefaultRoute, out EconomicShipment shipment))
            return Fail("shipment creation failed");

        int initialInbound = c.Economy.GetInboundQuantity(c.ShortDestination, c.ConsumerGoods);
        TraderCargoStack stack = shipment.Manifest.Stacks.FirstOrDefault(candidate => candidate.Commodity == c.ConsumerGoods);
        if (stack == null)
            return Fail("consumer goods was not selected for shortage relief");

        stack.Remove(Math.Min(1, stack.Quantity));
        int afterPiracy = c.Economy.GetInboundQuantity(c.ShortDestination, c.ConsumerGoods);
        c.Economy.NotifyTraderDestroyed(trader);
        int afterLoss = c.Economy.GetInboundQuantity(c.ShortDestination, c.ConsumerGoods);
        return initialInbound == stack.InitialQuantity && afterPiracy == stack.Quantity && afterLoss == 0
            ? Pass()
            : Fail($"inbound lifecycle mismatch ({initialInbound}->{afterPiracy}->{afterLoss})");
    }

    private (bool Success, string FailureReason) DestroyedReliefShipmentAllowsReplacementPlanning()
    {
        Context c = new();
        c.KeepOnlyConsumerGoodsAtOrigin();
        c.CreateShortage();
        NpcShip firstTrader = c.CreateTrader("Phase62 Relief A");
        if (!c.Economy.TryAttachTrader(firstTrader, c.DefaultRoute, out EconomicShipment first))
            return Fail("first relief shipment was not created");

        c.Economy.NotifyTraderDestroyed(firstTrader);
        c.Economy.FinalizeDestroyedTrader(firstTrader);
        AdaptiveTraderRoutePlan replacementPlan = c.Economy.RoutingPlanner
            .GetCandidateDestinations(c.CreateTrader("Phase62 Relief B"), c.DefaultRoute)
            .FirstOrDefault(plan => !plan.IsDefaultRoute);
        return c.Economy.GetInboundQuantity(c.ShortDestination, c.ConsumerGoods) == 0 &&
            replacementPlan?.Destination == c.ShortDestination && first.Settlement == EconomicShipmentSettlement.Lost
            ? Pass()
            : Fail("destroyed relief shipment did not reopen the live shortage opportunity");
    }

    private (bool Success, string FailureReason) SaveLoadRestoresDynamicRouteAndInboundOnce()
    {
        Context c = new();
        c.KeepOnlyConsumerGoodsAtOrigin();
        c.CreateShortage();
        NpcShip original = c.CreateTrader("Phase62 Save Trader");
        if (!c.Economy.TryAttachTrader(original, c.DefaultRoute, out EconomicShipment shipment))
            return Fail("dynamic save shipment was not created");

        shipment.Manifest.Stacks.First().Remove(Math.Min(1, shipment.Manifest.Stacks.First().Quantity));
        int remaining = shipment.RemainingQuantity;
        string destinationId = shipment.DestinationStationId;
        List<SaveEconomicShipmentData> saved = c.Economy.CaptureState();
        List<SaveMarketStateData> marketState = c.Market.CaptureRuntimeState();
        c.Economy.PrepareForSaveLoadRebind(saved);
        int pendingInbound = c.Economy.GetInboundQuantity(destinationId, shipment.Manifest.Stacks.First().Commodity);
        c.Economy.ResetForWorldTeardown(restoreOriginStock: true);
        c.Market.RestoreRuntimeState(marketState);

        NpcShip rebound = c.CreateTrader("Phase62 Save Trader");
        if (!c.Economy.TryAttachTrader(rebound, c.DefaultRoute, out EconomicShipment restored))
            return Fail("dynamic shipment did not rebind");

        bool sameRoute = restored.DestinationStationId == destinationId && restored.RouteId == shipment.RouteId &&
            rebound.TrafficRouteEnd.HasValue && Vector3.DistanceSquared(rebound.TrafficRouteEnd.Value, c.ShortDestination.Position) < 1f;
        bool inboundExact = pendingInbound == remaining &&
            c.Economy.GetInboundQuantity(destinationId, shipment.Manifest.Stacks.First().Commodity) == remaining;
        bool noSecondDebit = c.Market.GetListingForCommodity(c.Origin, c.ConsumerGoods).Stock ==
            marketState.First(state => state.StationKey == "fortbush").Listings.First(listing => listing.CommodityId == "consumer-goods").Stock;
        c.Economy.CompleteRebind();
        return sameRoute && inboundExact && noSecondDebit && c.Economy.ActiveShipmentCount == 1
            ? Pass()
            : Fail("save/load replanned, double-debited, or duplicated inbound supply");
    }

    private (bool Success, string FailureReason) DeliveryIsExactAndScannerShowsRoute()
    {
        Context c = new();
        c.KeepOnlyConsumerGoodsAtOrigin();
        c.CreateShortage();
        NpcShip trader = c.CreateTrader("Phase62 Scan Trader");
        if (!c.Economy.TryAttachTrader(trader, c.DefaultRoute, out EconomicShipment shipment))
            return Fail("scanner shipment was not created");

        PlayerTargetScanService scanner = new(
            new[] { trader },
            new FactionManager(),
            target => c.Economy.TryGetManifestSnapshot(target, out NpcCargoManifestSnapshot snapshot)
                ? snapshot
                : NpcCargoManifestSnapshot.NoRegisteredCargo(),
            target => c.Economy.TryGetRouteInfo(target, out PlayerTargetScanRouteInfo routeInfo) ? routeInfo : null);
        Ship player = new(trader.Position + new Vector3(100f, 0f, 0f));
        if (!scanner.TryStartScan(player, trader, false, out _))
            return Fail("scanner could not start");
        scanner.Update(PlayerTargetScanService.ScanDurationSeconds, player, trader, false);
        PlayerTargetScanResult scan = scanner.GetResultFor(trader);
        int scannedCargo = scan?.Cargo.Sum(entry => entry.Quantity) ?? 0;
        int before = c.Market.GetListingForCommodity(c.ShortDestination, c.ConsumerGoods).Stock;
        c.Economy.NotifyRouteEndpointReached(trader, true);
        int after = c.Market.GetListingForCommodity(c.ShortDestination, c.ConsumerGoods).Stock;
        int delivered = shipment.Manifest.InitialQuantity;
        c.Economy.NotifyRouteEndpointReached(trader, true);
        return scan?.HasRoute == true && scan.DestinationStationName == c.ShortDestination.Name &&
            scannedCargo == delivered && after == before + delivered &&
            c.Economy.GetInboundQuantity(c.ShortDestination, c.ConsumerGoods) == 0
            ? Pass()
            : Fail($"scanner route or exact once-only delivery was incorrect (hasRoute={scan?.HasRoute}, destination={scan?.DestinationStationName}, cargo={scan?.Cargo.Sum(entry => entry.Quantity)}, remaining={shipment.RemainingQuantity}, before={before}, after={after}, delivered={delivered}, inbound={c.Economy.GetInboundQuantity(c.ShortDestination, c.ConsumerGoods)})");
    }

    private (bool Success, string FailureReason) ScoreIsBoundedAndDeterministic()
    {
        int first = AdaptiveTraderRoutingPlanner.CalculateOpportunityScore(
            int.MaxValue,
            int.MaxValue,
            int.MaxValue,
            int.MaxValue,
            int.MaxValue);
        int second = AdaptiveTraderRoutingPlanner.CalculateOpportunityScore(
            int.MaxValue,
            int.MaxValue,
            int.MaxValue,
            int.MaxValue,
            int.MaxValue);
        int negative = AdaptiveTraderRoutingPlanner.CalculateOpportunityScore(
            int.MinValue,
            0,
            0,
            int.MaxValue);
        return first == second && first <= AdaptiveTraderRoutingPlanner.MaximumShipmentOpportunityScore &&
            negative >= AdaptiveTraderRoutingPlanner.MinimumShipmentOpportunityScore
            ? Pass()
            : Fail("opportunity scoring was not deterministic or bounded");
    }

    private static (bool Success, string FailureReason) Pass() => (true, string.Empty);
    private static (bool Success, string FailureReason) Fail(string reason) => (false, reason);

    private static T RunSilenced<T>(Func<T> action)
    {
        var original = Console.Out;
        using var writer = new System.IO.StringWriter();
        Console.SetOut(writer);
        try { return action(); }
        finally { Console.SetOut(original); }
    }
}
