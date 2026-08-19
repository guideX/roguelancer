using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Roguelancer.Configuration
{
    /// <summary>
    /// Configures a station-specific commodity market.
    /// </summary>
    public class StationMarketConfig
    {
        [JsonPropertyName("station_id")]
        public string StationId { get; set; } = string.Empty;

        [JsonPropertyName("station_name")]
        public string StationName { get; set; } = string.Empty;

        [JsonPropertyName("faction_id")]
        public string FactionId { get; set; } = string.Empty;

        [JsonPropertyName("goods")]
        public List<StationMarketGoodConfig> Goods { get; set; } = new();
    }

    /// <summary>
    /// Commodity entry inside a station market.
    /// </summary>
    public class StationMarketGoodConfig
    {
        [JsonPropertyName("commodity_id")]
        public string CommodityId { get; set; } = string.Empty;

        [JsonPropertyName("buy_price")]
        public int BuyPrice { get; set; }

        [JsonPropertyName("sell_price")]
        public int SellPrice { get; set; }

        [JsonPropertyName("stock")]
        public int Stock { get; set; }

        [JsonPropertyName("demand_level")]
        public int DemandLevel { get; set; }

        [JsonPropertyName("is_available")]
        public bool IsAvailable { get; set; } = true;

        /// <summary>
        /// Optional lower stock bound. Configured stock is the normal/equilibrium
        /// stock, not a price multiplier.
        /// </summary>
        [JsonPropertyName("minimum_stock")]
        public int? MinimumStock { get; set; }

        /// <summary>
        /// Optional practical inventory ceiling for player sales.
        /// </summary>
        [JsonPropertyName("maximum_stock")]
        public int? MaximumStock { get; set; }

        /// <summary>
        /// Seconds required to recover one full stock gap toward baseline.
        /// </summary>
        [JsonPropertyName("recovery_seconds")]
        public int RecoverySeconds { get; set; }
    }
}
