#nullable enable

using System;

namespace Roguelancer;

/// <summary>
/// Phase 76 bounded Liberty Police attention transition feedback. This is a
/// pure observer over one authoritative <see cref="ReputationChangeResult"/>:
/// it derives the Phase 75 attention level from the change's previous and
/// current standing and emits at most one notification when the level
/// materially changes.
///
/// It owns no persisted state, no enforcement authority, no fugitive heat, no
/// scan or lawful-stop state, and no criminal history. The authoritative
/// answer always remains the live standing-derived level. Because the
/// reputation change already carries both endpoints, no transient cache is
/// required to detect a transition, which makes new-game and save/load
/// initialization silent by construction.
///
/// Hostility transitions are deliberately left to the existing faction
/// reputation/hostility feedback so the player is never told twice.
/// </summary>
public sealed class PoliceAttentionTransitionNotifier
{
    private readonly ReputationManager _reputationManager;
    private readonly Action<string> _notify;

    public PoliceAttentionTransitionNotifier(ReputationManager reputationManager, Action<string> notify)
    {
        _reputationManager = reputationManager ?? throw new ArgumentNullException(nameof(reputationManager));
        _notify = notify ?? throw new ArgumentNullException(nameof(notify));
    }

    /// <summary>
    /// Observes one authoritative reputation mutation. Returns true when this
    /// notifier produced the Liberty Police attention feedback for that
    /// mutation, so a caller that also owns generic reputation messaging can
    /// suppress a duplicate line. Returns false when the change did not touch
    /// Liberty Police, did not change the derived attention level, or belongs
    /// to an existing hostility/recovery feedback path.
    /// </summary>
    public bool TryObserve(ReputationChangeResult? change)
    {
        if (change == null || !IsLibertyPolice(change.FactionId))
            return false;

        LibertyPoliceAttentionLevel previous = PoliceAttentionPresentation.GetLevel(change.OldValue);
        LibertyPoliceAttentionLevel current = PoliceAttentionPresentation.GetLevel(_reputationManager);
        if (previous == current)
            return false;

        // Hostility and hostility recovery are already owned by the existing
        // faction band/hostility feedback. Do not add a second voice.
        if (previous == LibertyPoliceAttentionLevel.Hostile || current == LibertyPoliceAttentionLevel.Hostile)
            return false;

        string? message = BuildTransitionMessage(previous, current);
        if (string.IsNullOrEmpty(message))
            return false;

        _notify(message!);
        return true;
    }

    /// <summary>
    /// Maps one non-hostile attention transition to its bounded notification
    /// text. Multi-band jumps resolve to the single final state, so a direct
    /// Severe to Standard recovery emits only the recovery line.
    /// </summary>
    public static string? BuildTransitionMessage(
        LibertyPoliceAttentionLevel previous,
        LibertyPoliceAttentionLevel current)
    {
        if (previous == current)
            return null;

        if (current > previous)
        {
            return current == LibertyPoliceAttentionLevel.Severe
                ? "Liberty Police attention increased — you are under elevated scrutiny."
                : "Liberty Police attention increased — patrols are watching you more closely.";
        }

        return current == LibertyPoliceAttentionLevel.Elevated
            ? "Liberty Police attention decreased, but patrols remain wary."
            : "Liberty Police attention has returned to normal.";
    }

    private static bool IsLibertyPolice(string? factionId) =>
        string.Equals(
            FactionManager.NormalizeFactionId(factionId),
            FactionManager.LibertyPolice,
            StringComparison.OrdinalIgnoreCase);
}
