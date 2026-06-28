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
        /// True when the current station is using the legacy fallback catalog instead of a station-specific config.
        /// </summary>
        public bool IsUsingLegacyFallback => _currentStation == null || !_marketManager.HasMarketConfigForStation(_currentStation);

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
                string marketMode = _marketManager.HasMarketConfigForStation(station)
                    ? "station market"
                    : "legacy fallback catalog";
                Console.WriteLine($"[MARKET] Docked at {station.Name} using {marketMode}");
            }
        }

        public void ClearDockedStation()
        {
            if (_currentStation != null)
            {
                Console.WriteLine($"[MARKET] Undocked from {_currentStation.Name}");
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
            if (listing == null || credits == null)
            {
                return false;
            }

            int totalCost = listing.BuyPrice * quantity;
            return credits.CanAfford(totalCost);
        }

        public bool HasSpace(Commodity commodity, int quantity, CargoHold cargoHold)
        {
            if (cargoHold == null)
            {
                return false;
            }

            return cargoHold.CanFit(commodity, quantity);
        }

        public bool BuyCommodity(Commodity commodity, int quantity, PlayerCredits credits, CargoHold cargoHold)
        {
            return TryBuyCommodity(commodity, quantity, credits, cargoHold, out _);
        }

        public bool TryBuyCommodity(Commodity commodity, int quantity, PlayerCredits credits, CargoHold cargoHold, out string message)
        {
            if (_currentStation == null || !_marketManager.HasMarketConfigForStation(_currentStation))
            {
                bool fallbackSuccess = BuyWithFallback(commodity, quantity, credits, cargoHold, out message);
                LogMarketResult(fallbackSuccess, message);
                return fallbackSuccess;
            }

            bool marketSuccess = _marketManager.TryBuy(_currentStation, commodity, quantity, credits, cargoHold, out message);
            LogMarketResult(marketSuccess, message);

            return marketSuccess;
        }

        public bool SellCommodity(Commodity commodity, int quantity, PlayerCredits credits, CargoHold cargoHold)
        {
            return TrySellCommodity(commodity, quantity, credits, cargoHold, out _);
        }

        public bool TrySellCommodity(Commodity commodity, int quantity, PlayerCredits credits, CargoHold cargoHold, out string message)
        {
            if (_currentStation == null || !_marketManager.HasMarketConfigForStation(_currentStation))
            {
                bool fallbackSuccess = SellWithFallback(commodity, quantity, credits, cargoHold, out message);
                LogMarketResult(fallbackSuccess, message);
                return fallbackSuccess;
            }

            bool marketSuccess = _marketManager.TrySell(_currentStation, commodity, quantity, credits, cargoHold, out message);
            LogMarketResult(marketSuccess, message);

            return marketSuccess;
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

        private bool BuyWithFallback(Commodity commodity, int quantity, PlayerCredits credits, CargoHold cargoHold, out string message)
        {
            message = string.Empty;
            if (commodity == null)
            {
                message = "No commodity selected.";
                return false;
            }

            if (credits == null || cargoHold == null)
            {
                message = "Trading system unavailable.";
                return false;
            }

            if (quantity <= 0)
            {
                message = "Quantity must be at least 1.";
                return false;
            }

            int totalCost = commodity.BasePrice * quantity;
            if (!credits.CanAfford(totalCost))
            {
                message = "Not enough credits.";
                return false;
            }

            if (!cargoHold.CanFit(commodity, quantity))
            {
                message = "Not enough cargo space.";
                return false;
            }

            if (!credits.RemoveCredits(totalCost))
            {
                message = "Credit transfer failed.";
                return false;
            }

            if (!cargoHold.AddCommodity(commodity, quantity))
            {
                credits.AddCredits(totalCost);
                message = "Cargo transfer failed.";
                return false;
            }

            message = $"Bought {quantity}x {commodity.Name} at fallback prices.";
            return true;
        }

        private bool SellWithFallback(Commodity commodity, int quantity, PlayerCredits credits, CargoHold cargoHold, out string message)
        {
            message = string.Empty;
            if (commodity == null)
            {
                message = "No commodity selected.";
                return false;
            }

            if (credits == null || cargoHold == null)
            {
                message = "Trading system unavailable.";
                return false;
            }

            if (quantity <= 0)
            {
                message = "Quantity must be at least 1.";
                return false;
            }

            if (cargoHold.GetCommodityQuantity(commodity.Name) < quantity)
            {
                message = "You do not own enough quantity to sell.";
                return false;
            }

            if (!cargoHold.RemoveCommodity(commodity, quantity))
            {
                message = "Cargo removal failed.";
                return false;
            }

            int totalValue = commodity.BasePrice * quantity;
            credits.AddCredits(totalValue);
            message = $"Sold {quantity}x {commodity.Name} at fallback prices.";
            return true;
        }

        private static void LogMarketResult(bool success, string message)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                return;
            }

            string prefix = success ? "[MARKET]" : "[MARKET][FAIL]";
            Console.WriteLine($"{prefix} {message}");
        }
    }
}
