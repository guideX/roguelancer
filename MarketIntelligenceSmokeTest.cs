using Microsoft.Xna.Framework;
using Roguelancer.Configuration;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace Roguelancer;

/// <summary>
/// Focused Phase 17 proof. Each case uses fresh runtime economy/knowledge so
/// the suite also proves that observations do not leak between game sessions.
/// </summary>
internal sealed class MarketIntelligenceSmokeTest
{
    private readonly IReadOnlyList<Station> _stations;

    public MarketIntelligenceSmokeTest(IReadOnlyList<Station> stations = null)
    {
        _stations = stations != null && RequiredStationNames.All(name => stations.Any(station => Same(station?.Name, name)))
            ? stations
            : LoadFixtureStations();
    }

    private static readonly string[] RequiredStationNames =
        { "Fort Bush", "Newark Station", "Rochester Base", "Detroit Munitions", "Buffalo Base" };

    public (int Passed, int Failed) Run()
    {
        int passed = 0;
        int failed = 0;

        RunCase("new station starts unknown", () => Check(!CreateContext().Intelligence.IsStationKnown("Newark Station"), "new station was known"), ref passed, ref failed);
        RunCase("visit records station", () => Check(Observe(CreateContext(), "Fort Bush"), "visit did not record"), ref passed, ref failed);
        RunCase("observation station id", () => Check(ObserveFood().StationId == "fort_bush", "station id mismatch"), ref passed, ref failed);
        RunCase("observation commodity id", () => Check(ObserveFood().Commodity?.Id == "food-rations", "commodity id mismatch"), ref passed, ref failed);
        RunCase("observed buy price", () => Check(ObserveFood().BuyPrice == 85, "buy price mismatch"), ref passed, ref failed);
        RunCase("observed sell price", () => Check(ObserveFood().SellPrice == 60, "sell price mismatch"), ref passed, ref failed);
        RunCase("observation timestamp", () => Check(ObserveFood().ObservedAtMilliseconds == 0, "timestamp mismatch"), ref passed, ref failed);
        RunCase("current age band", () => Check(ObserveFood().GetAgeBand(0) == MarketObservationAgeBand.Current, "not current"), ref passed, ref failed);
        RunCase("recent age band", () => Check(AgeBandAfter(16 * 60) == MarketObservationAgeBand.Recent, "not recent"), ref passed, ref failed);
        RunCase("stale age band", () => Check(AgeBandAfter(3 * 60 * 60) == MarketObservationAgeBand.Stale, "not stale"), ref passed, ref failed);
        RunCase("remote market does not refresh quote", ValidateRemoteDoesNotRefresh, ref passed, ref failed);
        RunCase("revisit refreshes quote", ValidateRevisitRefresh, ref passed, ref failed);
        RunCase("local buy refreshes quote", ValidateLocalBuyRefresh, ref passed, ref failed);
        RunCase("local sell refreshes quote", ValidateLocalSellRefresh, ref passed, ref failed);
        RunCase("freight delivery refreshes local quote", ValidateFreightDeliveryRefresh, ref passed, ref failed);
        RunCase("export delivery refreshes local quote", ValidateExportDeliveryRefresh, ref passed, ref failed);
        RunCase("unvisited prices stay hidden", () => Check(!CreateContext().Intelligence.TryGetObservation("Newark Station", "food-rations", out _), "remote quote exposed"), ref passed, ref failed);
        RunCase("mission intel is qualitative", ValidateMissionIntel, ref passed, ref failed);
        RunCase("known route generated", ValidateKnownRoute, ref passed, ref failed);
        RunCase("unknown source suppresses route", ValidateUnknownSource, ref passed, ref failed);
        RunCase("unknown destination suppresses route", ValidateUnknownDestination, ref passed, ref failed);
        RunCase("profitable spread appears", ValidateProfitableSpread, ref passed, ref failed);
        RunCase("non-profitable spread suppressed", ValidateNonProfitableSpread, ref passed, ref failed);
        RunCase("same station route suppressed", ValidateSameStationRoute, ref passed, ref failed);
        RunCase("invalid commodity excluded", ValidateInvalidCommodity, ref passed, ref failed);
        RunCase("mission-only commodity excluded", ValidateMissionOnlyCommodity, ref passed, ref failed);
        RunCase("stale route remains usable", ValidateStaleRouteUsable, ref passed, ref failed);
        RunCase("stale ranking penalty", ValidateStalePenalty, ref passed, ref failed);
        RunCase("stale quote is historical", ValidateStaleQuoteUnchanged, ref passed, ref failed);
        RunCase("route distance deterministic", ValidateRouteDistance, ref passed, ref failed);
        RunCase("distance affects score", ValidateDistanceAffectsScore, ref passed, ref failed);
        RunCase("ranking deterministic", ValidateDeterministicRanking, ref passed, ref failed);
        RunCase("opportunity list bounded", () => Check(CreateThreeKnownContext().Mission.GetKnownMarketOpportunities(100).Count <= MissionManager.MarketOpportunityMaximumEntries, "list exceeded bound"), ref passed, ref failed);
        RunCase("strongest useful route ranks", ValidateStrongestRoute, ref passed, ref failed);
        RunCase("query does not mutate economy", ValidateQueryDoesNotMutate, ref passed, ref failed);
        RunCase("save preserves observations", ValidateSaveRoundTrip, ref passed, ref failed);
        RunCase("save preserves age", ValidateSavePreservesAge, ref passed, ref failed);
        RunCase("old save without intelligence loads", ValidateOldSave, ref passed, ref failed);
        RunCase("new game clears knowledge", () => { Context c = CreateContext(); Observe(c, "Fort Bush"); c.Intelligence.Clear(); return Check(!c.Intelligence.IsStationKnown("Fort Bush"), "knowledge leaked"); }, ref passed, ref failed);
        RunCase("invalid commodity save ignored", ValidateInvalidCommoditySave, ref passed, ref failed);
        RunCase("invalid station save ignored", ValidateInvalidStationSave, ref passed, ref failed);
        RunCase("current station stays live", ValidateCurrentStationLive, ref passed, ref failed);
        RunCase("remote station stays snapshot", ValidateRemoteSnapshot, ref passed, ref failed);
        RunCase("local transaction changes route", ValidateLocalTransactionChangesRoute, ref passed, ref failed);
        RunCase("delivery changes route data", ValidateDeliveryChangesRoute, ref passed, ref failed);
        RunCase("debug opportunity scan remains available", ValidateDebugScan, ref passed, ref failed);
        RunCase("player query excludes unknown stations", ValidatePlayerQueryExcludesUnknown, ref passed, ref failed);
        RunCase("duplicate routes collapse", ValidateNoDuplicateRoutes, ref passed, ref failed);
        RunCase("large elapsed time is safe", ValidateLargeElapsedTime, ref passed, ref failed);
        RunCase("unreachable route is deterministic", ValidateUnreachableRoute, ref passed, ref failed);
        RunCase("failure paths are deterministic", ValidateFailurePaths, ref passed, ref failed);

        Console.WriteLine($"[MARKET INTELLIGENCE SMOKE] RESULT: {passed} passed, {failed} failed");
        return (passed, failed);
    }

    private void RunCase(string label, Func<(bool Success, string FailureReason)> test, ref int passed, ref int failed)
    {
        try
        {
            (bool success, string reason) = RunSilenced(test);
            if (success)
            {
                passed++;
                Console.WriteLine($"[MARKET INTELLIGENCE SMOKE] PASS {label}");
            }
            else
            {
                failed++;
                Console.WriteLine($"[MARKET INTELLIGENCE SMOKE] FAIL {label}: {reason}");
            }
        }
        catch (Exception ex)
        {
            failed++;
            Console.WriteLine($"[MARKET INTELLIGENCE SMOKE] FAIL {label}: {ex.Message}");
        }
    }

    private (bool, string) ValidateRemoteDoesNotRefresh()
    {
        Context c = CreateContext();
        Observe(c, "Fort Bush");
        int remembered = GetFoodObservation(c, "Fort Bush").BuyPrice;
        c.Manager.TryRemoveSupply(Station("Fort Bush"), Food, 100, 0, out _);
        return Check(GetFoodObservation(c, "Fort Bush").BuyPrice == remembered, "remote mutation changed quote");
    }

    private (bool, string) ValidateRevisitRefresh()
    {
        Context c = CreateContext();
        Station fort = Station("Fort Bush");
        c.Intelligence.ObserveStation(fort);
        int remembered = GetFoodObservation(c, "Fort Bush").BuyPrice;
        c.Manager.TryRemoveSupply(fort, Food, 100, 0, out _);
        c.Intelligence.ObserveStation(fort);
        return Check(GetFoodObservation(c, "Fort Bush").BuyPrice > remembered, "revisit did not refresh price");
    }

    private (bool, string) ValidateLocalBuyRefresh()
    {
        (CommodityDealer dealer, MarketIntelligence intel, Station station) = CreateDealer("Fort Bush");
        int before = GetFoodObservation(intel, "Fort Bush").BuyPrice;
        PlayerCredits credits = new(100_000);
        CargoHold cargo = new(500);
        if (!dealer.TryBuyCommodity(Food, 100, credits, cargo, out string message)) return Fail(message);
        return Check(GetFoodObservation(intel, "Fort Bush").BuyPrice == dealer.CurrentMarketListings.First(l => l.Commodity.Id == Food.Id).BuyPrice, "buy observation did not refresh");
    }

    private (bool, string) ValidateLocalSellRefresh()
    {
        (CommodityDealer dealer, MarketIntelligence intel, Station station) = CreateDealer("Newark Station");
        CargoHold cargo = new(500);
        cargo.AddCommodity(Food, 10);
        if (!dealer.TrySellCommodity(Food, 10, new PlayerCredits(0), cargo, out string message)) return Fail(message);
        return Check(GetFoodObservation(intel, "Newark Station").SellPrice == dealer.CurrentMarketListings.First(l => l.Commodity.Id == Food.Id).SellPrice, "sell observation did not refresh");
    }

    private (bool, string) ValidateFreightDeliveryRefresh()
    {
        Context c = CreateContext();
        Station destination = Station("Newark Station");
        c.Intelligence.SetCurrentStation(destination);
        int before = GetFoodObservation(c, destination.Name).Stock;
        if (!c.Manager.TryAddSupply(destination, Food, 10, out string message)) return Fail(message);
        c.Intelligence.ObserveStation(destination, "CurrentStation");
        return Check(GetFoodObservation(c, destination.Name).Stock == before + 10, "freight delivery not observed");
    }

    private (bool, string) ValidateExportDeliveryRefresh()
    {
        Context c = CreateContext();
        Station destination = Station("Newark Station");
        c.Intelligence.SetCurrentStation(destination);
        int before = GetFoodObservation(c, destination.Name).Stock;
        if (!c.Manager.TryAddSupply(destination, Food, 5, out string message)) return Fail(message);
        c.Intelligence.ObserveStation(destination, "CurrentStation");
        return Check(GetFoodObservation(c, destination.Name).Stock == before + 5, "export delivery not observed");
    }

    private (bool, string) ValidateMissionIntel()
    {
        Context c = CreateContext();
        Mission mission = Mission.CreateFreightContract(Food, Station("Newark Station"), 10, 500, 1);
        c.Intelligence.RecordMissionIntel(mission);
        bool qualitative = !c.Intelligence.IsStationKnown("Newark Station") &&
            c.Intelligence.MissionIntel.Any(intel => intel.StationId == "newark_station" && intel.Condition == "SHORTAGE");
        return Check(qualitative && !c.Intelligence.TryGetObservation("Newark Station", Food.Id, out _), "mission intel exposed full market");
    }

    private (bool, string) ValidateKnownRoute()
    {
        Context c = CreateContext();
        Observe(c, "Fort Bush");
        Observe(c, "Newark Station");
        MarketOpportunity route = c.Mission.GetKnownMarketOpportunities().FirstOrDefault(opportunity => opportunity.Type == MarketOpportunityType.TradeRoute);
        return Check(route != null && route.CurrentSpread == 30 && route.SourceAgeBand == "CURRENT" && route.DestinationAgeBand == "CURRENT", "known route was not generated");
    }

    private (bool, string) ValidateUnknownSource()
    {
        Context c = CreateContext();
        Observe(c, "Newark Station");
        return Check(!c.Mission.GetKnownMarketOpportunities().Any(opportunity => opportunity.Type == MarketOpportunityType.TradeRoute), "unknown source generated route");
    }

    private (bool, string) ValidateUnknownDestination()
    {
        Context c = CreateContext();
        Observe(c, "Fort Bush");
        return Check(!c.Mission.GetKnownMarketOpportunities().Any(opportunity => opportunity.Type == MarketOpportunityType.TradeRoute), "unknown destination generated route");
    }

    private (bool, string) ValidateProfitableSpread() => ValidateKnownRoute();

    private (bool, string) ValidateNonProfitableSpread()
    {
        Context c = CreateContext();
        Observe(c, "Newark Station");
        Observe(c, "Fort Bush");
        return Check(!c.Mission.GetKnownMarketOpportunities().Any(opportunity => opportunity.Type == MarketOpportunityType.TradeRoute && opportunity.OriginStationName == "Newark Station" && opportunity.DestinationStationName == "Fort Bush"), "non-profitable route appeared");
    }

    private (bool, string) ValidateSameStationRoute()
    {
        Context c = CreateContext();
        Observe(c, "Fort Bush");
        return Check(!c.Mission.GetKnownMarketOpportunities().Any(opportunity => opportunity.Type == MarketOpportunityType.TradeRoute && opportunity.OriginStationName == opportunity.DestinationStationName), "same station route appeared");
    }

    private (bool, string) ValidateInvalidCommodity()
    {
        Context c = CreateContext();
        List<SaveMarketIntelligenceData> state = c.Intelligence.CaptureState();
        state.Add(new SaveMarketIntelligenceData { StationId = "fort_bush", StationName = "Fort Bush", CommodityId = "deleted-commodity", BuyPrice = 1, SellPrice = 1, Stock = 1, BaselineStock = 1 });
        c.Intelligence.RestoreState(state);
        return Check(!c.Intelligence.TryGetObservation("Fort Bush", "deleted-commodity", out _), "invalid commodity survived");
    }

    private (bool, string) ValidateMissionOnlyCommodity()
    {
        Context c = CreateContext();
        c.Intelligence.ObserveStation(Station("Fort Bush"));
        return Check(!c.Intelligence.TryGetObservation("Fort Bush", "sealed-data-package", out _), "mission-only commodity became market knowledge");
    }

    private (bool, string) ValidateStaleRouteUsable()
    {
        Context c = CreateContext();
        Observe(c, "Fort Bush"); Observe(c, "Newark Station");
        c.Manager.AdvanceTime(3 * 60 * 60);
        MarketOpportunity route = c.Mission.GetKnownMarketOpportunities().FirstOrDefault(opportunity => opportunity.Type == MarketOpportunityType.TradeRoute);
        return Check(route != null && route.SourceAgeBand == "STALE", "stale route was hidden");
    }

    private (bool, string) ValidateStalePenalty()
    {
        Context current = CreateContext(); Observe(current, "Fort Bush"); Observe(current, "Newark Station");
        int currentScore = current.Mission.GetKnownMarketOpportunities().First(opportunity => opportunity.Type == MarketOpportunityType.TradeRoute).Score;
        Context stale = CreateContext(); Observe(stale, "Fort Bush"); Observe(stale, "Newark Station"); stale.Manager.AdvanceTime(3 * 60 * 60);
        int staleScore = stale.Mission.GetKnownMarketOpportunities().First(opportunity => opportunity.Type == MarketOpportunityType.TradeRoute).Score;
        return Check(staleScore < currentScore, $"stale score {staleScore} was not below current {currentScore}");
    }

    private (bool, string) ValidateStaleQuoteUnchanged()
    {
        Context c = CreateContext(); Observe(c, "Fort Bush");
        int remembered = GetFoodObservation(c, "Fort Bush").BuyPrice;
        c.Manager.AdvanceTime(4 * 60 * 60);
        c.Manager.TryRemoveSupply(Station("Fort Bush"), Food, 100, 0, out _);
        return Check(GetFoodObservation(c, "Fort Bush").BuyPrice == remembered, "stale quote changed");
    }

    private (bool, string) ValidateRouteDistance()
    {
        Context c = CreateThreeKnownContext();
        MarketKnowledgeStation fort = c.Intelligence.KnownStations.First(s => s.StationName == "Fort Bush");
        MarketKnowledgeStation newark = c.Intelligence.KnownStations.First(s => s.StationName == "Newark Station");
        return Check(c.Route.TryGetRoute(fort, newark, out MarketRouteMetric route) && route.JumpCount == 0 && route.DistanceUnits > 0, "route metric invalid");
    }

    private (bool, string) ValidateDistanceAffectsScore()
    {
        Context c = CreateThreeKnownContext();
        IReadOnlyList<MarketOpportunity> routes = c.Mission.GetKnownMarketOpportunities(8).Where(o => o.Type == MarketOpportunityType.TradeRoute).ToList();
        MarketOpportunity near = routes.FirstOrDefault(o => o.OriginStationName == "Fort Bush" && o.DestinationStationName == "Newark Station");
        MarketOpportunity far = routes.FirstOrDefault(o => o.OriginStationName == "Rochester Base" && o.DestinationStationName == "Newark Station");
        return Check(near != null && far != null && near.RouteDistanceUnits != far.RouteDistanceUnits && near.Score != far.Score, "distance did not affect route score");
    }

    private (bool, string) ValidateDeterministicRanking()
    {
        Context a = CreateThreeKnownContext();
        Context b = CreateThreeKnownContext();
        string left = string.Join("|", a.Mission.GetKnownMarketOpportunities().Select(o => o.GetDisplayText()));
        string right = string.Join("|", b.Mission.GetKnownMarketOpportunities().Select(o => o.GetDisplayText()));
        return Check(left == right, "ranking was not deterministic");
    }

    private (bool, string) ValidateStrongestRoute()
    {
        Context c = CreateContext(); Observe(c, "Fort Bush"); Observe(c, "Newark Station");
        MarketOpportunity top = c.Mission.GetKnownMarketOpportunities().FirstOrDefault();
        return Check(top?.Type == MarketOpportunityType.TradeRoute && top.OriginStationName == "Fort Bush" && top.DestinationStationName == "Newark Station", "best useful route was not first");
    }

    private (bool, string) ValidateQueryDoesNotMutate()
    {
        Context c = CreateContext(); Observe(c, "Fort Bush"); Observe(c, "Newark Station");
        int before = c.Manager.GetListingForCommodity(Station("Fort Bush"), Food).Stock;
        c.Mission.GetKnownMarketOpportunities();
        int after = c.Manager.GetListingForCommodity(Station("Fort Bush"), Food).Stock;
        return Check(before == after, "knowledge query mutated market");
    }

    private (bool, string) ValidateSaveRoundTrip()
    {
        Context c = CreateContext(); Observe(c, "Fort Bush"); Observe(c, "Newark Station");
        List<SaveMarketIntelligenceData> state = c.Intelligence.CaptureState();
        Context restored = CreateContext(); restored.Manager.RestoreElapsedMilliseconds(c.Manager.ElapsedMilliseconds); restored.Intelligence.RestoreState(state);
        return Check(restored.Intelligence.TryGetObservation("Fort Bush", Food.Id, out MarketObservation observation) && observation.BuyPrice == 85, "observation did not round-trip");
    }

    private (bool, string) ValidateSavePreservesAge()
    {
        Context c = CreateContext(); Observe(c, "Fort Bush"); c.Manager.AdvanceTime(3 * 60 * 60);
        List<SaveMarketIntelligenceData> state = c.Intelligence.CaptureState();
        Context restored = CreateContext(); restored.Manager.RestoreElapsedMilliseconds(c.Manager.ElapsedMilliseconds); restored.Intelligence.RestoreState(state);
        return Check(restored.Intelligence.TryGetObservation("Fort Bush", Food.Id, out MarketObservation observation) && observation.GetAgeBand(restored.Manager.ElapsedMilliseconds) == MarketObservationAgeBand.Stale, "age did not round-trip");
    }

    private (bool, string) ValidateOldSave()
    {
        Context c = CreateContext();
        c.Intelligence.RestoreState(null);
        return Check(c.Intelligence.KnownStations.Count == 0, "old save initialized omniscient knowledge");
    }

    private (bool, string) ValidateInvalidCommoditySave()
    {
        Context c = CreateContext();
        c.Intelligence.RestoreState(new[] { new SaveMarketIntelligenceData { StationId = "fort_bush", CommodityId = "missing", Stock = 1, BuyPrice = 1, SellPrice = 1, BaselineStock = 1 } });
        return Check(c.Intelligence.KnownStations.Count == 0, "missing commodity save was not discarded");
    }

    private (bool, string) ValidateInvalidStationSave()
    {
        Context c = CreateContext();
        c.Intelligence.RestoreState(new[] { new SaveMarketIntelligenceData { StationId = "deleted_station", CommodityId = Food.Id, Stock = 1, BuyPrice = 1, SellPrice = 1, BaselineStock = 1 } });
        return Check(c.Intelligence.KnownStations.Count == 0, "missing station save was not discarded");
    }

    private (bool, string) ValidateCurrentStationLive()
    {
        Context c = CreateContext();
        Station fort = Station("Fort Bush"); c.Intelligence.SetCurrentStation(fort);
        int before = GetFoodObservation(c, fort.Name).BuyPrice;
        c.Manager.TryRemoveSupply(fort, Food, 100, 0, out _);
        c.Intelligence.RefreshCurrentStation();
        return Check(GetFoodObservation(c, fort.Name).BuyPrice > before, "current station stayed historical");
    }

    private (bool, string) ValidateRemoteSnapshot()
    {
        Context c = CreateContext(); Station fort = Station("Fort Bush"); c.Intelligence.ObserveStation(fort);
        int before = GetFoodObservation(c, fort.Name).Stock; c.Manager.TryRemoveSupply(fort, Food, 10, 0, out _);
        return Check(GetFoodObservation(c, fort.Name).Stock == before, "remote snapshot changed");
    }

    private (bool, string) ValidateLocalTransactionChangesRoute()
    {
        (CommodityDealer dealer, MarketIntelligence intel, Station station) = CreateDealer("Fort Bush");
        Observe(intel, "Newark Station");
        int before = dealer.CurrentMarketListings.First(l => l.Commodity.Id == Food.Id).BuyPrice;
        dealer.TryBuyCommodity(Food, 100, new PlayerCredits(100_000), new CargoHold(500), out _);
        int after = dealer.CurrentMarketListings.First(l => l.Commodity.Id == Food.Id).BuyPrice;
        return Check(after > before && GetFoodObservation(intel, station.Name).BuyPrice == after, "local transaction did not refresh route source");
    }

    private (bool, string) ValidateDeliveryChangesRoute()
    {
        Context c = CreateContext(); Observe(c, "Fort Bush"); Observe(c, "Newark Station");
        MarketOpportunity before = c.Mission.GetKnownMarketOpportunities().First(o => o.Type == MarketOpportunityType.TradeRoute && o.CommodityId == Food.Id && o.OriginStationName == "Fort Bush" && o.DestinationStationName == "Newark Station");
        Station newark = Station("Newark Station"); c.Manager.TryAddSupply(newark, Food, 50, out _); c.Intelligence.ObserveStation(newark, "CurrentStation");
        MarketOpportunity after = c.Mission.GetKnownMarketOpportunities().First(o => o.Type == MarketOpportunityType.TradeRoute && o.CommodityId == Food.Id && o.OriginStationName == "Fort Bush" && o.DestinationStationName == "Newark Station");
        StationMarketListing live = c.Manager.GetListingForCommodity(newark, Food);
        return Check(after.CurrentSpread < before.CurrentSpread, $"delivery did not refresh destination quote ({before.CurrentSpread}->{after.CurrentSpread}; live {live?.BuyPrice}/{live?.SellPrice} stock {live?.Stock}; remembered {GetFoodObservation(c, newark.Name)?.BuyPrice}/{GetFoodObservation(c, newark.Name)?.SellPrice})");
    }

    private (bool, string) ValidateDebugScan()
    {
        MarketManager manager = new();
        MissionManager missions = new(new PlayerCredits(1000), null, null, manager, new CargoHold(100));
        return Check(missions.GetMarketOpportunities().Count == 0, "unbound debug scan was not safely bounded");
    }

    private (bool, string) ValidatePlayerQueryExcludesUnknown()
    {
        Context c = CreateContext(); Observe(c, "Fort Bush");
        return Check(c.Mission.GetKnownMarketOpportunities().All(o => o.DestinationStationName != "Newark Station"), "unknown station appeared in player query");
    }

    private (bool, string) ValidateNoDuplicateRoutes()
    {
        Context c = CreateThreeKnownContext();
        List<MarketOpportunity> routes = c.Mission.GetKnownMarketOpportunities(8).Where(o => o.Type == MarketOpportunityType.TradeRoute).ToList();
        return Check(routes.Select(o => $"{o.CommodityId}|{o.OriginStationId}|{o.DestinationStationId}").Distinct(StringComparer.OrdinalIgnoreCase).Count() == routes.Count, "duplicate route appeared");
    }

    private (bool, string) ValidateLargeElapsedTime()
    {
        Context c = CreateContext(); Observe(c, "Fort Bush"); c.Manager.AdvanceTime(1_000_000_000_000d);
        return Check(GetFoodObservation(c, "Fort Bush").GetAgeMilliseconds(c.Manager.ElapsedMilliseconds) >= 0, "age overflowed");
    }

    private (bool, string) ValidateUnreachableRoute()
    {
        MarketKnowledgeStation a = new("a", "A", 1, Vector3.Zero);
        MarketKnowledgeStation b = new("b", "B", 2, Vector3.One);
        return Check(!new MarketRouteAuthority().TryGetRoute(a, b, out _), "unreachable route was fabricated");
    }

    private (bool, string) ValidateFailurePaths()
    {
        Context c = CreateContext();
        bool invalidVisit = !c.Intelligence.ObserveStation(new Station(new StationConfig { Description = "Unknown", SystemIndex = 1 }, null));
        bool invalidQuery = !c.Intelligence.TryGetObservation("Unknown", Food.Id, out _);
        return Check(invalidVisit && invalidQuery, "invalid path was not deterministic");
    }

    private MarketObservationAgeBand AgeBandAfter(double seconds)
    {
        Context c = CreateContext(); Observe(c, "Fort Bush"); c.Manager.AdvanceTime(seconds);
        return GetFoodObservation(c, "Fort Bush").GetAgeBand(c.Manager.ElapsedMilliseconds);
    }

    private MarketObservation ObserveFood()
    {
        Context c = CreateContext();
        Observe(c, "Fort Bush");
        return GetFoodObservation(c, "Fort Bush");
    }

    private Context CreateThreeKnownContext()
    {
        Context c = CreateContext();
        Observe(c, "Fort Bush"); Observe(c, "Newark Station"); Observe(c, "Rochester Base");
        return c;
    }

    private Context CreateContext()
    {
        MarketManager manager = new();
        MarketIntelligence intelligence = new(manager);
        MissionManager mission = new(new PlayerCredits(100_000), null, null, manager, new CargoHold(500), intelligence);
        mission.SetRouteAuthority(new MarketRouteAuthority());
        return new Context(manager, intelligence, mission, new MarketRouteAuthority());
    }

    private (CommodityDealer Dealer, MarketIntelligence Intelligence, Station Station) CreateDealer(string stationName)
    {
        CommodityDealer dealer = new();
        MarketIntelligence intelligence = new(dealer.MarketManager);
        dealer.SetMarketIntelligence(intelligence);
        Station station = Station(stationName);
        dealer.SetDockedStation(station);
        return (dealer, intelligence, station);
    }

    private bool Observe(Context context, string stationName) => Observe(context.Intelligence, stationName);
    private bool Observe(MarketIntelligence intelligence, string stationName) => intelligence.ObserveStation(Station(stationName));

    private MarketObservation GetFoodObservation(Context context, string stationName) => GetFoodObservation(context.Intelligence, stationName);
    private MarketObservation GetFoodObservation(MarketIntelligence intelligence, string stationName)
    {
        return intelligence.TryGetObservation(stationName, Food.Id, out MarketObservation observation) ? observation : null;
    }

    private Station Station(string name) => _stations.First(station => Same(station.Name, name));

    private static Commodity Food => CommodityCatalog.GetById("food-rations");

    private sealed record Context(MarketManager Manager, MarketIntelligence Intelligence, MissionManager Mission, MarketRouteAuthority Route);

    private static bool Same(string left, string right) => string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
    private static (bool, string) Check(bool value, string reason) => value ? Pass() : Fail(reason);
    private static (bool, string) Pass() => (true, string.Empty);
    private static (bool, string) Fail(string reason) => (false, reason ?? "failed");

    private static T RunSilenced<T>(Func<T> action)
    {
        TextWriter original = Console.Out;
        try { Console.SetOut(TextWriter.Null); return action(); }
        finally { Console.SetOut(original); }
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
}
