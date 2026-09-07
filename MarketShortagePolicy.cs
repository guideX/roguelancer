using System;

namespace Roguelancer
{
    /// <summary>
    /// Bounded, deterministic market shortage bands. The state is derived from
    /// a listing's configured baseline stock; the policy does not own stock.
    /// </summary>
    public enum MarketShortageLevel
    {
        Normal,
        Shortage,
        Critical
    }

    public sealed class MarketShortageState
    {
        public string StationId { get; init; } = string.Empty;
        public string StationName { get; init; } = string.Empty;
        public Commodity Commodity { get; init; }
        public int CurrentStock { get; init; }
        public int TargetStock { get; init; }
        public int Capacity { get; init; }
        public int Deficit { get; init; }
        public int StockPercent { get; init; }
        public int EnterThresholdPercent { get; init; }
        public int RecoveryThresholdPercent { get; init; }
        public MarketShortageLevel Level { get; init; }
        public bool IsShortage => Level != MarketShortageLevel.Normal;
        public bool IsMature { get; init; }
        public long AgeMilliseconds { get; init; }

        public string ConditionLabel => Level switch
        {
            MarketShortageLevel.Critical => "CRITICAL SHORTAGE",
            MarketShortageLevel.Shortage => "SHORTAGE",
            _ => "NORMAL"
        };
    }

    public static class MarketShortagePolicy
    {
        public const int EnterThresholdPercent = 20;
        public const int RecoveryThresholdPercent = 35;
        public const int CriticalThresholdPercent = 10;
        public const int MinimumDeficitUnits = 2;
        public const int MaturitySeconds = 30;
        public const int MaximumOffersPerStation = 3;

        public static bool IsBelowEnterThreshold(int stock, int targetStock)
        {
            return targetStock > 0 && stock >= 0 &&
                (long)stock * 100L <= (long)targetStock * EnterThresholdPercent;
        }

        public static bool IsRecovered(int stock, int targetStock)
        {
            return targetStock <= 0 ||
                (long)Math.Max(0, stock) * 100L >= (long)targetStock * RecoveryThresholdPercent;
        }

        public static MarketShortageLevel GetLevel(int stock, int targetStock, bool shortageLatched)
        {
            if (!shortageLatched || targetStock <= 0)
                return MarketShortageLevel.Normal;

            return (long)Math.Max(0, stock) * 100L <= (long)targetStock * CriticalThresholdPercent
                ? MarketShortageLevel.Critical
                : MarketShortageLevel.Shortage;
        }
    }
}
