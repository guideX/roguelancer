using Microsoft.Xna.Framework;
using Roguelancer.Configuration;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace Roguelancer;

/// <summary>
/// Phase 20 validation against the checked-in production configuration. The
/// suite deliberately constructs no synthetic systems or stations.
/// </summary>
internal sealed class ProductionMultiSystemTradeRouteSmokeTest
{
    private const int NewYorkSystem = 1;
    private const int CaliforniaSystem = 2;
    private const int TexasSystem = 4;
    private const string SourceStationId = "fort_bush";
    private const string DestinationStationId = "riverside_station";
    private const string CommodityId = "food-rations";

    private readonly ConfigurationManager _configuration;
    private readonly IReadOnlyList<Station> _stations;
    private readonly Station _source;
    private readonly Station _destination;
    private readonly Commodity _food;
    private readonly ProductionTopologyReport _topology;
    private string _lastFailureReason = string.Empty;

    public ProductionMultiSystemTradeRouteSmokeTest(ConfigurationManager configuration = null)
    {
        _configuration = configuration ?? new ConfigurationManager();
        if (_configuration.Systems.Count == 0)
        {
            _configuration.LoadAll();
        }

        _stations = _configuration.Stations
            .Where(config => config != null)
            .Select(config => new Station(config, null))
            .ToList();
        _source = _stations.FirstOrDefault(station => Same(station?.Name, "Fort Bush"));
        _destination = _stations.FirstOrDefault(station => Same(station?.Name, "Riverside Station"));
        _food = CommodityCatalog.GetByIdOrName(CommodityId);
        _topology = ProductionTopologyValidator.Validate(_configuration);
    }

    public (int Passed, int Failed) Run()
    {
        int passed = 0;
        int failed = 0;

        RunCase("New York system resolves", () => Check(System(NewYorkSystem)?.CanonicalId == "new_york", "New York system is not canonical"), ref passed, ref failed);
        RunCase("Texas system resolves", () => Check(System(TexasSystem)?.CanonicalId == "texas", "Texas system config is missing"), ref passed, ref failed);
        RunCase("California system resolves", () => Check(System(CaliforniaSystem)?.CanonicalId == "california", "California system is not canonical"), ref passed, ref failed);
        RunCase("remote destination system resolves", () => Check(_destination?.Config?.SystemIndex == CaliforniaSystem, "destination is not in California"), ref passed, ref failed);
        RunCase("Fort Bush resolves", () => Check(_source != null && _source.Config.SystemIndex == NewYorkSystem, "Fort Bush is missing or moved"), ref passed, ref failed);
        RunCase("Riverside Station resolves", () => Check(_destination != null && _destination.Config.SystemIndex == CaliforniaSystem, "Riverside Station is missing or moved"), ref passed, ref failed);
        RunCase("source has production market", () => Check(CreateContext().Manager.HasMarketConfigForStation(_source), "Fort Bush market config missing"), ref passed, ref failed);
        RunCase("destination has production market", () => Check(CreateContext().Manager.HasMarketConfigForStation(_destination), "Riverside market config missing"), ref passed, ref failed);
        RunCase("selected commodity exists", () => Check(_food != null && _food.Id == CommodityId && !_food.IsMissionCargo, "Food Rations is not an ordinary commodity"), ref passed, ref failed);
        RunCase("source baseline buy is positive", () => Check(SourceListing(CreateContext())?.BaseBuyPrice > 0, "source baseline buy is not positive"), ref passed, ref failed);
        RunCase("destination baseline sell is positive", () => Check(DestinationListing(CreateContext())?.BaseSellPrice > 0, "destination baseline sell is not positive"), ref passed, ref failed);
        RunCase("baseline route is profitable", ValidateBaselineProfit, ref passed, ref failed);
        RunCase("New York to Texas link is valid", () => Check(HasLeg(NewYorkSystem, TexasSystem, "Jump Hole to Texas"), "New York to Texas link is not valid"), ref passed, ref failed);
        RunCase("Texas to California link is valid", () => Check(HasLeg(TexasSystem, CaliforniaSystem, "California Jump Hole"), "Texas to California link is not valid"), ref passed, ref failed);
        RunCase("California destination is local after arrival", () => Check(ProductionTopologyValidator.TryGetRoute(_configuration, CaliforniaSystem, CaliforniaSystem, out MarketSystemRoute route) && route.JumpCount == 0, "California local route failed"), ref passed, ref failed);
        RunCase("selected transitions have stable ids", ValidateStableTransitionIds, ref passed, ref failed);
        RunCase("selected transition destinations exist", ValidateTransitionDestinationSystems, ref passed, ref failed);
        RunCase("selected arrival identities resolve", ValidateArrivalIdentities, ref passed, ref failed);
        RunCase("BFS finds the production corridor", ValidateProductionRoute, ref passed, ref failed);
        RunCase("production corridor hop count is two", () => Check(ProductionRoute()?.JumpCount == 2, "expected New York -> Texas -> California"), ref passed, ref failed);
        RunCase("first jump target resolves in New York", () => Check(ProductionRoute()?.Legs[0].DestinationSystemIndex == TexasSystem, "first target is not Texas"), ref passed, ref failed);
        RunCase("next jump target resolves in Texas", () => Check(ProductionRoute()?.Legs[1].DestinationSystemIndex == CaliforniaSystem, "second target is not California"), ref passed, ref failed);
        RunCase("final target resolves in California", () => Check(ProductionRoute()?.DestinationSystemIndex == CaliforniaSystem, "final target system is not California"), ref passed, ref failed);
        RunCase("final station id remains stable", () => Check(CreateContext().Manager.GetStationId(_destination) == DestinationStationId, "destination station id changed"), ref passed, ref failed);
        RunCase("source Trade Plan creation works", ValidatePlanCreation, ref passed, ref failed);
        RunCase("source market observation works", () => Check(CreateObservedContext().Intel.TryGetObservation(SourceStationId, CommodityId, out _), "source observation missing"), ref passed, ref failed);
        RunCase("destination observation can be restored", ValidateObservationSaveRoundTrip, ref passed, ref failed);
        RunCase("production market opportunity appears", ValidateOpportunity, ref passed, ref failed);
        RunCase("Trade Plan selects production route", ValidatePlanSelectsRoute, ref passed, ref failed);
        RunCase("source purchase uses normal dealer", ValidateSourceTransaction, ref passed, ref failed);
        RunCase("source stock decreases", ValidateSourceStockDecrease, ref passed, ref failed);
        RunCase("source price reacts", ValidateSourcePriceReaction, ref passed, ref failed);
        RunCase("source cargo remains ordinary", ValidateOrdinaryCargo, ref passed, ref failed);
        RunCase("intermediate transition does not reveal market prices", ValidateIntermediateKnowledgeBoundary, ref passed, ref failed);
        RunCase("Texas save/load preserves route", ValidateTexasSaveLoad, ref passed, ref failed);
        RunCase("California save/load preserves route", ValidateCaliforniaSaveLoad, ref passed, ref failed);
        RunCase("destination station resolves after load", ValidateDestinationResolveAfterLoad, ref passed, ref failed);
        RunCase("destination docking advances plan", ValidateDestinationDocking, ref passed, ref failed);
        RunCase("destination sale uses normal dealer", ValidateDestinationTransaction, ref passed, ref failed);
        RunCase("destination stock increases", ValidateDestinationStockIncrease, ref passed, ref failed);
        RunCase("destination price reacts", ValidateDestinationPriceReaction, ref passed, ref failed);
        RunCase("credits increase by trade margin", ValidateCreditsIncrease, ref passed, ref failed);
        RunCase("Trade Plan completes normally", ValidatePlanCompletion, ref passed, ref failed);
        RunCase("no mission reward is added", ValidateNoMissionReward, ref passed, ref failed);
        RunCase("route recalculates from Texas", ValidateIntermediateRecalculation, ref passed, ref failed);
        RunCase("reverse route resolves", () => Check(ProductionTopologyValidator.TryGetRoute(_configuration, CaliforniaSystem, NewYorkSystem, out MarketSystemRoute route) && route.JumpCount > 0, "reverse route is unavailable"), ref passed, ref failed);
        RunCase("malformed unrelated links do not break corridor", () => Check(ValidateProductionRoute() && _topology.UnresolvedLegacyLinks.Count > 0, "legacy links did not remain isolated"), ref passed, ref failed);
        RunCase("topology validator reports remaining issues", () => Check(_topology.HasIssues && _topology.MalformedLinks.Count > 0, "validator silently skipped malformed links"), ref passed, ref failed);
        RunCase("remote recovery moves stock toward baseline", ValidateRemoteRecovery, ref passed, ref failed);
        RunCase("remote market save/load persists state", ValidateRemoteMarketSaveLoad, ref passed, ref failed);
        RunCase("remote freight eligibility follows normal rules", ValidateRemoteFreightEligibility, ref passed, ref failed);
        RunCase("remote export eligibility follows normal rules", ValidateRemoteExportEligibility, ref passed, ref failed);
        RunCase("commodity catalog regression remains intact", () => Check(CommodityCatalog.All.Count >= 12 && _food != null, "commodity catalog is incomplete"), ref passed, ref failed);
        RunCase("market intelligence aging remains correct", ValidateAging, ref passed, ref failed);
        RunCase("stale remote quote remains historical", ValidateStaleQuote, ref passed, ref failed);
        RunCase("local destination observation refreshes", ValidateLocalRefresh, ref passed, ref failed);
        RunCase("repeated route exploitation changes margin", ValidateRepeatedMarginChange, ref passed, ref failed);
        RunCase("remote stock never goes negative", ValidateNoNegativeStock, ref passed, ref failed);
        RunCase("market prices do not invert", ValidateNoPriceInversion, ref passed, ref failed);
        RunCase("route calculation does not mutate economy", ValidateRouteNoEconomyMutation, ref passed, ref failed);
        RunCase("route calculation does not mutate cargo", ValidateRouteNoCargoMutation, ref passed, ref failed);
        RunCase("route plotting does not mutate missions", ValidateRouteNoMissionMutation, ref passed, ref failed);
        RunCase("new game does not reveal remote market", () => Check(new MarketIntelligence(CreateContext().Manager).KnownStations.Count == 0, "new game revealed remote knowledge"), ref passed, ref failed);
        RunCase("old save initializes remote market", ValidateOldMarketState, ref passed, ref failed);
        RunCase("production topology scan is bounded", () => Check(_topology.ConfiguredJumpHoleCount == 23 && _topology.Issues.Count < 4096, "topology scan was not bounded"), ref passed, ref failed);
        RunCase("production route search terminates without loops", () => Check(ProductionRoute()?.Legs.Select(leg => leg.DestinationSystemIndex).Distinct().Count() == 2, "route repeated a system"), ref passed, ref failed);
        RunCase("missing optional legacy systems fail safely", () => Check(!ProductionTopologyValidator.TryGetRoute(_configuration, NewYorkSystem, 5, out _), "missing legacy system unexpectedly resolved"), ref passed, ref failed);
        RunCase("selected production assertions are deterministic", ValidateDeterministicScenario, ref passed, ref failed);

        foreach (ProductionTopologyIssue issue in _topology.Issues)
        {
            Console.WriteLine($"[PRODUCTION TOPOLOGY] {issue}");
        }

        MarketRouteLeg[] routeLegs = ProductionRoute()?.Legs?.ToArray() ?? Array.Empty<MarketRouteLeg>();
        StationMarketListing sourceListing = SourceListing(CreateContext());
        StationMarketListing destinationListing = DestinationListing(CreateContext());
        Console.WriteLine($"[PRODUCTION TRADE ROUTE SCENARIO] source=Fort Bush/New York (1), destination=Riverside Station/California (2), path=1 -> 4 -> 2, transitions={string.Join(" -> ", routeLegs.Select(leg => leg.TransitionName))}, hops={routeLegs.Length}, commodity=Food Rations, source_buy={sourceListing?.BaseBuyPrice}, destination_sell={destinationListing?.BaseSellPrice}, spread={(destinationListing?.BaseSellPrice ?? 0) - (sourceListing?.BaseBuyPrice ?? 0)}");
        Console.WriteLine($"[PRODUCTION ROUTE SMOKE] RESULT: {passed} passed, {failed} failed");
        return (passed, failed);
    }

    private bool ValidateBaselineProfit()
    {
        Context context = CreateContext();
        StationMarketListing source = SourceListing(context);
        StationMarketListing destination = DestinationListing(context);
        return Check(source != null && destination != null && destination.BaseSellPrice > source.BaseBuyPrice, "Food Rations baseline spread is not positive");
    }

    private bool ValidateStableTransitionIds()
    {
        MarketSystemRoute route = ProductionRoute();
        return Check(route != null && route.Legs.All(leg => !string.IsNullOrWhiteSpace(leg.TransitionId) && !string.IsNullOrWhiteSpace(leg.ArrivalTransitionId)) && route.Legs.Select(leg => leg.TransitionId).Distinct(StringComparer.OrdinalIgnoreCase).Count() == route.Legs.Count, "transition id was missing or duplicated");
    }

    private bool ValidateTransitionDestinationSystems()
    {
        MarketSystemRoute route = ProductionRoute();
        return Check(route != null && route.Legs.All(leg => System(leg.DestinationSystemIndex) != null), "route points at a missing system");
    }

    private bool ValidateArrivalIdentities()
    {
        MarketSystemRoute route = ProductionRoute();
        return Check(route != null && route.Legs.All(leg => leg.ArrivalConfig != null), "a selected arrival identity did not resolve");
    }

    private bool ValidateProductionRoute()
    {
        MarketSystemRoute route = ProductionRoute();
        return Check(route != null && route.OriginSystemIndex == NewYorkSystem && route.DestinationSystemIndex == CaliforniaSystem && route.Legs.Select(leg => leg.DestinationSystemIndex).SequenceEqual(new[] { TexasSystem, CaliforniaSystem }), "production route did not resolve as New York -> Texas -> California");
    }

    private bool ValidatePlanCreation()
    {
        Context context = CreateObservedContext();
        MarketOpportunity opportunity = FindOpportunity(context);
        bool created = opportunity != null && context.Plans.TryCreatePlan(opportunity, out _);
        return Check(created && context.Plans.ActivePlan?.DestinationStationId == DestinationStationId && context.Plans.ActivePlan.RouteHops == 2, "production Trade Plan was not created");
    }

    private bool ValidateObservationSaveRoundTrip()
    {
        Context context = CreateObservedContext();
        List<SaveMarketIntelligenceData> saved = context.Intel.CaptureState();
        MarketIntelligence restored = new(context.Manager);
        restored.RestoreState(saved);
        return Check(restored.TryGetObservation(DestinationStationId, CommodityId, out MarketObservation observation) && observation.SellPrice > 0 && observation.SystemIndex == CaliforniaSystem, "destination observation did not round-trip");
    }

    private bool ValidateOpportunity()
    {
        Context context = CreateObservedContext();
        MarketOpportunity opportunity = FindOpportunity(context);
        return Check(opportunity != null && opportunity.CurrentSpread > 0 && opportunity.RouteHops == 2 && opportunity.OriginStationId == SourceStationId && opportunity.DestinationStationId == DestinationStationId, "production opportunity was not ranked");
    }

    private bool ValidatePlanSelectsRoute()
    {
        Context context = StartPlanAtSource(out _);
        if (!context.Dealer.TryBuyCommodity(_food, 10, context.Credits, context.Cargo, out _)) return false;
        return Check(context.Plans.TryPlanNavigation(NewYorkSystem, out TradePlanNavigationState state, out _) && state.Status == TradePlanRouteStatus.TransitionRequired && state.NextTransition.DestinationSystemIndex == TexasSystem && state.FinalStationId == DestinationStationId, "plan did not select first production jump");
    }

    private bool ValidateSourceTransaction()
    {
        Context context = StartPlanAtSource(out _);
        return Check(context.Dealer.CurrentStation == _source && context.Dealer.TryBuyCommodity(_food, 10, context.Credits, context.Cargo, out _), "normal source dealer transaction failed");
    }

    private bool ValidateSourceStockDecrease()
    {
        Context context = StartPlanAtSource(out _);
        int before = SourceListing(context).Stock;
        if (!context.Dealer.TryBuyCommodity(_food, 10, context.Credits, context.Cargo, out _)) return false;
        return Check(SourceListing(context).Stock < before, "source stock did not decrease");
    }

    private bool ValidateSourcePriceReaction()
    {
        Context context = StartPlanAtSource(out _);
        int before = SourceListing(context).BuyPrice;
        if (!context.Dealer.TryBuyCommodity(_food, 10, context.Credits, context.Cargo, out _)) return false;
        return Check(SourceListing(context).BuyPrice >= before && SourceListing(context).BuyPrice > SourceListing(context).BaseBuyPrice, "source buy price did not respond to scarcity");
    }

    private bool ValidateOrdinaryCargo()
    {
        Context context = StartPlanAtSource(out _);
        if (!context.Dealer.TryBuyCommodity(_food, 10, context.Credits, context.Cargo, out _)) return false;
        return Check(context.Cargo.GetMissionReservedQuantity(_food.Name) == 0 && context.Cargo.GetCommodityQuantity(_food.Name) == 10, "source purchase became mission cargo");
    }

    private bool ValidateIntermediateKnowledgeBoundary()
    {
        Context context = StartPlanAtSource(out _);
        if (!context.Dealer.TryBuyCommodity(_food, 10, context.Credits, context.Cargo, out _)) return false;
        bool planned = context.Plans.TryPlanNavigation(TexasSystem, out TradePlanNavigationState state, out _);
        return Check(planned && state.NextTransition.DestinationSystemIndex == CaliforniaSystem && context.Intel.KnownStations.Count == 2, "intermediate route changed market knowledge");
    }

    private bool ValidateTexasSaveLoad()
    {
        Context context = StartPlanAtSource(out _);
        if (!context.Dealer.TryBuyCommodity(_food, 10, context.Credits, context.Cargo, out _)) return false;
        SaveTradePlanData saved = context.Plans.CaptureState();
        TradePlanManager restored = new(context.Manager, context.Intel, context.Authority, context.Cargo, context.Credits);
        restored.RestoreState(JsonSerializer.Deserialize<SaveTradePlanData>(JsonSerializer.Serialize(saved)));
        return Check(restored.TryPlanNavigation(TexasSystem, out TradePlanNavigationState state, out _) && state.RemainingHopCount == 1 && state.NextTransition.DestinationSystemIndex == CaliforniaSystem && restored.ActivePlan.DestinationStationId == DestinationStationId, "Texas save/load lost the California leg");
    }

    private bool ValidateCaliforniaSaveLoad()
    {
        Context context = StartPlanAtSource(out _);
        if (!context.Dealer.TryBuyCommodity(_food, 10, context.Credits, context.Cargo, out _)) return false;
        TradePlanManager restored = RestorePlan(context);
        return Check(restored.TryPlanNavigation(CaliforniaSystem, out TradePlanNavigationState state, out _) && state.Status == TradePlanRouteStatus.LocalStation && state.FinalStationId == DestinationStationId, "California save/load did not retain local destination state");
    }

    private bool ValidateDestinationResolveAfterLoad()
    {
        Context context = StartPlanAtSource(out _);
        if (!context.Dealer.TryBuyCommodity(_food, 10, context.Credits, context.Cargo, out _)) return false;
        TradePlanManager restored = RestorePlan(context);
        return Check(TradePlanNavigation.TryResolveNextStation(restored.ActivePlan, _stations.Where(station => station.Config.SystemIndex == CaliforniaSystem), context.Manager, out Station station, out _) && station == _destination, "destination station did not resolve after system load");
    }

    private bool ValidateDestinationDocking()
    {
        Context context = StartPlanAtSource(out _);
        if (!context.Dealer.TryBuyCommodity(_food, 10, context.Credits, context.Cargo, out _)) return false;
        context.Dealer.SetDockedStation(_destination);
        bool docked = context.Plans.NotifyDocked(_destination, out _);
        return Check(docked && context.Plans.ActivePlan.Stage == TradePlanStage.SellCommodity, "destination docking did not advance plan");
    }

    private bool ValidateDestinationTransaction()
    {
        Context context = StartPlanAtSource(out _);
        if (!context.Dealer.TryBuyCommodity(_food, 10, context.Credits, context.Cargo, out _)) return false;
        context.Dealer.SetDockedStation(_destination);
        context.Plans.NotifyDocked(_destination, out _);
        return Check(context.Dealer.TrySellCommodity(_food, 10, context.Credits, context.Cargo, out _) && context.Plans.LastCompletedPlan?.SoldQuantity == 10, "normal destination dealer transaction failed");
    }

    private bool ValidateDestinationStockIncrease()
    {
        Context context = StartPlanAtSource(out _);
        if (!context.Dealer.TryBuyCommodity(_food, 10, context.Credits, context.Cargo, out _)) return false;
        int before = DestinationListing(context).Stock;
        context.Dealer.SetDockedStation(_destination);
        context.Plans.NotifyDocked(_destination, out _);
        if (!context.Dealer.TrySellCommodity(_food, 10, context.Credits, context.Cargo, out _)) return false;
        return Check(DestinationListing(context).Stock > before, "destination stock did not increase");
    }

    private bool ValidateDestinationPriceReaction()
    {
        Context context = StartPlanAtSource(out _);
        if (!context.Dealer.TryBuyCommodity(_food, 10, context.Credits, context.Cargo, out _)) return false;
        int before = DestinationListing(context).SellPrice;
        context.Dealer.SetDockedStation(_destination);
        context.Plans.NotifyDocked(_destination, out _);
        if (!context.Dealer.TrySellCommodity(_food, 10, context.Credits, context.Cargo, out _)) return false;
        return Check(DestinationListing(context).SellPrice <= before && DestinationListing(context).SellPrice < DestinationListing(context).BaseSellPrice, "destination sell price did not respond to surplus");
    }

    private bool ValidateCreditsIncrease()
    {
        Context context = StartPlanAtSource(out _);
        int initial = context.Credits.Credits;
        if (!context.Dealer.TryBuyCommodity(_food, 10, context.Credits, context.Cargo, out _)) return false;
        int afterBuy = context.Credits.Credits;
        context.Dealer.SetDockedStation(_destination);
        context.Plans.NotifyDocked(_destination, out _);
        if (!context.Dealer.TrySellCommodity(_food, 10, context.Credits, context.Cargo, out _)) return false;
        return Check(afterBuy < initial && context.Credits.Credits > initial, "credits did not reflect profitable trade");
    }

    private bool ValidatePlanCompletion()
    {
        Context context = StartPlanAtSource(out _);
        if (!context.Dealer.TryBuyCommodity(_food, 10, context.Credits, context.Cargo, out _)) return false;
        context.Dealer.SetDockedStation(_destination);
        context.Plans.NotifyDocked(_destination, out _);
        if (!context.Dealer.TrySellCommodity(_food, 10, context.Credits, context.Cargo, out _)) return false;
        return Check(context.Plans.ActivePlan == null && context.Plans.LastCompletedPlan?.Stage == TradePlanStage.Complete, "Trade Plan did not complete");
    }

    private bool ValidateNoMissionReward()
    {
        Context context = StartPlanAtSource(out _);
        int initial = context.Credits.Credits;
        if (!context.Dealer.TryBuyCommodity(_food, 1, context.Credits, context.Cargo, out _)) return false;
        context.Dealer.SetDockedStation(_destination);
        context.Plans.NotifyDocked(_destination, out _);
        if (!context.Dealer.TrySellCommodity(_food, 1, context.Credits, context.Cargo, out _)) return false;
        return Check(context.Plans.LastCompletedPlan?.SoldQuantity == 1 && context.Missions.CompletedMissions.Count == 0 && context.Credits.Credits - initial == DestinationListing(context).SellPrice - SourceListing(context).BuyPrice, "route added a mission reward");
    }

    private bool ValidateIntermediateRecalculation()
    {
        Context context = StartPlanAtSource(out _);
        if (!context.Dealer.TryBuyCommodity(_food, 10, context.Credits, context.Cargo, out _)) return false;
        return Check(context.Plans.TryPlanNavigation(TexasSystem, out TradePlanNavigationState state, out _) && state.RemainingHopCount == 1 && state.NextTransition.TransitionId == "4:California Jump Hole", "intermediate recalculation did not resolve next leg");
    }

    private bool ValidateRemoteRecovery()
    {
        Context context = CreateContext();
        StationMarketListing before = DestinationListing(context);
        int baseline = before.BaselineStock;
        if (!context.Manager.TryRemoveSupply(_destination, _food, 100, 0, out _)) return false;
        int low = DestinationListing(context).Stock;
        context.Manager.AdvanceTime(DestinationListing(context).RecoverySeconds);
        int recovered = DestinationListing(context).Stock;
        return Check(low < baseline && recovered == baseline, "remote market did not recover to baseline");
    }

    private bool ValidateRemoteMarketSaveLoad()
    {
        Context context = CreateContext();
        if (!context.Manager.TryRemoveSupply(_destination, _food, 40, 0, out _)) return false;
        long elapsed = context.Manager.ElapsedMilliseconds;
        List<SaveMarketStateData> saved = context.Manager.CaptureRuntimeState();
        MarketManager restored = new();
        restored.RestoreElapsedMilliseconds(elapsed);
        restored.RestoreRuntimeState(saved);
        StationMarketListing listing = restored.GetListingForCommodity(_destination, _food);
        return Check(listing != null && listing.Stock == DestinationListing(context).Stock && listing.BuyPrice == DestinationListing(context).BuyPrice, "remote market runtime state did not round-trip");
    }

    private bool ValidateRemoteFreightEligibility()
    {
        Context context = CreateContext();
        if (!context.Manager.TryRemoveSupply(_destination, _food, 150, 0, out _)) return false;
        List<Mission> offers = context.Missions.CreateBoardMissions(_destination);
        return Check(offers.Any(mission => mission.Type == MissionType.FreightContract && mission.DestinationStationId == Mission.BuildStationIdentity(_destination)), "remote shortage did not create normal freight eligibility");
    }

    private bool ValidateRemoteExportEligibility()
    {
        Context context = CreateContext();
        if (!context.Manager.TryAddSupply(_destination, _food, 200, out _)) return false;
        if (!context.Manager.TryRemoveSupply(_source, _food, 300, 0, out _)) return false;
        List<Mission> offers = context.Missions.CreateBoardMissions(_destination);
        return Check(offers.Any(mission => mission.Type == MissionType.ExportContract && mission.OriginStationId == Mission.BuildStationIdentity(_destination)), "remote surplus did not create normal export eligibility");
    }

    private bool ValidateAging()
    {
        Context context = CreateObservedContext();
        context.Manager.AdvanceTime((MarketIntelligence.CurrentThresholdMilliseconds + 1) / 1000d);
        return Check(context.Intel.TryGetObservation(DestinationStationId, CommodityId, out MarketObservation observation) && observation.GetAgeBand(context.Manager.ElapsedMilliseconds) == MarketObservationAgeBand.Recent, "market intelligence age did not enter RECENT");
    }

    private bool ValidateStaleQuote()
    {
        Context context = CreateObservedContext();
        int remembered = context.Intel.KnownStations.First(station => station.StationId == DestinationStationId).Observations.First(observation => observation.Commodity.Id == CommodityId).SellPrice;
        context.Manager.AdvanceTime((MarketIntelligence.RecentThresholdMilliseconds + 1) / 1000d);
        return Check(context.Intel.TryGetObservation(DestinationStationId, CommodityId, out MarketObservation observation) && observation.SellPrice == remembered && observation.GetAgeBand(context.Manager.ElapsedMilliseconds) == MarketObservationAgeBand.Stale, "stale destination quote was refreshed or misclassified");
    }

    private bool ValidateLocalRefresh()
    {
        Context context = CreateContext();
        context.Intel.SetCurrentStation(_destination);
        int before = context.Intel.KnownStations.First(station => station.StationId == DestinationStationId).Observations.First(observation => observation.Commodity.Id == CommodityId).Stock;
        if (!context.Manager.TryAddSupply(_destination, _food, 20, out _)) return false;
        context.Intel.RefreshCurrentStation();
        int after = context.Intel.KnownStations.First(station => station.StationId == DestinationStationId).Observations.First(observation => observation.Commodity.Id == CommodityId).Stock;
        return Check(after == before + 20, "local destination observation did not refresh");
    }

    private bool ValidateRepeatedMarginChange()
    {
        Context context = CreateObservedContext();
        int before = FindOpportunity(context).CurrentSpread;
        context.Dealer.SetDockedStation(_source);
        if (!context.Dealer.TryBuyCommodity(_food, 30, context.Credits, context.Cargo, out _)) return false;
        context.Intel.ObserveStation(_source, "RepeatedRoute");
        int after = FindOpportunity(context).CurrentSpread;
        return Check(after < before, "repeated route exploitation did not reduce margin");
    }

    private bool ValidateNoNegativeStock()
    {
        Context context = CreateContext();
        StationMarketListing listing = DestinationListing(context);
        context.Manager.TryRemoveSupply(_destination, _food, listing.Stock + 1000, 0, out _);
        context.Manager.TryBuy(_destination, _food, listing.Stock + 1000, context.Credits, context.Cargo, out _);
        return Check(DestinationListing(context).Stock >= 0, "remote stock became negative");
    }

    private bool ValidateNoPriceInversion()
    {
        Context context = CreateContext();
        return Check(_stations.Where(station => context.Manager.HasMarketConfigForStation(station)).SelectMany(station => context.Manager.GetListingsForStation(station)).Where(listing => listing.IsAvailable).All(listing => listing.BuyPrice >= listing.SellPrice && listing.SellPrice > 0), "a production market price inverted");
    }

    private bool ValidateRouteNoEconomyMutation()
    {
        Context context = CreateObservedContext();
        StationMarketListing source = SourceListing(context);
        StationMarketListing destination = DestinationListing(context);
        int sourceStock = source.Stock;
        int destinationStock = destination.Stock;
        if (!context.Plans.TryCreatePlan(FindOpportunity(context), out _)) return false;
        ProductionTopologyValidator.TryGetRoute(_configuration, NewYorkSystem, CaliforniaSystem, out _);
        return Check(SourceListing(context).Stock == sourceStock && DestinationListing(context).Stock == destinationStock, "route computation mutated market stock");
    }

    private bool ValidateRouteNoCargoMutation()
    {
        Context context = CreateObservedContext();
        int cargoBefore = context.Cargo.GetCommodityQuantity(_food.Name);
        if (!context.Plans.TryCreatePlan(FindOpportunity(context), out _)) return false;
        context.Plans.TryPlanNavigation(NewYorkSystem, out _, out _);
        return Check(context.Cargo.GetCommodityQuantity(_food.Name) == cargoBefore, "route plotting mutated cargo");
    }

    private bool ValidateRouteNoMissionMutation()
    {
        Context context = CreateObservedContext();
        int missionBefore = context.Missions.ActiveMissions.Count;
        if (!context.Plans.TryCreatePlan(FindOpportunity(context), out _)) return false;
        context.Plans.TryPlanNavigation(TexasSystem, out _, out _);
        return Check(context.Missions.ActiveMissions.Count == missionBefore && context.Missions.CompletedMissions.Count == 0, "route plotting mutated mission state");
    }

    private bool ValidateOldMarketState()
    {
        Context context = CreateContext();
        MarketManager restored = new();
        restored.RestoreRuntimeState(Array.Empty<SaveMarketStateData>());
        StationMarketListing listing = restored.GetListingForCommodity(_destination, _food);
        return Check(listing != null && listing.BaseBuyPrice > 0 && listing.Stock == DestinationListing(context).BaselineStock, "old save did not initialize configured remote baseline");
    }

    private bool ValidateDeterministicScenario()
    {
        Context first = CreateObservedContext();
        Context second = CreateObservedContext();
        MarketOpportunity firstOpportunity = FindOpportunity(first);
        MarketOpportunity secondOpportunity = FindOpportunity(second);
        return Check(firstOpportunity != null && secondOpportunity != null && firstOpportunity.Score == secondOpportunity.Score && firstOpportunity.RouteDistanceUnits == secondOpportunity.RouteDistanceUnits && firstOpportunity.RouteHops == secondOpportunity.RouteHops, "production scenario was not deterministic");
    }

    private Context StartPlanAtSource(out TradePlan plan)
    {
        Context context = CreateObservedContext();
        context.Plans.TryCreatePlan(FindOpportunity(context), out _);
        context.Dealer.SetDockedStation(_source);
        context.Plans.NotifyDocked(_source, out _);
        plan = context.Plans.ActivePlan;
        return context;
    }

    private TradePlanManager RestorePlan(Context context)
    {
        TradePlanManager restored = new(context.Manager, context.Intel, context.Authority, context.Cargo, context.Credits);
        restored.RestoreState(JsonSerializer.Deserialize<SaveTradePlanData>(JsonSerializer.Serialize(context.Plans.CaptureState())));
        return restored;
    }

    private Context CreateObservedContext()
    {
        Context context = CreateContext();
        context.Intel.ObserveStation(_source, "ProductionVisit");
        context.Intel.ObserveStation(_destination, "ProductionVisit");
        return context;
    }

    private Context CreateContext()
    {
        CommodityDealer dealer = new();
        MarketManager manager = dealer.MarketManager;
        MarketIntelligence intelligence = new(manager);
        MarketRouteAuthority authority = new(_configuration.JumpHoles);
        CargoHold cargo = new(1000);
        PlayerCredits credits = new(1_000_000);
        TradePlanManager plans = new(manager, intelligence, authority, cargo, credits);
        MissionManager missions = new(credits, null, marketManager: manager, cargoHold: cargo, marketIntelligence: intelligence);
        missions.SetRouteAuthority(authority);
        MissionWorldManager world = new(missions, null, null, new List<NpcShip>(), new List<SpaceObject>(), () => _stations, marketManager: manager, marketIntelligence: intelligence);
        missions.SetWorldManager(world);
        dealer.SetMarketIntelligence(intelligence);
        dealer.TransactionCompleted += transaction => plans.ObserveTransaction(transaction, out _);
        return new Context(manager, dealer, intelligence, authority, cargo, credits, plans, missions);
    }

    private MarketOpportunity FindOpportunity(Context context)
    {
        return context.Missions.GetKnownMarketOpportunities(100).FirstOrDefault(opportunity =>
            opportunity.Type == MarketOpportunityType.TradeRoute &&
            opportunity.CommodityId == CommodityId &&
            opportunity.OriginStationId == SourceStationId &&
            opportunity.DestinationStationId == DestinationStationId);
    }

    private StationMarketListing SourceListing(Context context) => context.Manager.GetListingForCommodity(_source, _food);
    private StationMarketListing DestinationListing(Context context) => context.Manager.GetListingForCommodity(_destination, _food);
    private MarketSystemRoute ProductionRoute() => ProductionTopologyValidator.TryGetRoute(_configuration, NewYorkSystem, CaliforniaSystem, out MarketSystemRoute route) ? route : null;

    private bool HasLeg(int origin, int destination, string transitionName)
    {
        MarketSystemRoute route = ProductionRoute();
        return route != null && route.Legs.Any(leg => leg.OriginSystemIndex == origin && leg.DestinationSystemIndex == destination && leg.TransitionName == transitionName);
    }

    private SystemConfig System(int index) => _configuration.GetSystem(index);

    private static bool Same(string left, string right) => string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
    private bool Check(bool condition, string failureReason)
    {
        if (!condition)
        {
            _lastFailureReason = failureReason ?? "assertion failed";
        }

        return condition;
    }

    private void RunCase(string label, Func<bool> test, ref int passed, ref int failed)
    {
        _lastFailureReason = string.Empty;
        try
        {
            (bool success, string reason) = RunSilenced(test);
            if (success)
            {
                passed++;
                Console.WriteLine($"[PRODUCTION ROUTE SMOKE] PASS {label}");
            }
            else
            {
                failed++;
                Console.WriteLine($"[PRODUCTION ROUTE SMOKE] FAIL {label}: {(_lastFailureReason.Length > 0 ? _lastFailureReason : reason)}");
            }
        }
        catch (Exception ex)
        {
            failed++;
            Console.WriteLine($"[PRODUCTION ROUTE SMOKE] FAIL {label}: {ex.Message}");
        }
    }

    private (bool Success, string FailureReason) RunSilenced(Func<bool> test)
    {
        TextWriter original = Console.Out;
        Console.SetOut(TextWriter.Null);
        try
        {
            bool success = test();
            return (success, success ? string.Empty : (_lastFailureReason.Length > 0 ? _lastFailureReason : "assertion failed"));
        }
        finally
        {
            Console.SetOut(original);
        }
    }

    private sealed class Context
    {
        public Context(MarketManager manager, CommodityDealer dealer, MarketIntelligence intel, MarketRouteAuthority authority, CargoHold cargo, PlayerCredits credits, TradePlanManager plans, MissionManager missions)
        {
            Manager = manager;
            Dealer = dealer;
            Intel = intel;
            Authority = authority;
            Cargo = cargo;
            Credits = credits;
            Plans = plans;
            Missions = missions;
        }

        public MarketManager Manager { get; }
        public CommodityDealer Dealer { get; }
        public MarketIntelligence Intel { get; }
        public MarketRouteAuthority Authority { get; }
        public CargoHold Cargo { get; }
        public PlayerCredits Credits { get; }
        public TradePlanManager Plans { get; }
        public MissionManager Missions { get; }
    }
}
