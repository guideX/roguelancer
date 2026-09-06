using System;

namespace Roguelancer;

/// <summary>
/// Bounded station-level access policy for illicit commerce. The policy uses
/// existing station/faction identity and the live ReputationManager; it does
/// not persist access or create a second criminal economy.
/// </summary>
public static class BlackMarketPolicy
{
    /// <summary>
    /// Neutral standing is the first accepted Rogue/Junker tier. This keeps
    /// hostile and unfriendly contacts locked while avoiding an Allied gate.
    /// </summary>
    public const float MinimumStanding = 0.00f;

    public static bool IsEligibleHostFaction(string factionId)
    {
        string normalized = FactionManager.NormalizeFactionId(factionId);
        return string.Equals(normalized, FactionManager.LibertyRogues, StringComparison.OrdinalIgnoreCase) ||
               string.Equals(normalized, FactionManager.Junkers, StringComparison.OrdinalIgnoreCase);
    }

    public static FactionAccessResult EvaluateAccess(
        ReputationManager reputationManager,
        string hostFactionId,
        string stationName,
        bool hasMarketHost)
    {
        string normalizedFactionId = FactionManager.NormalizeFactionId(hostFactionId);
        float currentStanding = reputationManager?.GetStanding(normalizedFactionId) ?? 0f;

        if (!hasMarketHost || !IsEligibleHostFaction(normalizedFactionId))
        {
            return FactionAccessResult.Create(
                false,
                normalizedFactionId,
                currentStanding,
                null,
                false,
                $"Black Market unavailable at {stationName ?? "this station"}.",
                reputationManager);
        }

        return FactionAccessService.Evaluate(
            reputationManager,
            new FactionReputationRequirement(normalizedFactionId, MinimumStanding),
            "Black Market");
    }
}
