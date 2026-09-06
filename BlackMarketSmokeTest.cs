using Microsoft.Xna.Framework;
using Roguelancer.Configuration;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace Roguelancer;

/// <summary>
/// Focused Phase 55 proof. Every transaction is driven through CommodityDealer
/// and the existing MarketManager/CargoHold/PlayerCredits authorities.
/// </summary>
internal sealed class BlackMarketSmokeTest
{
    private static readonly IReadOnlyList<Station> Stations = LoadFixtureStations();

    public (int Passed, int Failed) Run()
    {
        int passed = 0;
        int failed = 0;

        RunCase("canonical contraband drives eligibility", CanonicalContrabandDrivesEligibility, ref passed, ref failed);
        RunCase("legal commodity excluded", LegalCommodityExcluded, ref passed, ref failed);
        RunCase("Buffalo hosts black market", BuffaloHostsBlackMarket, ref passed, ref failed);
        RunCase("Rochester salvage contact hosts black market", RochesterHostsBlackMarket, ref passed, ref failed);
        RunCase("Police station has no black market", PoliceStationHasNoBlackMarket, ref passed, ref failed);
        RunCase("corporation station has no black market", CorporationStationHasNoBlackMarket, ref passed, ref failed);
        RunCase("station without market has no black market", StationWithoutMarketHasNoBlackMarket, ref passed, ref failed);
        RunCase("host faction identity is deterministic", HostFactionIdentity, ref passed, ref failed);
        RunCase("hostile standing is denied", HostileStandingDenied, ref passed, ref failed);
        RunCase("unfriendly standing is denied", UnfriendlyStandingDenied, ref passed, ref failed);
        RunCase("neutral standing is allowed", NeutralStandingAllowed, ref passed, ref failed);
        RunCase("friendly standing is allowed", FriendlyStandingAllowed, ref passed, ref failed);
        RunCase("access uses live reputation", AccessUsesLiveReputation, ref passed, ref failed);
        RunCase("locked reason names required tier", LockedReasonNamesRequiredTier, ref passed, ref failed);
        RunCase("initial stock is bounded", InitialStockIsBounded, ref passed, ref failed);
        RunCase("initial stock is deterministic", InitialStockIsDeterministic, ref passed, ref failed);
        RunCase("initial price is deterministic", InitialPriceIsDeterministic, ref passed, ref failed);
        RunCase("buy prices use illicit bounds", BuyPricesUseIllicitBounds, ref passed, ref failed);
        RunCase("sell prices use illicit bounds", SellPricesUseIllicitBounds, ref passed, ref failed);
        RunCase("same station spread is negative", SameStationSpreadIsNegative, ref passed, ref failed);
        RunCase("same station arbitrage is impossible", SameStationArbitrageIsImpossible, ref passed, ref failed);
        RunCase("black market lists only contraband", BlackMarketListsOnlyContraband, ref passed, ref failed);
        RunCase("black market rejects legal trade", BlackMarketRejectsLegalTrade, ref passed, ref failed);
        RunCase("buy without credits fails", BuyWithoutCreditsFails, ref passed, ref failed);
        RunCase("failed credit check is atomic", FailedCreditCheckIsAtomic, ref passed, ref failed);
        RunCase("purchase respects exact cargo capacity", PurchaseRespectsExactCapacity, ref passed, ref failed);
        RunCase("failed capacity check is atomic", FailedCapacityCheckIsAtomic, ref passed, ref failed);
        RunCase("zero quantity purchase fails", ZeroQuantityPurchaseFails, ref passed, ref failed);
        RunCase("purchase stock is bounded", PurchaseStockIsBounded, ref passed, ref failed);
        RunCase("successful purchase deducts exact credits", PurchaseDeductsExactCredits, ref passed, ref failed);
        RunCase("successful purchase removes exact stock", PurchaseRemovesExactStock, ref passed, ref failed);
        RunCase("successful purchase adds exact cargo", PurchaseAddsExactCargo, ref passed, ref failed);
        RunCase("purchased cargo remains canonical", PurchasedCargoRemainsCanonical, ref passed, ref failed);
        RunCase("Police recognizes purchased cargo", PoliceRecognizesPurchasedCargo, ref passed, ref failed);
        RunCase("purchased cargo is confiscatable", PurchasedCargoIsConfiscatable, ref passed, ref failed);
        RunCase("purchased cargo supports refusal", PurchasedCargoSupportsRefusal, ref passed, ref failed);
        RunCase("purchased cargo supports pursuit", PurchasedCargoSupportsPursuit, ref passed, ref failed);
        RunCase("ordinary contraband is sellable", OrdinaryContrabandIsSellable, ref passed, ref failed);
        RunCase("sale removes exact cargo", SaleRemovesExactCargo, ref passed, ref failed);
        RunCase("sale awards exact credits", SaleAwardsExactCredits, ref passed, ref failed);
        RunCase("sale increases exact stock", SaleIncreasesExactStock, ref passed, ref failed);
        RunCase("oversell is atomic", OversellIsAtomic, ref passed, ref failed);
        RunCase("lawful dealer refuses contraband buy", LawfulDealerRefusesContrabandBuy, ref passed, ref failed);
        RunCase("lawful dealer refuses contraband sale", LawfulDealerRefusesContrabandSale, ref passed, ref failed);
        RunCase("lawful refusal gives no credits", LawfulRefusalGivesNoCredits, ref passed, ref failed);
        RunCase("lawful refusal leaves cargo", LawfulRefusalLeavesCargo, ref passed, ref failed);
        RunCase("legal commodity trade remains normal", LegalCommodityTradeRemainsNormal, ref passed, ref failed);
        RunCase("mission reservation is protected", MissionReservationIsProtected, ref passed, ref failed);
        RunCase("mixed reserved quantity is computed", MixedReservedQuantityIsComputed, ref passed, ref failed);
        RunCase("unreserved identical quantity remains sellable", UnreservedIdenticalQuantityIsSellable, ref passed, ref failed);
        RunCase("mission reservation survives sale", MissionReservationSurvivesSale, ref passed, ref failed);
        RunCase("physical contraband recovery remains ordinary", PhysicalContrabandRecoveryRemainsOrdinary, ref passed, ref failed);
        RunCase("recovered contraband is sellable", RecoveredContrabandIsSellable, ref passed, ref failed);
        RunCase("black-market trade does not lower Police reputation", TradeDoesNotLowerPoliceReputation, ref passed, ref failed);
        RunCase("black-market trade grants no Rogue reputation", TradeGrantsNoRogueReputation, ref passed, ref failed);
        RunCase("stations have deterministic price differences", StationsHaveDeterministicPriceDifferences, ref passed, ref failed);
        RunCase("cross-station prices remain bounded", CrossStationPricesRemainBounded, ref passed, ref failed);
        RunCase("stock survives save/load", StockSurvivesSaveLoad, ref passed, ref failed);
        RunCase("stock does not duplicate after load", StockDoesNotDuplicateAfterLoad, ref passed, ref failed);
        RunCase("prices recompute after load", PricesRecomputeAfterLoad, ref passed, ref failed);
        RunCase("reputation gate survives load", ReputationGateSurvivesLoad, ref passed, ref failed);
        RunCase("reset restores market state", ResetRestoresMarketState, ref passed, ref failed);
        RunCase("buy transaction emits one completion", BuyTransactionEmitsOneCompletion, ref passed, ref failed);
        RunCase("failed transaction emits no completion", FailedTransactionEmitsNoCompletion, ref passed, ref failed);
        RunCase("no negative stock", NoNegativeStock, ref passed, ref failed);
        RunCase("no negative credits", NoNegativeCredits, ref passed, ref failed);
        RunCase("no negative cargo", NoNegativeCargo, ref passed, ref failed);
        RunCase("Rogue contact exposes the action", RogueContactExposesAction, ref passed, ref failed);
        RunCase("station UI opens black market", StationUiOpensBlackMarket, ref passed, ref failed);
        RunCase("station UI keeps lawful contact locked", StationUiKeepsLawfulContactLocked, ref passed, ref failed);

        Console.WriteLine($"[BLACK MARKET SMOKE] RESULT: {passed} passed, {failed} failed");
        return (passed, failed);
    }

    private static void RunCase(string label, Func<(bool Success, string FailureReason)> test, ref int passed, ref int failed)
    {
        try
        {
            (bool success, string reason) = test();
            if (success)
            {
                passed++;
                Console.WriteLine($"[BLACK MARKET SMOKE] PASS {label}");
            }
            else
            {
                failed++;
                Console.WriteLine($"[BLACK MARKET SMOKE] FAIL {label}: {reason}");
            }
        }
        catch (Exception ex)
        {
            failed++;
            Console.WriteLine($"[BLACK MARKET SMOKE] FAIL {label}: {ex.Message}");
        }
    }

    private static (bool, string) CanonicalContrabandDrivesEligibility()
    {
        Context c = Create("Buffalo Base");
        Commodity sideArms = CommodityCatalog.GetById("side-arms");
        return Check(sideArms?.IsContraband == true && c.Manager.GetBlackMarketListingsForStation(c.Station).Any(l => l.Commodity.Id == sideArms.Id), "canonical side-arms metadata did not drive listing");
    }

    private static (bool, string) LegalCommodityExcluded()
    {
        Context c = Create("Buffalo Base");
        return Check(c.Manager.GetBlackMarketListingsForStation(c.Station).All(l => l.Commodity.IsContraband) && !c.Manager.GetBlackMarketListingsForStation(c.Station).Any(l => l.Commodity.Id == "food-rations"), "legal food appeared in black market");
    }

    private static (bool, string) BuffaloHostsBlackMarket() => Check(Create("Buffalo Base").Manager.HasBlackMarketForStation(Create("Buffalo Base").Station), "Buffalo Base was not recognized as a host");
    private static (bool, string) RochesterHostsBlackMarket() => Check(Create("Rochester Base").Manager.HasBlackMarketForStation(Create("Rochester Base").Station), "Rochester Base salvage contact was not recognized as a host");
    private static (bool, string) PoliceStationHasNoBlackMarket() => Check(!Create("Fort Bush").Manager.HasBlackMarketForStation(Create("Fort Bush").Station), "Police station exposed a black market");
    private static (bool, string) CorporationStationHasNoBlackMarket() => Check(!Create("Detroit Munitions").Manager.HasBlackMarketForStation(Create("Detroit Munitions").Station), "corporation station exposed a black market");
    private static (bool, string) StationWithoutMarketHasNoBlackMarket() => Check(!Create("Alcatraz Depot").Manager.HasBlackMarketForStation(Create("Alcatraz Depot").Station), "unconfigured station exposed a black market");
    private static (bool, string) HostFactionIdentity() => Check(Create("Buffalo Base").Manager.GetMarketFactionId(Create("Buffalo Base").Station) == FactionManager.LibertyRogues && Create("Rochester Base").Manager.GetMarketFactionId(Create("Rochester Base").Station) == FactionManager.Junkers, "host faction identity was not resolved from existing station/market data");

    private static (bool, string) HostileStandingDenied() => OpenAtStanding(-0.70f, false, "hostile standing was accepted");
    private static (bool, string) UnfriendlyStandingDenied() => OpenAtStanding(-0.20f, false, "unfriendly standing was accepted");
    private static (bool, string) NeutralStandingAllowed() => OpenAtStanding(0.00f, true, "neutral standing was denied");
    private static (bool, string) FriendlyStandingAllowed() => OpenAtStanding(0.35f, true, "friendly standing was denied");

    private static (bool, string) AccessUsesLiveReputation()
    {
        Context c = Create("Buffalo Base");
        if (!c.Dealer.TryOpenBlackMarket(out _)) return Fail("initial neutral/friendly access failed");
        c.Reputation.SetReputation(FactionManager.LibertyRogues, -0.70f);
        bool locked = c.Dealer.GetCurrentListings().Count == 0;
        c.Reputation.SetReputation(FactionManager.LibertyRogues, 0.00f);
        return Check(locked && c.Dealer.GetCurrentListings().Count > 0, "open surface did not follow live reputation");
    }

    private static (bool, string) LockedReasonNamesRequiredTier()
    {
        Context c = Create("Buffalo Base");
        c.Reputation.SetReputation(FactionManager.LibertyRogues, -0.70f);
        return c.Dealer.TryOpenBlackMarket(out string message)
            ? Fail("hostile access unexpectedly opened")
            : Check(message.Contains("Neutral", StringComparison.OrdinalIgnoreCase) && message.Contains("Liberty Rogues", StringComparison.OrdinalIgnoreCase), $"bounded lock reason was '{message}'");
    }

    private static (bool, string) InitialStockIsBounded()
    {
        Context c = Create("Buffalo Base");
        return Check(c.Manager.GetBlackMarketListingsForStation(c.Station).All(l => l.Stock >= 2 && l.Stock <= 12), "initial contraband stock escaped the 2-12 bound");
    }

    private static (bool, string) InitialStockIsDeterministic()
    {
        Context a = Create("Buffalo Base");
        Context b = Create("Buffalo Base");
        return Check(SameBlackListings(a, b, (x, y) => x.Stock == y.Stock), "same station stock differed between fresh managers");
    }

    private static (bool, string) InitialPriceIsDeterministic()
    {
        Context a = Create("Buffalo Base");
        Context b = Create("Buffalo Base");
        return Check(SameBlackListings(a, b, (x, y) => x.BuyPrice == y.BuyPrice && x.SellPrice == y.SellPrice), "same station price differed between fresh managers");
    }

    private static (bool, string) BuyPricesUseIllicitBounds() => Check(AllBlackListings("Buffalo Base").All(l => l.BuyPrice >= Math.Ceiling(l.Commodity.BasePrice * 1.15m) && l.BuyPrice <= Math.Ceiling(l.Commodity.BasePrice * 1.40m)), "black-market buy price escaped the bounded illicit range");
    private static (bool, string) SellPricesUseIllicitBounds() => Check(AllBlackListings("Buffalo Base").All(l => l.SellPrice >= Math.Floor(l.Commodity.BasePrice * 0.70m) && l.SellPrice <= l.Commodity.BasePrice), "black-market sell price escaped the bounded illicit range");
    private static (bool, string) SameStationSpreadIsNegative() => Check(AllBlackListings("Buffalo Base").All(l => l.BuyPrice > l.SellPrice), "black-market buy price was not above sell price");

    private static (bool, string) SameStationArbitrageIsImpossible()
    {
        Context c = Create("Buffalo Base");
        if (!c.Dealer.TryOpenBlackMarket(out _)) return Fail("could not open black market");
        Commodity commodity = c.Manager.GetBlackMarketListingsForStation(c.Station).First().Commodity;
        StationMarketListing before = c.Manager.GetListingForCommodity(c.Station, commodity, MarketSurface.BlackMarket);
        PlayerCredits credits = new(100_000);
        CargoHold cargo = new(50);
        if (!c.Manager.TryBuy(c.Station, commodity, 1, credits, cargo, MarketSurface.BlackMarket, c.Reputation, out _)) return Fail("black-market purchase failed");
        if (!c.Manager.TrySell(c.Station, commodity, 1, credits, cargo, MarketSurface.BlackMarket, c.Reputation, out _)) return Fail("black-market sale failed");
        return Check(credits.Credits < 100_000 && before.BuyPrice > before.SellPrice, "same-station round trip was profitable");
    }

    private static (bool, string) BlackMarketListsOnlyContraband() => Check(AllBlackListings("Rochester Base").Count > 0 && AllBlackListings("Rochester Base").All(l => l.Commodity.IsContraband), "legal listing appeared on illicit surface");
    private static (bool, string) BlackMarketRejectsLegalTrade()
    {
        Context c = Create("Buffalo Base");
        c.Dealer.TryOpenBlackMarket(out _);
        Commodity food = CommodityCatalog.GetById("food-rations");
        return c.Dealer.TryBuyCommodity(food, 1, new PlayerCredits(100_000), new CargoHold(10), out string message)
            ? Fail("black market bought legal food")
            : Check(message.Contains("contraband", StringComparison.OrdinalIgnoreCase), $"legal rejection was '{message}'");
    }

    private static (bool, string) BuyWithoutCreditsFails()
    {
        Context c = OpenContext("Buffalo Base");
        Commodity commodity = FirstBlack(c);
        PlayerCredits credits = new(0);
        CargoHold cargo = new(20);
        return !c.Dealer.TryBuyCommodity(commodity, 1, credits, cargo, out _) && credits.Credits == 0 && cargo.GetCommodityQuantity(commodity.Name) == 0
            ? Pass() : Fail("purchase without credits mutated or succeeded");
    }

    private static (bool, string) FailedCreditCheckIsAtomic()
    {
        Context c = OpenContext("Buffalo Base");
        Commodity commodity = FirstBlack(c);
        StationMarketListing before = c.Manager.GetListingForCommodity(c.Station, commodity, MarketSurface.BlackMarket);
        PlayerCredits credits = new(1);
        CargoHold cargo = new(20);
        c.Dealer.TryBuyCommodity(commodity, 1, credits, cargo, out _);
        StationMarketListing after = c.Manager.GetListingForCommodity(c.Station, commodity, MarketSurface.BlackMarket);
        return Check(credits.Credits == 1 && cargo.UsedCapacity == 0 && after.Stock == before.Stock, "failed credit check changed state");
    }

    private static (bool, string) PurchaseRespectsExactCapacity()
    {
        Context c = OpenContext("Buffalo Base");
        Commodity commodity = FirstBlack(c);
        CargoHold cargo = new(commodity.VolumePerUnit);
        return Check(c.Dealer.TryBuyCommodity(commodity, 1, new PlayerCredits(100_000), cargo, out _), "exact remaining capacity was rejected");
    }

    private static (bool, string) FailedCapacityCheckIsAtomic()
    {
        Context c = OpenContext("Buffalo Base");
        Commodity commodity = FirstBlack(c);
        StationMarketListing before = c.Manager.GetListingForCommodity(c.Station, commodity, MarketSurface.BlackMarket);
        PlayerCredits credits = new(100_000);
        CargoHold cargo = new(Math.Max(0, commodity.VolumePerUnit - 1));
        bool success = c.Dealer.TryBuyCommodity(commodity, 1, credits, cargo, out _);
        StationMarketListing after = c.Manager.GetListingForCommodity(c.Station, commodity, MarketSurface.BlackMarket);
        return Check(!success && credits.Credits == 100_000 && cargo.UsedCapacity == 0 && after.Stock == before.Stock, "failed capacity check changed state");
    }

    private static (bool, string) ZeroQuantityPurchaseFails()
    {
        Context c = OpenContext("Buffalo Base");
        Commodity commodity = FirstBlack(c);
        return Check(!c.Dealer.TryBuyCommodity(commodity, 0, new PlayerCredits(100_000), new CargoHold(20), out _), "zero quantity purchase succeeded");
    }

    private static (bool, string) PurchaseStockIsBounded()
    {
        Context c = OpenContext("Buffalo Base");
        Commodity commodity = FirstBlack(c);
        StationMarketListing before = c.Manager.GetListingForCommodity(c.Station, commodity, MarketSurface.BlackMarket);
        CargoHold cargo = new(100);
        PlayerCredits credits = new(1_000_000);
        if (!c.Dealer.TryBuyCommodity(commodity, before.Stock, credits, cargo, out _)) return Fail("could not drain bounded stock");
        StationMarketListing after = c.Manager.GetListingForCommodity(c.Station, commodity, MarketSurface.BlackMarket);
        return Check(after.Stock >= after.MinimumStock && after.Stock >= 0, "purchase drove stock below its bound");
    }

    private static (bool, string) PurchaseDeductsExactCredits()
    {
        Context c = OpenContext("Buffalo Base");
        Commodity commodity = FirstBlack(c);
        int price = BlackListing(c, commodity).BuyPrice;
        PlayerCredits credits = new(100_000);
        return Check(c.Dealer.TryBuyCommodity(commodity, 2, credits, new CargoHold(20), out _) && credits.Credits == 100_000 - price * 2, "purchase credit mutation was not exact");
    }

    private static (bool, string) PurchaseRemovesExactStock()
    {
        Context c = OpenContext("Buffalo Base");
        Commodity commodity = FirstBlack(c);
        int stock = BlackListing(c, commodity).Stock;
        if (!c.Dealer.TryBuyCommodity(commodity, 2, new PlayerCredits(100_000), new CargoHold(20), out _)) return Fail("purchase failed");
        return Check(BlackListing(c, commodity).Stock == stock - 2, "purchase stock mutation was not exact");
    }

    private static (bool, string) PurchaseAddsExactCargo()
    {
        Context c = OpenContext("Buffalo Base");
        Commodity commodity = FirstBlack(c);
        CargoHold cargo = new(20);
        return Check(c.Dealer.TryBuyCommodity(commodity, 2, new PlayerCredits(100_000), cargo, out _) && cargo.GetCommodityQuantity(commodity.Name) == 2 && cargo.UsedCapacity == commodity.VolumePerUnit * 2, "purchase cargo mutation was not exact");
    }

    private static (bool, string) PurchasedCargoRemainsCanonical()
    {
        Context c = OpenContext("Buffalo Base");
        Commodity commodity = FirstBlack(c);
        CargoHold cargo = new(20);
        c.Dealer.TryBuyCommodity(commodity, 1, new PlayerCredits(100_000), cargo, out _);
        return Check(cargo.GetCommodityQuantity(commodity.Name) == 1 && CommodityCatalog.GetById(commodity.Id).IsContraband, "purchased cargo was not canonical contraband");
    }

    private static (bool, string) PoliceRecognizesPurchasedCargo()
    {
        Context c = OpenContext("Buffalo Base");
        Commodity commodity = FirstBlack(c);
        CargoHold cargo = new(20);
        c.Dealer.TryBuyCommodity(commodity, 1, new PlayerCredits(100_000), cargo, out _);
        PoliceEnforcementOffer offer = new PoliceEnforcementService().Evaluate(FactionManager.LibertyPolice, cargo, new PlayerCredits(100_000));
        return Check(offer.HasContraband && offer.Contraband.Any(f => f.CommodityId == commodity.Id && f.Quantity == 1), "Police did not detect purchased contraband");
    }

    private static (bool, string) PurchasedCargoIsConfiscatable()
    {
        Context c = OpenContext("Buffalo Base");
        Commodity commodity = FirstBlack(c);
        CargoHold cargo = new(20);
        PlayerCredits credits = new(100_000);
        c.Dealer.TryBuyCommodity(commodity, 1, credits, cargo, out _);
        PoliceEnforcementService service = new();
        PoliceEnforcementOffer offer = service.Evaluate(FactionManager.LibertyPolice, cargo, credits);
        bool resolved = service.TryResolve(offer, PoliceEnforcementResolution.Comply, cargo, credits, c.Reputation, out PoliceEnforcementResult result, out _);
        return Check(resolved && result.ConfiscatedQuantity == 1 && cargo.GetCommodityQuantity(commodity.Name) == 0, "purchased cargo was not confiscatable");
    }

    private static (bool, string) PurchasedCargoSupportsRefusal()
    {
        Context c = OpenContext("Buffalo Base");
        Commodity commodity = FirstBlack(c);
        CargoHold cargo = new(20);
        PlayerCredits credits = new(100_000);
        c.Dealer.TryBuyCommodity(commodity, 1, credits, cargo, out _);
        PoliceEnforcementService service = new();
        PoliceEnforcementOffer offer = service.Evaluate(FactionManager.LibertyPolice, cargo, credits);
        bool resolved = service.TryResolve(offer, PoliceEnforcementResolution.Refuse, cargo, credits, c.Reputation, out PoliceEnforcementResult result, out _);
        return Check(resolved && result.Outcome == PoliceEnforcementOutcome.Refused && cargo.GetCommodityQuantity(commodity.Name) == 1, "refusal did not use ordinary purchased cargo");
    }

    private static (bool, string) PurchasedCargoSupportsPursuit()
    {
        Context c = OpenContext("Buffalo Base");
        Commodity commodity = FirstBlack(c);
        CargoHold cargo = new(20);
        c.Dealer.TryBuyCommodity(commodity, 1, new PlayerCredits(100_000), cargo, out _);
        Ship ship = new(Vector3.Zero);
        PoliceFugitiveManager fugitive = new(c.Reputation);
        return Check(cargo.GetCommodityQuantity(commodity.Name) == 1 && fugitive.BeginPursuit(ship), "pursuit path was not available alongside purchased cargo");
    }

    private static (bool, string) OrdinaryContrabandIsSellable()
    {
        Context c = OpenContext("Buffalo Base");
        Commodity commodity = FirstBlack(c);
        CargoHold cargo = new(20);
        PlayerCredits credits = new(100);
        cargo.AddCommodity(commodity, 1);
        return Check(c.Dealer.TrySellCommodity(commodity, 1, credits, cargo, out _), "ordinary held contraband could not be fenced");
    }

    private static (bool, string) SaleRemovesExactCargo()
    {
        Context c = OpenContext("Buffalo Base");
        Commodity commodity = FirstBlack(c);
        CargoHold cargo = new(20);
        cargo.AddCommodity(commodity, 3);
        if (!c.Dealer.TrySellCommodity(commodity, 2, new PlayerCredits(100), cargo, out _)) return Fail("sale failed");
        return Check(cargo.GetCommodityQuantity(commodity.Name) == 1 && cargo.UsedCapacity == commodity.VolumePerUnit, "sale did not remove exact cargo");
    }

    private static (bool, string) SaleAwardsExactCredits()
    {
        Context c = OpenContext("Buffalo Base");
        Commodity commodity = FirstBlack(c);
        int price = BlackListing(c, commodity).SellPrice;
        CargoHold cargo = new(20);
        cargo.AddCommodity(commodity, 2);
        PlayerCredits credits = new(100);
        return Check(c.Dealer.TrySellCommodity(commodity, 2, credits, cargo, out _) && credits.Credits == 100 + price * 2, "sale credit mutation was not exact");
    }

    private static (bool, string) SaleIncreasesExactStock()
    {
        Context c = OpenContext("Buffalo Base");
        Commodity commodity = FirstBlack(c);
        int stock = BlackListing(c, commodity).Stock;
        CargoHold cargo = new(20);
        cargo.AddCommodity(commodity, 1);
        if (!c.Dealer.TrySellCommodity(commodity, 1, new PlayerCredits(100), cargo, out _)) return Fail("sale failed");
        return Check(BlackListing(c, commodity).Stock == stock + 1, "sale stock mutation was not exact");
    }

    private static (bool, string) OversellIsAtomic()
    {
        Context c = OpenContext("Buffalo Base");
        Commodity commodity = FirstBlack(c);
        CargoHold cargo = new(20);
        PlayerCredits credits = new(100);
        cargo.AddCommodity(commodity, 1);
        int stock = BlackListing(c, commodity).Stock;
        bool success = c.Dealer.TrySellCommodity(commodity, 2, credits, cargo, out _);
        return Check(!success && credits.Credits == 100 && cargo.GetCommodityQuantity(commodity.Name) == 1 && BlackListing(c, commodity).Stock == stock, "oversell changed state");
    }

    private static (bool, string) LawfulDealerRefusesContrabandBuy()
    {
        Context c = Create("Fort Bush");
        Commodity commodity = CommodityCatalog.GetById("side-arms");
        PlayerCredits credits = new(100_000);
        CargoHold cargo = new(20);
        bool success = c.Dealer.TryBuyCommodity(commodity, 1, credits, cargo, out string message);
        return Check(!success && message.Contains("will not handle contraband", StringComparison.OrdinalIgnoreCase), $"lawful buy response was '{message}'");
    }

    private static (bool, string) LawfulDealerRefusesContrabandSale()
    {
        Context c = Create("Fort Bush");
        Commodity commodity = CommodityCatalog.GetById("side-arms");
        CargoHold cargo = new(20);
        cargo.AddCommodity(commodity, 1);
        return c.Dealer.TrySellCommodity(commodity, 1, new PlayerCredits(100), cargo, out string message)
            ? Fail("lawful dealer bought contraband")
            : Check(message.Contains("will not handle contraband", StringComparison.OrdinalIgnoreCase), $"lawful sale response was '{message}'");
    }

    private static (bool, string) LawfulRefusalGivesNoCredits()
    {
        Context c = Create("Newark Station");
        Commodity commodity = CommodityCatalog.GetById("side-arms");
        CargoHold cargo = new(20);
        cargo.AddCommodity(commodity, 1);
        PlayerCredits credits = new(100);
        c.Dealer.TrySellCommodity(commodity, 1, credits, cargo, out _);
        return Check(credits.Credits == 100, "lawful refusal awarded credits");
    }

    private static (bool, string) LawfulRefusalLeavesCargo()
    {
        Context c = Create("Newark Station");
        Commodity commodity = CommodityCatalog.GetById("side-arms");
        CargoHold cargo = new(20);
        cargo.AddCommodity(commodity, 1);
        c.Dealer.TrySellCommodity(commodity, 1, new PlayerCredits(100), cargo, out _);
        return Check(cargo.GetCommodityQuantity(commodity.Name) == 1, "lawful refusal removed cargo");
    }

    private static (bool, string) LegalCommodityTradeRemainsNormal()
    {
        Context c = Create("Fort Bush");
        Commodity food = CommodityCatalog.GetById("food-rations");
        CargoHold cargo = new(20);
        PlayerCredits credits = new(10_000);
        StationMarketListing listing = c.Manager.GetListingForCommodity(c.Station, food);
        return Check(c.Dealer.TryBuyCommodity(food, 1, credits, cargo, out _) && credits.Credits == 10_000 - listing.BuyPrice && cargo.GetCommodityQuantity(food.Name) == 1, "legal commodity trade regressed");
    }

    private static (bool, string) MissionReservationIsProtected()
    {
        Context c = OpenContext("Buffalo Base");
        Commodity commodity = FirstBlack(c);
        CargoHold cargo = new(30);
        cargo.AddMissionCargo(5501, commodity, 2);
        PlayerCredits credits = new(100);
        bool success = c.Dealer.TrySellCommodity(commodity, 1, credits, cargo, out string message);
        return Check(!success && cargo.GetCommodityQuantity(commodity.Name) == 2 && cargo.HasMissionCargo(5501, commodity.Id, 2) && message.Contains("mission", StringComparison.OrdinalIgnoreCase), "reserved contraband was sold");
    }

    private static (bool, string) MixedReservedQuantityIsComputed()
    {
        Context c = OpenContext("Buffalo Base");
        Commodity commodity = FirstBlack(c);
        CargoHold cargo = new(30);
        cargo.AddMissionCargo(5502, commodity, 2);
        cargo.AddCommodity(commodity, 3);
        return Check(cargo.GetCommodityQuantity(commodity.Name) == 5 && cargo.GetMissionReservedQuantity(commodity.Name) == 2 && cargo.GetSellableCommodityQuantity(commodity.Name) == 3, "mixed reservation totals were incorrect");
    }

    private static (bool, string) UnreservedIdenticalQuantityIsSellable()
    {
        Context c = OpenContext("Buffalo Base");
        Commodity commodity = FirstBlack(c);
        CargoHold cargo = new(30);
        cargo.AddMissionCargo(5503, commodity, 2);
        cargo.AddCommodity(commodity, 3);
        return Check(c.Dealer.TrySellCommodity(commodity, 3, new PlayerCredits(100), cargo, out _) && cargo.GetCommodityQuantity(commodity.Name) == 2, "unreserved identical quantity was not sellable");
    }

    private static (bool, string) MissionReservationSurvivesSale()
    {
        Context c = OpenContext("Buffalo Base");
        Commodity commodity = FirstBlack(c);
        CargoHold cargo = new(30);
        cargo.AddMissionCargo(5504, commodity, 2);
        cargo.AddCommodity(commodity, 3);
        c.Dealer.TrySellCommodity(commodity, 3, new PlayerCredits(100), cargo, out _);
        return Check(cargo.HasMissionCargo(5504, commodity.Id, 2) && cargo.GetSellableCommodityQuantity(commodity.Name) == 0, "mission reservation was lost during fencing");
    }

    private static (bool, string) PhysicalContrabandRecoveryRemainsOrdinary()
    {
        Context c = OpenContext("Buffalo Base");
        Commodity commodity = FirstBlack(c);
        Ship ship = new(Vector3.Zero);
        ship.CargoHold.AddCommodity(commodity, 1);
        using LootManager loot = new();
        int removed = loot.TryJettisonContraband(ship.CargoHold, Vector3.Zero, Vector3.Zero, out int pods);
        loot.Update(new GameTime(TimeSpan.FromSeconds(0.1), TimeSpan.FromSeconds(0.1)), ship, false);
        return Check(removed == 1 && pods == 1 && ship.CargoHold.GetCommodityQuantity(commodity.Name) == 1 && ship.CargoHold.GetMissionReservedQuantity(commodity.Name) == 0, "jettison/recovery changed ordinary cargo identity");
    }

    private static (bool, string) RecoveredContrabandIsSellable()
    {
        Context c = OpenContext("Buffalo Base");
        Commodity commodity = FirstBlack(c);
        Ship ship = new(Vector3.Zero);
        ship.CargoHold.AddCommodity(commodity, 1);
        using LootManager loot = new();
        loot.TryJettisonContraband(ship.CargoHold, Vector3.Zero, Vector3.Zero, out _);
        loot.Update(new GameTime(TimeSpan.FromSeconds(0.1), TimeSpan.FromSeconds(0.1)), ship, false);
        return Check(ship.CargoHold.GetSellableCommodityQuantity(commodity.Name) == 1 && c.Dealer.TrySellCommodity(commodity, 1, new PlayerCredits(100), ship.CargoHold, out _), "recovered contraband was not sellable");
    }

    private static (bool, string) TradeDoesNotLowerPoliceReputation()
    {
        Context c = OpenContext("Buffalo Base");
        float before = c.Reputation.GetStanding(FactionManager.LibertyPolice);
        Commodity commodity = FirstBlack(c);
        CargoHold cargo = new(20);
        c.Dealer.TryBuyCommodity(commodity, 1, new PlayerCredits(100_000), cargo, out _);
        c.Dealer.TrySellCommodity(commodity, 1, new PlayerCredits(100_000), cargo, out _);
        return Check(c.Reputation.GetStanding(FactionManager.LibertyPolice) == before, "black-market commerce changed Police reputation");
    }

    private static (bool, string) TradeGrantsNoRogueReputation()
    {
        Context c = OpenContext("Buffalo Base");
        float before = c.Reputation.GetStanding(FactionManager.LibertyRogues);
        Commodity commodity = FirstBlack(c);
        CargoHold cargo = new(20);
        PlayerCredits credits = new(100_000);
        c.Dealer.TryBuyCommodity(commodity, 1, credits, cargo, out _);
        c.Dealer.TrySellCommodity(commodity, 1, credits, cargo, out _);
        return Check(c.Reputation.GetStanding(FactionManager.LibertyRogues) == before, "black-market commerce changed Rogue reputation");
    }

    private static (bool, string) StationsHaveDeterministicPriceDifferences()
    {
        Context buffalo = Create("Buffalo Base");
        Context rochester = Create("Rochester Base");
        Commodity sideArms = CommodityCatalog.GetById("side-arms");
        StationMarketListing a = buffalo.Manager.GetListingForCommodity(buffalo.Station, sideArms, MarketSurface.BlackMarket);
        StationMarketListing b = rochester.Manager.GetListingForCommodity(rochester.Station, sideArms, MarketSurface.BlackMarket);
        return Check(a != null && b != null && (a.BuyPrice != b.BuyPrice || a.SellPrice != b.SellPrice), "black-market stations had identical prices");
    }

    private static (bool, string) CrossStationPricesRemainBounded() => Check(new[] { "Buffalo Base", "Rochester Base" }.SelectMany(AllBlackListings).All(l => l.BuyPrice > l.SellPrice && l.BuyPrice > 0 && l.SellPrice > 0 && l.Stock >= 0 && l.Stock <= l.MaximumStock), "cross-station illicit price or stock escaped bounds");

    private static (bool, string) StockSurvivesSaveLoad()
    {
        Context c = OpenContext("Buffalo Base");
        Commodity commodity = FirstBlack(c);
        c.Dealer.TryBuyCommodity(commodity, 1, new PlayerCredits(100_000), new CargoHold(20), out _);
        int expected = BlackListing(c, commodity).Stock;
        List<SaveMarketStateData> state = c.Dealer.CaptureMarketState();
        Context resumed = OpenContext("Buffalo Base");
        resumed.Dealer.RestoreMarketState(state);
        return Check(BlackListing(resumed, commodity).Stock == expected, "black-market stock did not survive save/load");
    }

    private static (bool, string) StockDoesNotDuplicateAfterLoad()
    {
        Context c = OpenContext("Buffalo Base");
        Commodity commodity = FirstBlack(c);
        c.Dealer.TryBuyCommodity(commodity, 1, new PlayerCredits(100_000), new CargoHold(20), out _);
        List<SaveMarketStateData> state = c.Dealer.CaptureMarketState();
        int expected = BlackListing(c, commodity).Stock;
        Context resumed = OpenContext("Buffalo Base");
        resumed.Dealer.RestoreMarketState(state);
        resumed.Dealer.RestoreMarketState(state);
        return Check(BlackListing(resumed, commodity).Stock == expected, "repeated load duplicated stock");
    }

    private static (bool, string) PricesRecomputeAfterLoad()
    {
        Context c = OpenContext("Buffalo Base");
        Commodity commodity = FirstBlack(c);
        c.Dealer.TryBuyCommodity(commodity, 1, new PlayerCredits(100_000), new CargoHold(20), out _);
        StationMarketListing expected = BlackListing(c, commodity);
        List<SaveMarketStateData> state = c.Dealer.CaptureMarketState();
        string json = JsonSerializer.Serialize(state);
        Context resumed = OpenContext("Buffalo Base");
        resumed.Dealer.RestoreMarketState(state);
        StationMarketListing actual = BlackListing(resumed, commodity);
        bool pricesRecomputed = actual.BuyPrice == expected.BuyPrice && actual.SellPrice == expected.SellPrice;
        bool pricesAreDerived = !json.Contains("\"buy_price\"", StringComparison.OrdinalIgnoreCase) &&
            !json.Contains("\"sell_price\"", StringComparison.OrdinalIgnoreCase);
        return Check(pricesAreDerived && pricesRecomputed,
            $"derived illicit prices did not recompute after load (before {expected.BuyPrice}/{expected.SellPrice}, after {actual.BuyPrice}/{actual.SellPrice})");
    }

    private static (bool, string) ReputationGateSurvivesLoad()
    {
        Context c = OpenContext("Buffalo Base");
        c.Dealer.TryOpenBlackMarket(out _);
        List<SaveMarketStateData> state = c.Dealer.CaptureMarketState();
        Context resumed = OpenContext("Buffalo Base");
        resumed.Dealer.RestoreMarketState(state);
        resumed.Reputation.SetReputation(FactionManager.LibertyRogues, -0.70f);
        return Check(resumed.Dealer.GetCurrentListings().Count == 0 && !resumed.Dealer.CurrentBlackMarketAccess.IsAllowed, "loaded market ignored live reputation gate");
    }

    private static (bool, string) ResetRestoresMarketState()
    {
        Context c = OpenContext("Buffalo Base");
        Commodity commodity = FirstBlack(c);
        int baseline = BlackListing(c, commodity).Stock;
        c.Dealer.TryBuyCommodity(commodity, 1, new PlayerCredits(100_000), new CargoHold(20), out _);
        c.Manager.ResetRuntimeState();
        return Check(BlackListing(c, commodity).Stock == baseline, "reset did not restore initial stock");
    }

    private static (bool, string) BuyTransactionEmitsOneCompletion()
    {
        Context c = OpenContext("Buffalo Base");
        Commodity commodity = FirstBlack(c);
        int completions = 0;
        c.Dealer.TransactionCompleted += _ => completions++;
        bool success = c.Dealer.TryBuyCommodity(commodity, 1, new PlayerCredits(100_000), new CargoHold(20), out _);
        return Check(success && completions == 1, "successful purchase did not emit exactly one completion");
    }

    private static (bool, string) FailedTransactionEmitsNoCompletion()
    {
        Context c = OpenContext("Buffalo Base");
        Commodity commodity = FirstBlack(c);
        int completions = 0;
        c.Dealer.TransactionCompleted += _ => completions++;
        c.Dealer.TryBuyCommodity(commodity, 1, new PlayerCredits(0), new CargoHold(20), out _);
        return Check(completions == 0, "failed transaction emitted a completion");
    }

    private static (bool, string) NoNegativeStock() => Check(new[] { "Buffalo Base", "Rochester Base" }.SelectMany(AllBlackListings).All(l => l.Stock >= 0), "negative stock was observable");
    private static (bool, string) NoNegativeCredits()
    {
        Context c = OpenContext("Buffalo Base");
        Commodity commodity = FirstBlack(c);
        PlayerCredits credits = new(0);
        c.Dealer.TryBuyCommodity(commodity, 1, credits, new CargoHold(20), out _);
        return Check(credits.Credits >= 0, "negative credits were observable");
    }
    private static (bool, string) NoNegativeCargo()
    {
        Context c = OpenContext("Buffalo Base");
        Commodity commodity = FirstBlack(c);
        CargoHold cargo = new(20);
        c.Dealer.TrySellCommodity(commodity, 1, new PlayerCredits(100), cargo, out _);
        return Check(cargo.GetCommodityQuantity(commodity.Name) >= 0 && cargo.UsedCapacity >= 0, "negative cargo was observable");
    }

    private static (bool, string) RogueContactExposesAction()
    {
        BarNpc contact = BarNpc.GenerateBarNpcs().FirstOrDefault(npc => npc.OffersBlackMarket);
        return Check(contact != null && contact.FactionId == FactionManager.LibertyRogues, "Rogue contact did not expose the black-market action");
    }

    private static (bool, string) StationUiOpensBlackMarket()
    {
        Context c = Create("Buffalo Base");
        StationDockUI ui = new(null, null, null, c.Dealer, null, c.Reputation);
        if (!ui.DockAtStation(c.Station)) return Fail("could not dock at Buffalo Base");
        bool opened = ui.TryOpenBlackMarket(out _);
        return Check(opened && ui.IsBlackMarketOpen && ui.CurrentArea == StationArea.Dealer, "station UI did not open black market");
    }

    private static (bool, string) StationUiKeepsLawfulContactLocked()
    {
        Context c = Create("Fort Bush");
        StationDockUI ui = new(null, null, null, c.Dealer, null, c.Reputation);
        if (!ui.DockAtStation(c.Station)) return Fail("could not dock at Fort Bush");
        return Check(!ui.TryOpenBlackMarket(out string message) && message.Contains("unavailable", StringComparison.OrdinalIgnoreCase), $"lawful UI lock response was '{message}'");
    }

    private static (bool, string) OpenAtStanding(float standing, bool expected, string failure)
    {
        Context c = Create("Buffalo Base");
        c.Reputation.SetReputation(FactionManager.LibertyRogues, standing);
        bool actual = c.Dealer.TryOpenBlackMarket(out _);
        return Check(actual == expected, failure);
    }

    private static Context OpenContext(string stationName)
    {
        Context c = Create(stationName);
        c.Dealer.TryOpenBlackMarket(out _);
        return c;
    }

    private static Commodity FirstBlack(Context c) => c.Manager.GetBlackMarketListingsForStation(c.Station).First().Commodity;

    private static StationMarketListing BlackListing(Context c, Commodity commodity) => c.Manager.GetListingForCommodity(c.Station, commodity, MarketSurface.BlackMarket);

    private static List<StationMarketListing> AllBlackListings(string stationName)
    {
        Context c = Create(stationName);
        return c.Manager.GetBlackMarketListingsForStation(c.Station);
    }

    private static bool SameBlackListings(Context a, Context b, Func<StationMarketListing, StationMarketListing, bool> predicate)
    {
        List<StationMarketListing> first = a.Manager.GetBlackMarketListingsForStation(a.Station);
        List<StationMarketListing> second = b.Manager.GetBlackMarketListingsForStation(b.Station);
        return first.Count == second.Count && first.Zip(second, predicate).All(result => result);
    }

    private sealed class Context
    {
        public Context(Station station, CommodityDealer dealer, ReputationManager reputation)
        {
            Station = station;
            Dealer = dealer;
            Reputation = reputation;
        }

        public Station Station { get; }
        public CommodityDealer Dealer { get; }
        public MarketManager Manager => Dealer.MarketManager;
        public ReputationManager Reputation { get; }
    }

    private static Context Create(string stationName)
    {
        Station station = Stations.FirstOrDefault(candidate => NormalizeKey(candidate?.Name) == NormalizeKey(stationName));
        if (station == null) throw new InvalidOperationException($"station fixture '{stationName}' was not found");
        FactionManager factions = new();
        ReputationManager reputation = new(factions);
        CommodityDealer dealer = new();
        dealer.SetReputationManager(reputation);
        dealer.SetDockedStation(station);
        return new Context(station, dealer, reputation);
    }

    private static IReadOnlyList<Station> LoadFixtureStations()
    {
        string directory = Path.Combine("Configuration", "stations");
        if (!Directory.Exists(directory)) return Array.Empty<Station>();
        JsonSerializerOptions options = new() { PropertyNameCaseInsensitive = true };
        List<Station> result = new();
        foreach (string file in Directory.GetFiles(directory, "station_*.json"))
        {
            StationConfig config = JsonSerializer.Deserialize<StationConfig>(File.ReadAllText(file), options);
            if (config != null) result.Add(new Station(config, null));
        }
        return result;
    }

    private static string NormalizeKey(string value) => string.IsNullOrWhiteSpace(value)
        ? string.Empty
        : new string(value.Trim().ToLowerInvariant().Where(char.IsLetterOrDigit).ToArray());

    private static (bool, string) Check(bool condition, string failure) => condition ? Pass() : Fail(failure);
    private static (bool, string) Pass() => (true, string.Empty);
    private static (bool, string) Fail(string failure) => (false, failure);
}
