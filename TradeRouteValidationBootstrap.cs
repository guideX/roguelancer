using Microsoft.Xna.Framework;
using Roguelancer.Configuration;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Roguelancer;

/// <summary>
/// Resolves and prepares the explicit developer-only Fort Bush to Riverside
/// validation scenario. It owns no production route or market authority; it
/// only resets in-memory state and supplies the remembered remote observation
/// needed to exercise the real player-facing opportunity flow.
/// </summary>
public sealed class TradeRouteValidationBootstrap
{
    public const string Flag = "--dev-trade-route";
    public const string ScenarioValue = "fort-bush-riverside";
    public const string ValidationObservationSource = "DeveloperTradeRouteValidation";
    public const int ValidationCredits = 100_000;
    public const float ValidationTravelMultiplier = 5f;
    public const int MinimumSuggestedUnits = 20;

    private readonly ConfigurationManager _configuration;

    public TradeRouteValidationBootstrap(ConfigurationManager configuration)
    {
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
    }

    public static bool IsRequested(IEnumerable<string> args)
    {
        return args?.Any(argument =>
            string.Equals(argument, Flag, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(argument, $"{Flag}={ScenarioValue}", StringComparison.OrdinalIgnoreCase)) == true;
    }

    public static string GetValidationSavePath()
    {
        string root = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrWhiteSpace(root)) root = AppContext.BaseDirectory;
        return Path.Combine(root, "Roguelancer", "Saves", "trade-route-validation.json");
    }

    public bool TryPrepare(
        CommodityDealer dealer,
        MarketIntelligence intelligence,
        MissionManager missions,
        PlayerCredits credits,
        Ship playerShip,
        IReadOnlyList<Station> loadedStations,
        out TradeRouteValidationIdentity identity,
        out string failureReason)
    {
        identity = null;
        failureReason = string.Empty;

        if (!TryResolve(dealer?.MarketManager, loadedStations, out identity, out failureReason))
        {
            return false;
        }

        if (dealer == null || intelligence == null || credits == null || playerShip?.CargoHold == null)
        {
            failureReason = "market, intelligence, credits, ship, or cargo authority was unavailable";
            return false;
        }

        if (playerShip.CargoHold.MaxCapacity < identity.FoodRations.VolumePerUnit * MinimumSuggestedUnits)
        {
            failureReason = $"existing ship '{playerShip.DisplayName}' has only {playerShip.CargoHold.MaxCapacity} cargo capacity; at least {MinimumSuggestedUnits} Food Rations units are required";
            return false;
        }

        // These are all runtime objects. No configuration file or normal save
        // is written, and the caller intentionally skips ordinary auto-load.
        dealer.MarketManager.ResetRuntimeState();
        intelligence.Clear();
        missions?.ClearState();
        playerShip.CargoHold.Clear();
        credits.SetCredits(ValidationCredits);

        StationMarketListing source = dealer.MarketManager.GetListingForCommodity(identity.FortBush, identity.FoodRations);
        StationMarketListing destination = dealer.MarketManager.GetListingForCommodity(identity.Riverside, identity.FoodRations);
        if (!IsBaselineListing(source) || !IsBaselineListing(destination))
        {
            failureReason = "resolved production market did not expose valid baseline Food Rations listings";
            return false;
        }

        identity.SourceBaseline = new TradeRouteValidationMarketSnapshot(source);
        identity.DestinationBaseline = new TradeRouteValidationMarketSnapshot(destination);

        // Restore through the same serializable intelligence model used by
        // saves. The quote is read from the authoritative runtime Riverside
        // market immediately above; it is not a hand-written price.
        intelligence.RestoreState(new[]
        {
            new SaveMarketIntelligenceData
            {
                StationId = identity.RiversideId,
                StationName = identity.Riverside.Name,
                SystemIndex = identity.Riverside.Config.SystemIndex,
                StationPosition = SaveVector3Data.From(identity.Riverside.Position),
                CommodityId = identity.FoodRations.Id,
                Stock = destination.Stock,
                BuyPrice = destination.BuyPrice,
                SellPrice = destination.SellPrice,
                BaselineStock = destination.BaselineStock,
                DemandLevel = destination.DemandLevel,
                MarketCondition = destination.MarketCondition,
                ObservedAtMilliseconds = dealer.MarketManager.ElapsedMilliseconds,
                Source = ValidationObservationSource
            }
        });

        // RestoreState replaces the knowledge collection, so observe the
        // current source after the developer-only remote seed. This remains a
        // live source observation and keeps ordinary discovery semantics
        // untouched outside this explicit mode.
        if (!intelligence.ObserveStation(identity.FortBush, "DeveloperTradeRouteCurrent") ||
            !intelligence.TryGetObservation(identity.FortBushId, identity.FoodRations.Id, out _))
        {
            failureReason = "could not create the current Fort Bush observation from the live market";
            return false;
        }

        playerShip.ValidationTravelMultiplier = ValidationTravelMultiplier;
        return true;
    }

    public bool TryResolve(
        MarketManager marketManager,
        IReadOnlyList<Station> loadedStations,
        out TradeRouteValidationIdentity identity,
        out string failureReason)
    {
        identity = null;
        failureReason = string.Empty;
        if (marketManager == null)
        {
            failureReason = "market manager was unavailable";
            return false;
        }

        int newYork = ResolveSystemIndex("new_york", 1, out SystemConfig newYorkConfig, out failureReason);
        if (newYork <= 0) return false;
        int texas = ResolveSystemIndex("texas", 4, out SystemConfig texasConfig, out failureReason);
        if (texas <= 0) return false;
        int california = ResolveSystemIndex("california", 2, out SystemConfig californiaConfig, out failureReason);
        if (california <= 0) return false;

        Station fortBush = loadedStations?.FirstOrDefault(station =>
            station != null && string.Equals(station.Name, "Fort Bush", StringComparison.OrdinalIgnoreCase) &&
            station.Config?.SystemIndex == newYork);
        Station riverside = loadedStations?.FirstOrDefault(station =>
            station != null && string.Equals(station.Name, "Riverside Station", StringComparison.OrdinalIgnoreCase) &&
            station.Config?.SystemIndex == california);

        // Normal startup loads only the current system's station models. Keep Riverside as a
        // production-config identity until California is entered and its runtime station is
        // loaded; this is not a synthetic station or topology.
        if (riverside == null)
        {
            StationConfig riversideConfig = _configuration.Stations.FirstOrDefault(station =>
                station?.SystemIndex == california &&
                string.Equals(station.Description, "Riverside Station", StringComparison.OrdinalIgnoreCase));
            if (riversideConfig != null)
            {
                riverside = new Station(riversideConfig, null);
            }
        }

        if (fortBush == null || riverside == null)
        {
            failureReason = "Fort Bush/New York or Riverside Station/California did not resolve from production configuration";
            return false;
        }

        string fortBushId = marketManager.GetStationId(fortBush);
        string riversideId = marketManager.GetStationId(riverside);
        Commodity food = marketManager.ResolveCommodity("food-rations");
        if (string.IsNullOrWhiteSpace(fortBushId) || string.IsNullOrWhiteSpace(riversideId) ||
            !marketManager.IsKnownStationId(fortBushId) || !marketManager.IsKnownStationId(riversideId) ||
            food == null || !string.Equals(food.Id, "food-rations", StringComparison.OrdinalIgnoreCase) || food.IsMissionCargo)
        {
            failureReason = "real Fort Bush, Riverside, or Food Rations market identity could not be resolved";
            return false;
        }

        JumpHoleConfig first = FindTransition(newYork, texas, "Jump Hole to Texas");
        JumpHoleConfig second = FindTransition(texas, california, "California Jump Hole");
        if (first == null || second == null)
        {
            failureReason = "production jump-hole identities for New York -> Texas -> California are missing or point at the wrong systems";
            return false;
        }

        if (!_configuration.JumpHoles.Any(candidate => candidate?.SystemIndex == first.TargetSystemIndex &&
            string.Equals(candidate.Name, first.TargetJumpHoleName, StringComparison.OrdinalIgnoreCase)) ||
            !_configuration.JumpHoles.Any(candidate => candidate?.SystemIndex == second.TargetSystemIndex &&
            string.Equals(candidate.Name, second.TargetJumpHoleName, StringComparison.OrdinalIgnoreCase)))
        {
            failureReason = "selected jump-hole arrival identities do not resolve in production configuration";
            return false;
        }

        if (!marketManager.HasMarketConfigForStation(fortBush) || !marketManager.HasMarketConfigForStation(riverside))
        {
            failureReason = "Fort Bush or Riverside does not have a production market config";
            return false;
        }

        identity = new TradeRouteValidationIdentity(
            newYorkConfig,
            texasConfig,
            californiaConfig,
            fortBush,
            riverside,
            fortBushId,
            riversideId,
            food,
            first,
            second);
        return true;
    }

    private int ResolveSystemIndex(string canonicalId, int expectedIndex, out SystemConfig config, out string failureReason)
    {
        config = null;
        failureReason = string.Empty;
        if (!_configuration.TryGetSystemIndex(canonicalId, out int index) || index != expectedIndex ||
            (config = _configuration.GetSystem(index)) == null)
        {
            failureReason = $"production system '{canonicalId}' did not resolve to expected system {expectedIndex}";
            return 0;
        }

        return index;
    }

    private JumpHoleConfig FindTransition(int sourceSystem, int targetSystem, string name)
    {
        return _configuration.JumpHoles.FirstOrDefault(candidate =>
            candidate != null && candidate.SystemIndex == sourceSystem && candidate.TargetSystemIndex == targetSystem &&
            string.Equals(candidate.Name, name, StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsBaselineListing(StationMarketListing listing)
    {
        return listing != null && listing.Commodity != null && listing.IsAvailable && listing.BuyPrice > 0 &&
            listing.SellPrice > 0 && listing.Stock >= 0 && listing.BaselineStock > 0 &&
            listing.Stock == listing.BaselineStock && listing.BuyPrice == listing.BaseBuyPrice &&
            listing.SellPrice == listing.BaseSellPrice;
    }
}

public sealed class TradeRouteValidationIdentity
{
    internal TradeRouteValidationIdentity(
        SystemConfig newYork,
        SystemConfig texas,
        SystemConfig california,
        Station fortBush,
        Station riverside,
        string fortBushId,
        string riversideId,
        Commodity foodRations,
        JumpHoleConfig firstTransition,
        JumpHoleConfig secondTransition)
    {
        NewYork = newYork;
        Texas = texas;
        California = california;
        FortBush = fortBush;
        Riverside = riverside;
        FortBushId = fortBushId;
        RiversideId = riversideId;
        FoodRations = foodRations;
        FirstTransition = firstTransition;
        SecondTransition = secondTransition;
    }

    public SystemConfig NewYork { get; }
    public SystemConfig Texas { get; }
    public SystemConfig California { get; }
    public Station FortBush { get; }
    public Station Riverside { get; }
    public string FortBushId { get; }
    public string RiversideId { get; }
    public Commodity FoodRations { get; }
    public JumpHoleConfig FirstTransition { get; }
    public JumpHoleConfig SecondTransition { get; }
    public TradeRouteValidationMarketSnapshot SourceBaseline { get; internal set; }
    public TradeRouteValidationMarketSnapshot DestinationBaseline { get; internal set; }
}

public sealed class TradeRouteValidationMarketSnapshot
{
    internal TradeRouteValidationMarketSnapshot(StationMarketListing listing)
    {
        Stock = listing.Stock;
        BuyPrice = listing.BuyPrice;
        SellPrice = listing.SellPrice;
        BaselineStock = listing.BaselineStock;
        DemandLevel = listing.DemandLevel;
        MarketCondition = listing.MarketCondition;
    }

    public int Stock { get; }
    public int BuyPrice { get; }
    public int SellPrice { get; }
    public int BaselineStock { get; }
    public int DemandLevel { get; }
    public string MarketCondition { get; }
}

/// <summary>Diagnostics-only state machine for the genuine interactive proof.</summary>
public sealed class TradeRouteValidationDiagnostics
{
    private readonly TradeRouteValidationIdentity _identity;

    public TradeRouteValidationDiagnostics(TradeRouteValidationIdentity identity)
    {
        _identity = identity ?? throw new ArgumentNullException(nameof(identity));
    }

    public bool PlanCreated { get; private set; }
    public bool SourcePurchaseObserved { get; private set; }
    public bool NewYorkToTexasObserved { get; private set; }
    public bool TexasToCaliforniaObserved { get; private set; }
    public bool RiversideDockObserved { get; private set; }
    public bool DestinationSaleObserved { get; private set; }
    public bool PlanCompleted { get; private set; }
    public bool PassEmitted { get; private set; }
    public int PurchasedQuantity { get; private set; }
    public int SoldQuantity { get; private set; }
    public int SourceUnitPrice { get; private set; }
    public int DestinationUnitPrice { get; private set; }
    public int SourceCreditsBefore { get; private set; }
    public int SourceCreditsAfter { get; private set; }
    public int DestinationCreditsBefore { get; private set; }
    public int DestinationCreditsAfter { get; private set; }
    public int SourceStockAfter { get; private set; }
    public int DestinationStockAfter { get; private set; }
    public int SourceBuyPriceAfter { get; private set; }
    public int DestinationSellPriceAfter { get; private set; }
    public string LastTransitionId { get; private set; } = string.Empty;

    public void RecordPlanCreated(TradePlan plan)
    {
        if (plan == null) return;
        if (string.Equals(plan.SourceStationId, _identity.FortBushId, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(plan.DestinationStationId, _identity.RiversideId, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(plan.CommodityId, _identity.FoodRations.Id, StringComparison.OrdinalIgnoreCase))
        {
            PlanCreated = true;
            Console.WriteLine($"[TRADE VALIDATION] Trade Plan created: {plan.CommodityName} {plan.SourceStationName} -> {plan.DestinationStationName}, suggested={plan.SuggestedQuantity}, hops={plan.RouteHops}");
        }

        if (plan.IsComplete) PlanCompleted = true;
    }

    public void RecordTransaction(CommodityTransaction transaction, CommodityDealer dealer, PlayerCredits credits)
    {
        if (transaction == null || dealer == null || credits == null || transaction.Quantity <= 0 ||
            !string.Equals(transaction.CommodityId, _identity.FoodRations.Id, StringComparison.OrdinalIgnoreCase)) return;

        if (transaction.IsPurchase && string.Equals(transaction.StationId, _identity.FortBushId, StringComparison.OrdinalIgnoreCase))
        {
            SourcePurchaseObserved = true;
            PurchasedQuantity += transaction.Quantity;
            SourceUnitPrice = transaction.UnitPrice;
            int total = checked(transaction.UnitPrice * transaction.Quantity);
            SourceCreditsAfter = credits.Credits;
            SourceCreditsBefore = SourceCreditsAfter + total;
            StationMarketListing listing = dealer.MarketManager.GetListingForCommodity(_identity.FortBush, _identity.FoodRations);
            SourceStockAfter = listing?.Stock ?? 0;
            SourceBuyPriceAfter = listing?.BuyPrice ?? 0;
            Console.WriteLine($"[TRADE VALIDATION] Purchased {transaction.Quantity} Food Rations at Fort Bush");
            Console.WriteLine($"[TRADE VALIDATION] Credits: {SourceCreditsBefore} -> {SourceCreditsAfter}");
            Console.WriteLine($"[TRADE VALIDATION] Fort Bush stock/price: {SourceStockAfter} / {SourceBuyPriceAfter}");
        }
        else if (!transaction.IsPurchase && string.Equals(transaction.StationId, _identity.RiversideId, StringComparison.OrdinalIgnoreCase))
        {
            DestinationSaleObserved = true;
            SoldQuantity += transaction.Quantity;
            DestinationUnitPrice = transaction.UnitPrice;
            int total = checked(transaction.UnitPrice * transaction.Quantity);
            DestinationCreditsAfter = credits.Credits;
            DestinationCreditsBefore = DestinationCreditsAfter - total;
            StationMarketListing listing = dealer.MarketManager.GetListingForCommodity(_identity.Riverside, _identity.FoodRations);
            DestinationStockAfter = listing?.Stock ?? 0;
            DestinationSellPriceAfter = listing?.SellPrice ?? 0;
            Console.WriteLine($"[TRADE VALIDATION] Sold {transaction.Quantity} Food Rations at Riverside Station");
            Console.WriteLine($"[TRADE VALIDATION] Credits: {DestinationCreditsBefore} -> {DestinationCreditsAfter}");
            Console.WriteLine($"[TRADE VALIDATION] Riverside stock/price: {DestinationStockAfter} / {DestinationSellPriceAfter}");
        }
    }

    public void RecordSystemChange(int oldSystem, int newSystem, string arrivalJumpHoleName)
    {
        if (!NewYorkToTexasObserved && oldSystem == _identity.NewYork.SystemIndex && newSystem == _identity.Texas.SystemIndex)
        {
            NewYorkToTexasObserved = true;
            LastTransitionId = _identity.FirstTransition.TransitionId;
            Console.WriteLine($"[TRADE VALIDATION] NY->TX transition observed: {LastTransitionId}; arrival={arrivalJumpHoleName}");
        }
        else if (NewYorkToTexasObserved && !TexasToCaliforniaObserved && oldSystem == _identity.Texas.SystemIndex && newSystem == _identity.California.SystemIndex)
        {
            TexasToCaliforniaObserved = true;
            LastTransitionId = _identity.SecondTransition.TransitionId;
            Console.WriteLine($"[TRADE VALIDATION] TX->CA transition observed: {LastTransitionId}; arrival={arrivalJumpHoleName}");
        }
    }

    public void RecordDocking(Station station)
    {
        if (station == null) return;
        if (string.Equals(station.Name, _identity.Riverside.Name, StringComparison.OrdinalIgnoreCase) &&
            station.Config?.SystemIndex == _identity.California.SystemIndex)
        {
            RiversideDockObserved = true;
            Console.WriteLine("[TRADE VALIDATION] Riverside Station dock observed through normal station docking");
        }
    }

    public void RecordPlanChanged(TradePlan plan)
    {
        if (plan?.IsComplete == true) PlanCompleted = true;
    }

    public bool TryEmitPass()
    {
        if (PassEmitted || !PlanCreated || !SourcePurchaseObserved || !NewYorkToTexasObserved ||
            !TexasToCaliforniaObserved || !RiversideDockObserved || !DestinationSaleObserved || !PlanCompleted)
        {
            return false;
        }

        PassEmitted = true;
        Console.WriteLine("[TRADE VALIDATION COMPLETE]");
        Console.WriteLine("Route: Fort Bush -> Texas -> Riverside Station");
        Console.WriteLine($"Food Rations: Purchased {PurchasedQuantity} at {SourceUnitPrice} CR, sold {SoldQuantity} at {DestinationUnitPrice} CR");
        Console.WriteLine($"Systems crossed: 2 | Realized trade result: {DestinationCreditsAfter - DestinationCreditsBefore - (SourceCreditsBefore - SourceCreditsAfter):N0} CR");
        Console.WriteLine("[TRADE VALIDATION] PASS");
        return true;
    }

    public IReadOnlyList<string> GetMissingMarkers()
    {
        List<string> missing = new();
        if (!SourcePurchaseObserved) missing.Add("SOURCE PURCHASE NOT OBSERVED");
        if (!NewYorkToTexasObserved) missing.Add("NY->TX TRANSITION NOT OBSERVED");
        if (!TexasToCaliforniaObserved) missing.Add("TX->CA TRANSITION NOT OBSERVED");
        if (!RiversideDockObserved) missing.Add("RIVERSIDE DOCK NOT OBSERVED");
        if (!DestinationSaleObserved) missing.Add("DESTINATION SALE NOT OBSERVED");
        return missing;
    }
}
