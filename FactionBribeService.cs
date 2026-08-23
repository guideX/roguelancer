#nullable enable

using System;
using System.Linq;

namespace Roguelancer;

/// <summary>
/// A deterministic, read-only quote generated for one bar contact. The quote
/// contains no mutable gameplay state; the service revalidates it at purchase
/// time against the authoritative reputation and credit managers.
/// </summary>
public sealed record FactionBribeOffer
{
    public string ContactName { get; init; } = string.Empty;
    public string ContactFactionId { get; init; } = FactionManager.NeutralCivilians;
    public string StationFactionId { get; init; } = FactionManager.NeutralCivilians;
    public string TargetFactionId { get; init; } = FactionManager.NeutralCivilians;
    public string TargetFactionDisplayName { get; init; } = string.Empty;
    public float CurrentReputation { get; init; }
    public float ResultingReputation { get; init; }
    public int CreditCost { get; init; }
    public bool CanAfford { get; init; }
    public bool IsValid { get; init; }
    public string UnavailableReason { get; init; } = string.Empty;

    public string BuildDialogueLine()
    {
        if (!IsValid)
        {
            return string.IsNullOrWhiteSpace(UnavailableReason)
                ? $"I cannot move your standing with {TargetFactionDisplayName} right now."
                : $"I cannot help with {TargetFactionDisplayName}: {UnavailableReason}.";
        }

        string affordability = CanAfford
            ? "Want me to make the call?"
            : "Come back when you have the credits.";
        return $"I know people in {TargetFactionDisplayName}. For {CreditCost:N0} credits, I can move your standing from {ReputationManager.FormatStanding(CurrentReputation)} to {ReputationManager.FormatStanding(ResultingReputation)}. {affordability}";
    }
}

/// <summary>
/// Owns the rules for social reputation recovery while delegating all actual
/// standing and credit mutations to ReputationManager and PlayerCredits.
/// </summary>
public static class FactionBribeService
{
    // Money can repair relations into the upper Neutral band, but cannot buy
    // Friendly (+0.20) or Allied (+0.60) standing.
    public const float BriberyCeiling = 0.10f;
    public const float DeepHostilityThreshold = -0.85f;
    public const int BaseCost = 2_500;
    public const float RecoveryCostPerPoint = 6_000f;
    public const float NegativeStandingCostWeight = 1.50f;

    /// <summary>
    /// Returns the one deterministic quote this contact can currently make.
    /// The most damaged contextually relevant faction wins; ties use the
    /// contact's configured order. An otherwise relevant but unavailable quote
    /// is retained so the bar can explain why assistance is refused.
    /// </summary>
    public static FactionBribeOffer? GetOfferForContact(
        BarNpc? contact,
        string? stationFactionId,
        ReputationManager? reputationManager,
        PlayerCredits? credits)
    {
        if (contact == null || reputationManager == null || contact.ReputationBribeTargetFactionIds.Count == 0)
            return null;

        string normalizedStationFactionId = FactionManager.NormalizeFactionId(stationFactionId);
        string? targetFactionId = contact.ReputationBribeTargetFactionIds
            .Select(FactionManager.NormalizeFactionId)
            .Where(target => IsTargetContextuallyAvailable(
                contact.FactionId,
                target,
                normalizedStationFactionId,
                reputationManager.FactionManager))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(target => target.Equals(normalizedStationFactionId, StringComparison.OrdinalIgnoreCase) ? 0 : 1)
            .ThenBy(target => reputationManager.GetStanding(target))
            .ThenBy(target => target, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();

        return targetFactionId == null
            ? null
            : CreateOfferForTarget(
                contact.Name,
                contact.FactionId,
                normalizedStationFactionId,
                targetFactionId,
                reputationManager,
                credits);
    }

    public static FactionBribeOffer? CreateOfferForTarget(
        string? contactName,
        string? contactFactionId,
        string? stationFactionId,
        string? targetFactionId,
        ReputationManager? reputationManager,
        PlayerCredits? credits)
    {
        if (reputationManager == null || string.IsNullOrWhiteSpace(targetFactionId))
            return null;

        string normalizedContactFactionId = FactionManager.NormalizeFactionId(contactFactionId);
        string normalizedStationFactionId = FactionManager.NormalizeFactionId(stationFactionId);
        string normalizedTargetFactionId = FactionManager.NormalizeFactionId(targetFactionId);
        if (!IsTargetContextuallyAvailable(
            normalizedContactFactionId,
            normalizedTargetFactionId,
            normalizedStationFactionId,
            reputationManager.FactionManager))
        {
            return null;
        }

        float currentReputation = reputationManager.GetStanding(normalizedTargetFactionId);
        bool belowCeiling = currentReputation < BriberyCeiling - ReputationManager.Precision;
        bool tooHostile = currentReputation <= DeepHostilityThreshold + ReputationManager.Precision;
        float resultingReputation = belowCeiling
            ? BriberyCeiling
            : currentReputation;
        int creditCost = belowCeiling
            ? CalculateCost(normalizedTargetFactionId, normalizedContactFactionId, currentReputation, reputationManager.FactionManager)
            : 0;

        string unavailableReason = tooHostile
            ? $"the relationship is beyond a routine fix ({ReputationManager.FormatStanding(DeepHostilityThreshold)} limit)"
            : !belowCeiling
                ? $"standing is already at the bribe ceiling ({ReputationManager.FormatStanding(BriberyCeiling)})"
                : string.Empty;

        return new FactionBribeOffer
        {
            ContactName = string.IsNullOrWhiteSpace(contactName) ? "Faction contact" : contactName.Trim(),
            ContactFactionId = normalizedContactFactionId,
            StationFactionId = normalizedStationFactionId,
            TargetFactionId = normalizedTargetFactionId,
            TargetFactionDisplayName = reputationManager.FactionManager.GetFaction(normalizedTargetFactionId).DisplayName,
            CurrentReputation = currentReputation,
            ResultingReputation = resultingReputation,
            CreditCost = creditCost,
            CanAfford = creditCost > 0 && credits?.CanAfford(creditCost) == true,
            IsValid = belowCeiling && !tooHostile && creditCost > 0,
            UnavailableReason = unavailableReason
        };
    }

    /// <summary>
    /// Buys one quote after reconstructing it from its immutable identity.
    /// This makes repeated input, stale UI, and save/reopen attempts harmless.
    /// </summary>
    public static bool TryPurchase(
        FactionBribeOffer? offer,
        ReputationManager? reputationManager,
        PlayerCredits? credits,
        out ReputationChangeResult? reputationChange,
        out string failureReason)
    {
        reputationChange = null;
        failureReason = string.Empty;

        if (offer == null || reputationManager == null || credits == null)
        {
            failureReason = "offer is no longer valid";
            return false;
        }

        FactionBribeOffer? currentOffer = CreateOfferForTarget(
            offer.ContactName,
            offer.ContactFactionId,
            offer.StationFactionId,
            offer.TargetFactionId,
            reputationManager,
            credits);
        if (currentOffer == null)
        {
            failureReason = "offer is not available from this contact here";
            return false;
        }

        if (!currentOffer.IsValid)
        {
            failureReason = currentOffer.UnavailableReason;
            return false;
        }

        if (Math.Abs(currentOffer.CurrentReputation - offer.CurrentReputation) >= ReputationManager.Precision ||
            currentOffer.CreditCost != offer.CreditCost)
        {
            failureReason = "offer is no longer valid";
            return false;
        }

        if (!credits.CanAfford(currentOffer.CreditCost))
        {
            failureReason = $"insufficient credits (need {currentOffer.CreditCost:N0})";
            return false;
        }

        if (!credits.RemoveCredits(currentOffer.CreditCost))
        {
            failureReason = "insufficient credits";
            return false;
        }

        float delta = currentOffer.ResultingReputation - currentOffer.CurrentReputation;
        ReputationChangeResult? change = reputationManager.AdjustReputation(
            currentOffer.TargetFactionId,
            delta,
            ReputationChangeReason.ReputationBribe);
        if (change == null)
        {
            // The economy authority is still authoritative; refund the only
            // deduction if the reputation authority rejected the transaction.
            credits.AddCredits(currentOffer.CreditCost);
            failureReason = "offer is no longer valid";
            return false;
        }

        reputationChange = change;
        return true;
    }

    public static int CalculateCost(
        string? targetFactionId,
        string? contactFactionId,
        float currentReputation,
        FactionManager? factionManager = null)
    {
        float normalizedCurrent = Math.Clamp(
            float.IsNaN(currentReputation) || float.IsInfinity(currentReputation) ? 0f : currentReputation,
            ReputationManager.MinimumStanding,
            ReputationManager.MaximumStanding);
        float recovery = BriberyCeiling - normalizedCurrent;
        if (recovery <= 0f)
            return 0;

        FactionManager factions = factionManager ?? new FactionManager();
        Faction targetFaction = factions.GetFaction(targetFactionId);
        float factionMultiplier = targetFaction.IsLawful
            ? 1.25f
            : targetFaction.IsCriminal
                ? 0.95f
                : 1.05f;
        float contactMultiplier = string.Equals(
            FactionManager.NormalizeFactionId(targetFactionId),
            FactionManager.NormalizeFactionId(contactFactionId),
            StringComparison.OrdinalIgnoreCase)
            ? 0.95f
            : 1.05f;
        float severityMultiplier = 1f + MathF.Max(0f, -normalizedCurrent) * NegativeStandingCostWeight;
        double rawCost = BaseCost + recovery * RecoveryCostPerPoint * severityMultiplier * factionMultiplier * contactMultiplier;
        return Math.Max(BaseCost, (int)Math.Ceiling(rawCost));
    }

    public static bool IsTargetContextuallyAvailable(
        string? contactFactionId,
        string? targetFactionId,
        string? stationFactionId,
        FactionManager? factionManager = null)
    {
        string contact = FactionManager.NormalizeFactionId(contactFactionId);
        string target = FactionManager.NormalizeFactionId(targetFactionId);
        string station = FactionManager.NormalizeFactionId(stationFactionId);
        if (target == FactionManager.NeutralCivilians)
            return false;

        if (target.Equals(station, StringComparison.OrdinalIgnoreCase))
            return true;

        FactionManager factions = factionManager ?? new FactionManager();
        Faction contactFaction = factions.GetFaction(contact);
        Faction targetFaction = factions.GetFaction(target);
        Faction stationFaction = factions.GetFaction(station);

        // A lawful station can host lawful government, corporate, and
        // enforcement contacts; a criminal station can host criminal contacts.
        if (stationFaction.IsLawful && targetFaction.IsLawful &&
            (contactFaction.IsLawful || contact.Equals(FactionManager.NeutralCivilians, StringComparison.OrdinalIgnoreCase)))
            return true;

        if (stationFaction.IsCriminal && targetFaction.IsCriminal &&
            (contactFaction.IsCriminal || contact.Equals(FactionManager.NeutralCivilians, StringComparison.OrdinalIgnoreCase)))
            return true;

        // Unaligned stations still support an affiliated specialist, but do
        // not become a universal market for every faction in the sector.
        return station.Equals(FactionManager.NeutralCivilians, StringComparison.OrdinalIgnoreCase) &&
            contact.Equals(target, StringComparison.OrdinalIgnoreCase);
    }
}
