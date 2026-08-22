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
        private long _elapsedMilliseconds;

        // Dynamic pricing uses integer basis points and a conservative bounded
        // response around the configured station anchors:
        //   buy  = base buy  * (1 + pressure * 35%), clamped to 65%-135%
        //   sell = base sell * (1 + pressure * 50%), clamped to 50%-150%
        // where pressure is (baseline stock - current stock) / baseline stock.
        // A five-percent minimum spread (or the configured spread when smaller)
        // prevents same-station price inversion. A player's immediate buy is
        // also protected from becoming a round-trip profit after its stock impact.
        private const int BuyPressureResponsePercent = 35;
        private const int SellPressureResponsePercent = 50;
        private const int BuyPriceFloorPercent = 65;
        private const int BuyPriceCeilingPercent = 135;
        private const int SellPriceFloorPercent = 50;
        private const int SellPriceCeilingPercent = 150;
        private const int MinimumSpreadPercent = 5;
        private const int BasisPoints = 10_000;

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

        /// <summary>
        /// Simulation time used by dynamic markets and market intelligence.
        /// This is game time, never wall-clock time.
        /// </summary>
        public long ElapsedMilliseconds => _elapsedMilliseconds;

        /// <summary>
        /// Clears only the in-memory runtime economy. Configuration files and
        /// the caller's save data are never changed. Developer validation uses
        /// this to make a deliberately isolated run repeatable.
        /// </summary>
        public void ResetRuntimeState()
        {
            _runtimeMarkets.Clear();
            _elapsedMilliseconds = 0L;
        }

        public void RestoreElapsedMilliseconds(long elapsedMilliseconds)
        {
            _elapsedMilliseconds = Math.Max(0L, elapsedMilliseconds);
        }

        /// <summary>
        /// Advances the economy using elapsed simulation time. No market is
        /// iterated here; accessed runtime listings lazily consume the elapsed
        /// time when read or transacted against.
        /// </summary>
        public void AdvanceTime(double elapsedSeconds)
        {
            if (double.IsNaN(elapsedSeconds) || double.IsInfinity(elapsedSeconds) || elapsedSeconds <= 0d)
            {
                return;
            }

            long elapsedMilliseconds = (long)Math.Min(long.MaxValue / 2d, Math.Round(elapsedSeconds * 1000d, MidpointRounding.AwayFromZero));
            if (elapsedMilliseconds <= 0)
            {
                return;
            }

            _elapsedMilliseconds = _elapsedMilliseconds > long.MaxValue - elapsedMilliseconds
                ? long.MaxValue
                : _elapsedMilliseconds + elapsedMilliseconds;
        }

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

        /// <summary>
        /// Returns the stable station id from the authoritative station market
        /// configuration. Stations without a configured market are not market
        /// intelligence stations.
        /// </summary>
        public string GetStationId(Station station) => GetStationIdByName(station?.Name);

        public string GetStationIdByName(string stationName)
        {
            string key = NormalizeKey(stationName);
            if (string.IsNullOrWhiteSpace(key)) return string.Empty;

            if (_marketConfigs.TryGetValue(key, out StationMarketConfig config))
                return config.StationId ?? key;

            return _marketConfigs.Values
                .FirstOrDefault(candidate => NormalizeKey(candidate?.StationName) == key)?.StationId ?? string.Empty;
        }

        public bool IsKnownStationId(string stationId)
        {
            string key = NormalizeKey(stationId);
            return !string.IsNullOrWhiteSpace(key) && _marketConfigs.ContainsKey(key);
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
                AdvanceListings(runtimeListings);
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

            int availableStock = Math.Max(0, listing.Stock - listing.MinimumStock);
            if (availableStock < quantity)
            {
                message = availableStock <= 0
                    ? "Out of stock."
                    : $"Only {availableStock} units of {commodity.Name} are available.";
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

            int paidPrice = listing.BuyPrice;
            listing.Stock = Math.Max(listing.MinimumStock, listing.Stock - quantity);
            listing.ImmediateSellPriceCeiling = listing.ImmediateSellPriceCeiling > 0
                ? Math.Min(listing.ImmediateSellPriceCeiling, paidPrice)
                : paidPrice;
            listing.RecoveryRemainderMilliseconds = 0;
            RefreshPrices(listing);
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

            if ((long)listing.Stock + quantity > listing.MaximumStock)
            {
                message = $"Station inventory can hold only {Math.Max(0, listing.MaximumStock - listing.Stock)} more units.";
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
            listing.Stock = Math.Min(listing.MaximumStock, listing.Stock + quantity);
            listing.ImmediateSellPriceCeiling = 0;
            listing.RecoveryRemainderMilliseconds = 0;
            RefreshPrices(listing);
            message = $"Sold {quantity} {marketCommodity.Name} for {totalValue:N0} CR.";
            return true;
        }

        /// <summary>
        /// Validates whether a real shipment can enter a station's configured
        /// market without changing any state.
        /// </summary>
        public bool CanAddSupply(Station station, Commodity commodity, int quantity, out string message)
        {
            message = string.Empty;
            if (!TryResolveSupplyListing(station, commodity, quantity, out StationMarketListing listing, out message))
            {
                return false;
            }

            if ((long)listing.Stock + quantity > listing.MaximumStock)
            {
                message = $"Station inventory can hold only {Math.Max(0, listing.MaximumStock - listing.Stock)} more units.";
                return false;
            }

            return true;
        }

        /// <summary>
        /// Validates removal of real station inventory without changing state.
        /// Export contracts use this to keep a shipment above its normal stock
        /// floor while the terms are being accepted.
        /// </summary>
        public bool CanRemoveSupply(
            Station station,
            Commodity commodity,
            int quantity,
            int minimumRemainingStock,
            out string message)
        {
            message = string.Empty;
            if (!TryResolveSupplyListing(station, commodity, quantity, out StationMarketListing listing, out message))
            {
                return false;
            }

            minimumRemainingStock = Math.Max(0, minimumRemainingStock);
            if (listing.Stock - quantity < minimumRemainingStock)
            {
                message = $"Station inventory can export only {Math.Max(0, listing.Stock - minimumRemainingStock)} units.";
                return false;
            }

            return true;
        }

        /// <summary>
        /// Adds delivered freight to the authoritative destination market.
        /// Prices are recomputed from the resulting stock immediately.
        /// </summary>
        public bool TryAddSupply(Station station, Commodity commodity, int quantity, out string message)
        {
            message = string.Empty;
            if (!TryResolveSupplyListing(station, commodity, quantity, out StationMarketListing listing, out message))
            {
                return false;
            }

            if ((long)listing.Stock + quantity > listing.MaximumStock)
            {
                message = $"Station inventory can hold only {Math.Max(0, listing.MaximumStock - listing.Stock)} more units.";
                return false;
            }

            listing.Stock += quantity;
            listing.ImmediateSellPriceCeiling = 0;
            listing.RecoveryRemainderMilliseconds = 0;
            RefreshPrices(listing);
            message = $"Delivered {quantity} {listing.Commodity.Name}; station stock is now {listing.Stock:N0}.";
            return true;
        }

        /// <summary>
        /// Removes real stock from a station and immediately recomputes its
        /// dynamic prices. This is intentionally separate from player sales so
        /// export cargo can be issued without charging the player.
        /// </summary>
        public bool TryRemoveSupply(
            Station station,
            Commodity commodity,
            int quantity,
            int minimumRemainingStock,
            out string message)
        {
            message = string.Empty;
            if (!CanRemoveSupply(station, commodity, quantity, minimumRemainingStock, out message))
            {
                return false;
            }

            string stationKey = GetStationKey(station.Name, station.Config?.Description);
            StationMarketListing listing = GetMutableListing(stationKey, commodity);
            if (listing == null)
            {
                message = "Commodity unavailable at this station.";
                return false;
            }

            listing.Stock -= quantity;
            listing.ImmediateSellPriceCeiling = 0;
            listing.RecoveryRemainderMilliseconds = 0;
            RefreshPrices(listing);
            message = $"Exported {quantity} {listing.Commodity.Name}; station stock is now {listing.Stock:N0}.";
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

                AdvanceListings(kvp.Value);

                states.Add(new SaveMarketStateData
                {
                    StationKey = kvp.Key,
                    StationName = string.Empty,
                    Listings = kvp.Value
                        .Where(listing => listing != null && listing.Commodity != null)
                        .Select(listing => new SaveMarketListingData
                        {
                            CommodityId = listing.Commodity.Id,
                            Stock = Math.Clamp(listing.Stock, listing.MinimumStock, listing.MaximumStock),
                            DemandLevel = listing.DemandLevel,
                            IsAvailable = listing.IsAvailable,
                            RecoveryRemainderMilliseconds = Math.Max(0, listing.RecoveryRemainderMilliseconds),
                            ImmediateSellPriceCeiling = Math.Max(0, listing.ImmediateSellPriceCeiling)
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

                    if (configuredListings != null)
                    {
                        if (!configuredListings.TryGetValue(NormalizeKey(commodity.Id), out var configuredListing))
                        {
                            continue;
                        }

                        var restoredListing = new StationMarketListing(configuredListing)
                        {
                            Stock = Math.Clamp(listing.Stock, configuredListing.MinimumStock, configuredListing.MaximumStock),
                            RecoveryRemainderMilliseconds = Math.Max(0, listing.RecoveryRemainderMilliseconds),
                            ImmediateSellPriceCeiling = Math.Clamp(
                                listing.ImmediateSellPriceCeiling,
                                0,
                                configuredListing.BaseBuyPrice),
                            LastAdvancedMilliseconds = _elapsedMilliseconds
                        };
                        RefreshPrices(restoredListing);
                        listings.Add(restoredListing);
                        continue;
                    }

                    // A legacy snapshot for a station that no longer has a
                    // configured market is ignored rather than becoming a
                    // second, non-authoritative economy.
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

                StationMarketListing listing = new(commodity, good)
                {
                    LastAdvancedMilliseconds = _elapsedMilliseconds
                };
                RefreshPrices(listing);
                listings.Add(listing);
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
                clones.Add(new StationMarketListing(listing));
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

            AdvanceListings(listings);
            return listings.FirstOrDefault(l =>
                string.Equals(l.Commodity.Id, commodity.Id, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(l.Commodity.Name, commodity.Name, StringComparison.OrdinalIgnoreCase));
        }

        private bool TryResolveSupplyListing(
            Station station,
            Commodity commodity,
            int quantity,
            out StationMarketListing listing,
            out string message)
        {
            listing = null;
            message = string.Empty;
            if (station == null || commodity == null)
            {
                message = "No destination market selected.";
                return false;
            }

            if (quantity <= 0)
            {
                message = "Quantity must be at least 1.";
                return false;
            }

            string stationKey = GetStationKey(station.Name, station.Config?.Description);
            listing = GetMutableListing(stationKey, commodity);
            if (listing == null || !IsValidCommodity(listing.Commodity) ||
                listing.Commodity.IsMissionCargo || listing.Commodity.IsContraband ||
                !listing.IsAvailable || listing.BaseBuyPrice <= 0 || listing.BaseSellPrice <= 0 ||
                listing.Stock < 0)
            {
                message = "Commodity is not a legitimate tradable good at this station.";
                return false;
            }

            return true;
        }

        private void AdvanceListings(List<StationMarketListing> listings)
        {
            if (listings == null)
            {
                return;
            }

            foreach (StationMarketListing listing in listings)
            {
                AdvanceListing(listing);
            }
        }

        private void AdvanceListing(StationMarketListing listing)
        {
            if (listing == null)
            {
                return;
            }

            if (listing.LastAdvancedMilliseconds > _elapsedMilliseconds)
            {
                listing.LastAdvancedMilliseconds = _elapsedMilliseconds;
            }

            long elapsedMilliseconds = _elapsedMilliseconds - listing.LastAdvancedMilliseconds;
            if (elapsedMilliseconds <= 0)
            {
                return;
            }

            RecoverStock(listing, elapsedMilliseconds);
            listing.LastAdvancedMilliseconds = _elapsedMilliseconds;
            RefreshPrices(listing);
        }

        private static void RecoverStock(StationMarketListing listing, long elapsedMilliseconds)
        {
            if (listing.BaselineStock <= 0 || listing.Stock == listing.BaselineStock || elapsedMilliseconds <= 0)
            {
                listing.RecoveryRemainderMilliseconds = 0;
                return;
            }

            long recoveryPeriodMilliseconds = Math.Max(1L, (long)listing.RecoverySeconds * 1000L);
            // A fixed stock-unit rate makes the result independent of whether
            // elapsed time arrives as one large interval or many small ones.
            // The gap only determines when the bounded movement stops.
            long gap = Math.Abs((long)listing.BaselineStock - listing.Stock);
            decimal work = (decimal)listing.BaselineStock * elapsedMilliseconds + listing.RecoveryRemainderMilliseconds;
            long recoveredUnits = (long)(work / recoveryPeriodMilliseconds);
            listing.RecoveryRemainderMilliseconds = (long)(work % recoveryPeriodMilliseconds);

            if (recoveredUnits <= 0)
            {
                return;
            }

            recoveredUnits = Math.Min(recoveredUnits, gap);
            if (listing.Stock < listing.BaselineStock)
            {
                listing.Stock = (int)Math.Min(listing.BaselineStock, (long)listing.Stock + recoveredUnits);
            }
            else
            {
                listing.Stock = (int)Math.Max(listing.BaselineStock, (long)listing.Stock - recoveredUnits);
            }

            if (listing.Stock == listing.BaselineStock)
            {
                listing.RecoveryRemainderMilliseconds = 0;
            }
        }

        private static void RefreshPrices(StationMarketListing listing)
        {
            if (listing == null)
            {
                return;
            }

            listing.Stock = Math.Clamp(listing.Stock, listing.MinimumStock, listing.MaximumStock);
            if (!listing.IsAvailable || listing.BaselineStock <= 0 || listing.BaseBuyPrice <= 0 || listing.BaseSellPrice <= 0)
            {
                listing.BuyPrice = listing.BaseBuyPrice;
                listing.SellPrice = listing.BaseSellPrice;
                return;
            }

            long pressureBasisPoints = ((long)listing.BaselineStock - listing.Stock) * BasisPoints / listing.BaselineStock;
            pressureBasisPoints = Math.Clamp(pressureBasisPoints, -BasisPoints, BasisPoints);

            int buyMultiplier = Math.Clamp(
                BasisPoints + (int)(pressureBasisPoints * BuyPressureResponsePercent / 100L),
                BuyPriceFloorPercent * 100,
                BuyPriceCeilingPercent * 100);
            int sellMultiplier = Math.Clamp(
                BasisPoints + (int)(pressureBasisPoints * SellPressureResponsePercent / 100L),
                SellPriceFloorPercent * 100,
                SellPriceCeilingPercent * 100);

            int buyPrice = ScalePrice(listing.BaseBuyPrice, buyMultiplier);
            int sellPrice = ScalePrice(listing.BaseSellPrice, sellMultiplier);

            int configuredSpread = Math.Max(1, listing.BaseBuyPrice - listing.BaseSellPrice);
            int minimumSpread = Math.Max(1, Math.Min(configuredSpread, (int)Math.Ceiling(listing.BaseBuyPrice * MinimumSpreadPercent / 100m)));
            int maximumSellPrice = Math.Max(1, buyPrice - minimumSpread);
            maximumSellPrice = Math.Min(maximumSellPrice, Math.Max(1, listing.BaseBuyPrice - minimumSpread));
            if (listing.ImmediateSellPriceCeiling > 0)
            {
                maximumSellPrice = Math.Min(maximumSellPrice, listing.ImmediateSellPriceCeiling);
            }

            listing.BuyPrice = Math.Max(1, buyPrice);
            listing.SellPrice = Math.Clamp(sellPrice, 1, Math.Max(1, maximumSellPrice));
        }

        private static int ScalePrice(int basePrice, int multiplierBasisPoints)
        {
            long scaled = ((long)basePrice * multiplierBasisPoints + BasisPoints / 2) / BasisPoints;
            return (int)Math.Clamp(scaled, 1L, int.MaxValue);
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

            if (good.BuyPrice < 0 || good.SellPrice < 0 || good.Stock < 0 || good.Stock > 1_000_000 || good.DemandLevel < 0 ||
                (good.MinimumStock.HasValue && good.MinimumStock.Value < 0) ||
                (good.MaximumStock.HasValue && (good.MaximumStock.Value <= 0 || good.MaximumStock.Value > 1_000_000)) ||
                good.RecoverySeconds < 0)
            {
                failureReason = $"negative market data for '{commodityId}'";
                return false;
            }

            if (good.MinimumStock.HasValue && good.MinimumStock.Value > good.Stock)
            {
                failureReason = $"minimum stock exceeds baseline stock for '{commodityId}'";
                return false;
            }

            if (good.MaximumStock.HasValue && good.MaximumStock.Value < good.Stock)
            {
                failureReason = $"maximum stock is below baseline stock for '{commodityId}'";
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
