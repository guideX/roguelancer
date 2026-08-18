using Roguelancer.Configuration;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace Roguelancer
{
    /// <summary>
    /// Loads station market configs and manages runtime stock / pricing state.
    /// </summary>
    public class MarketManager
    {
        private const string MarketDirectory = "Configuration/markets";

        private readonly Dictionary<string, StationMarketConfig> _marketConfigs = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, List<StationMarketListing>> _runtimeMarkets = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, Commodity> _commodityIndex = new(StringComparer.OrdinalIgnoreCase);
        private readonly List<Commodity> _fallbackCatalog = new();

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            AllowTrailingCommas = true
        };

        public MarketManager()
        {
            foreach (var commodity in CommodityCatalog.All)
            {
                _fallbackCatalog.Add(CloneCommodity(commodity));
                RegisterCommodity(commodity);
            }

            LoadMarketConfigs();
        }

        public IReadOnlyList<Commodity> FallbackCatalog => _fallbackCatalog;

        public bool HasMarketConfigForStation(Station station)
        {
            string stationKey = GetStationKey(station?.Name, station?.Config?.Description);
            return !string.IsNullOrWhiteSpace(stationKey) && _marketConfigs.ContainsKey(stationKey);
        }

        public void RegisterCommodity(Commodity commodity)
        {
            if (commodity == null)
            {
                return;
            }

            if (!string.IsNullOrWhiteSpace(commodity.Id))
            {
                _commodityIndex[NormalizeKey(commodity.Id)] = commodity;
            }

            if (!string.IsNullOrWhiteSpace(commodity.Name))
            {
                _commodityIndex[NormalizeKey(commodity.Name)] = commodity;
            }
        }

        public Commodity ResolveCommodity(string commodityIdOrName)
        {
            if (string.IsNullOrWhiteSpace(commodityIdOrName))
            {
                return null;
            }

            _commodityIndex.TryGetValue(NormalizeKey(commodityIdOrName), out var commodity);
            return commodity;
        }

        public void LoadMarketConfigs()
        {
            _marketConfigs.Clear();
            _runtimeMarkets.Clear();

            Console.WriteLine($"[MARKET] Loading station market configs from {MarketDirectory}");
            if (!Directory.Exists(MarketDirectory))
            {
                Console.WriteLine("[MARKET] Market config directory not found. Falling back to legacy catalog.");
                return;
            }

            foreach (var file in Directory.GetFiles(MarketDirectory, "*.json"))
            {
                try
                {
                    var json = File.ReadAllText(file);
                    var config = JsonSerializer.Deserialize<StationMarketConfig>(json, JsonOptions);
                    if (config == null)
                    {
                        Console.WriteLine($"[MARKET] Skipped invalid config: {Path.GetFileName(file)}");
                        continue;
                    }

                    var key = GetStationKey(config.StationId, config.StationName);
                    if (string.IsNullOrWhiteSpace(key))
                    {
                        Console.WriteLine($"[MARKET] Skipped config without station id/name: {Path.GetFileName(file)}");
                        continue;
                    }

                    if (_marketConfigs.ContainsKey(key))
                    {
                        Console.WriteLine($"[MARKET] Skipped duplicate station market key '{key}': {Path.GetFileName(file)}");
                        continue;
                    }

                    List<StationMarketGoodConfig> validGoods = new();
                    HashSet<string> commodityIds = new(StringComparer.OrdinalIgnoreCase);
                    foreach (var good in config.Goods ?? new List<StationMarketGoodConfig>())
                    {
                        if (!ValidateGoodConfig(good, commodityIds, out string failureReason))
                        {
                            Console.WriteLine($"[MARKET] Skipped invalid listing in {Path.GetFileName(file)}: {failureReason}");
                            continue;
                        }

                        validGoods.Add(good);
                    }

                    config.Goods = validGoods;
                    if (config.Goods.Count == 0)
                    {
                        Console.WriteLine($"[MARKET] Skipped market with no valid listings: {Path.GetFileName(file)}");
                        continue;
                    }

                    _marketConfigs[key] = config;
                    Console.WriteLine($"[MARKET] Loaded market config for {config.StationName ?? config.StationId} with {config.Goods?.Count ?? 0} goods");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[MARKET] Error loading {Path.GetFileName(file)}: {ex.Message}");
                }
            }

            Console.WriteLine($"[MARKET] Loaded {_marketConfigs.Count} station market configs");
        }

        public List<StationMarketListing> GetListingsForStation(Station station)
        {
            string stationKey = GetStationKey(station?.Name, station?.Config?.Description);
            if (string.IsNullOrWhiteSpace(stationKey))
            {
                return BuildFallbackListings();
            }

            if (!_marketConfigs.TryGetValue(stationKey, out var config))
            {
                return BuildFallbackListings();
            }

            if (_runtimeMarkets.TryGetValue(stationKey, out var runtimeListings))
            {
                return CloneListings(runtimeListings);
            }

            runtimeListings = BuildRuntimeListings(config);
            _runtimeMarkets[stationKey] = runtimeListings;
            return CloneListings(runtimeListings);
        }

        public StationMarketListing GetListingForCommodity(Station station, Commodity commodity)
        {
            if (station == null || commodity == null)
            {
                return null;
            }

            var listings = GetListingsForStation(station);
            return listings.FirstOrDefault(l =>
                string.Equals(l.Commodity.Id, commodity.Id, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(l.Commodity.Name, commodity.Name, StringComparison.OrdinalIgnoreCase));
        }

        public bool TryBuy(Station station, Commodity commodity, int quantity, PlayerCredits credits, CargoHold cargoHold, out string message)
        {
            message = string.Empty;
            if (station == null || commodity == null)
            {
                message = "No station market selected.";
                return false;
            }

            if (credits == null || cargoHold == null)
            {
                message = "Trading system unavailable.";
                return false;
            }

            var stationKey = GetStationKey(station.Name, station.Config?.Description);
            var listing = GetMutableListing(stationKey, commodity);
            if (listing == null)
            {
                message = "Commodity unavailable at this station.";
                return false;
            }

            Commodity marketCommodity = listing.Commodity;
            if (!IsValidCommodity(marketCommodity) || !listing.IsAvailable || listing.BuyPrice <= 0 || listing.Stock < 0)
            {
                message = "Commodity unavailable at this station.";
                return false;
            }

            if (quantity <= 0)
            {
                message = "Quantity must be at least 1.";
                return false;
            }

            if (listing.Stock < quantity)
            {
                message = listing.Stock <= 0
                    ? "Out of stock."
                    : $"Only {listing.Stock} units of {commodity.Name} are in stock.";
                return false;
            }

            if (!TryCalculateTotal(listing.BuyPrice, quantity, out int totalCost))
            {
                message = "Purchase total is invalid.";
                return false;
            }

            if (!credits.CanAfford(totalCost))
            {
                message = "Not enough credits.";
                return false;
            }

            if (!cargoHold.CanFit(marketCommodity, quantity))
            {
                message = "Not enough cargo space.";
                return false;
            }

            if (!credits.RemoveCredits(totalCost))
            {
                message = "Credit transfer failed.";
                return false;
            }

            if (!cargoHold.AddCommodity(marketCommodity, quantity))
            {
                credits.AddCredits(totalCost);
                message = "Cargo transfer failed.";
                return false;
            }

            listing.Stock -= quantity;
            message = $"Purchased {quantity} {marketCommodity.Name} for {totalCost:N0} CR.";
            return true;
        }

        public bool TrySell(Station station, Commodity commodity, int quantity, PlayerCredits credits, CargoHold cargoHold, out string message)
        {
            message = string.Empty;
            if (station == null || commodity == null)
            {
                message = "No station market selected.";
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

            if (commodity.IsMissionCargo)
            {
                message = "Mission cargo cannot be sold.";
                return false;
            }

            var stationKey = GetStationKey(station.Name, station.Config?.Description);
            var listing = GetMutableListing(stationKey, commodity);
            if (listing == null)
            {
                message = "Commodity unavailable at this station.";
                return false;
            }

            Commodity marketCommodity = listing.Commodity;
            if (!IsValidCommodity(marketCommodity) || marketCommodity.IsMissionCargo)
            {
                message = "Mission cargo cannot be sold.";
                return false;
            }

            if (!listing.IsAvailable || listing.SellPrice <= 0)
            {
                message = "Commodity unavailable at this station.";
                return false;
            }

            int ownedQuantity = cargoHold.GetCommodityQuantity(marketCommodity.Name);
            int sellableQuantity = cargoHold.GetSellableCommodityQuantity(marketCommodity.Name);
            if (sellableQuantity < quantity)
            {
                message = ownedQuantity > sellableQuantity
                    ? "Mission cargo cannot be sold."
                    : "You do not own enough quantity to sell.";
                return false;
            }

            if (!TryCalculateTotal(listing.SellPrice, quantity, out int totalValue))
            {
                message = "Sale total is invalid.";
                return false;
            }

            if ((long)credits.Credits + totalValue > int.MaxValue)
            {
                message = "Credit total is invalid.";
                return false;
            }

            if ((long)listing.Stock + quantity > int.MaxValue)
            {
                message = "Station inventory cannot accept that sale.";
                return false;
            }

            if (!cargoHold.RemoveCommodity(marketCommodity, quantity))
            {
                message = cargoHold.GetMissionReservedQuantity(marketCommodity.Name) > 0
                    ? "Mission cargo cannot be sold."
                    : "Cargo removal failed.";
                return false;
            }

            credits.AddCredits(totalValue);
            listing.Stock += quantity;
            message = $"Sold {quantity} {marketCommodity.Name} for {totalValue:N0} CR.";
            return true;
        }

        public Commodity GetCommodityByIndex(int index, Station station = null)
        {
            var listings = GetListingsForStation(station);
            if (index < 0 || index >= listings.Count)
            {
                return null;
            }

            return listings[index].Commodity;
        }

        public int GetMarketCount(Station station = null)
        {
            return GetListingsForStation(station).Count;
        }

        public Dictionary<Commodity, int> GetCommodityRegistry()
        {
            return CommodityCatalog.BuildRegistry();
        }

        public List<SaveMarketStateData> CaptureRuntimeState()
        {
            var states = new List<SaveMarketStateData>();

            foreach (var kvp in _runtimeMarkets)
            {
                if (kvp.Value == null || kvp.Value.Count == 0)
                {
                    continue;
                }

                states.Add(new SaveMarketStateData
                {
                    StationKey = kvp.Key,
                    StationName = string.Empty,
                    Listings = kvp.Value
                        .Where(listing => listing != null && listing.Commodity != null)
                        .Select(listing => new SaveMarketListingData
                        {
                            CommodityId = listing.Commodity.Id,
                            BuyPrice = listing.BuyPrice,
                            SellPrice = listing.SellPrice,
                            Stock = listing.Stock,
                            DemandLevel = listing.DemandLevel,
                            IsAvailable = listing.IsAvailable
                        })
                        .ToList()
                });
            }

            return states;
        }

        public void RestoreRuntimeState(IEnumerable<SaveMarketStateData> states)
        {
            _runtimeMarkets.Clear();

            if (states == null)
            {
                return;
            }

            foreach (var state in states)
            {
                if (state == null)
                {
                    continue;
                }

                string stationKey = GetStationKey(state.StationKey, state.StationName);
                if (string.IsNullOrWhiteSpace(stationKey))
                {
                    continue;
                }

                Dictionary<string, StationMarketListing> configuredListings = null;
                if (_marketConfigs.TryGetValue(stationKey, out var config))
                {
                    configuredListings = BuildRuntimeListings(config)
                        .Where(listing => listing?.Commodity != null)
                        .ToDictionary(listing => NormalizeKey(listing.Commodity.Id), StringComparer.OrdinalIgnoreCase);
                }

                var listings = new List<StationMarketListing>();
                foreach (var listing in state.Listings ?? new List<SaveMarketListingData>())
                {
                    if (listing == null)
                    {
                        continue;
                    }

                    var commodity = ResolveCommodity(listing.CommodityId);
                    if (commodity == null)
                    {
                        continue;
                    }

                    int buyPrice = listing.BuyPrice;
                    int sellPrice = listing.SellPrice;
                    bool isAvailable = listing.IsAvailable;
                    if (configuredListings != null)
                    {
                        if (!configuredListings.TryGetValue(NormalizeKey(commodity.Id), out var configuredListing))
                        {
                            continue;
                        }

                        buyPrice = configuredListing.BuyPrice;
                        sellPrice = configuredListing.SellPrice;
                        isAvailable = configuredListing.IsAvailable;
                    }

                    listings.Add(new StationMarketListing(
                        commodity,
                        buyPrice,
                        sellPrice,
                        listing.Stock,
                        listing.DemandLevel,
                        isAvailable));
                }

                if (listings.Count > 0)
                {
                    _runtimeMarkets[stationKey] = listings;
                }
            }

            Console.WriteLine($"[MARKET] Restored {_runtimeMarkets.Count} runtime market snapshots");
        }

        private List<StationMarketListing> BuildRuntimeListings(StationMarketConfig config)
        {
            var listings = new List<StationMarketListing>();
            HashSet<string> commodityIds = new(StringComparer.OrdinalIgnoreCase);

            foreach (var good in config.Goods ?? new List<StationMarketGoodConfig>())
            {
                if (!ValidateGoodConfig(good, commodityIds, out string failureReason))
                {
                    Console.WriteLine($"[MARKET] Ignored invalid runtime listing: {failureReason}");
                    continue;
                }

                var commodity = ResolveCommodity(good.CommodityId);
                if (commodity == null)
                {
                    Console.WriteLine($"[MARKET] Unknown commodity '{good.CommodityId}' in station config.");
                    continue;
                }

                listings.Add(new StationMarketListing(commodity, good));
            }

            if (listings.Count == 0)
            {
                return BuildFallbackListings();
            }

            return listings;
        }

        private List<StationMarketListing> BuildFallbackListings()
        {
            var fallback = new List<StationMarketListing>();
            foreach (var commodity in _fallbackCatalog)
            {
                int sellPrice = commodity.BasePrice > 1
                    ? Math.Max(1, (int)Math.Floor(commodity.BasePrice * 0.75d))
                    : 0;
                bool available = IsValidCommodity(commodity) && !commodity.IsMissionCargo && commodity.BasePrice > 0;
                fallback.Add(new StationMarketListing(commodity, commodity.BasePrice, sellPrice, 9999, 0, available));
            }

            return fallback;
        }

        private List<StationMarketListing> CloneListings(List<StationMarketListing> listings)
        {
            var clones = new List<StationMarketListing>(listings.Count);
            foreach (var listing in listings)
            {
                clones.Add(new StationMarketListing(listing.Commodity, listing.BuyPrice, listing.SellPrice, listing.Stock, listing.DemandLevel, listing.IsAvailable));
            }

            return clones;
        }

        private StationMarketListing GetMutableListing(string stationKey, Commodity commodity)
        {
            if (string.IsNullOrWhiteSpace(stationKey) || commodity == null)
            {
                return null;
            }

            if (!_runtimeMarkets.TryGetValue(stationKey, out var listings))
            {
                if (!_marketConfigs.TryGetValue(stationKey, out var config))
                {
                    return null;
                }

                listings = BuildRuntimeListings(config);
                _runtimeMarkets[stationKey] = listings;
            }

            return listings.FirstOrDefault(l =>
                string.Equals(l.Commodity.Id, commodity.Id, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(l.Commodity.Name, commodity.Name, StringComparison.OrdinalIgnoreCase));
        }

        private bool ValidateGoodConfig(
            StationMarketGoodConfig good,
            HashSet<string> commodityIds,
            out string failureReason)
        {
            failureReason = string.Empty;
            if (good == null || string.IsNullOrWhiteSpace(good.CommodityId))
            {
                failureReason = "commodity id is missing";
                return false;
            }

            Commodity commodity = ResolveCommodity(good.CommodityId);
            if (!IsValidCommodity(commodity))
            {
                failureReason = $"unknown or invalid commodity '{good.CommodityId}'";
                return false;
            }

            string commodityId = commodity.Id.Trim();
            if (!commodityIds.Add(commodityId))
            {
                failureReason = $"duplicate commodity listing '{commodityId}'";
                return false;
            }

            if (good.BuyPrice < 0 || good.SellPrice < 0 || good.Stock < 0 || good.DemandLevel < 0)
            {
                failureReason = $"negative market data for '{commodityId}'";
                return false;
            }

            if (commodity.IsMissionCargo)
            {
                failureReason = $"mission cargo '{commodityId}' cannot be listed";
                return false;
            }

            if (good.IsAvailable && (good.BuyPrice <= 0 || good.SellPrice <= 0))
            {
                failureReason = $"available listing '{commodityId}' needs positive buy and sell prices";
                return false;
            }

            if (good.IsAvailable && good.BuyPrice < good.SellPrice)
            {
                failureReason = $"same-station arbitrage on '{commodityId}'";
                return false;
            }

            return true;
        }

        private static bool IsValidCommodity(Commodity commodity)
        {
            return commodity != null &&
                !string.IsNullOrWhiteSpace(commodity.Id) &&
                !string.IsNullOrWhiteSpace(commodity.Name) &&
                commodity.VolumePerUnit > 0;
        }

        private static bool TryCalculateTotal(int unitPrice, int quantity, out int total)
        {
            total = 0;
            if (unitPrice <= 0 || quantity <= 0)
            {
                return false;
            }

            long value = (long)unitPrice * quantity;
            if (value > int.MaxValue)
            {
                return false;
            }

            total = (int)value;
            return true;
        }

        private static Commodity CloneCommodity(Commodity commodity)
        {
            return new Commodity(
                commodity.Id,
                commodity.Name,
                commodity.Description,
                commodity.BasePrice,
                commodity.VolumePerUnit,
                commodity.IsContraband,
                commodity.Category,
                commodity.DisplayColor);
        }

        private static string GetStationKey(string stationId, string stationName)
        {
            string raw = !string.IsNullOrWhiteSpace(stationId) ? stationId : stationName;
            return NormalizeKey(raw);
        }

        private static string NormalizeKey(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            char[] buffer = value.Trim().ToLowerInvariant()
                .Where(ch => char.IsLetterOrDigit(ch))
                .ToArray();
            return new string(buffer);
        }
    }
}
