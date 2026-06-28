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
                Console.WriteLine($"[MARKET] No config for {station?.Name ?? "unknown station"}; using fallback catalog.");
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

            var stationKey = GetStationKey(station.Name, station.Config?.Description);
            var listing = GetMutableListing(stationKey, commodity);
            if (listing == null)
            {
                message = $"{commodity.Name} is not sold here.";
                return false;
            }

            if (!listing.IsAvailable || listing.BuyPrice <= 0)
            {
                message = $"{commodity.Name} is not available for purchase at this station.";
                return false;
            }

            if (quantity <= 0)
            {
                message = "Quantity must be at least 1.";
                return false;
            }

            if (listing.Stock < quantity)
            {
                message = $"Only {listing.Stock} units of {commodity.Name} are in stock.";
                return false;
            }

            int totalCost = listing.BuyPrice * quantity;
            if (!credits.CanAfford(totalCost))
            {
                message = "Insufficient credits.";
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

            listing.Stock -= quantity;
            Console.WriteLine($"[MARKET] BUY {quantity}x {commodity.Name} @ {listing.BuyPrice:N0} on {station.Name} | stock={listing.Stock}");
            message = $"Bought {quantity}x {commodity.Name}.";
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

            if (quantity <= 0)
            {
                message = "Quantity must be at least 1.";
                return false;
            }

            if (cargoHold.GetCommodityQuantity(commodity.Name) < quantity)
            {
                message = $"You do not have enough {commodity.Name}.";
                return false;
            }

            var stationKey = GetStationKey(station.Name, station.Config?.Description);
            var listing = GetMutableListing(stationKey, commodity);
            if (listing == null)
            {
                message = $"{commodity.Name} is not bought here.";
                return false;
            }

            if (listing.SellPrice <= 0)
            {
                message = $"{commodity.Name} is not purchased here.";
                return false;
            }

            if (!cargoHold.RemoveCommodity(commodity, quantity))
            {
                message = "Cargo removal failed.";
                return false;
            }

            int totalValue = listing.SellPrice * quantity;
            credits.AddCredits(totalValue);
            listing.Stock += quantity;
            Console.WriteLine($"[MARKET] SELL {quantity}x {commodity.Name} @ {listing.SellPrice:N0} on {station.Name} | stock={listing.Stock}");
            message = $"Sold {quantity}x {commodity.Name}.";
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

        private List<StationMarketListing> BuildRuntimeListings(StationMarketConfig config)
        {
            var listings = new List<StationMarketListing>();

            foreach (var good in config.Goods ?? new List<StationMarketGoodConfig>())
            {
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
                fallback.Add(new StationMarketListing(commodity, commodity.BasePrice, commodity.BasePrice, 9999, 0, true));
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
