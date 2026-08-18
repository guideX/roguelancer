using Microsoft.Xna.Framework;
using Roguelancer.Configuration;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace Roguelancer;

/// <summary>
/// Focused headless proof for the first real commodity loop. Each case creates
/// fresh dealer/runtime state so stock changes cannot leak between assertions.
/// </summary>
internal sealed class CommodityMarketSmokeTest
{
    private readonly IReadOnlyList<Station> _stations;

    public CommodityMarketSmokeTest(IReadOnlyList<Station> stations = null)
    {
        string[] requiredStations = { "Fort Bush", "Newark Station", "Rochester Base", "Detroit Munitions", "Buffalo Base" };
        bool suppliedStationsAreComplete = stations != null && requiredStations.All(required =>
            stations.Any(station => NormalizeKey(station?.Name) == NormalizeKey(required)));
        _stations = suppliedStationsAreComplete ? stations : LoadFixtureStations();
    }

    public (int Passed, int Failed) Run()
    {
        int passed = 0;
        int failed = 0;
        RunCase("terminal placement", ValidateTerminalPlacement, ref passed, ref failed);
        RunCase("catalog and station listings", ValidateCatalog, ref passed, ref failed);
        RunCase("buy transaction", ValidateBuy, ref passed, ref failed);
        RunCase("buy rejection atomicity", ValidateBuyRejections, ref passed, ref failed);
        RunCase("sell transaction and repeat guard", ValidateSell, ref passed, ref failed);
        RunCase("protected and mixed cargo", ValidateProtectedCargo, ref passed, ref failed);
        RunCase("two-station trade route", ValidateTradeRoute, ref passed, ref failed);
        RunCase("save/load cargo and market state", ValidateSaveLoad, ref passed, ref failed);
        RunCase("ship dealer normal cargo preservation", ValidateShipDealerCargo, ref passed, ref failed);
        Console.WriteLine($"[COMMODITY MARKET SMOKE] RESULT: {passed} passed, {failed} failed");
        return (passed, failed);
    }

    private void RunCase(string label, Func<(bool Success, string FailureReason)> test, ref int passed, ref int failed)
    {
        try
        {
            var result = RunSilenced(test);
            if (result.Success)
            {
                passed++;
                Console.WriteLine($"[COMMODITY MARKET SMOKE] PASS {label}");
            }
            else
            {
                failed++;
                Console.WriteLine($"[COMMODITY MARKET SMOKE] FAIL {label}: {result.FailureReason}");
            }
        }
        catch (Exception ex)
        {
            failed++;
            Console.WriteLine($"[COMMODITY MARKET SMOKE] FAIL {label}: {ex.Message}");
        }
    }

    private (bool Success, string FailureReason) ValidateTerminalPlacement()
    {
        StationTestScene scene = new();
        Vector3 position = scene.CommodityTraderInteractionPosition;
        if (scene.CommodityTraderSignText != "COMMODITY TRADER")
            return Fail("commodity trader terminal sign was not configured");
        if (position.X < -17f || position.X > 17f || position.Z < -17f || position.Z > 62.5f)
            return Fail("commodity trader terminal was placed outside the station concourse");

        StationInteraction target = new(
            "commodity-trader",
            position,
            2.0f,
            scene.CommodityTraderSignText,
            "Press E to trade",
            () => { });
        StationInteraction resolved = StationInteractionResolver.FindNearest(
            new[] { target }, position + Vector3.Backward * 0.5f, Vector3.Forward);
        return resolved?.Id == "commodity-trader"
            ? Pass()
            : Fail("commodity trader interaction was not resolvable from its kiosk");
    }

    private (bool Success, string FailureReason) ValidateCatalog()
    {
        MarketManager manager = new();
        foreach (string stationName in new[] { "Fort Bush", "Newark Station", "Rochester Base", "Detroit Munitions", "Buffalo Base" })
        {
            Station station = ResolveStation(stationName);
            if (station == null) return Fail($"configured station '{stationName}' was not found");

            List<StationMarketListing> listings = manager.GetListingsForStation(station);
            if (listings.Count == 0) return Fail($"station '{stationName}' had no listings");
            HashSet<string> ids = new(StringComparer.OrdinalIgnoreCase);
            foreach (StationMarketListing listing in listings)
            {
                if (listing?.Commodity == null || !ids.Add(listing.Commodity.Id))
                    return Fail($"station '{stationName}' had a duplicate or null listing");
                if (CommodityCatalog.GetById(listing.Commodity.Id) == null)
                    return Fail($"unknown commodity '{listing.Commodity.Id}' was listed");
                if (listing.BuyPrice < 0 || listing.SellPrice < 0 || listing.Stock < 0)
                    return Fail($"invalid negative market data at '{stationName}'");
                if (listing.Commodity.IsMissionCargo)
                    return Fail("mission cargo appeared in a station market");
                if (listing.IsAvailable && (listing.BuyPrice <= 0 || listing.SellPrice <= 0 || listing.BuyPrice < listing.SellPrice))
                    return Fail($"available listing '{listing.Commodity.Id}' was invalid at '{stationName}'");
            }
        }

        Commodity food = CommodityCatalog.GetById("food-rations");
        Station fortBush = ResolveStation("Fort Bush");
        Station newark = ResolveStation("Newark Station");
        StationMarketListing fortFood = manager.GetListingForCommodity(fortBush, food);
        StationMarketListing newarkFood = manager.GetListingForCommodity(newark, food);
        if (fortFood == null || newarkFood == null || fortFood.BuyPrice >= newarkFood.SellPrice)
            return Fail("Fort Bush/Newark food route did not have a profitable configured spread");

        return Pass();
    }

    private (bool Success, string FailureReason) ValidateBuy()
    {
        Station station = ResolveStation("Fort Bush");
        Commodity food = CommodityCatalog.GetById("food-rations");
        CommodityDealer dealer = DockedDealer(station);
        Ship ship = new(Vector3.Zero);
        PlayerCredits credits = new(10_000);
        StationMarketListing listing = FindListing(dealer, food);
        int startingStock = listing.Stock;

        if (!dealer.TryBuyCommodity(food, 3, credits, ship.CargoHold, out string message))
            return Fail($"affordable buy failed: {message}");
        if (credits.Credits != 10_000 - listing.BuyPrice * 3)
            return Fail("buy did not deduct credits exactly once");
        if (ship.CargoHold.GetCommodityQuantity(food.Name) != 3 || ship.CargoHold.UsedCapacity != food.VolumePerUnit * 3)
            return Fail("buy did not add authoritative cargo exactly once");
        if (FindListing(dealer, food).Stock != startingStock - 3)
            return Fail("buy did not update station stock exactly once");
        return Pass();
    }

    private (bool Success, string FailureReason) ValidateBuyRejections()
    {
        Station station = ResolveStation("Fort Bush");
        Commodity food = CommodityCatalog.GetById("food-rations");

        CommodityDealer fundsDealer = DockedDealer(station);
        CargoHold fundsCargo = new(50);
        PlayerCredits poorCredits = new(1);
        if (fundsDealer.TryBuyCommodity(food, 1, poorCredits, fundsCargo, out _))
            return Fail("insufficient-funds buy unexpectedly succeeded");
        if (poorCredits.Credits != 1 || fundsCargo.UsedCapacity != 0)
            return Fail("insufficient-funds rejection mutated state");

        CommodityDealer spaceDealer = DockedDealer(station);
        CargoHold fullCargo = new(0);
        PlayerCredits fullCredits = new(10_000);
        if (spaceDealer.TryBuyCommodity(food, 1, fullCredits, fullCargo, out string spaceMessage))
            return Fail("insufficient-space buy unexpectedly succeeded");
        if (fullCredits.Credits != 10_000 || fullCargo.UsedCapacity != 0 || !spaceMessage.Contains("cargo", StringComparison.OrdinalIgnoreCase))
            return Fail("insufficient-space rejection was not atomic or readable");

        CommodityDealer invalidDealer = DockedDealer(station);
        if (invalidDealer.TryBuyCommodity(food, 0, new PlayerCredits(10_000), new CargoHold(50), out _))
            return Fail("zero-quantity buy unexpectedly succeeded");
        if (invalidDealer.TryBuyCommodity(food, -1, new PlayerCredits(10_000), new CargoHold(50), out _))
            return Fail("negative-quantity buy unexpectedly succeeded");
        return Pass();
    }

    private (bool Success, string FailureReason) ValidateSell()
    {
        Station station = ResolveStation("Newark Station");
        Commodity food = CommodityCatalog.GetById("food-rations");
        CommodityDealer dealer = DockedDealer(station);
        CargoHold cargo = new(50);
        PlayerCredits credits = new(1_000);
        cargo.AddCommodity(food, 3);
        StationMarketListing listing = FindListing(dealer, food);

        if (!dealer.TrySellCommodity(food, 2, credits, cargo, out string message))
            return Fail($"ordinary cargo sale failed: {message}");
        if (credits.Credits != 1_000 + listing.SellPrice * 2 || cargo.GetCommodityQuantity(food.Name) != 1)
            return Fail("ordinary sale did not mutate credits/cargo exactly once");

        int creditsAfter = credits.Credits;
        int cargoAfter = cargo.GetCommodityQuantity(food.Name);
        if (dealer.TrySellCommodity(food, 2, credits, cargo, out _))
            return Fail("repeated sale callback unexpectedly paid twice");
        if (credits.Credits != creditsAfter || cargo.GetCommodityQuantity(food.Name) != cargoAfter)
            return Fail("repeated sale rejection mutated state");
        if (dealer.TrySellCommodity(food, 0, credits, cargo, out _) || dealer.TrySellCommodity(food, -1, credits, cargo, out _))
            return Fail("zero/negative sale unexpectedly succeeded");
        return Pass();
    }

    private (bool Success, string FailureReason) ValidateProtectedCargo()
    {
        Station station = ResolveStation("Newark Station");
        Commodity food = CommodityCatalog.GetById("food-rations");
        Commodity package = CommodityCatalog.GetById("sealed-data-package");
        CommodityDealer dealer = DockedDealer(station);
        CargoHold cargo = new(50);
        PlayerCredits credits = new(500);
        int missionId = 9001;
        if (!cargo.AddMissionCargo(missionId, food, 1) || !cargo.AddCommodity(food, 2))
            return Fail("could not create mixed ordinary/protected food stack");
        if (!cargo.AddMissionCargo(missionId + 1, package, 1))
            return Fail("could not create protected courier package");

        int creditsBeforePackageSale = credits.Credits;
        int quantityBeforePackageSale = cargo.GetCommodityQuantity(package.Name);
        if (dealer.TrySellCommodity(package, 1, credits, cargo, out string packageMessage))
            return Fail("courier package sale unexpectedly succeeded");
        if (!packageMessage.Contains("mission cargo", StringComparison.OrdinalIgnoreCase) ||
            credits.Credits != creditsBeforePackageSale ||
            cargo.GetCommodityQuantity(package.Name) != quantityBeforePackageSale ||
            !cargo.HasMissionCargo(missionId + 1, package.Id, 1))
            return Fail("rejected package sale changed credits, cargo, or reservation");

        int foodSellPrice = FindListing(dealer, food).SellPrice;
        if (!dealer.TrySellCommodity(food, 2, credits, cargo, out _))
            return Fail("ordinary food was not sellable beside protected food");
        if (cargo.GetCommodityQuantity(food.Name) != 1 || cargo.GetSellableCommodityQuantity(food.Name) != 0 ||
            !cargo.HasMissionCargo(missionId, food.Id, 1))
            return Fail("ordinary sale consumed protected mixed-stack units");

        int creditsAfterOrdinarySale = credits.Credits;
        if (dealer.TrySellCommodity(food, 1, credits, cargo, out string protectedMessage))
            return Fail("protected remainder sale unexpectedly succeeded");
        if (!protectedMessage.Contains("mission cargo", StringComparison.OrdinalIgnoreCase) || credits.Credits != creditsAfterOrdinarySale)
            return Fail("protected remainder rejection was not authoritative");
        if (creditsAfterOrdinarySale != creditsBeforePackageSale + foodSellPrice * 2)
            return Fail("ordinary mixed-stack sale paid the wrong amount");

        return Pass();
    }

    private (bool Success, string FailureReason) ValidateTradeRoute()
    {
        Station origin = ResolveStation("Fort Bush");
        Station destination = ResolveStation("Newark Station");
        Commodity food = CommodityCatalog.GetById("food-rations");
        CommodityDealer dealer = DockedDealer(origin);
        Ship ship = new(Vector3.Zero);
        PlayerCredits credits = new(10_000);
        StationMarketListing originListing = FindListing(dealer, food);
        int buyPrice = originListing.BuyPrice;

        if (!dealer.TryBuyCommodity(food, 5, credits, ship.CargoHold, out string buyMessage))
            return Fail($"route buy failed: {buyMessage}");
        dealer.SetDockedStation(destination);
        StationMarketListing destinationListing = FindListing(dealer, food);
        if (destinationListing.SellPrice <= buyPrice)
            return Fail("destination did not pay more than origin buy price");
        if (!dealer.TrySellCommodity(food, 5, credits, ship.CargoHold, out string sellMessage))
            return Fail($"route sale failed: {sellMessage}");
        if (credits.Credits <= 10_000 || credits.Credits != 10_000 - buyPrice * 5 + destinationListing.SellPrice * 5)
            return Fail("configured trade route did not produce the expected profit");
        if (ship.CargoHold.GetCommodityQuantity(food.Name) != 0)
            return Fail("trade route did not return cargo to its original quantity");

        CommodityDealer sameStationDealer = DockedDealer(origin);
        Ship sameStationShip = new(Vector3.Zero);
        PlayerCredits sameStationCredits = new(10_000);
        StationMarketListing sameListing = FindListing(sameStationDealer, food);
        if (!sameStationDealer.TryBuyCommodity(food, 1, sameStationCredits, sameStationShip.CargoHold, out _) ||
            !sameStationDealer.TrySellCommodity(food, 1, sameStationCredits, sameStationShip.CargoHold, out _))
            return Fail("same-station round trip could not be exercised");
        if (sameStationCredits.Credits >= 10_000 || sameListing.BuyPrice < sameListing.SellPrice)
            return Fail("same-station round trip created unintended profit");
        return Pass();
    }

    private (bool Success, string FailureReason) ValidateSaveLoad()
    {
        string directory = Path.Combine(Path.GetTempPath(), "Roguelancer_CommodityMarket_" + Guid.NewGuid().ToString("N"));
        string savePath = Path.Combine(directory, "market-save.json");
        try
        {
            Directory.CreateDirectory(directory);
            Station origin = ResolveStation("Fort Bush");
            Commodity food = CommodityCatalog.GetById("food-rations");
            Commodity package = CommodityCatalog.GetById("sealed-data-package");
            CommodityDealer dealer = DockedDealer(origin);
            Ship ship = new(Vector3.Zero);
            PlayerCredits credits = new(12_000);
            if (!dealer.TryBuyCommodity(food, 2, credits, ship.CargoHold, out string buyMessage))
                return Fail($"could not buy before save: {buyMessage}");
            if (!ship.CargoHold.AddMissionCargo(9100, package, 1))
                return Fail("could not stage courier package before save");

            SaveGameManager saveManager = new(savePath);
            Mission mission = Mission.FromDefinition(MissionCatalog.GetById(MissionCatalog.PriorityDispatchId));
            SaveGameData data = new()
            {
                PlayerCredits = credits.Credits,
                Cargo = saveManager.CaptureCargo(ship.CargoHold),
                StationMarkets = dealer.CaptureMarketState(),
                ActiveMissions = new List<SaveMissionData>
                {
                    new SaveMissionData
                    {
                        MissionId = mission.Id,
                        Type = MissionType.CourierDelivery,
                        Status = MissionStatus.InProgress,
                        PackageId = package.Id,
                        PackageQuantity = 1,
                        MissionCargoLoaded = true
                    }
                }
            };
            string saveFailure = string.Empty;
            string loadFailure = string.Empty;
            if (!saveManager.TrySave(data, out saveFailure) || !saveManager.TryLoad(out SaveGameData loaded, out loadFailure))
                return Fail($"commodity save/load failed: {saveFailure} {loadFailure}");
            if (loaded.PlayerCredits != credits.Credits || loaded.ActiveMissions.Count != 1)
                return Fail("credits or mission state did not survive save/load");

            CargoHold restoredCargo = new(50);
            saveManager.ApplyCargo(restoredCargo, loaded, out List<string> warnings);
            if (warnings.Count != 0 || restoredCargo.GetCommodityQuantity(food.Name) != 2 ||
                !restoredCargo.HasMissionCargo(9100, package.Id, 1))
                return Fail($"ordinary and mission cargo did not survive reload: {string.Join("; ", warnings)}");

            saveManager.ApplyCargo(restoredCargo, loaded, out warnings);
            if (warnings.Count != 0 || restoredCargo.GetCommodityQuantity(food.Name) != 2 ||
                !restoredCargo.HasMissionCargo(9100, package.Id, 1))
                return Fail("repeated load duplicated or lost mixed cargo");

            CommodityDealer resumedDealer = new();
            resumedDealer.SetDockedStation(origin);
            resumedDealer.RestoreMarketState(loaded.StationMarkets);
            StationMarketListing resumedOriginFood = FindListing(resumedDealer, food);
            if (resumedOriginFood.BuyPrice != FindListing(dealer, food).BuyPrice || resumedOriginFood.Stock != FindListing(dealer, food).Stock)
                return Fail("configured origin prices or runtime stock did not survive reload");

            resumedDealer.SetDockedStation(ResolveStation("Newark Station"));
            StationMarketListing resumedDestinationFood = FindListing(resumedDealer, food);
            if (resumedDestinationFood.BuyPrice == resumedOriginFood.BuyPrice || resumedDestinationFood.SellPrice <= resumedOriginFood.BuyPrice)
                return Fail("station-specific prices did not switch after reload");
            return Pass();
        }
        finally
        {
            try
            {
                if (Directory.Exists(directory)) Directory.Delete(directory, true);
            }
            catch
            {
                // Temporary smoke cleanup must not hide the assertion result.
            }
        }
    }

    private (bool Success, string FailureReason) ValidateShipDealerCargo()
    {
        ShipDealer dealer = new();
        Ship playerShip = new(Vector3.Zero);
        Commodity food = CommodityCatalog.GetById("food-rations");
        if (!playerShip.CargoHold.AddCommodity(food, 5))
            return Fail("could not stage ordinary cargo for ship dealer regression");
        ShipDefinition upgrade = dealer.GetShipByName("Pirate Transport");
        PlayerCredits credits = new(dealer.GetTotalCost(upgrade));
        if (!dealer.TryPurchaseShip(upgrade, credits, playerShip, out string message))
            return Fail($"ship upgrade rejected ordinary cargo: {message}");
        if (playerShip.CargoHold.GetCommodityQuantity(food.Name) != 5 || playerShip.CargoHold.UsedCapacity != food.VolumePerUnit * 5)
            return Fail("ship upgrade lost or duplicated ordinary commodity cargo");
        return Pass();
    }

    private StationMarketListing FindListing(CommodityDealer dealer, Commodity commodity)
    {
        return dealer.CurrentMarketListings.FirstOrDefault(listing =>
            string.Equals(listing?.Commodity?.Id, commodity?.Id, StringComparison.OrdinalIgnoreCase));
    }

    private CommodityDealer DockedDealer(Station station)
    {
        CommodityDealer dealer = new();
        dealer.SetDockedStation(station);
        return dealer;
    }

    private Station ResolveStation(string name)
    {
        string target = NormalizeKey(name);
        return _stations.FirstOrDefault(station => NormalizeKey(station?.Name) == target);
    }

    private static IReadOnlyList<Station> LoadFixtureStations()
    {
        string directory = Path.Combine("Configuration", "stations");
        if (!Directory.Exists(directory)) return Array.Empty<Station>();
        JsonSerializerOptions options = new() { PropertyNameCaseInsensitive = true };
        List<Station> result = new();
        foreach (string file in Directory.GetFiles(directory, "station_*.json"))
        {
            try
            {
                StationConfig config = JsonSerializer.Deserialize<StationConfig>(File.ReadAllText(file), options);
                if (config != null) result.Add(new Station(config, null));
            }
            catch
            {
                // The production station loader reports malformed fixtures; this
                // harness only needs valid station identities.
            }
        }
        return result;
    }

    private static string NormalizeKey(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        return new string(value.Trim().ToLowerInvariant().Where(char.IsLetterOrDigit).ToArray());
    }

    private static (bool Success, string FailureReason) Pass() => (true, string.Empty);
    private static (bool Success, string FailureReason) Fail(string reason) => (false, reason);

    private static T RunSilenced<T>(Func<T> action)
    {
        TextWriter original = Console.Out;
        try
        {
            Console.SetOut(TextWriter.Null);
            return action();
        }
        finally
        {
            Console.SetOut(original);
        }
    }
}
