using Microsoft.Xna.Framework;
using Roguelancer.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Roguelancer;

/// <summary>
/// Focused Phase 66 proof for deterministic autonomous Rogue raids. The
/// integration cases attach to the live shipment manager, so manifest loss,
/// physical pods, route risk, and save state all use production seams.
/// </summary>
internal sealed class Phase66AmbientPirateRaidSmokeTest
{
    private sealed class Context
    {
        public MarketManager Market { get; } = new();
        public Station Origin { get; }
        public Station Destination { get; }
        public TrafficZoneConfig Route { get; }
        public NpcShip Trader { get; }
        public List<Station> Stations { get; }
        public List<NpcShip> Npcs { get; } = new();
        public List<SpaceObject> Objects { get; } = new();
        public List<CargoPod> Pods { get; } = new();
        public TradeRouteRiskManager Risk { get; } = new();
        public EconomicShipmentManager Economy { get; }
        public AmbientPirateRaidManager Raids { get; }
        public EconomicShipment Shipment { get; }
        public int SpawnCalls { get; private set; }
        public int RetireCalls { get; private set; }

        public Context(string traderName = "Phase66 Ambient Trader")
        {
            Origin = CreateStation("Rochester Base", FactionManager.Junkers, new Vector3(6000f, 600f, -4500f));
            Destination = CreateStation("Newark Station", FactionManager.LibertyPolice, new Vector3(-7500f, -900f, 6000f));
            Stations = new List<Station> { Origin, Destination };
            Route = new TrafficZoneConfig
            {
                Id = "phase66-rochester-newark",
                Name = "Phase 66 Rochester-Newark route",
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
                Origin.Position + new Vector3(350f, 0f, 0f),
                Origin.Position,
                1000f,
                0.2f,
                FactionManager.NeutralCivilians);
            Trader.ConfigureTrafficBehavior(
                TrafficZoneBehaviorType.TraderRoute,
                Route.Id,
                Origin.Position,
                1000f,
                190f,
                7000f,
                Route.RouteStart,
                Route.RouteEnd);
            Npcs.Add(Trader);
            Objects.Add(Trader);

            Economy = new EconomicShipmentManager(
                Market,
                () => Stations,
                _ => false,
                () => new[] { Route });
            Economy.ConfigureRiskManager(Risk);
            Risk.RecordTraderDestroyed(Route.Id, TradeLaneDirection.Forward, incidentKey: "phase66-risk-a");
            Risk.RecordTraderDestroyed(Route.Id, TradeLaneDirection.Forward, incidentKey: "phase66-risk-b");
            Risk.RecordTraderDestroyed(Route.Id, TradeLaneDirection.Forward, incidentKey: "phase66-risk-c");

            if (!Economy.TryAttachTrader(Trader, Route, out EconomicShipment shipment))
                throw new InvalidOperationException("real Phase 66 shipment could not be attached");
            Shipment = shipment;

            Raids = new AmbientPirateRaidManager(
                Npcs,
                Objects,
                Economy,
                () => new[] { Route });
            Raids.ConfigureEconomicShipments(Economy, _ => true);
            Raids.ConfigureRuntime(
                SpawnRaider,
                RetireRaider,
                SpawnCargo,
                CollectCargo,
                () => Pods);
        }

        public AmbientPirateRaid StartRaid()
        {
            Raids.Update(31f);
            return Raids.ActiveRaids.FirstOrDefault();
        }

        private NpcShip SpawnRaider(
            EconomicShipment shipment,
            string raidIdentity,
            int index,
            NpcLoadoutTier loadoutTier,
            Vector3 position,
            string stableIdentity)
        {
            SpawnCalls++;
            NpcShip raider = new(
                stableIdentity,
                position,
                shipment.Trader.Position,
                900f,
                0.2f,
                FactionManager.LibertyRogues);
            raider.ConfigureTrafficBehavior(
                TrafficZoneBehaviorType.PirateAmbush,
                "phase66-pirate-zone",
                shipment.Trader.Position,
                900f,
                220f,
                12000f);
            raider.RestoreStableIdentity(stableIdentity);
            Npcs.Add(raider);
            Objects.Add(raider);
            return raider;
        }

        private void RetireRaider(NpcShip raider, string reason)
        {
            RetireCalls++;
            Npcs.Remove(raider);
            Objects.Remove(raider);
        }

        private int SpawnCargo(NpcShip source, string commodityId, int quantity)
        {
            Commodity commodity = CommodityCatalog.GetById(commodityId);
            int safeQuantity = Math.Clamp(quantity, 1, 40);
            if (source == null || commodity == null || commodity.IsContraband || commodity.IsMissionCargo ||
                !CargoPod.TryCreate(
                    commodity.Id,
                    safeQuantity,
                    source.Position + new Vector3(110f, 0f, 0f),
                    Vector3.Zero,
                    300f,
                    200f,
                    out CargoPod pod))
                return 0;

            pod.SetSalvageSource(source, CombatSalvageTier.Standard);
            pod.SetStolenProvenance(true);
            Pods.Add(pod);
            return safeQuantity;
        }

        private bool CollectCargo(
            NpcShip collector,
            CargoPod pod,
            int maximumQuantity,
            out string commodityId,
            out int collectedQuantity)
        {
            commodityId = string.Empty;
            collectedQuantity = 0;
            if (collector == null || pod == null || !Pods.Contains(pod) || pod.IsExpired || pod.IsDepleted ||
                !pod.IsStolen || !pod.IsWithinPickupRange(collector.Position) || maximumQuantity <= 0)
                return false;

            collectedQuantity = pod.TakeQuantity(Math.Min(1, Math.Min(maximumQuantity, pod.Quantity)));
            commodityId = pod.CommodityId;
            if (pod.IsDepleted)
                Pods.Remove(pod);
            return collectedQuantity > 0;
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
        Check("valuable legal routes qualify while trivial and protected traffic do not", QualifyingGates, ref passed, ref failed);
        Check("security deterrence and deterministic budgets are bounded", DeterministicBudgets, ref passed, ref failed);
        Check("surrender mutates the live manifest into stolen physical cargo", LiveSurrenderIsPhysical, ref passed, ref failed);
        Check("delayed raid save/load does not duplicate raiders", DelayedSaveLoadIsIdempotent, ref passed, ref failed);
        Check("NPC pickup is exact and destroyed raiders drop physical haul", PhysicalRecoveryAndDrop, ref passed, ref failed);
        Console.WriteLine($"[PHASE 66 AMBIENT PIRATE RAID SMOKE] RESULT: {passed} passed, {failed} failed");
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
            (bool success, string reason) = RunSilenced(test);
            if (success)
            {
                passed++;
                Console.WriteLine($"[PHASE 66 AMBIENT PIRATE RAID SMOKE] PASS {label}");
            }
            else
            {
                failed++;
                Console.WriteLine($"[PHASE 66 AMBIENT PIRATE RAID SMOKE] FAIL {label}: {reason}");
            }
        }
        catch (Exception ex)
        {
            failed++;
            Console.WriteLine($"[PHASE 66 AMBIENT PIRATE RAID SMOKE] FAIL {label}: {ex.Message}");
        }
    }

    private static (bool Success, string FailureReason) QualifyingGates()
    {
        TrafficZoneConfig route = CreateTestRoute();
        AmbientPirateRaidManager manager = new(
            new List<NpcShip>(),
            new List<SpaceObject>(),
            routesProvider: () => new[] { route });
        EconomicShipment lowValue = CreateDirectShipment("Phase66 Low", CommodityCatalog.GetById("luxury-goods"), 2, 70, route);
        EconomicShipment valuable = CreateDirectShipment("Phase66 Valuable", CommodityCatalog.GetById("diamonds"), 2, 0, route);
        EconomicShipment transit = CreateDirectShipment("Phase66 Transit", CommodityCatalog.GetById("diamonds"), 2, 0, route);
        transit.Trader.SetTradeLaneTransit(true, "phase66-lane", TradeLaneDirection.Forward, 0);
        EconomicShipment mission = CreateDirectShipment("Phase66 Mission", CommodityCatalog.GetById("diamonds"), 2, 0, route);
        mission.EscortMissionId = 22;
        EconomicShipment contraband = CreateDirectShipment("Phase66 Contraband", CommodityCatalog.GetById("side-arms"), 2, 0, route);

        bool lowRejected = !manager.TryEvaluateOpportunity(lowValue, out _);
        bool valuableAccepted = manager.TryEvaluateOpportunity(valuable, out AmbientPirateRaidOpportunity opportunity);
        bool excluded = !manager.TryEvaluateOpportunity(transit, out _) &&
            !manager.TryEvaluateOpportunity(mission, out _) &&
            !manager.TryEvaluateOpportunity(contraband, out _);
        return lowRejected && valuableAccepted && excluded && opportunity.RaiderCount is >= 1 and <= 3
            ? Pass()
            : Fail($"lowRejected={lowRejected}, valuableAccepted={valuableAccepted}, excluded={excluded}");
    }

    private static (bool Success, string FailureReason) DeterministicBudgets()
    {
        TrafficZoneConfig route = CreateTestRoute();
        EconomicShipment first = CreateDirectShipment("Phase66 Deterministic", CommodityCatalog.GetById("diamonds"), 2, 70, route);
        EconomicShipment second = CreateDirectShipment("Phase66 Deterministic", CommodityCatalog.GetById("diamonds"), 2, 70, route);
        AmbientPirateRaidManager firstManager = new(new List<NpcShip>(), new List<SpaceObject>(), routesProvider: () => new[] { route });
        AmbientPirateRaidManager secondManager = new(new List<NpcShip>(), new List<SpaceObject>(), routesProvider: () => new[] { route });
        bool firstAccepted = firstManager.TryEvaluateOpportunity(first, out AmbientPirateRaidOpportunity firstOpportunity);
        bool secondAccepted = secondManager.TryEvaluateOpportunity(second, out AmbientPirateRaidOpportunity secondOpportunity);
        int noSecurity = AmbientPirateRaidManager.CalculateOpportunityScore(12_000, 12, 70, 0, 0, true);
        int withSecurity = AmbientPirateRaidManager.CalculateOpportunityScore(12_000, 12, 70, 2, 0, true);
        return firstAccepted && secondAccepted &&
            firstOpportunity.Score == secondOpportunity.Score &&
            firstOpportunity.DelaySeconds == secondOpportunity.DelaySeconds &&
            firstOpportunity.RaiderCount == secondOpportunity.RaiderCount &&
            firstOpportunity.DelaySeconds is >= AmbientPirateRaidManager.MinimumDelaySeconds and <= AmbientPirateRaidManager.MaximumDelaySeconds &&
            firstOpportunity.RaiderCount is >= 1 and <= AmbientPirateRaidManager.MaximumRaidersPerRaid &&
            withSecurity < noSecurity && withSecurity >= AmbientPirateRaidManager.OpportunityThreshold
            ? Pass()
            : Fail($"deterministic={firstAccepted}/{secondAccepted}, score={firstOpportunity?.Score}/{secondOpportunity?.Score}, security={withSecurity}/{noSecurity}");
    }

    private static (bool Success, string FailureReason) LiveSurrenderIsPhysical()
    {
        Context context = new();
        AmbientPirateRaid raid = context.StartRaid();
        if (raid == null || context.Shipment.InitialManifestValue < AmbientPirateRaidManager.ExceptionalRemainingManifestValue)
            return Fail($"raid did not qualify from live shipment (raid={raid != null}, value={context.Shipment.InitialManifestValue})");

        int before = context.Shipment.RemainingQuantity;
        NpcShip raider = raid.Raiders.First().Ship;
        context.Trader.ApplyDamage(60f, NpcDestructionSource.Npc);
        for (int i = 0; i < 7 && raid.SurrenderedQuantity == 0; i++)
        {
            context.Raids.NotifyNpcDamage(raider, context.Trader, 10f);
            context.Raids.Update(1f);
        }

        bool physical = raid.SurrenderedQuantity > 0 && context.Shipment.RemainingQuantity < before &&
            context.Pods.Any(pod => pod.IsStolen && pod.PayloadType == CargoPodPayloadType.Commodity &&
                string.Equals(pod.SourceNpcName, context.Trader.Name, StringComparison.OrdinalIgnoreCase));
        bool riskRecorded = context.Risk.GetRisk(context.Route.Id, TradeLaneDirection.Forward) > 0;
        return physical && riskRecorded
            ? Pass()
            : Fail($"state={raid.State}, surrender={raid.SurrenderedQuantity}, pressure={raid.PressureSeconds:0.0}, recent={raid.RecentPressureSeconds:0.0}, evaluated={raid.SurrenderEvaluated}, remaining={context.Shipment.RemainingQuantity}/{before}, pods={context.Pods.Count}, risk={riskRecorded}");
    }

    private static (bool Success, string FailureReason) DelayedSaveLoadIsIdempotent()
    {
        Context context = new("Phase66 Save Trader");
        context.Raids.Update(0f);
        List<SaveAmbientPirateRaidData> saved = context.Raids.CaptureState();
        context.Raids.RestoreState(saved);
        bool delayed = saved.Count == 1 && context.Raids.TrackedRaidCount == 1 && context.SpawnCalls == 0;
        AmbientPirateRaid raid = context.StartRaid();
        bool activatedOnce = raid != null && context.Raids.ActiveRaidCount == 1 &&
            context.SpawnCalls == raid.Raiders.Count && context.SpawnCalls <= AmbientPirateRaidManager.MaximumRaidersPerRaid;
        return delayed && activatedOnce ? Pass() : Fail($"saved={saved.Count}, tracked={context.Raids.TrackedRaidCount}, active={context.Raids.ActiveRaidCount}, spawns={context.SpawnCalls}");
    }

    private static (bool Success, string FailureReason) PhysicalRecoveryAndDrop()
    {
        Context context = new("Phase66 Recovery Trader");
        AmbientPirateRaid raid = context.StartRaid();
        if (raid == null || raid.Raiders.Count == 0)
            return Fail("raid did not activate");

        AmbientPirateRaider raider = raid.Raiders.First();
        string commodityId = context.Shipment.Manifest.Stacks.First(stack => stack.Quantity > 0).Commodity.Id;
        raider.MutableHaul.Add(new AmbientPirateHaulEntry { CommodityId = commodityId, Quantity = 2 });
        NpcShip destroyed = raider.Ship;
        string destroyedName = destroyed.Name;
        context.Raids.NotifyNpcDestroyed(destroyed);
        bool dropped = context.Pods.Any(pod => pod.IsStolen &&
            string.Equals(pod.SourceNpcName, destroyedName, StringComparison.OrdinalIgnoreCase) &&
            pod.Quantity == 2);

        LootManager loot = new(salvageService: new CombatSalvageService(), worldObjectsProvider: () => context.Objects);
        NpcShip collector = new("Phase66 Collector", Vector3.Zero, Vector3.Zero, 500f, 0.2f, FactionManager.LibertyRogues);
        int spawned = loot.SpawnStolenCargo(collector, commodityId, 3, out _, null);
        CargoPod podToCollect = loot.ActivePods.FirstOrDefault();
        bool collected = podToCollect != null &&
            loot.TryCollectCargoPodForNpc(collector, podToCollect, 3, out string collectedCommodity, out int collectedQuantity) &&
            collectedQuantity == spawned && string.Equals(collectedCommodity, commodityId, StringComparison.OrdinalIgnoreCase) &&
            !loot.ActivePods.Contains(podToCollect);
        return dropped && spawned == 3 && collected ? Pass() : Fail($"dropped={dropped}, spawned={spawned}, collected={collected}");
    }

    private static EconomicShipment CreateDirectShipment(
        string name,
        Commodity commodity,
        int quantity,
        int risk,
        TrafficZoneConfig route)
    {
        NpcShip trader = new(name, Vector3.Zero, Vector3.Zero, 900f, 0.2f, FactionManager.NeutralCivilians);
        trader.ConfigureTrafficBehavior(
            TrafficZoneBehaviorType.TraderRoute,
            route.Id,
            Vector3.Zero,
            900f,
            190f,
            7000f,
            route.RouteStart,
            route.RouteEnd);
        EconomicShipment shipment = new(
            trader,
            route.Id,
            "origin",
            "destination",
            "Origin",
            "Destination",
            true,
            new TraderCargoManifest(new[] { new TraderCargoStack(commodity, quantity) }));
        shipment.EffectiveRouteRisk = risk;
        return shipment;
    }

    private static TrafficZoneConfig CreateTestRoute() => new()
    {
        Id = "phase66-test-route",
        Name = "Phase 66 test route",
        SystemIndex = 1,
        BehaviorType = TrafficZoneBehaviorType.TraderRoute,
        RouteStartX = -10000f,
        RouteStartY = 0f,
        RouteStartZ = 0f,
        RouteEndX = 10000f,
        RouteEndY = 0f,
        RouteEndZ = 0f
    };

    private static (bool Success, string FailureReason) Pass() => (true, string.Empty);
    private static (bool Success, string FailureReason) Fail(string reason) => (false, reason);

    private static T RunSilenced<T>(Func<T> action)
    {
        System.IO.TextWriter original = Console.Out;
        using System.IO.StringWriter writer = new();
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
