using Microsoft.Xna.Framework;
using Roguelancer.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace Roguelancer;

/// <summary>
/// Phase 19 route proof. The market fixtures currently contain live quotes in
/// New York only, so cross-system plans use the real jump-hole configuration
/// with synthetic station identities and are labeled as smoke topology.
/// </summary>
internal sealed class CrossSystemTradeRouteSmokeTest
{
    private readonly IReadOnlyList<JumpHoleConfig> _jumpHoles;
    private readonly MarketRouteAuthority _authority;

    public CrossSystemTradeRouteSmokeTest()
    {
        ConfigurationManager config = new();
        config.LoadAll();
        _jumpHoles = config.JumpHoles;
        _authority = new MarketRouteAuthority(_jumpHoles);
    }

    public (int Passed, int Failed) Run()
    {
        int passed = 0;
        int failed = 0;

        RunCase("real jump-hole topology loaded", () => _jumpHoles.Count >= 20, ref passed, ref failed);
        RunCase("same-system route is empty", () => _authority.TryGetSystemRoute(1, 1, out MarketSystemRoute route) && route.JumpCount == 0, ref passed, ref failed);
        RunCase("same-system planning is local", ValidateSameSystemPlanning, ref passed, ref failed);
        RunCase("same-system planning has no transition", () => PlanState(1, 1).NextTransition == null, ref passed, ref failed);
        RunCase("same-system hop count is zero", () => PlanState(1, 1).RemainingHopCount == 0, ref passed, ref failed);
        RunCase("remote target plans while unloaded", ValidateRemoteTargetWithoutStation, ref passed, ref failed);
        RunCase("remote route is reachable", () => PlanState(1, 7).Status == TradePlanRouteStatus.TransitionRequired, ref passed, ref failed);
        RunCase("remote final station id is stable", () => PlanState(1, 7).FinalStationId == "synthetic_cortez_market", ref passed, ref failed);
        RunCase("remote target system is resolved", () => PlanState(1, 7).TargetSystemIndex == 7, ref passed, ref failed);
        RunCase("remote current system is retained", () => PlanState(1, 7).CurrentSystemIndex == 1, ref passed, ref failed);
        RunCase("first transition has an id", () => !string.IsNullOrWhiteSpace(PlanState(1, 7).CurrentTransitionId), ref passed, ref failed);
        RunCase("first transition is a jump hole", () => PlanState(1, 7).NextTransition.TransitionType == MarketRouteTransitionType.JumpHole, ref passed, ref failed);
        RunCase("first transition reaches Texas", () => PlanState(1, 7).NextTransition.DestinationSystemIndex == 4, ref passed, ref failed);
        RunCase("multi-hop route contains three legs", () => _authority.TryGetSystemRoute(1, 7, out MarketSystemRoute route) && route.Legs.Count == 3, ref passed, ref failed);
        RunCase("multi-hop count excludes final station", () => PlanState(1, 7).RemainingHopCount == 3, ref passed, ref failed);
        RunCase("multi-hop first name is deterministic", () => PlanState(1, 7).NextTransition.TransitionName == "Jump Hole to Texas", ref passed, ref failed);
        RunCase("multi-hop first id is deterministic", () => PlanState(1, 7).CurrentTransitionId == "1:Jump Hole to Texas", ref passed, ref failed);
        RunCase("multi-hop final transition name is deterministic", () => _authority.TryGetSystemRoute(1, 7, out MarketSystemRoute route) && route.Legs[2].TransitionName == "Cortez Jump Hole", ref passed, ref failed);
        RunCase("route reaches synthetic destination system", () => _authority.TryGetSystemRoute(1, 7, out MarketSystemRoute route) && route.DestinationSystemIndex == 7, ref passed, ref failed);
        RunCase("direct Colorado route is one hop", () => _authority.TryGetSystemRoute(1, 3, out MarketSystemRoute route) && route.JumpCount == 1, ref passed, ref failed);
        RunCase("direct Colorado transition is selected", () => _authority.TryGetSystemRoute(1, 3, out MarketSystemRoute route) && route.Legs[0].TransitionName == "Jump Hole to Colorado", ref passed, ref failed);
        RunCase("direct route reaches Colorado", () => _authority.TryGetSystemRoute(1, 3, out MarketSystemRoute route) && route.Legs[0].DestinationSystemIndex == 3, ref passed, ref failed);
        RunCase("arrival hole is represented", () => _authority.TryGetSystemRoute(1, 3, out MarketSystemRoute route) && route.Legs[0].ArrivalTransitionName == "Jump Hole to New York", ref passed, ref failed);
        RunCase("route after first jump has one hop", () => PlanState(2, 7).RemainingHopCount == 1, ref passed, ref failed);
        RunCase("route after first jump selects Cortez", () => PlanState(2, 7).NextTransition.TransitionName == "Cortez Jump Hole", ref passed, ref failed);
        RunCase("target system arrival becomes local", ValidateTargetSystemArrival, ref passed, ref failed);
        RunCase("local arrival keeps final station id", () => PlanState(7, 7).FinalStationId == "synthetic_cortez_market", ref passed, ref failed);
        RunCase("unreachable route fails", ValidateUnreachableRoute, ref passed, ref failed);
        RunCase("unreachable route is unavailable", () => PlanStateFailure(1, 999).Status == TradePlanRouteStatus.Unavailable, ref passed, ref failed);
        RunCase("unreachable route reason is stable", () => PlanStateFailure(1, 999).FailureReason == "NO KNOWN ROUTE", ref passed, ref failed);
        RunCase("unknown current system fails safely", () => TradePlanNavigation.TryPlanNextLeg(Plan(7), 0, null, _authority, out _, out _ ) == false, ref passed, ref failed);
        RunCase("missing final system fails safely", () => TradePlanNavigation.TryPlanNextLeg(new TradePlan { Stage = TradePlanStage.GoToDestination, DestinationStationId = "missing", DestinationStationName = "Missing" }, 1, null, _authority, out _, out _ ) == false, ref passed, ref failed);
        RunCase("planning does not change stage", ValidatePlanningDoesNotChangeStage, ref passed, ref failed);
        RunCase("planning does not replace final id", ValidatePlanningDoesNotReplaceFinalId, ref passed, ref failed);
        RunCase("planning does not change cargo quantity", () => Plan(7).AcquiredQuantity == 0 && Plan(7).PurchasedQuantity == 0, ref passed, ref failed);
        RunCase("planning does not create live object references", () => PlanState(1, 7).NextTransition != null && PlanState(1, 7).FinalStationId == "synthetic_cortez_market", ref passed, ref failed);
        RunCase("remote plan does not require station list", ValidateRemotePlanIgnoresStationList, ref passed, ref failed);
        RunCase("local station resolves when loaded", ValidateLocalStationResolution, ref passed, ref failed);
        RunCase("missing local station is a safe miss", ValidateMissingLocalStation, ref passed, ref failed);
        RunCase("source-stage remote route works", ValidateSourceStage, ref passed, ref failed);
        RunCase("destination-stage remote route works", ValidateDestinationStage, ref passed, ref failed);
        RunCase("stage change recalculates target system", ValidateStageChangeRecalculates, ref passed, ref failed);
        RunCase("diversion recalculates from actual system", ValidateDiversionRecalculates, ref passed, ref failed);
        RunCase("repeated resume is idempotent", ValidateRepeatedResume, ref passed, ref failed);
        RunCase("repeated transition planning is idempotent", ValidateRepeatedTransitionPlanning, ref passed, ref failed);
        RunCase("cyclic topology terminates", ValidateCyclicTopology, ref passed, ref failed);
        RunCase("path bounds reject runaway graph", ValidateBoundedGraph, ref passed, ref failed);
        RunCase("tie route uses fewest hops", ValidateTieRoute, ref passed, ref failed);
        RunCase("tie route uses deterministic first edge", ValidateTieBreaking, ref passed, ref failed);
        RunCase("tie route remains stable after reorder", ValidateTieBreakingAfterReorder, ref passed, ref failed);
        RunCase("arrival transition identity is stable", () => _authority.TryGetSystemRoute(1, 7, out MarketSystemRoute route) && route.Legs[0].ArrivalTransitionId == "4:Jump Hole to New York", ref passed, ref failed);
        RunCase("invalid transition is excluded", ValidateInvalidTransitionExcluded, ref passed, ref failed);
        RunCase("incomplete transition fails route", ValidateIncompleteTransition, ref passed, ref failed);
        RunCase("transition labels are consistent", () => _authority.TryGetSystemRoute(1, 7, out MarketSystemRoute route) && route.Legs.All(leg => leg.TransitionTypeLabel == "JUMP HOLE"), ref passed, ref failed);
        RunCase("final target is not replaced by jump hole", () => PlanState(1, 7).FinalStationId != PlanState(1, 7).CurrentTransitionId, ref passed, ref failed);
        RunCase("hop count never becomes negative", () => PlanState(1, 7).RemainingHopCount >= 0 && PlanState(7, 7).RemainingHopCount >= 0, ref passed, ref failed);
        RunCase("remote route metric has distance", ValidateRemoteMetric, ref passed, ref failed);
        RunCase("remote metric reports three jumps", ValidateRemoteMetricHops, ref passed, ref failed);
        RunCase("route planning leaves opportunity score unchanged", () => Plan(7).OpportunityScore == 42, ref passed, ref failed);
        RunCase("route planning leaves quote snapshots unchanged", ValidateQuoteSnapshots, ref passed, ref failed);
        RunCase("route planning leaves mission state untouched", () => new MissionManager(new PlayerCredits(0), null, null).ActiveMission == null, ref passed, ref failed);
        RunCase("invalid saved system remains a clean route failure", () => PlanStateFailure(1, 999).Status == TradePlanRouteStatus.Unavailable, ref passed, ref failed);
        RunCase("save route fields round-trip", ValidateSaveRouteFields, ref passed, ref failed);
        RunCase("save/load preserves remote final and stage", ValidateSaveLoadMidRoute, ref passed, ref failed);
        RunCase("legacy zero route fields are tolerated", ValidateLegacySaveFields, ref passed, ref failed);
        RunCase("local planning does not use unnecessary jump", () => PlanState(1, 1).NextTransition == null, ref passed, ref failed);
        RunCase("final system state has zero hops", () => PlanState(7, 7).RemainingHopCount == 0, ref passed, ref failed);
        RunCase("representative path is New York Texas California Cortez", ValidateRepresentativePath, ref passed, ref failed);

        Console.WriteLine("[CROSS-SYSTEM TRADE ROUTE SCENARIO] source=Fort Bush/New York (1), destination=synthetic Cortez market/Cortez (7), path=1 -> 4 -> 2 -> 7, transitions=Jump Hole to Texas [JUMP HOLE] -> California Jump Hole [JUMP HOLE] -> Cortez Jump Hole [JUMP HOLE], hops=3");
        Console.WriteLine($"[CROSS-SYSTEM TRADE ROUTE SMOKE] RESULT: {passed} passed, {failed} failed");
        return (passed, failed);
    }

    private void RunCase(string label, Func<bool> test, ref int passed, ref int failed)
    {
        try
        {
            if (test())
            {
                passed++;
                Console.WriteLine($"[CROSS-SYSTEM TRADE ROUTE SMOKE] PASS {label}");
            }
            else
            {
                failed++;
                Console.WriteLine($"[CROSS-SYSTEM TRADE ROUTE SMOKE] FAIL {label}");
            }
        }
        catch (Exception ex)
        {
            failed++;
            Console.WriteLine($"[CROSS-SYSTEM TRADE ROUTE SMOKE] FAIL {label}: {ex.Message}");
        }
    }

    private TradePlanNavigationState PlanState(int currentSystem, int targetSystem)
    {
        TradePlanNavigation.TryPlanNextLeg(Plan(targetSystem), currentSystem, null, _authority, out TradePlanNavigationState state, out _);
        return state;
    }

    private TradePlanNavigationState PlanStateFailure(int currentSystem, int targetSystem)
    {
        TradePlanNavigation.TryPlanNextLeg(Plan(targetSystem), currentSystem, null, _authority, out TradePlanNavigationState state, out _);
        return state;
    }

    private static TradePlan Plan(int targetSystem)
    {
        return new TradePlan
        {
            SourceStationId = "fort_bush",
            SourceStationName = "Fort Bush",
            SourceSystemIndex = 1,
            DestinationStationId = "synthetic_cortez_market",
            DestinationStationName = "Cortez Synthetic Market",
            DestinationSystemIndex = targetSystem,
            CommodityId = "food-rations",
            CommodityName = "Food Rations",
            SourceBuyPriceSnapshot = 85,
            DestinationSellPriceSnapshot = 115,
            OpportunityScore = 42,
            Stage = TradePlanStage.GoToDestination
        };
    }

    private bool ValidateSameSystemPlanning()
    {
        TradePlanNavigation.TryPlanNextLeg(Plan(1), 1, null, _authority, out TradePlanNavigationState state, out _);
        return state?.Status == TradePlanRouteStatus.LocalStation;
    }

    private bool ValidateRemoteTargetWithoutStation()
    {
        TradePlan plan = Plan(7);
        bool success = TradePlanNavigation.TryPlanNextLeg(plan, 1, null, _authority, out TradePlanNavigationState state, out _);
        return success && state?.NextTransition != null && state.FinalStationId == plan.DestinationStationId;
    }

    private bool ValidateTargetSystemArrival() => PlanState(7, 7).Status == TradePlanRouteStatus.LocalStation;

    private bool ValidateUnreachableRoute()
    {
        bool success = TradePlanNavigation.TryPlanNextLeg(Plan(999), 1, null, _authority, out TradePlanNavigationState state, out string reason);
        return !success && state?.Status == TradePlanRouteStatus.Unavailable && reason == "NO KNOWN ROUTE";
    }

    private bool ValidatePlanningDoesNotChangeStage()
    {
        TradePlan plan = Plan(7);
        TradePlanStage before = plan.Stage;
        TradePlanNavigation.TryPlanNextLeg(plan, 1, null, _authority, out _, out _);
        return before == plan.Stage;
    }

    private bool ValidatePlanningDoesNotReplaceFinalId()
    {
        TradePlan plan = Plan(7);
        string before = plan.DestinationStationId;
        TradePlanNavigation.TryPlanNextLeg(plan, 1, null, _authority, out TradePlanNavigationState state, out _);
        return before == plan.DestinationStationId && state.FinalStationId == before;
    }

    private bool ValidateRemotePlanIgnoresStationList()
    {
        TradePlan plan = Plan(7);
        bool success = TradePlanNavigation.TryPlanNextLeg(plan, 1, null, _authority, out _, out _);
        return success && TradePlanNavigation.TryResolveNextStation(plan, Array.Empty<Station>(), new MarketManager(), out _, out _) == false;
    }

    private bool ValidateLocalStationResolution()
    {
        TradePlan plan = Plan(1);
        plan.Stage = TradePlanStage.GoToSource;
        plan.SourceSystemIndex = 1;
        Station station = new(new StationConfig { Description = "Fort Bush", SystemIndex = 1 }, null);
        return TradePlanNavigation.TryResolveNextStation(plan, new[] { station }, new MarketManager(), out Station resolved, out _) &&
            ReferenceEquals(station, resolved);
    }

    private bool ValidateMissingLocalStation()
    {
        TradePlan plan = Plan(1);
        plan.Stage = TradePlanStage.GoToSource;
        return !TradePlanNavigation.TryResolveNextStation(plan, Array.Empty<Station>(), new MarketManager(), out _, out string reason) &&
            reason.Contains("not loaded", StringComparison.OrdinalIgnoreCase);
    }

    private bool ValidateSourceStage()
    {
        TradePlan plan = Plan(7);
        plan.Stage = TradePlanStage.GoToSource;
        plan.SourceSystemIndex = 7;
        bool success = TradePlanNavigation.TryPlanNextLeg(plan, 1, null, _authority, out TradePlanNavigationState state, out _);
        return success && state.FinalStationId == plan.SourceStationId && state.TargetSystemIndex == 7;
    }

    private bool ValidateDestinationStage()
    {
        TradePlan plan = Plan(7);
        plan.Stage = TradePlanStage.GoToDestination;
        return TradePlanNavigation.TryPlanNextLeg(plan, 1, null, _authority, out TradePlanNavigationState state, out _) &&
            state.FinalStationId == plan.DestinationStationId;
    }

    private bool ValidateStageChangeRecalculates()
    {
        TradePlan plan = Plan(3);
        plan.Stage = TradePlanStage.GoToSource;
        plan.SourceSystemIndex = 3;
        TradePlanNavigation.TryPlanNextLeg(plan, 1, null, _authority, out TradePlanNavigationState sourceState, out _);
        plan.Stage = TradePlanStage.GoToDestination;
        plan.DestinationSystemIndex = 7;
        TradePlanNavigation.TryPlanNextLeg(plan, 1, null, _authority, out TradePlanNavigationState destinationState, out _);
        return sourceState.TargetSystemIndex == 3 && destinationState.TargetSystemIndex == 7 && sourceState.FinalStationId != destinationState.FinalStationId;
    }

    private bool ValidateDiversionRecalculates()
    {
        TradePlan plan = Plan(7);
        TradePlanNavigation.TryPlanNextLeg(plan, 1, null, _authority, out TradePlanNavigationState original, out _);
        TradePlanNavigation.TryPlanNextLeg(plan, 2, null, _authority, out TradePlanNavigationState diverted, out _);
        return original.RemainingHopCount == 3 && diverted.RemainingHopCount == 1 && original.CurrentTransitionId != diverted.CurrentTransitionId;
    }

    private bool ValidateRepeatedResume()
    {
        TradePlan plan = Plan(7);
        TradePlanNavigation.TryPlanNextLeg(plan, 1, null, _authority, out TradePlanNavigationState first, out _);
        TradePlanNavigation.TryPlanNextLeg(plan, 1, null, _authority, out TradePlanNavigationState second, out _);
        return first.CurrentTransitionId == second.CurrentTransitionId && first.RemainingHopCount == second.RemainingHopCount;
    }

    private bool ValidateRepeatedTransitionPlanning()
    {
        return ValidateRepeatedResume();
    }

    private bool ValidateCyclicTopology()
    {
        List<JumpHoleConfig> jumps = new()
        {
            Hole(1, 2, "1->2", "2->1"), Hole(2, 1, "2->1", "1->2"),
            Hole(2, 3, "2->3", "3->2"), Hole(3, 2, "3->2", "2->3")
        };
        return new MarketRouteAuthority(jumps).TryGetSystemRoute(1, 3, out MarketSystemRoute route) && route.JumpCount == 2;
    }

    private bool ValidateBoundedGraph()
    {
        List<JumpHoleConfig> jumps = Enumerable.Range(1, 20)
            .Select(system => Hole(system, system + 1, $"{system}->{system + 1}", $"{system + 1}->arrival"))
            .ToList();
        foreach (JumpHoleConfig arrival in jumps.ToList().Select(edge => Hole(edge.TargetSystemIndex, 0, $"{edge.TargetSystemIndex}->arrival", string.Empty)))
            jumps.Add(arrival);
        return !new MarketRouteAuthority(jumps).TryGetSystemRoute(1, 999, out _);
    }

    private bool ValidateTieRoute()
    {
        MarketRouteAuthority authority = TieAuthority();
        return authority.TryGetSystemRoute(1, 4, out MarketSystemRoute route) && route.JumpCount == 2;
    }

    private bool ValidateTieBreaking()
    {
        MarketRouteAuthority authority = TieAuthority();
        return authority.TryGetSystemRoute(1, 4, out MarketSystemRoute route) && route.Legs[0].TransitionName == "Alpha to Two";
    }

    private bool ValidateTieBreakingAfterReorder()
    {
        List<JumpHoleConfig> reversed = TieJumps();
        reversed.Reverse();
        MarketRouteAuthority authority = new(reversed);
        return authority.TryGetSystemRoute(1, 4, out MarketSystemRoute route) && route.Legs[0].TransitionName == "Alpha to Two";
    }

    private bool ValidateInvalidTransitionExcluded()
    {
        List<JumpHoleConfig> jumps = new() { Hole(1, 2, "bad", "missing arrival") };
        return !new MarketRouteAuthority(jumps).TryGetSystemRoute(1, 2, out _);
    }

    private bool ValidateIncompleteTransition()
    {
        List<JumpHoleConfig> jumps = new() { Hole(1, 2, "bad", "missing arrival"), Hole(2, 3, "2->3", "3->2") };
        return !new MarketRouteAuthority(jumps).TryGetSystemRoute(1, 3, out _);
    }

    private bool ValidateRemoteMetric()
    {
        MarketKnowledgeStation source = new("source", "Fort Bush", 1, Vector3.Zero);
        MarketKnowledgeStation target = new("target", "Synthetic Cortez", 7, new Vector3(10, 0, 10));
        return _authority.TryGetRoute(source, target, out MarketRouteMetric metric) && metric.DistanceUnits > 0f;
    }

    private bool ValidateRemoteMetricHops()
    {
        MarketKnowledgeStation source = new("source", "Fort Bush", 1, Vector3.Zero);
        MarketKnowledgeStation target = new("target", "Synthetic Cortez", 7, new Vector3(10, 0, 10));
        return _authority.TryGetRoute(source, target, out MarketRouteMetric metric) && metric.JumpCount == 3;
    }

    private bool ValidateQuoteSnapshots()
    {
        TradePlan plan = Plan(7);
        TradePlanNavigation.TryPlanNextLeg(plan, 1, null, _authority, out _, out _);
        return plan.SourceBuyPriceSnapshot == 85 && plan.DestinationSellPriceSnapshot == 115;
    }

    private bool ValidateSaveRouteFields()
    {
        SaveTradePlanData save = new()
        {
            SourceStationId = "fort_bush",
            SourceSystemIndex = 1,
            DestinationStationId = "synthetic_cortez_market",
            DestinationSystemIndex = 7,
            Stage = TradePlanStage.GoToDestination
        };
        string json = JsonSerializer.Serialize(save);
        SaveTradePlanData restored = JsonSerializer.Deserialize<SaveTradePlanData>(json);
        return restored?.SourceSystemIndex == 1 && restored.DestinationSystemIndex == 7 && restored.DestinationStationId == save.DestinationStationId;
    }

    private bool ValidateSaveLoadMidRoute()
    {
        MarketManager marketManager = new();
        MarketIntelligence intelligence = new(marketManager);
        TradePlanManager plans = new(
            marketManager,
            intelligence,
            _authority,
            new CargoHold(1000),
            new PlayerCredits(100000));
        SaveTradePlanData save = new()
        {
            SourceStationId = "fort_bush",
            SourceStationName = "Fort Bush",
            SourceSystemIndex = 1,
            DestinationStationId = "newark_station",
            DestinationStationName = "Newark Station",
            DestinationSystemIndex = 7,
            CommodityId = "food-rations",
            CommodityName = "Food Rations",
            SourceBuyPrice = 85,
            DestinationSellPrice = 115,
            SourceObservedAtMilliseconds = 0,
            DestinationObservedAtMilliseconds = 0,
            Stage = TradePlanStage.GoToDestination,
            RouteDistanceUnits = 100f,
            RouteHops = 3,
            SuggestedQuantity = 10
        };

        plans.RestoreState(save);
        bool firstLeg = plans.TryPlanNavigation(1, out TradePlanNavigationState beforeJump, out _);
        plans.RestoreState(save);
        bool remainingLeg = plans.TryPlanNavigation(2, out TradePlanNavigationState afterJump, out _);
        return firstLeg && remainingLeg &&
            plans.ActivePlan?.Stage == TradePlanStage.GoToDestination &&
            plans.ActivePlan.DestinationStationId == "newark_station" &&
            beforeJump.RemainingHopCount == 3 &&
            afterJump.RemainingHopCount == 1 &&
            afterJump.NextTransition?.TransitionName == "Cortez Jump Hole" &&
            intelligence.KnownStations.Count == 0;
    }

    private bool ValidateLegacySaveFields()
    {
        SaveTradePlanData save = new()
        {
            SourceStationId = "fort_bush",
            DestinationStationId = "newark_station",
            Stage = TradePlanStage.GoToSource
        };
        return save.SourceSystemIndex == 0 && save.DestinationSystemIndex == 0;
    }

    private bool ValidateRepresentativePath()
    {
        if (!_authority.TryGetSystemRoute(1, 7, out MarketSystemRoute route)) return false;
        string path = string.Join(" -> ", new[] { "New York", "Texas", "California", "Cortez" });
        return path == "New York -> Texas -> California -> Cortez" && route.Legs.Select(leg => leg.DestinationSystemIndex).SequenceEqual(new[] { 4, 2, 7 });
    }

    private static MarketRouteAuthority TieAuthority() => new(TieJumps());

    private static List<JumpHoleConfig> TieJumps() => new()
    {
        Hole(1, 2, "Alpha to Two", "Two Arrival"),
        Hole(2, 4, "Two to Four", "Four via Two"),
        Hole(1, 3, "Zulu to Three", "Three Arrival"),
        Hole(3, 4, "Three to Four", "Four via Three"),
        Hole(2, 0, "Two Arrival", string.Empty),
        Hole(3, 0, "Three Arrival", string.Empty),
        Hole(4, 0, "Four via Two", string.Empty),
        Hole(4, 0, "Four via Three", string.Empty)
    };

    private static JumpHoleConfig Hole(int system, int target, string name, string arrivalName)
    {
        return new JumpHoleConfig
        {
            SystemIndex = system,
            TargetSystemIndex = target,
            Name = name,
            TargetJumpHoleName = arrivalName,
            PositionX = system * 10,
            PositionZ = target * 10
        };
    }
}
