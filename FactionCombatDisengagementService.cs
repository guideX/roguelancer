#nullable enable

using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;

namespace Roguelancer;

public enum FactionCombatDisengagementReason
{
    TargetDestroyed,
    TargetDespawned,
    TargetNoLongerHostile,
    ExcessivePursuitDistance,
    StalePursuit,
    EncounterExpired,
    Reset
}

public readonly record struct FactionCombatEngagementSnapshot(
    bool IsPlayerTarget,
    NpcShip? Target,
    float AcquiredAt,
    float LastMeaningfulHostileEvidenceAt,
    float LastKnownTargetDistance,
    FactionCombatTargetOrigin TargetOrigin);

/// <summary>
/// Owns the lifetime of an existing transient combat pursuit. Faction
/// relationships remain authoritative elsewhere; this service only decides
/// when one NPC should stop pursuing one stale local target.
/// </summary>
public sealed class FactionCombatDisengagementService
{
    public const float SoftPursuitRadius = 7_500f;
    public const float HardPursuitRadius = 10_000f;
    public const float RecentHostileEvidenceGraceSeconds = 10f;
    public const float MaximumStalePursuitSeconds = 30f;
    public const float TargetReacquisitionSuppressionSeconds = 5f;
    public const float MeaningfulHostileEvidenceThreshold = FactionDistressResponseService.MeaningfulDamageThreshold;

    private sealed class EngagementState
    {
        public bool IsPlayerTarget { get; init; }
        public NpcShip? Target { get; init; }
        public float AcquiredAt { get; set; }
        public float LastMeaningfulHostileEvidenceAt { get; set; }
        public float LastKnownTargetDistance { get; set; }
        public FactionCombatTargetOrigin TargetOrigin { get; set; }
    }

    private readonly record struct SuppressionKey(NpcShip Source, NpcShip Target);

    private sealed class PlayerDamageObservation
    {
        public int Sequence { get; init; }
    }

    private readonly IReadOnlyList<NpcShip> _npcShips;
    private ReputationManager? _reputationManager;
    private readonly Func<string, bool>? _isResponseEncounterActive;
    private readonly Dictionary<NpcShip, EngagementState> _engagements = new();
    private readonly Dictionary<SuppressionKey, float> _suppressedTargets = new();
    private readonly Dictionary<NpcShip, float> _suppressedPlayers = new();
    private readonly Dictionary<NpcShip, PlayerDamageObservation> _observedPlayerDamage = new();
    private readonly Dictionary<NpcShip, float> _pendingPlayerEvidence = new();
    private readonly Dictionary<NpcShip, FactionCombatDisengagementReason> _lastDisengagementReasons = new();
    private readonly HashSet<NpcShip> _activeNpcShips = new();
    private float _simulationTime;

    public FactionCombatDisengagementService(
        IReadOnlyList<NpcShip> npcShips,
        ReputationManager? reputationManager = null,
        Func<string, bool>? isResponseEncounterActive = null)
    {
        _npcShips = npcShips ?? throw new ArgumentNullException(nameof(npcShips));
        _reputationManager = reputationManager;
        _isResponseEncounterActive = isResponseEncounterActive;
    }

    public float SimulationTime => _simulationTime;
    public int ActiveEngagementCount => _engagements.Count;
    public int SuppressedTargetCount => _suppressedTargets.Count + _suppressedPlayers.Count;

    public void SetReputationManager(ReputationManager? reputationManager)
    {
        _reputationManager = reputationManager;
    }

    /// <summary>
    /// Advances simulation-time bookkeeping and validates only ships that
    /// currently carry a combat target. The active index is refreshed once so
    /// removed runtime ships are O(1) target-validity checks below.
    /// </summary>
    public void Update(float deltaTime, Ship? playerShip = null)
    {
        float boundedDelta = Math.Max(
            0f,
            float.IsNaN(deltaTime) || float.IsInfinity(deltaTime) ? 0f : deltaTime);
        _simulationTime += boundedDelta;
        RefreshActiveNpcIndex();
        CleanupSuppressionState();

        for (int i = 0; i < _npcShips.Count; i++)
        {
            NpcShip? source = _npcShips[i];
            if (source == null || source.IsDestroyed || !_activeNpcShips.Contains(source))
                continue;

            SynchronizeEngagement(source, playerShip);
        }

        if (_engagements.Count == 0)
            return;

        List<NpcShip> sources = new(_engagements.Keys);
        for (int i = 0; i < sources.Count; i++)
        {
            if (!_engagements.TryGetValue(sources[i], out EngagementState? engagement))
                continue;

            EvaluateEngagement(sources[i], engagement, playerShip);
        }
    }

    /// <summary>
    /// Records qualifying NPC-vs-NPC damage for either side of the current
    /// pursuit. Zero, self, environmental, dead, transient-invalid, and
    /// non-hostile evidence is rejected before any timestamp is refreshed.
    /// </summary>
    public bool RecordNpcDamage(NpcShip? attacker, NpcShip? damagedShip, float damage)
    {
        if (attacker == null || damagedShip == null || attacker == damagedShip ||
            attacker.IsDestroyed || damagedShip.IsDestroyed ||
            !_activeNpcShips.Contains(attacker) || !_activeNpcShips.Contains(damagedShip) ||
            float.IsNaN(damage) || float.IsInfinity(damage) ||
            damage < MeaningfulHostileEvidenceThreshold ||
            !NpcFactionCombatTargeting.IsHostileFactionPair(attacker.FactionId, damagedShip.FactionId))
        {
            return false;
        }

        bool recorded = false;
        if (attacker.FactionCombatTarget == damagedShip)
        {
            MarkMeaningfulEvidence(attacker, isPlayerTarget: false, damagedShip);
            recorded = true;
        }

        if (damagedShip.FactionCombatTarget == attacker)
        {
            MarkMeaningfulEvidence(damagedShip, isPlayerTarget: false, attacker);
            recorded = true;
        }

        return recorded;
    }

    /// <summary>
    /// Consumes the existing monotonic player-damage attribution sequence.
    /// It never invents a new hostility rule and therefore follows the live
    /// ReputationManager/FactionDisposition authority on the next update.
    /// </summary>
    public bool RecordPlayerDamage(NpcShip? damagedShip)
    {
        if (damagedShip == null || damagedShip.IsDestroyed ||
            !_activeNpcShips.Contains(damagedShip) || !damagedShip.WasDamagedByPlayer ||
            damagedShip.PlayerDamageSequence <= 0 ||
            damagedShip.LastPlayerDamage < MeaningfulHostileEvidenceThreshold)
        {
            return false;
        }

        if (_observedPlayerDamage.TryGetValue(damagedShip, out PlayerDamageObservation? observation) &&
            observation.Sequence >= damagedShip.PlayerDamageSequence)
        {
            return false;
        }

        _observedPlayerDamage[damagedShip] = new PlayerDamageObservation
        {
            Sequence = damagedShip.PlayerDamageSequence
        };

        if (damagedShip.HasPlayerTarget && damagedShip.HasValidPlayerTarget(_reputationManager))
        {
            MarkMeaningfulEvidence(damagedShip, isPlayerTarget: true, target: null);
            return true;
        }

        // The existing player-disposition update may establish retaliation on
        // the following NpcShip update. Keep only this transient same-encounter
        // timestamp so the newly established target starts with real evidence.
        _pendingPlayerEvidence[damagedShip] = _simulationTime;
        return false;
    }

    public bool IsTargetAcquisitionSuppressed(NpcShip? source, NpcShip? target)
    {
        if (source == null || target == null)
            return false;

        return _suppressedTargets.TryGetValue(new SuppressionKey(source, target), out float until) &&
            _simulationTime < until;
    }

    public bool IsPlayerAcquisitionSuppressed(NpcShip? source)
    {
        return source != null && _suppressedPlayers.TryGetValue(source, out float until) &&
            _simulationTime < until;
    }

    public bool TryGetEngagementState(NpcShip? source, out FactionCombatEngagementSnapshot snapshot)
    {
        if (source != null && _engagements.TryGetValue(source, out EngagementState? state))
        {
            snapshot = new FactionCombatEngagementSnapshot(
                state.IsPlayerTarget,
                state.Target,
                state.AcquiredAt,
                state.LastMeaningfulHostileEvidenceAt,
                state.LastKnownTargetDistance,
                state.TargetOrigin);
            return true;
        }

        snapshot = default;
        return false;
    }

    public bool TryGetLastDisengagementReason(
        NpcShip? source,
        out FactionCombatDisengagementReason reason)
    {
        if (source != null && _lastDisengagementReasons.TryGetValue(source, out reason))
            return true;

        reason = default;
        return false;
    }

    public void RegisterShip(NpcShip? ship)
    {
        if (ship != null)
            _activeNpcShips.Add(ship);
    }

    public void UnregisterShip(NpcShip? ship)
    {
        if (ship == null)
            return;

        _activeNpcShips.Remove(ship);
        _engagements.Remove(ship);
        _observedPlayerDamage.Remove(ship);
        _pendingPlayerEvidence.Remove(ship);
        _suppressedPlayers.Remove(ship);
        _lastDisengagementReasons.Remove(ship);

        List<SuppressionKey>? removed = null;
        foreach (SuppressionKey key in _suppressedTargets.Keys)
        {
            if (key.Source != ship && key.Target != ship)
                continue;

            removed ??= new List<SuppressionKey>();
            removed.Add(key);
        }

        if (removed == null)
            return;

        for (int i = 0; i < removed.Count; i++)
            _suppressedTargets.Remove(removed[i]);
    }

    public void NotifyNpcDestroyed(NpcShip? destroyedShip)
    {
        if (destroyedShip == null)
            return;

        for (int i = 0; i < _npcShips.Count; i++)
        {
            NpcShip? source = _npcShips[i];
            if (source == null || source == destroyedShip || source.FactionCombatTarget != destroyedShip)
                continue;

            source.ClearFactionCombatTarget();
            _engagements.Remove(source);
        }

        UnregisterShip(destroyedShip);
    }

    public void NotifyNpcDespawned(NpcShip? despawnedShip)
    {
        if (despawnedShip == null)
            return;

        for (int i = 0; i < _npcShips.Count; i++)
        {
            NpcShip? source = _npcShips[i];
            if (source == null || source == despawnedShip || source.FactionCombatTarget != despawnedShip)
                continue;

            source.ClearFactionCombatTarget();
            _engagements.Remove(source);
        }

        UnregisterShip(despawnedShip);
    }

    public void Reset()
    {
        for (int i = 0; i < _npcShips.Count; i++)
        {
            NpcShip? ship = _npcShips[i];
            if (ship == null)
                continue;

            ship.SetFactionCombatDisengagementManaged(false);
            ship.ClearEncounterState();
        }

        _engagements.Clear();
        _suppressedTargets.Clear();
        _suppressedPlayers.Clear();
        _observedPlayerDamage.Clear();
        _pendingPlayerEvidence.Clear();
        _lastDisengagementReasons.Clear();
        _activeNpcShips.Clear();
        _simulationTime = 0f;
    }

    private void SynchronizeEngagement(NpcShip source, Ship? playerShip)
    {
        if (source.FactionCombatTarget != null)
        {
            source.SetFactionCombatDisengagementManaged(true);
            if (!_engagements.TryGetValue(source, out EngagementState? existing) ||
                existing.IsPlayerTarget || existing.Target != source.FactionCombatTarget)
            {
                EngagementState state = new()
                {
                    IsPlayerTarget = false,
                    Target = source.FactionCombatTarget,
                    AcquiredAt = _simulationTime,
                    LastMeaningfulHostileEvidenceAt = _simulationTime,
                    LastKnownTargetDistance = Vector3.Distance(source.Position, source.FactionCombatTarget.Position),
                    TargetOrigin = source.FactionCombatTargetOrigin
                };
                _engagements[source] = state;
            }
            else
            {
                existing.LastKnownTargetDistance = Vector3.Distance(source.Position, existing.Target!.Position);
                existing.TargetOrigin = source.FactionCombatTargetOrigin;
            }

            return;
        }

        source.SetFactionCombatDisengagementManaged(false);
        if (!source.HasPlayerTarget)
        {
            _engagements.Remove(source);
            return;
        }

        if (!_engagements.TryGetValue(source, out EngagementState? playerEngagement) ||
            !playerEngagement.IsPlayerTarget)
        {
            playerEngagement = new EngagementState
            {
                IsPlayerTarget = true,
                Target = null,
                AcquiredAt = _simulationTime,
                LastMeaningfulHostileEvidenceAt = _simulationTime,
                LastKnownTargetDistance = GetPlayerDistance(source, playerShip),
                TargetOrigin = GetPlayerTargetOrigin(source)
            };
            _engagements[source] = playerEngagement;
        }
        else
        {
            playerEngagement.LastKnownTargetDistance = GetPlayerDistance(source, playerShip);
            playerEngagement.TargetOrigin = GetPlayerTargetOrigin(source);
        }

        if (_pendingPlayerEvidence.TryGetValue(source, out float evidenceAt) && evidenceAt >= playerEngagement.AcquiredAt)
        {
            playerEngagement.LastMeaningfulHostileEvidenceAt = evidenceAt;
            _pendingPlayerEvidence.Remove(source);
        }
    }

    private void EvaluateEngagement(NpcShip source, EngagementState engagement, Ship? playerShip)
    {
        if (engagement.IsPlayerTarget)
        {
            if (!source.HasPlayerTarget || !source.HasValidPlayerTarget(_reputationManager))
            {
                Disengage(source, engagement, FactionCombatDisengagementReason.TargetNoLongerHostile, suppress: false);
                return;
            }

            float playerDistance = GetPlayerDistance(source, playerShip);
            engagement.LastKnownTargetDistance = playerDistance;
            EvaluateDistanceAndStaleness(source, engagement, playerDistance);
            return;
        }

        NpcShip? target = engagement.Target;
        if (target == null)
        {
            Disengage(source, engagement, FactionCombatDisengagementReason.TargetDespawned, suppress: false);
            return;
        }

        if (!_activeNpcShips.Contains(target))
        {
            Disengage(source, engagement, FactionCombatDisengagementReason.TargetDespawned, suppress: false);
            return;
        }

        if (target.IsDestroyed)
        {
            Disengage(source, engagement, FactionCombatDisengagementReason.TargetDestroyed, suppress: false);
            return;
        }

        if (!NpcFactionCombatTargeting.IsValidHostileTarget(source, target))
        {
            Disengage(source, engagement, FactionCombatDisengagementReason.TargetNoLongerHostile, suppress: false);
            return;
        }

        if (HasExpiredResponseEncounter(source, engagement))
        {
            Disengage(source, engagement, FactionCombatDisengagementReason.EncounterExpired, suppress: false);
            return;
        }

        float distance = Vector3.Distance(source.Position, target.Position);
        engagement.LastKnownTargetDistance = distance;
        EvaluateDistanceAndStaleness(source, engagement, distance);
    }

    private void EvaluateDistanceAndStaleness(NpcShip source, EngagementState engagement, float distance)
    {
        float evidenceAge = Math.Max(0f, _simulationTime - engagement.LastMeaningfulHostileEvidenceAt);
        bool hasRecentEvidence = evidenceAge <= RecentHostileEvidenceGraceSeconds;

        if (distance > HardPursuitRadius && !hasRecentEvidence)
        {
            Disengage(source, engagement, FactionCombatDisengagementReason.ExcessivePursuitDistance, suppress: true);
            return;
        }

        if (distance > SoftPursuitRadius && !hasRecentEvidence)
        {
            Disengage(source, engagement, FactionCombatDisengagementReason.ExcessivePursuitDistance, suppress: true);
            return;
        }

        float ordinaryRange = Math.Max(100f, source.TrafficActivationRange);
        if (distance > ordinaryRange && evidenceAge > MaximumStalePursuitSeconds)
        {
            Disengage(source, engagement, FactionCombatDisengagementReason.StalePursuit, suppress: true);
        }
    }

    private bool HasExpiredResponseEncounter(NpcShip source, EngagementState engagement)
    {
        if (_isResponseEncounterActive == null ||
            engagement.TargetOrigin == FactionCombatTargetOrigin.OrdinaryAcquisition)
        {
            return false;
        }

        string encounterId = engagement.TargetOrigin == FactionCombatTargetOrigin.DistressResponse
            ? source.DistressReinforcementEncounterId
            : source.EscalationReinforcementEncounterId;
        if (string.IsNullOrWhiteSpace(encounterId) || _isResponseEncounterActive(encounterId))
            return false;

        float evidenceAge = Math.Max(0f, _simulationTime - engagement.LastMeaningfulHostileEvidenceAt);
        return evidenceAge > RecentHostileEvidenceGraceSeconds;
    }

    private void MarkMeaningfulEvidence(NpcShip source, bool isPlayerTarget, NpcShip? target)
    {
        EngagementState state;
        if (!_engagements.TryGetValue(source, out state!) ||
            state.IsPlayerTarget != isPlayerTarget ||
            (!isPlayerTarget && state.Target != target))
        {
            state = new EngagementState
            {
                IsPlayerTarget = isPlayerTarget,
                Target = isPlayerTarget ? null : target,
                AcquiredAt = _simulationTime,
                LastKnownTargetDistance = isPlayerTarget ? 0f : Vector3.Distance(source.Position, target!.Position),
                TargetOrigin = isPlayerTarget ? GetPlayerTargetOrigin(source) : source.FactionCombatTargetOrigin
            };
            _engagements[source] = state;
        }

        state.LastMeaningfulHostileEvidenceAt = _simulationTime;
        if (!isPlayerTarget && target != null)
            state.LastKnownTargetDistance = Vector3.Distance(source.Position, target.Position);
    }

    private void Disengage(
        NpcShip source,
        EngagementState engagement,
        FactionCombatDisengagementReason reason,
        bool suppress)
    {
        if (engagement.IsPlayerTarget)
        {
            source.ClearEncounterState();
            if (suppress)
                _suppressedPlayers[source] = _simulationTime + TargetReacquisitionSuppressionSeconds;
        }
        else if (source.FactionCombatTarget == engagement.Target)
        {
            source.ClearFactionCombatTarget();
            if (suppress && engagement.Target != null)
            {
                _suppressedTargets[new SuppressionKey(source, engagement.Target)] =
                    _simulationTime + TargetReacquisitionSuppressionSeconds;
            }
        }

        source.SetFactionCombatDisengagementManaged(false);
        _lastDisengagementReasons[source] = reason;
        _engagements.Remove(source);
    }

    private void RefreshActiveNpcIndex()
    {
        _activeNpcShips.Clear();
        for (int i = 0; i < _npcShips.Count; i++)
        {
            NpcShip? ship = _npcShips[i];
            if (ship != null)
                _activeNpcShips.Add(ship);
        }
    }

    private void CleanupSuppressionState()
    {
        if (_suppressedTargets.Count > 0)
        {
            List<SuppressionKey>? expiredTargets = null;
            foreach (KeyValuePair<SuppressionKey, float> entry in _suppressedTargets)
            {
                if (_simulationTime < entry.Value)
                    continue;

                expiredTargets ??= new List<SuppressionKey>();
                expiredTargets.Add(entry.Key);
            }

            if (expiredTargets != null)
            {
                for (int i = 0; i < expiredTargets.Count; i++)
                    _suppressedTargets.Remove(expiredTargets[i]);
            }
        }

        if (_suppressedPlayers.Count > 0)
        {
            List<NpcShip>? expiredPlayers = null;
            foreach (KeyValuePair<NpcShip, float> entry in _suppressedPlayers)
            {
                if (_simulationTime < entry.Value)
                    continue;

                expiredPlayers ??= new List<NpcShip>();
                expiredPlayers.Add(entry.Key);
            }

            if (expiredPlayers != null)
            {
                for (int i = 0; i < expiredPlayers.Count; i++)
                    _suppressedPlayers.Remove(expiredPlayers[i]);
            }
        }
    }

    private static float GetPlayerDistance(NpcShip source, Ship? playerShip)
    {
        if (playerShip != null)
            return Vector3.Distance(source.Position, playerShip.Position);

        return source.EncounterTargetPosition.HasValue
            ? Vector3.Distance(source.Position, source.EncounterTargetPosition.Value)
            : 0f;
    }

    private static FactionCombatTargetOrigin GetPlayerTargetOrigin(NpcShip source)
    {
        if (source.IsEscalationReinforcement)
            return FactionCombatTargetOrigin.EscalationResponse;
        if (source.IsDistressReinforcement)
            return FactionCombatTargetOrigin.DistressResponse;
        return FactionCombatTargetOrigin.OrdinaryAcquisition;
    }
}
