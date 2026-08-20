using Microsoft.Xna.Framework;
using Roguelancer.Configuration;
using System;
using System.Collections.Generic;

namespace Roguelancer;

public sealed class MarketRouteMetric
{
    public bool IsReachable { get; internal set; }
    public float DistanceUnits { get; internal set; }
    public int JumpCount { get; internal set; }
}

/// <summary>
/// Reuses station world coordinates/system indices and the existing jump-hole
/// configuration. Same-system routes use the existing navigation coordinate
/// distance; cross-system routes use the configured jump-hole graph.
/// </summary>
public sealed class MarketRouteAuthority
{
    private readonly IReadOnlyList<JumpHoleConfig> _jumpHoles;

    public MarketRouteAuthority(IReadOnlyList<JumpHoleConfig> jumpHoles = null)
    {
        _jumpHoles = jumpHoles ?? Array.Empty<JumpHoleConfig>();
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
            if (float.IsNaN(distance) || float.IsInfinity(distance) || distance <= 0f) return false;
            metric = new MarketRouteMetric { IsReachable = true, DistanceUnits = distance, JumpCount = 0 };
            return true;
        }

        if (!TryFindSystemPath(origin.SystemIndex, destination.SystemIndex, out List<JumpHoleConfig> path))
            return false;

        float routeDistance = Vector3.Distance(origin.StationPosition, path[0].Position);
        int currentSystem = origin.SystemIndex;
        Vector3 currentPosition = path[0].Position;
        foreach (JumpHoleConfig jumpHole in path)
        {
            if (jumpHole.SystemIndex != currentSystem) return false;
            JumpHoleConfig arrival = FindArrival(jumpHole);
            if (arrival == null) return false;
            routeDistance += Vector3.Distance(jumpHole.Position, arrival.Position);
            currentSystem = arrival.SystemIndex;
            currentPosition = arrival.Position;
            if (currentSystem != destination.SystemIndex)
            {
                JumpHoleConfig next = path.Find(candidate => candidate.SystemIndex == currentSystem);
                if (next == null) return false;
                routeDistance += Vector3.Distance(currentPosition, next.Position);
                currentPosition = next.Position;
            }
        }

        routeDistance += Vector3.Distance(currentPosition, destination.StationPosition);
        if (float.IsNaN(routeDistance) || float.IsInfinity(routeDistance) || routeDistance <= 0f) return false;
        metric = new MarketRouteMetric
        {
            IsReachable = true,
            DistanceUnits = routeDistance,
            JumpCount = path.Count
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

        while (queue.Count > 0)
        {
            int system = queue.Dequeue();
            if (system == destinationSystem) break;
            foreach (JumpHoleConfig edge in _jumpHoles)
            {
                if (edge == null || edge.SystemIndex != system || edge.TargetSystemIndex <= 0 ||
                    visited.ContainsKey(edge.TargetSystemIndex) || FindArrival(edge) == null)
                    continue;
                visited[edge.TargetSystemIndex] = (system, edge);
                queue.Enqueue(edge.TargetSystemIndex);
            }
        }

        if (!visited.ContainsKey(destinationSystem)) return false;
        path = new List<JumpHoleConfig>();
        for (int system = destinationSystem; system != originSystem; system = visited[system].Previous)
            path.Add(visited[system].Edge);
        path.Reverse();
        return path.Count > 0;
    }

    private JumpHoleConfig FindArrival(JumpHoleConfig departure)
    {
        if (departure == null) return null;
        foreach (JumpHoleConfig candidate in _jumpHoles)
        {
            if (candidate != null && candidate.SystemIndex == departure.TargetSystemIndex &&
                string.Equals(candidate.Name, departure.TargetJumpHoleName, StringComparison.OrdinalIgnoreCase))
                return candidate;
        }
        return null;
    }
}
