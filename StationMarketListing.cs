using Roguelancer.Configuration;

namespace Roguelancer
{
    /// <summary>
    /// Runtime market listing that combines commodity data with station-specific pricing and stock.
    /// </summary>
    public class StationMarketListing
    {
        public Commodity Commodity { get; }
        public StationMarketGoodConfig Config { get; }
        public int BuyPrice { get; set; }
        public int SellPrice { get; set; }
        public int Stock { get; set; }
        public int DemandLevel { get; set; }
        public bool IsAvailable { get; set; }

        public StationMarketListing(Commodity commodity, StationMarketGoodConfig config)
        {
            Commodity = commodity;
            Config = config;
            BuyPrice = config.BuyPrice;
            SellPrice = config.SellPrice;
            Stock = config.Stock;
            DemandLevel = config.DemandLevel;
            IsAvailable = config.IsAvailable;
        }

        public StationMarketListing(Commodity commodity, int buyPrice, int sellPrice, int stock, int demandLevel, bool isAvailable)
        {
            Commodity = commodity;
            Config = new StationMarketGoodConfig();
            BuyPrice = buyPrice;
            SellPrice = sellPrice;
            Stock = stock;
            DemandLevel = demandLevel;
            IsAvailable = isAvailable;
        }
    }
}
