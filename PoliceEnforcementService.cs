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

    internal bool IsResolved { get; set; }

    internal string CargoFingerprint => string.Join(
        "|",
        Contraband.Select(finding => $"{finding.CargoKey}:{finding.Quantity}"));

    internal bool MatchesSnapshot(PoliceEnforcementOffer other)
    {
        if (other == null ||
            IsInspectionApplicable != other.IsInspectionApplicable ||
            !string.Equals(PolicingFactionId, other.PolicingFactionId, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        // Jettisoning after detection is too late to invalidate the citation.
        // The live hold may therefore contain fewer (or zero) units, but it
        // may not contain a new illegal stack that was absent from the quote.
        Dictionary<string, int> detected = Contraband.ToDictionary(
            finding => finding.CargoKey,
            finding => finding.Quantity,
            StringComparer.OrdinalIgnoreCase);
        foreach (ContrabandFinding finding in other.Contraband)
        {
            if (!detected.TryGetValue(finding.CargoKey, out int detectedQuantity) ||
                finding.Quantity > detectedQuantity)
            {
                return false;
            }
        }

        return true;
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
    public IReadOnlyList<MissionCargoConfiscation> MissionConfiscations { get; init; } = Array.Empty<MissionCargoConfiscation>();
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
    // Phase 53 economy policy: 500 CR base plus 25% of canonical base value,
    // rounded up and clamped to [500, 10,000].
    public const int BaseFineCredits = 500;
    public const int MinimumFineCredits = 500;
    public const decimal ContrabandValueFineRate = 0.25m;
    public const int MaximumFineCredits = 10_000;
    public const float PaidComplianceReputationPenalty = -0.03f;
    // Insufficient funds no longer confiscate or penalize; the player must
    // refuse, flee, or time out. Refusal retains the established bounded
    // enforcement standing consequence so Phase 28 combat provenance remains
    // compatible.
    public const float UnableToPayReputationPenalty = 0f;
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
                ? canAfford
                    ? new[] { PoliceEnforcementResolution.Comply, PoliceEnforcementResolution.Refuse }
                    : new[] { PoliceEnforcementResolution.Refuse }
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

        return rawFine < MinimumFineCredits
            ? MinimumFineCredits
            : rawFine >= MaximumFineCredits
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

        if (offered == null || cargoHold == null || credits == null || reputationManager == null || offered.IsResolved)
        {
            failureReason = "enforcement offer is no longer valid";
            return false;
        }

        PoliceEnforcementOffer current = Evaluate(offered.PolicingFactionId, cargoHold, credits);
        if (!current.IsInspectionApplicable || !offered.IsInspectionApplicable || !offered.HasContraband ||
            !offered.MatchesSnapshot(current))
        {
            failureReason = "enforcement offer is no longer valid";
            return false;
        }

        if (resolution == PoliceEnforcementResolution.Refuse)
        {
            ReputationChangeResult? refusalChange = ApplyHostilityAndPenalty(
                current.PolicingFactionId,
                RefusalReputationPenalty,
                ReputationChangeReason.PoliceEnforcementRefused,
                reputationManager);
            offered.IsResolved = true;
            result = new PoliceEnforcementResult
            {
                Outcome = PoliceEnforcementOutcome.Refused,
                PolicingFactionId = current.PolicingFactionId,
                FineAmount = offered.FineAmount,
                ReputationChange = refusalChange,
                TemporaryHostilityStarted = reputationManager.IsTemporarilyHostile(current.PolicingFactionId),
                Message = $"{current.PolicingFactionDisplayName} are temporarily hostile"
            };
            return true;
        }

        if (!credits.CanAfford(offered.FineAmount))
        {
            failureReason = $"Insufficient credits to comply: need {offered.FineAmount:N0} CR";
            return false;
        }

        Dictionary<string, int> confiscation = current.Contraband.ToDictionary(
            finding => finding.CargoKey,
            finding => finding.Quantity,
            StringComparer.OrdinalIgnoreCase);

        IReadOnlyList<MissionCargoConfiscation> missionConfiscations = Array.Empty<MissionCargoConfiscation>();
        if (confiscation.Count > 0 && !cargoHold.TryConfiscateContraband(confiscation, out missionConfiscations))
        {
            failureReason = "contraband could not be confiscated";
            return false;
        }

        if (!credits.RemoveCredits(offered.FineAmount))
        {
            failureReason = "insufficient credits";
            return false;
        }

        ReputationChangeResult? reputationChange = reputationManager.AdjustReputationDirect(
            current.PolicingFactionId,
            PaidComplianceReputationPenalty,
            ReputationChangeReason.PoliceEnforcementPaid);
        offered.IsResolved = true;
        result = BuildConfiscationResult(
            PoliceEnforcementOutcome.PaidAndConfiscated,
            offered,
            offered.FineAmount,
            reputationChange,
            temporaryHostilityStarted: false,
            $"Surrendered contraband and paid fine: {offered.FineAmount:N0} credits",
            missionConfiscations,
            current.Contraband);
        return true;
    }

    private PoliceEnforcementResult BuildConfiscationResult(
        PoliceEnforcementOutcome outcome,
        PoliceEnforcementOffer offer,
        int creditsCharged,
        ReputationChangeResult? reputationChange,
        bool temporaryHostilityStarted,
        string message,
        IReadOnlyList<MissionCargoConfiscation>? missionConfiscations = null,
        IReadOnlyList<ContrabandFinding>? confiscatedContraband = null) =>
        new()
        {
            Outcome = outcome,
            PolicingFactionId = offer.PolicingFactionId,
            FineAmount = offer.FineAmount,
            CreditsCharged = creditsCharged,
            ConfiscatedQuantity = confiscatedContraband?.Sum(finding => finding.Quantity) ?? 0,
            ConfiscatedContraband = confiscatedContraband ?? Array.Empty<ContrabandFinding>(),
            MissionConfiscations = missionConfiscations ?? Array.Empty<MissionCargoConfiscation>(),
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
        return Math.Abs(reputationPenalty) < ReputationManager.Precision
            ? null
            : reputationManager.AdjustReputationDirect(factionId, reputationPenalty, reason);
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
