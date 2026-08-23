#nullable enable

using System;
using System.Collections.Generic;

namespace Roguelancer;

/// <summary>
/// Resolves discrete, authoritative player combat events into faction
/// consequences. It owns policy and event de-duplication; ReputationManager
/// remains the authority that stores permanent standing and temporary hostility.
/// </summary>
public sealed class FactionCombatConsequenceService
{
    public const float InitialAggressionReputationPenalty = -0.03f;
    public const float NeutralNpcDestructionReputationPenalty = -0.12f;
    public const float HostileNpcDestructionReputationPenalty = 0f;
    public const float EnemyKillReputationReward = 0.04f;
    public const float AlliedKillReputationPenalty = -0.04f;
    public const float TemporaryHostilityDurationSeconds = TemporaryHostilityManager.DefaultDurationSeconds;

    private readonly ReputationManager _reputationManager;
    private readonly Dictionary<NpcShip, int> _observedPlayerDamage = new();
    private readonly HashSet<NpcShip> _processedPlayerDestructions = new();
    private readonly HashSet<NpcShip> _trackedShips = new();

    public FactionCombatConsequenceService(ReputationManager reputationManager)
    {
        _reputationManager = reputationManager ?? throw new ArgumentNullException(nameof(reputationManager));
    }

    public bool RecordPlayerDamage(NpcShip? damagedShip)
    {
        if (damagedShip == null || !damagedShip.WasDamagedByPlayer ||
            damagedShip.PlayerDamageSequence <= 0)
            return false;

        if (_observedPlayerDamage.TryGetValue(damagedShip, out int observedSequence) &&
            observedSequence >= damagedShip.PlayerDamageSequence)
            return false;

        _observedPlayerDamage[damagedShip] = damagedShip.PlayerDamageSequence;
        _trackedShips.Add(damagedShip);

        bool wasFactionHostile = WasAlreadyHostileForSelfDefense(damagedShip.FactionId);
        bool firstAggressionAgainstVictim = damagedShip.CapturePlayerAggressionProvenance(wasFactionHostile);

        if (wasFactionHostile)
            return true;

        _reputationManager.TemporaryHostility.RecordHostileAction(
            damagedShip.FactionId,
            TemporaryHostilityManager.PlayerAggressionReason,
            TemporaryHostilityDurationSeconds,
            causedByPlayerAggression: true);

        if (firstAggressionAgainstVictim)
        {
            _reputationManager.AdjustReputationDirect(
                damagedShip.FactionId,
                InitialAggressionReputationPenalty,
                ReputationChangeReason.FactionShipAttacked);
        }

        return true;
    }

    public ReputationChangeResult? ApplyPlayerShipDestroyed(NpcShip? destroyedShip)
    {
        if (destroyedShip == null || !destroyedShip.WasDamagedByPlayer)
            return null;

        // The destruction callback can arrive before the frame-level event
        // sweep. This also captures the self-defense provenance exactly once.
        RecordPlayerDamage(destroyedShip);
        if (!_processedPlayerDestructions.Add(destroyedShip))
            return null;

        float penalty = destroyedShip.WasFactionHostileBeforePlayerAggression
            ? HostileNpcDestructionReputationPenalty
            : NeutralNpcDestructionReputationPenalty;

        ReputationChangeResult? primaryChange = Math.Abs(penalty) < ReputationManager.Precision
            ? null
            : _reputationManager.AdjustReputationDirect(
                destroyedShip.FactionId,
                penalty,
                ReputationChangeReason.FactionShipDestroyed);

        ApplyRelationshipRipples(destroyedShip.FactionId);
        return primaryChange;
    }

    private void ApplyRelationshipRipples(string? destroyedFactionId)
    {
        string sourceFactionId = FactionManager.NormalizeFactionId(destroyedFactionId);
        foreach (FactionRelationshipDefinition relationship in
            FactionRelationshipMatrix.GetCombatRelationshipsFrom(sourceFactionId))
        {
            float delta = relationship.Kind switch
            {
                FactionRelationshipKind.Hostile => EnemyKillReputationReward,
                FactionRelationshipKind.Allied => AlliedKillReputationPenalty,
                _ => 0f
            };

            if (Math.Abs(delta) < ReputationManager.Precision)
                continue;

            _reputationManager.AdjustReputationSecondary(
                relationship.TargetFactionId,
                delta,
                ReputationChangeReason.FactionShipDestroyed,
                sourceFactionId);
        }
    }

    public void Reset()
    {
        foreach (NpcShip ship in _trackedShips)
            ship.ResetPlayerCombatAttribution();

        _observedPlayerDamage.Clear();
        _processedPlayerDestructions.Clear();
        _trackedShips.Clear();
    }

    private bool WasAlreadyHostileForSelfDefense(string? factionId)
    {
        if (_reputationManager.IsHostile(factionId))
            return true;

        return _reputationManager.IsTemporarilyHostile(factionId) &&
            !_reputationManager.TemporaryHostility.IsPlayerCausedByAggression(factionId);
    }
}
