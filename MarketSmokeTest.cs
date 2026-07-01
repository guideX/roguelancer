using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace Roguelancer
{
    /// <summary>
    /// Developer-only validation harness for station-specific markets.
    /// </summary>
    internal sealed class MarketSmokeTest
    {
        private static readonly string[] ConfiguredStations =
        {
            "Newark Station",
            "Rochester Base",
            "Buffalo Base",
            "Fort Bush",
            "Detroit Munitions"
        };

        private const string FallbackStationName = "Trenton Outpost";

        private readonly IReadOnlyList<Station> _stations;
        private readonly CommodityDealer _commodityDealer;
        private readonly StationDockUI _stationDockUi;
        private readonly Ship _tempShip;
        private readonly PlayerCredits _tempCredits;
        private readonly FieldInfo _selectedCommodityIndexField;

        public MarketSmokeTest(IReadOnlyList<Station> stations, SpriteFont font, Texture2D pixel)
        {
            _stations = stations ?? Array.Empty<Station>();
            _commodityDealer = RunSilenced(() => new CommodityDealer());
            _tempShip = new Ship(Vector3.Zero);
            _tempCredits = new PlayerCredits(50_000);

            var tempShipDealer = new ShipDealer();
            _stationDockUi = new StationDockUI(font, pixel, tempShipDealer, _commodityDealer, null, null);
            _selectedCommodityIndexField = typeof(StationDockUI).GetField("_selectedCommodityIndex", BindingFlags.Instance | BindingFlags.NonPublic)
                ?? throw new InvalidOperationException("Could not access StationDockUI selection state.");
        }

        public (int Passed, int Failed) Run()
        {
            int passed = 0;
            int failed = 0;
            int selectionSeed = 15;

            foreach (string stationName in ConfiguredStations)
            {
                var result = RunSilenced(() => RunConfiguredStationSmoke(stationName, selectionSeed));
                if (result.Success)
                {
                    passed++;
                    selectionSeed = result.NextSelectionSeed;
                    Console.WriteLine($"[MARKET SMOKE] PASS {result.Summary}");
                }
                else
                {
                    failed++;
                    Console.WriteLine($"[MARKET SMOKE] FAIL {stationName}: {result.FailureReason}");
                }
            }

            var fallbackResult = RunSilenced(() => RunFallbackStationSmoke(selectionSeed));
            if (fallbackResult.Success)
            {
                passed++;
                Console.WriteLine($"[MARKET SMOKE] PASS {fallbackResult.Summary}");
            }
            else
            {
                failed++;
                Console.WriteLine($"[MARKET SMOKE] FAIL fallback station: {fallbackResult.FailureReason}");
            }

            var balanceResult = RunSilenced(ValidateEarlyRouteBalance);
            if (balanceResult.Success)
            {
                passed++;
                Console.WriteLine("[MARKET SMOKE] PASS early route balance");
            }
            else
            {
                failed++;
                Console.WriteLine($"[MARKET SMOKE] FAIL early route balance: {balanceResult.FailureReason}");
            }

            Console.WriteLine($"[MARKET SMOKE] RESULT: {passed} passed, {failed} failed");
            return (passed, failed);
        }

        private (bool Success, string Summary, int NextSelectionSeed, string FailureReason) RunConfiguredStationSmoke(string stationName, int selectionSeed)
        {
            var station = ResolveStation(stationName);
            if (station == null)
            {
                return (false, string.Empty, selectionSeed, $"station '{stationName}' was not found");
            }

            return RunStationSmoke(station, selectionSeed, requireStationMarket: true);
        }

        private (bool Success, string Summary, int NextSelectionSeed, string FailureReason) RunStationSmoke(Station station, int selectionSeed, bool requireStationMarket)
        {
            if (station == null)
            {
                return (false, string.Empty, selectionSeed, "station was null");
            }

            int creditsBefore = _tempCredits.Credits;
            Dictionary<string, int> cargoSnapshot = _tempShip.CargoHold.GetAllCommodities();

            try
            {
                bool docked = _stationDockUi.DockAtStation(station);
                if (!docked)
                {
                    string deniedReason = _stationDockUi.LastDockingDeniedReason;
                    return (false, string.Empty, selectionSeed, string.IsNullOrWhiteSpace(deniedReason)
                        ? $"failed to dock at {station.Name}"
                        : deniedReason);
                }

                _stationDockUi.NavigateToArea(StationArea.Dealer);

                if (requireStationMarket && _commodityDealer.IsUsingLegacyFallback)
                {
                    return (false, string.Empty, selectionSeed, $"legacy fallback market selected for {station.Name}");
                }

                var listings = _commodityDealer.CurrentMarketListings;
                if (!ValidateListingsCanBeRead(listings, out int availableCount, out string readFailure))
                {
                    return (false, string.Empty, selectionSeed, readFailure);
                }

                if (!ExerciseSelectionSafety(selectionSeed, listings.Count, out string selectionFailure))
                {
                    return (false, string.Empty, selectionSeed, selectionFailure);
                }

                var tradeListing = SelectSafeTradeListing(listings);
                if (tradeListing == null)
                {
                    return (false, string.Empty, selectionSeed, $"no safe buy/sell commodity found at {station.Name}");
                }

                if (!RunTradeCycle(station, tradeListing, out string tradeFailure))
                {
                    return (false, string.Empty, selectionSeed, tradeFailure);
                }

                string summary = $"{station.Name}: bought/sold {tradeListing.Commodity.Name}";
                int nextSelectionSeed = availableCount + 5;
                return (true, summary, nextSelectionSeed, string.Empty);
            }
            catch (Exception ex)
            {
                return (false, string.Empty, selectionSeed, ex.Message);
            }
            finally
            {
                RestoreTemporaryState(creditsBefore, cargoSnapshot);

                if (_stationDockUi.IsDocked)
                {
                    _stationDockUi.Undock();
                }
            }
        }

        private (bool Success, string Summary, string FailureReason) RunFallbackStationSmoke(int selectionSeed)
        {
            var station = ResolveStation(FallbackStationName);
            if (station == null)
            {
                return (false, string.Empty, $"fallback station '{FallbackStationName}' was not found");
            }

            int creditsBefore = _tempCredits.Credits;
            Dictionary<string, int> cargoSnapshot = _tempShip.CargoHold.GetAllCommodities();

            try
            {
                bool docked = _stationDockUi.DockAtStation(station);
                if (!docked)
                {
                    string deniedReason = _stationDockUi.LastDockingDeniedReason;
                    return (false, string.Empty, string.IsNullOrWhiteSpace(deniedReason)
                        ? $"failed to dock at fallback station {station.Name}"
                        : deniedReason);
                }

                _stationDockUi.NavigateToArea(StationArea.Dealer);

                if (!_commodityDealer.IsUsingLegacyFallback)
                {
                    return (false, string.Empty, $"station market unexpectedly active at fallback station {station.Name}");
                }

                var listings = _commodityDealer.CurrentMarketListings;
                if (!ValidateListingsCanBeRead(listings, out _, out string readFailure))
                {
                    return (false, string.Empty, readFailure);
                }

                if (!ExerciseSelectionSafety(selectionSeed, listings.Count, out string selectionFailure))
                {
                    return (false, string.Empty, selectionFailure);
                }

                var firstListing = _commodityDealer.GetListingByIndex(0);
                if (firstListing == null)
                {
                    return (false, string.Empty, "fallback listing index 0 was not readable");
                }

                return (true, "fallback station: legacy dealer active", string.Empty);
            }
            catch (Exception ex)
            {
                return (false, string.Empty, ex.Message);
            }
            finally
            {
                RestoreTemporaryState(creditsBefore, cargoSnapshot);

                if (_stationDockUi.IsDocked)
                {
                    _stationDockUi.Undock();
                }
            }
        }

        private (bool Success, string FailureReason) ValidateEarlyRouteBalance()
        {
            Station fortBush = ResolveStation("Fort Bush");
            Station newark = ResolveStation("Newark Station");
            Station rochester = ResolveStation("Rochester Base");
            Station buffalo = ResolveStation("Buffalo Base");

            if (fortBush == null || newark == null || rochester == null || buffalo == null)
            {
                return Fail("one or more early-route stations were not found");
            }

            if (!TryGetListings(fortBush, out var fortListings, out string fortFailure))
            {
                return Fail(fortFailure);
            }

            if (!TryGetListings(newark, out var newarkListings, out string newarkFailure))
            {
                return Fail(newarkFailure);
            }

            if (!TryGetListings(rochester, out var rochesterListings, out string rochesterFailure))
            {
                return Fail(rochesterFailure);
            }

            if (!TryGetListings(buffalo, out var buffaloListings, out string buffaloFailure))
            {
                return Fail(buffaloFailure);
            }

            if (!ValidateNoSameStationArbitrage(fortBush.Name, fortListings, out string fortArbFailure))
            {
                return Fail(fortArbFailure);
            }

            if (!ValidateNoSameStationArbitrage(newark.Name, newarkListings, out string newarkArbFailure))
            {
                return Fail(newarkArbFailure);
            }

            if (!ValidateNoSameStationArbitrage(rochester.Name, rochesterListings, out string rochesterArbFailure))
            {
                return Fail(rochesterArbFailure);
            }

            if (!ValidateNoSameStationArbitrage(buffalo.Name, buffaloListings, out string buffaloArbFailure))
            {
                return Fail(buffaloArbFailure);
            }

            int legalMargin = GetBestRouteMargin(fortListings, newarkListings, requireContraband: false);
            if (legalMargin <= 0 || legalMargin > 200)
            {
                return Fail($"Fort Bush to Newark legal route margin was {legalMargin}, expected a modest positive spread");
            }

            int contrabandMargin = GetBestRouteMargin(buffaloListings, rochesterListings, requireContraband: true);
            if (contrabandMargin <= legalMargin)
            {
                return Fail($"Buffalo to Rochester contraband route margin {contrabandMargin} did not exceed legal route margin {legalMargin}");
            }

            if (contrabandMargin < 100)
            {
                return Fail($"Buffalo to Rochester contraband route margin was too small: {contrabandMargin}");
            }

            return Pass();
        }

        private Station ResolveStation(string stationName)
        {
            string target = NormalizeKey(stationName);
            return _stations.FirstOrDefault(station =>
                NormalizeKey(station?.Name) == target ||
                NormalizeKey(station?.Config?.Description) == target);
        }

        private bool ValidateListingsCanBeRead(IReadOnlyList<StationMarketListing> listings, out int availableCount, out string failureReason)
        {
            availableCount = 0;
            failureReason = string.Empty;

            if (listings == null || listings.Count == 0)
            {
                failureReason = "no market listings were returned";
                return false;
            }

            foreach (var listing in listings)
            {
                if (listing == null || listing.Commodity == null)
                {
                    failureReason = "encountered a null market listing";
                    return false;
                }

                int buyPrice = listing.BuyPrice;
                int sellPrice = listing.SellPrice;
                int stock = listing.Stock;
                bool available = listing.IsAvailable;
                bool contraband = listing.Commodity.IsContraband;
                int ownedQuantity = _tempShip.CargoHold.GetCommodityQuantity(listing.Commodity.Name);

                _ = buyPrice;
                _ = sellPrice;
                _ = stock;
                _ = available;
                _ = contraband;
                _ = ownedQuantity;

                if (available && buyPrice > 0 && sellPrice > 0 && stock > 0)
                {
                    availableCount++;
                }
            }

            if (availableCount <= 0)
            {
                failureReason = "no safely tradable commodity was available";
                return false;
            }

            return true;
        }

        private bool TryGetListings(Station station, out IReadOnlyList<StationMarketListing> listings, out string failureReason)
        {
            listings = Array.Empty<StationMarketListing>();
            failureReason = string.Empty;

            if (station == null)
            {
                failureReason = "station was null";
                return false;
            }

            int creditsBefore = _tempCredits.Credits;
            Dictionary<string, int> cargoSnapshot = _tempShip.CargoHold.GetAllCommodities();

            try
            {
                if (!_stationDockUi.DockAtStation(station))
                {
                    failureReason = string.IsNullOrWhiteSpace(_stationDockUi.LastDockingDeniedReason)
                        ? $"failed to dock at {station.Name}"
                        : _stationDockUi.LastDockingDeniedReason;
                    return false;
                }

                _stationDockUi.NavigateToArea(StationArea.Dealer);
                listings = _commodityDealer.CurrentMarketListings;
                if (listings == null || listings.Count == 0)
                {
                    failureReason = $"no market listings were returned for {station.Name}";
                    return false;
                }

                return true;
            }
            finally
            {
                RestoreTemporaryState(creditsBefore, cargoSnapshot);

                if (_stationDockUi.IsDocked)
                {
                    _stationDockUi.Undock();
                }
            }
        }

        private bool ValidateNoSameStationArbitrage(string stationName, IReadOnlyList<StationMarketListing> listings, out string failureReason)
        {
            failureReason = string.Empty;

            foreach (var listing in listings ?? Array.Empty<StationMarketListing>())
            {
                if (listing == null || listing.Commodity == null)
                {
                    continue;
                }

                if (!listing.IsAvailable || listing.BuyPrice <= 0 || listing.SellPrice <= 0)
                {
                    continue;
                }

                if (listing.BuyPrice < listing.SellPrice)
                {
                    failureReason = $"{stationName} allows same-station arbitrage on {listing.Commodity.Name}";
                    return false;
                }
            }

            return true;
        }

        private int GetBestRouteMargin(IReadOnlyList<StationMarketListing> originListings, IReadOnlyList<StationMarketListing> destinationListings, bool requireContraband)
        {
            int bestMargin = int.MinValue;

            foreach (var origin in originListings ?? Array.Empty<StationMarketListing>())
            {
                if (origin == null || origin.Commodity == null || !origin.IsAvailable || origin.BuyPrice <= 0)
                {
                    continue;
                }

                if (requireContraband && !origin.Commodity.IsContraband)
                {
                    continue;
                }

                var destination = destinationListings?.FirstOrDefault(listing =>
                    listing != null &&
                    listing.Commodity != null &&
                    string.Equals(listing.Commodity.Id, origin.Commodity.Id, StringComparison.OrdinalIgnoreCase));

                if (destination == null || !destination.IsAvailable || destination.SellPrice <= 0)
                {
                    continue;
                }

                int margin = destination.SellPrice - origin.BuyPrice;
                if (margin > bestMargin)
                {
                    bestMargin = margin;
                }
            }

            return bestMargin == int.MinValue ? 0 : bestMargin;
        }

        private bool ExerciseSelectionSafety(int selectionSeed, int listingCount, out string failureReason)
        {
            failureReason = string.Empty;

            if (listingCount <= 0)
            {
                failureReason = "market listing count was zero";
                return false;
            }

            _stationDockUi.NavigateToArea(StationArea.Dealer);
            SetSelectedCommodityIndex(selectionSeed);
            _stationDockUi.HandleCommodityDealerInput(new KeyboardState(), new KeyboardState(), _tempCredits, _tempShip);

            int clampedIndex = GetSelectedCommodityIndex();
            int expectedIndex = Math.Clamp(selectionSeed, 0, listingCount - 1);
            if (clampedIndex != expectedIndex)
            {
                failureReason = $"selected commodity index was {clampedIndex}, expected {expectedIndex}";
                return false;
            }

            return true;
        }

        private StationMarketListing SelectSafeTradeListing(IReadOnlyList<StationMarketListing> listings)
        {
            return listings
                .FirstOrDefault(listing =>
                    listing != null &&
                    listing.IsAvailable &&
                    listing.BuyPrice > 0 &&
                    listing.SellPrice > 0 &&
                    listing.Stock > 0 &&
                    listing.Commodity != null &&
                    !listing.Commodity.IsContraband)
                ?? listings.FirstOrDefault(listing =>
                    listing != null &&
                    listing.IsAvailable &&
                    listing.BuyPrice > 0 &&
                    listing.SellPrice > 0 &&
                    listing.Stock > 0 &&
                    listing.Commodity != null);
        }

        private bool RunTradeCycle(Station station, StationMarketListing listing, out string failureReason)
        {
            failureReason = string.Empty;

            var commodity = listing?.Commodity;
            if (station == null || commodity == null)
            {
                failureReason = "trade setup failed because the station or commodity was null";
                return false;
            }

            int startingCredits = _tempCredits.Credits;
            int startingCargo = _tempShip.CargoHold.GetCommodityQuantity(commodity.Name);
            int startingStock = listing.Stock;

            string buyMessage = string.Empty;
            bool buySuccess = RunSilenced(() => _commodityDealer.TryBuyCommodity(commodity, 1, _tempCredits, _tempShip.CargoHold, out buyMessage));
            if (!buySuccess)
            {
                failureReason = $"buy failed at {station.Name} for {commodity.Name}: {buyMessage}";
                return false;
            }

            if (_tempCredits.Credits != startingCredits - listing.BuyPrice)
            {
                failureReason = $"credits did not decrease correctly after buying {commodity.Name}";
                return false;
            }

            if (_tempShip.CargoHold.GetCommodityQuantity(commodity.Name) != startingCargo + 1)
            {
                failureReason = $"cargo quantity did not increase correctly after buying {commodity.Name}";
                return false;
            }

            var updatedListing = _commodityDealer.CurrentMarketListings.FirstOrDefault(entry =>
                entry != null &&
                entry.Commodity != null &&
                string.Equals(entry.Commodity.Id, commodity.Id, StringComparison.OrdinalIgnoreCase));

            if (updatedListing == null || updatedListing.Stock != startingStock - 1)
            {
                failureReason = $"stock did not decrease correctly after buying {commodity.Name}";
                return false;
            }

            string sellMessage = string.Empty;
            bool sellSuccess = RunSilenced(() => _commodityDealer.TrySellCommodity(commodity, 1, _tempCredits, _tempShip.CargoHold, out sellMessage));
            if (!sellSuccess)
            {
                failureReason = $"sell failed at {station.Name} for {commodity.Name}: {sellMessage}";
                return false;
            }

            if (_tempCredits.Credits != startingCredits - listing.BuyPrice + listing.SellPrice)
            {
                failureReason = $"credits did not increase correctly after selling {commodity.Name}";
                return false;
            }

            if (_tempShip.CargoHold.GetCommodityQuantity(commodity.Name) != startingCargo)
            {
                failureReason = $"cargo quantity did not decrease correctly after selling {commodity.Name}";
                return false;
            }

            return true;
        }

        private void RestoreTemporaryState(int creditsBefore, Dictionary<string, int> cargoSnapshot)
        {
            if (_tempCredits.Credits != creditsBefore)
            {
                int difference = Math.Abs(_tempCredits.Credits - creditsBefore);
                if (_tempCredits.Credits > creditsBefore)
                {
                    RunSilenced(() => _tempCredits.RemoveCredits(difference));
                }
                else
                {
                    RunSilenced(() => _tempCredits.AddCredits(difference));
                }
            }

            RunSilenced(() =>
            {
                _tempShip.CargoHold.Clear();
                foreach (var kvp in cargoSnapshot)
                {
                    var commodity = CommodityCatalog.GetByName(kvp.Key);
                    if (commodity != null && kvp.Value > 0)
                    {
                        _tempShip.CargoHold.AddCommodity(commodity, kvp.Value);
                    }
                }
            });
        }

        private void SetSelectedCommodityIndex(int index)
        {
            _selectedCommodityIndexField.SetValue(_stationDockUi, index);
        }

        private int GetSelectedCommodityIndex()
        {
            object value = _selectedCommodityIndexField.GetValue(_stationDockUi);
            return value is int selectedIndex ? selectedIndex : 0;
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

        private static (bool Success, string FailureReason) Pass()
        {
            return (true, string.Empty);
        }

        private static (bool Success, string FailureReason) Fail(string reason)
        {
            return (false, reason);
        }

        private static T RunSilenced<T>(Func<T> action)
        {
            TextWriter originalOut = Console.Out;
            using var sink = new StringWriter();
            Console.SetOut(sink);
            try
            {
                return action();
            }
            finally
            {
                Console.SetOut(originalOut);
            }
        }

        private static void RunSilenced(Action action)
        {
            TextWriter originalOut = Console.Out;
            using var sink = new StringWriter();
            Console.SetOut(sink);
            try
            {
                action();
            }
            finally
            {
                Console.SetOut(originalOut);
            }
        }
    }
}
