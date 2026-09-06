#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;

namespace Roguelancer;

public enum PoliceEnforcementResolution
{
    Comply,
    Refuse
}

public enum PoliceEnforcementOutcome
{
    PaidAndConfiscated,
    ConfiscatedUnpaid,
    Refused
}

/// <summary>
/// One deterministic, metadata-backed illegal cargo finding.
/// </summary>
public sealed class ContrabandFinding
{
    public string CargoKey { get; init; } = string.Empty;
    public string CommodityId { get; init; } = string.Empty;
    public string CommodityName { get; init; } = string.Empty;
    public int Quantity { get; init; }
    public int UnitValue { get; init; }
    public long TotalValue { get; init; }
}

/// <summary>
/// Read-only enforcement quote. It contains no mutation authority; the
/// service re-evaluates it before applying a resolution.
/// </summary>
public sealed class PoliceEnforcementOffer
{
    public bool IsInspectionApplicable { get; init; }
    public bool HasContraband { get; init; }
    public string PolicingFactionId { get; init; } = FactionManager.NeutralCivilians;
    public string PolicingFactionDisplayName { get; init; } = string.Empty;
    public IReadOnlyList<ContrabandFinding> Contraband { get; init; } = Array.Empty<ContrabandFinding>();
    public int TotalContrabandQuantity { get; init; }
    public long TotalContrabandValue { get; init; }
    public int FineAmount { get; init; }
    public bool CanAffordFine { get; init; }
    public IReadOnlyList<PoliceEnforcementResolution> AvailableResolutions { get; init; } = Array.Empty<PoliceEnforcementResolution>();
    public string Summary { get; init; } = string.Empty;

    internal string CargoFingerprint => string.Join(
        "|",
        Contraband.Select(finding => $"{finding.CargoKey}:{finding.Quantity}"));

    internal bool Matches(PoliceEnforcementOffer other)
    {
        return other != null &&
            IsInspectionApplicable == other.IsInspectionApplicable &&
            string.Equals(PolicingFactionId, other.PolicingFactionId, StringComparison.OrdinalIgnoreCase) &&
            FineAmount == other.FineAmount &&
            string.Equals(CargoFingerprint, other.CargoFingerprint, StringComparison.Ordinal);
    }
}

public sealed class PoliceEnforcementResult
{
    public PoliceEnforcementOutcome Outcome { get; init; }
    public string PolicingFactionId { get; init; } = FactionManager.NeutralCivilians;
    public int FineAmount { get; init; }
    public int CreditsCharged { get; init; }
    public int ConfiscatedQuantity { get; init; }
    public IReadOnlyList<ContrabandFinding> ConfiscatedContraband { get; init; } = Array.Empty<ContrabandFinding>();
    public ReputationChangeResult? ReputationChange { get; init; }
    public bool TemporaryHostilityStarted { get; init; }
    public string Message { get; init; } = string.Empty;
}

/// <summary>
/// Authoritative first-pass law-enforcement rules. Liberty Police is the
/// only policing faction in this phase; criminal factions do not inherit the
/// scanner role merely because they share the NPC pipeline.
/// </summary>
public sealed class PoliceEnforcementService
{
    public const string InitialPolicingFactionId = FactionManager.LibertyPolice;
    public const int BaseFineCredits = 250;
    public const decimal ContrabandValueFineRate = 0.25m;
    public const int MaximumFineCredits = 25_000;
    public const float PaidComplianceReputationPenalty = -0.02f;
    public const float UnableToPayReputationPenalty = -0.30f;
    public const float RefusalReputationPenalty = -0.20f;
    public const float TemporaryHostilityDurationSeconds = TemporaryHostilityManager.DefaultDurationSeconds;

    public bool IsPolicingFaction(string? factionId) =>
        string.Equals(
            FactionManager.NormalizeFactionId(factionId),
            InitialPolicingFactionId,
            StringComparison.OrdinalIgnoreCase);

    public PoliceEnforcementOffer Evaluate(
        string? policingFactionId,
        CargoHold? cargoHold,
        PlayerCredits? credits)
    {
        string normalizedFactionId = FactionManager.NormalizeFactionId(policingFactionId);
        bool applicable = IsPolicingFaction(normalizedFactionId);
        List<ContrabandFinding> findings = applicable
            ? FindContraband(cargoHold)
            : new List<ContrabandFinding>();
        long totalValue = findings.Aggregate(0L, (sum, finding) => SaturatingAdd(sum, finding.TotalValue));
        bool hasContraband = findings.Count > 0;
        int fine = hasContraband ? CalculateFine(totalValue) : 0;
        bool canAfford = hasContraband && credits?.CanAfford(fine) == true;

        return new PoliceEnforcementOffer
        {
            IsInspectionApplicable = applicable,
            HasContraband = hasContraband,
            PolicingFactionId = normalizedFactionId,
            PolicingFactionDisplayName = FactionManager.GetFactionDisplayName(normalizedFactionId),
            Contraband = findings,
            TotalContrabandQuantity = findings.Sum(finding => finding.Quantity),
            TotalContrabandValue = totalValue,
            FineAmount = fine,
            CanAffordFine = canAfford,
            AvailableResolutions = hasContraband
                ? new[] { PoliceEnforcementResolution.Comply, PoliceEnforcementResolution.Refuse }
                : Array.Empty<PoliceEnforcementResolution>(),
            Summary = !applicable
                ? "Inspection not applicable."
                : !hasContraband
                    ? $"{FactionManager.GetFactionDisplayName(normalizedFactionId)} inspection: cargo clean"
                    : $"{FactionManager.GetFactionDisplayName(normalizedFactionId)} inspection: contraband detected"
        };
    }

    public int CalculateFine(long totalContrabandValue)
    {
        long boundedValue = Math.Clamp(totalContrabandValue, 0L, long.MaxValue);
        decimal rawFine = BaseFineCredits + Math.Ceiling(boundedValue * ContrabandValueFineRate);
        if (rawFine <= 0m)
        {
            return 0;
        }

        return rawFine >= MaximumFineCredits
            ? MaximumFineCredits
            : (int)rawFine;
    }

    public bool TryResolve(
        PoliceEnforcementOffer? offered,
        PoliceEnforcementResolution resolution,
        CargoHold? cargoHold,
        PlayerCredits? credits,
        ReputationManager? reputationManager,
        out PoliceEnforcementResult? result,
        out string failureReason)
    {
        result = null;
        failureReason = string.Empty;

        if (offered == null || cargoHold == null || credits == null || reputationManager == null)
        {
            failureReason = "enforcement offer is no longer valid";
            return false;
        }

        PoliceEnforcementOffer current = Evaluate(offered.PolicingFactionId, cargoHold, credits);
        if (!current.IsInspectionApplicable || !current.HasContraband || !offered.Matches(current))
        {
            failureReason = "enforcement offer is no longer valid";
            return false;
        }

        if (resolution == PoliceEnforcementResolution.Refuse)
        {
            ReputationChangeResult? reputationChange = ApplyHostilityAndPenalty(
                current.PolicingFactionId,
                RefusalReputationPenalty,
                ReputationChangeReason.PoliceEnforcementRefused,
                reputationManager);
            result = new PoliceEnforcementResult
            {
                Outcome = PoliceEnforcementOutcome.Refused,
                PolicingFactionId = current.PolicingFactionId,
                FineAmount = current.FineAmount,
                ReputationChange = reputationChange,
                TemporaryHostilityStarted = reputationManager.IsTemporarilyHostile(current.PolicingFactionId),
                Message = $"{current.PolicingFactionDisplayName} are temporarily hostile"
            };
            return true;
        }

        Dictionary<string, int> confiscation = current.Contraband.ToDictionary(
            finding => finding.CargoKey,
            finding => finding.Quantity,
            StringComparer.OrdinalIgnoreCase);

        if (!cargoHold.TryRemoveCommodityBatch(confiscation))
        {
            failureReason = "contraband could not be confiscated";
            return false;
        }

        if (current.CanAffordFine)
        {
            if (!credits.RemoveCredits(current.FineAmount))
            {
                RollBackConfiscation(cargoHold, current.Contraband);
                failureReason = "insufficient credits";
                return false;
            }

            ReputationChangeResult? reputationChange = reputationManager.AdjustReputationDirect(
                current.PolicingFactionId,
                PaidComplianceReputationPenalty,
                ReputationChangeReason.PoliceEnforcementPaid);
            result = BuildConfiscationResult(
                PoliceEnforcementOutcome.PaidAndConfiscated,
                current,
                current.FineAmount,
                reputationChange,
                temporaryHostilityStarted: false,
                $"Surrendered contraband and paid fine: {current.FineAmount:N0} credits");
            return true;
        }

        ReputationChangeResult? unpaidReputationChange = ApplyHostilityAndPenalty(
            current.PolicingFactionId,
            UnableToPayReputationPenalty,
            ReputationChangeReason.PoliceEnforcementUnableToPay,
            reputationManager);
        result = BuildConfiscationResult(
            PoliceEnforcementOutcome.ConfiscatedUnpaid,
            current,
            0,
            unpaidReputationChange,
            temporaryHostilityStarted: reputationManager.IsTemporarilyHostile(current.PolicingFactionId),
            "Cannot afford fine — contraband confiscated; Liberty Police are temporarily hostile");
        return true;
    }

    private PoliceEnforcementResult BuildConfiscationResult(
        PoliceEnforcementOutcome outcome,
        PoliceEnforcementOffer offer,
        int creditsCharged,
        ReputationChangeResult? reputationChange,
        bool temporaryHostilityStarted,
        string message) =>
        new()
        {
            Outcome = outcome,
            PolicingFactionId = offer.PolicingFactionId,
            FineAmount = offer.FineAmount,
            CreditsCharged = creditsCharged,
            ConfiscatedQuantity = offer.TotalContrabandQuantity,
            ConfiscatedContraband = offer.Contraband,
            ReputationChange = reputationChange,
            TemporaryHostilityStarted = temporaryHostilityStarted,
            Message = message
        };

    private ReputationChangeResult? ApplyHostilityAndPenalty(
        string factionId,
        float reputationPenalty,
        ReputationChangeReason reason,
        ReputationManager reputationManager)
    {
        reputationManager.TemporaryHostility.RecordHostileAction(
            factionId,
            "police enforcement refusal",
            TemporaryHostilityDurationSeconds);
        return reputationManager.AdjustReputationDirect(factionId, reputationPenalty, reason);
    }

    private static List<ContrabandFinding> FindContraband(CargoHold? cargoHold)
    {
        if (cargoHold == null)
        {
            return new List<ContrabandFinding>();
        }

        return cargoHold.GetAllCommodities()
            .Select(entry =>
            {
                Commodity? commodity = CommodityCatalog.GetByName(entry.Key) ?? CommodityCatalog.GetById(entry.Key);
                // A police inspection sees the physical hold, including units
                // reserved for an active contract. Market sale protection is a
                // separate CargoHold concern; it must not hide contraband from
                // an authoritative scan.
                int quantity = commodity == null
                    ? 0
                    : Math.Max(0, entry.Value);
                return commodity?.IsContraband == true && quantity > 0
                    ? new ContrabandFinding
                    {
                        CargoKey = entry.Key,
                        CommodityId = commodity.Id ?? string.Empty,
                        CommodityName = commodity.Name ?? string.Empty,
                        Quantity = quantity,
                        UnitValue = Math.Max(0, commodity.BasePrice),
                        TotalValue = SaturatingProduct(Math.Max(0, commodity.BasePrice), quantity)
                    }
                    : null;
            })
            .Where(finding => finding != null)
            .Cast<ContrabandFinding>()
            .OrderBy(finding => finding.CommodityId, StringComparer.OrdinalIgnoreCase)
            .ThenBy(finding => finding.CargoKey, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static void RollBackConfiscation(CargoHold cargoHold, IReadOnlyList<ContrabandFinding> findings)
    {
        foreach (ContrabandFinding finding in findings)
        {
            Commodity? commodity = CommodityCatalog.GetById(finding.CommodityId) ?? CommodityCatalog.GetByName(finding.CommodityName);
            if (commodity != null)
            {
                cargoHold.AddCommodity(commodity, finding.Quantity);
            }
        }
    }

    private static long SaturatingProduct(int unitValue, int quantity)
    {
        decimal product = (decimal)Math.Max(0, unitValue) * Math.Max(0, quantity);
        return product >= long.MaxValue ? long.MaxValue : (long)product;
    }

    private static long SaturatingAdd(long left, long right)
    {
        if (right <= 0)
        {
            return Math.Max(0L, left);
        }

        return left > long.MaxValue - right ? long.MaxValue : left + right;
    }
}
