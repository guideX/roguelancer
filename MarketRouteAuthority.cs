using Microsoft.Xna.Framework;
using Roguelancer.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Roguelancer;

public sealed class MarketRouteMetric
{
    public bool IsReachable { get; internal set; }
    public float DistanceUnits { get; internal set; }
    public int JumpCount { get; internal set; }
}

public enum MarketRouteTransitionType
{
    JumpHole
}

/// <summary>One concrete inter-system transition in a market route.</summary>
public sealed class MarketRouteLeg
{
    public int OriginSystemIndex { get; internal set; }
    public int DestinationSystemIndex { get; internal set; }
    public string TransitionId { get; internal set; } = string.Empty;
    public string TransitionName { get; internal set; } = string.Empty;
    public string ArrivalTransitionId { get; internal set; } = string.Empty;
    public string ArrivalTransitionName { get; internal set; } = string.Empty;
    public MarketRouteTransitionType TransitionType { get; internal set; }

    public string TransitionTypeLabel => TransitionType switch
    {
        MarketRouteTransitionType.JumpHole => "JUMP HOLE",
        _ => "TRANSITION"
    };

    internal JumpHoleConfig DepartureConfig { get; set; }
    internal JumpHoleConfig ArrivalConfig { get; set; }
}

/// <summary>A bounded, deterministic system-only route.</summary>
public sealed class MarketSystemRoute
{
    public int OriginSystemIndex { get; internal set; }
    public int DestinationSystemIndex { get; internal set; }
    public IReadOnlyList<MarketRouteLeg> Legs { get; internal set; } = Array.Empty<MarketRouteLeg>();
    public int JumpCount => Legs?.Count ?? 0;
}

/// <summary>
/// Reuses station world coordinates/system indices and the existing jump-hole
/// configuration. Same-system routes use the existing navigation coordinate
/// distance; cross-system routes use the configured jump-hole graph.
/// </summary>
public sealed class MarketRouteAuthority
{
    private const int MaxVisitedSystems = 4096;
    private readonly IReadOnlyList<JumpHoleConfig> _jumpHoles;

    public MarketRouteAuthority(IReadOnlyList<JumpHoleConfig> jumpHoles = null)
    {
        _jumpHoles = (jumpHoles ?? Array.Empty<JumpHoleConfig>())
            .Where(config => config != null)
            .OrderBy(config => config.SystemIndex)
            .ThenBy(config => config.TargetSystemIndex)
            .ThenBy(config => config.Name ?? string.Empty, StringComparer.OrdinalIgnoreCase)
            .ThenBy(config => config.TargetJumpHoleName ?? string.Empty, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public bool TryGetRoute(MarketKnowledgeStation origin, MarketKnowledgeStation destination, out MarketRouteMetric metric)
    {
        metric = null;
        if (origin == null || destination == null ||
            string.Equals(origin.StationId, destination.StationId, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (origin.SystemIndex == destination.SystemIndex)
        {
            float distance = Vector3.Distance(origin.StationPosition, destination.StationPosition);
            if (!IsPositiveFinite(distance)) return false;
            metric = new MarketRouteMetric { IsReachable = true, DistanceUnits = distance, JumpCount = 0 };
            return true;
        }

        if (!TryGetSystemRoute(origin.SystemIndex, destination.SystemIndex, out MarketSystemRoute route) ||
            route == null || route.Legs.Count == 0)
        {
            return false;
        }

        float routeDistance = Vector3.Distance(origin.StationPosition, route.Legs[0].DepartureConfig.Position);
        Vector3 currentPosition = route.Legs[0].DepartureConfig.Position;
        int currentSystem = origin.SystemIndex;

        for (int i = 0; i < route.Legs.Count; i++)
        {
            MarketRouteLeg leg = route.Legs[i];
            if (leg?.DepartureConfig == null || leg.ArrivalConfig == null || leg.OriginSystemIndex != currentSystem)
                return false;

            if (i > 0)
            {
                routeDistance += Vector3.Distance(currentPosition, leg.DepartureConfig.Position);
            }

            routeDistance += Vector3.Distance(leg.DepartureConfig.Position, leg.ArrivalConfig.Position);
            currentSystem = leg.DestinationSystemIndex;
            currentPosition = leg.ArrivalConfig.Position;
        }

        if (currentSystem != destination.SystemIndex) return false;
        routeDistance += Vector3.Distance(currentPosition, destination.StationPosition);
        if (!IsPositiveFinite(routeDistance)) return false;

        metric = new MarketRouteMetric
        {
            IsReachable = true,
            DistanceUnits = routeDistance,
            JumpCount = route.JumpCount
        };
        return true;
    }

    /// <summary>
    /// Returns a shortest-hop route. Candidate edges and ties are ordered by
    /// destination system, departure name, then arrival name, so the result is
    /// stable even when configuration files are discovered in another order.
    /// </summary>
    public bool TryGetSystemRoute(int originSystem, int destinationSystem, out MarketSystemRoute route)
    {
        route = null;
        if (originSystem <= 0 || destinationSystem <= 0) return false;

        if (originSystem == destinationSystem)
        {
            route = new MarketSystemRoute
            {
                OriginSystemIndex = originSystem,
                DestinationSystemIndex = destinationSystem,
                Legs = Array.Empty<MarketRouteLeg>()
            };
            return true;
        }

        if (!TryFindSystemPath(originSystem, destinationSystem, out List<JumpHoleConfig> path))
            return false;

        List<MarketRouteLeg> legs = new(path.Count);
        foreach (JumpHoleConfig departure in path)
        {
            JumpHoleConfig arrival = FindArrival(departure);
            if (arrival == null) return false;

            legs.Add(new MarketRouteLeg
            {
                OriginSystemIndex = departure.SystemIndex,
                DestinationSystemIndex = departure.TargetSystemIndex,
                TransitionId = GetTransitionId(departure),
                TransitionName = departure.Name ?? string.Empty,
                ArrivalTransitionId = GetTransitionId(arrival),
                ArrivalTransitionName = arrival.Name ?? string.Empty,
                TransitionType = MarketRouteTransitionType.JumpHole,
                DepartureConfig = departure,
                ArrivalConfig = arrival
            });
        }

        route = new MarketSystemRoute
        {
            OriginSystemIndex = originSystem,
            DestinationSystemIndex = destinationSystem,
            Legs = legs
        };
        return true;
    }

    private bool TryFindSystemPath(int originSystem, int destinationSystem, out List<JumpHoleConfig> path)
    {
        path = null;
        Queue<int> queue = new();
        Dictionary<int, (int Previous, JumpHoleConfig Edge)> visited = new();
        queue.Enqueue(originSystem);
        visited[originSystem] = (-1, null);

        while (queue.Count > 0 && visited.Count <= MaxVisitedSystems)
        {
            int system = queue.Dequeue();
            if (system == destinationSystem) break;

            foreach (JumpHoleConfig edge in GetOutgoingEdges(system))
            {
                if (visited.ContainsKey(edge.TargetSystemIndex)) continue;
                visited[edge.TargetSystemIndex] = (system, edge);
                queue.Enqueue(edge.TargetSystemIndex);
            }
        }

        if (!visited.ContainsKey(destinationSystem)) return false;
        path = new List<JumpHoleConfig>();
        for (int system = destinationSystem; system != originSystem; system = visited[system].Previous)
        {
            JumpHoleConfig edge = visited[system].Edge;
            if (edge == null) return false;
            path.Add(edge);
        }

        path.Reverse();
        return path.Count > 0;
    }

    private IEnumerable<JumpHoleConfig> GetOutgoingEdges(int systemIndex)
    {
        foreach (JumpHoleConfig edge in _jumpHoles)
        {
            if (edge.SystemIndex == systemIndex &&
                edge.TargetSystemIndex > 0 &&
                !string.IsNullOrWhiteSpace(edge.Name) &&
                !string.IsNullOrWhiteSpace(edge.TargetJumpHoleName) &&
                FindArrival(edge) != null)
            {
                yield return edge;
            }
        }
    }

    private JumpHoleConfig FindArrival(JumpHoleConfig departure)
    {
        if (departure == null) return null;
        return _jumpHoles.FirstOrDefault(candidate =>
            candidate.SystemIndex == departure.TargetSystemIndex &&
            string.Equals(candidate.Name, departure.TargetJumpHoleName, StringComparison.OrdinalIgnoreCase));
    }

    private static string GetTransitionId(JumpHoleConfig config) =>
        config == null ? string.Empty : $"{config.SystemIndex}:{config.Name}";

    private static bool IsPositiveFinite(float value) =>
        !float.IsNaN(value) && !float.IsInfinity(value) && value > 0f;
}
