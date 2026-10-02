#nullable enable

using System;

namespace Roguelancer;

/// <summary>
/// Phase 75 player-facing readability for the Liberty Police relationship.
/// This is a pure, read-only projection of existing authoritative state: it
/// owns no fields, no timers, and no history. The single source of truth for
/// the enforcement bands remains
/// <see cref="PoliceEnforcementEscalationPolicy.GetTier(ReputationManager?)"/>
/// and the existing <see cref="ReputationManager.HostileThreshold"/> band.
/// Presentation observes that state; it never mutates reputation, fugitive
/// heat, or lawful-stop state.
/// </summary>
public enum LibertyPoliceAttentionLevel
{
    /// <summary>No unusual attention; ordinary Liberty Police relations.</summary>
    Standard = 0,

    /// <summary>Phase 74 elevated band: patrols are paying increased attention.</summary>
    Elevated = 1,

    /// <summary>Phase 74 severe lawful band: substantial scrutiny short of hostility.</summary>
    Severe = 2,

    /// <summary>Existing hostile relationship (standing &lt;= -0.60).</summary>
    Hostile = 3
}

public static class PoliceAttentionPresentation
{
    /// <summary>
    /// Derives the current attention level live from the authoritative Police
    /// standing. Hostility is resolved first so a genuinely hostile
    /// relationship is never described as mere inspection attention.
    /// </summary>
    public static LibertyPoliceAttentionLevel GetLevel(ReputationManager? reputationManager)
    {
        if (reputationManager == null)
            return LibertyPoliceAttentionLevel.Standard;

        if (reputationManager.IsHostile(FactionManager.LibertyPolice))
            return LibertyPoliceAttentionLevel.Hostile;

        return PoliceEnforcementEscalationPolicy.GetTier(reputationManager) switch
        {
            PoliceEnforcementTier.Severe => LibertyPoliceAttentionLevel.Severe,
            PoliceEnforcementTier.Elevated => LibertyPoliceAttentionLevel.Elevated,
            _ => LibertyPoliceAttentionLevel.Standard
        };
    }

    /// <summary>
    /// Short diegetic sentence for ambient delivery (bar/patrol chatter).
    /// Standard returns empty because ordinary relations have nothing unusual
    /// to report.
    /// </summary>
    public static string GetReadout(ReputationManager? reputationManager) => GetLevel(reputationManager) switch
    {
        LibertyPoliceAttentionLevel.Hostile => "Liberty Police regard you as hostile.",
        LibertyPoliceAttentionLevel.Severe => "Word is Liberty Police have your ship flagged for extra scrutiny.",
        LibertyPoliceAttentionLevel.Elevated => "You've been attracting some attention from Liberty patrols lately.",
        _ => string.Empty
    };

    /// <summary>
    /// Always-populated line for the existing reputation overview UI so the
    /// standard relationship still reads as ordinary rather than blank.
    /// </summary>
    public static string GetOverviewLine(ReputationManager? reputationManager) => GetLevel(reputationManager) switch
    {
        LibertyPoliceAttentionLevel.Hostile => "LIBERTY POLICE ATTENTION: HOSTILE — patrols will treat you as an enemy.",
        LibertyPoliceAttentionLevel.Severe => "LIBERTY POLICE ATTENTION: ELEVATED SCRUTINY — another incident could have serious consequences.",
        LibertyPoliceAttentionLevel.Elevated => "LIBERTY POLICE ATTENTION: RECENT ACTIVITY NOTED — patrols are watching more closely.",
        _ => "LIBERTY POLICE ATTENTION: NORMAL — no unusual attention."
    };

    /// <summary>
    /// True when the station faction is part of Liberty space, so Liberty
    /// Police-specific dialogue is contextually appropriate.
    /// </summary>
    public static bool IsLibertyAffiliated(string? factionId)
    {
        string normalized = FactionManager.NormalizeFactionId(factionId);
        return string.Equals(normalized, FactionManager.LibertyPolice, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(normalized, FactionManager.LibertyNavy, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(normalized, FactionManager.LibertyCorporations, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Ambient Liberty Police line for a station social NPC, or empty when the
    /// station is outside Liberty space or relations are ordinary.
    /// </summary>
    public static string GetStationAmbientLine(string? stationFactionId, ReputationManager? reputationManager)
    {
        if (!IsLibertyAffiliated(stationFactionId))
            return string.Empty;

        return GetReadout(reputationManager);
    }
}
