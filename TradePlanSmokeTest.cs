using Microsoft.Xna.Framework;
using Roguelancer.Configuration;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace Roguelancer;

/// <summary>
/// Focused Phase 18 proof. The suite keeps each case isolated so it can verify
/// that planning reads remembered intelligence without changing the economy.
/// </summary>
internal sealed class TradePlanSmokeTest
{
    private readonly IReadOnlyList<Station> _stations;

    public TradePlanSmokeTest(IReadOnlyList<Station> stations = null)
    {
        _stations = stations != null && RequiredStations.All(name => stations.Any(station => Same(station?.Name, name)))
            ? stations
            : LoadFixtureStations();
    }

    private static readonly string[] RequiredStations = { "Fort Bush", "Newark Station", "Rochester Base" };

    public (int Passed, int Failed) Run()
    {
        int passed = 0;
        int failed = 0;

        RunCase("no plan initially", () => Check(CreateContext().Plans.ActivePlan == null, "new context has a plan"), ref passed, ref failed);
        RunCase("profitable known opportunity creates plan", ValidateCreate, ref passed, ref failed);
        RunCase("unknown source rejected", ValidateUnknownSource, ref passed, ref failed);
        RunCase("unknown destination rejected", ValidateUnknownDestination, ref passed, ref failed);
        RunCase("same station rejected", ValidateSameStation, ref passed, ref failed);
        RunCase("invalid commodity rejected", ValidateInvalidCommodity, ref passed, ref failed);
        RunCase("mission-only commodity rejected", ValidateMissionCommodity, ref passed, ref failed);
        RunCase("source station id stable", () => Check(StartPlan().SourceStationId == "fort_bush", "source id changed"), ref passed, ref failed);
        RunCase("destination station id stable", () => Check(StartPlan().DestinationStationId == "newark_station", "destination id changed"), ref passed, ref failed);
        RunCase("commodity id stable", () => Check(StartPlan().CommodityId == "food-rations", "commodity id changed"), ref passed, ref failed);
        RunCase("source quote snapshot", () => Check(StartPlan().SourceBuyPriceSnapshot == 85, "source quote mismatch"), ref passed, ref failed);
        RunCase("destination quote snapshot", () => Check(StartPlan().DestinationSellPriceSnapshot == 115, "destination quote mismatch"), ref passed, ref failed);
        RunCase("age snapshot current", () => Check(StartPlan().SourceAgeBandSnapshot == MarketObservationAgeBand.Current && StartPlan().DestinationAgeBandSnapshot == MarketObservationAgeBand.Current, "age snapshot mismatch"), ref passed, ref failed);
        RunCase("route metric captured", () => Check(StartPlan().RouteDistanceUnits > 0f && StartPlan().RouteHops == 0, "route metric missing"), ref passed, ref failed);
        RunCase("suggested quantity positive", () => Check(StartPlan().SuggestedQuantity > 0, "suggestion was not positive"), ref passed, ref failed);
        RunCase("suggestion limited by remembered stock", ValidateStockBound, ref passed, ref failed);
        RunCase("suggestion limited by cargo capacity", ValidateCapacityBound, ref passed, ref failed);
        RunCase("suggestion limited by credits", ValidateCreditBound, ref passed, ref failed);
        RunCase("protected cargo excluded", ValidateProtectedCargo, ref passed, ref failed);
        RunCase("plan does not reserve cargo", () =>
        {
            PlanContext c = CreateContext();
            StartPlan(c);
            return Check(c.Cargo.GetMissionCargoReservations().Count == 0 && c.Cargo.GetMissionReservedQuantity(Food.Name) == 0, "planning created a reservation");
        }, ref passed, ref failed);
        RunCase("planning does not mutate source stock", ValidatePlanningDoesNotMutateSource, ref passed, ref failed);
        RunCase("planning does not mutate destination stock", ValidatePlanningDoesNotMutateDestination, ref passed, ref failed);
        RunCase("planning does not mutate credits", () =>
        {
            PlanContext c = CreateContext();
            int before = c.Credits.Credits;
            StartPlan(c);
            return Check(c.Credits.Credits == before, "planning changed credits");
        }, ref passed, ref failed);
        RunCase("source becomes next navigation target", () =>
        {
            PlanContext c = CreateContext();
            TradePlan plan = StartPlan(c);
            bool resolved = TradePlanNavigation.TryResolveNextStation(plan, c.Stations, c.Manager, out Station target, out _);
            return Check(resolved && Same(target?.Name, "Fort Bush") && plan.NextStationId == "fort_bush", "source was not resolved as nav target");
        }, ref passed, ref failed);
        RunCase("legitimate source arrival advances stage", ValidateSourceArrival, ref passed, ref failed);
        RunCase("source arrival refreshes intelligence", ValidateSourceRefresh, ref passed, ref failed);
        RunCase("changed source price updates guidance", ValidateChangedSource, ref passed, ref failed);
        RunCase("source buy remains dealer transaction", ValidateSourceDealerTransaction, ref passed, ref failed);
        RunCase("purchased cargo remains ordinary", ValidateOrdinaryCargo, ref passed, ref failed);
        RunCase("partial suggested quantity accepted", ValidatePartialQuantity, ref passed, ref failed);
        RunCase("destination becomes next navigation target", ValidateDestinationNavigation, ref passed, ref failed);
        RunCase("legitimate destination arrival advances stage", ValidateDestinationArrival, ref passed, ref failed);
        RunCase("destination arrival refreshes intelligence", ValidateDestinationRefresh, ref passed, ref failed);
        RunCase("changed destination price updates guidance", ValidateChangedDestination, ref passed, ref failed);
        RunCase("destination sale remains dealer transaction", ValidateDestinationDealerTransaction, ref passed, ref failed);
        RunCase("zero remaining cargo completes route", ValidateCompletion, ref passed, ref failed);
        RunCase("completion pays no mission reward", ValidateNoMissionReward, ref passed, ref failed);
        RunCase("completion creates no mission status", ValidateNoMissionStatus, ref passed, ref failed);
        RunCase("cancellation leaves economy unchanged", ValidateCancellation, ref passed, ref failed);
        RunCase("replacement leaves economy unchanged", ValidateReplacement, ref passed, ref failed);
        RunCase("stale route remains selectable", ValidateStaleSelectable, ref passed, ref failed);
        RunCase("stale warning is visible", ValidateStaleWarning, ref passed, ref failed);
        RunCase("remote mutation does not update plan quote", ValidateRemoteMutationDoesNotUpdatePlan, ref passed, ref failed);
        RunCase("arrival reveals actual market change", ValidateArrivalRevealsChange, ref passed, ref failed);
        RunCase("route score deterministic", ValidateRouteScoreDeterministic, ref passed, ref failed);
        RunCase("navigation resolution deterministic", ValidateNavigationDeterministic, ref passed, ref failed);
        RunCase("active mission coexists", ValidateMissionCoexistence, ref passed, ref failed);
        RunCase("courier remains a mission type", () => Check(Mission.IsDeliveryType(MissionType.CourierDelivery), "courier type changed"), ref passed, ref failed);
        RunCase("freight reservation remains protected", ValidateFreightReservation, ref passed, ref failed);
        RunCase("export reservation remains protected", ValidateExportReservation, ref passed, ref failed);
        RunCase("save preserves active plan", ValidateSaveActivePlan, ref passed, ref failed);
        RunCase("save preserves stage", ValidateSaveStage, ref passed, ref failed);
        RunCase("save does not refresh remote market", ValidateSaveDoesNotRefresh, ref passed, ref failed);
        RunCase("older save without plan loads", () =>
        {
            PlanContext c = CreateContext();
            StartPlan(c);
            c.Plans.RestoreState(null);
            return Check(c.Plans.ActivePlan == null, "old save left plan active");
        }, ref passed, ref failed);
        RunCase("new game clears plan", () =>
        {
            PlanContext c = CreateContext();
            StartPlan(c);
            c.Plans.Clear();
            return Check(c.Plans.ActivePlan == null && c.Plans.LastCompletedPlan == null, "clear leaked plan");
        }, ref passed, ref failed);
        RunCase("one active plan has no duplicate state", () =>
        {
            PlanContext c = CreateContext();
            StartPlan(c);
            SaveTradePlanData saved = c.Plans.CaptureState();
            return Check(saved != null && c.Plans.ActivePlan != null, "duplicate or missing active state");
        }, ref passed, ref failed);
        RunCase("invalid saved station discarded", ValidateInvalidSavedStation, ref passed, ref failed);
        RunCase("invalid saved commodity discarded", ValidateInvalidSavedCommodity, ref passed, ref failed);
        RunCase("large advisory math is safe", ValidateLargeMath, ref passed, ref failed);
        RunCase("failed replacement is atomic", ValidateAtomicFailure, ref passed, ref failed);

        PlanContext scenario = CreateContext();
        TradePlan scenarioPlan = StartPlan(scenario);
        Console.WriteLine($"[TRADE PLAN SCENARIO] source={scenarioPlan.SourceBuyPriceSnapshot} destination={scenarioPlan.DestinationSellPriceSnapshot} age={scenarioPlan.SourceAgeBandSnapshot}/{scenarioPlan.DestinationAgeBandSnapshot} score={scenarioPlan.OpportunityScore} route={scenarioPlan.RouteDistanceUnits:0.0} units/{scenarioPlan.RouteHops} hops suggested={scenarioPlan.SuggestedQuantity}");
        Console.WriteLine($"[TRADE PLAN SMOKE] RESULT: {passed} passed, {failed} failed");
        return (passed, failed);
    }

    private (bool, string) ValidateCreate()
    {
        PlanContext c = CreateContext();
        TradePlan plan = StartPlan(c);
        return Check(plan != null && plan.Stage == TradePlanStage.GoToSource && plan.ProjectedGrossSpread == 30 && plan.ProjectedGrossResult > 0, "plan was not created from exact known route");
    }

    private (bool, string) ValidateUnknownSource()
    {
        PlanContext c = CreateContext(observeSource: false);
        MarketOpportunity opportunity = BuildOpportunity("fort_bush", "newark_station", "food-rations");
        return Check(!c.Plans.TryCreatePlan(opportunity, out _), "unknown source was accepted");
    }

    private (bool, string) ValidateUnknownDestination()
    {
        PlanContext c = CreateContext(observeDestination: false);
        MarketOpportunity opportunity = BuildOpportunity("fort_bush", "newark_station", "food-rations");
        return Check(!c.Plans.TryCreatePlan(opportunity, out _), "unknown destination was accepted");
    }

    private (bool, string) ValidateSameStation()
    {
        PlanContext c = CreateContext();
        return Check(!c.Plans.TryCreatePlan(BuildOpportunity("fort_bush", "fort_bush", "food-rations"), out _), "same station was accepted");
    }

    private (bool, string) ValidateInvalidCommodity()
    {
        PlanContext c = CreateContext();
        return Check(!c.Plans.TryCreatePlan(BuildOpportunity("fort_bush", "newark_station", "missing-commodity"), out _), "invalid commodity was accepted");
    }

    private (bool, string) ValidateMissionCommodity()
    {
        PlanContext c = CreateContext();
        Commodity missionCommodity = new("phase18-mission", "Phase 18 Mission Cargo", "protected", 1, 1, false, "Mission Cargo", Color.White);
        c.Manager.RegisterCommodity(missionCommodity);
        MarketOpportunity opportunity = new(MarketOpportunityType.TradeRoute, missionCommodity, string.Empty, "Fort Bush", "Newark Station", 10, 1, "TRADE ROUTE", 10, "fort_bush", "newark_station", "CURRENT", "CURRENT", 1, 0);
        return Check(!c.Plans.TryCreatePlan(opportunity, out _), "mission cargo was accepted");
    }

    private (bool, string) ValidateStockBound()
    {
        PlanContext c = CreateContext(capacity: int.MaxValue, credits: int.MaxValue);
        TradePlan plan = StartPlan(c);
        return Check(plan.SuggestedQuantity > 0 && plan.SuggestedQuantity <= GetObservation(c, "fort_bush").Stock, "stock bound failed");
    }

    private (bool, string) ValidateCapacityBound()
    {
        PlanContext c = CreateContext(capacity: 7, credits: 1_000_000);
        TradePlan plan = StartPlan(c);
        return Check(plan.SuggestedQuantity <= 7 / Food.VolumePerUnit, "capacity bound failed");
    }

    private (bool, string) ValidateCreditBound()
    {
        PlanContext c = CreateContext(capacity: 100, credits: 100);
        TradePlan plan = StartPlan(c);
        return Check(plan.SuggestedQuantity <= 100 / plan.SourceBuyPriceSnapshot, "credit bound failed");
    }

    private (bool, string) ValidateProtectedCargo()
    {
        PlanContext c = CreateContext(capacity: 100, credits: 100_000);
        c.Cargo.RegisterFreightReservation(9001, Food, 10);
        c.Cargo.AddCommodity(Food, 10);
        StartPlan(c);
        return Check(c.Plans.GetTradableQuantity(Food.Id) == 0 && c.Plans.ActivePlan.InitialOrdinaryQuantity == 0, "protected cargo counted as ordinary");
    }

    private (bool, string) ValidatePlanningDoesNotMutateSource()
    {
        PlanContext c = CreateContext();
        int before = c.Manager.GetListingForCommodity(c.Fort, Food).Stock;
        StartPlan(c);
        return Check(c.Manager.GetListingForCommodity(c.Fort, Food).Stock == before, "source stock changed");
    }

    private (bool, string) ValidatePlanningDoesNotMutateDestination()
    {
        PlanContext c = CreateContext();
        int before = c.Manager.GetListingForCommodity(c.Newark, Food).Stock;
        StartPlan(c);
        return Check(c.Manager.GetListingForCommodity(c.Newark, Food).Stock == before, "destination stock changed");
    }

    private (bool, string) ValidateSourceArrival()
    {
        PlanContext c = CreateContext();
        StartPlan(c);
        c.Plans.NotifyDocked(c.Fort, out _);
        return Check(c.Plans.ActivePlan.Stage == TradePlanStage.AcquireCommodity && c.Plans.ActivePlan.SourceReached, "source arrival did not advance");
    }

    private (bool, string) ValidateSourceRefresh()
    {
        PlanContext c = CreateContext();
        StartPlan(c);
        long before = GetObservation(c, "fort_bush").ObservedAtMilliseconds;
        c.Manager.AdvanceTime(1000);
        c.Manager.TryRemoveSupply(c.Fort, Food, 50, 0, out _);
        c.Plans.NotifyDocked(c.Fort, out _);
        return Check(GetObservation(c, "fort_bush").ObservedAtMilliseconds > before, "source observation was not refreshed");
    }

    private (bool, string) ValidateChangedSource()
    {
        PlanContext c = CreateContext();
        TradePlan plan = StartPlan(c);
        c.Manager.TryRemoveSupply(c.Fort, Food, 250, 0, out _);
        c.Plans.NotifyDocked(c.Fort, out _);
        return Check(plan.ActualSourceBuyPrice != plan.SourceBuyPriceSnapshot && plan.WarningMessage.Contains("MARKET CHANGED", StringComparison.OrdinalIgnoreCase), "source change was not surfaced");
    }

    private (bool, string) ValidateSourceDealerTransaction()
    {
        PlanContext c = CreateContext(capacity: 50, credits: 100_000);
        TradePlan plan = StartPlan(c);
        c.Dealer.SetDockedStation(c.Fort);
        c.Plans.NotifyDocked(c.Fort, out _);
        bool success = c.Dealer.TryBuyCommodity(Food, 5, c.Credits, c.Cargo, out _);
        return Check(success && plan.Stage == TradePlanStage.GoToDestination && plan.PurchasedQuantity == 5, "dealer purchase did not advance plan");
    }

    private (bool, string) ValidateOrdinaryCargo()
    {
        PlanContext c = CreateContext(capacity: 50, credits: 100_000);
        StartPlan(c);
        c.Dealer.SetDockedStation(c.Fort);
        c.Plans.NotifyDocked(c.Fort, out _);
        c.Dealer.TryBuyCommodity(Food, 5, c.Credits, c.Cargo, out _);
        return Check(c.Cargo.GetMissionCargoReservations().Count == 0 && c.Cargo.GetSellableCommodityQuantity(Food.Name) == 5, "purchase became protected cargo");
    }

    private (bool, string) ValidatePartialQuantity()
    {
        PlanContext c = CreateContext(capacity: 50, credits: 100_000);
        TradePlan plan = StartPlan(c);
        c.Dealer.SetDockedStation(c.Fort);
        c.Plans.NotifyDocked(c.Fort, out _);
        c.Dealer.TryBuyCommodity(Food, Math.Min(5, plan.SuggestedQuantity), c.Credits, c.Cargo, out _);
        return Check(plan.PurchasedQuantity > 0 && plan.PurchasedQuantity < plan.SuggestedQuantity && plan.Stage == TradePlanStage.GoToDestination, "partial quantity was rejected");
    }

    private (bool, string) ValidateDestinationNavigation()
    {
        PlanContext c = CreateContext(capacity: 50, credits: 100_000);
        TradePlan plan = StartPlan(c);
        AcquireFive(c);
        bool resolved = TradePlanNavigation.TryResolveNextStation(plan, c.Stations, c.Manager, out Station target, out _);
        return Check(resolved && Same(target?.Name, "Newark Station") && plan.NextStationId == "newark_station", "destination was not resolved as nav target");
    }

    private (bool, string) ValidateDestinationArrival()
    {
        PlanContext c = CreateContext(capacity: 50, credits: 100_000);
        StartPlan(c);
        AcquireFive(c);
        c.Plans.NotifyDocked(c.Newark, out _);
        return Check(c.Plans.ActivePlan.Stage == TradePlanStage.SellCommodity && c.Plans.ActivePlan.DestinationReached, "destination arrival did not advance");
    }

    private (bool, string) ValidateDestinationRefresh()
    {
        PlanContext c = CreateContext(capacity: 50, credits: 100_000);
        StartPlan(c);
        AcquireFive(c);
        long before = GetObservation(c, "newark_station").ObservedAtMilliseconds;
        c.Manager.AdvanceTime(1000);
        c.Manager.TryAddSupply(c.Newark, Food, 40, out _);
        c.Plans.NotifyDocked(c.Newark, out _);
        return Check(GetObservation(c, "newark_station").ObservedAtMilliseconds > before, "destination observation was not refreshed");
    }

    private (bool, string) ValidateChangedDestination()
    {
        PlanContext c = CreateContext(capacity: 50, credits: 100_000);
        TradePlan plan = StartPlan(c);
        AcquireFive(c);
        bool added = c.Manager.TryAddSupply(c.Newark, Food, 200, out string addMessage);
        c.Plans.NotifyDocked(c.Newark, out _);
        return Check(
            added && plan.ActualDestinationSellPrice != plan.DestinationSellPriceSnapshot &&
            (plan.WarningMessage.Contains("MARKET CHANGED", StringComparison.OrdinalIgnoreCase) ||
             plan.WarningMessage.Contains("ROUTE NO LONGER PROFITABLE", StringComparison.OrdinalIgnoreCase)),
            $"destination change was not surfaced actual={plan.ActualDestinationSellPrice} snapshot={plan.DestinationSellPriceSnapshot} add='{addMessage}'");
    }

    private (bool, string) ValidateDestinationDealerTransaction()
    {
        PlanContext c = CreateContext(capacity: 50, credits: 100_000);
        StartPlan(c);
        AcquireFive(c);
        c.Dealer.SetDockedStation(c.Newark);
        c.Plans.NotifyDocked(c.Newark, out _);
        int before = c.Credits.Credits;
        bool success = c.Dealer.TrySellCommodity(Food, 5, c.Credits, c.Cargo, out _);
        return Check(success && c.Credits.Credits > before && c.Cargo.GetSellableCommodityQuantity(Food.Name) == 0, "dealer sale did not commit normally");
    }

    private (bool, string) ValidateCompletion()
    {
        PlanContext c = CreateContext(capacity: 50, credits: 100_000);
        StartPlan(c);
        AcquireFive(c);
        c.Dealer.SetDockedStation(c.Newark);
        c.Plans.NotifyDocked(c.Newark, out _);
        c.Dealer.TrySellCommodity(Food, 5, c.Credits, c.Cargo, out _);
        return Check(c.Plans.ActivePlan == null && c.Plans.LastCompletedPlan?.Stage == TradePlanStage.Complete, "plan did not complete");
    }

    private (bool, string) ValidateNoMissionReward()
    {
        PlanContext c = CreateContext(capacity: 50, credits: 100_000);
        StartPlan(c);
        AcquireFive(c);
        c.Dealer.SetDockedStation(c.Newark);
        c.Plans.NotifyDocked(c.Newark, out _);
        int before = c.Credits.Credits;
        c.Dealer.TrySellCommodity(Food, 5, c.Credits, c.Cargo, out _);
        return Check(c.Credits.Credits - before == 5 * 115, "completion added a mission reward");
    }

    private (bool, string) ValidateNoMissionStatus()
    {
        PlanContext c = CreateContext(capacity: 50, credits: 100_000);
        StartPlan(c);
        AcquireFive(c);
        c.Dealer.SetDockedStation(c.Newark);
        c.Plans.NotifyDocked(c.Newark, out _);
        c.Dealer.TrySellCommodity(Food, 5, c.Credits, c.Cargo, out _);
        return Check(c.Missions.ActiveMission == null && c.Missions.CompletedMissions.Count == 0, "completion created mission state");
    }

    private (bool, string) ValidateCancellation()
    {
        PlanContext c = CreateContext();
        StartPlan(c);
        int credits = c.Credits.Credits;
        int source = c.Manager.GetListingForCommodity(c.Fort, Food).Stock;
        int destination = c.Manager.GetListingForCommodity(c.Newark, Food).Stock;
        c.Plans.CancelActivePlan(out _);
        return Check(c.Plans.ActivePlan == null && c.Credits.Credits == credits && c.Manager.GetListingForCommodity(c.Fort, Food).Stock == source && c.Manager.GetListingForCommodity(c.Newark, Food).Stock == destination, "cancellation changed economy");
    }

    private (bool, string) ValidateReplacement()
    {
        PlanContext c = CreateContext();
        StartPlan(c);
        int credits = c.Credits.Credits;
        MarketOpportunity replacement = BuildOpportunity("rochester_base", "newark_station", "food-rations");
        bool replaced = c.Plans.TryCreatePlan(replacement, out _);
        return Check(replaced && c.Plans.ActivePlan.SourceStationId == "rochester_base" && c.Credits.Credits == credits, "replacement failed or changed credits");
    }

    private (bool, string) ValidateStaleSelectable()
    {
        PlanContext c = CreateContext();
        c.Manager.AdvanceTime(3 * 60 * 60);
        MarketOpportunity stale = c.Missions.GetKnownMarketOpportunities(8).First(opportunity => opportunity.Type == MarketOpportunityType.TradeRoute && opportunity.OriginStationId == "fort_bush" && opportunity.DestinationStationId == "newark_station");
        return Check(c.Plans.TryCreatePlan(stale, out _), "stale route was rejected");
    }

    private (bool, string) ValidateStaleWarning()
    {
        PlanContext c = CreateContext();
        c.Manager.AdvanceTime(3 * 60 * 60);
        MarketOpportunity stale = c.Missions.GetKnownMarketOpportunities(8).First(opportunity => opportunity.Type == MarketOpportunityType.TradeRoute && opportunity.OriginStationId == "fort_bush" && opportunity.DestinationStationId == "newark_station");
        c.Plans.TryCreatePlan(stale, out string message);
        return Check(message.Contains("stale", StringComparison.OrdinalIgnoreCase), "stale warning missing");
    }

    private (bool, string) ValidateRemoteMutationDoesNotUpdatePlan()
    {
        PlanContext c = CreateContext();
        TradePlan plan = StartPlan(c);
        c.Manager.TryRemoveSupply(c.Fort, Food, 250, 0, out _);
        return Check(plan.SourceBuyPriceSnapshot == 85 && GetObservation(c, "fort_bush").BuyPrice == 85, "remote mutation silently changed quote");
    }

    private (bool, string) ValidateArrivalRevealsChange()
    {
        PlanContext c = CreateContext();
        TradePlan plan = StartPlan(c);
        c.Manager.TryRemoveSupply(c.Fort, Food, 250, 0, out _);
        c.Plans.NotifyDocked(c.Fort, out _);
        return Check(plan.ActualSourceBuyPrice > plan.SourceBuyPriceSnapshot && GetObservation(c, "fort_bush").BuyPrice == plan.ActualSourceBuyPrice, "arrival did not reveal actual price");
    }

    private (bool, string) ValidateRouteScoreDeterministic()
    {
        PlanContext c = CreateContext();
        int first = GetRoute(c).Score;
        int second = c.Missions.GetKnownMarketOpportunities(8).First(opportunity => opportunity.Type == MarketOpportunityType.TradeRoute && opportunity.OriginStationId == "fort_bush" && opportunity.DestinationStationId == "newark_station").Score;
        return Check(first == second && first > 0, "route score was not deterministic");
    }

    private (bool, string) ValidateNavigationDeterministic()
    {
        PlanContext c = CreateContext();
        TradePlan plan = StartPlan(c);
        bool first = TradePlanNavigation.TryResolveNextStation(plan, c.Stations, c.Manager, out Station firstTarget, out _);
        bool second = TradePlanNavigation.TryResolveNextStation(plan, c.Stations, c.Manager, out Station secondTarget, out _);
        return Check(first && second && Same(firstTarget?.Name, secondTarget?.Name), "navigation resolution changed");
    }

    private (bool, string) ValidateMissionCoexistence()
    {
        PlanContext c = CreateContext();
        Mission mission = Mission.FromDefinition(MissionCatalog.GetById(MissionCatalog.PriorityDispatchId), "Phase 18 Smoke", FactionManager.LibertyCorporations);
        mission.Status = MissionStatus.InProgress;
        c.Missions.RestoreState(new[] { mission }, null);
        TradePlan plan = StartPlan(c);
        return Check(plan != null && ReferenceEquals(c.Missions.ActiveMission, mission) && c.Missions.ActiveMission.Status == MissionStatus.InProgress, "trade plan replaced mission state");
    }

    private (bool, string) ValidateFreightReservation()
    {
        PlanContext c = CreateContext();
        c.Cargo.RegisterFreightReservation(1702, Food, 4);
        c.Cargo.AddCommodity(Food, 4);
        int before = c.Cargo.GetMissionCargoQuantity(1702);
        StartPlan(c);
        return Check(before == 4 && c.Plans.GetTradableQuantity(Food.Id) == 0 && c.Cargo.GetMissionCargoQuantity(1702) == 4, "freight reservation was exposed or changed");
    }

    private (bool, string) ValidateExportReservation()
    {
        PlanContext c = CreateContext();
        c.Cargo.AddMissionCargo(1703, Food, 4);
        StartPlan(c);
        return Check(c.Plans.GetTradableQuantity(Food.Id) == 0 && c.Cargo.GetMissionCargoQuantity(1703) == 4, "export reservation was exposed or changed");
    }

    private (bool, string) ValidateSaveActivePlan()
    {
        PlanContext c = CreateContext();
        TradePlan plan = StartPlan(c);
        SaveTradePlanData saved = c.Plans.CaptureState();
        c.Plans.RestoreState(saved);
        return Check(c.Plans.ActivePlan != null && c.Plans.ActivePlan.SourceStationId == plan.SourceStationId && c.Plans.ActivePlan.CommodityId == plan.CommodityId, "active plan did not round-trip");
    }

    private (bool, string) ValidateSaveStage()
    {
        PlanContext c = CreateContext(capacity: 50, credits: 100_000);
        StartPlan(c);
        AcquireFive(c);
        SaveTradePlanData saved = c.Plans.CaptureState();
        c.Plans.RestoreState(saved);
        return Check(c.Plans.ActivePlan?.Stage == TradePlanStage.GoToDestination && c.Plans.ActivePlan?.CargoAcquired == true, "stage did not round-trip");
    }

    private (bool, string) ValidateSaveDoesNotRefresh()
    {
        PlanContext c = CreateContext();
        StartPlan(c);
        long before = GetObservation(c, "newark_station").ObservedAtMilliseconds;
        c.Manager.AdvanceTime(1000);
        SaveTradePlanData saved = c.Plans.CaptureState();
        c.Plans.RestoreState(saved);
        return Check(GetObservation(c, "newark_station").ObservedAtMilliseconds == before, "save restore refreshed remote market");
    }

    private (bool, string) ValidateInvalidSavedStation()
    {
        PlanContext c = CreateContext();
        c.Plans.RestoreState(new SaveTradePlanData
        {
            SourceStationId = "missing_station",
            DestinationStationId = "newark_station",
            CommodityId = Food.Id,
            SourceBuyPrice = 85,
            DestinationSellPrice = 115,
            RouteDistanceUnits = 1f,
            Stage = TradePlanStage.GoToSource
        });
        return Check(c.Plans.ActivePlan == null, "invalid station save survived");
    }

    private (bool, string) ValidateInvalidSavedCommodity()
    {
        PlanContext c = CreateContext();
        c.Plans.RestoreState(new SaveTradePlanData
        {
            SourceStationId = "fort_bush",
            DestinationStationId = "newark_station",
            CommodityId = "missing_commodity",
            SourceBuyPrice = 85,
            DestinationSellPrice = 115,
            RouteDistanceUnits = 1f,
            Stage = TradePlanStage.GoToSource
        });
        return Check(c.Plans.ActivePlan == null, "invalid commodity save survived");
    }

    private (bool, string) ValidateLargeMath()
    {
        PlanContext c = CreateContext(capacity: int.MaxValue, credits: int.MaxValue);
        TradePlan plan = StartPlan(c);
        return Check(plan.SuggestedQuantity >= 0 && plan.ProjectedGrossResult >= 0 && plan.ProjectedGrossResult <= long.MaxValue, "advisory math overflowed");
    }

    private (bool, string) ValidateAtomicFailure()
    {
        PlanContext c = CreateContext();
        TradePlan plan = StartPlan(c);
        bool accepted = c.Plans.TryCreatePlan(BuildOpportunity("fort_bush", "missing_station", "food-rations"), out _);
        return Check(!accepted && ReferenceEquals(c.Plans.ActivePlan, plan), "failed replacement discarded existing plan");
    }

    private void AcquireFive(PlanContext c)
    {
        c.Dealer.SetDockedStation(c.Fort);
        c.Plans.NotifyDocked(c.Fort, out _);
        c.Dealer.TryBuyCommodity(Food, 5, c.Credits, c.Cargo, out _);
    }

    private TradePlan StartPlan(PlanContext context = null)
    {
        PlanContext c = context ?? CreateContext();
        if (!c.Plans.TryCreatePlan(GetRoute(c), out string failure)) throw new InvalidOperationException(failure);
        return c.Plans.ActivePlan;
    }

    private MarketOpportunity GetRoute(PlanContext context)
    {
        return context.Missions.GetKnownMarketOpportunities(8).First(opportunity =>
            opportunity.Type == MarketOpportunityType.TradeRoute &&
            opportunity.OriginStationId == "fort_bush" &&
            opportunity.DestinationStationId == "newark_station" &&
            opportunity.CommodityId == Food.Id);
    }

    private MarketOpportunity BuildOpportunity(string sourceId, string destinationId, string commodityId)
    {
        Commodity commodity = CommodityCatalog.GetById(commodityId);
        return new MarketOpportunity(MarketOpportunityType.TradeRoute, commodity, string.Empty, sourceId, destinationId, 1, 1, "TRADE ROUTE", 1, sourceId, destinationId, "CURRENT", "CURRENT", 1, 0);
    }

    private PlanContext CreateContext(int capacity = 100, int credits = 100_000, bool observeSource = true, bool observeDestination = true)
    {
        CommodityDealer dealer = new();
        MarketManager manager = dealer.MarketManager;
        MarketIntelligence intelligence = new(manager);
        dealer.SetMarketIntelligence(intelligence);
        CargoHold cargo = new(Math.Max(0, capacity));
        PlayerCredits playerCredits = new(Math.Max(0, credits));
        MissionManager missions = new(playerCredits, null, null, manager, cargo, intelligence);
        missions.SetRouteAuthority(new MarketRouteAuthority());
        TradePlanManager plans = new(manager, intelligence, new MarketRouteAuthority(), cargo, playerCredits);
        dealer.TransactionCompleted += transaction => plans.ObserveTransaction(transaction, out _);

        Station fort = Station("Fort Bush");
        Station newark = Station("Newark Station");
        Station rochester = Station("Rochester Base");
        if (observeSource) intelligence.ObserveStation(fort);
        if (observeDestination) intelligence.ObserveStation(newark);
        intelligence.ObserveStation(rochester);
        return new PlanContext(dealer, manager, intelligence, plans, missions, cargo, playerCredits, fort, newark, _stations);
    }

    private MarketObservation GetObservation(PlanContext context, string stationId) =>
        context.Intelligence.TryGetObservation(stationId, Food.Id, out MarketObservation observation) ? observation : null;

    private Station Station(string name) => _stations.First(station => Same(station?.Name, name));

    private static Commodity Food => CommodityCatalog.GetById("food-rations");

    private void RunCase(string label, Func<(bool Success, string FailureReason)> test, ref int passed, ref int failed)
    {
        try
        {
            (bool success, string reason) = RunSilenced(test);
            if (success)
            {
                passed++;
                Console.WriteLine($"[TRADE PLAN SMOKE] PASS {label}");
            }
            else
            {
                failed++;
                Console.WriteLine($"[TRADE PLAN SMOKE] FAIL {label}: {reason}");
            }
        }
        catch (Exception ex)
        {
            failed++;
            Console.WriteLine($"[TRADE PLAN SMOKE] FAIL {label}: {ex.Message}");
        }
    }

    private static (bool, string) Check(bool value, string reason) => value ? (true, string.Empty) : (false, reason);
    private static (bool, string) Fail(string reason) => (false, reason);
    private static bool Same(string left, string right) => string.Equals(left, right, StringComparison.OrdinalIgnoreCase);

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
            catch { }
        }
        return result;
    }

    private sealed record PlanContext(
        CommodityDealer Dealer,
        MarketManager Manager,
        MarketIntelligence Intelligence,
        TradePlanManager Plans,
        MissionManager Missions,
        CargoHold Cargo,
        PlayerCredits Credits,
        Station Fort,
        Station Newark,
        IReadOnlyList<Station> Stations);
}
