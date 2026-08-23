#nullable enable

using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;

namespace Roguelancer;

public enum FactionDistressDamageSource
{
    Player,
    HostileNpc
}

public readonly record struct FactionDistressResponseResult(
    bool Accepted,
    bool WaveSpawned,
    bool CooldownBlocked,
    int AssistedShipCount,
    int SpawnedShipCount,
    string EncounterId,
    string FactionId,
    string Reason);

/// <summary>
/// Resolves meaningful local attacks into bounded faction assistance. This
/// service owns evidence validation, encounter de-duplication, cooldowns,
/// helper assignment, and response caps. TrafficManager remains the only
/// component that creates the returned reinforcement ships.
/// </summary>
public sealed class FactionDistressResponseService
{
    public const int ReinforcementWaveSize = 2;
    public const int ActiveReinforcementCap = 2;
    public const float ReinforcementCooldownSeconds = 60f;
    public const float LocalContextRadius = 6500f;
    public const float ExistingAssistanceRange = 3500f;
    public const float EncounterExpirySeconds = 90f;
    public const float MeaningfulDamageThreshold = 0.01f;

    private sealed class EncounterRecord
    {
        public string Id { get; init; } = string.Empty;
        public float LastSeenTime { get; set; }
        public bool WaveRequested { get; set; }
    }

    private sealed class PlayerDamageObservation
    {
        public int Sequence { get; init; }
        public float LastSeenTime { get; set; }
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
    private ReputationManager? _reputationManager;
    private readonly Func<string, Vector3, string, int, IReadOnlyList<NpcShip>> _spawnReinforcements;
    private readonly Action<string>? _log;
    private readonly Dictionary<EncounterKey, EncounterRecord> _encounters = new();
    private readonly Dictionary<ResponseContextKey, float> _lastResponseTimes = new();
    private readonly Dictionary<NpcShip, PlayerDamageObservation> _observedPlayerDamage = new();
    private float _simulationTime;
    private int _nextEncounterSerial = 1;

    public FactionDistressResponseService(
        IReadOnlyList<NpcShip> npcShips,
        ReputationManager? reputationManager,
        Func<string, Vector3, string, int, IReadOnlyList<NpcShip>> spawnReinforcements,
        Action<string>? log = null)
    {
        _npcShips = npcShips ?? throw new ArgumentNullException(nameof(npcShips));
        _reputationManager = reputationManager;
        _spawnReinforcements = spawnReinforcements ?? throw new ArgumentNullException(nameof(spawnReinforcements));
        _log = log;
    }

    public event Action<FactionDistressResponseResult>? ResponseGenerated;

    public float SimulationTime => _simulationTime;
    public int ActiveEncounterCount => _encounters.Count;
    public int CooldownRecordCount => _lastResponseTimes.Count;

    public void SetReputationManager(ReputationManager? reputationManager)
    {
        _reputationManager = reputationManager;
    }

    public void Update(float deltaTime)
    {
        _simulationTime += Math.Max(0f, deltaTime);
        CleanupExpiredState();
    }

    public FactionDistressResponseResult ProcessPlayerDamage(NpcShip? target, Ship? playerShip = null)
    {
        if (target == null || !IsSupportedFaction(target.FactionId) ||
            !target.WasDamagedByPlayer || target.PlayerDamageSequence <= 0 ||
            target.LastPlayerDamage < MeaningfulDamageThreshold)
        {
            return Rejected("invalid player damage attribution");
        }

        if (_observedPlayerDamage.TryGetValue(target, out PlayerDamageObservation? observation) &&
            observation.Sequence >= target.PlayerDamageSequence)
        {
            return Rejected("duplicate player damage event");
        }

        _observedPlayerDamage[target] = new PlayerDamageObservation
        {
            Sequence = target.PlayerDamageSequence,
            LastSeenTime = _simulationTime
        };
        return ProcessValidated(
            target,
            attacker: null,
            attackerPosition: playerShip?.Position ?? target.Position,
            FactionDistressDamageSource.Player,
            target.LastPlayerDamage,
            allowDestroyedTarget: true);
    }

    public FactionDistressResponseResult ProcessNpcDamage(
        NpcShip? attacker,
        NpcShip? target,
        float damage)
    {
        if (attacker == null || target == null ||
            float.IsNaN(damage) || float.IsInfinity(damage) ||
            damage < MeaningfulDamageThreshold)
        {
            return Rejected("invalid NPC damage event");
        }

        return ProcessValidated(
            target,
            attacker,
            attacker.Position,
            FactionDistressDamageSource.HostileNpc,
            damage,
            allowDestroyedTarget: false);
    }

    public int CountActiveReinforcements(string factionId, Vector3 contextPosition)
    {
        string normalizedFaction = FactionManager.NormalizeFactionId(factionId);
        float radiusSquared = LocalContextRadius * LocalContextRadius;
        int count = 0;

        for (int i = 0; i < _npcShips.Count; i++)
        {
            NpcShip? ship = _npcShips[i];
            if (ship == null || ship.IsDestroyed || !ship.IsDistressReinforcement ||
                !string.Equals(ship.FactionId, normalizedFaction, StringComparison.OrdinalIgnoreCase) ||
                Vector3.DistanceSquared(ship.Position, contextPosition) > radiusSquared)
            {
                continue;
            }

            count++;
        }

        return count;
    }

    public void Reset()
    {
        for (int i = 0; i < _npcShips.Count; i++)
        {
            NpcShip? ship = _npcShips[i];
            if (ship == null || !ship.IsDistressReinforcement)
                continue;

            ship.ClearEncounterState();
            ship.ClearDistressReinforcementProvenance();
        }

        _encounters.Clear();
        _lastResponseTimes.Clear();
        _observedPlayerDamage.Clear();
        _simulationTime = 0f;
        _nextEncounterSerial = 1;
    }

    private FactionDistressResponseResult ProcessValidated(
        NpcShip target,
        NpcShip? attacker,
        Vector3 attackerPosition,
        FactionDistressDamageSource source,
        float damage,
        bool allowDestroyedTarget)
    {
        if (target.IsDistressReinforcement ||
            (!allowDestroyedTarget && target.IsDestroyed) ||
            (attacker != null && (attacker == target || attacker.IsDestroyed || attacker.IsDistressReinforcement)) ||
            damage < MeaningfulDamageThreshold)
        {
            return Rejected("damage cannot generate distress");
        }

        string factionId = FactionManager.NormalizeFactionId(target.FactionId);
        if (!IsSupportedFaction(factionId))
        {
            return Rejected("faction has no distress response");
        }

        if (source == FactionDistressDamageSource.HostileNpc &&
            !NpcFactionCombatTargeting.IsHostileFactionPair(attacker?.FactionId, target.FactionId))
        {
            return Rejected("NPC factions are not hostile");
        }

        EncounterKey key = new(target, attacker, factionId, source == FactionDistressDamageSource.Player);
        if (!_encounters.TryGetValue(key, out EncounterRecord? encounter))
        {
            encounter = new EncounterRecord
            {
                Id = $"distress-{_nextEncounterSerial++}",
                LastSeenTime = _simulationTime
            };
            _encounters[key] = encounter;
        }
        else
        {
            encounter.LastSeenTime = _simulationTime;
        }

        int assistedCount = AssignNearbyAssistance(target, attacker, attackerPosition, source);
        if (encounter.WaveRequested)
        {
            return new FactionDistressResponseResult(
                Accepted: true,
                WaveSpawned: false,
                CooldownBlocked: false,
                AssistedShipCount: assistedCount,
                SpawnedShipCount: 0,
                EncounterId: encounter.Id,
                FactionId: factionId,
                Reason: "encounter already answered");
        }

        ResponseContextKey context = GetContextKey(factionId, target.Position);
        if (_lastResponseTimes.TryGetValue(context, out float lastResponseTime) &&
            _simulationTime - lastResponseTime < ReinforcementCooldownSeconds)
        {
            return new FactionDistressResponseResult(
                Accepted: true,
                WaveSpawned: false,
                CooldownBlocked: true,
                AssistedShipCount: assistedCount,
                SpawnedShipCount: 0,
                EncounterId: encounter.Id,
                FactionId: factionId,
                Reason: "faction response cooldown active");
        }

        int activeCount = CountActiveReinforcements(factionId, target.Position);
        int requestCount = Math.Min(ReinforcementWaveSize, Math.Max(0, ActiveReinforcementCap - activeCount));
        IReadOnlyList<NpcShip> spawned = requestCount > 0
            ? _spawnReinforcements(factionId, target.Position, encounter.Id, requestCount) ?? Array.Empty<NpcShip>()
            : Array.Empty<NpcShip>();

        for (int i = 0; i < spawned.Count; i++)
        {
            spawned[i]?.MarkDistressReinforcement(encounter.Id);
        }

        encounter.WaveRequested = true;
        _lastResponseTimes[context] = _simulationTime;

        FactionDistressResponseResult result = new(
            Accepted: true,
            WaveSpawned: spawned.Count > 0,
            CooldownBlocked: false,
            AssistedShipCount: assistedCount,
            SpawnedShipCount: spawned.Count,
            EncounterId: encounter.Id,
            FactionId: factionId,
            Reason: spawned.Count > 0 ? "reinforcements inbound" : "response cap or spawn unavailable");

        if (spawned.Count > 0)
        {
            string displayName = FactionManager.GetFactionDisplayName(factionId);
            _log?.Invoke($"[DISTRESS] {displayName} reinforcements inbound ({spawned.Count}).");
            ResponseGenerated?.Invoke(result);
        }

        return result;
    }

    private int AssignNearbyAssistance(
        NpcShip distressedShip,
        NpcShip? attacker,
        Vector3 attackerPosition,
        FactionDistressDamageSource source)
    {
        float rangeSquared = ExistingAssistanceRange * ExistingAssistanceRange;
        int assistedCount = 0;
        string factionId = FactionManager.NormalizeFactionId(distressedShip.FactionId);

        for (int i = 0; i < _npcShips.Count; i++)
        {
            NpcShip? helper = _npcShips[i];
            if (helper == null || helper == distressedShip || helper.IsDestroyed ||
                !string.Equals(helper.FactionId, factionId, StringComparison.OrdinalIgnoreCase) ||
                Vector3.DistanceSquared(helper.Position, distressedShip.Position) > rangeSquared)
            {
                continue;
            }

            if (source == FactionDistressDamageSource.HostileNpc)
            {
                if (attacker == null || !NpcFactionCombatTargeting.IsValidHostileTarget(helper, attacker, ExistingAssistanceRange))
                    continue;

                if (helper.HasValidFactionCombatTarget())
                {
                    if (helper.FactionCombatTarget == attacker)
                        assistedCount++;
                    continue;
                }

                helper.ClearFactionCombatTarget();
                if (helper.EncounterState == TrafficEncounterState.AttackingPlayer &&
                    !helper.HasValidPlayerTarget(_reputationManager))
                {
                    helper.ClearEncounterState();
                }

                if (helper.HasPlayerTarget && helper.HasValidPlayerTarget(_reputationManager) ||
                    helper.EncounterState == TrafficEncounterState.Fleeing ||
                    helper.EncounterState == TrafficEncounterState.AttackingTrader)
                {
                    continue;
                }

                if (helper.SetFactionCombatTarget(attacker, preserveExistingEncounterState: helper.EncounterState == TrafficEncounterState.InterceptingPirate))
                    assistedCount++;
                continue;
            }

            if (_reputationManager == null ||
                !FactionDispositionEvaluator.IsHostile(helper.FactionId, _reputationManager) ||
                Vector3.DistanceSquared(helper.Position, attackerPosition) > rangeSquared)
            {
                continue;
            }

            if (helper.HasPlayerTarget && helper.HasValidPlayerTarget(_reputationManager))
            {
                assistedCount++;
                continue;
            }

            if (helper.FactionCombatTarget != null && helper.HasValidFactionCombatTarget())
                continue;

            helper.ClearEncounterState();
            helper.SetPlayerTarget(attackerPosition, NpcPlayerTargetReason.FactionDisposition);
            assistedCount++;
        }

        return assistedCount;
    }

    private void CleanupExpiredState()
    {
        if (_observedPlayerDamage.Count > 0)
        {
            List<NpcShip>? staleDamageObservations = null;
            foreach (KeyValuePair<NpcShip, PlayerDamageObservation> entry in _observedPlayerDamage)
            {
                if (!entry.Key.IsDestroyed && _simulationTime - entry.Value.LastSeenTime < EncounterExpirySeconds)
                    continue;

                staleDamageObservations ??= new List<NpcShip>();
                staleDamageObservations.Add(entry.Key);
            }

            if (staleDamageObservations != null)
            {
                for (int i = 0; i < staleDamageObservations.Count; i++)
                    _observedPlayerDamage.Remove(staleDamageObservations[i]);
            }
        }

        if (_encounters.Count > 0)
        {
            List<EncounterKey>? expiredEncounters = null;
            foreach (KeyValuePair<EncounterKey, EncounterRecord> entry in _encounters)
            {
                if (_simulationTime - entry.Value.LastSeenTime < EncounterExpirySeconds)
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

        if (_lastResponseTimes.Count > 0)
        {
            List<ResponseContextKey>? expiredCooldowns = null;
            foreach (KeyValuePair<ResponseContextKey, float> entry in _lastResponseTimes)
            {
                if (_simulationTime - entry.Value < ReinforcementCooldownSeconds)
                    continue;

                expiredCooldowns ??= new List<ResponseContextKey>();
                expiredCooldowns.Add(entry.Key);
            }

            if (expiredCooldowns != null)
            {
                for (int i = 0; i < expiredCooldowns.Count; i++)
                    _lastResponseTimes.Remove(expiredCooldowns[i]);
            }
        }
    }

    private static bool IsSupportedFaction(string? factionId)
    {
        string normalized = FactionManager.NormalizeFactionId(factionId);
        return normalized.Equals(FactionManager.LibertyPolice, StringComparison.OrdinalIgnoreCase) ||
            normalized.Equals(FactionManager.LibertyRogues, StringComparison.OrdinalIgnoreCase);
    }

    private static ResponseContextKey GetContextKey(string factionId, Vector3 position)
    {
        return new(
            factionId,
            (int)Math.Floor(position.X / LocalContextRadius),
            (int)Math.Floor(position.Y / LocalContextRadius),
            (int)Math.Floor(position.Z / LocalContextRadius));
    }

    private static FactionDistressResponseResult Rejected(string reason) =>
        new(
            Accepted: false,
            WaveSpawned: false,
            CooldownBlocked: false,
            AssistedShipCount: 0,
            SpawnedShipCount: 0,
            EncounterId: string.Empty,
            FactionId: string.Empty,
            Reason: reason);
}
