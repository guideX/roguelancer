using System;

namespace Roguelancer
{
    public enum MarketOpportunityType
    {
        Shortage,
        Surplus,
        Pairing
    }

    /// <summary>
    /// Read-only display data for a bounded, live market intelligence list.
    /// It contains no authority over market state or mission state.
    /// </summary>
    public sealed class MarketOpportunity
    {
        public MarketOpportunity(
            MarketOpportunityType type,
            Commodity commodity,
            string stationName,
            string originStationName,
            string destinationStationName,
            int score,
            int quantity,
            string reason,
            int currentSpread)
        {
            Type = type;
            CommodityId = commodity?.Id ?? string.Empty;
            CommodityName = commodity?.Name ?? string.Empty;
            StationName = stationName ?? string.Empty;
            OriginStationName = originStationName ?? string.Empty;
            DestinationStationName = destinationStationName ?? string.Empty;
            Score = Math.Max(0, score);
            Quantity = Math.Max(0, quantity);
            Reason = reason ?? string.Empty;
            CurrentSpread = currentSpread;
        }

        public MarketOpportunityType Type { get; }
        public string CommodityId { get; }
        public string CommodityName { get; }
        public string StationName { get; }
        public string OriginStationName { get; }
        public string DestinationStationName { get; }
        public int Score { get; }
        public int Quantity { get; }
        public string Reason { get; }
        public int CurrentSpread { get; }

        public string GetDisplayText()
        {
            return Type switch
            {
                MarketOpportunityType.Pairing =>
                    $"{CommodityName}: {OriginStationName} -> {DestinationStationName}",
                _ => $"{CommodityName} - {Reason} - {StationName}"
            };
        }
    }
}
