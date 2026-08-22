using System;

namespace Roguelancer;

public enum TradePlanRouteStatus
{
    None,
    LocalStation,
    TransitionRequired,
    Unavailable
}

/// <summary>
/// Ephemeral navigation state derived from the active plan and current system.
/// It intentionally contains no live world-object references and is not saved.
/// </summary>
public sealed class TradePlanNavigationState
{
    public string FinalStationId { get; internal set; } = string.Empty;
    public string FinalStationName { get; internal set; } = string.Empty;
    public int TargetSystemIndex { get; internal set; }
    public int CurrentSystemIndex { get; internal set; }
    public TradePlanRouteStatus Status { get; internal set; }
    public MarketRouteLeg NextTransition { get; internal set; }
    public int RemainingHopCount { get; internal set; }
    public string FailureReason { get; internal set; } = string.Empty;

    public bool IsRemote => Status == TradePlanRouteStatus.TransitionRequired;
    public string CurrentTransitionId => NextTransition?.TransitionId ?? string.Empty;
    public string NextSystemLabel => NextTransition == null
        ? string.Empty
        : $"System {NextTransition.DestinationSystemIndex}";
}

public static partial class TradePlanNavigation
{
    /// <summary>
    /// Computes the next bounded navigation leg without resolving the final
    /// station object. A remote target therefore remains plottable while its
    /// station is unloaded from the active system.
    /// </summary>
    public static bool TryPlanNextLeg(
        TradePlan plan,
        int currentSystemIndex,
        MarketIntelligence marketIntelligence,
        MarketRouteAuthority routeAuthority,
        out TradePlanNavigationState state,
        out string failureReason)
    {
        state = null;
        failureReason = string.Empty;

        if (plan == null || string.IsNullOrWhiteSpace(plan.NextStationId))
        {
            failureReason = "trade plan has no flight destination";
            return false;
        }

        if (currentSystemIndex <= 0)
        {
            failureReason = "current system is unknown";
            return false;
        }

        int targetSystemIndex = plan.NextStationSystemIndex;
        if (targetSystemIndex <= 0 && marketIntelligence?.TryGetKnownStation(plan.NextStationId, out MarketKnowledgeStation knownStation) == true)
        {
            targetSystemIndex = knownStation.SystemIndex;
        }

        state = new TradePlanNavigationState
        {
            FinalStationId = plan.NextStationId,
            FinalStationName = plan.NextStationName,
            TargetSystemIndex = targetSystemIndex,
            CurrentSystemIndex = currentSystemIndex
        };

        if (targetSystemIndex <= 0)
        {
            state.Status = TradePlanRouteStatus.Unavailable;
            state.FailureReason = failureReason = "target station system is unknown";
            return false;
        }

        if (targetSystemIndex == currentSystemIndex)
        {
            state.Status = TradePlanRouteStatus.LocalStation;
            state.RemainingHopCount = 0;
            return true;
        }

        if (routeAuthority == null ||
            !routeAuthority.TryGetSystemRoute(currentSystemIndex, targetSystemIndex, out MarketSystemRoute route) ||
            route == null || route.Legs.Count == 0)
        {
            state.Status = TradePlanRouteStatus.Unavailable;
            state.FailureReason = failureReason = "NO KNOWN ROUTE";
            return false;
        }

        state.Status = TradePlanRouteStatus.TransitionRequired;
        state.NextTransition = route.Legs[0];
        state.RemainingHopCount = Math.Max(0, route.JumpCount);
        return true;
    }
}
