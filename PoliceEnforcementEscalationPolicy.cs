#nullable enable

using System;

namespace Roguelancer;

/// <summary>
/// Repeat-smuggler enforcement tier. This is derived state, never persisted:
/// it is recomputed from the authoritative Liberty Police standing whenever
/// an enforcement demand is created or fugitive grace is resolved.
/// </summary>
public enum PoliceEnforcementTier
{
    Standard = 0,
    Elevated = 1,
    Severe = 2
}

/// <summary>
/// Phase 74 bounded enforcement-escalation policy. The single durable
/// authority is the existing Liberty Police standing owned by
/// ReputationManager (persisted via faction_reputation); repeated
/// refusal/evasion lowers that standing through the unchanged
/// PoliceEnforcementService penalties, which naturally moves future stops
/// into harsher tiers. No parallel crime counter, wanted level, offense
/// log, or save-schema extension exists here.
/// </summary>
public static class PoliceEnforcementEscalationPolicy
{
    /// <summary>
    /// Standing at or below this value is elevated enforcement. The new-game
    /// Liberty Police standing (-0.25) and a first isolated incident stay
    /// standard; one refusal (-0.20) from the starting profile reaches
    /// -0.45 and therefore elevates the next stop.
    /// </summary>
    public const float ElevatedStandingThreshold = -0.35f;

    /// <summary>
    /// Standing at or below this value is severe enforcement while the
    /// relationship is still lawful. Permanent hostility begins at
    /// ReputationManager.HostileThreshold (-0.60), so the lawful severe
    /// band is (-0.60, -0.50]: reachable through mixed compliance,
    /// combat, and refusal consequences, but never a peaceful-stop
    /// override once genuine hostility holds.
    /// </summary>
    public const float SevereStandingThreshold = -0.50f;

    public const decimal ElevatedFineMultiplier = 1.25m;
    public const decimal SevereFineMultiplier = 1.50m;

    /// <summary>
    /// Tier-aware post-escape re-stop separation. Tier 0 preserves the
    /// Phase 72 20-second grace; elevated tiers shorten it without ever
    /// permitting same-tick immediate reacquisition (the floor stays well
    /// above one scan cycle plus detection range closure).
    /// </summary>
    public const float ElevatedReacquisitionGraceSeconds = 12f;
    public const float SevereReacquisitionGraceSeconds = 6f;
    public const float MinimumReacquisitionGraceSeconds = 6f;

    public static PoliceEnforcementTier GetTier(float standing)
    {
        if (float.IsNaN(standing) || float.IsInfinity(standing))
            return PoliceEnforcementTier.Standard;

        if (standing <= SevereStandingThreshold)
            return PoliceEnforcementTier.Severe;

        return standing <= ElevatedStandingThreshold
            ? PoliceEnforcementTier.Elevated
            : PoliceEnforcementTier.Standard;
    }

    public static PoliceEnforcementTier GetTier(ReputationManager? reputationManager) =>
        reputationManager == null
            ? PoliceEnforcementTier.Standard
            : GetTier(reputationManager.GetStanding(FactionManager.LibertyPolice));

    public static decimal GetFineMultiplier(PoliceEnforcementTier tier) =>
        tier switch
        {
            PoliceEnforcementTier.Severe => SevereFineMultiplier,
            PoliceEnforcementTier.Elevated => ElevatedFineMultiplier,
            _ => 1m
        };

    /// <summary>
    /// Applies the tier multiplier to an already-calculated base violation
    /// fine. The existing absolute safety clamp is preserved: escalation
    /// can never push a fine below the minimum or above the maximum, and
    /// cargo value is never mutated.
    /// </summary>
    public static int ApplyFineEscalation(int baseFine, PoliceEnforcementTier tier)
    {
        if (baseFine <= 0)
            return 0;

        decimal scaled = Math.Ceiling(baseFine * GetFineMultiplier(tier));
        if (scaled < PoliceEnforcementService.MinimumFineCredits)
            return PoliceEnforcementService.MinimumFineCredits;

        return scaled >= PoliceEnforcementService.MaximumFineCredits
            ? PoliceEnforcementService.MaximumFineCredits
            : (int)scaled;
    }

    /// <summary>
    /// Initial fugitive heat for a refusal at the given tier. Only the two
    /// existing bounded heats are used: standard/elevated refusals open the
    /// established Heat 1 pursuit, while a severe lawful refusal opens the
    /// strongest supported Heat 2 pursuit. No third heat level exists.
    /// </summary>
    public static PoliceHeatLevel GetInitialHeat(PoliceEnforcementTier tier) =>
        tier == PoliceEnforcementTier.Severe
            ? PoliceHeatLevel.HotPursuit
            : PoliceHeatLevel.Pursuit;

    public static float GetReacquisitionGraceSeconds(PoliceEnforcementTier tier) =>
        tier switch
        {
            PoliceEnforcementTier.Severe => SevereReacquisitionGraceSeconds,
            PoliceEnforcementTier.Elevated => ElevatedReacquisitionGraceSeconds,
            _ => PoliceFugitiveManager.ContrabandReacquisitionGraceSeconds
        };

    /// <summary>
    /// Concise demand/HUD wording for the tier. Standard stops keep their
    /// existing text; elevated stops name the reason without exposing raw
    /// standing floats or building a wanted-level UI.
    /// </summary>
    public static string GetTierLabel(PoliceEnforcementTier tier) =>
        tier switch
        {
            PoliceEnforcementTier.Severe => "Severe contraband violation — elevated enforcement",
            PoliceEnforcementTier.Elevated => "Repeat smuggling offense — elevated fine",
            _ => string.Empty
        };
}
