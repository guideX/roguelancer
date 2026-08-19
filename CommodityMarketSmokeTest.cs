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
        RunCase("baseline prices and stock", ValidateBaselineConfiguration, ref passed, ref failed);
        RunCase("buy transaction", ValidateBuy, ref passed, ref failed);
        RunCase("buy scarcity response", ValidateBuyScarcityResponse, ref passed, ref failed);
        RunCase("sell oversupply response", ValidateSellOversupplyResponse, ref passed, ref failed);
        RunCase("bounded price floor and ceiling", ValidatePriceBounds, ref passed, ref failed);
        RunCase("stock bounds and buy limits", ValidateStockAndBuyLimits, ref passed, ref failed);
        RunCase("buy rejection atomicity", ValidateBuyRejections, ref passed, ref failed);
        RunCase("sell transaction and repeat guard", ValidateSell, ref passed, ref failed);
        RunCase("sell quantity and overflow guard", ValidateSellLimits, ref passed, ref failed);
        RunCase("protected and mixed cargo", ValidateProtectedCargo, ref passed, ref failed);
        RunCase("two-station trade route", ValidateTradeRoute, ref passed, ref failed);
        RunCase("repeated route diminishing returns", ValidateDiminishingRoute, ref passed, ref failed);
        RunCase("market recovery and no overshoot", ValidateRecovery, ref passed, ref failed);
        RunCase("elapsed-time determinism", ValidateElapsedTimeDeterminism, ref passed, ref failed);
        RunCase("station identity remains distinct", ValidateStationIdentity, ref passed, ref failed);
        RunCase("unrelated market isolation", ValidateUnrelatedMarketIsolation, ref passed, ref failed);
        RunCase("save/load cargo and market state", ValidateSaveLoad, ref passed, ref failed);
        RunCase("derived prices after save/load", ValidateDerivedPricesAfterLoad, ref passed, ref failed);
        RunCase("old/default save initialization", ValidateOldSaveInitialization, ref passed, ref failed);
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

    private (bool Success, string FailureReason) ValidateBaselineConfiguration()
    {
        MarketManager manager = new();
        Commodity food = CommodityCatalog.GetById("food-rations");
        Station fortBush = ResolveStation("Fort Bush");
        Station newark = ResolveStation("Newark Station");
        StationMarketListing fort = manager.GetListingForCommodity(fortBush, food);
        StationMarketListing destination = manager.GetListingForCommodity(newark, food);

        if (fort == null || destination == null)
        {
            return Fail("baseline food listings were not available");
        }

        if (fort.BuyPrice != 85 || fort.SellPrice != 60 || fort.Stock != 450 ||
            fort.BaselineStock != 450 || fort.BaseBuyPrice != 85 || fort.BaseSellPrice != 60)
        {
            return Fail($"Fort Bush baseline changed: {fort.BuyPrice}/{fort.SellPrice} at {fort.Stock}");
        }

        if (destination.BuyPrice != 150 || destination.SellPrice != 115 || destination.Stock != 220 ||
            destination.BaselineStock != 220)
        {
            return Fail("Newark baseline configuration was not preserved");
        }

        return Pass();
    }

    private (bool Success, string FailureReason) ValidateBuyScarcityResponse()
    {
        Station station = ResolveStation("Fort Bush");
        Commodity food = CommodityCatalog.GetById("food-rations");
        CommodityDealer dealer = DockedDealer(station);
        CargoHold cargo = new(1_000);
        PlayerCredits credits = new(100_000);
        StationMarketListing baseline = FindListing(dealer, food);

        if (!dealer.TryBuyCommodity(food, 300, credits, cargo, out string message))
        {
            return Fail($"meaningful scarcity buy failed: {message}");
        }

        StationMarketListing scarce = FindListing(dealer, food);
        if (scarce.Stock != 150 || scarce.BuyPrice <= baseline.BuyPrice || scarce.MarketCondition == "NORMAL")
        {
            return Fail($"buy did not create bounded scarcity: stock={scarce.Stock}, buy={scarce.BuyPrice}, condition={scarce.MarketCondition}");
        }

        return scarce.SellPrice <= scarce.BuyPrice ? Pass() : Fail("scarcity inverted the local buy/sell spread");
    }

    private (bool Success, string FailureReason) ValidateSellOversupplyResponse()
    {
        Station station = ResolveStation("Newark Station");
        Commodity food = CommodityCatalog.GetById("food-rations");
        CommodityDealer dealer = DockedDealer(station);
        CargoHold cargo = new(200);
        PlayerCredits credits = new(1_000);
        StationMarketListing baseline = FindListing(dealer, food);
        string message = string.Empty;
        if (!cargo.AddCommodity(food, 100) || !dealer.TrySellCommodity(food, 100, credits, cargo, out message))
        {
            return Fail($"meaningful oversupply sale failed: {message}");
        }

        StationMarketListing glutted = FindListing(dealer, food);
        if (glutted.Stock != 320 || glutted.SellPrice >= baseline.SellPrice || glutted.BuyPrice >= baseline.BuyPrice)
        {
            return Fail($"sale did not lower oversupply prices: stock={glutted.Stock}, buy={glutted.BuyPrice}, sell={glutted.SellPrice}");
        }

        return Pass();
    }

    private (bool Success, string FailureReason) ValidatePriceBounds()
    {
        Station station = ResolveStation("Fort Bush");
        Commodity food = CommodityCatalog.GetById("food-rations");

        CommodityDealer shortageDealer = DockedDealer(station);
        CargoHold shortageCargo = new(1_000);
        PlayerCredits shortageCredits = new(1_000_000);
        StationMarketListing baseline = FindListing(shortageDealer, food);
        if (!shortageDealer.TryBuyCommodity(food, baseline.Stock, shortageCredits, shortageCargo, out _))
        {
            return Fail("could not drain a market to test the price ceiling");
        }

        StationMarketListing shortage = FindListing(shortageDealer, food);
        int buyCeiling = (int)Math.Ceiling(baseline.BaseBuyPrice * 1.35m);
        int sellCeiling = (int)Math.Ceiling(baseline.BaseSellPrice * 1.50m);
        if (shortage.Stock < 0 || shortage.BuyPrice > buyCeiling || shortage.SellPrice > sellCeiling || shortage.SellPrice >= shortage.BuyPrice)
        {
            return Fail($"scarcity bounds failed: stock={shortage.Stock}, buy={shortage.BuyPrice}, sell={shortage.SellPrice}");
        }

        CommodityDealer surplusDealer = DockedDealer(station);
        StationMarketListing surplusBaseline = FindListing(surplusDealer, food);
        int saleQuantity = surplusBaseline.MaximumStock - surplusBaseline.Stock;
        CargoHold surplusCargo = new(saleQuantity + 10);
        PlayerCredits surplusCredits = new(0);
        string saleMessage = string.Empty;
        if (!surplusCargo.AddCommodity(food, saleQuantity) ||
            !surplusDealer.TrySellCommodity(food, saleQuantity, surplusCredits, surplusCargo, out saleMessage))
        {
            return Fail($"could not fill a market to test the price floor: {saleMessage}");
        }

        StationMarketListing surplus = FindListing(surplusDealer, food);
        int buyFloor = (int)Math.Floor(surplus.BaseBuyPrice * 0.65m);
        int sellFloor = (int)Math.Floor(surplus.BaseSellPrice * 0.50m);
        if (surplus.Stock > surplus.MaximumStock || surplus.BuyPrice < buyFloor || surplus.SellPrice < Math.Max(1, sellFloor))
        {
            return Fail($"oversupply bounds failed: stock={surplus.Stock}, buy={surplus.BuyPrice}, sell={surplus.SellPrice}");
        }

        return Pass();
    }

    private (bool Success, string FailureReason) ValidateStockAndBuyLimits()
    {
        Station station = ResolveStation("Fort Bush");
        Commodity food = CommodityCatalog.GetById("food-rations");
        CommodityDealer dealer = DockedDealer(station);
        StationMarketListing before = FindListing(dealer, food);
        CargoHold cargo = new(1_000);
        PlayerCredits credits = new(1_000_000);
        if (dealer.TryBuyCommodity(food, before.Stock + 1, credits, cargo, out _))
        {
            return Fail("buy above station stock unexpectedly succeeded");
        }

        if (FindListing(dealer, food).Stock != before.Stock || credits.Credits != 1_000_000 || cargo.UsedCapacity != 0)
        {
            return Fail("out-of-stock rejection mutated state");
        }

        if (!dealer.TryBuyCommodity(food, before.Stock, credits, cargo, out string message))
        {
            return Fail($"buying exactly available stock failed: {message}");
        }

        StationMarketListing drained = FindListing(dealer, food);
        return drained.Stock == 0 && drained.BuyPrice > 0 ? Pass() : Fail("stock became negative or failed to reach zero");
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

    private (bool Success, string FailureReason) ValidateSellLimits()
    {
        Station station = ResolveStation("Newark Station");
        Commodity food = CommodityCatalog.GetById("food-rations");
        CommodityDealer dealer = DockedDealer(station);
        CargoHold cargo = new(10);
        PlayerCredits credits = new(100);
        if (!cargo.AddCommodity(food, 2))
        {
            return Fail("could not stage ordinary cargo for sell limit test");
        }

        StationMarketListing before = FindListing(dealer, food);
        int creditsBefore = credits.Credits;
        if (dealer.TrySellCommodity(food, 3, credits, cargo, out _))
        {
            return Fail("sale above ordinary owned quantity unexpectedly succeeded");
        }

        if (credits.Credits != creditsBefore || cargo.GetCommodityQuantity(food.Name) != 2 || FindListing(dealer, food).Stock != before.Stock)
        {
            return Fail("ordinary sell limit rejection was not atomic");
        }

        if (dealer.TrySellCommodity(food, int.MaxValue, credits, cargo, out _))
        {
            return Fail("overflow-sized sale unexpectedly succeeded");
        }

        return credits.Credits == creditsBefore && cargo.GetCommodityQuantity(food.Name) == 2
            ? Pass()
            : Fail("overflow-sized sale mutated state");
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

    private (bool Success, string FailureReason) ValidateDiminishingRoute()
    {
        Station origin = ResolveStation("Fort Bush");
        Station destination = ResolveStation("Newark Station");
        Commodity food = CommodityCatalog.GetById("food-rations");
        CommodityDealer dealer = new();
        CargoHold cargo = new(20);
        PlayerCredits credits = new(1_000_000);
        List<int> margins = new();

        for (int run = 0; run < 40; run++)
        {
            dealer.SetDockedStation(origin);
            StationMarketListing originListing = FindListing(dealer, food);
            int buyPrice = originListing.BuyPrice;
            if (!dealer.TryBuyCommodity(food, 10, credits, cargo, out string buyMessage))
            {
                return Fail($"route run {run + 1} buy failed: {buyMessage}");
            }

            dealer.SetDockedStation(destination);
            StationMarketListing destinationListing = FindListing(dealer, food);
            int sellPrice = destinationListing.SellPrice;
            if (!dealer.TrySellCommodity(food, 10, credits, cargo, out string sellMessage))
            {
                return Fail($"route run {run + 1} sale failed: {sellMessage}");
            }

            margins.Add(sellPrice - buyPrice);
        }

        if (margins.Count == 0 || margins[0] <= 0)
        {
            return Fail("representative Fort Bush/Newark route was not profitable at baseline");
        }

        if (!margins.Skip(1).Any(margin => margin < margins[0]))
        {
            return Fail("repeated route runs did not reduce the initial margin");
        }

        return margins.Any(margin => margin <= 0)
            ? Pass()
            : Fail($"route remained profitable after 40 unrecovered runs; final margin {margins[^1]}");
    }

    private (bool Success, string FailureReason) ValidateRecovery()
    {
        Station station = ResolveStation("Fort Bush");
        Commodity food = CommodityCatalog.GetById("food-rations");
        CommodityDealer dealer = DockedDealer(station);
        CargoHold cargo = new(500);
        PlayerCredits credits = new(100_000);
        if (!dealer.TryBuyCommodity(food, 300, credits, cargo, out string buyMessage))
        {
            return Fail($"could not create recovery shortage: {buyMessage}");
        }

        int shortageStock = FindListing(dealer, food).Stock;
        dealer.AdvanceTime(600);
        int recoveringStock = FindListing(dealer, food).Stock;
        if (recoveringStock <= shortageStock || recoveringStock >= 450)
        {
            return Fail($"recovery did not move gradually: {shortageStock} -> {recoveringStock}");
        }

        dealer.AdvanceTime(3_600);
        StationMarketListing normalized = FindListing(dealer, food);
        if (normalized.Stock != normalized.BaselineStock || normalized.Stock > normalized.MaximumStock)
        {
            return Fail($"recovery overshot or failed to normalize: {normalized.Stock}/{normalized.BaselineStock}");
        }

        return normalized.BuyPrice == normalized.BaseBuyPrice && normalized.SellPrice == normalized.BaseSellPrice
            ? Pass()
            : Fail("normalized market prices did not return to configured anchors");
    }

    private (bool Success, string FailureReason) ValidateElapsedTimeDeterminism()
    {
        Station station = ResolveStation("Fort Bush");
        Commodity food = CommodityCatalog.GetById("food-rations");

        CommodityDealer oneStep = DockedDealer(station);
        CommodityDealer twoSteps = DockedDealer(station);
        CargoHold oneCargo = new(500);
        CargoHold twoCargo = new(500);
        PlayerCredits oneCredits = new(100_000);
        PlayerCredits twoCredits = new(100_000);
        if (!oneStep.TryBuyCommodity(food, 300, oneCredits, oneCargo, out _) ||
            !twoSteps.TryBuyCommodity(food, 300, twoCredits, twoCargo, out _))
        {
            return Fail("could not create deterministic recovery fixtures");
        }

        oneStep.AdvanceTime(1_800);
        twoSteps.AdvanceTime(900);
        twoSteps.AdvanceTime(900);
        StationMarketListing one = FindListing(oneStep, food);
        StationMarketListing two = FindListing(twoSteps, food);
        return one.Stock == two.Stock && one.BuyPrice == two.BuyPrice && one.SellPrice == two.SellPrice
            ? Pass()
            : Fail($"same elapsed time diverged: {one.Stock}/{one.BuyPrice}/{one.SellPrice} vs {two.Stock}/{two.BuyPrice}/{two.SellPrice}");
    }

    private (bool Success, string FailureReason) ValidateStationIdentity()
    {
        Commodity food = CommodityCatalog.GetById("food-rations");
        MarketManager manager = new();
        StationMarketListing fort = manager.GetListingForCommodity(ResolveStation("Fort Bush"), food);
        StationMarketListing newark = manager.GetListingForCommodity(ResolveStation("Newark Station"), food);
        StationMarketListing rochester = manager.GetListingForCommodity(ResolveStation("Rochester Base"), food);

        if (fort == null || newark == null || rochester == null ||
            fort.BuyPrice != 85 || newark.BuyPrice != 150 || rochester.BuyPrice != 105 ||
            fort.BaselineStock == newark.BaselineStock)
        {
            return Fail("station-specific food anchors were not distinct");
        }

        return Pass();
    }

    private (bool Success, string FailureReason) ValidateUnrelatedMarketIsolation()
    {
        Commodity food = CommodityCatalog.GetById("food-rations");
        Commodity water = CommodityCatalog.GetById("water");
        CommodityDealer dealer = new();
        Station fort = ResolveStation("Fort Bush");
        Station newark = ResolveStation("Newark Station");
        dealer.SetDockedStation(newark);
        StationMarketListing before = FindListing(dealer, water);
        dealer.SetDockedStation(fort);
        CargoHold cargo = new(100);
        if (!dealer.TryBuyCommodity(food, 50, new PlayerCredits(100_000), cargo, out _))
        {
            return Fail("isolated Fort Bush transaction failed");
        }

        dealer.SetDockedStation(newark);
        StationMarketListing after = FindListing(dealer, water);
        return before.Stock == after.Stock && before.BuyPrice == after.BuyPrice && before.SellPrice == after.SellPrice
            ? Pass()
            : Fail("Fort Bush transaction changed an unrelated Newark water market");
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

    private (bool Success, string FailureReason) ValidateDerivedPricesAfterLoad()
    {
        Station station = ResolveStation("Fort Bush");
        Commodity food = CommodityCatalog.GetById("food-rations");
        CommodityDealer dealer = DockedDealer(station);
        CargoHold cargo = new(500);
        PlayerCredits credits = new(100_000);
        if (!dealer.TryBuyCommodity(food, 250, credits, cargo, out _))
        {
            return Fail("could not perturb market before derived-price save test");
        }

        StationMarketListing expected = FindListing(dealer, food);
        List<SaveMarketStateData> state = dealer.CaptureMarketState();
        SaveMarketListingData savedListing = state.SelectMany(snapshot => snapshot.Listings ?? new List<SaveMarketListingData>())
            .FirstOrDefault(listing => string.Equals(listing.CommodityId, food.Id, StringComparison.OrdinalIgnoreCase));
        if (savedListing == null || savedListing.BuyPrice != 0 || savedListing.SellPrice != 0)
        {
            return Fail("derived prices were serialized as authoritative state");
        }

        string json = JsonSerializer.Serialize(state);
        if (json.Contains("\"buy_price\"", StringComparison.OrdinalIgnoreCase) || json.Contains("\"sell_price\"", StringComparison.OrdinalIgnoreCase))
        {
            return Fail("market save payload still contains duplicate derived price fields");
        }

        CommodityDealer resumed = DockedDealer(station);
        resumed.RestoreMarketState(state);
        StationMarketListing actual = FindListing(resumed, food);
        return actual.Stock == expected.Stock && actual.BuyPrice == expected.BuyPrice && actual.SellPrice == expected.SellPrice &&
            actual.BaseBuyPrice == 85 && actual.BaseSellPrice == 60
            ? Pass()
            : Fail("derived prices were not recomputed consistently after load");
    }

    private (bool Success, string FailureReason) ValidateOldSaveInitialization()
    {
        Station station = ResolveStation("Newark Station");
        Commodity food = CommodityCatalog.GetById("food-rations");
        CommodityDealer defaultDealer = DockedDealer(station);
        StationMarketListing baseline = FindListing(defaultDealer, food);
        if (baseline.Stock != 220 || baseline.BuyPrice != 150 || baseline.SellPrice != 115)
        {
            return Fail("default market did not initialize from current configuration");
        }

        CommodityDealer oldSaveDealer = DockedDealer(station);
        oldSaveDealer.RestoreMarketState(new List<SaveMarketStateData>
        {
            new SaveMarketStateData
            {
                StationKey = "newarkstation",
                Listings = new List<SaveMarketListingData>
                {
                    new SaveMarketListingData
                    {
                        CommodityId = food.Id,
                        BuyPrice = 1,
                        SellPrice = 1,
                        Stock = 10,
                        DemandLevel = 1,
                        IsAvailable = true
                    }
                }
            }
        });

        StationMarketListing restored = FindListing(oldSaveDealer, food);
        return restored.Stock == 10 && restored.BaseBuyPrice == 150 && restored.BuyPrice != 1 && restored.SellPrice > 0
            ? Pass()
            : Fail("old/default save did not initialize dynamic state safely from configuration");
    }

    private (bool Success, string FailureReason) ValidateShipDealerCargo()
    {
        ShipDealer dealer = new();
        Ship playerShip = new(Vector3.Zero);
        Commodity food = CommodityCatalog.GetById("food-rations");
        Commodity package = CommodityCatalog.GetById("sealed-data-package");
        CommodityDealer marketDealer = DockedDealer(ResolveStation("Fort Bush"));
        int marketStockBefore = FindListing(marketDealer, food).Stock;
        if (!playerShip.CargoHold.AddCommodity(food, 5))
            return Fail("could not stage ordinary cargo for ship dealer regression");
        if (!playerShip.CargoHold.AddMissionCargo(9901, package, 1))
            return Fail("could not stage mission cargo for ship dealer regression");
        ShipDefinition upgrade = dealer.GetShipByName("Pirate Transport");
        PlayerCredits credits = new(dealer.GetTotalCost(upgrade));
        if (!dealer.TryPurchaseShip(upgrade, credits, playerShip, out string message))
            return Fail($"ship upgrade rejected ordinary cargo: {message}");
        if (playerShip.CargoHold.GetCommodityQuantity(food.Name) != 5 ||
            !playerShip.CargoHold.HasMissionCargo(9901, package.Id, 1) ||
            playerShip.CargoHold.UsedCapacity != food.VolumePerUnit * 5 + package.VolumePerUnit)
            return Fail("ship upgrade lost or duplicated ordinary commodity cargo");

        if (FindListing(marketDealer, food).Stock != marketStockBefore)
            return Fail("unrelated ship purchase changed station market stock");

        ShipDefinition tiny = new(
            "Focused Tiny Ship",
            "Regression fixture",
            "SHIPS/scimitar/Scimitar2",
            1000)
        {
            CargoCapacity = 1
        };
        Ship rejectedShip = new(Vector3.Zero);
        if (!rejectedShip.CargoHold.AddMissionCargo(9902, package, 1))
            return Fail("could not stage protected cargo for over-capacity test");
        PlayerCredits rejectedCredits = new(10_000);
        if (dealer.TryPurchaseShip(tiny, rejectedCredits, rejectedShip, out _))
            return Fail("over-capacity ship purchase unexpectedly succeeded");
        if (!rejectedShip.CargoHold.HasMissionCargo(9902, package.Id, 1) || rejectedCredits.Credits != 10_000)
            return Fail("rejected over-capacity ship purchase mutated protected cargo or credits");

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
