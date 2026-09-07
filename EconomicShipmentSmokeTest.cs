using Microsoft.Xna.Framework;
using Roguelancer.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Roguelancer;

/// <summary>
/// Focused Phase 59 proof for stock reservation, shared scanner/piracy cargo,
/// exact delivery, destruction, deterministic selection, and save rebind.
/// </summary>
internal sealed class EconomicShipmentSmokeTest
{
    private sealed class Context
    {
        public MarketManager Market { get; } = RunSilenced(() => new MarketManager());
        public Station Origin { get; }
        public Station Destination { get; }
        public List<Station> Stations { get; }
        public EconomicShipmentManager Economy { get; }
        public TrafficZoneConfig Route { get; }
        public NpcShip Trader { get; }
        public List<NpcShip> Npcs { get; } = new();
        public List<SpaceObject> Objects { get; } = new();
        public ReputationManager Reputation { get; }
        public Ship Player { get; }
        public LootManager Loot { get; }
        public PirateCargoDemandService Demand { get; }

        public Context(string name = "Phase59 Trader", bool missionOwned = false)
        {
            Origin = CreateStation("Fort Bush", "fort_bush", new Vector3(6000f, 600f, -4500f));
            Destination = CreateStation("Newark Station", "newark_station", new Vector3(-7500f, -900f, 6000f));
            Stations = new List<Station> { Origin, Destination };
            Economy = new EconomicShipmentManager(Market, () => Stations, _ => missionOwned);
            Route = new TrafficZoneConfig
            {
                Id = "phase59-route",
                Name = "Phase 59 Route",
                BehaviorType = TrafficZoneBehaviorType.TraderRoute,
                OriginStationId = "fort_bush",
                DestinationStationId = "newark_station",
                RouteStartX = Origin.Position.X,
                RouteStartY = Origin.Position.Y,
                RouteStartZ = Origin.Position.Z,
                RouteEndX = Destination.Position.X,
                RouteEndY = Destination.Position.Y,
                RouteEndZ = Destination.Position.Z
            };
            Trader = CreateTrader(name, Origin.Position, Route);
            Trader.Position = Origin.Position + new Vector3(1000f, 0f, 0f);
            Npcs.Add(Trader);
            Objects.Add(Trader);
            Reputation = new ReputationManager(new FactionManager());
            Player = new Ship(Trader.Position + new Vector3(250f, 0f, 0f));
            Loot = new LootManager(
                graphicsDevice: null,
                random: null,
                font: null,
                pixel: null,
                salvageService: new CombatSalvageService(),
                worldObjectsProvider: () => Objects);
            Demand = new PirateCargoDemandService(
                Npcs,
                Reputation,
                spawnCargo: (trader, commodityId, quantity) => Loot.SpawnExtortionCargo(trader, commodityId, quantity, out _),
                manifestResolver: trader => Economy.GetManifest(trader));
        }

        private static Station CreateStation(string name, string id, Vector3 position)
        {
            StationConfig config = new()
            {
                Description = name,
                StartupPositionX = position.X,
                StartupPositionY = position.Y,
                StartupPositionZ = position.Z
            };
            return new Station(config, null);
        }

        private static NpcShip CreateTrader(string name, Vector3 position, TrafficZoneConfig route)
        {
            NpcShip trader = new(name, position, Vector3.Zero, 1000f, 0.1f, FactionManager.NeutralCivilians);
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
        RunCase(ValidRouteReservesStock, "valid route reserves real origin stock", ref passed, ref failed);
        RunCase(InvalidRouteHasNoDebit, "invalid route has no debit", ref passed, ref failed);
        RunCase(EmptyOriginCreatesNoSyntheticCargo, "empty origin creates no synthetic cargo", ref passed, ref failed);
        RunCase(MissionTraderExcluded, "mission-owned trader is excluded", ref passed, ref failed);
        RunCase(SelectionIsCanonicalDeterministicAndBounded, "selection is canonical, deterministic, and bounded", ref passed, ref failed);
        RunCase(ScannerSeesSharedManifest, "scanner sees shared economic manifest", ref passed, ref failed);
        RunCase(RefusalPreservesFullShipment, "refusal preserves full shipment", ref passed, ref failed);
        RunCase(PiracyReducesExactDeliveredRemainder, "piracy reduces exact delivered remainder", ref passed, ref failed);
        RunCase(UntouchedDeliveryIsExactAndOnceOnly, "untouched delivery is exact and once-only", ref passed, ref failed);
        RunCase(DestructionStopsDelivery, "destruction cannot deliver and salvage is bounded", ref passed, ref failed);
        RunCase(SaveRebindIsExact, "save rebind restores reduced manifest without double debit", ref passed, ref failed);
        RunCase(WorldTeardownRefundsReservation, "world teardown refunds active reservation", ref passed, ref failed);
        Console.WriteLine($"[PHASE 59 ECONOMY SMOKE] RESULT: {passed} passed, {failed} failed");
        return (passed, failed);
    }

    private void RunCase(Func<(bool Success, string FailureReason)> test, string label, ref int passed, ref int failed)
    {
        try
        {
            (bool Success, string FailureReason) result = RunSilenced(test);
            if (result.Success)
            {
                passed++;
                Console.WriteLine($"[PHASE 59 ECONOMY SMOKE] PASS {label}");
            }
            else
            {
                failed++;
                Console.WriteLine($"[PHASE 59 ECONOMY SMOKE] FAIL {label}: {result.FailureReason}");
            }
        }
        catch (Exception ex)
        {
            failed++;
            Console.WriteLine($"[PHASE 59 ECONOMY SMOKE] FAIL {label}: {ex.Message}");
        }
    }

    private (bool Success, string FailureReason) ValidRouteReservesStock()
    {
        Context c = new();
        Dictionary<string, int> before = c.Market.GetListingsForStation(c.Origin)
            .Where(listing => listing?.Commodity != null)
            .ToDictionary(listing => listing.Commodity.Id, listing => listing.Stock, StringComparer.OrdinalIgnoreCase);
        if (before == null || !c.Economy.TryAttachTrader(c.Trader, c.Route, out EconomicShipment shipment))
            return Fail("valid configured market route did not create a shipment");

        int exported = shipment.Manifest.Stacks.Sum(stack => before[stack.Commodity.Id] -
            c.Market.GetListingForCommodity(c.Origin, stack.Commodity).Stock);
        return exported == shipment.InitialQuantity
            ? Pass()
            : Fail($"origin stock changed by {exported}, expected {shipment.InitialQuantity}");
    }

    private (bool Success, string FailureReason) InvalidRouteHasNoDebit()
    {
        Context c = new();
        Dictionary<string, int> before = c.Market.GetListingsForStation(c.Origin)
            .Where(listing => listing?.Commodity != null)
            .ToDictionary(listing => listing.Commodity.Id, listing => listing.Stock, StringComparer.OrdinalIgnoreCase);
        c.Route.DestinationStationId = "missing-market";
        bool attached = c.Economy.TryAttachTrader(c.Trader, c.Route, out _);
        bool unchanged = before.All(entry => c.Market.GetListingForCommodity(c.Origin,
            CommodityCatalog.GetById(entry.Key)).Stock == entry.Value);
        return !attached && unchanged ? Pass() : Fail("invalid route mutated origin stock");
    }

    private (bool Success, string FailureReason) MissionTraderExcluded()
    {
        Context c = new(missionOwned: true);
        Dictionary<string, int> before = c.Market.GetListingsForStation(c.Origin)
            .Where(listing => listing?.Commodity != null)
            .ToDictionary(listing => listing.Commodity.Id, listing => listing.Stock, StringComparer.OrdinalIgnoreCase);
        bool attached = c.Economy.TryAttachTrader(c.Trader, c.Route, out _);
        bool unchanged = before.All(entry => c.Market.GetListingForCommodity(c.Origin,
            CommodityCatalog.GetById(entry.Key)).Stock == entry.Value);
        return !attached && c.Economy.ActiveShipmentCount == 0 && unchanged
            ? Pass()
            : Fail("mission-owned trader entered ambient economy");
    }

    private (bool Success, string FailureReason) EmptyOriginCreatesNoSyntheticCargo()
    {
        Context c = new();
        foreach (StationMarketListing listing in c.Market.GetListingsForStation(c.Origin)
            .Where(candidate => candidate?.Commodity != null && !candidate.Commodity.IsContraband && !candidate.Commodity.IsMissionCargo))
        {
            int exportable = Math.Max(0, listing.Stock - listing.MinimumStock);
            if (exportable > 0)
                c.Market.TryRemoveSupply(c.Origin, listing.Commodity, exportable, 0, out _);
        }

        bool attached = c.Economy.TryAttachTrader(c.Trader, c.Route, out _);
        bool nonNegative = c.Market.GetListingsForStation(c.Origin).All(listing => listing.Stock >= 0);
        return !attached && c.Economy.ActiveShipmentCount == 0 && nonNegative
            ? Pass()
            : Fail("empty origin created a shipment or negative stock");
    }

    private (bool Success, string FailureReason) SelectionIsCanonicalDeterministicAndBounded()
    {
        Context a = new("Phase59 Deterministic");
        Context b = new("Phase59 Deterministic");
        if (!a.Economy.TryAttachTrader(a.Trader, a.Route, out EconomicShipment first) ||
            !b.Economy.TryAttachTrader(b.Trader, b.Route, out EconomicShipment second))
            return Fail("deterministic route could not create both shipments");

        bool canonical = first.Manifest.Stacks.All(stack => CommodityCatalog.GetById(stack.Commodity.Id) == stack.Commodity);
        bool bounded = first.InitialQuantity >= 1 && first.InitialQuantity <= 12 && first.Manifest.CommodityTypeCount <= 3;
        bool same = first.OriginStationId == second.OriginStationId &&
            first.DestinationStationId == second.DestinationStationId &&
            first.Manifest.Stacks.Select(stack => $"{stack.Commodity.Id}:{stack.Quantity}")
                .SequenceEqual(second.Manifest.Stacks.Select(stack => $"{stack.Commodity.Id}:{stack.Quantity}"));
        return canonical && bounded && same ? Pass() : Fail("selection was not canonical/deterministic/bounded");
    }

    private (bool Success, string FailureReason) ScannerSeesSharedManifest()
    {
        Context c = new();
        if (!c.Economy.TryAttachTrader(c.Trader, c.Route, out EconomicShipment shipment))
            return Fail("shipment creation failed");

        if (!c.Economy.TryGetManifestSnapshot(c.Trader, out NpcCargoManifestSnapshot snapshot))
            return Fail("economic scanner snapshot was unavailable");

        return snapshot.RemainingQuantity == shipment.RemainingQuantity &&
            snapshot.Stacks.Select(stack => stack.Commodity.Id).SequenceEqual(shipment.Manifest.Stacks.Select(stack => stack.Commodity.Id))
            ? Pass()
            : Fail("scanner did not read the active manifest");
    }

    private (bool Success, string FailureReason) PiracyReducesExactDeliveredRemainder()
    {
        Context c = new();
        if (!c.Economy.TryAttachTrader(c.Trader, c.Route, out EconomicShipment shipment))
            return Fail("shipment creation failed");

        int initial = shipment.RemainingQuantity;
        c.Trader.MarkDamagedByPlayer(4f);
        c.Trader.ApplyDamage(4f, NpcDestructionSource.Player);
        if (!c.Demand.TryIssueDemand(c.Player, c.Trader, out string demandFailure))
            return Fail($"shared economic trader could not receive piracy demand: {demandFailure}");
        c.Demand.Update(PirateCargoDemandService.ResponseWindowSeconds + 0.1f, c.Player);
        PiracyDemandResult result = c.Demand.LastResult;
        if (result?.State != PiracyDemandState.Complying || result.SurrenderedQuantity <= 0 ||
            result.SurrenderedQuantity >= initial || c.Loot.ActivePods.Count == 0 ||
            c.Loot.ActivePods.Any(pod => !pod.IsStolen))
            return Fail($"piracy did not release a positive physical remainder (state={result?.State}, surrendered={result?.SurrenderedQuantity}, reason={result?.ResolutionReason}, pods={c.Loot.ActivePods.Count})");

        Dictionary<string, int> expectedByCommodity = shipment.Manifest.Stacks
            .ToDictionary(stack => stack.Commodity.Id, stack => stack.Quantity, StringComparer.OrdinalIgnoreCase);
        Dictionary<string, int> beforeByCommodity = expectedByCommodity.Keys.ToDictionary(
            commodityId => commodityId,
            commodityId => c.Market.GetListingForCommodity(c.Destination, CommodityCatalog.GetById(commodityId)).Stock,
            StringComparer.OrdinalIgnoreCase);
        c.Economy.NotifyRouteEndpointReached(c.Trader, true);
        bool exact = expectedByCommodity.All(entry =>
            c.Market.GetListingForCommodity(c.Destination, CommodityCatalog.GetById(entry.Key)).Stock ==
            beforeByCommodity[entry.Key] + entry.Value);
        return exact && c.Economy.ActiveShipmentCount == 0
            ? Pass()
            : Fail($"delivery did not reflect shared manifest remainder (surrendered={result.SurrenderedQuantity}, remaining={shipment.RemainingQuantity})");
    }

    private (bool Success, string FailureReason) RefusalPreservesFullShipment()
    {
        Context c = new();
        if (!c.Economy.TryAttachTrader(c.Trader, c.Route, out EconomicShipment shipment))
            return Fail("shipment creation failed");

        NpcShip police = new("Phase59 Police Witness", c.Trader.Position + new Vector3(150f, 0f, 0f), Vector3.Zero, 500f, 0.1f, FactionManager.LibertyPolice);
        c.Npcs.Add(police);
        c.Objects.Add(police);
        int initial = shipment.RemainingQuantity;
        Dictionary<string, int> expectedByCommodity = shipment.Manifest.Stacks
            .ToDictionary(stack => stack.Commodity.Id, stack => stack.Quantity, StringComparer.OrdinalIgnoreCase);
        Dictionary<string, int> beforeByCommodity = shipment.Manifest.Stacks
            .ToDictionary(stack => stack.Commodity.Id, stack => c.Market.GetListingForCommodity(c.Destination, stack.Commodity).Stock, StringComparer.OrdinalIgnoreCase);
        if (!c.Demand.TryIssueDemand(c.Player, c.Trader, out string demandFailure))
            return Fail($"refusal test could not issue demand: {demandFailure}");
        c.Demand.Update(PirateCargoDemandService.ResponseWindowSeconds + 0.1f, c.Player);
        PiracyDemandResult result = c.Demand.LastResult;
        if (result?.State != PiracyDemandState.RefusingFleeing || shipment.RemainingQuantity != initial || c.Loot.ActivePods.Count != 0)
            return Fail($"refusal changed the shipment (state={result?.State}, remaining={shipment.RemainingQuantity}, pods={c.Loot.ActivePods.Count})");

        c.Economy.NotifyRouteEndpointReached(c.Trader, true);
        bool exact = expectedByCommodity.All(entry =>
            c.Market.GetListingForCommodity(c.Destination, CommodityCatalog.GetById(entry.Key)).Stock ==
            beforeByCommodity[entry.Key] + entry.Value);
        return exact && c.Economy.ActiveShipmentCount == 0 ? Pass() : Fail("refused shipment did not deliver its full remaining manifest");
    }

    private (bool Success, string FailureReason) UntouchedDeliveryIsExactAndOnceOnly()
    {
        Context c = new();
        if (!c.Economy.TryAttachTrader(c.Trader, c.Route, out EconomicShipment shipment))
            return Fail("shipment creation failed");

        Commodity commodity = shipment.Manifest.Stacks.First().Commodity;
        int quantity = shipment.Manifest.Stacks.Where(stack => stack.Commodity == commodity).Sum(stack => stack.Quantity);
        int before = c.Market.GetListingForCommodity(c.Destination, commodity).Stock;
        c.Economy.NotifyRouteEndpointReached(c.Trader, true);
        int delivered = c.Market.GetListingForCommodity(c.Destination, commodity).Stock;
        c.Economy.NotifyRouteEndpointReached(c.Trader, true);
        int afterRepeat = c.Market.GetListingForCommodity(c.Destination, commodity).Stock;
        return delivered == before + quantity && afterRepeat == delivered ? Pass() : Fail("delivery was not exact or once-only");
    }

    private (bool Success, string FailureReason) DestructionStopsDelivery()
    {
        Context c = new();
        if (!c.Economy.TryAttachTrader(c.Trader, c.Route, out EconomicShipment shipment))
            return Fail("shipment creation failed");

        int remaining = shipment.RemainingQuantity;
        int destinationBefore = c.Market.GetListingForCommodity(c.Destination, shipment.Manifest.Stacks.First().Commodity).Stock;
        c.Economy.NotifyTraderDestroyed(c.Trader);
        int salvage = c.Economy.GetDestructionSalvage(c.Trader)?.Sum(drop => drop.Quantity) ?? 0;
        c.Economy.FinalizeDestroyedTrader(c.Trader);
        c.Economy.NotifyRouteEndpointReached(c.Trader, true);
        int destinationAfter = c.Market.GetListingForCommodity(c.Destination, shipment.Manifest.Stacks.First().Commodity).Stock;
        return salvage == remaining && destinationAfter == destinationBefore && c.Economy.ActiveShipmentCount == 0
            ? Pass()
            : Fail("destroyed shipment later delivered or exceeded remaining cargo");
    }

    private (bool Success, string FailureReason) SaveRebindIsExact()
    {
        Context c = new("Phase59 Save Trader");
        if (!c.Economy.TryAttachTrader(c.Trader, c.Route, out EconomicShipment shipment))
            return Fail("shipment creation failed");

        TraderCargoStack stack = shipment.Manifest.Stacks.First();
        stack.Remove(Math.Min(1, stack.Quantity));
        int reduced = shipment.RemainingQuantity;
        List<SaveEconomicShipmentData> saved = c.Economy.CaptureState();
        List<SaveMarketStateData> markets = c.Market.CaptureRuntimeState();
        c.Economy.PrepareForSaveLoadRebind(saved);
        c.Economy.ResetForWorldTeardown(restoreOriginStock: true);
        c.Market.RestoreRuntimeState(markets);

        NpcShip rebound = CreateTrader("Phase59 Save Trader", c.Origin.Position, c.Route);
        if (!c.Economy.TryAttachTrader(rebound, c.Route, out EconomicShipment restored))
            return Fail("saved shipment did not rebind");

        bool sameQuantity = restored.RemainingQuantity == reduced &&
            restored.InitialQuantity == shipment.InitialQuantity;
        int destinationBefore = c.Market.GetListingForCommodity(c.Destination, restored.Manifest.Stacks.First().Commodity).Stock;
        c.Economy.NotifyRouteEndpointReached(rebound, true);
        int destinationAfter = c.Market.GetListingForCommodity(c.Destination, restored.Manifest.Stacks.First().Commodity).Stock;
        c.Economy.CompleteRebind();
        return sameQuantity && destinationAfter >= destinationBefore && c.Economy.ActiveShipmentCount == 0
            ? Pass()
            : Fail("save rebind changed the reduced manifest or failed to settle");
    }

    private (bool Success, string FailureReason) WorldTeardownRefundsReservation()
    {
        Context c = new();
        Dictionary<string, int> before = c.Market.GetListingsForStation(c.Origin)
            .Where(listing => listing?.Commodity != null)
            .ToDictionary(listing => listing.Commodity.Id, listing => listing.Stock, StringComparer.OrdinalIgnoreCase);
        if (!c.Economy.TryAttachTrader(c.Trader, c.Route, out _))
            return Fail("shipment creation failed");
        c.Economy.ResetForWorldTeardown(restoreOriginStock: true);
        bool unchanged = before.All(entry => c.Market.GetListingForCommodity(c.Origin,
            CommodityCatalog.GetById(entry.Key)).Stock == entry.Value);
        return c.Economy.ActiveShipmentCount == 0 && unchanged ? Pass() : Fail("teardown did not refund reservation");
    }

    private static (bool Success, string FailureReason) Pass() => (true, string.Empty);
    private static (bool Success, string FailureReason) Fail(string reason) => (false, reason);

    private static NpcShip CreateTrader(string name, Vector3 position, TrafficZoneConfig route)
    {
        NpcShip trader = new(name, position, Vector3.Zero, 1000f, 0.1f, FactionManager.NeutralCivilians);
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

    private static T RunSilenced<T>(Func<T> action)
    {
        var original = Console.Out;
        using var writer = new System.IO.StringWriter();
        Console.SetOut(writer);
        try { return action(); }
        finally { Console.SetOut(original); }
    }
}
