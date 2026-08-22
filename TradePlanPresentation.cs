using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Roguelancer;

/// <summary>
/// Bounded, player-facing projection of an active Trade Plan. This type is
/// intentionally free of draw calls and economy mutation so HUD/station views
/// can share one deterministic vocabulary.
/// </summary>
public sealed class TradePlanPresentationState
{
    public string Header { get; internal set; } = string.Empty;
    public string Commodity { get; internal set; } = string.Empty;
    public string Source { get; internal set; } = string.Empty;
    public string Destination { get; internal set; } = string.Empty;
    public string Breadcrumb { get; internal set; } = string.Empty;
    public string CurrentSystem { get; internal set; } = string.Empty;
    public string NextAction { get; internal set; } = string.Empty;
    public string JumpProgress { get; internal set; } = string.Empty;
    public string NextTransition { get; internal set; } = string.Empty;
    public string FinalDestination { get; internal set; } = string.Empty;
    public string Cargo { get; internal set; } = string.Empty;
    public string SuggestedQuantity { get; internal set; } = string.Empty;
    public string SourcePrice { get; internal set; } = string.Empty;
    public string DestinationPrice { get; internal set; } = string.Empty;
    public string Spread { get; internal set; } = string.Empty;
    public string SourceIntel { get; internal set; } = string.Empty;
    public string DestinationIntel { get; internal set; } = string.Empty;
    public string Warning { get; internal set; } = string.Empty;
    public string MarketUpdate { get; internal set; } = string.Empty;
    public string RouteStatus { get; internal set; } = string.Empty;
    public bool IsSameSystem { get; internal set; }
    public bool IsCurrentSystemFinal { get; internal set; }
    public int OrdinaryCargoQuantity { get; internal set; }
    public int JumpCount { get; internal set; }
    public IReadOnlyList<string> HudLines { get; internal set; } = Array.Empty<string>();
    public IReadOnlyList<string> DetailLines { get; internal set; } = Array.Empty<string>();

    public static TradePlanPresentationState Empty { get; } = new();
}

public static class TradePlanPresentation
{
    public const int MaterialChangeMinimumCredits = 3;
    public const double MaterialChangePercentage = 0.05d;

    public static TradePlanPresentationState Build(
        TradePlan plan,
        TradePlanNavigationState navigation,
        MarketIntelligence intelligence,
        MarketRouteAuthority routeAuthority,
        int currentSystemIndex,
        Func<int, string> systemNameResolver = null,
        int ordinaryCargoQuantity = 0)
    {
        if (plan == null) return TradePlanPresentationState.Empty;

        systemNameResolver ??= index => $"System {index}";
        routeAuthority ??= new MarketRouteAuthority();

        bool sourceKnown = TryGetObservation(intelligence, plan.SourceStationId, plan.CommodityId, out MarketObservation sourceObservation);
        bool destinationKnown = TryGetObservation(intelligence, plan.DestinationStationId, plan.CommodityId, out MarketObservation destinationObservation);
        int sourcePrice = plan.ActualSourceBuyPrice > 0
            ? plan.ActualSourceBuyPrice
            : sourceKnown && sourceObservation.BuyPrice > 0
                ? sourceObservation.BuyPrice
                : plan.SourceBuyPriceSnapshot;
        int destinationPrice = plan.ActualDestinationSellPrice > 0
            ? plan.ActualDestinationSellPrice
            : destinationKnown && destinationObservation.SellPrice > 0
                ? destinationObservation.SellPrice
                : plan.DestinationSellPriceSnapshot;
        bool hasSourcePrice = sourcePrice > 0;
        bool hasDestinationPrice = destinationPrice > 0;

        string sourceIntel = GetIntelLabel(plan, sourceObservation, sourceKnown, source: true, intelligence?.ElapsedMilliseconds ?? 0L);
        string destinationIntel = GetIntelLabel(plan, destinationObservation, destinationKnown, source: false, intelligence?.ElapsedMilliseconds ?? 0L);
        string sourceName = SafeLabel(plan.SourceStationName, plan.SourceStationId);
        string destinationName = SafeLabel(plan.DestinationStationName, plan.DestinationStationId);
        int cargo = Math.Max(0, ordinaryCargoQuantity);
        int suggested = Math.Max(0, plan.SuggestedQuantity);
        bool sameSystem = plan.SourceSystemIndex > 0 && plan.SourceSystemIndex == plan.DestinationSystemIndex;
        string breadcrumb = BuildBreadcrumb(plan.SourceSystemIndex, plan.DestinationSystemIndex, routeAuthority, currentSystemIndex, systemNameResolver);
        bool finalSystem = plan.DestinationSystemIndex > 0 && currentSystemIndex == plan.DestinationSystemIndex;

        TradePlanNavigationState effectiveNavigation = navigation;
        if (effectiveNavigation == null && currentSystemIndex > 0)
        {
            TryBuildNavigation(plan, currentSystemIndex, intelligence, routeAuthority, out effectiveNavigation);
        }

        string nextTransition = CleanTransitionName(effectiveNavigation?.NextTransition?.TransitionName);
        int remainingJumps = ResolveRemainingJumps(plan, effectiveNavigation, currentSystemIndex, routeAuthority);
        string jumpProgress = BuildJumpProgress(plan, effectiveNavigation, finalSystem, remainingJumps);
        string nextAction = BuildNextAction(
            plan,
            effectiveNavigation,
            currentSystemIndex,
            finalSystem,
            systemNameResolver,
            sourceName,
            destinationName,
            nextTransition,
            cargo);

        string sourcePriceLine = hasSourcePrice
            ? $"{sourceName}: {sourcePrice:N0} CR"
            : $"{sourceName}: PRICE UNKNOWN";
        string destinationPriceLine = hasDestinationPrice
            ? $"{destinationName}: {destinationPrice:N0} CR"
            : $"{destinationName}: PRICE UNKNOWN";
        string spread = hasSourcePrice && hasDestinationPrice
            ? $"CURRENT SPREAD: {FormatSigned((long)destinationPrice - sourcePrice)} CR/unit"
            : "CURRENT SPREAD: UNKNOWN";

        string warning = BuildWarning(plan, sourceIntel, destinationIntel, hasSourcePrice, hasDestinationPrice, sourcePrice, destinationPrice);
        string cargoLine = BuildCargoLine(plan, cargo, suggested);
        string suggestedLine = suggested > 0
            ? $"SUGGESTED: {suggested:N0} units (ADVISORY)"
            : "SUGGESTED: UNKNOWN";
        string routeStatus = effectiveNavigation?.Status switch
        {
            TradePlanRouteStatus.Unavailable => "TRADE ROUTE UNAVAILABLE",
            TradePlanRouteStatus.LocalStation => "LOCAL STATION ROUTE",
            TradePlanRouteStatus.TransitionRequired => "CROSS-SYSTEM ROUTE",
            _ => sameSystem ? "LOCAL ROUTE" : string.Empty
        };

        List<string> hudLines = new()
        {
            "TRADE ROUTE",
            plan.CommodityName,
            $"{sourceName} -> {destinationName}"
        };
        if (!string.IsNullOrWhiteSpace(breadcrumb)) hudLines.Add(breadcrumb);
        hudLines.Add(nextAction);
        hudLines.Add(jumpProgress);
        hudLines.Add($"DESTINATION: {destinationName}");
        hudLines.Add(cargoLine);
        hudLines.Add(spread);
        hudLines.Add($"INTEL: {sourceIntel} / {destinationIntel}");
        if (!string.IsNullOrWhiteSpace(warning)) hudLines.Add(warning);
        if (!string.IsNullOrWhiteSpace(plan.LastMarketChangeMessage)) hudLines.Add(plan.LastMarketChangeMessage);

        List<string> detailLines = new()
        {
            "TRADE ROUTE",
            plan.CommodityName,
            $"{sourceName} -> {destinationName}",
            string.IsNullOrWhiteSpace(breadcrumb) ? "LOCAL ROUTE" : breadcrumb,
            nextAction,
            jumpProgress,
            $"DESTINATION: {destinationName}",
            cargoLine,
            suggestedLine,
            "KNOWN PRICES",
            sourcePriceLine,
            destinationPriceLine,
            spread,
            $"INTEL: {sourceName} {sourceIntel} | {destinationName} {destinationIntel}"
        };
        if (!string.IsNullOrWhiteSpace(warning)) detailLines.Add(warning);
        if (!string.IsNullOrWhiteSpace(plan.LastMarketChangeMessage)) detailLines.Add(plan.LastMarketChangeMessage);

        return new TradePlanPresentationState
        {
            Header = "TRADE ROUTE",
            Commodity = plan.CommodityName ?? string.Empty,
            Source = sourceName,
            Destination = destinationName,
            Breadcrumb = breadcrumb,
            CurrentSystem = currentSystemIndex > 0 ? systemNameResolver(currentSystemIndex) : string.Empty,
            NextAction = nextAction,
            JumpProgress = jumpProgress,
            NextTransition = nextTransition,
            FinalDestination = destinationName,
            Cargo = cargoLine,
            SuggestedQuantity = suggestedLine,
            SourcePrice = sourcePriceLine,
            DestinationPrice = destinationPriceLine,
            Spread = spread,
            SourceIntel = sourceIntel,
            DestinationIntel = destinationIntel,
            Warning = warning,
            MarketUpdate = plan.LastMarketChangeMessage ?? string.Empty,
            RouteStatus = routeStatus,
            IsSameSystem = sameSystem,
            IsCurrentSystemFinal = finalSystem,
            OrdinaryCargoQuantity = cargo,
            JumpCount = remainingJumps,
            HudLines = hudLines,
            DetailLines = detailLines
        };
    }

    public static string BuildPausedNavigationLine() => "TRADE ROUTE PAUSED - R TO RESUME";

    public static string BuildPurchaseFeedback(TradePlan plan, int purchasedQuantity, int cargoQuantity)
    {
        if (plan == null || purchasedQuantity <= 0) return string.Empty;
        return $"Purchased {purchasedQuantity:N0} {plan.CommodityName}. Cargo: {Math.Max(0, cargoQuantity):N0}. Next: {plan.DestinationStationName}.";
    }

    public static string BuildSaleFeedback(TradePlan plan, int soldQuantity, int remainingCargo, bool includeMargin)
    {
        if (plan == null || soldQuantity <= 0) return string.Empty;
        string margin = includeMargin && plan.HasExactRealizedMargin
            ? $" Trade margin: {FormatSigned(plan.RealizedGrossMargin)} CR."
            : string.Empty;
        return $"Sold {soldQuantity:N0} {plan.CommodityName} at {plan.DestinationStationName}. Remaining cargo: {Math.Max(0, remainingCargo):N0}.{margin}";
    }

    public static IReadOnlyList<string> BuildCompletionSummary(TradePlan plan)
    {
        if (plan == null) return Array.Empty<string>();
        List<string> lines = new()
        {
            "TRADE ROUTE COMPLETE",
            plan.CommodityName ?? string.Empty,
            $"{SafeLabel(plan.SourceStationName, plan.SourceStationId)} -> {SafeLabel(plan.DestinationStationName, plan.DestinationStationId)}",
            $"{Math.Max(0, plan.SoldQuantity):N0} units delivered"
        };
        if (plan.PurchasedCost > 0) lines.Add($"Bought: {plan.PurchasedCost:N0} CR");
        if (plan.SoldProceeds > 0) lines.Add($"Sold: {plan.SoldProceeds:N0} CR");
        lines.Add(plan.RouteHops == 1 ? "1 jump" : $"{Math.Max(0, plan.RouteHops):N0} jumps");
        lines.Add(plan.HasExactRealizedMargin
            ? $"Gross margin: {FormatSigned(plan.RealizedGrossMargin)} CR"
            : "Gross margin: unavailable (cargo provenance mixed)");
        return lines;
    }

    public static string Truncate(string value, int maxLength)
    {
        if (string.IsNullOrEmpty(value) || value.Length <= maxLength) return value ?? string.Empty;
        return value.Substring(0, Math.Max(0, maxLength - 3)) + "...";
    }

    public static string BuildArrivalMessage(TradePlan plan, bool destination)
    {
        if (plan == null) return string.Empty;
        string station = destination ? plan.DestinationStationName : plan.SourceStationName;
        string role = destination ? "DESTINATION" : "SOURCE";
        return $"TRADE ROUTE: {role} REACHED - {SafeLabel(station, destination ? plan.DestinationStationId : plan.SourceStationId).ToUpperInvariant()}";
    }

    public static string BuildSystemTransitionMessage(
        string systemName,
        string jumpProgress,
        bool finalSystem)
    {
        string label = SafeLabel(systemName, "UNKNOWN SYSTEM").ToUpperInvariant();
        return finalSystem
            ? $"FINAL SYSTEM REACHED - {label}"
            : $"ENTERED {label} - {jumpProgress}";
    }

    public static string CleanTransitionName(string value)
    {
        string cleaned = (value ?? string.Empty).Trim();
        cleaned = Regex.Replace(cleaned, "^\\s*\\d+\\s*:\\s*", string.Empty);
        return string.IsNullOrWhiteSpace(cleaned) ? "NEXT JUMP" : cleaned;
    }

    public static string FormatSigned(long value) => value >= 0 ? $"+{value:N0}" : value.ToString("N0");

    private static string BuildNextAction(
        TradePlan plan,
        TradePlanNavigationState navigation,
        int currentSystemIndex,
        bool finalSystem,
        Func<int, string> systemNameResolver,
        string sourceName,
        string destinationName,
        string nextTransition,
        int cargo)
    {
        if (navigation?.Status == TradePlanRouteStatus.Unavailable)
            return "NEXT: TRADE ROUTE UNAVAILABLE";

        if (plan.Stage == TradePlanStage.AcquireCommodity)
            return $"NEXT: BUY {plan.CommodityName?.ToUpperInvariant()}";
        if (plan.Stage == TradePlanStage.SellCommodity)
            return cargo > 0
                ? $"NEXT: SELL {plan.CommodityName?.ToUpperInvariant()}"
                : "NEXT: NO TRADE CARGO ABOARD";

        if (navigation?.Status == TradePlanRouteStatus.TransitionRequired && navigation.NextTransition != null)
        {
            string targetSystem = SafeLabel(systemNameResolver(navigation.NextTransition.DestinationSystemIndex), $"System {navigation.NextTransition.DestinationSystemIndex}");
            return $"NEXT: JUMP TO {targetSystem.ToUpperInvariant()}";
        }

        if (plan.Stage == TradePlanStage.GoToDestination && !finalSystem && !string.IsNullOrWhiteSpace(nextTransition))
            return $"NEXT: {nextTransition.ToUpperInvariant()}";

        if (plan.Stage == TradePlanStage.GoToSource)
            return currentSystemIndex == plan.SourceSystemIndex
                ? $"NEXT: DOCK AT {sourceName.ToUpperInvariant()}"
                : $"NEXT: DOCK AT {sourceName.ToUpperInvariant()}";

        if (plan.Stage == TradePlanStage.GoToDestination)
            return $"NEXT: DOCK AT {destinationName.ToUpperInvariant()}";

        return "NEXT: TRADE ROUTE";
    }

    private static string BuildJumpProgress(TradePlan plan, TradePlanNavigationState navigation, bool finalSystem, int remainingJumps)
    {
        if (plan.SourceSystemIndex == plan.DestinationSystemIndex) return "LOCAL ROUTE";
        if (finalSystem) return "FINAL SYSTEM";
        if (navigation?.Status == TradePlanRouteStatus.Unavailable) return "TRADE ROUTE UNAVAILABLE";
        if (remainingJumps <= 0) return "LOCAL SYSTEM";
        return $"{remainingJumps:N0} JUMP{(remainingJumps == 1 ? string.Empty : "S")} REMAIN{(remainingJumps == 1 ? "S" : "")}";
    }

    private static string BuildCargoLine(TradePlan plan, int cargo, int suggested)
    {
        if (suggested > 0 && cargo == 0 && plan.Stage is TradePlanStage.GoToSource or TradePlanStage.AcquireCommodity)
            return $"CARGO: 0 / {suggested:N0} {plan.CommodityName}";
        if (suggested > 0 && cargo < suggested)
            return $"CARGO: {cargo:N0} / suggested {suggested:N0} {plan.CommodityName}";
        return $"CARGO: {cargo:N0} {plan.CommodityName}";
    }

    private static string BuildWarning(
        TradePlan plan,
        string sourceIntel,
        string destinationIntel,
        bool hasSourcePrice,
        bool hasDestinationPrice,
        int sourcePrice,
        int destinationPrice)
    {
        if (hasSourcePrice && hasDestinationPrice && (long)destinationPrice <= sourcePrice && plan.HasMaterialPriceChange)
            return "ROUTE NO LONGER PROFITABLE";
        if (sourceIntel == "STALE" && destinationIntel == "STALE") return "WARNING: SOURCE AND DESTINATION PRICES ARE STALE";
        if (sourceIntel == "STALE") return "WARNING: SOURCE PRICE IS STALE";
        if (destinationIntel == "STALE") return "WARNING: DESTINATION PRICE IS STALE";
        if (!hasSourcePrice || !hasDestinationPrice) return "WARNING: MARKET DATA UNKNOWN";
        if (!string.IsNullOrWhiteSpace(plan.WarningMessage)) return plan.WarningMessage;
        return string.Empty;
    }

    private static string BuildBreadcrumb(
        int sourceSystem,
        int destinationSystem,
        MarketRouteAuthority routeAuthority,
        int currentSystemIndex,
        Func<int, string> resolver)
    {
        if (sourceSystem <= 0 || destinationSystem <= 0 || sourceSystem == destinationSystem) return string.Empty;
        if (!routeAuthority.TryGetSystemRoute(sourceSystem, destinationSystem, out MarketSystemRoute route) || route == null)
            return string.Empty;

        List<int> systems = new() { sourceSystem };
        systems.AddRange(route.Legs.Where(leg => leg != null).Select(leg => leg.DestinationSystemIndex));
        List<string> labels = systems
            .Distinct()
            .Select(index =>
            {
                string label = SafeLabel(resolver(index), $"System {index}");
                return index == currentSystemIndex ? $"[{label}]" : label;
            })
            .ToList();
        return labels.Count > 1 ? $"ROUTE: {string.Join(" -> ", labels)}" : string.Empty;
    }

    private static int ResolveRemainingJumps(TradePlan plan, TradePlanNavigationState navigation, int currentSystemIndex, MarketRouteAuthority authority)
    {
        if (plan == null || plan.SourceSystemIndex == plan.DestinationSystemIndex) return 0;
        if (navigation?.Status == TradePlanRouteStatus.TransitionRequired)
            return Math.Max(0, navigation.RemainingHopCount);
        int target = plan.Stage == TradePlanStage.GoToSource ? plan.SourceSystemIndex : plan.DestinationSystemIndex;
        if (currentSystemIndex <= 0 || target <= 0 || currentSystemIndex == target) return 0;
        return authority.TryGetSystemRoute(currentSystemIndex, target, out MarketSystemRoute route) && route != null
            ? route.JumpCount
            : Math.Max(0, plan.RouteHops);
    }

    private static bool TryBuildNavigation(
        TradePlan plan,
        int currentSystemIndex,
        MarketIntelligence intelligence,
        MarketRouteAuthority authority,
        out TradePlanNavigationState navigation)
    {
        return TradePlanNavigation.TryPlanNextLeg(plan, currentSystemIndex, intelligence, authority, out navigation, out _);
    }

    private static bool TryGetObservation(MarketIntelligence intelligence, string stationId, string commodityId, out MarketObservation observation)
    {
        observation = null;
        return intelligence?.TryGetObservation(stationId, commodityId, out observation) == true && observation != null;
    }

    private static string GetIntelLabel(TradePlan plan, MarketObservation observation, bool known, bool source, long currentMilliseconds)
    {
        if (known && observation != null && plan != null)
            return observation.GetAgeLabel(currentMilliseconds);
        MarketObservationAgeBand snapshot = source ? plan.SourceAgeBandSnapshot : plan.DestinationAgeBandSnapshot;
        return snapshot switch
        {
            MarketObservationAgeBand.Current => "CURRENT",
            MarketObservationAgeBand.Recent => "RECENT",
            MarketObservationAgeBand.Stale => "STALE",
            _ => "UNKNOWN"
        };
    }

    private static string SafeLabel(string value, string fallback) => string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
}
