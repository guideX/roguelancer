#nullable enable

using System;
using System.Collections.Generic;

namespace Roguelancer;

/// <summary>
/// Phase 78 bounded eligibility for a lawful Liberty Police fugitive
/// surrender. The enum exists only so tests and the player-facing message can
/// distinguish refusal reasons; it is never persisted and carries no
/// authority of its own.
/// </summary>
public enum PoliceSurrenderEligibility
{
    Eligible,
    NotFugitive,
    PlayerDestroyed,
    NoPolicePresence,
    PermanentlyHostile,
    InsufficientCredits
}

/// <summary>
/// Read-only surrender assessment. It contains no mutation authority; the
/// transaction is re-evaluated before any consequence is applied.
/// </summary>
public sealed class PoliceSurrenderAssessment
{
    public bool IsEligible { get; init; }
    public PoliceSurrenderEligibility Eligibility { get; init; } = PoliceSurrenderEligibility.NotFugitive;
    public int FeeAmount { get; init; }
    public string FailureReason { get; init; } = string.Empty;
    public NpcShip? AcceptingOfficer { get; init; }

    public static PoliceSurrenderAssessment Eligible(int feeAmount, NpcShip? acceptingOfficer) => new()
    {
        IsEligible = true,
        Eligibility = PoliceSurrenderEligibility.Eligible,
        FeeAmount = feeAmount,
        AcceptingOfficer = acceptingOfficer
    };

    public static PoliceSurrenderAssessment Ineligible(
        PoliceSurrenderEligibility eligibility,
        string failureReason) => new()
    {
        IsEligible = false,
        Eligibility = eligibility,
        FailureReason = failureReason
    };
}

/// <summary>
/// Result of one committed surrender transaction. The fee is a direct,
/// bounded consequence of the canonical current fugitive Heat; it is never
/// persisted, never becomes debt, and never creates a criminal record.
/// </summary>
public sealed class PoliceSurrenderResult
{
    public bool Success { get; init; }
    public int FeeAmount { get; init; }
    public int CreditsCharged { get; init; }
    public string Message { get; init; } = string.Empty;
}

/// <summary>
/// Phase 78 lawful fugitive surrender transaction owner. This is the one
/// small seam that lets a player deliberately stop running from Liberty
/// Police while the canonical <see cref="PoliceFugitiveManager"/> says the
/// player is actively fugitive.
///
/// Design rules honored here:
///   - The fugitive authority remains authoritative before, during, and
///     after surrender. This service only requests resolution; it never
///     mutates fugitive fields directly.
///   - The fee is derived solely from the canonical current fugitive Heat
///     (Heat 1 / Heat 2). No Heat 3, no fine ledger, no debt, no history.
///   - No cargo is confiscated and no cargo is fabricated. Surrender is not
///     a scanner; ordinary contraband rules apply unchanged before and after.
///   - Durable Police reputation is never improved and never reset. The
///     refusal/pursuit already imposed its durable penalty; surrender adds
///     no reputation change.
    ///   - The transaction is atomic: eligibility and affordability are
    ///     revalidated, then the fee is charged, then the pursuit is resolved.
    ///     Any failure before commit leaves the player fugitive with no partial
    ///     mutation.
    ///   - After commit, incompatible player flight automation (GOTO/dock
    ///     assist, cruise) is cancelled through the existing flight-control
    ///     authorities. This adds no detention, movement, or criminal-state
    ///     authority and never rewrites ship physics.
    ///   - Only Liberty Police (canonical faction id) can accept a surrender.
/// </summary>
public static class PoliceFugitiveSurrenderService
{
    // Bounded, deterministic surrender fees derived from the canonical
    // current fugitive Heat. Heat 1 carries the lower fee, HotPursuit the
    // higher fee. These are consequences of the live incident, not a second
    // enforcement authority, and they are never persisted.
    public const int Heat1SurrenderFee = 1_000;
    public const int Heat2SurrenderFee = 2_500;

    public static int GetSurrenderFee(PoliceHeatLevel heat) =>
        heat == PoliceHeatLevel.HotPursuit ? Heat2SurrenderFee : Heat1SurrenderFee;

    /// <summary>
    /// Evaluates surrender eligibility without mutating any state. The
    /// order of checks is deliberate: fugitive state, player survival,
    /// permanent hostility boundary, Police presence, then affordability.
    /// </summary>
    public static PoliceSurrenderAssessment Assess(
        PoliceFugitiveManager fugitive,
        ReputationManager reputation,
        Ship? player,
        IReadOnlyList<NpcShip>? npcs,
        PlayerCredits? credits)
    {
        if (fugitive == null || !fugitive.IsActive)
            return PoliceSurrenderAssessment.Ineligible(
                PoliceSurrenderEligibility.NotFugitive,
                "Surrender is only available during an active Liberty Police pursuit.");

        if (player == null || player.Hull?.IsDestroyed == true)
            return PoliceSurrenderAssessment.Ineligible(
                PoliceSurrenderEligibility.PlayerDestroyed,
                "Surrender is unavailable.");

        // Hostility boundary: an independently hostile Liberty Police
        // relationship (permanent standing at or below the existing -0.60
        // hostile threshold) is never silently reset by surrender. Temporary
        // hostility does not block eligibility: the fugitive incident itself
        // creates it, and any player-caused transient hostility is preserved
        // (never cleared) by the resolution authority.
        if (reputation == null || reputation.IsHostile(FactionManager.LibertyPolice))
            return PoliceSurrenderAssessment.Ineligible(
                PoliceSurrenderEligibility.PermanentlyHostile,
                "Unable to surrender: Liberty Police are hostile.");

        NpcShip? acceptingOfficer = fugitive.FindSurrenderPresence(npcs, player);
        if (acceptingOfficer == null)
            return PoliceSurrenderAssessment.Ineligible(
                PoliceSurrenderEligibility.NoPolicePresence,
                "No Liberty Police unit is available to accept surrender.");

        int fee = GetSurrenderFee(fugitive.Heat);
        if (credits == null || !credits.CanAfford(fee))
            return PoliceSurrenderAssessment.Ineligible(
                PoliceSurrenderEligibility.InsufficientCredits,
                "Unable to surrender: insufficient credits for the assessed penalty.");

        return PoliceSurrenderAssessment.Eligible(fee, acceptingOfficer);
    }

    /// <summary>
    /// Attempts one atomic surrender transaction. On success the canonical
    /// fugitive state is resolved through
    /// <see cref="PoliceFugitiveManager.ResolveSurrender"/> and exactly one
    /// bounded success message is produced. On any failure the fugitive
    /// state, credits, cargo, and reputation are left completely unchanged.
    /// </summary>
    public static bool TrySurrender(
        PoliceFugitiveManager fugitive,
        ReputationManager reputation,
        Ship? player,
        IReadOnlyList<NpcShip>? npcs,
        PlayerCredits? credits,
        Action<string>? log,
        out PoliceSurrenderResult? result,
        out string failureReason,
        PoliceScanSystem? scan = null)
    {
        result = null;
        failureReason = string.Empty;

        PoliceSurrenderAssessment assessment = Assess(fugitive, reputation, player, npcs, credits);
        if (!assessment.IsEligible)
        {
            failureReason = assessment.FailureReason;
            log?.Invoke($"[POLICE SURRENDER] Declined: {assessment.FailureReason}");
            return false;
        }

        // Affordability is revalidated through the atomic credit removal
        // below; a failure here leaves the player fugitive with no partial
        // mutation and no fee charged.
        if (credits == null || !credits.RemoveCredits(assessment.FeeAmount))
        {
            failureReason = "Unable to surrender: insufficient credits for the assessed penalty.";
            log?.Invoke($"[POLICE SURRENDER] Declined: {failureReason}");
            return false;
        }

        string message = assessment.FeeAmount > 0
            ? $"Surrender accepted. {assessment.FeeAmount:N0} credits assessed. Liberty Police pursuit ended."
            : "Surrender accepted. Liberty Police pursuit ended.";

        fugitive.ResolveSurrender(log, message, assessment.AcceptingOfficer);

        // Phase 79: the enforcement result that the surrendered pursuit
        // produced is now resolved. Dropping the presentational result hold
        // prevents the contraband stop coordinator from re-opening the
        // incident later in this same tick. This does not scan, confiscate,
        // change cargo, alter a fine, or add grace.
        scan?.ClearResolvedResultHold();

        // Phase 80: the surrender transaction has committed, so any player
        // flight automation that only made sense as escape/departure movement
        // is now incompatible with the resolved incident. Cancel it through
        // the existing flight-control authorities: GOTO (which also owns
        // dock assist) and cruise. This never teleports the ship, rewrites
        // physics, zeroes velocity, invents a destination, or takes manual
        // control away; existing momentum simply stays governed by normal
        // ship physics. Failed surrenders never reach this point.
        NormalizePlayerFlightAutomation(player);

        result = new PoliceSurrenderResult
        {
            Success = true,
            FeeAmount = assessment.FeeAmount,
            CreditsCharged = assessment.FeeAmount,
            Message = message
        };
        log?.Invoke($"[POLICE SURRENDER] Accepted ({assessment.FeeAmount:N0} CR).");
        return true;
    }

    /// <summary>
    /// Phase 80 flight-state normalization, executed only after the surrender
    /// transaction has committed. Cancels the two player flight authorities
    /// that can own escape/departure movement and coexist with an active
    /// fugitive incident:
    ///   - GOTO autopilot (including its dock-assist mode) via
    ///     <see cref="Ship.CancelGoto"/>, which also releases the
    ///     automation-owned autopilot speed override and clears the route.
    ///   - cruise via <see cref="Ship.CancelCruise"/> with the existing
    ///     IncompatibleFlight reason.
    /// Both calls are no-ops when the mode is not active, so repeated input is
    /// naturally idempotent. No new notification is produced: the goto
    /// cancellation is silent and cruise cancellation has no presentation of
    /// its own, so the single surrender-success message remains the only
    /// notification. Trade-lane and jump-hole transit are intentionally not
    /// touched here: the production update loop returns before the surrender
    /// input is processed while either transit owns the ship, so the
    /// transaction is unreachable in that state.
    /// </summary>
    private static void NormalizePlayerFlightAutomation(Ship? player)
    {
        if (player == null)
            return;

        player.CancelGoto(showNotification: false);
        player.CancelCruise(CruiseCancellationReason.IncompatibleFlight);
    }
}
