using Microsoft.Xna.Framework;
using Roguelancer.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Roguelancer;

internal sealed class TradeRouteValidationBootstrapSmokeTest
{
    private readonly ConfigurationManager _configuration = new();

    public TradeRouteValidationBootstrapSmokeTest()
    {
        _configuration.LoadAll();
    }

    public (int Passed, int Failed) Run()
    {
        int passed = 0;
        int failed = 0;
        Context normal = CreateContext(false);
        Context validation = CreateContext(true);
        TradeRouteValidationBootstrap bootstrap = new(_configuration);

        RunCase("flag absent leaves normal startup unrequested", () => !TradeRouteValidationBootstrap.IsRequested(Array.Empty<string>()), ref passed, ref failed);
        RunCase("flag is recognized", () => TradeRouteValidationBootstrap.IsRequested(new[] { "--dev-trade-route" }), ref passed, ref failed);
        RunCase("named scenario flag is recognized", () => TradeRouteValidationBootstrap.IsRequested(new[] { "--dev-trade-route=fort-bush-riverside" }), ref passed, ref failed);
        RunCase("unrelated smoke flag is not overloaded", () => !TradeRouteValidationBootstrap.IsRequested(new[] { "--production-multi-system-trade-route-smoke" }), ref passed, ref failed);
        RunCase("validation save is isolated", () => !string.Equals(new SaveGameManager().SavePath, TradeRouteValidationBootstrap.GetValidationSavePath(), StringComparison.OrdinalIgnoreCase), ref passed, ref failed);
        RunCase("New York resolves", () => validation.Identity.NewYork.CanonicalId == "new_york" && validation.Identity.NewYork.SystemIndex == 1, ref passed, ref failed);
        RunCase("Texas resolves", () => validation.Identity.Texas.CanonicalId == "texas" && validation.Identity.Texas.SystemIndex == 4, ref passed, ref failed);
        RunCase("California resolves", () => validation.Identity.California.CanonicalId == "california" && validation.Identity.California.SystemIndex == 2, ref passed, ref failed);
        RunCase("Fort Bush resolves in New York", () => validation.Identity.FortBush.Name == "Fort Bush" && validation.Identity.FortBush.Config.SystemIndex == 1, ref passed, ref failed);
        RunCase("Riverside resolves in California", () => validation.Identity.Riverside.Name == "Riverside Station" && validation.Identity.Riverside.Config.SystemIndex == 2, ref passed, ref failed);
        RunCase("Food Rations resolves", () => validation.Identity.FoodRations.Id == "food-rations" && !validation.Identity.FoodRations.IsMissionCargo, ref passed, ref failed);
        RunCase("first production transition resolves", () => validation.Identity.FirstTransition.TransitionId == "1:Jump Hole to Texas" && validation.Identity.FirstTransition.TargetSystemIndex == 4, ref passed, ref failed);
        RunCase("second production transition resolves", () => validation.Identity.SecondTransition.TransitionId == "4:California Jump Hole" && validation.Identity.SecondTransition.TargetSystemIndex == 2, ref passed, ref failed);
        RunCase("first arrival identity resolves", () => _configuration.JumpHoles.Any(jump => jump.SystemIndex == 4 && jump.Name == validation.Identity.FirstTransition.TargetJumpHoleName), ref passed, ref failed);
        RunCase("second arrival identity resolves", () => _configuration.JumpHoles.Any(jump => jump.SystemIndex == 2 && jump.Name == validation.Identity.SecondTransition.TargetJumpHoleName), ref passed, ref failed);
        RunCase("Fort Bush market id is real", () => validation.Identity.FortBushId == "fort_bush", ref passed, ref failed);
        RunCase("Riverside market id is real", () => validation.Identity.RiversideId == "riverside_station", ref passed, ref failed);
        RunCase("source has production market", () => validation.Dealer.MarketManager.HasMarketConfigForStation(validation.Identity.FortBush), ref passed, ref failed);
        RunCase("destination has production market", () => validation.Dealer.MarketManager.HasMarketConfigForStation(validation.Identity.Riverside), ref passed, ref failed);
        RunCase("source baseline stock is 450", () => validation.Identity.SourceBaseline.Stock == 450, ref passed, ref failed);
        RunCase("source baseline buy is 85", () => validation.Identity.SourceBaseline.BuyPrice == 85, ref passed, ref failed);
        RunCase("destination baseline stock is 220", () => validation.Identity.DestinationBaseline.Stock == 220, ref passed, ref failed);
        RunCase("destination baseline sell is 125", () => validation.Identity.DestinationBaseline.SellPrice == 125, ref passed, ref failed);
        RunCase("baseline spread is positive", () => validation.Identity.DestinationBaseline.SellPrice > validation.Identity.SourceBaseline.BuyPrice, ref passed, ref failed);
        RunCase("bootstrap was applied", () => validation.BootstrapApplied, ref passed, ref failed);
        RunCase("validation starts in New York", () => validation.CurrentSystem == 1, ref passed, ref failed);
        RunCase("dealer is bound to Fort Bush", () => validation.Dealer.CurrentStation == validation.Identity.FortBush, ref passed, ref failed);
        RunCase("Fort Bush observation is current", () => validation.Intelligence.CurrentStation == validation.Identity.FortBush && validation.Intelligence.TryGetObservation("fort_bush", "food-rations", out MarketObservation source) && source.GetAgeBand(validation.Dealer.MarketManager.ElapsedMilliseconds) == MarketObservationAgeBand.Current, ref passed, ref failed);
        RunCase("Riverside developer observation exists", () => validation.Intelligence.TryGetObservation("riverside_station", "food-rations", out MarketObservation destination) && destination.Source == TradeRouteValidationBootstrap.ValidationObservationSource, ref passed, ref failed);
        RunCase("source observation uses live baseline", () => validation.Intelligence.TryGetObservation("fort_bush", "food-rations", out MarketObservation source) && source.BuyPrice == 85 && source.Stock == 450, ref passed, ref failed);
        RunCase("remote observation uses runtime baseline", () => validation.Intelligence.TryGetObservation("riverside_station", "food-rations", out MarketObservation destination) && destination.SellPrice == validation.Identity.DestinationBaseline.SellPrice && destination.Stock == validation.Identity.DestinationBaseline.Stock, ref passed, ref failed);
        RunCase("credits are deterministic", () => validation.Credits.Credits == TradeRouteValidationBootstrap.ValidationCredits, ref passed, ref failed);
        RunCase("cargo starts empty", () => validation.Ship.CargoHold.UsedCapacity == 0 && validation.Ship.CargoHold.GetAllCommodities().Count == 0, ref passed, ref failed);
        RunCase("mission reservations start empty", () => validation.Ship.CargoHold.GetMissionCargoReservations().Count == 0 && validation.Missions.ActiveMissions.Count == 0, ref passed, ref failed);
        RunCase("existing ship is used", () => validation.Ship.DisplayName == "Scimitar", ref passed, ref failed);
        RunCase("ship capacity is 50", () => validation.Ship.CargoHold.MaxCapacity == 50, ref passed, ref failed);
        RunCase("validation travel multiplier is 5x", () => validation.Ship.ValidationTravelMultiplier == 5f, ref passed, ref failed);
        RunCase("bootstrap preserves source stock", () => validation.Identity.SourceBaseline.Stock == validation.Dealer.MarketManager.GetListingForCommodity(validation.Identity.FortBush, validation.Identity.FoodRations).Stock, ref passed, ref failed);
        RunCase("bootstrap preserves destination stock", () => validation.Identity.DestinationBaseline.Stock == validation.Dealer.MarketManager.GetListingForCommodity(validation.Identity.Riverside, validation.Identity.FoodRations).Stock, ref passed, ref failed);
        RunCase("bootstrap does not buy cargo", () => validation.Ship.CargoHold.GetCommodityQuantity("Food Rations") == 0, ref passed, ref failed);
        RunCase("bootstrap grants no mission reward", () => validation.Credits.Credits == TradeRouteValidationBootstrap.ValidationCredits && validation.Missions.CompletedMissions.Count == 0, ref passed, ref failed);
        MarketOpportunity opportunity = validation.Missions.GetKnownMarketOpportunities(8).FirstOrDefault(candidate => candidate.Type == MarketOpportunityType.TradeRoute && candidate.OriginStationId == "fort_bush" && candidate.DestinationStationId == "riverside_station");
        RunCase("normal opportunity generator sees route", () => opportunity != null, ref passed, ref failed);
        RunCase("opportunity commodity is Food Rations", () => opportunity?.CommodityId == "food-rations", ref passed, ref failed);
        RunCase("opportunity source is Fort Bush", () => opportunity?.OriginStationId == "fort_bush", ref passed, ref failed);
        RunCase("opportunity destination is Riverside", () => opportunity?.DestinationStationId == "riverside_station", ref passed, ref failed);
        RunCase("opportunity spread is positive", () => opportunity?.CurrentSpread > 0, ref passed, ref failed);
        RunCase("opportunity route has two hops", () => opportunity?.RouteHops == 2, ref passed, ref failed);
        RunCase("Trade Plan can be created", () => opportunity != null && validation.Plans.TryCreatePlan(opportunity, out _), ref passed, ref failed);
        RunCase("Trade Plan source is Fort Bush", () => validation.Plans.ActivePlan?.SourceStationId == "fort_bush", ref passed, ref failed);
        RunCase("Trade Plan destination is Riverside", () => validation.Plans.ActivePlan?.DestinationStationId == "riverside_station", ref passed, ref failed);
        RunCase("Trade Plan route has two jumps", () => validation.Plans.ActivePlan?.RouteHops == 2, ref passed, ref failed);
        RunCase("source plot waits for cargo purchase", () => !validation.Plans.TryPlanNavigation(1, out _, out _) && validation.Plans.NavigationState == null, ref passed, ref failed);
        RunCase("source plot does not invent a jump target", () => validation.Plans.NavigationState?.NextTransition == null, ref passed, ref failed);
        RunCase("source plot does not consume route hops", () => validation.Plans.ActivePlan?.RouteHops == 2, ref passed, ref failed);
        int sourceStockBeforePurchase = validation.Dealer.MarketManager.GetListingForCommodity(validation.Identity.FortBush, validation.Identity.FoodRations).Stock;
        RunCase("normal CommodityDealer purchase succeeds", () => validation.Dealer.TryBuyCommodity(validation.Identity.FoodRations, 20, validation.Credits, validation.Ship.CargoHold, out _), ref passed, ref failed);
        RunCase("source stock decreases", () => validation.Dealer.MarketManager.GetListingForCommodity(validation.Identity.FortBush, validation.Identity.FoodRations).Stock == sourceStockBeforePurchase - 20, ref passed, ref failed);
        RunCase("source buy price reacts", () => validation.Dealer.MarketManager.GetListingForCommodity(validation.Identity.FortBush, validation.Identity.FoodRations).BuyPrice > 85, ref passed, ref failed);
        RunCase("cargo remains ordinary", () => validation.Ship.CargoHold.GetMissionReservedQuantity("Food Rations") == 0 && validation.Ship.CargoHold.GetCommodityQuantity("Food Rations") == 20, ref passed, ref failed);
        RunCase("purchase advances Trade Plan", () => validation.Plans.ActivePlan?.Stage == TradePlanStage.GoToDestination && validation.Plans.ActivePlan.PurchasedQuantity == 20, ref passed, ref failed);
        RunCase("post-purchase navigation still targets Texas", () => validation.Plans.TryPlanNavigation(1, out TradePlanNavigationState postPurchaseState, out _) && postPurchaseState.NextTransition?.DestinationSystemIndex == 4, ref passed, ref failed);
        RunCase("Texas continuation targets California", () => validation.Plans.TryPlanNavigation(4, out TradePlanNavigationState texasState, out _) && texasState.NextTransition?.TransitionName == "California Jump Hole" && texasState.RemainingHopCount == 1, ref passed, ref failed);
        RunCase("Texas traversal does not reveal a market", () => validation.Intelligence.KnownStations.Count == 2, ref passed, ref failed);
        validation.Validation.RecordSystemChange(1, 4, "Jump Hole to New York");
        RunCase("NY to Texas diagnostic event is observed", () => validation.Validation.NewYorkToTexasObserved && validation.Validation.LastTransitionId == "1:Jump Hole to Texas", ref passed, ref failed);
        validation.Validation.RecordSystemChange(4, 2, "Texas Jump Hole");
        RunCase("Texas to California diagnostic event is observed", () => validation.Validation.TexasToCaliforniaObserved && validation.Validation.LastTransitionId == "4:California Jump Hole", ref passed, ref failed);
        RunCase("California resolves local destination", () => validation.Plans.TryPlanNavigation(2, out TradePlanNavigationState californiaState, out _) && californiaState.Status == TradePlanRouteStatus.LocalStation && californiaState.FinalStationId == "riverside_station", ref passed, ref failed);
        validation.Dealer.SetDockedStation(validation.Identity.Riverside);
        validation.Plans.NotifyDocked(validation.Identity.Riverside, out _);
        validation.Validation.RecordDocking(validation.Identity.Riverside);
        RunCase("Riverside docking refreshes current observation", () => validation.Intelligence.CurrentStation == validation.Identity.Riverside && validation.Intelligence.TryGetObservation("riverside_station", "food-rations", out MarketObservation current) && current.Source == "TradePlanArrival", ref passed, ref failed);
        RunCase("Riverside dock diagnostic is observed", () => validation.Validation.RiversideDockObserved, ref passed, ref failed);
        int destinationStockBeforeSale = validation.Dealer.MarketManager.GetListingForCommodity(validation.Identity.Riverside, validation.Identity.FoodRations).Stock;
        RunCase("normal destination sale succeeds", () => validation.Dealer.TrySellCommodity(validation.Identity.FoodRations, 20, validation.Credits, validation.Ship.CargoHold, out _), ref passed, ref failed);
        RunCase("destination stock increases", () => validation.Dealer.MarketManager.GetListingForCommodity(validation.Identity.Riverside, validation.Identity.FoodRations).Stock == destinationStockBeforeSale + 20, ref passed, ref failed);
        RunCase("destination sell price reacts", () => validation.Dealer.MarketManager.GetListingForCommodity(validation.Identity.Riverside, validation.Identity.FoodRations).SellPrice < validation.Identity.DestinationBaseline.SellPrice, ref passed, ref failed);
        RunCase("Trade Plan completes", () => validation.Plans.ActivePlan == null && validation.Plans.LastCompletedPlan?.Stage == TradePlanStage.Complete, ref passed, ref failed);
        RunCase("destination sale grants no mission reward", () => validation.Missions.CompletedMissions.Count == 0 && validation.Missions.ActiveMissions.Count == 0, ref passed, ref failed);
        Context early = CreateContext(true);
        RunCase("PASS is blocked before required lifecycle", () => !early.Validation.TryEmitPass(), ref passed, ref failed);
        RunCase("validation lifecycle recognizes PASS", () => validation.Validation.PassEmitted, ref passed, ref failed);
        Context repeat = CreateContext(true);
        RunCase("repeated bootstrap resets credits", () => repeat.Credits.Credits == TradeRouteValidationBootstrap.ValidationCredits, ref passed, ref failed);
        RunCase("repeated bootstrap resets cargo", () => repeat.Ship.CargoHold.UsedCapacity == 0, ref passed, ref failed);
        RunCase("repeated bootstrap resets source market", () => repeat.Identity.SourceBaseline.Stock == repeat.Dealer.MarketManager.GetListingForCommodity(repeat.Identity.FortBush, repeat.Identity.FoodRations).Stock, ref passed, ref failed);
        RunCase("normal context has no Riverside knowledge", () => !normal.Intelligence.IsStationKnown("riverside_station"), ref passed, ref failed);
        RunCase("normal context does not force Fort Bush", () => normal.Dealer.CurrentStation == null && normal.CurrentSystem == 1, ref passed, ref failed);
        RunCase("normal ship has no validation multiplier", () => normal.Ship.ValidationTravelMultiplier == 1f, ref passed, ref failed);
        TradeRouteValidationBootstrap invalidBootstrap = new(new ConfigurationManager());
        RunCase("invalid production identity fails clearly", () => !invalidBootstrap.TryResolve(normal.Dealer.MarketManager, Array.Empty<Station>(), out _, out string failure) && !string.IsNullOrWhiteSpace(failure), ref passed, ref failed);

        Console.WriteLine($"[TRADE ROUTE VALIDATION BOOTSTRAP SMOKE] RESULT: {passed} passed, {failed} failed");
        return (passed, failed);
    }

    private Context CreateContext(bool validationMode)
    {
        CommodityDealer dealer = new();
        MarketIntelligence intelligence = new(dealer.MarketManager);
        dealer.SetMarketIntelligence(intelligence);
        PlayerCredits credits = new(3_000);
        Ship ship = new(Vector3.Zero);
        ShipDefinition.CreateScimitar().ApplyToShip(ship);
        MissionManager missions = new(credits, null, null, dealer.MarketManager, ship.CargoHold, intelligence);
        missions.SetRouteAuthority(new MarketRouteAuthority(_configuration.JumpHoles));
        TradePlanManager plans = new(dealer.MarketManager, intelligence, new MarketRouteAuthority(_configuration.JumpHoles), ship.CargoHold, credits);
        IReadOnlyList<Station> stations = _configuration.Stations.Select(config => new Station(config, null)).ToList();
        TradeRouteValidationIdentity identity = null;
        TradeRouteValidationDiagnostics validation = null;
        bool bootstrapApplied = false;
        int currentSystem = 1;
        if (validationMode)
        {
            TradeRouteValidationBootstrap bootstrap = new(_configuration);
            bootstrapApplied = bootstrap.TryPrepare(dealer, intelligence, missions, credits, ship, stations, out identity, out string failure);
            if (!bootstrapApplied) throw new InvalidOperationException(failure);
            dealer.SetDockedStation(identity.FortBush);
            validation = new TradeRouteValidationDiagnostics(identity);
            dealer.TransactionCompleted += transaction =>
            {
                validation.RecordTransaction(transaction, dealer, credits);
                plans.ObserveTransaction(transaction, out _);
            };
            plans.PlanChanged += plan =>
            {
                validation.RecordPlanCreated(plan);
                validation.RecordPlanChanged(plan);
                validation.TryEmitPass();
            };
        }

        return new Context(dealer, intelligence, credits, ship, missions, plans, identity, validation, bootstrapApplied, currentSystem);
    }

    private static void RunCase(string name, Func<bool> test, ref int passed, ref int failed)
    {
        try
        {
            if (test())
            {
                passed++;
                return;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[TRADE ROUTE VALIDATION BOOTSTRAP SMOKE] {name}: {ex.Message}");
        }

        failed++;
        Console.WriteLine($"[TRADE ROUTE VALIDATION BOOTSTRAP SMOKE] FAIL: {name}");
    }

    private sealed record Context(
        CommodityDealer Dealer,
        MarketIntelligence Intelligence,
        PlayerCredits Credits,
        Ship Ship,
        MissionManager Missions,
        TradePlanManager Plans,
        TradeRouteValidationIdentity Identity,
        TradeRouteValidationDiagnostics Validation,
        bool BootstrapApplied,
        int CurrentSystem);
}
