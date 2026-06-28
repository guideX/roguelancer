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
    }
}
