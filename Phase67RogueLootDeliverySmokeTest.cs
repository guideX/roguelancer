using Microsoft.Xna.Framework;
using Roguelancer.Configuration;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Roguelancer;

/// <summary>
/// End-to-end Phase 67 proof. The primary path uses the live shipment
/// manifest, real CargoPods, real NpcShip movement, and the existing market
/// runtime. A separate market assertion proves the contraband branch enters
/// canonical black-market stock while legal stolen cargo keeps the Phase 57
/// fence-only policy.
/// </summary>
internal sealed class Phase67RogueLootDeliverySmokeTest
{
    private sealed class Context
    {
        public MarketManager Market { get; } = new();
        public Station Origin { get; }
        public Station Destination { get; }
        public Station Receiver { get; }
        public List<Station> Stations { get; }
        public TrafficZoneConfig Route { get; }
        public NpcShip Trader { get; }
        public List<NpcShip> Npcs { get; } = new();
        public List<SpaceObject> Objects { get; } = new();
        public LootManager Loot { get; }
        public EconomicShipmentManager Economy { get; }
        public AmbientPirateRaidManager Raids { get; }
        public EconomicShipment Shipment { get; }

        public Context(string traderName = "Phase67 Rogue Trader")
        {
            Origin = CreateStation("Rochester Base", FactionManager.LibertyPolice, Vector3.Zero);
            Destination = CreateStation("Newark Station", FactionManager.LibertyPolice, new Vector3(4_000f, 0f, 0f));
            Receiver = CreateStation("Buffalo Base", FactionManager.LibertyRogues, new Vector3(11_000f, 0f, 0f));
            Stations = new List<Station> { Origin, Destination, Receiver };
            Route = new TrafficZoneConfig
            {
                Id = "phase67-rochester-newark",
                Name = "Phase 67 same-system delivery route",
                SystemIndex = 1,
                BehaviorType = TrafficZoneBehaviorType.TraderRoute,
                OriginStationId = "rochester_base",
                DestinationStationId = "newark_station",
                RouteStartX = Origin.Position.X,
                RouteStartY = Origin.Position.Y,
                RouteStartZ = Origin.Position.Z,
                RouteEndX = Destination.Position.X,
                RouteEndY = Destination.Position.Y,
                RouteEndZ = Destination.Position.Z
            };

            Trader = new NpcShip(
                traderName,
                new Vector3(300f, 0f, 0f),
                Origin.Position,
                900f,
                0.2f,
                FactionManager.NeutralCivilians);
            Trader.ConfigureTrafficBehavior(
                TrafficZoneBehaviorType.TraderRoute,
                Route.Id,
                Origin.Position,
                900f,
                190f,
                7_000f,
                Route.RouteStart,
                Route.RouteEnd);
            Npcs.Add(Trader);
            Objects.Add(Trader);

            Economy = new EconomicShipmentManager(
                Market,
                () => Stations,
                _ => false,
                () => new[] { Route });
            if (!Economy.TryAttachTrader(Trader, Route, out EconomicShipment shipment))
                throw new InvalidOperationException("real Phase 67 shipment could not be attached");
            Shipment = shipment;

            Loot = new LootManager(
                salvageService: new CombatSalvageService(),
                worldObjectsProvider: () => Objects);
            Raids = new AmbientPirateRaidManager(
                Npcs,
                Objects,
                Economy,
                () => new[] { Route });
            Raids.ConfigureEconomicShipments(Economy, _ => true);
            Raids.ConfigureCriminalDelivery(Market, () => Stations);
            Raids.ConfigureRuntime(
                SpawnRaider,
                RetireRaider,
                (source, commodityId, quantity) => Loot.SpawnStolenCargo(source, commodityId, quantity, out _, null),
                (NpcShip collector, CargoPod pod, int maximumQuantity, out string commodityId, out int collectedQuantity) =>
                    Loot.TryCollectCargoPodForNpc(collector, pod, maximumQuantity, out commodityId, out collectedQuantity),
                () => Loot.ActivePods);
        }

        public AmbientPirateRaid StartRaid()
        {
            Raids.Update(31f);
            return Raids.Raids.FirstOrDefault(raid => raid.State == AmbientPirateRaidState.Active);
        }

        private NpcShip SpawnRaider(
            EconomicShipment shipment,
            string raidIdentity,
            int index,
            NpcLoadoutTier loadoutTier,
            Vector3 position,
            string stableIdentity)
        {
            NpcShip raider = new(
                stableIdentity,
                position,
                shipment.Trader.Position,
                900f,
                0.2f,
                FactionManager.LibertyRogues);
            raider.ConfigureTrafficBehavior(
                TrafficZoneBehaviorType.PirateAmbush,
                "phase67-pirate-zone",
                shipment.Trader.Position,
                900f,
                220f,
                12_000f);
            raider.RestoreStableIdentity(stableIdentity);
            Npcs.Add(raider);
            Objects.Add(raider);
            return raider;
        }

        private void RetireRaider(NpcShip raider, string reason)
        {
            Npcs.Remove(raider);
            Objects.Remove(raider);
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
        Check("real shipment surrender creates physical stolen cargo", RealSurrenderAndPickup, ref passed, ref failed);
        Check("pickup is close-range, exact, and mission-attributed pods are ignored", PickupBoundaries, ref passed, ref failed);
        Check("carrier begins a physical return with a deterministic receiver", PhysicalReturnBegins, ref passed, ref failed);
        Check("carrier reaches the receiver and settles exactly once", CarrierDeliversExactly, ref passed, ref failed);
        Check("returning carrier save/load preserves exact haul and receiver", ReturningSaveLoad, ref passed, ref failed);
        Check("destroyed carrier drops exact physical haul without delivery", DestroyedCarrierDrops, ref passed, ref failed);
        Check("canonical criminal stock and legal fence policy are distinct", CriminalMarketBoundary, ref passed, ref failed);
        Check("scanner exposes actual stolen carrier haul", ScannerShowsStolenHaul, ref passed, ref failed);
        Check("world reset cannot deliver or duplicate a carrier haul", ResetDoesNotDeliver, ref passed, ref failed);
        Check("ineligible receiver cannot accept a criminal receipt", IneligibleReceiverRejected, ref passed, ref failed);
        Console.WriteLine($"[PHASE 67 ROGUE LOOT DELIVERY SMOKE] RESULT: {passed} passed, {failed} failed");
        return (passed, failed);
    }

    private static (bool Success, string FailureReason) RealSurrenderAndPickup()
    {
        Context context = new();
        AmbientPirateRaid raid = context.StartRaid();
        if (raid == null || raid.Raiders.Count == 0)
            return Fail("raid did not activate");

        int before = context.Shipment.RemainingQuantity;
        NpcShip raider = raid.Raiders.First().Ship;
        context.Trader.ApplyDamage(60f, NpcDestructionSource.Npc);
        for (int i = 0; i < 8 && raid.SurrenderedQuantity == 0; i++)
        {
            context.Raids.NotifyNpcDamage(raider, context.Trader, 10f);
            context.Raids.Update(1f);
        }

        CargoPod pod = context.Loot.ActivePods.FirstOrDefault(candidate =>
            candidate.IsStolen && !candidate.IsMissionCargo &&
            string.Equals(candidate.SourceNpcName, context.Trader.Name, StringComparison.OrdinalIgnoreCase));
        if (raid.SurrenderedQuantity <= 0 || pod == null || context.Shipment.RemainingQuantity >= before)
            return Fail($"surrender={raid.SurrenderedQuantity}, pods={context.Loot.ActivePods.Count}, remaining={context.Shipment.RemainingQuantity}/{before}");

        raider.Position = pod.Position;
        context.Raids.Update(0f);
        int hauled = raid.Raiders.Sum(candidate => candidate.Haul.Sum(entry => entry.Quantity));
        return hauled == raid.RecoveredQuantity && hauled > 0 && !context.Loot.ActivePods.Contains(pod)
            ? Pass()
            : Fail($"hauled={hauled}, recovered={raid.RecoveredQuantity}, podRemaining={pod.Quantity}");
    }

    private static (bool Success, string FailureReason) PickupBoundaries()
    {
        Context context = new("Phase67 Pickup Boundary Trader");
        AmbientPirateRaid raid = context.StartRaid();
        AmbientPirateRaider raider = raid?.Raiders.FirstOrDefault();
        Commodity commodity = context.Shipment.Manifest.Stacks.First(stack => stack.Quantity > 0).Commodity;
        if (raider?.Ship == null || commodity == null)
            return Fail("raid did not activate");

        int spawned = context.Loot.SpawnStolenCargo(context.Trader, commodity.Id, 4, out _, null);
        CargoPod pod = context.Loot.ActivePods.LastOrDefault();
        pod.SetMissionCargoAttribution(77, 2, context.Trader.Name);
        raid.SurrenderedQuantity = 4;
        raider.Ship.Position = pod.Position;
        context.Raids.Update(0f);
        bool protectedIgnored = raider.HaulQuantity == 0 && context.Loot.ActivePods.Contains(pod);
        pod.ClearMissionCargoAttribution();
        raider.Ship.Position = pod.Position + new Vector3(pod.PickupRadius + 1f, 0f, 0f);
        context.Raids.Update(0f);
        bool outOfRange = raider.HaulQuantity == 0 && context.Loot.ActivePods.Contains(pod);
        raider.Ship.Position = pod.Position;
        context.Raids.Update(0f);
        bool exact = raider.HaulQuantity == spawned && !context.Loot.ActivePods.Contains(pod);
        return protectedIgnored && outOfRange && exact ? Pass() : Fail($"spawned={spawned}, protected={protectedIgnored}, outOfRange={outOfRange}, exact={exact}");
    }

    private static (bool Success, string FailureReason) PhysicalReturnBegins()
    {
        Context context = PrepareReturningContext(out AmbientPirateRaid raid, out AmbientPirateRaider carrier);
        if (raid == null || carrier?.Ship == null || raid.State != AmbientPirateRaidState.ReturningWithLoot)
            return Fail($"state={raid?.State}, carrier={carrier?.Ship != null}");

        bool receiverBound = string.Equals(carrier.ReceiverStationId, context.Market.GetStationId(context.Receiver), StringComparison.OrdinalIgnoreCase);
        bool notInstant = Vector3.Distance(carrier.Ship.Position, context.Receiver.Position) > AmbientPirateRaidManager.ReceiverArrivalDistance &&
            !carrier.DeliverySettled;
        Vector3 before = carrier.Ship.Position;
        carrier.Ship.Update(new GameTime(TimeSpan.Zero, TimeSpan.FromSeconds(1)), null);
        bool moved = Vector3.DistanceSquared(before, carrier.Ship.Position) > 1f;
        return receiverBound && notInstant && moved ? Pass() : Fail($"receiver={carrier.ReceiverStationId}, distance={Vector3.Distance(carrier.Ship.Position, context.Receiver.Position):0}, moved={moved}");
    }

    private static (bool Success, string FailureReason) CarrierDeliversExactly()
    {
        Context context = PrepareReturningContext(out AmbientPirateRaid raid, out AmbientPirateRaider carrier);
        int quantity = carrier.HaulQuantity;
        string receiverId = carrier.ReceiverStationId;
        for (int i = 0; i < 60 && raid.State == AmbientPirateRaidState.ReturningWithLoot; i++)
        {
            if (carrier.Ship != null)
                carrier.Ship.Update(new GameTime(TimeSpan.Zero, TimeSpan.FromSeconds(1)), null);
            context.Raids.Update(1f);
        }

        return raid.State == AmbientPirateRaidState.Delivered && carrier.DeliverySettled &&
            carrier.HaulQuantity == 0 && carrier.HasEscaped &&
            string.Equals(receiverId, context.Market.GetStationId(context.Receiver), StringComparison.OrdinalIgnoreCase) &&
            quantity > 0
            ? Pass()
            : Fail($"state={raid.State}, settled={carrier.DeliverySettled}, haul={carrier.HaulQuantity}, escaped={carrier.HasEscaped}, receiver={receiverId}");
    }

    private static (bool Success, string FailureReason) ReturningSaveLoad()
    {
        Context context = PrepareReturningContext(out AmbientPirateRaid raid, out AmbientPirateRaider carrier);
        int savedQuantity = carrier.HaulQuantity;
        string savedReceiver = carrier.ReceiverStationId;
        List<SaveAmbientPirateRaidData> saved = context.Raids.CaptureState();
        context.Raids.RestoreState(saved);
        AmbientPirateRaid restored = context.Raids.Raids.SingleOrDefault(candidate => candidate.ShipmentIdentity == raid.ShipmentIdentity);
        AmbientPirateRaider restoredCarrier = restored?.Raiders.FirstOrDefault(candidate => candidate.HaulQuantity > 0);
        int liveCarriers = context.Raids.ReturningRaids.Sum(candidate => candidate.Raiders.Count(raider => raider.IsAlive));
        return saved.Count == 1 && restored?.State == AmbientPirateRaidState.ReturningWithLoot &&
            restoredCarrier?.HaulQuantity == savedQuantity &&
            string.Equals(restoredCarrier.ReceiverStationId, savedReceiver, StringComparison.OrdinalIgnoreCase) &&
            liveCarriers == 1
            ? Pass()
            : Fail($"saved={saved.Count}, state={restored?.State}, haul={restoredCarrier?.HaulQuantity}/{savedQuantity}, receiver={restoredCarrier?.ReceiverStationId}/{savedReceiver}, live={liveCarriers}");
    }

    private static (bool Success, string FailureReason) DestroyedCarrierDrops()
    {
        Context context = new("Phase67 Destroyed Carrier");
        AmbientPirateRaid raid = context.StartRaid();
        AmbientPirateRaider carrier = raid?.Raiders.FirstOrDefault();
        Commodity commodity = context.Shipment.Manifest.Stacks.First(stack => stack.Quantity > 0).Commodity;
        carrier.MutableHaul.Add(new AmbientPirateHaulEntry { CommodityId = commodity.Id, Quantity = 3 });
        NpcShip ship = carrier.Ship;
        context.Raids.NotifyNpcDestroyed(ship);
        foreach (AmbientPirateRaider other in raid.Raiders.Where(candidate => candidate.IsAlive && candidate.Ship != null).ToList())
            context.Raids.NotifyNpcDestroyed(other.Ship);
        CargoPod drop = context.Loot.ActivePods.FirstOrDefault(pod =>
            pod.IsStolen && string.Equals(pod.SourceNpcName, ship.Name, StringComparison.OrdinalIgnoreCase));
        return drop?.Quantity == 3 && carrier.HaulQuantity == 0 && !carrier.DeliverySettled &&
            raid.State == AmbientPirateRaidState.Lost && context.Raids.ReturningRaids.Count == 0
            ? Pass()
            : Fail($"drop={drop?.Quantity}, haul={carrier.HaulQuantity}, settled={carrier.DeliverySettled}, state={raid.State}");
    }

    private static (bool Success, string FailureReason) CriminalMarketBoundary()
    {
        MarketManager market = new();
        Station receiver = CreateStation("Buffalo Base", FactionManager.LibertyRogues, Vector3.Zero);
        StationMarketListing contrabandBefore = market.GetListingForCommodity(receiver, CommodityCatalog.GetById("side-arms"), MarketSurface.BlackMarket);
        int before = contrabandBefore?.Stock ?? -1;
        bool criminalAccepted = market.TryAddCriminalSupply(
            receiver,
            CommodityCatalog.GetById("side-arms"),
            2,
            out int accepted,
            out _);
        int after = market.GetListingForCommodity(receiver, CommodityCatalog.GetById("side-arms"), MarketSurface.BlackMarket)?.Stock ?? -1;

        StationMarketListing legalBefore = market.GetListingForCommodity(receiver, CommodityCatalog.GetById("consumer-goods"), MarketSurface.Ordinary);
        bool legalAccepted = market.TryAddCriminalSupply(
            receiver,
            CommodityCatalog.GetById("consumer-goods"),
            2,
            out int legalQuantity,
            out _);
        int legalAfter = market.GetListingForCommodity(receiver, CommodityCatalog.GetById("consumer-goods"), MarketSurface.Ordinary)?.Stock ?? -1;
        return criminalAccepted && accepted == 2 && after == before + 2 &&
            legalAccepted && legalQuantity == 2 && legalBefore?.Stock == legalAfter
            ? Pass()
            : Fail($"criminal={criminalAccepted} {before}->{after}, legal={legalAccepted} {legalBefore?.Stock}->{legalAfter}");
    }

    private static (bool Success, string FailureReason) ScannerShowsStolenHaul()
    {
        Context context = PrepareReturningContext(out _, out AmbientPirateRaider carrier);
        if (!context.Raids.TryGetHaulSnapshot(carrier.Ship, out NpcCargoManifestSnapshot snapshot))
            return Fail("carrier scan had no registered haul");
        NpcCargoManifestStackSnapshot stack = snapshot.Stacks.FirstOrDefault();
        return snapshot.HasRegisteredCargo && stack?.IsStolen == true && stack.Quantity == carrier.HaulQuantity
            ? Pass()
            : Fail($"registered={snapshot.HasRegisteredCargo}, stolen={stack?.IsStolen}, quantity={stack?.Quantity}/{carrier.HaulQuantity}");
    }

    private static (bool Success, string FailureReason) ResetDoesNotDeliver()
    {
        Context context = PrepareReturningContext(out AmbientPirateRaid raid, out AmbientPirateRaider carrier);
        int before = context.Market.GetListingForCommodity(context.Receiver, CommodityCatalog.GetById("consumer-goods"), MarketSurface.Ordinary)?.Stock ?? -1;
        context.Raids.Reset();
        int after = context.Market.GetListingForCommodity(context.Receiver, CommodityCatalog.GetById("consumer-goods"), MarketSurface.Ordinary)?.Stock ?? -1;
        return raid.State == AmbientPirateRaidState.Lost && carrier.HaulQuantity == 0 &&
            before == after && context.Raids.ReturningRaids.Count == 0
            ? Pass()
            : Fail($"state={raid.State}, haul={carrier.HaulQuantity}, stock={before}->{after}, returning={context.Raids.ReturningRaids.Count}");
    }

    private static (bool Success, string FailureReason) IneligibleReceiverRejected()
    {
        MarketManager market = new();
        Station lawful = CreateStation("Newark Station", FactionManager.LibertyPolice, Vector3.Zero);
        bool accepted = market.TryAddCriminalSupply(
            lawful,
            CommodityCatalog.GetById("side-arms"),
            1,
            out int quantity,
            out _);
        return !accepted && quantity == 0 && !market.HasBlackMarketForStation(lawful)
            ? Pass()
            : Fail($"accepted={accepted}, quantity={quantity}, blackMarket={market.HasBlackMarketForStation(lawful)}");
    }

    private static Context PrepareReturningContext(out AmbientPirateRaid raid, out AmbientPirateRaider carrier)
    {
        Context context = new("Phase67 Returning Trader");
        raid = context.StartRaid();
        if (raid == null || raid.Raiders.Count == 0)
            throw new InvalidOperationException("raid did not activate");

        carrier = raid.Raiders.First();
        Commodity commodity = context.Shipment.Manifest.Stacks.First(stack => stack.Quantity > 0).Commodity;
        int spawned = context.Loot.SpawnStolenCargo(context.Trader, commodity.Id, 4, out _, null);
        CargoPod pod = context.Loot.ActivePods.LastOrDefault();
        if (spawned <= 0 || pod == null)
            throw new InvalidOperationException("physical stolen pod did not spawn");

        raid.SurrenderedQuantity = spawned;
        carrier.Ship.Position = pod.Position;
        context.Raids.Update(0f);
        if (carrier.HaulQuantity <= 0)
            throw new InvalidOperationException("carrier did not pick up physical pod");
        raid.RecoveredQuantity = raid.SurrenderedQuantity;
        context.Raids.Update(0f);
        return context;
    }

    private static void Check(
        string label,
        Func<(bool Success, string FailureReason)> test,
        ref int passed,
        ref int failed)
    {
        try
        {
            (bool success, string reason) = RunSilenced(test);
            if (success)
            {
                passed++;
                Console.WriteLine($"[PHASE 67 ROGUE LOOT DELIVERY SMOKE] PASS {label}");
            }
            else
            {
                failed++;
                Console.WriteLine($"[PHASE 67 ROGUE LOOT DELIVERY SMOKE] FAIL {label}: {reason}");
            }
        }
        catch (Exception ex)
        {
            failed++;
            Console.WriteLine($"[PHASE 67 ROGUE LOOT DELIVERY SMOKE] FAIL {label}: {ex.Message}");
        }
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

    private static (bool Success, string FailureReason) Pass() => (true, string.Empty);
    private static (bool Success, string FailureReason) Fail(string reason) => (false, reason);

    private static T RunSilenced<T>(Func<T> action)
    {
        TextWriter original = Console.Out;
        using StringWriter writer = new();
        Console.SetOut(writer);
        try
        {
            return action();
        }
        finally
        {
            Console.SetOut(original);
        }
    }
}
