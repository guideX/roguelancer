#nullable enable

using System;

namespace Roguelancer;

/// <summary>
/// Static access metadata. It is deliberately not persisted with player state;
/// the current ReputationManager value is evaluated whenever access is queried.
/// </summary>
public sealed class FactionReputationRequirement
{
    public FactionReputationRequirement(string factionId, float minimumStanding)
    {
        FactionId = FactionManager.NormalizeFactionId(factionId);
        MinimumStanding = minimumStanding;
    }

    public string FactionId { get; }
    public float MinimumStanding { get; }

    public string BuildRequirementLine(ReputationManager? reputationManager)
    {
        string factionName = reputationManager?.FactionManager.GetFaction(FactionId).DisplayName
            ?? FactionManager.GetFactionDisplayName(FactionId);
        return $"Requires {factionName}: {ReputationPresentation.FormatBand(ReputationManager.GetMinimumRequirementBand(MinimumStanding))} ({ReputationManager.FormatStanding(MinimumStanding)})";
    }
}

/// <summary>
/// A live result for a service or offer. The result is presentation-friendly,
/// but the authority remains the reputation manager queried during evaluation.
/// </summary>
public sealed class FactionAccessResult
{
    private FactionAccessResult(
        bool isAllowed,
        string factionId,
        float currentStanding,
        float? minimumStanding,
        bool isTemporarilyHostile,
        string failureMessage,
        ReputationManager? reputationManager)
    {
        IsAllowed = isAllowed;
        FactionId = FactionManager.NormalizeFactionId(factionId);
        CurrentStanding = currentStanding;
        MinimumStanding = minimumStanding;
        IsTemporarilyHostile = isTemporarilyHostile;
        FailureMessage = failureMessage ?? string.Empty;
        FactionDisplayName = reputationManager?.FactionManager.GetFaction(FactionId).DisplayName
            ?? FactionManager.GetFactionDisplayName(FactionId);
        CurrentBand = ReputationManager.GetBandForStanding(CurrentStanding);
    }

    public bool IsAllowed { get; }
    public string FactionId { get; }
    public string FactionDisplayName { get; }
    public float CurrentStanding { get; }
    public ReputationBand CurrentBand { get; }
    public float? MinimumStanding { get; }
    public bool IsTemporarilyHostile { get; }
    public string FailureMessage { get; }

    public string BuildCurrentStandingLine() =>
        $"{FactionDisplayName} — {ReputationPresentation.FormatBand(CurrentBand)} ({ReputationManager.FormatStanding(CurrentStanding)})";

    public string BuildRequirementLine(ReputationManager? reputationManager)
    {
        if (!MinimumStanding.HasValue)
            return "Requires: NONE";

        return new FactionReputationRequirement(FactionId, MinimumStanding.Value)
            .BuildRequirementLine(reputationManager);
    }

    public string BuildFailureMessage(string subject)
    {
        if (IsAllowed)
            return string.Empty;

        if (IsTemporarilyHostile)
            return "Service unavailable while faction forces are hostile.";

        if (!MinimumStanding.HasValue)
            return string.IsNullOrWhiteSpace(FailureMessage)
                ? "Service unavailable."
                : FailureMessage;

        string name = string.IsNullOrWhiteSpace(subject) ? "This offer" : subject;
        return string.IsNullOrWhiteSpace(FailureMessage)
            ? $"{name} locked: {BuildCurrentStandingLine()} does not meet {ReputationManager.GetMinimumRequirementBand(MinimumStanding.Value)} ({ReputationManager.FormatStanding(MinimumStanding.Value)}) required."
            : FailureMessage;
    }

    internal static FactionAccessResult Create(
        bool isAllowed,
        string factionId,
        float currentStanding,
        float? minimumStanding,
        bool isTemporarilyHostile,
        string failureMessage,
        ReputationManager? reputationManager) =>
        new(isAllowed, factionId, currentStanding, minimumStanding, isTemporarilyHostile, failureMessage, reputationManager);
}

/// <summary>
/// Shared access policy for faction-controlled station services, offers, and docking.
/// </summary>
public static class FactionAccessService
{
    // A service remains available through Unfriendly standing, but a hostile
    // persistent relationship is refused. The epsilon keeps the displayed
    // requirement at the existing -0.60 hostile boundary.
    public const float StationServiceMinimumStanding =
        ReputationManager.HostileThreshold + ReputationManager.Precision;

    /// <summary>
    /// Permanent docking denial begins at the existing Hostile band boundary.
    /// Values above this boundary remain dockable unless temporary hostility is active.
    /// </summary>
    public const float DockingHostileThreshold = ReputationManager.HostileThreshold;

    public static FactionAccessResult EvaluateDocking(
        ReputationManager? reputationManager,
        string? factionId,
        string? stationName = null)
    {
        string normalizedFactionId = FactionManager.NormalizeFactionId(factionId);
        float currentStanding = reputationManager?.GetStanding(normalizedFactionId) ?? 0f;
        bool temporarilyHostile = reputationManager?.IsTemporarilyHostile(normalizedFactionId) == true;
        bool permanentlyHostile = reputationManager?.IsHostile(normalizedFactionId) == true;
        bool allowed = reputationManager == null || (!temporarilyHostile && !permanentlyHostile);

        string stationSuffix = string.IsNullOrWhiteSpace(stationName)
            ? string.Empty
            : $" at {stationName.Trim()}";
        string failure = temporarilyHostile
            ? $"Docking denied — {FactionManager.GetFactionDisplayName(normalizedFactionId)} are temporarily hostile{stationSuffix}."
            : permanentlyHostile
                ? $"Docking denied — hostile with {FactionManager.GetFactionDisplayName(normalizedFactionId)}{stationSuffix}."
                : string.Empty;

        return FactionAccessResult.Create(
            allowed,
            normalizedFactionId,
            currentStanding,
            null,
            temporarilyHostile,
            failure,
            reputationManager);
    }

    public static FactionAccessResult EvaluateService(
        ReputationManager? reputationManager,
        string? factionId,
        string serviceName)
    {
        string normalizedFactionId = FactionManager.NormalizeFactionId(factionId);
        return Evaluate(
            reputationManager,
            new FactionReputationRequirement(normalizedFactionId, StationServiceMinimumStanding),
            serviceName);
    }

    public static FactionAccessResult Evaluate(
        ReputationManager? reputationManager,
        FactionReputationRequirement requirement,
        string subject)
    {
        if (requirement == null)
        {
            return FactionAccessResult.Create(
                true,
                FactionManager.NeutralCivilians,
                0f,
                null,
                false,
                string.Empty,
                reputationManager);
        }

        float currentStanding = reputationManager?.GetStanding(requirement.FactionId) ?? 0f;
        bool temporarilyHostile = reputationManager?.IsTemporarilyHostile(requirement.FactionId) == true;
        bool meetsStanding = reputationManager == null || reputationManager.MeetsRequirement(
            requirement.FactionId,
            requirement.MinimumStanding);
        bool allowed = !temporarilyHostile && meetsStanding;

        string failure = string.Empty;
        if (temporarilyHostile)
        {
            failure = "Service unavailable while faction forces are hostile.";
        }
        else if (!meetsStanding)
        {
            string name = string.IsNullOrWhiteSpace(subject) ? "This offer" : subject;
            failure = $"{name} locked: {FactionManager.GetFactionDisplayName(requirement.FactionId)} standing " +
                $"{ReputationPresentation.FormatBand(ReputationManager.GetBandForStanding(currentStanding))} " +
                $"({ReputationManager.FormatStanding(currentStanding)}) requires " +
                $"{ReputationPresentation.FormatBand(ReputationManager.GetMinimumRequirementBand(requirement.MinimumStanding))} " +
                $"({ReputationManager.FormatStanding(requirement.MinimumStanding)}).";
        }

        return FactionAccessResult.Create(
            allowed,
            requirement.FactionId,
            currentStanding,
            requirement.MinimumStanding,
            temporarilyHostile,
            failure,
            reputationManager);
    }
}
