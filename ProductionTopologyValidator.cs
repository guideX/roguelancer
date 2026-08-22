using Roguelancer.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Roguelancer;

public enum ProductionTopologyIssueClassification
{
    Malformed,
    UnresolvedLegacyData
}

public sealed class ProductionTopologyIssue
{
    public ProductionTopologyIssueClassification Classification { get; internal set; }
    public string Source { get; internal set; } = string.Empty;
    public string Message { get; internal set; } = string.Empty;

    public override string ToString() => $"[{Classification}] {Source}: {Message}";
}

/// <summary>
/// Bounded validation of the production system/station/jump graph. This is
/// intentionally a tool/test helper; normal gameplay only asks the route
/// authority for the route it currently needs.
/// </summary>
public sealed class ProductionTopologyReport
{
    internal readonly List<ProductionTopologyIssue> IssuesInternal = new();

    public int ConfiguredSystemCount { get; internal set; }
    public int ConfiguredStationCount { get; internal set; }
    public int ConfiguredJumpHoleCount { get; internal set; }
    public IReadOnlyList<ProductionTopologyIssue> Issues => IssuesInternal;
    public IReadOnlyList<ProductionTopologyIssue> UnresolvedLegacyLinks =>
        IssuesInternal.Where(issue => issue.Classification == ProductionTopologyIssueClassification.UnresolvedLegacyData).ToList();
    public IReadOnlyList<ProductionTopologyIssue> MalformedLinks =>
        IssuesInternal.Where(issue => issue.Classification == ProductionTopologyIssueClassification.Malformed).ToList();

    public bool HasIssues => IssuesInternal.Count > 0;
}

public static class ProductionTopologyValidator
{
    public static ProductionTopologyReport Validate(ConfigurationManager configuration)
    {
        ProductionTopologyReport report = new();
        if (configuration == null) return report;

        IReadOnlyList<SystemConfig> systems = configuration.Systems ?? new List<SystemConfig>();
        IReadOnlyList<StationConfig> stations = configuration.Stations ?? new List<StationConfig>();
        IReadOnlyList<JumpHoleConfig> jumpHoles = configuration.JumpHoles ?? new List<JumpHoleConfig>();
        HashSet<int> systemIds = systems
            .Where(system => system != null && system.SystemIndex > 0)
            .Select(system => system.SystemIndex)
            .ToHashSet();

        report.ConfiguredSystemCount = systems.Count;
        report.ConfiguredStationCount = stations.Count;
        report.ConfiguredJumpHoleCount = jumpHoles.Count;

        Dictionary<string, JumpHoleConfig> transitionIds = new(StringComparer.OrdinalIgnoreCase);
        foreach (JumpHoleConfig jumpHole in jumpHoles.Where(candidate => candidate != null))
        {
            string source = jumpHole.CanonicalId;
            bool originExists = systemIds.Contains(jumpHole.SystemIndex);
            bool targetExists = systemIds.Contains(jumpHole.TargetSystemIndex);

            if (!originExists || !targetExists)
            {
                AddIssue(
                    report,
                    ProductionTopologyIssueClassification.UnresolvedLegacyData,
                    source,
                    $"references {(originExists ? string.Empty : $"missing origin system {jumpHole.SystemIndex}")}{(!originExists && !targetExists ? " and " : string.Empty)}{(targetExists ? string.Empty : $"missing destination system {jumpHole.TargetSystemIndex}")}");
            }

            if (jumpHole.SystemIndex <= 0 || jumpHole.TargetSystemIndex <= 0 ||
                string.IsNullOrWhiteSpace(jumpHole.Name) || string.IsNullOrWhiteSpace(jumpHole.TargetJumpHoleName))
            {
                AddIssue(report, ProductionTopologyIssueClassification.Malformed, source, "has an incomplete canonical transition identity");
                continue;
            }

            if (transitionIds.TryGetValue(jumpHole.CanonicalId, out JumpHoleConfig existing) &&
                (existing.TargetSystemIndex != jumpHole.TargetSystemIndex ||
                 !string.Equals(existing.TargetJumpHoleName, jumpHole.TargetJumpHoleName, StringComparison.OrdinalIgnoreCase)))
            {
                AddIssue(report, ProductionTopologyIssueClassification.Malformed, source, "duplicate transition id has conflicting destination metadata");
            }
            else
            {
                transitionIds[jumpHole.CanonicalId] = jumpHole;
            }

            JumpHoleConfig arrival = jumpHoles.FirstOrDefault(candidate =>
                candidate != null &&
                candidate.SystemIndex == jumpHole.TargetSystemIndex &&
                string.Equals(candidate.Name?.Trim(), jumpHole.TargetJumpHoleName?.Trim(), StringComparison.OrdinalIgnoreCase));
            if (arrival == null)
            {
                AddIssue(
                    report,
                    targetExists
                        ? ProductionTopologyIssueClassification.Malformed
                        : ProductionTopologyIssueClassification.UnresolvedLegacyData,
                    source,
                    $"arrival '{jumpHole.TargetJumpHoleName}' is not configured in system {jumpHole.TargetSystemIndex}");
            }
        }

        foreach (StationConfig station in stations.Where(candidate => candidate != null))
        {
            if (!systemIds.Contains(station.SystemIndex))
            {
                AddIssue(
                    report,
                    ProductionTopologyIssueClassification.UnresolvedLegacyData,
                    station.Description ?? "<unnamed station>",
                    $"references missing system {station.SystemIndex}");
            }
        }

        return report;
    }

    public static bool TryGetRoute(
        ConfigurationManager configuration,
        int originSystem,
        int destinationSystem,
        out MarketSystemRoute route)
    {
        route = null;
        if (configuration == null) return false;
        if (configuration.GetSystem(originSystem) == null || configuration.GetSystem(destinationSystem) == null)
        {
            return false;
        }

        return new MarketRouteAuthority(configuration.JumpHoles).TryGetSystemRoute(originSystem, destinationSystem, out route);
    }

    private static void AddIssue(
        ProductionTopologyReport report,
        ProductionTopologyIssueClassification classification,
        string source,
        string message)
    {
        if (report.IssuesInternal.Any(issue =>
            issue.Classification == classification &&
            string.Equals(issue.Source, source, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(issue.Message, message, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        report.IssuesInternal.Add(new ProductionTopologyIssue
        {
            Classification = classification,
            Source = source ?? string.Empty,
            Message = message ?? string.Empty
        });
    }
}
