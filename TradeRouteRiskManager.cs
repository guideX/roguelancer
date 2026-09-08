using Microsoft.Xna.Framework;
using Roguelancer.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Roguelancer;

public enum TradeRouteRiskIncident
{
    TraderDestroyed,
    CargoExtorted,
    RefusalDistress,
    PartialCargoLoss,
    SafeDelivery
}

/// <summary>
/// Bounded, directional memory of recent ordinary trade-route incidents.
/// Risk is deliberately lazy: reading a route applies a small capped decay
/// from the shared market clock, so it neither simulates nor mutates traffic.
/// </summary>
public sealed class TradeRouteRiskManager
{
    public const int MaximumRiskScore = 100;
    public const int DecayPointsPerEconomicMinute = 7;
    public const int TraderDestroyedRiskPoints = 25;
    public const int CargoExtortedRiskPoints = 12;
    public const int RefusalDistressRiskPoints = 5;
    public const int PartialCargoLossRiskPoints = 4;
    public const int SafeDeliveryRecoveryPoints = 3;
    public const int MaximumTrackedRoutes = 64;
    public const int MaximumCatchUpMinutes = 5;
    public const int MaximumValueRiskModifier = 8;
    public const int DisruptedLaneRiskPoints = 20;
    public const int RecoveringLaneRiskPoints = 8;
    public const int MaximumLaneRiskBonus = 25;
    public const int RouteRiskPenaltyPerPoint = 2;
    public const int MaximumRouteRiskPenalty = 200;
    public const int ElevatedRiskThreshold = 20;
    public const int DangerousRiskThreshold = 40;
    public const int SevereRiskThreshold = 70;

    private sealed class RouteRiskState
    {
        public string RouteId { get; init; } = string.Empty;
        public TradeLaneDirection Direction { get; init; }
        public int Score { get; set; }
        public long LastProcessedMilliseconds { get; set; }
    }

    private readonly Func<long> _simulationTimeProvider;
    private readonly Func<IEnumerable<TradeLane>> _tradeLaneProvider;
    private readonly Dictionary<string, RouteRiskState> _states = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _incidentKeys = new(StringComparer.Ordinal);
    private long _manualSimulationTimeMilliseconds;

    public TradeRouteRiskManager(
        Func<long> simulationTimeProvider = null,
        Func<IEnumerable<TradeLane>> tradeLaneProvider = null)
    {
        _simulationTimeProvider = simulationTimeProvider;
        _tradeLaneProvider = tradeLaneProvider ?? (() => Array.Empty<TradeLane>());
    }

    public int TrackedRouteCount
    {
        get
        {
            ApplyDecay();
            return _states.Count;
        }
    }

    public IReadOnlyList<SaveTradeRouteRiskData> CaptureState()
    {
        ApplyDecay();
        return _states.Values
            .Where(state => state != null && state.Score > 0 && !string.IsNullOrWhiteSpace(state.RouteId))
            .OrderBy(state => state.RouteId, StringComparer.OrdinalIgnoreCase)
            .ThenBy(state => state.Direction)
            .Take(MaximumTrackedRoutes)
            .Select(state => new SaveTradeRouteRiskData
            {
                RouteId = state.RouteId,
                Direction = state.Direction,
                Score = state.Score,
                LastProcessedMilliseconds = state.LastProcessedMilliseconds,
                IncidentKeys = _incidentKeys
                    .Where(key => key.StartsWith(
                        BuildKey(state.RouteId, state.Direction) + "|",
                        StringComparison.OrdinalIgnoreCase))
                    .Take(MaximumTrackedRoutes * 4)
                    .ToList()
            })
            .ToList();
    }

    public int GetRisk(string routeId, TradeLaneDirection direction)
    {
        if (string.IsNullOrWhiteSpace(routeId))
            return 0;

        ApplyDecay();
        return _states.TryGetValue(BuildKey(routeId, direction), out RouteRiskState state)
            ? Math.Clamp(state.Score, 0, MaximumRiskScore)
            : 0;
    }

    public int GetEffectiveRisk(string routeId, TradeLaneDirection direction)
    {
        int routeRisk = GetRisk(routeId, direction);
        int laneRisk = GetDerivedLaneRisk(routeId, direction, null, null);
        return Math.Clamp(routeRisk + laneRisk, 0, MaximumRiskScore);
    }

    public int GetEffectiveRisk(TrafficZoneConfig route, bool routeTowardEnd)
    {
        if (route == null || string.IsNullOrWhiteSpace(route.Id))
            return 0;

        TradeLaneDirection direction = routeTowardEnd ? TradeLaneDirection.Forward : TradeLaneDirection.Reverse;
        int routeRisk = GetRisk(route.Id, direction);
        int laneRisk = GetDerivedLaneRisk(route.Id, direction, route, null);
        return Math.Clamp(routeRisk + laneRisk, 0, MaximumRiskScore);
    }

    public int GetRouteRiskPenalty(string routeId, TradeLaneDirection direction)
    {
        return Math.Clamp(
            GetEffectiveRisk(routeId, direction) * RouteRiskPenaltyPerPoint,
            0,
            MaximumRouteRiskPenalty);
    }

    public int GetRouteRiskPenalty(TrafficZoneConfig route, bool routeTowardEnd)
    {
        return Math.Clamp(
            GetEffectiveRisk(route, routeTowardEnd) * RouteRiskPenaltyPerPoint,
            0,
            MaximumRouteRiskPenalty);
    }

    public string GetRiskLabel(string routeId, TradeLaneDirection direction) =>
        GetRiskLabel(GetEffectiveRisk(routeId, direction));

    public static string GetRiskLabel(int score)
    {
        score = Math.Clamp(score, 0, MaximumRiskScore);
        return score >= SevereRiskThreshold ? "SEVERE" :
            score >= DangerousRiskThreshold ? "DANGEROUS" :
            score >= ElevatedRiskThreshold ? "ELEVATED" : "SAFE";
    }

    public bool RecordIncident(
        string routeId,
        TradeLaneDirection direction,
        TradeRouteRiskIncident incident,
        int shipmentValue = 0,
        string incidentKey = null)
    {
        if (string.IsNullOrWhiteSpace(routeId))
            return false;

        string canonicalIncidentKey = string.IsNullOrWhiteSpace(incidentKey)
            ? null
            : BuildIncidentKey(routeId, direction, incidentKey);
        if (canonicalIncidentKey != null && !_incidentKeys.Add(canonicalIncidentKey))
            return false;

        int points = incident switch
        {
            TradeRouteRiskIncident.TraderDestroyed => TraderDestroyedRiskPoints,
            TradeRouteRiskIncident.CargoExtorted => CargoExtortedRiskPoints,
            TradeRouteRiskIncident.RefusalDistress => RefusalDistressRiskPoints,
            TradeRouteRiskIncident.PartialCargoLoss => PartialCargoLossRiskPoints,
            TradeRouteRiskIncident.SafeDelivery => -SafeDeliveryRecoveryPoints,
            _ => 0
        };

        if (incident is TradeRouteRiskIncident.TraderDestroyed or
            TradeRouteRiskIncident.CargoExtorted or
            TradeRouteRiskIncident.PartialCargoLoss)
        {
            points += Math.Clamp(Math.Max(0, shipmentValue) / 1_000, 0, MaximumValueRiskModifier);
        }

        ApplyDecay();
        long now = GetSimulationTimeMilliseconds();
        string key = BuildKey(routeId, direction);
        if (!_states.TryGetValue(key, out RouteRiskState state))
        {
            if (_states.Count >= MaximumTrackedRoutes)
            {
                RouteRiskState evicted = _states.Values
                    .OrderBy(candidate => candidate.Score)
                    .ThenBy(candidate => candidate.LastProcessedMilliseconds)
                    .FirstOrDefault();
                if (evicted == null || points <= 0)
                    return false;
                _states.Remove(BuildKey(evicted.RouteId, evicted.Direction));
            }

            state = new RouteRiskState
            {
                RouteId = routeId.Trim(),
                Direction = direction,
                LastProcessedMilliseconds = now
            };
            _states[key] = state;
        }

        state.Score = Math.Clamp(state.Score + points, 0, MaximumRiskScore);
        state.LastProcessedMilliseconds = now;
        if (state.Score == 0)
            _states.Remove(key);
        TrimIncidentKeys();
        return true;
    }

    public bool RecordTraderDestroyed(string routeId, TradeLaneDirection direction, int shipmentValue = 0, string incidentKey = null) =>
        RecordIncident(routeId, direction, TradeRouteRiskIncident.TraderDestroyed, shipmentValue, incidentKey);

    public bool RecordCargoExtorted(string routeId, TradeLaneDirection direction, int shipmentValue = 0, string incidentKey = null) =>
        RecordIncident(routeId, direction, TradeRouteRiskIncident.CargoExtorted, shipmentValue, incidentKey);

    public bool RecordRefusalDistress(string routeId, TradeLaneDirection direction, string incidentKey = null) =>
        RecordIncident(routeId, direction, TradeRouteRiskIncident.RefusalDistress, 0, incidentKey);

    public bool RecordPartialCargoLoss(string routeId, TradeLaneDirection direction, int shipmentValue = 0, string incidentKey = null) =>
        RecordIncident(routeId, direction, TradeRouteRiskIncident.PartialCargoLoss, shipmentValue, incidentKey);

    public bool RecordSafeDelivery(string routeId, TradeLaneDirection direction, string incidentKey = null) =>
        RecordIncident(routeId, direction, TradeRouteRiskIncident.SafeDelivery, 0, incidentKey);

    /// <summary>Advances only the risk clock for deterministic smoke tests.</summary>
    public void AdvanceTime(double seconds)
    {
        if (_simulationTimeProvider != null || double.IsNaN(seconds) || double.IsInfinity(seconds) || seconds <= 0d)
            return;
        _manualSimulationTimeMilliseconds += (long)Math.Clamp(seconds * 1000d, 0d, long.MaxValue);
        ApplyDecay();
    }

    public void RestoreState(IEnumerable<SaveTradeRouteRiskData> states)
    {
        _states.Clear();
        _incidentKeys.Clear();
        long now = GetSimulationTimeMilliseconds();
        foreach (SaveTradeRouteRiskData saved in states ?? Array.Empty<SaveTradeRouteRiskData>())
        {
            if (saved == null || string.IsNullOrWhiteSpace(saved.RouteId) ||
                !Enum.IsDefined(typeof(TradeLaneDirection), saved.Direction) || saved.Score <= 0)
                continue;
            if (_states.Count >= MaximumTrackedRoutes)
                break;
            _states[BuildKey(saved.RouteId, saved.Direction)] = new RouteRiskState
            {
                RouteId = saved.RouteId.Trim(),
                Direction = saved.Direction,
                Score = Math.Clamp(saved.Score, 1, MaximumRiskScore),
                LastProcessedMilliseconds = Math.Clamp(saved.LastProcessedMilliseconds, 0L, now)
            };
            foreach (string incidentKey in saved.IncidentKeys ?? new List<string>())
            {
                if (!string.IsNullOrWhiteSpace(incidentKey))
                    _incidentKeys.Add(incidentKey.Trim());
            }
        }
        TrimIncidentKeys();
        ApplyDecay();
    }

    public void Reset()
    {
        _states.Clear();
        _incidentKeys.Clear();
        _manualSimulationTimeMilliseconds = 0L;
    }

    private void ApplyDecay()
    {
        long now = GetSimulationTimeMilliseconds();
        foreach (RouteRiskState state in _states.Values.ToList())
        {
            if (state == null)
                continue;
            long elapsed = Math.Max(0L, now - state.LastProcessedMilliseconds);
            long cappedElapsed = Math.Min(elapsed, MaximumCatchUpMinutes * 60_000L);
            int decay = (int)(cappedElapsed / 60_000L) * DecayPointsPerEconomicMinute;
            if (decay > 0)
            {
                state.Score = Math.Max(0, state.Score - decay);
                state.LastProcessedMilliseconds = now;
            }
            if (state.Score <= 0)
                _states.Remove(BuildKey(state.RouteId, state.Direction));
        }
    }

    private int GetDerivedLaneRisk(
        string routeId,
        TradeLaneDirection direction,
        TrafficZoneConfig route,
        IEnumerable<TradeLane> explicitLanes)
    {
        // Route IDs remain authoritative. Geometry is only an optional,
        // read-only bridge to transient lane disruption state.
        if (route == null || route.RouteStart is not Vector3 routeStart || route.RouteEnd is not Vector3 routeEnd)
            return 0;

        TradeLane bestLane = null;
        float bestDistance = float.MaxValue;
        foreach (TradeLane lane in explicitLanes ?? _tradeLaneProvider() ?? Array.Empty<TradeLane>())
        {
            if (lane?.Config == null || lane.IsBroken || lane.Config.SystemIndex != route.SystemIndex)
                continue;

            Vector3 expectedStart = direction == TradeLaneDirection.Forward
                ? lane.Config.StartPosition
                : lane.Config.EndPosition;
            Vector3 expectedEnd = direction == TradeLaneDirection.Forward
                ? lane.Config.EndPosition
                : lane.Config.StartPosition;
            float distance = Vector3.DistanceSquared(routeStart, expectedStart) +
                Vector3.DistanceSquared(routeEnd, expectedEnd);
            const float maximumEndpointDistance = 7_500f;
            if (distance <= maximumEndpointDistance * maximumEndpointDistance * 2f && distance < bestDistance)
            {
                bestDistance = distance;
                bestLane = lane;
            }
        }

        if (bestLane == null)
            return 0;

        int bonus = 0;
        foreach (TradeLaneDisruptionInfo disruption in bestLane.GetActiveDisruptions() ?? Array.Empty<TradeLaneDisruptionInfo>())
        {
            bonus = Math.Max(bonus, disruption.State == TradeLaneDisruptionState.Disrupted
                ? DisruptedLaneRiskPoints
                : RecoveringLaneRiskPoints);
        }
        return Math.Clamp(bonus, 0, MaximumLaneRiskBonus);
    }

    private long GetSimulationTimeMilliseconds() =>
        _simulationTimeProvider?.Invoke() is long provided && provided >= 0
            ? provided
            : Math.Max(0L, _manualSimulationTimeMilliseconds);

    private static string BuildKey(string routeId, TradeLaneDirection direction) =>
        $"{routeId?.Trim()}|{direction}";

    private static string BuildIncidentKey(
        string routeId,
        TradeLaneDirection direction,
        string incidentKey) =>
        $"{BuildKey(routeId, direction)}|{incidentKey?.Trim()}";

    private void TrimIncidentKeys()
    {
        if (_incidentKeys.Count <= MaximumTrackedRoutes * 4)
            return;
        foreach (string key in _incidentKeys.Take(_incidentKeys.Count - MaximumTrackedRoutes * 3).ToList())
            _incidentKeys.Remove(key);
    }
}
