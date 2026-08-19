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

        /// <summary>
        /// Advances station-market simulation time without requiring the trader
        /// terminal to be open. MarketManager applies recovery lazily when a
        /// listing is next accessed.
        /// </summary>
        public void AdvanceTime(double elapsedSeconds)
        {
            _marketManager.AdvanceTime(elapsedSeconds);
        }

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

        public List<SaveMarketStateData> CaptureMarketState()
        {
            return _marketManager.CaptureRuntimeState();
        }

        public void RestoreMarketState(IEnumerable<SaveMarketStateData> states)
        {
            _marketManager.RestoreRuntimeState(states);
        }

        public bool CanAfford(Commodity commodity, int quantity, PlayerCredits credits)
        {
            var listing = ResolveListing(commodity);
            if (listing == null || credits == null || quantity <= 0 || listing.BuyPrice <= 0)
            {
                return false;
            }

            long totalCost = (long)listing.BuyPrice * quantity;
            return totalCost <= int.MaxValue && credits.CanAfford((int)totalCost);
        }

        public bool HasSpace(Commodity commodity, int quantity, CargoHold cargoHold)
        {
            if (cargoHold == null)
            {
                return false;
            }

            StationMarketListing listing = ResolveListing(commodity);
            return listing != null && cargoHold.CanFit(listing.Commodity, quantity);
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

            var listing = ResolveListing(commodity);
            if (listing == null || !listing.IsAvailable || listing.BuyPrice <= 0)
            {
                message = "Commodity unavailable at this station.";
                return false;
            }

            if (quantity <= 0)
            {
                message = "Quantity must be at least 1.";
                return false;
            }

            long totalCostLong = (long)listing.BuyPrice * quantity;
            if (totalCostLong > int.MaxValue)
            {
                message = "Purchase total is invalid.";
                return false;
            }

            int totalCost = (int)totalCostLong;
            if (!credits.CanAfford(totalCost))
            {
                message = "Not enough credits.";
                return false;
            }

            if (!cargoHold.CanFit(listing.Commodity, quantity))
            {
                message = "Not enough cargo space.";
                return false;
            }

            if (!credits.RemoveCredits(totalCost))
            {
                message = "Credit transfer failed.";
                return false;
            }

            if (!cargoHold.AddCommodity(listing.Commodity, quantity))
            {
                credits.AddCredits(totalCost);
                message = "Cargo transfer failed.";
                return false;
            }

            message = $"Purchased {quantity} {listing.Commodity.Name} for {totalCost:N0} CR.";
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

            if (commodity.IsMissionCargo)
            {
                message = "Mission cargo cannot be sold.";
                return false;
            }

            var listing = ResolveListing(commodity);
            if (listing == null || !listing.IsAvailable || listing.SellPrice <= 0)
            {
                message = "Commodity unavailable at this station.";
                return false;
            }

            if (listing.Commodity.IsMissionCargo)
            {
                message = "Mission cargo cannot be sold.";
                return false;
            }

            if (quantity <= 0)
            {
                message = "Quantity must be at least 1.";
                return false;
            }

            int ownedQuantity = cargoHold.GetCommodityQuantity(listing.Commodity.Name);
            int sellableQuantity = cargoHold.GetSellableCommodityQuantity(listing.Commodity.Name);
            if (sellableQuantity < quantity)
            {
                message = ownedQuantity > sellableQuantity
                    ? "Mission cargo cannot be sold."
                    : "You do not own enough quantity to sell.";
                return false;
            }

            long totalValueLong = (long)listing.SellPrice * quantity;
            if (totalValueLong > int.MaxValue)
            {
                message = "Sale total is invalid.";
                return false;
            }

            int totalValue = (int)totalValueLong;
            if ((long)credits.Credits + totalValue > int.MaxValue)
            {
                message = "Credit total is invalid.";
                return false;
            }

            if (!cargoHold.RemoveCommodity(listing.Commodity, quantity))
            {
                message = cargoHold.GetMissionReservedQuantity(listing.Commodity.Name) > 0
                    ? "Mission cargo cannot be sold."
                    : "Cargo removal failed.";
                return false;
            }

            credits.AddCredits(totalValue);
            message = $"Sold {quantity} {listing.Commodity.Name} for {totalValue:N0} CR.";
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
