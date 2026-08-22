using System;

namespace Roguelancer
{
    public enum MarketOpportunityType
    {
        Shortage,
        Surplus,
        TradeRoute,
        Pairing = TradeRoute
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
            int currentSpread,
            string originStationId = "",
            string destinationStationId = "",
            string sourceAgeBand = "",
            string destinationAgeBand = "",
            long routeDistanceUnits = 0,
            int routeHops = 0)
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
            OriginStationId = originStationId ?? string.Empty;
            DestinationStationId = destinationStationId ?? string.Empty;
            SourceAgeBand = sourceAgeBand ?? string.Empty;
            DestinationAgeBand = destinationAgeBand ?? string.Empty;
            RouteDistanceUnits = Math.Max(0L, routeDistanceUnits);
            RouteHops = Math.Max(0, routeHops);
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
        public string OriginStationId { get; }
        public string DestinationStationId { get; }
        public string SourceAgeBand { get; }
        public string DestinationAgeBand { get; }
        public long RouteDistanceUnits { get; }
        public int RouteHops { get; }

        public string GetDisplayText()
        {
            return Type switch
            {
                MarketOpportunityType.TradeRoute =>
                    $"[TRADE] {CommodityName} | {OriginStationName} -> {DestinationStationName} | {FormatSpread()} | {FormatRoute()} | {FormatIntel()}",
                MarketOpportunityType.Shortage => $"[SHORTAGE] {CommodityName} | {StationName} | {Reason}",
                MarketOpportunityType.Surplus => $"[SURPLUS] {CommodityName} | {StationName} | {Reason}",
                _ => $"[{Type.ToString().ToUpperInvariant()}] {CommodityName} | {StationName}"
            };
        }

        public string GetTypeLabel() => Type switch
        {
            MarketOpportunityType.TradeRoute => "TRADE",
            MarketOpportunityType.Shortage => "SHORTAGE",
            MarketOpportunityType.Surplus => "SURPLUS",
            _ => Type.ToString().ToUpperInvariant()
        };

        private string FormatSpread() => CurrentSpread > 0
            ? $"+{CurrentSpread:N0} CR/unit"
            : "SPREAD UNKNOWN";

        private string FormatIntel() => string.IsNullOrWhiteSpace(SourceAgeBand) && string.IsNullOrWhiteSpace(DestinationAgeBand)
            ? "MARKET DATA UNKNOWN"
            : $"{(string.IsNullOrWhiteSpace(SourceAgeBand) ? "UNKNOWN" : SourceAgeBand)}/{(string.IsNullOrWhiteSpace(DestinationAgeBand) ? "UNKNOWN" : DestinationAgeBand)}";

        private string FormatRoute()
        {
            string route = RouteHops > 0
                ? $"{RouteHops} jump{(RouteHops == 1 ? string.Empty : "s")}"
                : RouteDistanceUnits > 0
                    ? $"{RouteDistanceUnits / 1000f:0.0}k route units"
                    : "ROUTE UNKNOWN";
            return route;
        }
    }
}
