#nullable enable

using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;

namespace Roguelancer;

public readonly record struct FactionCombatEscalationResult(
    bool Accepted,
    bool WaveSpawned,
    bool CooldownBlocked,
    int SpawnedShipCount,
    bool PlayerInvolved,
    string EncounterId,
    string FactionId,
    string Reason);

/// <summary>
/// Resolves the one bounded second-stage response for an already answered
/// local faction encounter. DistressResponse remains authoritative for the
/// first wave; this service only accumulates later evidence and asks the
/// traffic owner for a small escalation wave.
/// </summary>
public sealed class FactionCombatEscalationService
{
    public const int EscalationWaveSize = 2;
    public const int ActiveEscalationCap = 2;
    public const float EscalationCooldownSeconds = 120f;
    public const float LocalContextRadius = FactionDistressResponseService.LocalContextRadius;
    public const float EncounterExpirySeconds = FactionDistressResponseService.EncounterExpirySeconds;
    public const float MeaningfulDamageThreshold = FactionDistressResponseService.MeaningfulDamageThreshold;

    // Ten seconds makes the second response feel like a continuing battle,
    // rather than a second reaction to the same opening shot.
    public const float MinimumPostResponseDurationSeconds = 10f;

    // NPC weapons currently deliver 5 damage per hit. Four later hits are a
    // deliberately small but visible seriousness threshold; one qualifying
    // faction loss is an alternate threshold.
    public const float CumulativePostResponseDamageThreshold = 20f;

    private sealed class EncounterRecord
    {
        public string Id { get; init; } = string.Empty;
        public NpcShip Target { get; init; } = null!;
        public NpcShip? Attacker { get; init; }
        public string FactionId { get; init; } = string.Empty;
        public bool PlayerInvolved { get; init; }
        public Ship? PlayerShip { get; set; }
        public float FirstEvidenceTime { get; init; }
        public float DistressResponseTime { get; init; }
        public float LastEvidenceTime { get; set; }
        public float PostResponseDamage { get; set; }
        public int QualifyingDestructionCount { get; set; }
        public bool TargetDestructionObserved { get; set; }
        public bool EscalationAttempted { get; set; }
        public NpcShip? LastDamageAttacker { get; set; }
        public float LastDamageAmount { get; set; }
        public float LastDamageTime { get; set; } = -1f;
    }

    private readonly record struct EncounterKey(
        NpcShip Target,
        NpcShip? Attacker,
        string FactionId,
        bool IsPlayerAttack);

    private readonly record struct ResponseContextKey(
        string FactionId,
        int X,
        int Y,
        int Z);

    private readonly IReadOnlyList<NpcShip> _npcShips;
    private readonly FactionDistressResponseService _distressResponse;
    private ReputationManager? _reputationManager;
    private readonly Func<string, Vector3, string, int, IReadOnlyList<NpcShip>> _spawnEscalationReinforcements;
    private readonly Action<string>? _log;
    private readonly Dictionary<EncounterKey, EncounterRecord> _encounters = new();
    private readonly Dictionary<ResponseContextKey, float> _lastEscalationTimes = new();
    private float _simulationTime;

    public FactionCombatEscalationService(
        IReadOnlyList<NpcShip> npcShips,
        FactionDistressResponseService distressResponse,
        ReputationManager? reputationManager,
        Func<string, Vector3, string, int, IReadOnlyList<NpcShip>> spawnEscalationReinforcements,
        Action<string>? log = null)
    {
        _npcShips = npcShips ?? throw new ArgumentNullException(nameof(npcShips));
        _distressResponse = distressResponse ?? throw new ArgumentNullException(nameof(distressResponse));
        _reputationManager = reputationManager;
        _spawnEscalationReinforcements = spawnEscalationReinforcements ?? throw new ArgumentNullException(nameof(spawnEscalationReinforcements));
        _log = log;
    }

    public event Action<FactionCombatEscalationResult>? ResponseGenerated;

    public float SimulationTime => _simulationTime;
    public int ActiveEncounterCount => _encounters.Count;
    public int CooldownRecordCount => _lastEscalationTimes.Count;
    public bool IsEncounterActive(string? encounterId)
    {
        if (string.IsNullOrWhiteSpace(encounterId))
            return false;

        foreach (EncounterRecord encounter in _encounters.Values)
        {
            if (string.Equals(encounter.Id, encounterId, StringComparison.Ordinal))
                return true;
        }

        return false;
    }
    public int EscalatedEncounterCount
    {
        get
        {
            int count = 0;
            foreach (EncounterRecord encounter in _encounters.Values)
            {
                if (encounter.EscalationAttempted)
                    count++;
            }

            return count;
        }
    }

    public void SetReputationManager(ReputationManager? reputationManager)
    {
        _reputationManager = reputationManager;
        _distressResponse.SetReputationManager(reputationManager);
    }

    /// <summary>
    /// Advances both response stages on simulation time. The escalation pass
    /// only visits its small encounter dictionary; it never scans all NPCs.
    /// </summary>
    public void Update(float deltaTime)
    {
        float boundedDelta = Math.Max(0f, float.IsNaN(deltaTime) || float.IsInfinity(deltaTime) ? 0f : deltaTime);
        _distressResponse.Update(boundedDelta);
        _simulationTime += boundedDelta;
        CleanupExpiredState();

        if (_encounters.Count == 0)
            return;

        List<EncounterRecord> records = new(_encounters.Values);
        foreach (EncounterRecord encounter in records)
        {
            if (!encounter.EscalationAttempted)
                TryResolveEscalation(encounter);
        }
    }

    public FactionDistressResponseResult ProcessPlayerDamage(NpcShip? target, Ship? playerShip = null)
    {
        FactionDistressResponseResult ordinary = _distressResponse.ProcessPlayerDamage(target, playerShip);
        if (!ordinary.Accepted || target == null)
            return ordinary;

        ObserveValidated(
            target,
            attacker: null,
            playerShip,
            ordinary,
            target.LastPlayerDamage,
            isPlayerAttack: true);
        return ordinary;
    }

    public FactionDistressResponseResult ProcessNpcDamage(
        NpcShip? attacker,
        NpcShip? target,
        float damage)
    {
        FactionDistressResponseResult ordinary = _distressResponse.ProcessNpcDamage(attacker, target, damage);
        if (!ordinary.Accepted || attacker == null || target == null)
            return ordinary;

        ObserveValidated(
            target,
            attacker,
            playerShip: null,
            ordinary,
            damage,
            isPlayerAttack: false);
        return ordinary;
    }

    /// <summary>
    /// Called from the existing NPC destruction lifecycle. Destruction is
    /// evidence only for the original encounter victim; responder losses are
    /// explicitly ignored so they cannot create a new chain.
    /// </summary>
    public void NotifyNpcDestroyed(NpcShip? destroyedShip)
    {
        if (destroyedShip == null || destroyedShip.IsFactionTransientReinforcement)
            return;

        List<EncounterRecord> records = new(_encounters.Values);
        foreach (EncounterRecord encounter in records)
        {
            if (encounter.Target != destroyedShip || encounter.TargetDestructionObserved)
                continue;

            encounter.TargetDestructionObserved = true;
            encounter.QualifyingDestructionCount++;
            encounter.LastEvidenceTime = _simulationTime;
            TryResolveEscalation(encounter);
        }
    }

    public int CountActiveEscalationReinforcements(string factionId, Vector3 contextPosition)
    {
        string normalizedFaction = FactionManager.NormalizeFactionId(factionId);
        float radiusSquared = LocalContextRadius * LocalContextRadius;
        int count = 0;

        for (int i = 0; i < _npcShips.Count; i++)
        {
            NpcShip? ship = _npcShips[i];
            if (ship == null || ship.IsDestroyed || !ship.IsEscalationReinforcement ||
                !string.Equals(ship.FactionId, normalizedFaction, StringComparison.OrdinalIgnoreCase) ||
                Vector3.DistanceSquared(ship.Position, contextPosition) > radiusSquared)
            {
                continue;
            }

            count++;
        }

        return count;
    }

    public bool IsEscalationCooldownActive(string factionId, Vector3 contextPosition)
    {
        ResponseContextKey context = GetContextKey(FactionManager.NormalizeFactionId(factionId), contextPosition);
        return _lastEscalationTimes.TryGetValue(context, out float lastEscalationTime) &&
            _simulationTime - lastEscalationTime < EscalationCooldownSeconds;
    }

    public void Reset()
    {
        _distressResponse.Reset();
        for (int i = 0; i < _npcShips.Count; i++)
        {
            NpcShip? ship = _npcShips[i];
            if (ship == null || !ship.IsFactionTransientReinforcement)
                continue;

            ship.ClearEncounterState();
            ship.ClearDistressReinforcementProvenance();
            ship.ClearEscalationReinforcementProvenance();
        }

        _encounters.Clear();
        _lastEscalationTimes.Clear();
        _simulationTime = 0f;
    }

    private void ObserveValidated(
        NpcShip target,
        NpcShip? attacker,
        Ship? playerShip,
        FactionDistressResponseResult ordinary,
        float damage,
        bool isPlayerAttack)
    {
        string factionId = FactionManager.NormalizeFactionId(ordinary.FactionId);
        if (!FactionDistressResponseService.IsSupportedFaction(factionId) ||
            string.IsNullOrWhiteSpace(ordinary.EncounterId) ||
            float.IsNaN(damage) || float.IsInfinity(damage) || damage < MeaningfulDamageThreshold)
        {
            return;
        }

        EncounterKey key = new(target, attacker, factionId, isPlayerAttack);
        bool isNew = !_encounters.TryGetValue(key, out EncounterRecord? encounter);
        if (isNew)
        {
            encounter = new EncounterRecord
            {
                Id = ordinary.EncounterId,
                Target = target,
                Attacker = attacker,
                FactionId = factionId,
                PlayerInvolved = isPlayerAttack,
                PlayerShip = playerShip,
                FirstEvidenceTime = _simulationTime,
                DistressResponseTime = _simulationTime,
                LastEvidenceTime = _simulationTime
            };
            _encounters[key] = encounter;
        }

        if (encounter == null)
            return;

        encounter.PlayerShip = playerShip ?? encounter.PlayerShip;
        bool duplicate = !isNew && IsDuplicateDamage(encounter, attacker, damage);
        if (!duplicate)
        {
            encounter.LastEvidenceTime = _simulationTime;
            encounter.LastDamageAttacker = attacker;
            encounter.LastDamageAmount = damage;
            encounter.LastDamageTime = _simulationTime;

            // The first hit is the ordinary distress trigger. Only later
            // evidence contributes to second-stage seriousness.
            if (!isNew)
                encounter.PostResponseDamage += damage;
        }

        if (target.IsDestroyed && !encounter.TargetDestructionObserved)
        {
            encounter.TargetDestructionObserved = true;
            encounter.QualifyingDestructionCount++;
        }

        if (duplicate || encounter.EscalationAttempted)
            return;

        TryResolveEscalation(encounter);
    }

    private FactionCombatEscalationResult TryResolveEscalation(EncounterRecord encounter)
    {
        if (!encounter.PlayerInvolved &&
            (encounter.Attacker == null || encounter.Attacker.IsDestroyed || encounter.Attacker.IsFactionTransientReinforcement))
        {
            return AcceptedWithoutResponse(encounter, "original hostile attacker is no longer valid");
        }

        if (_simulationTime - encounter.DistressResponseTime < MinimumPostResponseDurationSeconds)
            return AcceptedWithoutResponse(encounter, "ordinary response still recent");

        bool continuedHostileEvidence = encounter.PostResponseDamage >= MeaningfulDamageThreshold ||
            encounter.QualifyingDestructionCount > 0;
        bool seriousDamage = encounter.PostResponseDamage >= CumulativePostResponseDamageThreshold;
        bool seriousLoss = encounter.QualifyingDestructionCount > 0;
        if (!continuedHostileEvidence || (!seriousDamage && !seriousLoss))
            return AcceptedWithoutResponse(encounter, "seriousness threshold not met");

        ResponseContextKey context = GetContextKey(encounter.FactionId, encounter.Target.Position);
        if (_lastEscalationTimes.TryGetValue(context, out float lastEscalationTime) &&
            _simulationTime - lastEscalationTime < EscalationCooldownSeconds)
        {
            return new FactionCombatEscalationResult(
                Accepted: true,
                WaveSpawned: false,
                CooldownBlocked: true,
                SpawnedShipCount: 0,
                PlayerInvolved: encounter.PlayerInvolved,
                EncounterId: encounter.Id,
                FactionId: encounter.FactionId,
                Reason: "faction escalation cooldown active");
        }

        int activeCount = CountActiveEscalationReinforcements(encounter.FactionId, encounter.Target.Position);
        int requestCount = Math.Min(EscalationWaveSize, Math.Max(0, ActiveEscalationCap - activeCount));
        if (requestCount <= 0)
        {
            return AcceptedWithoutResponse(encounter, "escalation live cap active");
        }

        IReadOnlyList<NpcShip> spawned = _spawnEscalationReinforcements(
            encounter.FactionId,
            encounter.Target.Position,
            encounter.Id,
            requestCount) ?? Array.Empty<NpcShip>();

        for (int i = 0; i < spawned.Count; i++)
        {
            NpcShip? reinforcement = spawned[i];
            if (reinforcement == null)
                continue;

            reinforcement.MarkEscalationReinforcement(encounter.Id);
            AssignInitialTarget(reinforcement, encounter);
        }

        encounter.EscalationAttempted = true;
        _lastEscalationTimes[context] = _simulationTime;

        FactionCombatEscalationResult result = new(
            Accepted: true,
            WaveSpawned: spawned.Count > 0,
            CooldownBlocked: false,
            SpawnedShipCount: spawned.Count,
            PlayerInvolved: encounter.PlayerInvolved,
            EncounterId: encounter.Id,
            FactionId: encounter.FactionId,
            Reason: spawned.Count > 0 ? "heavy reinforcements inbound" : "escalation spawn unavailable");

        if (spawned.Count > 0)
        {
            string displayName = FactionManager.GetFactionDisplayName(encounter.FactionId);
            _log?.Invoke($"[ESCALATION] {displayName} heavy reinforcements inbound ({spawned.Count}).");
            ResponseGenerated?.Invoke(result);
        }

        return result;
    }

    private void AssignInitialTarget(NpcShip reinforcement, EncounterRecord encounter)
    {
        if (!encounter.PlayerInvolved)
        {
            if (encounter.Attacker != null &&
                NpcFactionCombatTargeting.IsValidHostileTarget(reinforcement, encounter.Attacker, LocalContextRadius))
            {
                reinforcement.SetFactionCombatTarget(
                    encounter.Attacker,
                    targetOrigin: FactionCombatTargetOrigin.EscalationResponse);
            }

            return;
        }

        Ship? playerShip = encounter.PlayerShip;
        bool playerIsValid = playerShip != null &&
            (_reputationManager == null || _reputationManager.IsFactionCurrentlyHostile(encounter.FactionId));
        if (playerIsValid)
            reinforcement.SetPlayerTarget(playerShip!.Position, NpcPlayerTargetReason.FactionDisposition);
    }

    private bool IsDuplicateDamage(EncounterRecord encounter, NpcShip? attacker, float damage)
    {
        return encounter.LastDamageTime >= 0f &&
            Math.Abs(encounter.LastDamageTime - _simulationTime) <= 0.0001f &&
            encounter.LastDamageAttacker == attacker &&
            Math.Abs(encounter.LastDamageAmount - damage) <= 0.0001f;
    }

    private void CleanupExpiredState()
    {
        if (_encounters.Count > 0)
        {
            List<EncounterKey>? expiredEncounters = null;
            foreach (KeyValuePair<EncounterKey, EncounterRecord> entry in _encounters)
            {
                if (_simulationTime - entry.Value.LastEvidenceTime < EncounterExpirySeconds)
                    continue;

                expiredEncounters ??= new List<EncounterKey>();
                expiredEncounters.Add(entry.Key);
            }

            if (expiredEncounters != null)
            {
                for (int i = 0; i < expiredEncounters.Count; i++)
                    _encounters.Remove(expiredEncounters[i]);
            }
        }

        if (_lastEscalationTimes.Count > 0)
        {
            List<ResponseContextKey>? expiredCooldowns = null;
            foreach (KeyValuePair<ResponseContextKey, float> entry in _lastEscalationTimes)
            {
                if (_simulationTime - entry.Value < EscalationCooldownSeconds)
                    continue;

                expiredCooldowns ??= new List<ResponseContextKey>();
                expiredCooldowns.Add(entry.Key);
            }

            if (expiredCooldowns != null)
            {
                for (int i = 0; i < expiredCooldowns.Count; i++)
                    _lastEscalationTimes.Remove(expiredCooldowns[i]);
            }
        }
    }

    private static ResponseContextKey GetContextKey(string factionId, Vector3 position)
    {
        return new(
            factionId,
            (int)Math.Floor(position.X / LocalContextRadius),
            (int)Math.Floor(position.Y / LocalContextRadius),
            (int)Math.Floor(position.Z / LocalContextRadius));
    }

    private static FactionCombatEscalationResult AcceptedWithoutResponse(EncounterRecord encounter, string reason) =>
        new(
            Accepted: true,
            WaveSpawned: false,
            CooldownBlocked: false,
            SpawnedShipCount: 0,
            PlayerInvolved: encounter.PlayerInvolved,
            EncounterId: encounter.Id,
            FactionId: encounter.FactionId,
            Reason: reason);
}
