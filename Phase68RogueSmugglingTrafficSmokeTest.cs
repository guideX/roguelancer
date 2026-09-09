using Microsoft.Xna.Framework;
using Roguelancer.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace Roguelancer;

/// <summary>
/// Focused Phase 68 proof. The harness uses the production MarketManager,
/// RogueSmugglingManager, NpcShip route movement, PlayerTargetScanService,
/// CargoPod/LootManager destruction path, and versioned save DTOs.
/// </summary>
internal sealed class Phase68RogueSmugglingTrafficSmokeTest
{
    private sealed class Context
    {
        public MarketManager Market { get; } = new();
        public Station Buffalo { get; }
        public Station Rochester { get; }
        public List<Station> Stations { get; }
        public TrafficZoneConfig Route { get; }
        public List<NpcShip> Npcs { get; } = new();
        public List<SpaceObject> Objects { get; } = new();
        public RogueSmugglingManager Smuggling { get; }

        public Context()
        {
            Buffalo = CreateStation("Buffalo Base", FactionManager.LibertyRogues,
                new Vector3(-30_000f, -1_200f, 36_000f));
            Rochester = CreateStation("Rochester Base", FactionManager.Junkers,
                new Vector3(42_000f, -600f, 30_000f));
            Stations = new List<Station> { Buffalo, Rochester };
            Route = new TrafficZoneConfig
            {
                Id = "phase68-buffalo-rochester",
                Name = "Phase 68 criminal route",
                SystemIndex = 1,
                BehaviorType = TrafficZoneBehaviorType.TraderRoute,
                IsRogueSmugglingRoute = true,
                OriginStationId = "buffalo_base",
                DestinationStationId = "rochester_base",
                RouteStartX = Buffalo.Position.X,
                RouteStartY = Buffalo.Position.Y,
                RouteStartZ = Buffalo.Position.Z,
                RouteEndX = Rochester.Position.X,
                RouteEndY = Rochester.Position.Y,
                RouteEndZ = Rochester.Position.Z,
                CenterX = 6_000f,
                CenterY = -900f,
                CenterZ = 33_000f,
                Radius = 12_000f,
                MaxShips = 2,
                ShipDescription = "Transport Ship Alpha",
                FactionId = FactionManager.LibertyRogues
            };

            Smuggling = new RogueSmugglingManager(
                Market,
                () => Stations,
                () => new[] { Route });
            Smuggling.ConfigureRuntime(SpawnCarrier, RetireCarrier);
        }

        public RogueSmugglingShipment SpawnShipment()
        {
            Smuggling.Update(RogueSmugglingManager.SpawnIntervalSeconds + 1f, Console.WriteLine);
            return Smuggling.ActiveShipments.SingleOrDefault();
        }

        public void RestoreSavedCarrier(RogueSmugglingShipment shipment)
        {
            List<SaveRogueSmugglingShipmentData> saved = Smuggling.CaptureState();
            NpcShip oldCarrier = shipment?.Carrier;
            RetireCarrier(oldCarrier, "save/load test");
            Smuggling.PrepareForSaveLoadRebind(saved);
            Smuggling.ResetForWorldTeardown(restoreOriginStock: true);
            Smuggling.TryRestorePendingCarriers();
            Smuggling.CompleteRebind();
        }

        public void ReachDestination(RogueSmugglingShipment shipment)
        {
            shipment.Carrier.Position = Route.RouteEnd.Value + new Vector3(90f, 0f, 0f);
            shipment.Carrier.Update(new GameTime(TimeSpan.Zero, TimeSpan.FromSeconds(1)), null);
        }

        private NpcShip SpawnCarrier(TrafficZoneConfig route, string identityHint)
        {
            NpcShip carrier = new(
                string.IsNullOrWhiteSpace(identityHint) ? "Phase68 Rogue Smuggler" : identityHint,
                route.RouteStart.Value + new Vector3(90f, 0f, 0f),
                route.Center,
                route.Radius,
                0.2f,
                FactionManager.LibertyRogues);
            carrier.ConfigureTrafficBehavior(
                TrafficZoneBehaviorType.TraderRoute,
                route.Id,
                route.Center,
                route.Radius,
                190f,
                7_000f,
                route.RouteStart,
                route.RouteEnd);
            carrier.RestoreStableIdentity(identityHint);
            carrier.OnDestroyed += Smuggling.NotifyCarrierDestroyed;
            carrier.TrafficRouteEndpointReached += (ship, reachedRouteEnd) =>
                Smuggling.NotifyRouteEndpointReached(ship, reachedRouteEnd);
            Npcs.Add(carrier);
            Objects.Add(carrier);
            return carrier;
        }

        private void RetireCarrier(NpcShip carrier, string reason)
        {
            if (carrier == null)
                return;

            Npcs.Remove(carrier);
            Objects.Remove(carrier);
        }

        private static Station CreateStation(string name, string faction, Vector3 position) =>
            new(new StationConfig
            {
                Description = name,
                FactionId = faction,
                SystemIndex = 1,
                StartupPositionX = position.X,
                StartupPositionY = position.Y,
                StartupPositionZ = position.Z
            }, null);
    }

    public (int Passed, int Failed) Run()
    {
        int passed = 0;
        int failed = 0;
        RunCase("source contraband is reserved exactly", SourceReservationIsExact, ref passed, ref failed);
        RunCase("carrier owns the exact canonical manifest", CarrierOwnsManifest, ref passed, ref failed);
        RunCase("scanner reports real contraband and route", ScannerReportsContraband, ref passed, ref failed);
        RunCase("physical arrival adds exact destination stock", ArrivalAddsExactDestinationStock, ref passed, ref failed);
        RunCase("arrival settlement is idempotent", ArrivalCannotSettleTwice, ref passed, ref failed);
        RunCase("lawful faction combat can acquire the smuggler", LawfulCombatCanAcquireSmuggler, ref passed, ref failed);
        RunCase("destruction exposes exact physical contraband drops", DestructionDropsExactCargo, ref passed, ref failed);
        RunCase("destroyed shipment never credits destination", DestructionDoesNotCreditDestination, ref passed, ref failed);
        RunCase("save/load preserves identity and exact transit cargo", SaveLoadPreservesTransit, ref passed, ref failed);
        RunCase("post-load arrival settles exactly once", PostLoadArrivalSettlesOnce, ref passed, ref failed);
        RunCase("post-load destruction drops and never settles", PostLoadDestructionDrops, ref passed, ref failed);
        RunCase("reset restores source without destination delivery", ResetCannotDeliver, ref passed, ref failed);
        RunCase("legal stolen cargo remains fence-only", LegalStolenCargoRemainsFenceOnly, ref passed, ref failed);
        RunCase("Phase 67 haul provenance stays separate", Phase67ProvenanceStaysSeparate, ref passed, ref failed);
        Console.WriteLine($"[PHASE 68 ROGUE SMUGGLING TRAFFIC SMOKE] RESULT: {passed} passed, {failed} failed");
        return (passed, failed);
    }

    private static void RunCase(
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
                Console.WriteLine($"[PHASE 68 ROGUE SMUGGLING TRAFFIC SMOKE] PASS {label}");
            }
            else
            {
                failed++;
                Console.WriteLine($"[PHASE 68 ROGUE SMUGGLING TRAFFIC SMOKE] FAIL {label}: {reason}");
            }
        }
        catch (Exception ex)
        {
            failed++;
            Console.WriteLine($"[PHASE 68 ROGUE SMUGGLING TRAFFIC SMOKE] FAIL {label}: {ex.Message}");
        }
    }

    private static (bool, string) SourceReservationIsExact()
    {
        Context context = new();
        Dictionary<string, int> before = context.Market
            .GetBlackMarketListingsForStation(context.Buffalo)
            .Where(listing => listing.Commodity?.IsContraband == true)
            .ToDictionary(listing => listing.Commodity.Id, listing => listing.Stock, StringComparer.OrdinalIgnoreCase);
        RogueSmugglingShipment shipment = context.SpawnShipment();
        if (shipment == null)
            return Fail("no real criminal shipment spawned");

        Dictionary<string, int> after = context.Market
            .GetBlackMarketListingsForStation(context.Buffalo)
            .Where(listing => listing.Commodity?.IsContraband == true)
            .ToDictionary(listing => listing.Commodity.Id, listing => listing.Stock, StringComparer.OrdinalIgnoreCase);
        bool exact = shipment.Manifest.Stacks.All(stack =>
            before.TryGetValue(stack.Commodity.Id, out int beforeStock) &&
            after.TryGetValue(stack.Commodity.Id, out int afterStock) &&
            beforeStock - afterStock == stack.InitialQuantity);
        return shipment.Manifest.Stacks.All(stack => stack.Commodity.IsContraband) &&
            exact
            ? Pass()
            : Fail($"source reservation did not match manifest={shipment.InitialQuantity}");
    }

    private static (bool, string) CarrierOwnsManifest()
    {
        Context context = new();
        RogueSmugglingShipment shipment = context.SpawnShipment();
        NpcCargoManifestSnapshot snapshot = null;
        bool scanned = shipment != null && context.Smuggling.TryGetManifestSnapshot(shipment.Carrier, out snapshot);
        int snapshotQuantity = snapshot?.Stacks.Sum(stack => stack.Quantity) ?? 0;
        return scanned && shipment.Carrier.IsRogueSmuggler &&
            snapshotQuantity == shipment.RemainingQuantity &&
            snapshot.Stacks.All(stack => stack.Commodity.IsContraband && !stack.IsStolen)
            ? Pass()
            : Fail($"carrier={shipment?.Carrier?.IsRogueSmuggler}, snapshot={snapshotQuantity}, manifest={shipment?.RemainingQuantity}");
    }

    private static (bool, string) ScannerReportsContraband()
    {
        Context context = new();
        RogueSmugglingShipment shipment = context.SpawnShipment();
        if (shipment == null)
            return Fail("shipment did not spawn");

        Ship player = new(shipment.Carrier.Position);
        PlayerTargetScanService scanner = new(
            context.Npcs,
            new FactionManager(),
            target => context.Smuggling.TryGetManifestSnapshot(target, out NpcCargoManifestSnapshot snapshot)
                ? snapshot
                : NpcCargoManifestSnapshot.NoRegisteredCargo(),
            target => context.Smuggling.TryGetRouteInfo(target, out PlayerTargetScanRouteInfo route)
                ? route
                : null);
        bool started = scanner.TryStartScan(player, shipment.Carrier, false, out _);
        scanner.Update(PlayerTargetScanService.ScanDurationSeconds, player, shipment.Carrier, false);
        PlayerTargetScanResult result = scanner.GetResultFor(shipment.Carrier);
        return started && result?.HasRoute == true &&
            result.Cargo.Sum(entry => entry.Quantity) == shipment.RemainingQuantity &&
            result.Cargo.All(entry => entry.IsContraband && !entry.IsStolen)
            ? Pass()
            : Fail($"started={started}, route={result?.HasRoute}, cargo={result?.Cargo.Sum(entry => entry.Quantity)}/{shipment.RemainingQuantity}");
    }

    private static (bool, string) ArrivalAddsExactDestinationStock()
    {
        Context context = new();
        RogueSmugglingShipment shipment = context.SpawnShipment();
        if (shipment == null)
            return Fail("shipment did not spawn");

        Dictionary<string, int> before = SnapshotStocks(context.Market, context.Rochester, shipment.Manifest.Stacks);
        context.ReachDestination(shipment);
        bool exact = shipment.Manifest.Stacks.All(stack =>
            GetStock(context.Market, context.Rochester, stack.Commodity.Id) ==
            before[stack.Commodity.Id] + stack.InitialQuantity);
        return exact && context.Smuggling.ActiveShipmentCount == 0 &&
            context.Smuggling.TryGetSettlementByIdentity(shipment.ShipmentIdentity, out RogueSmugglingShipmentSettlement settlement) &&
            settlement == RogueSmugglingShipmentSettlement.Delivered
            ? Pass()
            : Fail($"destination did not match manifest, active={context.Smuggling.ActiveShipmentCount}");
    }

    private static (bool, string) ArrivalCannotSettleTwice()
    {
        Context context = new();
        RogueSmugglingShipment shipment = context.SpawnShipment();
        Dictionary<string, int> before = SnapshotStocks(context.Market, context.Rochester, shipment.Manifest.Stacks);
        context.ReachDestination(shipment);
        Dictionary<string, int> once = SnapshotStocks(context.Market, context.Rochester, shipment.Manifest.Stacks);
        bool second = context.Smuggling.TryDeliver(shipment.Carrier);
        Dictionary<string, int> twice = SnapshotStocks(context.Market, context.Rochester, shipment.Manifest.Stacks);
        return !second && once.SequenceEqual(twice) &&
            shipment.Manifest.Stacks.All(stack => once[stack.Commodity.Id] == before[stack.Commodity.Id] + stack.InitialQuantity)
            ? Pass()
            : Fail($"second={second}, destination did not remain exact");
    }

    private static (bool, string) LawfulCombatCanAcquireSmuggler()
    {
        Context context = new();
        RogueSmugglingShipment shipment = context.SpawnShipment();
        NpcShip police = new(
            "Phase68 Police Interceptor",
            shipment.Carrier.Position + new Vector3(400f, 0f, 0f),
            shipment.Carrier.Position,
            1_000f,
            0.2f,
            FactionManager.LibertyPolice);
        police.ConfigureTrafficBehavior(
            TrafficZoneBehaviorType.LawfulPatrol,
            "phase68-police",
            police.Position,
            1_000f,
            180f,
            7_000f);
        bool acquired = NpcFactionCombatTargeting.IsValidHostileTarget(police, shipment.Carrier, 7_000f) &&
            police.SetFactionCombatTarget(shipment.Carrier);
        bool damaged = acquired && shipment.Carrier.ApplyCombatDamage(200f, NpcDestructionSource.Npc);
        return acquired && police.FactionCombatTarget == shipment.Carrier && damaged && shipment.Carrier.IsDestroyed
            ? Pass()
            : Fail($"acquired={acquired}, target={police.FactionCombatTarget == shipment.Carrier}");
    }

    private static (bool, string) DestructionDropsExactCargo()
    {
        Context context = new();
        RogueSmugglingShipment shipment = context.SpawnShipment();
        LootManager loot = CreateLoot(context);
        int expected = shipment.RemainingQuantity;
        bool destroyed = shipment.Carrier.ApplyCombatDamage(200f, NpcDestructionSource.Npc);
        int spawned = loot.SpawnSalvageForDestroyedNpc(shipment.Carrier);
        int dropped = loot.ActivePods
            .Where(pod => pod.PayloadType == CargoPodPayloadType.Commodity &&
                shipment.Manifest.Stacks.Any(stack => string.Equals(
                    pod.CommodityId, stack.Commodity.Id, StringComparison.OrdinalIgnoreCase)))
            .Sum(pod => pod.Quantity);
        context.Smuggling.FinalizeDestroyedCarrier(shipment.Carrier);
        return destroyed && shipment.Carrier.IsDestroyed && spawned > 0 && dropped == expected &&
            context.Smuggling.GetDestructionSalvage(shipment.Carrier) == null
            ? Pass()
            : Fail($"spawned={spawned}, dropped={dropped}/{expected}");
    }

    private static (bool, string) DestructionDoesNotCreditDestination()
    {
        Context context = new();
        RogueSmugglingShipment shipment = context.SpawnShipment();
        Commodity commodity = shipment.Manifest.Stacks[0].Commodity;
        int before = GetStock(context.Market, context.Rochester, commodity.Id);
        bool destroyed = shipment.Carrier.ApplyCombatDamage(200f, NpcDestructionSource.Npc);
        int after = GetStock(context.Market, context.Rochester, commodity.Id);
        bool delivered = context.Smuggling.TryDeliver(shipment.Carrier);
        bool hasSettlement = context.Smuggling.TryGetSettlementByIdentity(
            shipment.ShipmentIdentity,
            out RogueSmugglingShipmentSettlement settlement);
        return destroyed && before == after && !delivered &&
            hasSettlement &&
            settlement == RogueSmugglingShipmentSettlement.Lost
            ? Pass()
            : Fail($"stock={before}->{after}, delivered={delivered}, settlement={settlement}");
    }

    private static (bool, string) SaveLoadPreservesTransit()
    {
        Context context = new();
        RogueSmugglingShipment shipment = context.SpawnShipment();
        string identity = shipment.ShipmentIdentity;
        int quantity = shipment.RemainingQuantity;
        SaveRogueSmugglingShipmentData saved = context.Smuggling.CaptureState().Single();
        context.RestoreSavedCarrier(shipment);
        RogueSmugglingShipment restored = context.Smuggling.ActiveShipments.SingleOrDefault();
        return restored != null && restored.ShipmentIdentity == identity &&
            restored.RemainingQuantity == quantity &&
            restored.OriginStationId == saved.OriginStationId &&
            restored.DestinationStationId == saved.DestinationStationId &&
            restored.Manifest.Stacks[0].Commodity.IsContraband
            ? Pass()
            : Fail($"identity={restored?.ShipmentIdentity}/{identity}, quantity={restored?.RemainingQuantity}/{quantity}");
    }

    private static (bool, string) PostLoadArrivalSettlesOnce()
    {
        Context context = new();
        RogueSmugglingShipment original = context.SpawnShipment();
        Dictionary<string, int> before = SnapshotStocks(context.Market, context.Rochester, original.Manifest.Stacks);
        context.RestoreSavedCarrier(original);
        RogueSmugglingShipment restored = context.Smuggling.ActiveShipments.Single();
        context.ReachDestination(restored);
        Dictionary<string, int> once = SnapshotStocks(context.Market, context.Rochester, restored.Manifest.Stacks);
        bool second = context.Smuggling.TryDeliver(restored.Carrier);
        Dictionary<string, int> twice = SnapshotStocks(context.Market, context.Rochester, restored.Manifest.Stacks);
        return !second && once.SequenceEqual(twice) &&
            restored.Manifest.Stacks.All(stack => once[stack.Commodity.Id] == before[stack.Commodity.Id] + stack.InitialQuantity)
            ? Pass()
            : Fail($"destination did not remain exact, second={second}");
    }

    private static (bool, string) PostLoadDestructionDrops()
    {
        Context context = new();
        RogueSmugglingShipment original = context.SpawnShipment();
        Dictionary<string, int> before = SnapshotStocks(context.Market, context.Rochester, original.Manifest.Stacks);
        context.RestoreSavedCarrier(original);
        RogueSmugglingShipment restored = context.Smuggling.ActiveShipments.Single();
        LootManager loot = CreateLoot(context);
        int expected = restored.RemainingQuantity;
        bool destroyed = restored.Carrier.ApplyCombatDamage(200f, NpcDestructionSource.Npc);
        loot.SpawnSalvageForDestroyedNpc(restored.Carrier);
        int dropped = loot.ActivePods
            .Where(pod => pod.PayloadType == CargoPodPayloadType.Commodity &&
                restored.Manifest.Stacks.Any(stack => string.Equals(
                    pod.CommodityId, stack.Commodity.Id, StringComparison.OrdinalIgnoreCase)))
            .Sum(pod => pod.Quantity);
        context.Smuggling.FinalizeDestroyedCarrier(restored.Carrier);
        bool lost = context.Smuggling.TryGetSettlementByIdentity(
            restored.ShipmentIdentity,
            out RogueSmugglingShipmentSettlement settlement) &&
            settlement == RogueSmugglingShipmentSettlement.Lost;
        bool unchanged = restored.Manifest.Stacks.All(stack =>
            GetStock(context.Market, context.Rochester, stack.Commodity.Id) == before[stack.Commodity.Id]);
        return destroyed && restored.Carrier.IsDestroyed && dropped == expected && unchanged && lost
            ? Pass()
            : Fail($"dropped={dropped}/{expected}, destination unchanged={unchanged}");
    }

    private static (bool, string) ResetCannotDeliver()
    {
        Context context = new();
        RogueSmugglingShipment shipment = context.SpawnShipment();
        Dictionary<string, int> sourceBefore = SnapshotStocks(context.Market, context.Buffalo, shipment.Manifest.Stacks);
        int destinationBefore = GetStock(context.Market, context.Rochester, shipment.Manifest.Stacks[0].Commodity.Id);
        context.Smuggling.Reset();
        bool sourceRestored = shipment.Manifest.Stacks.All(stack =>
            GetStock(context.Market, context.Buffalo, stack.Commodity.Id) == sourceBefore[stack.Commodity.Id] + stack.InitialQuantity);
        int destinationAfter = GetStock(context.Market, context.Rochester, shipment.Manifest.Stacks[0].Commodity.Id);
        return context.Smuggling.ActiveShipmentCount == 0 &&
            sourceRestored && destinationAfter == destinationBefore
            ? Pass()
            : Fail($"source restored={sourceRestored}, destination={destinationBefore}->{destinationAfter}");
    }

    private static (bool, string) LegalStolenCargoRemainsFenceOnly()
    {
        MarketManager market = new();
        Station receiver = CreateFixtureStation("Buffalo Base", FactionManager.LibertyRogues);
        Commodity legal = CommodityCatalog.GetById("consumer-goods");
        int before = GetStock(market, receiver, legal.Id);
        bool accepted = market.TryAddCriminalSupply(receiver, legal, 2, out int quantity, out _);
        int after = GetStock(market, receiver, legal.Id);
        return accepted && quantity == 2 && before == after &&
            market.GetFenceListing(receiver, legal) != null
            ? Pass()
            : Fail($"accepted={accepted}, quantity={quantity}, stock={before}->{after}");
    }

    private static (bool, string) Phase67ProvenanceStaysSeparate()
    {
        Context context = new();
        RogueSmugglingShipment shipment = context.SpawnShipment();
        EconomicShipmentManager lawfulEconomy = new(
            context.Market,
            () => context.Stations,
            _ => false,
            () => Array.Empty<TrafficZoneConfig>());
        bool phase67SeesIt = lawfulEconomy.TryGetShipment(shipment.Carrier, out _);
        bool phase68SeesIt = context.Smuggling.TryGetShipment(shipment.Carrier, out _);
        context.Smuggling.TryGetManifestSnapshot(shipment.Carrier, out NpcCargoManifestSnapshot snapshot);
        return !phase67SeesIt && phase68SeesIt &&
            snapshot?.Stacks.All(stack => stack.Commodity.IsContraband && !stack.IsStolen) == true
            ? Pass()
            : Fail($"phase67={phase67SeesIt}, phase68={phase68SeesIt}, stolen={snapshot?.Stacks.Any(stack => stack.IsStolen)}");
    }

    private static LootManager CreateLoot(Context context)
    {
        LootManager loot = new(
            graphicsDevice: null,
            random: null,
            font: null,
            pixel: null,
            salvageService: new CombatSalvageService(),
            worldObjectsProvider: () => context.Objects);
        loot.ConfigureEconomicCargoCallbacks(
            npc => context.Smuggling.GetDestructionSalvage(npc),
            (npc, commodityId, quantity) => context.Smuggling.ConsumeDestructionSalvage(npc, commodityId, quantity));
        return loot;
    }

    private static int GetStock(MarketManager market, Station station, string commodityId)
    {
        Commodity commodity = CommodityCatalog.GetById(commodityId);
        MarketSurface surface = commodity?.IsContraband == true ? MarketSurface.BlackMarket : MarketSurface.Ordinary;
        return market.GetListingForCommodity(station, commodity, surface)?.Stock ?? -1;
    }

    private static Dictionary<string, int> SnapshotStocks(
        MarketManager market,
        Station station,
        IEnumerable<TraderCargoStack> stacks) =>
        stacks.ToDictionary(stack => stack.Commodity.Id, stack => GetStock(market, station, stack.Commodity.Id), StringComparer.OrdinalIgnoreCase);

    private static Station CreateFixtureStation(string name, string faction) =>
        new(new StationConfig
        {
            Description = name,
            FactionId = faction,
            SystemIndex = 1
        }, null);

    private static (bool Success, string FailureReason) Pass() => (true, string.Empty);
    private static (bool Success, string FailureReason) Fail(string reason) => (false, reason);
}
