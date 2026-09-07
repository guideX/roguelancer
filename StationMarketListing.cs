using Roguelancer.Configuration;
using System;

namespace Roguelancer
{
    /// <summary>
    /// Runtime market listing that combines commodity data with station-specific pricing and stock.
    /// </summary>
    public class StationMarketListing
    {
        public const int DefaultRecoverySeconds = 3600;
        public const int ConsumptionScale = 1_000;
        public const int MinimumConsumptionRateMilliUnitsPerMinute = 250;
        public const int MaximumConsumptionRateMilliUnitsPerMinute = 2_000;

        public Commodity Commodity { get; }
        public StationMarketGoodConfig Config { get; }
        public int BuyPrice { get; internal set; }
        public int SellPrice { get; internal set; }
        public int Stock { get; internal set; }
        public int DemandLevel { get; internal set; }
        public bool IsAvailable { get; internal set; }

        /// <summary>
        /// Station-configured normal stock. Dynamic prices move around this value.
        /// </summary>
        public int BaselineStock { get; internal set; }

        public int MinimumStock { get; internal set; }
        public int MaximumStock { get; internal set; }
        public int BaseBuyPrice { get; internal set; }
        public int BaseSellPrice { get; internal set; }
        public int RecoverySeconds { get; internal set; }

        /// <summary>
        /// Fixed-point autonomous demand rate. The value is in milli-units
        /// per economic minute so repeated lazy updates are deterministic.
        /// </summary>
        public int ConsumptionRateMilliUnitsPerMinute { get; internal set; }

        public double ConsumptionRatePerMinute =>
            ConsumptionRateMilliUnitsPerMinute / (double)ConsumptionScale;

        /// <summary>
        /// Numerator remainder for fixed-point consumption:
        /// rate_milli_units_per_minute * elapsed_ms. It is always below the
        /// milliseconds-per-minute denominator after an update.
        /// </summary>
        public long ConsumptionRemainder { get; internal set; }

        /// <summary>
        /// Whether ordinary transaction-driven recovery is still active. A
        /// consumed listing becomes demand-driven until a later delivery or
        /// player transaction explicitly supplies stock again.
        /// </summary>
        internal bool RecoveryEnabled { get; set; }

        /// <summary>
        /// Fixed-point remainder used by deterministic lazy recovery.
        /// </summary>
        public long RecoveryRemainderMilliseconds { get; internal set; }

        /// <summary>
        /// Prevents a buy from becoming an immediate same-station profit after
        /// its own stock impact is applied. It is reset after a successful sale.
        /// </summary>
        public int ImmediateSellPriceCeiling { get; internal set; }

        internal long LastAdvancedMilliseconds { get; set; }

        public int BuyPriceMovementPercent => GetPriceMovementPercent(BuyPrice, BaseBuyPrice);
        public int SellPriceMovementPercent => GetPriceMovementPercent(SellPrice, BaseSellPrice);

        public string MarketCondition
        {
            get
            {
                if (!IsAvailable || BaselineStock <= 0)
                {
                    return "UNAVAILABLE";
                }

                long stockPercent = (long)Stock * 100L / BaselineStock;
                if (stockPercent < 25) return "SHORTAGE";
                if (stockPercent < 75) return "LOW SUPPLY";
                if (stockPercent <= 125) return "NORMAL";
                if (stockPercent <= 175) return "HIGH SUPPLY";
                return "SURPLUS";
            }
        }

        public StationMarketListing(Commodity commodity, StationMarketGoodConfig config)
        {
            Commodity = commodity;
            Config = config ?? new StationMarketGoodConfig();
            BaseBuyPrice = Math.Max(0, Config.BuyPrice);
            BaseSellPrice = Math.Max(0, Config.SellPrice);
            BaselineStock = Math.Clamp(Config.Stock, 0, 1_000_000);
            MinimumStock = Math.Clamp(Config.MinimumStock ?? 0, 0, BaselineStock);
            MaximumStock = Config.MaximumStock.HasValue
                ? Math.Clamp(Config.MaximumStock.Value, BaselineStock, 1_000_000)
                : CalculateDefaultMaximumStock(BaselineStock);
            RecoverySeconds = Config.RecoverySeconds > 0 ? Config.RecoverySeconds : DefaultRecoverySeconds;
            ConsumptionRateMilliUnitsPerMinute = ResolveConsumptionRateMilliUnits(Config);
            BuyPrice = BaseBuyPrice;
            SellPrice = BaseSellPrice;
            Stock = Math.Clamp(BaselineStock, MinimumStock, MaximumStock);
            DemandLevel = Math.Max(0, Config.DemandLevel);
            IsAvailable = Config.IsAvailable;
            RecoveryEnabled = true;
        }

        public StationMarketListing(Commodity commodity, int buyPrice, int sellPrice, int stock, int demandLevel, bool isAvailable)
            : this(commodity, new StationMarketGoodConfig
            {
                CommodityId = commodity?.Id ?? string.Empty,
                BuyPrice = buyPrice,
                SellPrice = sellPrice,
                Stock = Math.Max(0, stock),
                DemandLevel = demandLevel,
                IsAvailable = isAvailable
            })
        {
        }

        internal StationMarketListing(StationMarketListing source)
        {
            Commodity = source?.Commodity;
            Config = source?.Config ?? new StationMarketGoodConfig();
            BuyPrice = source?.BuyPrice ?? 0;
            SellPrice = source?.SellPrice ?? 0;
            Stock = source?.Stock ?? 0;
            DemandLevel = source?.DemandLevel ?? 0;
            IsAvailable = source?.IsAvailable ?? false;
            BaselineStock = source?.BaselineStock ?? 0;
            MinimumStock = source?.MinimumStock ?? 0;
            MaximumStock = source?.MaximumStock ?? 0;
            BaseBuyPrice = source?.BaseBuyPrice ?? 0;
            BaseSellPrice = source?.BaseSellPrice ?? 0;
            RecoverySeconds = source?.RecoverySeconds ?? DefaultRecoverySeconds;
            ConsumptionRateMilliUnitsPerMinute = source?.ConsumptionRateMilliUnitsPerMinute ?? 0;
            ConsumptionRemainder = source?.ConsumptionRemainder ?? 0;
            RecoveryRemainderMilliseconds = source?.RecoveryRemainderMilliseconds ?? 0;
            ImmediateSellPriceCeiling = source?.ImmediateSellPriceCeiling ?? 0;
            LastAdvancedMilliseconds = source?.LastAdvancedMilliseconds ?? 0;
            RecoveryEnabled = source?.RecoveryEnabled ?? true;
        }

        private static int ResolveConsumptionRateMilliUnits(StationMarketGoodConfig config)
        {
            if (config?.ConsumptionRatePerMinute.HasValue == true)
            {
                double explicitRate = config.ConsumptionRatePerMinute.Value;
                if (double.IsNaN(explicitRate) || double.IsInfinity(explicitRate) || explicitRate <= 0d)
                    return 0;

                return Math.Clamp(
                    (int)Math.Round(explicitRate * ConsumptionScale, MidpointRounding.AwayFromZero),
                    MinimumConsumptionRateMilliUnitsPerMinute,
                    MaximumConsumptionRateMilliUnitsPerMinute);
            }

            if (config == null || !config.IsAvailable || config.Stock <= 0)
                return 0;

            // Stable, data-driven default: stock scale and configured demand
            // level influence the rate, while every market remains bounded.
            double defaultRate = 0.25d + config.Stock / 1_200d + config.DemandLevel / 100d;
            return Math.Clamp(
                (int)Math.Round(defaultRate * ConsumptionScale, MidpointRounding.AwayFromZero),
                MinimumConsumptionRateMilliUnitsPerMinute,
                MaximumConsumptionRateMilliUnitsPerMinute);
        }

        private static int CalculateDefaultMaximumStock(int baselineStock)
        {
            if (baselineStock <= 0)
            {
                return 0;
            }

            long practicalMaximum = Math.Max((long)baselineStock * 4L, (long)baselineStock + 100L);
            return (int)Math.Min(1_000_000L, practicalMaximum);
        }

        private static int GetPriceMovementPercent(int currentPrice, int basePrice)
        {
            if (basePrice <= 0)
            {
                return 0;
            }

            return (int)Math.Round((currentPrice - basePrice) * 100m / basePrice, MidpointRounding.AwayFromZero);
        }
    }
}
