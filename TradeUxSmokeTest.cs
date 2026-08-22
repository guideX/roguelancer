using Microsoft.Xna.Framework;
using Roguelancer.Configuration;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Roguelancer;

/// <summary>
/// Phase 22 presentation proof. These cases exercise the bounded presentation
/// projection rather than drawing, keeping the assertions deterministic and
/// ensuring that read-only UI derivation cannot mutate gameplay state.
/// </summary>
internal sealed class TradeUxSmokeTest
{
    private readonly MarketRouteAuthority _authority = CreateAuthority();

    public (int Passed, int Failed) Run()
    {
        int passed = 0;
        int failed = 0;

        RunCase("active plan header", () => Check(Build().Header == "TRADE ROUTE", "header mismatch"), ref passed, ref failed);
        RunCase("commodity displayed", () => Check(Build().Commodity == "Food Rations", "commodity missing"), ref passed, ref failed);
        RunCase("source displayed", () => Check(Build().Source == "Fort Bush", "source missing"), ref passed, ref failed);
        RunCase("destination displayed", () => Check(Build().Destination == "Riverside Station", "destination missing"), ref passed, ref failed);
        RunCase("source stage next action", () => Check(Build(TradePlanStage.GoToSource, 1).NextAction == "NEXT: DOCK AT FORT BUSH", "source action mismatch"), ref passed, ref failed);
        RunCase("acquire stage next action", () => Check(Build(TradePlanStage.AcquireCommodity, 1).NextAction == "NEXT: BUY FOOD RATIONS", "buy action mismatch"), ref passed, ref failed);
        RunCase("destination leg next action", () => Check(Build(TradePlanStage.GoToDestination, 1).NextAction == "NEXT: JUMP TO TEXAS", "first jump action mismatch"), ref passed, ref failed);
        RunCase("sell stage next action", () => Check(Build(TradePlanStage.SellCommodity, 2, cargo: 40).NextAction == "NEXT: SELL FOOD RATIONS", "sell action mismatch"), ref passed, ref failed);
        RunCase("cross-system breadcrumb", () => Check(Build().Breadcrumb == "ROUTE: [New York] -> Texas -> California", "breadcrumb mismatch"), ref passed, ref failed);
        RunCase("current system highlighted", () => Check(Build(currentSystem: 4).Breadcrumb == "ROUTE: New York -> [Texas] -> California", "current system not highlighted"), ref passed, ref failed);
        RunCase("plural jump grammar", () => Check(Build(currentSystem: 1).JumpProgress == "2 JUMPS REMAIN", "plural grammar mismatch"), ref passed, ref failed);
        RunCase("single jump grammar", () => Check(Build(currentSystem: 4).JumpProgress == "1 JUMP REMAINS", "singular grammar mismatch"), ref passed, ref failed);
        RunCase("final system label", () => Check(Build(currentSystem: 2).JumpProgress == "FINAL SYSTEM", "final label mismatch"), ref passed, ref failed);
        RunCase("transition strips stable id", () => Check(TradePlanPresentation.CleanTransitionName("1:Jump Hole to Texas") == "Jump Hole to Texas", "transition id remained"), ref passed, ref failed);
        RunCase("final destination remains visible", () => Check(Build(currentSystem: 4).FinalDestination == "Riverside Station" && Build(currentSystem: 4).DetailLines.Any(line => line.Contains("DESTINATION: Riverside Station")), "destination was replaced"), ref passed, ref failed);
        RunCase("ordinary cargo quantity", () => Check(Build(cargo: 42).OrdinaryCargoQuantity == 42 && Build(cargo: 42).Cargo.Contains("42"), "cargo mismatch"), ref passed, ref failed);
        RunCase("mission cargo is not rendered as ordinary", () => Check(Build(cargo: 42).Cargo == "CARGO: 42 / suggested 100 Food Rations", "ordinary cargo line mismatch"), ref passed, ref failed);
        RunCase("suggested quantity advisory", () => Check(Build(cargo: 42).SuggestedQuantity == "SUGGESTED: 100 units (ADVISORY)", "suggestion mismatch"), ref passed, ref failed);
        RunCase("source price semantics", () => Check(Build().SourcePrice == "Fort Bush: 85 CR", "source buy quote mismatch"), ref passed, ref failed);
        RunCase("destination price semantics", () => Check(Build().DestinationPrice == "Riverside Station: 125 CR", "destination sell quote mismatch"), ref passed, ref failed);
        RunCase("spread calculation", () => Check(Build().Spread == "CURRENT SPREAD: +40 CR/unit", "spread mismatch"), ref passed, ref failed);
        RunCase("current intel label", () => Check(Build(age: MarketObservationAgeBand.Current).SourceIntel == "CURRENT", "current label mismatch"), ref passed, ref failed);
        RunCase("recent intel label", () => Check(Build(age: MarketObservationAgeBand.Recent).DestinationIntel == "RECENT", "recent label mismatch"), ref passed, ref failed);
        RunCase("stale intel label", () => Check(Build(age: MarketObservationAgeBand.Stale).SourceIntel == "STALE", "stale label mismatch"), ref passed, ref failed);
        RunCase("stale source warning", () => Check(Build(sourceAge: MarketObservationAgeBand.Stale).Warning == "WARNING: SOURCE PRICE IS STALE", "source stale warning mismatch"), ref passed, ref failed);
        RunCase("stale destination warning", () => Check(Build(destinationAge: MarketObservationAgeBand.Stale).Warning == "WARNING: DESTINATION PRICE IS STALE", "destination stale warning mismatch"), ref passed, ref failed);
        RunCase("both stale warning", () => Check(Build(sourceAge: MarketObservationAgeBand.Stale, destinationAge: MarketObservationAgeBand.Stale).Warning.Contains("SOURCE AND DESTINATION"), "both stale warning missing"), ref passed, ref failed);
        RunCase("current has no stale warning", () => Check(string.IsNullOrEmpty(Build(sourceAge: MarketObservationAgeBand.Current, destinationAge: MarketObservationAgeBand.Current).Warning), "current data warned"), ref passed, ref failed);
        RunCase("favorable market change", () => Check(Build(actualDestination: 133, marketMessage: "MARKET IMPROVED - Riverside Station now pays 133 CR").MarketUpdate.StartsWith("MARKET IMPROVED"), "favorable update missing"), ref passed, ref failed);
        RunCase("adverse market change", () => Check(Build(actualDestination: 112, warning: "CURRENT SPREAD: +27 CR/unit", marketMessage: "MARKET UPDATE: Food Rations now 112 CR at Riverside Station").MarketUpdate.Contains("112 CR"), "adverse update missing"), ref passed, ref failed);
        RunCase("insignificant change does not warn", () => Check(string.IsNullOrEmpty(Build(actualDestination: 124).Warning), "one-credit change warned"), ref passed, ref failed);
        RunCase("route no longer profitable", () => Check(Build(actualDestination: 80, warning: "ROUTE NO LONGER PROFITABLE").Warning == "ROUTE NO LONGER PROFITABLE", "unprofitable state missing"), ref passed, ref failed);
        RunCase("improved spread visible", () => Check(Build(actualDestination: 143).Spread == "CURRENT SPREAD: +58 CR/unit", "improved spread missing"), ref passed, ref failed);
        RunCase("purchase feedback", () => Check(TradePlanPresentation.BuildPurchaseFeedback(Plan(), 40, 40).Contains("Purchased 40 Food Rations") && TradePlanPresentation.BuildPurchaseFeedback(Plan(), 40, 40).Contains("Next: Riverside Station"), "purchase feedback mismatch"), ref passed, ref failed);
        RunCase("partial purchase accounting facts", () => Check(PlanWithAccounting().PurchasedQuantity == 30 && PlanWithAccounting().PurchasedCost == 3480, "partial purchase facts mismatch"), ref passed, ref failed);
        RunCase("sale feedback", () => Check(TradePlanPresentation.BuildSaleFeedback(PlanWithAccounting(), 40, 0, true).Contains("Sold 40 Food Rations at Riverside Station"), "sale feedback mismatch"), ref passed, ref failed);
        RunCase("partial sale accounting facts", () => Check(PlanWithAccounting().SoldQuantity == 40 && PlanWithAccounting().SoldProceeds == 5000, "partial sale facts mismatch"), ref passed, ref failed);
        RunCase("unrelated commodity excluded", () => Check(!TradePlanPresentation.BuildPurchaseFeedback(Plan(), 0, 40).Contains("Purchased"), "unrelated transaction was rendered"), ref passed, ref failed);
        RunCase("mission reward excluded", () => Check(!TradePlanPresentation.BuildCompletionSummary(PlanWithAccounting()).Any(line => line.Contains("reward", StringComparison.OrdinalIgnoreCase)), "mission reward leaked"), ref passed, ref failed);
        RunCase("exact gross margin", () => Check(PlanWithAccounting().RealizedGrossMargin == 1520, "exact margin mismatch"), ref passed, ref failed);
        RunCase("ambiguous provenance omits exact margin", () => Check(!AmbiguousPlan().HasExactRealizedMargin && TradePlanPresentation.BuildCompletionSummary(AmbiguousPlan()).Last().Contains("unavailable"), "fake exact margin produced"), ref passed, ref failed);
        RunCase("completion banner", () => Check(TradePlanPresentation.BuildCompletionSummary(PlanWithAccounting())[0] == "TRADE ROUTE COMPLETE", "completion banner missing"), ref passed, ref failed);
        RunCase("completion commodity", () => Check(TradePlanPresentation.BuildCompletionSummary(PlanWithAccounting())[1] == "Food Rations", "completion commodity mismatch"), ref passed, ref failed);
        RunCase("completion route", () => Check(TradePlanPresentation.BuildCompletionSummary(PlanWithAccounting())[2] == "Fort Bush -> Riverside Station", "completion route mismatch"), ref passed, ref failed);
        RunCase("completion quantity", () => Check(TradePlanPresentation.BuildCompletionSummary(PlanWithAccounting()).Any(line => line == "40 units delivered"), "completion quantity mismatch"), ref passed, ref failed);
        RunCase("completion jump count", () => Check(TradePlanPresentation.BuildCompletionSummary(PlanWithAccounting()).Any(line => line == "2 jumps"), "completion jump mismatch"), ref passed, ref failed);
        RunCase("completion has no reward", () => Check(TradePlanPresentation.BuildCompletionSummary(PlanWithAccounting()).All(line => !line.Contains("mission", StringComparison.OrdinalIgnoreCase)), "completion reward leaked"), ref passed, ref failed);
        RunCase("bounded summary", () => Check(TradePlanPresentation.BuildCompletionSummary(PlanWithAccounting()).Count <= 8, "summary not bounded"), ref passed, ref failed);
        RunCase("manual override text", () => Check(TradePlanPresentation.BuildPausedNavigationLine() == "TRADE ROUTE PAUSED - R TO RESUME", "pause text mismatch"), ref passed, ref failed);
        RunCase("resume text", () => Check(TradePlanPresentation.BuildPausedNavigationLine().Contains("R TO RESUME"), "resume text missing"), ref passed, ref failed);
        RunCase("invalid route text", () => Check(Build(unavailable: true).NextAction == "NEXT: TRADE ROUTE UNAVAILABLE", "invalid route is technical"), ref passed, ref failed);
        RunCase("same-system no jump clutter", () => Check(BuildSameSystem().JumpProgress == "LOCAL ROUTE" && !BuildSameSystem().Breadcrumb.Contains("JUMP"), "same-system cluttered"), ref passed, ref failed);
        RunCase("same-system source destination", () => Check(BuildSameSystem().Source == "Fort Bush" && BuildSameSystem().Destination == "Newark Station", "same-system endpoints missing"), ref passed, ref failed);
        RunCase("unknown source price", () => Check(Build(sourcePrice: 0).SourcePrice.Contains("PRICE UNKNOWN"), "unknown source not shown"), ref passed, ref failed);
        RunCase("unknown destination price", () => Check(Build(destinationPrice: 0).DestinationPrice.Contains("PRICE UNKNOWN"), "unknown destination not shown"), ref passed, ref failed);
        RunCase("unknown spread is not zero", () => Check(Build(sourcePrice: 0).Spread.Contains("UNKNOWN") && !Build(sourcePrice: 0).Spread.Contains("0 CR"), "unknown spread became zero"), ref passed, ref failed);
        RunCase("opportunity detail source semantics", () => Check(new MarketOpportunity(MarketOpportunityType.TradeRoute, Food(), "", "Fort Bush", "Riverside Station", 1, 100, "TRADE ROUTE", 40, "fort_bush", "riverside_station", "CURRENT", "RECENT", 1, 2).GetDisplayText().Contains("+40 CR/unit"), "opportunity spread mismatch"), ref passed, ref failed);
        RunCase("planned commodity identity", () => Check(Plan().CommodityId == "food-rations" && Plan().CommodityName == "Food Rations", "commodity identity mismatch"), ref passed, ref failed);
        RunCase("source context identity", () => Check(Plan().SourceStationId == "fort_bush", "source context mismatch"), ref passed, ref failed);
        RunCase("destination context identity", () => Check(Plan().DestinationStationId == "riverside_station", "destination context mismatch"), ref passed, ref failed);
        RunCase("source arrival feedback", () => Check(TradePlanPresentation.BuildArrivalMessage(Plan(), false).Contains("SOURCE REACHED"), "source arrival missing"), ref passed, ref failed);
        RunCase("destination arrival feedback", () => Check(TradePlanPresentation.BuildArrivalMessage(Plan(), true).Contains("DESTINATION REACHED"), "destination arrival missing"), ref passed, ref failed);
        RunCase("system transition feedback", () => Check(TradePlanPresentation.BuildSystemTransitionMessage("Texas", "1 JUMP REMAINS", false).Contains("ENTERED TEXAS"), "transition feedback missing"), ref passed, ref failed);
        RunCase("final system feedback", () => Check(TradePlanPresentation.BuildSystemTransitionMessage("California", "FINAL SYSTEM", true).Contains("FINAL SYSTEM REACHED"), "final feedback missing"), ref passed, ref failed);
        RunCase("formatting deterministic", () => Check(Join(Build().HudLines) == Join(Build().HudLines), "formatting changed"), ref passed, ref failed);
        RunCase("long station safe", () => Check(TradePlanPresentation.Truncate(new string('S', 120), 40).Length == 40, "long station not bounded"), ref passed, ref failed);
        RunCase("long commodity safe", () => Check(TradePlanPresentation.Truncate(new string('C', 120), 40).EndsWith("..."), "long commodity not truncated"), ref passed, ref failed);
        RunCase("presentation does not change stage", ValidateReadOnlyStage, ref passed, ref failed);
        RunCase("presentation does not change cargo input", ValidateReadOnlyCargo, ref passed, ref failed);
        RunCase("presentation does not change plan quotes", ValidateReadOnlyQuotes, ref passed, ref failed);
        RunCase("presentation does not change warning", ValidateReadOnlyWarning, ref passed, ref failed);
        RunCase("dev route uses normal header", () => Check(Build().Header == "TRADE ROUTE" && !Build().Header.Contains("DEV"), "dev diagnostics leaked"), ref passed, ref failed);
        RunCase("normal presentation has no validation text", () => Check(Build().HudLines.All(line => !line.Contains("VALIDATION", StringComparison.OrdinalIgnoreCase)), "validation text leaked"), ref passed, ref failed);
        RunCase("save fields preserve purchase cost", () => Check(new SaveTradePlanData { PurchasedCost = 3480, SoldProceeds = 5000, HasAmbiguousProvenance = false }.PurchasedCost == 3480, "save cost field missing"), ref passed, ref failed);
        RunCase("completed summary does not become active", () => Check(PlanWithAccounting().Stage == TradePlanStage.Complete && PlanWithAccounting().IsComplete, "completed plan resurrected"), ref passed, ref failed);
        RunCase("cancellation projection is empty", () => Check(TradePlanPresentation.Build(null, null, null, _authority, 1).HudLines.Count == 0, "cancelled plan rendered"), ref passed, ref failed);
        RunCase("replacement projection follows new commodity", () => Check(Build(newCommodity: "Medical Supplies").Commodity == "Medical Supplies", "replacement did not update"), ref passed, ref failed);
        RunCase("new plan starts without stale warning", () => Check(string.IsNullOrEmpty(Build(sourceAge: MarketObservationAgeBand.Current, destinationAge: MarketObservationAgeBand.Current).Warning), "new plan inherited warning"), ref passed, ref failed);
        RunCase("route breadcrumb is topology-derived", () => Check(!Build().Breadcrumb.Contains("Colorado") && Build().Breadcrumb.Contains("Texas"), "breadcrumb was hard-coded incorrectly"), ref passed, ref failed);
        RunCase("final station remains in intermediate hud", () => Check(Build(currentSystem: 4).HudLines.Any(line => line.Contains("Riverside Station")), "final station disappeared"), ref passed, ref failed);
        RunCase("station name source of truth", () => Check(Build().Source == "Fort Bush" && Build().Destination == "Riverside Station", "station display name drifted"), ref passed, ref failed);
        RunCase("one jump label bounded", () => Check(Build(currentSystem: 4).JumpProgress.Length < 24, "jump label too verbose"), ref passed, ref failed);
        RunCase("transition name is player readable", () => Check(TradePlanPresentation.CleanTransitionName("1:Jump Hole to Texas").StartsWith("Jump Hole"), "internal transition id visible"), ref passed, ref failed);
        RunCase("projected spread remains advisory", () => Check(Build().SuggestedQuantity.Contains("ADVISORY"), "suggestion not advisory"), ref passed, ref failed);
        RunCase("source and destination prices align", () => Check(Build().DetailLines.ToList().IndexOf("Fort Bush: 85 CR") < Build().DetailLines.ToList().IndexOf("Riverside Station: 125 CR"), "price hierarchy mismatch"), ref passed, ref failed);
        RunCase("market update is one bounded line", () => Check(Build(marketMessage: "MARKET UPDATE: Food Rations now 112 CR at Riverside Station").HudLines.Count(line => line.StartsWith("MARKET UPDATE")) == 1, "market update duplicated"), ref passed, ref failed);
        RunCase("route status is readable", () => Check(Build().RouteStatus == "CROSS-SYSTEM ROUTE", "route status internal"), ref passed, ref failed);
        RunCase("final system status is readable", () => Check(Build(currentSystem: 2).RouteStatus == "LOCAL STATION ROUTE", "final status mismatch"), ref passed, ref failed);
        RunCase("same system status is readable", () => Check(BuildSameSystem().RouteStatus == "LOCAL STATION ROUTE", "local status mismatch"), ref passed, ref failed);
        RunCase("unknown market data warning", () => Check(Build(sourcePrice: 0).Warning.Contains("MARKET DATA UNKNOWN"), "unknown data warning missing"), ref passed, ref failed);
        RunCase("negative spread formatting", () => Check(TradePlanPresentation.FormatSigned(-4) == "-4", "negative spread formatting mismatch"), ref passed, ref failed);
        RunCase("positive spread formatting", () => Check(TradePlanPresentation.FormatSigned(58) == "+58", "positive spread formatting mismatch"), ref passed, ref failed);
        RunCase("safe overflow suggestion line", () => Check(TradePlanPresentation.Truncate("SUGGESTED: 2147483647 units (ADVISORY)", 80).Length <= 80, "suggestion overflowed"), ref passed, ref failed);
        RunCase("trade summary route bounded", () => Check(TradePlanPresentation.BuildCompletionSummary(PlanWithAccounting()).Count == 8, "summary length drifted"), ref passed, ref failed);
        RunCase("all presentation lines nonempty", () => Check(Build().HudLines.All(line => !string.IsNullOrWhiteSpace(line)), "empty presentation line"), ref passed, ref failed);

        Console.WriteLine($"[TRADE UX SMOKE] RESULT: {passed}/{passed + failed}");
        return (passed, failed);
    }

    private TradePlanPresentationState Build(
        TradePlanStage stage = TradePlanStage.GoToDestination,
        int currentSystem = 1,
        int cargo = 0,
        MarketObservationAgeBand age = MarketObservationAgeBand.Current,
        MarketObservationAgeBand? sourceAge = null,
        MarketObservationAgeBand? destinationAge = null,
        int sourcePrice = 85,
        int destinationPrice = 125,
        int actualDestination = 0,
        string warning = "",
        string marketMessage = "",
        bool unavailable = false,
        string newCommodity = null)
    {
        TradePlan plan = Plan(stage, sourcePrice, destinationPrice, sourceAge ?? age, destinationAge ?? age, newCommodity ?? "Food Rations");
        plan.ActualDestinationSellPrice = actualDestination;
        plan.WarningMessage = warning ?? string.Empty;
        plan.LastMarketChangeMessage = marketMessage ?? string.Empty;
        TradePlanNavigationState navigation = null;
        if (unavailable)
        {
            navigation = new TradePlanNavigationState { Status = TradePlanRouteStatus.Unavailable, FailureReason = "NO KNOWN ROUTE" };
        }
        else if (stage is TradePlanStage.GoToSource or TradePlanStage.GoToDestination)
        {
            TradePlanNavigation.TryPlanNextLeg(plan, currentSystem, null, _authority, out navigation, out _);
        }

        return TradePlanPresentation.Build(plan, navigation, null, _authority, currentSystem, SystemName, cargo);
    }

    private TradePlan Plan(
        TradePlanStage stage = TradePlanStage.GoToDestination,
        int sourcePrice = 85,
        int destinationPrice = 125,
        MarketObservationAgeBand sourceAge = MarketObservationAgeBand.Current,
        MarketObservationAgeBand destinationAge = MarketObservationAgeBand.Current,
        string commodityName = "Food Rations")
    {
        return new TradePlan
        {
            SourceStationId = "fort_bush",
            SourceStationName = "Fort Bush",
            SourceSystemIndex = 1,
            DestinationStationId = "riverside_station",
            DestinationStationName = "Riverside Station",
            DestinationSystemIndex = 2,
            CommodityId = "food-rations",
            CommodityName = commodityName,
            SourceBuyPriceSnapshot = sourcePrice,
            DestinationSellPriceSnapshot = destinationPrice,
            SourceAgeBandSnapshot = sourceAge,
            DestinationAgeBandSnapshot = destinationAge,
            RouteDistanceUnits = 100000,
            RouteHops = 2,
            SuggestedQuantity = 100,
            Stage = stage
        };
    }

    private TradePlanPresentationState BuildSameSystem()
    {
        TradePlan plan = Plan();
        plan.DestinationStationId = "newark_station";
        plan.DestinationStationName = "Newark Station";
        plan.DestinationSystemIndex = 1;
        plan.RouteHops = 0;
        return TradePlanPresentation.Build(plan, null, null, _authority, 1, SystemName, 0);
    }

    private static TradePlan PlanWithAccounting()
    {
        TradePlan plan = new TradeUxSmokeTest().Plan();
        plan.PurchasedQuantity = 30;
        plan.PurchasedCost = 3480;
        plan.SoldQuantity = 40;
        plan.SoldProceeds = 5000;
        plan.Stage = TradePlanStage.Complete;
        plan.CargoAcquired = true;
        return plan;
    }

    private static TradePlan AmbiguousPlan()
    {
        TradePlan plan = PlanWithAccounting();
        plan.HasAmbiguousProvenance = true;
        return plan;
    }

    private static Commodity Food() => CommodityCatalog.GetById("food-rations");

    private static string SystemName(int index) => index switch
    {
        1 => "New York",
        2 => "California",
        4 => "Texas",
        _ => $"System {index}"
    };

    private static MarketRouteAuthority CreateAuthority()
    {
        List<JumpHoleConfig> edges = new()
        {
            Edge("Jump Hole to Texas", 1, 4, "Jump Hole to New York", 36000),
            Edge("Jump Hole to New York", 4, 1, "Jump Hole to Texas", 0),
            Edge("California Jump Hole", 4, 2, "Texas Jump Hole", 0),
            Edge("Texas Jump Hole", 2, 4, "California Jump Hole", 13000)
        };
        return new MarketRouteAuthority(edges);
    }

    private static JumpHoleConfig Edge(string name, int origin, int destination, string arrivalName, float position)
    {
        return new JumpHoleConfig
        {
            Name = name,
            SystemIndex = origin,
            TargetSystemIndex = destination,
            TargetJumpHoleName = arrivalName,
            PositionX = position
        };
    }

    private static string Join(IEnumerable<string> values) => string.Join("|", values ?? Array.Empty<string>());

    private static (bool, string) ValidateReadOnlyStage()
    {
        TradePlan plan = new TradeUxSmokeTest().Plan();
        TradePlanStage before = plan.Stage;
        TradePlanPresentation.Build(plan, null, null, CreateAuthority(), 1, SystemName, 42);
        return Check(plan.Stage == before, "stage changed");
    }

    private static (bool, string) ValidateReadOnlyCargo()
    {
        TradePlan plan = new TradeUxSmokeTest().Plan();
        TradePlanPresentationState state = TradePlanPresentation.Build(plan, null, null, CreateAuthority(), 1, SystemName, 42);
        return Check(state.OrdinaryCargoQuantity == 42 && plan.AcquiredQuantity == 0, "cargo state changed");
    }

    private static (bool, string) ValidateReadOnlyQuotes()
    {
        TradePlan plan = new TradeUxSmokeTest().Plan();
        TradePlanPresentation.Build(plan, null, null, CreateAuthority(), 1, SystemName, 42);
        return Check(plan.SourceBuyPriceSnapshot == 85 && plan.DestinationSellPriceSnapshot == 125, "quote changed");
    }

    private static (bool, string) ValidateReadOnlyWarning()
    {
        TradePlan plan = new TradeUxSmokeTest().Plan();
        plan.WarningMessage = "KEEP";
        TradePlanPresentation.Build(plan, null, null, CreateAuthority(), 1, SystemName, 42);
        return Check(plan.WarningMessage == "KEEP", "warning changed");
    }

    private static void RunCase(string label, Func<(bool Success, string FailureReason)> test, ref int passed, ref int failed)
    {
        try
        {
            (bool success, string reason) = RunSilenced(test);
            if (success)
            {
                passed++;
                Console.WriteLine($"[TRADE UX SMOKE] PASS {label}");
            }
            else
            {
                failed++;
                Console.WriteLine($"[TRADE UX SMOKE] FAIL {label}: {reason}");
            }
        }
        catch (Exception ex)
        {
            failed++;
            Console.WriteLine($"[TRADE UX SMOKE] FAIL {label}: {ex.Message}");
        }
    }

    private static (bool, string) Check(bool value, string reason) => value ? (true, string.Empty) : (false, reason);

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
