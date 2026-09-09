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
        private readonly Dictionary<string, long> _shortageSinceMilliseconds = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<int, SupplyCapacityReservation> _supplyCapacityReservations = new();
        private readonly List<Commodity> _fallbackCatalog = new();
        private long _elapsedMilliseconds;

        private sealed class SupplyCapacityReservation
        {
            public string StationKey { get; init; } = string.Empty;
            public string CommodityId { get; init; } = string.Empty;
            public int Quantity { get; set; }
        }

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
        private const long MaximumListingCatchUpMilliseconds = 5L * 60L * 1000L;
        private const long ConsumptionDenominator = 60L * 1000L * StationMarketListing.ConsumptionScale;
        private const long MaximumConsumptionRemainder = ConsumptionDenominator - 1L;

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
            _shortageSinceMilliseconds.Clear();
            _supplyCapacityReservations.Clear();
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

        /// <summary>
        /// Returns whether this existing station has a configured illicit
        /// market host. A black market is a policy view over the station's
        /// ordinary runtime market, never a second inventory.
        /// </summary>
        public bool HasBlackMarketForStation(Station station)
        {
            if (!TryGetMarketConfig(station, out StationMarketConfig config))
            {
                return false;
            }

            return BlackMarketPolicy.IsEligibleHostFaction(GetEffectiveMarketFactionId(station, config)) &&
                HasConfiguredContraband(config);
        }

        public string GetMarketFactionId(Station station)
        {
            return TryGetMarketConfig(station, out StationMarketConfig config)
                ? GetEffectiveMarketFactionId(station, config)
                : FactionManager.NormalizeFactionId(station?.FactionId);
        }

        public FactionAccessResult EvaluateBlackMarketAccess(Station station, ReputationManager reputationManager)
        {
            string factionId = GetMarketFactionId(station);
            return BlackMarketPolicy.EvaluateAccess(
                reputationManager,
                factionId,
                station?.Name,
                HasBlackMarketForStation(station));
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
            _shortageSinceMilliseconds.Clear();
            _supplyCapacityReservations.Clear();

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

        public List<StationMarketListing> GetListingsForStation(
            Station station,
            MarketSurface surface = MarketSurface.Ordinary)
        {
            string stationKey = GetStationKey(station?.Name, station?.Config?.Description);
            if (string.IsNullOrWhiteSpace(stationKey))
            {
                return surface == MarketSurface.BlackMarket
                    ? new List<StationMarketListing>()
                    : FilterListings(BuildFallbackListings(), surface);
            }

            if (!_marketConfigs.TryGetValue(stationKey, out var config))
            {
                return surface == MarketSurface.BlackMarket
                    ? new List<StationMarketListing>()
                    : FilterListings(BuildFallbackListings(), surface);
            }

            if (_runtimeMarkets.TryGetValue(stationKey, out var runtimeListings))
            {
                AdvanceListings(runtimeListings);
                return FilterListings(CloneListings(runtimeListings), surface);
            }

            runtimeListings = BuildRuntimeListings(config);
            _runtimeMarkets[stationKey] = runtimeListings;
            AdvanceListings(runtimeListings);
            return FilterListings(CloneListings(runtimeListings), surface);
        }

        public List<StationMarketListing> GetBlackMarketListingsForStation(Station station)
        {
            return GetListingsForStation(station, MarketSurface.BlackMarket);
        }

        /// <summary>
        /// Returns a sink-only fence quote for a legal commodity that is
        /// currently stolen. It is derived from the local clean-market sell
        /// quote and is never added to canonical black-market stock.
        /// </summary>
        public StationMarketListing GetFenceListing(Station station, Commodity commodity)
        {
            StationMarketListing cleanListing = GetListingForCommodity(station, commodity, MarketSurface.Ordinary);
            if (cleanListing?.Commodity == null || cleanListing.Commodity.IsContraband ||
                !cleanListing.IsAvailable || cleanListing.SellPrice <= 0)
                return null;

            int fencePrice = Math.Max(1, (int)Math.Floor(cleanListing.SellPrice * 0.60m));
            return new StationMarketListing(cleanListing.Commodity, 0, fencePrice, 1, 0, true);
        }

        public StationMarketListing GetListingForCommodity(
            Station station,
            Commodity commodity,
            MarketSurface surface = MarketSurface.Ordinary)
        {
            if (station == null || commodity == null)
            {
                return null;
            }

            var listings = GetListingsForStation(station, surface);
            return listings.FirstOrDefault(l =>
                string.Equals(l.Commodity.Id, commodity.Id, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(l.Commodity.Name, commodity.Name, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Returns the shortage derived from the authoritative runtime
        /// listing. The small age cache is only hysteresis/maturity metadata;
        /// stock remains the sole economic authority.
        /// </summary>
        public MarketShortageState GetShortageState(Station station, Commodity commodity)
        {
            if (station == null || commodity == null || !HasMarketConfigForStation(station))
                return null;

            string stationKey = GetStationKey(station.Name, station.Config?.Description);
            StationMarketListing listing = GetMutableListing(stationKey, commodity);
            if (!IsEligibleShortageListing(listing))
                return null;

            int target = listing.BaselineStock;
            int stock = Math.Clamp(listing.Stock, 0, listing.MaximumStock);
            long deficitLong = Math.Max(0L, (long)target - stock);
            string shortageKey = BuildShortageKey(stationKey, listing.Commodity);

            if (MarketShortagePolicy.IsRecovered(stock, target))
            {
                _shortageSinceMilliseconds.Remove(shortageKey);
            }
            else if (deficitLong >= MarketShortagePolicy.MinimumDeficitUnits &&
                     MarketShortagePolicy.IsBelowEnterThreshold(stock, target) &&
                     !_shortageSinceMilliseconds.ContainsKey(shortageKey))
            {
                _shortageSinceMilliseconds[shortageKey] = _elapsedMilliseconds;
            }

            bool shortageLatched = _shortageSinceMilliseconds.TryGetValue(shortageKey, out long since);
            long age = shortageLatched
                ? Math.Max(0L, _elapsedMilliseconds - Math.Min(_elapsedMilliseconds, since))
                : 0L;
            int stockPercent = target > 0
                ? (int)Math.Clamp((long)stock * 100L / target, 0L, 100_000L)
                : 0;

            return new MarketShortageState
            {
                StationId = GetStationId(station),
                StationName = station.Name ?? string.Empty,
                Commodity = listing.Commodity,
                CurrentStock = stock,
                TargetStock = target,
                Capacity = listing.MaximumStock,
                Deficit = (int)Math.Min(deficitLong, int.MaxValue),
                StockPercent = stockPercent,
                EnterThresholdPercent = MarketShortagePolicy.EnterThresholdPercent,
                RecoveryThresholdPercent = MarketShortagePolicy.RecoveryThresholdPercent,
                Level = MarketShortagePolicy.GetLevel(stock, target, shortageLatched),
                IsMature = shortageLatched && age >= MarketShortagePolicy.MaturitySeconds * 1000L,
                AgeMilliseconds = age
            };
        }

        public bool IsShortage(Station station, Commodity commodity) =>
            GetShortageState(station, commodity)?.IsShortage == true;

        public IReadOnlyList<MarketShortageState> GetShortageStatesForStation(
            Station station,
            bool matureOnly = false)
        {
            if (station == null || !HasMarketConfigForStation(station))
                return Array.Empty<MarketShortageState>();

            return GetListingsForStation(station, MarketSurface.Ordinary)
                .Select(listing => GetShortageState(station, listing?.Commodity))
                .Where(state => state != null && state.IsShortage && (!matureOnly || state.IsMature))
                .OrderByDescending(state => state.Level)
                .ThenByDescending(state => state.Deficit)
                .ThenBy(state => state.Commodity.Id, StringComparer.OrdinalIgnoreCase)
                .Take(MarketShortagePolicy.MaximumOffersPerStation)
                .ToList();
        }

        public bool TryBuy(Station station, Commodity commodity, int quantity, PlayerCredits credits, CargoHold cargoHold, out string message)
        {
            return TryBuy(station, commodity, quantity, credits, cargoHold, MarketSurface.Ordinary, out message);
        }

        public bool TryBuy(
            Station station,
            Commodity commodity,
            int quantity,
            PlayerCredits credits,
            CargoHold cargoHold,
            MarketSurface surface,
            out string message)
        {
            return TryBuy(station, commodity, quantity, credits, cargoHold, surface, null, out message);
        }

        public bool TryBuy(
            Station station,
            Commodity commodity,
            int quantity,
            PlayerCredits credits,
            CargoHold cargoHold,
            MarketSurface surface,
            ReputationManager reputationManager,
            out string message)
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
            if (!IsValidCommodity(marketCommodity))
            {
                message = "Commodity unavailable at this station.";
                return false;
            }

            if (!CanTradeOnSurface(station, marketCommodity, surface, reputationManager, out message))
            {
                return false;
            }

            if (!listing.IsAvailable || listing.BuyPrice <= 0 || listing.Stock < 0)
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
            listing.RecoveryEnabled = true;
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
            return TrySell(station, commodity, quantity, credits, cargoHold, MarketSurface.Ordinary, out message);
        }

        public bool TrySell(
            Station station,
            Commodity commodity,
            int quantity,
            PlayerCredits credits,
            CargoHold cargoHold,
            MarketSurface surface,
            out string message)
        {
            return TrySell(station, commodity, quantity, credits, cargoHold, surface, null, out message);
        }

        public bool TrySell(
            Station station,
            Commodity commodity,
            int quantity,
            PlayerCredits credits,
            CargoHold cargoHold,
            MarketSurface surface,
            ReputationManager reputationManager,
            out string message)
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
            if (!IsValidCommodity(marketCommodity))
            {
                message = "Commodity unavailable at this station.";
                return false;
            }

            if (!CanTradeOnSurface(station, marketCommodity, surface, reputationManager, out message))
            {
                return false;
            }

            if (marketCommodity.IsMissionCargo)
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
            int sellableQuantity = surface == MarketSurface.BlackMarket
                ? cargoHold.GetSellableCommodityQuantity(marketCommodity.Name)
                : cargoHold.GetSellableCleanCommodityQuantity(marketCommodity.Name);
            if (sellableQuantity < quantity)
            {
                int stolenQuantity = cargoHold.GetStolenCommodityQuantity(marketCommodity.Name);
                int cleanSellable = cargoHold.GetSellableCleanCommodityQuantity(marketCommodity.Name);
                message = ownedQuantity > sellableQuantity && stolenQuantity > 0 && surface != MarketSurface.BlackMarket
                    ? $"{stolenQuantity:N0} units flagged as stolen property cannot be sold to a lawful dealer."
                    : ownedQuantity > sellableQuantity
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

            int availableCapacity = surface == MarketSurface.BlackMarket
                ? Math.Max(0, listing.MaximumStock - listing.Stock)
                : GetAvailableSupplyCapacity(station, marketCommodity);
            if (availableCapacity < quantity)
            {
                message = $"Station inventory can hold only {availableCapacity:N0} more units.";
                return false;
            }

            if (!cargoHold.RemoveSellableCommodity(
                    marketCommodity,
                    quantity,
                    preferStolen: surface == MarketSurface.BlackMarket))
            {
                message = surface != MarketSurface.BlackMarket && cargoHold.GetStolenCommodityQuantity(marketCommodity.Name) > 0
                    ? "Stolen property cannot be sold to a lawful dealer."
                    : cargoHold.GetMissionReservedQuantity(marketCommodity.Name) > 0
                        ? "Mission cargo cannot be sold."
                        : "Cargo removal failed.";
                return false;
            }

            credits.AddCredits(totalValue);
            listing.Stock = Math.Min(listing.MaximumStock, listing.Stock + quantity);
            listing.RecoveryEnabled = true;
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

            int availableCapacity = GetAvailableSupplyCapacity(station, listing.Commodity);
            if (availableCapacity < quantity)
            {
                message = $"Station inventory can hold only {availableCapacity:N0} more units.";
                return false;
            }

            return true;
        }

        /// <summary>
        /// Returns the exact bounded capacity available to a physical
        /// shipment at a configured destination listing.
        /// </summary>
        public int GetAvailableSupplyCapacity(Station station, Commodity commodity)
        {
            if (!TryResolveSupplyListing(station, commodity, 1, out StationMarketListing listing, out _))
                return 0;

            int reserved = GetReservedCapacity(
                GetStationKey(station.Name, station.Config?.Description),
                listing.Commodity.Id,
                excludingMissionId: 0);
            return Math.Max(0, listing.MaximumStock - listing.Stock - reserved);
        }

        /// <summary>
        /// Returns the exact bounded capacity available to a physical
        /// contraband shipment at a configured criminal receiver. This is
        /// deliberately separate from GetAvailableSupplyCapacity, whose
        /// lawful supply path rejects contraband by policy.
        /// </summary>
        public int GetAvailableCriminalSupplyCapacity(Station station, Commodity commodity)
        {
            Commodity canonical = ResolveCommodity(commodity?.Id ?? commodity?.Name);
            if (station == null || canonical?.IsContraband != true ||
                !HasBlackMarketForStation(station))
                return 0;

            StationMarketListing listing = GetMutableListing(
                GetStationKey(station.Name, station.Config?.Description), canonical);
            if (listing == null || !listing.IsAvailable || listing.SellPrice <= 0 ||
                !listing.Commodity.IsContraband)
                return 0;

            int reserved = GetReservedCapacity(
                GetStationKey(station.Name, station.Config?.Description),
                listing.Commodity.Id,
                excludingMissionId: 0);
            return Math.Max(0, listing.MaximumStock - listing.Stock - reserved);
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

            int availableCapacity = GetAvailableSupplyCapacity(station, listing.Commodity);
            if (availableCapacity < quantity)
            {
                message = $"Station inventory can hold only {availableCapacity:N0} more units.";
                return false;
            }

            listing.Stock += quantity;
            listing.RecoveryEnabled = true;
            listing.ImmediateSellPriceCeiling = 0;
            listing.RecoveryRemainderMilliseconds = 0;
            RefreshPrices(listing);
            message = $"Delivered {quantity} {listing.Commodity.Name}; station stock is now {listing.Stock:N0}.";
            return true;
        }

        /// <summary>
        /// Validates a physical Rogue haul at an existing criminal receiver.
        /// Contraband enters the station's canonical black-market listing;
        /// legal stolen cargo follows the existing Phase 57 fence-only policy.
        /// This is an NPC receipt boundary, so it deliberately does not ask
        /// for the player's reputation or create a second inventory.
        /// </summary>
        public bool CanReceiveCriminalSupply(
            Station station,
            Commodity commodity,
            int quantity,
            out string message)
        {
            message = string.Empty;
            Commodity canonical = ResolveCommodity(commodity?.Id ?? commodity?.Name);
            if (station == null || canonical == null || quantity <= 0)
            {
                message = "No valid criminal supply receipt.";
                return false;
            }

            if (canonical.IsMissionCargo || !HasBlackMarketForStation(station))
            {
                message = "This station is not an eligible criminal receiver.";
                return false;
            }

            StationMarketListing listing = GetMutableListing(
                GetStationKey(station.Name, station.Config?.Description), canonical);
            if (listing == null || !listing.IsAvailable || listing.SellPrice <= 0)
            {
                message = "The criminal receiver will not accept this commodity.";
                return false;
            }

            if (canonical.IsContraband)
            {
                if (!listing.Commodity.IsContraband ||
                    (long)listing.Stock + quantity > listing.MaximumStock)
                {
                    message = "The criminal receiver has no room for this haul.";
                    return false;
                }

                return true;
            }

            if (GetFenceListing(station, canonical) == null)
            {
                message = "No fence quote exists for this legal stolen cargo.";
                return false;
            }

            return true;
        }

        /// <summary>
        /// Settles one exact physical Rogue haul at an existing criminal
        /// receiver. The call is intentionally all-or-nothing so a carrier's
        /// saved haul can never be partially credited and then delivered
        /// again after a load.
        /// </summary>
        public bool TryAddCriminalSupply(
            Station station,
            Commodity commodity,
            int quantity,
            out int acceptedQuantity,
            out string message)
        {
            acceptedQuantity = 0;
            if (!CanReceiveCriminalSupply(station, commodity, quantity, out message))
                return false;

            Commodity canonical = ResolveCommodity(commodity?.Id ?? commodity?.Name);
            if (canonical.IsContraband)
            {
                StationMarketListing listing = GetMutableListing(
                    GetStationKey(station.Name, station.Config?.Description), canonical);
                listing.Stock = checked(listing.Stock + quantity);
                listing.RecoveryEnabled = true;
                listing.ImmediateSellPriceCeiling = 0;
                listing.RecoveryRemainderMilliseconds = 0;
                RefreshPrices(listing);
                message = $"Criminal receiver accepted {quantity} {listing.Commodity.Name}; stock is now {listing.Stock:N0}.";
            }
            else
            {
                message = $"Fence accepted {quantity} {canonical.Name}; legal stolen cargo remains outside canonical market stock.";
            }

            acceptedQuantity = quantity;
            return true;
        }

        /// <summary>
        /// Removes real contraband stock from an existing criminal market for
        /// a physical criminal shipment. The normal supply helper rejects
        /// contraband by policy and therefore cannot reserve this cargo.
        /// </summary>
        public bool TryRemoveCriminalSupply(
            Station station,
            Commodity commodity,
            int quantity,
            int minimumRemainingStock,
            out string message)
        {
            message = string.Empty;
            Commodity canonical = ResolveCommodity(commodity?.Id ?? commodity?.Name);
            if (station == null || canonical?.IsContraband != true || quantity <= 0 ||
                !HasBlackMarketForStation(station))
            {
                message = "No valid criminal supply export.";
                return false;
            }

            StationMarketListing listing = GetMutableListing(
                GetStationKey(station.Name, station.Config?.Description), canonical);
            if (listing == null || !listing.IsAvailable || listing.SellPrice <= 0 ||
                !listing.Commodity.IsContraband)
            {
                message = "Contraband unavailable at this criminal market.";
                return false;
            }

            minimumRemainingStock = Math.Max(0, minimumRemainingStock);
            if (listing.Stock - quantity < minimumRemainingStock)
            {
                message = $"Criminal market can export only {Math.Max(0, listing.Stock - minimumRemainingStock)} units.";
                return false;
            }

            listing.Stock -= quantity;
            listing.RecoveryEnabled = true;
            listing.ImmediateSellPriceCeiling = 0;
            listing.RecoveryRemainderMilliseconds = 0;
            RefreshPrices(listing);
            message = $"Exported {quantity} contraband {listing.Commodity.Name}; criminal stock is now {listing.Stock:N0}.";
            return true;
        }

        /// <summary>
        /// Reserves normal market capacity for an accepted emergency supply
        /// contract. The reservation is bounded by the listing capacity and
        /// prevents ambient traders or ordinary sales from making the accepted
        /// agreement impossible. It is not a commodity reservation.
        /// </summary>
        public bool TryReserveSupplyContractCapacity(
            int missionId,
            Station station,
            Commodity commodity,
            int quantity,
            out string message)
        {
            message = string.Empty;
            if (missionId <= 0 || station == null || commodity == null || quantity <= 0 ||
                !TryResolveSupplyListing(station, commodity, quantity, out StationMarketListing listing, out message))
            {
                return false;
            }

            string stationKey = GetStationKey(station.Name, station.Config?.Description);
            string commodityId = listing.Commodity.Id;
            if (_supplyCapacityReservations.TryGetValue(missionId, out SupplyCapacityReservation existing))
            {
                if (!string.Equals(existing.StationKey, stationKey, StringComparison.OrdinalIgnoreCase) ||
                    !string.Equals(existing.CommodityId, commodityId, StringComparison.OrdinalIgnoreCase) ||
                    existing.Quantity != quantity)
                {
                    message = "An incompatible supply contract capacity reservation already exists.";
                    return false;
                }

                return true;
            }

            int reservedByOthers = GetReservedCapacity(stationKey, commodityId, missionId);
            if ((long)listing.Stock + reservedByOthers + quantity > listing.MaximumStock)
            {
                message = $"Station inventory can use only {Math.Max(0, listing.MaximumStock - listing.Stock - reservedByOthers):N0} more units.";
                return false;
            }

            _supplyCapacityReservations[missionId] = new SupplyCapacityReservation
            {
                StationKey = stationKey,
                CommodityId = commodityId,
                Quantity = quantity
            };
            return true;
        }

        public void ReleaseSupplyContractCapacity(int missionId)
        {
            if (missionId > 0)
                _supplyCapacityReservations.Remove(missionId);
        }

        public bool CanAddSupplyForContract(
            int missionId,
            Station station,
            Commodity commodity,
            int quantity,
            out string message)
        {
            message = string.Empty;
            if (missionId <= 0 || !_supplyCapacityReservations.TryGetValue(missionId, out SupplyCapacityReservation reservation))
            {
                message = "Supply contract capacity is not reserved.";
                return false;
            }

            if (!TryResolveSupplyListing(station, commodity, quantity, out StationMarketListing listing, out message))
                return false;

            string stationKey = GetStationKey(station.Name, station.Config?.Description);
            if (!string.Equals(reservation.StationKey, stationKey, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(reservation.CommodityId, listing.Commodity.Id, StringComparison.OrdinalIgnoreCase) ||
                quantity > reservation.Quantity)
            {
                message = "Supply contract destination or quantity is invalid.";
                return false;
            }

            int reservedByOthers = GetReservedCapacity(stationKey, listing.Commodity.Id, missionId);
            if ((long)listing.Stock + reservedByOthers + quantity > listing.MaximumStock)
            {
                message = $"Station inventory can use only {Math.Max(0, listing.MaximumStock - listing.Stock - reservedByOthers):N0} more units.";
                return false;
            }

            return true;
        }

        public bool TryAddSupplyForContract(
            int missionId,
            Station station,
            Commodity commodity,
            int quantity,
            out string message)
        {
            if (!CanAddSupplyForContract(missionId, station, commodity, quantity, out message))
                return false;

            StationMarketListing listing = GetMutableListing(
                GetStationKey(station.Name, station.Config?.Description), commodity);
            listing.Stock += quantity;
            listing.RecoveryEnabled = true;
            listing.ImmediateSellPriceCeiling = 0;
            listing.RecoveryRemainderMilliseconds = 0;
            RefreshPrices(listing);

            SupplyCapacityReservation reservation = _supplyCapacityReservations[missionId];
            reservation.Quantity -= quantity;
            if (reservation.Quantity <= 0)
                _supplyCapacityReservations.Remove(missionId);

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
            listing.RecoveryEnabled = true;
            listing.ImmediateSellPriceCeiling = 0;
            listing.RecoveryRemainderMilliseconds = 0;
            RefreshPrices(listing);
            message = $"Exported {quantity} {listing.Commodity.Name}; station stock is now {listing.Stock:N0}.";
            return true;
        }

        public Commodity GetCommodityByIndex(
            int index,
            Station station = null,
            MarketSurface surface = MarketSurface.Ordinary)
        {
            var listings = GetListingsForStation(station, surface);
            if (index < 0 || index >= listings.Count)
            {
                return null;
            }

            return listings[index].Commodity;
        }

        public int GetMarketCount(
            Station station = null,
            MarketSurface surface = MarketSurface.Ordinary)
        {
            return GetListingsForStation(station, surface).Count;
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
                            Stock = Math.Clamp(listing.Stock, 0, listing.MaximumStock),
                            DemandLevel = listing.DemandLevel,
                            IsAvailable = listing.IsAvailable,
                            RecoveryRemainderMilliseconds = Math.Max(0, listing.RecoveryRemainderMilliseconds),
                            ImmediateSellPriceCeiling = Math.Max(0, listing.ImmediateSellPriceCeiling),
                            ShortageAgeMilliseconds = GetShortageAgeMilliseconds(kvp.Key, listing),
                            ConsumptionRemainder = Math.Clamp(listing.ConsumptionRemainder, 0L, MaximumConsumptionRemainder),
                            RecoveryEnabled = listing.RecoveryEnabled
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
                            Stock = Math.Clamp(listing.Stock, 0, configuredListing.MaximumStock),
                            RecoveryRemainderMilliseconds = Math.Max(0, listing.RecoveryRemainderMilliseconds),
                            ConsumptionRemainder = Math.Clamp(listing.ConsumptionRemainder, 0L, MaximumConsumptionRemainder),
                            ImmediateSellPriceCeiling = Math.Clamp(
                                listing.ImmediateSellPriceCeiling,
                                0,
                                configuredListing.BaseBuyPrice),
                            LastAdvancedMilliseconds = _elapsedMilliseconds,
                            RecoveryEnabled = listing.RecoveryEnabled
                        };
                        RestoreShortageAge(stationKey, restoredListing, listing.ShortageAgeMilliseconds);
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
                    // A newly created runtime market belongs to the current
                    // new-game simulation, so its first access must account
                    // for elapsed simulation time. Restored listings set this
                    // explicitly to the saved time and never catch up wall
                    // clock absence.
                    LastAdvancedMilliseconds = 0L
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

        private List<StationMarketListing> FilterListings(
            IEnumerable<StationMarketListing> listings,
            MarketSurface surface)
        {
            IEnumerable<StationMarketListing> filtered = listings ?? Enumerable.Empty<StationMarketListing>();
            filtered = surface == MarketSurface.BlackMarket
                ? filtered.Where(listing => listing?.Commodity?.IsContraband == true)
                : filtered.Where(listing => listing?.Commodity?.IsContraband != true);
            return filtered.ToList();
        }

        private bool CanTradeOnSurface(
            Station station,
            Commodity commodity,
            MarketSurface surface,
            ReputationManager reputationManager,
            out string message)
        {
            message = string.Empty;
            if (commodity == null)
            {
                message = "No commodity selected.";
                return false;
            }

            if (surface == MarketSurface.BlackMarket)
            {
                FactionAccessResult access = EvaluateBlackMarketAccess(station, reputationManager);
                if (!access.IsAllowed)
                {
                    message = access.BuildFailureMessage("Black Market");
                    return false;
                }

                if (!commodity.IsContraband)
                {
                    message = "Black Market only handles contraband.";
                    return false;
                }

                return true;
            }

            if (commodity.IsContraband)
            {
                message = "Ordinary commodity dealers will not handle contraband.";
                return false;
            }

            return true;
        }

        private bool TryGetMarketConfig(Station station, out StationMarketConfig config)
        {
            string stationKey = GetStationKey(station?.Name, station?.Config?.Description);
            if (!string.IsNullOrWhiteSpace(stationKey) && _marketConfigs.TryGetValue(stationKey, out config))
            {
                return true;
            }

            config = null;
            return false;
        }

        private string GetEffectiveMarketFactionId(Station station, StationMarketConfig config)
        {
            string stationFactionId = FactionManager.NormalizeFactionId(station?.FactionId);
            string configuredFactionId = FactionManager.NormalizeFactionId(config?.FactionId);

            // Several older station fixtures omitted faction_id and rely on
            // the market configuration's existing faction authority.
            return string.Equals(stationFactionId, FactionManager.NeutralCivilians, StringComparison.OrdinalIgnoreCase) &&
                   !string.IsNullOrWhiteSpace(config?.FactionId)
                ? configuredFactionId
                : stationFactionId;
        }

        private bool HasConfiguredContraband(StationMarketConfig config)
        {
            return (config?.Goods ?? new List<StationMarketGoodConfig>())
                .Select(good => ResolveCommodity(good?.CommodityId))
                .Any(commodity => commodity?.IsContraband == true);
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

            long boundedElapsedMilliseconds = Math.Min(elapsedMilliseconds, MaximumListingCatchUpMilliseconds);
            bool recoveryInProgress = listing.RecoveryEnabled && listing.Stock != listing.BaselineStock;
            if (recoveryInProgress)
            {
                // Existing transaction-driven recovery remains intact, but
                // autonomous demand is allowed to run continuously once a
                // listing has entered its demand-driven phase. This prevents
                // recovery from silently erasing the consumption sink.
                RecoverStock(listing, elapsedMilliseconds);
            }
            else
            {
                ConsumeStock(listing, boundedElapsedMilliseconds);
            }
            listing.LastAdvancedMilliseconds = _elapsedMilliseconds;
            RefreshPrices(listing);
        }

        private static void ConsumeStock(StationMarketListing listing, long elapsedMilliseconds)
        {
            if (listing == null || elapsedMilliseconds <= 0 ||
                listing.ConsumptionRateMilliUnitsPerMinute <= 0 ||
                !listing.IsAvailable || listing.Commodity == null ||
                listing.Commodity.IsContraband || listing.Commodity.IsMissionCargo ||
                listing.BaselineStock <= 0)
            {
                if (listing != null && listing.ConsumptionRateMilliUnitsPerMinute <= 0)
                    listing.ConsumptionRemainder = 0;
                return;
            }

            long work = checked((long)listing.ConsumptionRateMilliUnitsPerMinute * elapsedMilliseconds +
                Math.Clamp(listing.ConsumptionRemainder, 0L, MaximumConsumptionRemainder));
            long requestedUnits = work / ConsumptionDenominator;
            listing.ConsumptionRemainder = work % ConsumptionDenominator;

            int available = Math.Max(0, listing.Stock);
            if (available <= 0)
            {
                // There is no persistent backorder. Phase 60 shortage state
                // already represents the unmet need from real stock.
                listing.ConsumptionRemainder = 0;
                listing.RecoveryEnabled = false;
                return;
            }

            long consumed = Math.Min(requestedUnits, (long)available);
            if (consumed > 0)
            {
                listing.Stock = (int)Math.Max(0L, (long)listing.Stock - consumed);
                listing.RecoveryEnabled = false;
            }

            if (requestedUnits >= available)
                listing.ConsumptionRemainder = 0;
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

            listing.Stock = Math.Clamp(listing.Stock, 0, listing.MaximumStock);
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
                good.RecoverySeconds < 0 ||
                (good.ConsumptionRatePerMinute.HasValue &&
                    (double.IsNaN(good.ConsumptionRatePerMinute.Value) ||
                     double.IsInfinity(good.ConsumptionRatePerMinute.Value) ||
                     good.ConsumptionRatePerMinute.Value < 0d ||
                     good.ConsumptionRatePerMinute.Value > StationMarketListing.MaximumConsumptionRateMilliUnitsPerMinute / (double)StationMarketListing.ConsumptionScale)))
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

        private static bool IsEligibleShortageListing(StationMarketListing listing)
        {
            return listing != null && IsValidCommodity(listing.Commodity) &&
                !listing.Commodity.IsContraband && !listing.Commodity.IsMissionCargo &&
                listing.IsAvailable && listing.BaseBuyPrice > 0 && listing.BaseSellPrice > 0 &&
                listing.BaselineStock > 0 && listing.Stock >= 0 && listing.MaximumStock >= listing.BaselineStock;
        }

        private static string BuildShortageKey(string stationKey, Commodity commodity) =>
            $"{stationKey}:{NormalizeKey(commodity?.Id)}";

        private long GetShortageAgeMilliseconds(string stationKey, StationMarketListing listing)
        {
            if (!IsEligibleShortageListing(listing))
                return 0L;

            string key = BuildShortageKey(stationKey, listing.Commodity);
            if (!_shortageSinceMilliseconds.TryGetValue(key, out long since))
                return 0L;

            return Math.Clamp(_elapsedMilliseconds - Math.Min(_elapsedMilliseconds, since), 0L, (long)MarketShortagePolicy.MaturitySeconds * 1000L * 20L);
        }

        private void RestoreShortageAge(string stationKey, StationMarketListing listing, long ageMilliseconds)
        {
            if (!IsEligibleShortageListing(listing) ||
                !MarketShortagePolicy.IsBelowEnterThreshold(listing.Stock, listing.BaselineStock) ||
                listing.BaselineStock - listing.Stock < MarketShortagePolicy.MinimumDeficitUnits)
                return;

            long boundedAge = Math.Clamp(ageMilliseconds, 0L, (long)MarketShortagePolicy.MaturitySeconds * 1000L * 20L);
            _shortageSinceMilliseconds[BuildShortageKey(stationKey, listing.Commodity)] =
                Math.Max(0L, _elapsedMilliseconds - boundedAge);
        }

        private int GetReservedCapacity(string stationKey, string commodityId, int excludingMissionId)
        {
            if (string.IsNullOrWhiteSpace(stationKey) || string.IsNullOrWhiteSpace(commodityId))
                return 0;

            long reserved = _supplyCapacityReservations
                .Where(entry => entry.Key != excludingMissionId &&
                    string.Equals(entry.Value?.StationKey, stationKey, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(entry.Value?.CommodityId, commodityId, StringComparison.OrdinalIgnoreCase))
                .Sum(entry => (long)Math.Max(0, entry.Value?.Quantity ?? 0));
            return (int)Math.Clamp(reserved, 0L, int.MaxValue);
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
