#nullable enable

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Roguelancer;

public enum FactionBountyRejectionReason
{
    Eligible,
    InvalidVictim,
    DuplicateKill,
    VictimNotDestroyed,
    VictimWasNotAlive,
    NotPlayerAttributed,
    NoBountyPolicy,
    SponsorHostileToPlayer,
    InvalidFaction,
    NoRewardableShipDefinition,
    CreditMutationFailed
}

public enum FactionBountyTier
{
    None,
    Standard,
    Heavy
}

/// <summary>
/// Explicit sponsor/target policy. Phase 37 intentionally has one small
/// policy instead of treating every hostile faction relationship as payable.
/// </summary>
public readonly record struct FactionBountyPolicy(
    string SponsorFactionId,
    string TargetFactionId,
    int StandardPayout,
    int HeavyPayout);

/// <summary>
/// Immutable diagnostic and reward result for one authoritative destruction
/// callback. Credit mutation is performed only by the service before a
/// successful result is presented.
/// </summary>
public sealed class FactionBountyRewardResult
{
    public string SponsorFactionId { get; init; } = FactionManager.NeutralCivilians;
    public string TargetFactionId { get; init; } = FactionManager.NeutralCivilians;
    public int DestroyedNpcIdentity { get; init; }
    public string DestroyedNpcName { get; init; } = string.Empty;
    public bool PlayerAttributed { get; init; }
    public bool VictimWasValid { get; init; }
    public FactionBountyRejectionReason RejectionReason { get; init; }
    public FactionBountyTier Tier { get; init; }
    public int AmountAwarded { get; init; }
    public double SimulationTimestampSeconds { get; init; }
    public string NotificationText { get; init; } = string.Empty;

    public bool IsEligible => RejectionReason == FactionBountyRejectionReason.Eligible;
    public bool WasAwarded => AmountAwarded > 0;
}

/// <summary>
/// Resolves lawful immediate kill rewards at the authoritative NPC
/// destruction boundary. It owns bounty policy, eligibility, payout tiers,
/// transient duplicate protection, credit mutation, and reward presentation.
/// </summary>
public sealed class FactionBountyRewardService
{
    public const string LibertyPoliceSponsorFaction = FactionManager.LibertyPolice;
    public const string LibertyRoguesTargetFaction = FactionManager.LibertyRogues;
    public const int StandardRogueBounty = 250;
    public const int HeavyRogueBounty = 500;
    public const int MaximumRememberedDestructions = 256;

    private static readonly IReadOnlyList<FactionBountyPolicy> Policies =
        new[]
        {
            new FactionBountyPolicy(
                LibertyPoliceSponsorFaction,
                LibertyRoguesTargetFaction,
                StandardRogueBounty,
                HeavyRogueBounty)
        };

    private readonly ReputationManager _reputationManager;
    private readonly PlayerCredits _playerCredits;
    private readonly Action<string>? _presentNotification;
    private readonly Func<NpcShip, bool>? _isActiveNpc;
    private readonly HashSet<NpcShip> _processedDestructions = new();
    private readonly Queue<NpcShip> _processedDestructionOrder = new();

    public FactionBountyRewardService(
        ReputationManager reputationManager,
        PlayerCredits playerCredits,
        Action<string>? presentNotification = null,
        Func<NpcShip, bool>? isActiveNpc = null)
    {
        _reputationManager = reputationManager ?? throw new ArgumentNullException(nameof(reputationManager));
        _playerCredits = playerCredits ?? throw new ArgumentNullException(nameof(playerCredits));
        _presentNotification = presentNotification;
        _isActiveNpc = isActiveNpc;
    }

    public IReadOnlyList<FactionBountyPolicy> BountyPolicies => Policies;
    public int RememberedDestructionCount => _processedDestructions.Count;

    public event Action<FactionBountyRewardResult>? RewardResolved;

    /// <summary>
    /// Processes one destruction callback. The object overload deliberately
    /// rejects non-NPC objects so callers cannot turn proximity or generic
    /// world-object events into a bounty.
    /// </summary>
    public FactionBountyRewardResult ProcessDestruction(
        object? destroyedEntity,
        double simulationTimestampSeconds = 0d)
    {
        if (destroyedEntity is not NpcShip destroyedShip)
            return Reject(
                null,
                FactionBountyRejectionReason.InvalidVictim,
                simulationTimestampSeconds);

        if (_isActiveNpc != null && !_isActiveNpc(destroyedShip))
            return Reject(
                destroyedShip,
                FactionBountyRejectionReason.InvalidVictim,
                simulationTimestampSeconds);

        if (!RememberDestruction(destroyedShip))
            return Reject(
                destroyedShip,
                FactionBountyRejectionReason.DuplicateKill,
                simulationTimestampSeconds);

        if (!destroyedShip.IsDestroyed)
            return Reject(
                destroyedShip,
                FactionBountyRejectionReason.VictimNotDestroyed,
                simulationTimestampSeconds);

        if (!destroyedShip.WasAliveImmediatelyBeforeDestruction)
            return Reject(
                destroyedShip,
                FactionBountyRejectionReason.VictimWasNotAlive,
                simulationTimestampSeconds);

        bool playerAttributed = destroyedShip.DestructionSource == NpcDestructionSource.Player;
        if (!playerAttributed || !destroyedShip.WasDamagedByPlayer)
            return Reject(
                destroyedShip,
                FactionBountyRejectionReason.NotPlayerAttributed,
                simulationTimestampSeconds,
                playerAttributed);

        if (!_reputationManager.FactionManager.TryGetFaction(destroyedShip.FactionId, out _))
            return Reject(
                destroyedShip,
                FactionBountyRejectionReason.InvalidFaction,
                simulationTimestampSeconds,
                playerAttributed);

        if (!TryGetPolicy(LibertyPoliceSponsorFaction, destroyedShip.FactionId, out FactionBountyPolicy policy))
            return Reject(
                destroyedShip,
                FactionBountyRejectionReason.NoBountyPolicy,
                simulationTimestampSeconds,
                playerAttributed,
                policy: null);

        if (!_reputationManager.FactionManager.TryGetFaction(policy.SponsorFactionId, out _))
            return Reject(
                destroyedShip,
                FactionBountyRejectionReason.InvalidFaction,
                simulationTimestampSeconds,
                playerAttributed,
                policy);

        if (_reputationManager.IsFactionCurrentlyHostile(policy.SponsorFactionId))
            return Reject(
                destroyedShip,
                FactionBountyRejectionReason.SponsorHostileToPlayer,
                simulationTimestampSeconds,
                playerAttributed,
                policy);

        FactionBountyTier tier = DetermineTier(destroyedShip);
        int amount = tier switch
        {
            FactionBountyTier.Standard => policy.StandardPayout,
            FactionBountyTier.Heavy => policy.HeavyPayout,
            _ => 0
        };

        if (amount <= 0)
            return Reject(
                destroyedShip,
                FactionBountyRejectionReason.NoRewardableShipDefinition,
                simulationTimestampSeconds,
                playerAttributed,
                policy,
                tier);

        if (!_playerCredits.TryAddCredits(amount))
            return Reject(
                destroyedShip,
                FactionBountyRejectionReason.CreditMutationFailed,
                simulationTimestampSeconds,
                playerAttributed,
                policy,
                tier);

        double timestamp = NormalizeTimestamp(simulationTimestampSeconds);
        string notification = $"{FactionManager.GetFactionDisplayName(policy.SponsorFactionId)} bounty: +{amount:N0} credits";
        FactionBountyRewardResult result = BuildResult(
            destroyedShip,
            policy,
            FactionBountyRejectionReason.Eligible,
            playerAttributed,
            tier,
            amount,
            timestamp,
            notification);

        // The credit mutation is deliberately before presentation. A failed
        // or duplicate callback cannot produce a reward notification.
        try
        {
            _presentNotification?.Invoke(notification);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[BOUNTY] Presentation observer failed after award: {ex.Message}");
        }

        try
        {
            RewardResolved?.Invoke(result);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[BOUNTY] Reward observer failed after award: {ex.Message}");
        }

        return result;
    }

    /// <summary>
    /// Clears transient duplicate state for a new game or newly loaded world.
    /// Credits and reputation remain owned by their existing authorities.
    /// </summary>
    public void Reset()
    {
        _processedDestructions.Clear();
        _processedDestructionOrder.Clear();
    }

    private bool RememberDestruction(NpcShip destroyedShip)
    {
        if (!_processedDestructions.Add(destroyedShip))
            return false;

        _processedDestructionOrder.Enqueue(destroyedShip);
        while (_processedDestructionOrder.Count > MaximumRememberedDestructions)
        {
            NpcShip expired = _processedDestructionOrder.Dequeue();
            _processedDestructions.Remove(expired);
        }

        return true;
    }

    private static bool TryGetPolicy(
        string sponsorFactionId,
        string? targetFactionId,
        out FactionBountyPolicy policy)
    {
        string normalizedTarget = FactionManager.NormalizeFactionId(targetFactionId);
        for (int i = 0; i < Policies.Count; i++)
        {
            FactionBountyPolicy candidate = Policies[i];
            if (string.Equals(candidate.SponsorFactionId, sponsorFactionId, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(candidate.TargetFactionId, normalizedTarget, StringComparison.OrdinalIgnoreCase))
            {
                policy = candidate;
                return true;
            }
        }

        policy = default;
        return false;
    }

    private static FactionBountyTier DetermineTier(NpcShip destroyedShip)
    {
        string descriptor = $"{destroyedShip.Name} {destroyedShip.ModelPath}";
        return descriptor.Contains("warthog", StringComparison.OrdinalIgnoreCase) ||
            descriptor.Contains("heavy", StringComparison.OrdinalIgnoreCase)
            ? FactionBountyTier.Heavy
            : FactionBountyTier.Standard;
    }

    private FactionBountyRewardResult Reject(
        NpcShip? destroyedShip,
        FactionBountyRejectionReason reason,
        double simulationTimestampSeconds,
        bool playerAttributed = false,
        FactionBountyPolicy? policy = null,
        FactionBountyTier tier = FactionBountyTier.None)
    {
        FactionBountyPolicy selectedPolicy = policy ?? new FactionBountyPolicy(
            LibertyPoliceSponsorFaction,
            FactionManager.NormalizeFactionId(destroyedShip?.FactionId),
            StandardRogueBounty,
            HeavyRogueBounty);

        return BuildResult(
            destroyedShip,
            selectedPolicy,
            reason,
            playerAttributed,
            tier,
            0,
            NormalizeTimestamp(simulationTimestampSeconds),
            string.Empty);
    }

    private static FactionBountyRewardResult BuildResult(
        NpcShip? destroyedShip,
        FactionBountyPolicy policy,
        FactionBountyRejectionReason reason,
        bool playerAttributed,
        FactionBountyTier tier,
        int amount,
        double timestamp,
        string notification)
    {
        return new FactionBountyRewardResult
        {
            SponsorFactionId = policy.SponsorFactionId,
            TargetFactionId = policy.TargetFactionId,
            DestroyedNpcIdentity = destroyedShip == null ? 0 : RuntimeHelpers.GetHashCode(destroyedShip),
            DestroyedNpcName = destroyedShip?.Name ?? string.Empty,
            PlayerAttributed = playerAttributed,
            VictimWasValid = destroyedShip != null,
            RejectionReason = reason,
            Tier = tier,
            AmountAwarded = Math.Max(0, amount),
            SimulationTimestampSeconds = timestamp,
            NotificationText = notification
        };
    }

    private static double NormalizeTimestamp(double timestamp)
    {
        return double.IsNaN(timestamp) || double.IsInfinity(timestamp) || timestamp < 0d
            ? 0d
            : timestamp;
    }
}
