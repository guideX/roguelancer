using Microsoft.Xna.Framework;
using Roguelancer.Configuration;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Roguelancer
{
    /// <summary>
    /// Focused Phase 61 coverage for real stock consumption, deterministic
    /// lazy timing, save/load state, shortage integration, and the shared
    /// Phase 59 shipment authority.
    /// </summary>
    public sealed class Phase61MarketConsumptionSmokeTest
    {
        private sealed class Context
        {
            public MarketManager Market { get; } = RunSilenced(() => new MarketManager());
            public Station FortBush { get; } = CreateStation("Fort Bush", 6000f, 600f, -4500f);
            public Station Newark { get; } = CreateStation("Newark Station", -7500f, -900f, 6000f);
            public Station Riverside { get; } = CreateStation("Riverside Station", 12000f, 900f, 9000f);
            public Commodity Food { get; } = CommodityCatalog.GetById("food-rations");
            public Commodity Water { get; } = CommodityCatalog.GetById("water");
            public Commodity HFuel { get; } = CommodityCatalog.GetById("h-fuel");
            public Commodity Diamonds { get; } = CommodityCatalog.GetById("diamonds");
            public Commodity Contraband { get; } = CommodityCatalog.GetById("side-arms");

            public IReadOnlyList<Station> Stations => new[] { FortBush, Newark, Riverside };
        }

        public (int Passed, int Failed) Run()
        {
            int passed = 0;
            int failed = 0;
            RunCase(EligibleListingConsumesAuthoritativeStock, "eligible listing consumes real stock", ref passed, ref failed);
            RunCase(ContrabandAndZeroRateAreExcluded, "contraband and zero-rate listings are excluded", ref passed, ref failed);
            RunCase(FractionalProgressIsDeterministic, "fractional demand accumulates", ref passed, ref failed);
            RunCase(ZeroTimeAndStockFloorAreSafe, "zero time and stock floor are safe", ref passed, ref failed);
            RunCase(CatchUpIsBounded, "large elapsed time is bounded", ref passed, ref failed);
            RunCase(StationAndCommodityIsolation, "station and commodity isolation hold", ref passed, ref failed);
            RunCase(SaveLoadPreservesConsumptionProgress, "save/load preserves consumption progress", ref passed, ref failed);
            RunCase(OfflineLoadDoesNotDrain, "offline absence does not drain stock", ref passed, ref failed);
            RunCase(ResetRestoresBaselineConsumptionState, "reset restores baseline demand state", ref passed, ref failed);
            RunCase(ConsumptionCrossesShortageThreshold, "consumption crosses shortage threshold", ref passed, ref failed);
            RunCase(HysteresisAndMaturityRemainPhase60Compatible, "Phase 60 hysteresis and maturity remain intact", ref passed, ref failed);
            RunCase(NpcDeliveryUsesSharedStock, "NPC delivery replenishes shared stock", ref passed, ref failed);
            RunCase(FailedShipmentLeavesConsumptionDrivenShortage, "failed shipment leaves shortage maturing", ref passed, ref failed);
            RunCase(HealthyDeliveryOutpacesOneConsumptionWindow, "successful delivery can offset consumption", ref passed, ref failed);
            Console.WriteLine($"[PHASE 61 MARKET CONSUMPTION SMOKE] RESULT: {passed} passed, {failed} failed");
            return (passed, failed);
        }

        private (bool Success, string FailureReason) EligibleListingConsumesAuthoritativeStock()
        {
            Context c = new();
            StationMarketListing before = c.Market.GetListingForCommodity(c.Newark, c.Food);
            if (before == null || before.ConsumptionRatePerMinute <= 0)
                return Fail("configured food listing did not receive a positive bounded rate");

            c.Market.AdvanceTime(300d);
            StationMarketListing after = c.Market.GetListingForCommodity(c.Newark, c.Food);
            return after.Stock < before.Stock
                ? Pass()
                : Fail($"real stock did not consume during economic time: {before.Stock}->{after.Stock}");
        }

        private (bool Success, string FailureReason) ContrabandAndZeroRateAreExcluded()
        {
            Context c = new();
            int contrabandBefore = c.Market.GetListingForCommodity(c.Riverside, c.Contraband, MarketSurface.BlackMarket).Stock;
            int durableBefore = c.Market.GetListingForCommodity(c.Newark, c.Diamonds).Stock;
            c.Market.AdvanceTime(600d);
            int contrabandAfter = c.Market.GetListingForCommodity(c.Riverside, c.Contraband, MarketSurface.BlackMarket).Stock;
            int durableAfter = c.Market.GetListingForCommodity(c.Newark, c.Diamonds).Stock;
            return contrabandBefore == contrabandAfter && durableBefore == durableAfter
                ? Pass()
                : Fail("lawful demand touched contraband or an explicit zero-rate listing");
        }

        private (bool Success, string FailureReason) FractionalProgressIsDeterministic()
        {
            Context one = new();
            Context two = new();
            StationMarketListing baseline = one.Market.GetListingForCommodity(one.Newark, one.Food);
            one.Market.AdvanceTime(60d);
            StationMarketListing oneMinute = one.Market.GetListingForCommodity(one.Newark, one.Food);
            if (oneMinute.Stock != baseline.Stock || oneMinute.ConsumptionRemainder <= 0)
                return Fail("fractional progress was rounded away");

            one.Market.AdvanceTime(60d);
            two.Market.AdvanceTime(120d);
            StationMarketListing oneTwoMinutes = one.Market.GetListingForCommodity(one.Newark, one.Food);
            StationMarketListing twoMinutes = two.Market.GetListingForCommodity(two.Newark, two.Food);
            return oneTwoMinutes.Stock == twoMinutes.Stock &&
                oneTwoMinutes.ConsumptionRemainder == twoMinutes.ConsumptionRemainder
                ? Pass()
                : Fail($"fractional consumption depended on update chunking: {oneTwoMinutes.Stock}/{oneTwoMinutes.ConsumptionRemainder} vs {twoMinutes.Stock}/{twoMinutes.ConsumptionRemainder}");
        }

        private (bool Success, string FailureReason) ZeroTimeAndStockFloorAreSafe()
        {
            Context c = new();
            StationMarketListing initial = c.Market.GetListingForCommodity(c.Newark, c.Food);
            c.Market.AdvanceTime(0d);
            if (c.Market.GetListingForCommodity(c.Newark, c.Food).Stock != initial.Stock)
                return Fail("zero elapsed time consumed stock");

            for (int i = 0; i < 100; i++)
            {
                c.Market.AdvanceTime(300d);
                _ = c.Market.GetListingForCommodity(c.Newark, c.Food);
            }

            StationMarketListing drained = c.Market.GetListingForCommodity(c.Newark, c.Food);
            return drained.Stock >= 0 && drained.Stock <= drained.MaximumStock
                ? Pass()
                : Fail("consumption crossed the absolute stock bounds");
        }

        private (bool Success, string FailureReason) CatchUpIsBounded()
        {
            Context c = new();
            StationMarketListing initial = c.Market.GetListingForCommodity(c.Newark, c.Food);
            c.Market.AdvanceTime(10_000d);
            StationMarketListing after = c.Market.GetListingForCommodity(c.Newark, c.Food);
            int expectedMaximumConsumption = (int)Math.Floor(after.ConsumptionRatePerMinute * 5d);
            return initial.Stock - after.Stock <= expectedMaximumConsumption &&
                initial.Stock - after.Stock >= 0
                ? Pass()
                : Fail($"catch-up exceeded five economic minutes: {initial.Stock}->{after.Stock}");
        }

        private (bool Success, string FailureReason) StationAndCommodityIsolation()
        {
            Context c = new();
            StationMarketListing fortFood = c.Market.GetListingForCommodity(c.FortBush, c.Food);
            StationMarketListing fortWater = c.Market.GetListingForCommodity(c.FortBush, c.Water);
            StationMarketListing newarkFood = c.Market.GetListingForCommodity(c.Newark, c.Food);
            c.Market.AdvanceTime(600d);
            int fortFoodAfter = c.Market.GetListingForCommodity(c.FortBush, c.Food).Stock;
            int fortWaterAfter = c.Market.GetListingForCommodity(c.FortBush, c.Water).Stock;
            int newarkFoodAfter = c.Market.GetListingForCommodity(c.Newark, c.Food).Stock;
            return fortFoodAfter < fortFood.Stock && fortWaterAfter < fortWater.Stock &&
                newarkFoodAfter < newarkFood.Stock && fortFoodAfter != newarkFoodAfter
                ? Pass()
                : Fail("consumption leaked across station or commodity listings");
        }

        private (bool Success, string FailureReason) SaveLoadPreservesConsumptionProgress()
        {
            Context source = new();
            StationMarketListing initial = source.Market.GetListingForCommodity(source.Newark, source.Food);
            source.Market.AdvanceTime(60d);
            StationMarketListing progressed = source.Market.GetListingForCommodity(source.Newark, source.Food);
            List<SaveMarketStateData> saved = source.Market.CaptureRuntimeState();

            Context restored = new();
            restored.Market.RestoreElapsedMilliseconds(source.Market.ElapsedMilliseconds);
            restored.Market.RestoreRuntimeState(saved);
            StationMarketListing afterLoad = restored.Market.GetListingForCommodity(restored.Newark, restored.Food);
            if (afterLoad.Stock != progressed.Stock || afterLoad.ConsumptionRemainder != progressed.ConsumptionRemainder)
                return Fail("consumption state did not round-trip");

            restored.Market.AdvanceTime(60d);
            source.Market.AdvanceTime(60d);
            StationMarketListing sourceAfter = source.Market.GetListingForCommodity(source.Newark, source.Food);
            StationMarketListing restoredAfter = restored.Market.GetListingForCommodity(restored.Newark, restored.Food);
            return sourceAfter.Stock == restoredAfter.Stock && sourceAfter.Stock <= initial.Stock
                ? Pass()
                : Fail("save/load duplicated or lost fractional consumption");
        }

        private (bool Success, string FailureReason) OfflineLoadDoesNotDrain()
        {
            Context source = new();
            source.Market.AdvanceTime(300d);
            StationMarketListing savedListing = source.Market.GetListingForCommodity(source.Newark, source.Food);
            List<SaveMarketStateData> saved = source.Market.CaptureRuntimeState();

            Context restored = new();
            restored.Market.RestoreElapsedMilliseconds(source.Market.ElapsedMilliseconds + 7 * 24 * 60 * 60 * 1000L);
            restored.Market.RestoreRuntimeState(saved);
            StationMarketListing loaded = restored.Market.GetListingForCommodity(restored.Newark, restored.Food);
            return loaded.Stock == savedListing.Stock
                ? Pass()
                : Fail($"wall-clock absence caused unexpected market drain: saved {savedListing.Stock}, loaded {loaded.Stock}");
        }

        private (bool Success, string FailureReason) ResetRestoresBaselineConsumptionState()
        {
            Context c = new();
            StationMarketListing initial = c.Market.GetListingForCommodity(c.Newark, c.Food);
            c.Market.AdvanceTime(300d);
            if (c.Market.GetListingForCommodity(c.Newark, c.Food).Stock >= initial.Stock)
                return Fail("reset fixture did not consume stock");

            c.Market.ResetRuntimeState();
            StationMarketListing reset = c.Market.GetListingForCommodity(c.Newark, c.Food);
            return reset.Stock == reset.BaselineStock && reset.ConsumptionRemainder == 0
                ? Pass()
                : Fail("reset retained stale stock or fractional demand");
        }

        private (bool Success, string FailureReason) ConsumptionCrossesShortageThreshold()
        {
            Context c = new();
            StationMarketListing initial = c.Market.GetListingForCommodity(c.Newark, c.Food);
            for (int i = 0; i < 100; i++)
            {
                c.Market.AdvanceTime(300d);
                _ = c.Market.GetListingForCommodity(c.Newark, c.Food);
            }

            MarketShortageState state = c.Market.GetShortageState(c.Newark, c.Food);
            return state?.IsShortage == true && state.CurrentStock < initial.Stock
                ? Pass()
                : Fail("real consumption did not create a stock-derived shortage");
        }

        private (bool Success, string FailureReason) HysteresisAndMaturityRemainPhase60Compatible()
        {
            Context c = new();
            if (!c.Market.TryRemoveSupply(c.Newark, c.Food, 180, 0, out _))
                return Fail("could not stage a real shortage");
            MarketShortageState entered = c.Market.GetShortageState(c.Newark, c.Food);
            if (entered?.EnterThresholdPercent != 20 || entered.IsMature)
                return Fail("shortage did not preserve Phase 60 entry/maturity rules");

            c.Market.AdvanceTime(MarketShortagePolicy.MaturitySeconds + 1d);
            MarketShortageState mature = c.Market.GetShortageState(c.Newark, c.Food);
            if (mature?.IsMature != true)
                return Fail("shortage did not mature after 30 seconds");

            int needed = Math.Max(0, (int)Math.Ceiling(mature.TargetStock * MarketShortagePolicy.RecoveryThresholdPercent / 100d) - mature.CurrentStock);
            if (!c.Market.TryAddSupply(c.Newark, c.Food, needed, out _))
                return Fail("could not stage hysteresis recovery");
            MarketShortageState recovered = c.Market.GetShortageState(c.Newark, c.Food);
            return recovered?.IsShortage == false && recovered.RecoveryThresholdPercent == 35
                ? Pass()
                : Fail("shortage did not clear at the Phase 60 recovery threshold");
        }

        private (bool Success, string FailureReason) NpcDeliveryUsesSharedStock()
        {
            Context c = new();
            if (!c.Market.TryRemoveSupply(c.Newark, c.Food, 180, 0, out _))
                return Fail("could not stage destination demand");

            EconomicShipmentManager economy = CreateEconomy(c, out NpcShip trader, out TrafficZoneConfig route);
            if (!economy.TryAttachTrader(trader, route, out EconomicShipment shipment))
                return Fail("Phase 59 could not create a real supply shipment");
            TraderCargoStack food = shipment.Manifest.Stacks.FirstOrDefault(stack => stack.Commodity == c.Food);
            if (food == null)
                return Fail("shortage-aware selection did not consider destination food demand");

            int before = c.Market.GetListingForCommodity(c.Newark, c.Food).Stock;
            if (!economy.TryDeliver(trader))
                return Fail("real shipment could not deliver");
            int after = c.Market.GetListingForCommodity(c.Newark, c.Food).Stock;
            return after > before ? Pass() : Fail("NPC delivery did not increase authoritative destination stock");
        }

        private (bool Success, string FailureReason) FailedShipmentLeavesConsumptionDrivenShortage()
        {
            Context c = new();
            if (!c.Market.TryRemoveSupply(c.Newark, c.Food, 180, 0, out _))
                return Fail("could not stage failed-delivery demand");
            int before = c.Market.GetListingForCommodity(c.Newark, c.Food).Stock;
            _ = c.Market.GetShortageState(c.Newark, c.Food);
            EconomicShipmentManager economy = CreateEconomy(c, out NpcShip trader, out TrafficZoneConfig route);
            if (!economy.TryAttachTrader(trader, route, out EconomicShipment shipment))
                return Fail("Phase 59 shipment could not be staged");
            economy.NotifyTraderDestroyed(trader);
            economy.FinalizeDestroyedTrader(trader);
            c.Market.AdvanceTime(MarketShortagePolicy.MaturitySeconds + 1d);
            MarketShortageState state = c.Market.GetShortageState(c.Newark, c.Food);
            return c.Market.GetListingForCommodity(c.Newark, c.Food).Stock >= before &&
                state?.IsShortage == true && state.CurrentStock < state.TargetStock * MarketShortagePolicy.RecoveryThresholdPercent / 100 &&
                shipment.Settlement == EconomicShipmentSettlement.Lost && state.IsMature
                ? Pass()
                : Fail($"failed logistics did not leave the consumption-driven shortage maturing: stock {c.Market.GetListingForCommodity(c.Newark, c.Food).Stock}/{before}, settlement {shipment.Settlement}, state {state?.CurrentStock}/{state?.AgeMilliseconds}/{state?.IsMature}");
        }

        private (bool Success, string FailureReason) HealthyDeliveryOutpacesOneConsumptionWindow()
        {
            Context c = new();
            IReadOnlyList<StationMarketListing> initial = c.Market.GetListingsForStation(c.Newark);
            c.Market.AdvanceTime(300d);
            IReadOnlyList<StationMarketListing> consumed = c.Market.GetListingsForStation(c.Newark);
            EconomicShipmentManager economy = CreateEconomy(c, out NpcShip trader, out TrafficZoneConfig route);
            if (!economy.TryAttachTrader(trader, route, out EconomicShipment shipment))
                return Fail("healthy route could not create a shipment");
            TraderCargoStack selected = shipment.Manifest.Stacks.FirstOrDefault();
            if (selected?.Commodity == null)
                return Fail("healthy route did not carry a commodity");
            int consumedStock = consumed.First(listing => listing.Commodity == selected.Commodity).Stock;
            int initialStock = initial.First(listing => listing.Commodity == selected.Commodity).Stock;
            economy.TryDeliver(trader);
            int replenishedStock = c.Market.GetListingForCommodity(c.Newark, selected.Commodity).Stock;
            return consumedStock < initialStock && replenishedStock > consumedStock &&
                c.Market.GetShortageState(c.Newark, selected.Commodity)?.IsShortage != true
                ? Pass()
                : Fail("successful logistics did not offset the consumption window");
        }

        private static EconomicShipmentManager CreateEconomy(Context c, out NpcShip trader, out TrafficZoneConfig route)
        {
            route = new TrafficZoneConfig
            {
                Id = "phase61-consumption-route",
                Name = "Phase 61 Consumption Route",
                BehaviorType = TrafficZoneBehaviorType.TraderRoute,
                OriginStationId = "fort_bush",
                DestinationStationId = "newark_station",
                RouteStartX = c.FortBush.Position.X,
                RouteStartY = c.FortBush.Position.Y,
                RouteStartZ = c.FortBush.Position.Z,
                RouteEndX = c.Newark.Position.X,
                RouteEndY = c.Newark.Position.Y,
                RouteEndZ = c.Newark.Position.Z
            };
            trader = new NpcShip("Phase 61 Trader", c.FortBush.Position, Vector3.Zero, 1000f, 0.1f, FactionManager.NeutralCivilians);
            trader.ConfigureTrafficBehavior(
                TrafficZoneBehaviorType.TraderRoute,
                route.Id,
                Vector3.Zero,
                1000f,
                190f,
                3500f,
                route.RouteStart,
                route.RouteEnd);
            return new EconomicShipmentManager(c.Market, () => c.Stations);
        }

        private static Station CreateStation(string name, float x, float y, float z)
        {
            return new Station(new StationConfig
            {
                Description = name,
                StartupPositionX = x,
                StartupPositionY = y,
                StartupPositionZ = z
            }, null);
        }

        private void RunCase(Func<(bool Success, string FailureReason)> test, string label, ref int passed, ref int failed)
        {
            try
            {
                (bool Success, string FailureReason) result = RunSilenced(test);
                if (result.Success)
                {
                    passed++;
                    Console.WriteLine($"[PHASE 61 MARKET CONSUMPTION SMOKE] PASS {label}");
                }
                else
                {
                    failed++;
                    Console.WriteLine($"[PHASE 61 MARKET CONSUMPTION SMOKE] FAIL {label}: {result.FailureReason}");
                }
            }
            catch (Exception ex)
            {
                failed++;
                Console.WriteLine($"[PHASE 61 MARKET CONSUMPTION SMOKE] FAIL {label}: {ex.Message}");
            }
        }

        private static (bool Success, string FailureReason) Pass() => (true, string.Empty);
        private static (bool Success, string FailureReason) Fail(string reason) => (false, reason);

        private static T RunSilenced<T>(Func<T> action)
        {
            TextWriter original = Console.Out;
            using StringWriter writer = new();
            Console.SetOut(writer);
            try { return action(); }
            finally { Console.SetOut(original); }
        }
    }
}
