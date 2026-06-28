using System;
using System.Collections.Generic;
using System.Linq;

namespace Roguelancer
{
    /// <summary>
    /// Manages commodity trading at stations.
    /// </summary>
    public class CommodityDealer
    {
        private readonly MarketManager _marketManager;
        private Station _currentStation;

        public CommodityDealer()
        {
            _marketManager = new MarketManager();
        }

        /// <summary>
        /// Station currently hosting the market UI.
        /// </summary>
        public Station CurrentStation => _currentStation;

        /// <summary>
        /// Legacy commodity list fallback for callers that still expect a simple inventory.
        /// </summary>
        public IReadOnlyList<Commodity> AvailableCommodities => GetCurrentMarketCommodities();

        public IReadOnlyList<StationMarketListing> CurrentMarketListings => GetCurrentListings();

        public void SetDockedStation(Station station)
        {
            _currentStation = station;
            if (station != null)
            {
                Console.WriteLine($"[MARKET] Docked market context set to {station.Name}");
            }
        }

        public void ClearDockedStation()
        {
            if (_currentStation != null)
            {
                Console.WriteLine($"[MARKET] Clearing market context for {_currentStation.Name}");
            }

            _currentStation = null;
        }

        public IReadOnlyList<Commodity> GetCurrentMarketCommodities()
        {
            return GetCurrentListings().Select(listing => listing.Commodity).ToList();
        }

        public IReadOnlyList<StationMarketListing> GetCurrentListings()
        {
            if (_currentStation == null)
            {
                return _marketManager.GetListingsForStation(null);
            }

            return _marketManager.GetListingsForStation(_currentStation);
        }

        public StationMarketListing GetListingByIndex(int index)
        {
            var listings = GetCurrentListings();
            if (index < 0 || index >= listings.Count)
            {
                return null;
            }

            return listings[index];
        }

        public Commodity GetCommodityByName(string name)
        {
            return CommodityCatalog.GetByName(name);
        }

        public Commodity GetCommodityByIndex(int index)
        {
            var listing = GetListingByIndex(index);
            return listing?.Commodity;
        }

        public Dictionary<Commodity, int> GetCommodityRegistry()
        {
            return _marketManager.GetCommodityRegistry();
        }

        public bool CanAfford(Commodity commodity, int quantity, PlayerCredits credits)
        {
            var listing = ResolveListing(commodity);
            if (listing == null)
            {
                return false;
            }

            int totalCost = listing.BuyPrice * quantity;
            return credits.CanAfford(totalCost);
        }

        public bool HasSpace(Commodity commodity, int quantity, CargoHold cargoHold)
        {
            return cargoHold.CanFit(commodity, quantity);
        }

        public bool BuyCommodity(Commodity commodity, int quantity, PlayerCredits credits, CargoHold cargoHold)
        {
            if (_currentStation == null || !_marketManager.HasMarketConfigForStation(_currentStation))
            {
                return BuyWithFallback(commodity, quantity, credits, cargoHold);
            }

            bool success = _marketManager.TryBuy(_currentStation, commodity, quantity, credits, cargoHold, out string message);
            if (!string.IsNullOrWhiteSpace(message))
            {
                Console.WriteLine($"[MARKET] {message}");
            }

            return success;
        }

        public bool SellCommodity(Commodity commodity, int quantity, PlayerCredits credits, CargoHold cargoHold)
        {
            if (_currentStation == null || !_marketManager.HasMarketConfigForStation(_currentStation))
            {
                return SellWithFallback(commodity, quantity, credits, cargoHold);
            }

            bool success = _marketManager.TrySell(_currentStation, commodity, quantity, credits, cargoHold, out string message);
            if (!string.IsNullOrWhiteSpace(message))
            {
                Console.WriteLine($"[MARKET] {message}");
            }

            return success;
        }

        private StationMarketListing ResolveListing(Commodity commodity)
        {
            if (commodity == null)
            {
                return null;
            }

            return GetCurrentListings().FirstOrDefault(l =>
                string.Equals(l.Commodity.Id, commodity.Id, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(l.Commodity.Name, commodity.Name, StringComparison.OrdinalIgnoreCase));
        }

        private bool BuyWithFallback(Commodity commodity, int quantity, PlayerCredits credits, CargoHold cargoHold)
        {
            int totalCost = commodity.BasePrice * quantity;
            if (!credits.CanAfford(totalCost) || !cargoHold.CanFit(commodity, quantity))
            {
                return false;
            }

            if (!credits.RemoveCredits(totalCost))
            {
                return false;
            }

            if (!cargoHold.AddCommodity(commodity, quantity))
            {
                credits.AddCredits(totalCost);
                return false;
            }

            Console.WriteLine($"[MARKET] BUY {quantity}x {commodity.Name} @ {commodity.BasePrice:N0} (fallback)");
            return true;
        }

        private bool SellWithFallback(Commodity commodity, int quantity, PlayerCredits credits, CargoHold cargoHold)
        {
            if (cargoHold.GetCommodityQuantity(commodity.Name) < quantity)
            {
                return false;
            }

            if (!cargoHold.RemoveCommodity(commodity, quantity))
            {
                return false;
            }

            int totalValue = commodity.BasePrice * quantity;
            credits.AddCredits(totalValue);
            Console.WriteLine($"[MARKET] SELL {quantity}x {commodity.Name} @ {commodity.BasePrice:N0} (fallback)");
            return true;
        }
    }
}
