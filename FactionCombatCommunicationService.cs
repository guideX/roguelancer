#nullable enable

using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;

namespace Roguelancer;

public enum FactionCombatCommunicationType
{
    HostileContact,
    DistressRequest,
    DistressResponseAcknowledgement,
    HeavyEscalation,
    HeavyEscalationResponseAcknowledgement,
    AmbientCombatChatter,
    TargetLost,
    PlayerWarning,
    TraderDemandCompliance,
    TraderDemandRefusal,
    TraderDemandDistress
}

public enum FactionCombatCommunicationPriority
{
    Ambient = 0,
    Disengagement = 1,
    Engagement = 2,
    PlayerWarning = 3,
    Distress = 4,
    Escalation = 5
}

public readonly record struct FactionCombatCommunicationRequest(
    FactionCombatCommunicationType Type,
    FactionCombatCommunicationPriority Priority,
    float SimulationTime,
    string FactionId,
    string SpeakerName,
    string TargetName,
    string EncounterId,
    string LineId,
    string Text,
    bool PlayerInvolved,
    FactionCombatDisengagementReason? DisengagementReason);

/// <summary>
/// Presentation-only observer for local faction combat. It accepts narrow
/// reports from authoritative combat services, selects deterministic faction
/// lines, and exposes a small throttled presentation queue. It never changes
/// targets, reputation, damage, spawning, or disengagement state.
/// </summary>
public sealed class FactionCombatCommunicationService
{
    public const int QueueCapacity = 12;
    public const float LocalRelevanceRadius = FactionDistressResponseService.LocalContextRadius;
    public const float PerSpeakerCooldownSeconds = 8f;
    public const float AmbientEncounterCooldownSeconds = 18f;
    public const float GlobalPresentationGapSeconds = 2f;
    public const float ReacquisitionCommunicationSuppressionSeconds = 5f;

    private readonly record struct EngagementKey(NpcShip Source, NpcShip Target);
    private readonly record struct TargetLostKey(NpcShip Source, NpcShip? Target, bool IsPlayerTarget);

    private sealed class EncounterContext
    {
        public Vector3 Position { get; init; }
        public NpcShip? Speaker { get; init; }
        public NpcShip? Opponent { get; init; }
        public string FactionId { get; init; } = string.Empty;
        public bool PlayerInvolved { get; init; }
    }

    private readonly record struct LineDefinition(string Id, string Text);

    private readonly IReadOnlyList<NpcShip> _npcShips;
    private readonly List<FactionCombatCommunicationRequest> _queue = new();
    private readonly HashSet<NpcShip> _registeredShips = new();
    private readonly HashSet<EngagementKey> _engagementsCommunicated = new();
    private readonly HashSet<NpcShip> _playerWarningsCommunicated = new();
    private readonly HashSet<string> _distressRequestsCommunicated = new(StringComparer.Ordinal);
    private readonly HashSet<string> _distressAcknowledgementsCommunicated = new(StringComparer.Ordinal);
    private readonly HashSet<string> _escalationsCommunicated = new(StringComparer.Ordinal);
    private readonly HashSet<string> _escalationAcknowledgementsCommunicated = new(StringComparer.Ordinal);
    private readonly Dictionary<NpcShip, float> _speakerLastMessageTimes = new();
    private readonly Dictionary<NpcShip, FactionCombatCommunicationPriority> _speakerLastPriorities = new();
    private readonly Dictionary<string, float> _ambientEncounterTimes = new(StringComparer.Ordinal);
    private readonly Dictionary<TargetLostKey, float> _targetLostTimes = new();
    private readonly Dictionary<string, EncounterContext> _encounterContexts = new(StringComparer.Ordinal);
    private readonly Action<string>? _log;
    private Vector3 _playerPosition;
    private bool _hasPlayerContext;
    private float _simulationTime;
    private float _nextPresentationTime;
    private int _droppedMessageCount;

    public FactionCombatCommunicationService(
        IReadOnlyList<NpcShip> npcShips,
        Action<string>? log = null)
    {
        _npcShips = npcShips ?? throw new ArgumentNullException(nameof(npcShips));
        _log = log;
    }

    public float SimulationTime => _simulationTime;
    public int QueuedMessageCount => _queue.Count;
    public int DroppedMessageCount => _droppedMessageCount;
    public int RegisteredShipCount => _registeredShips.Count;
    public FactionCombatCommunicationRequest? LastPresentedRequest { get; private set; }

    public void Update(float deltaTime, Ship? playerShip = null)
    {
        float boundedDelta = Math.Max(
            0f,
            float.IsNaN(deltaTime) || float.IsInfinity(deltaTime) ? 0f : deltaTime);
        _simulationTime += boundedDelta;
        if (playerShip != null)
        {
            _playerPosition = playerShip.Position;
            _hasPlayerContext = true;
        }
    }

    public void RegisterShip(NpcShip? ship)
    {
        if (ship == null || !_registeredShips.Add(ship))
            return;

        ship.PlayerTargetAcquired += HandlePlayerTargetAcquired;
    }

    public void UnregisterShip(NpcShip? ship)
    {
        if (ship == null || !_registeredShips.Remove(ship))
            return;

        ship.PlayerTargetAcquired -= HandlePlayerTargetAcquired;
        _speakerLastMessageTimes.Remove(ship);
        _speakerLastPriorities.Remove(ship);
        _playerWarningsCommunicated.Remove(ship);

        List<EngagementKey>? engagementsToRemove = null;
        foreach (EngagementKey key in _engagementsCommunicated)
        {
            if (key.Source != ship && key.Target != ship)
                continue;

            engagementsToRemove ??= new List<EngagementKey>();
            engagementsToRemove.Add(key);
        }

        if (engagementsToRemove != null)
        {
            for (int i = 0; i < engagementsToRemove.Count; i++)
                _engagementsCommunicated.Remove(engagementsToRemove[i]);
        }
    }

    public bool NotifyEngagementAcquired(NpcShip? source, NpcShip? target, Ship? playerShip = null)
    {
        if (source == null || target == null || source == target ||
            source.IsDestroyed || target.IsDestroyed ||
            !IsSupportedFaction(source.FactionId) ||
            !NpcFactionCombatTargeting.IsHostileFactionPair(source.FactionId, target.FactionId))
        {
            return false;
        }

        EngagementKey key = new(source, target);
        if (!_engagementsCommunicated.Add(key))
            return false;

        bool playerInvolved = false;
        if (!IsRelevant(source.Position, target.Position, playerInvolved, playerShip))
            return false;

        return QueueLine(
            FactionCombatCommunicationType.HostileContact,
            FactionCombatCommunicationPriority.Engagement,
            source,
            target.Name,
            BuildEncounterKey(source, target),
            playerInvolved,
            GetLine(source.FactionId, FactionCombatCommunicationType.HostileContact, source, target, string.Empty));
    }

    public bool NotifyDistressRequest(
        FactionDistressResponseResult response,
        NpcShip? affectedShip,
        NpcShip? attacker,
        Ship? playerShip = null)
    {
        if (!response.Accepted || string.IsNullOrWhiteSpace(response.EncounterId) || affectedShip == null ||
            !IsSupportedFaction(response.FactionId))
        {
            return false;
        }

        if (!_distressRequestsCommunicated.Add(response.EncounterId))
            return false;

        bool playerInvolved = attacker == null || playerShip != null;
        if (!IsRelevant(affectedShip.Position, attacker?.Position ?? affectedShip.Position, playerInvolved, playerShip))
            return false;

        _encounterContexts[response.EncounterId] = new EncounterContext
        {
            Position = affectedShip.Position,
            Speaker = affectedShip,
            Opponent = attacker,
            FactionId = FactionManager.NormalizeFactionId(response.FactionId),
            PlayerInvolved = playerInvolved
        };

        return QueueLine(
            FactionCombatCommunicationType.DistressRequest,
            FactionCombatCommunicationPriority.Distress,
            affectedShip,
            attacker?.Name ?? "unknown hostile",
            response.EncounterId,
            playerInvolved,
            GetLine(response.FactionId, FactionCombatCommunicationType.DistressRequest, affectedShip, attacker, response.EncounterId));
    }

    /// <summary>
    /// Presents the bounded trader response generated by the piracy-demand
    /// authority. The communication service remains presentation-only; it
    /// never decides compliance, cargo quantity, or Police escalation.
    /// </summary>
    public bool NotifyPiracyDemandResponse(
        NpcShip? trader,
        PiracyDemandState state,
        int surrenderedQuantity,
        bool distressCalled,
        Ship? playerShip = null)
    {
        if (trader == null || trader.IsDestroyed || state is PiracyDemandState.None or PiracyDemandState.DemandIssued)
            return false;

        bool emitted = false;
        if (state == PiracyDemandState.Complying)
        {
            emitted |= QueueLine(
                FactionCombatCommunicationType.TraderDemandCompliance,
                FactionCombatCommunicationPriority.PlayerWarning,
                trader,
                "freelancer",
                $"piracy:{trader.StableIdentity}:comply",
                playerInvolved: true,
                GetTraderLine(FactionCombatCommunicationType.TraderDemandCompliance, trader, surrenderedQuantity));
        }
        else if (state == PiracyDemandState.RefusingFleeing)
        {
            emitted |= QueueLine(
                FactionCombatCommunicationType.TraderDemandRefusal,
                FactionCombatCommunicationPriority.PlayerWarning,
                trader,
                "freelancer",
                $"piracy:{trader.StableIdentity}:refuse",
                playerInvolved: true,
                GetTraderLine(FactionCombatCommunicationType.TraderDemandRefusal, trader, 0));
        }

        if (distressCalled)
        {
            emitted |= QueueLine(
                FactionCombatCommunicationType.TraderDemandDistress,
                FactionCombatCommunicationPriority.Distress,
                trader,
                "piracy response",
                $"piracy:{trader.StableIdentity}:distress",
                playerInvolved: true,
                GetTraderLine(FactionCombatCommunicationType.TraderDemandDistress, trader, 0));
        }

        return emitted;
    }

    public bool NotifyDistressResponse(FactionDistressResponseResult response)
    {
        bool waveParticipated = response.WaveSpawned && response.SpawnedShipCount > 0;
        bool nearbyAssistanceParticipated = response.AssistedShipCount > 0;
        if ((!waveParticipated && !nearbyAssistanceParticipated) ||
            string.IsNullOrWhiteSpace(response.EncounterId) ||
            !_distressAcknowledgementsCommunicated.Add(response.EncounterId))
        {
            return false;
        }

        EncounterContext? savedContext = TryGetEncounterContext(response.EncounterId, out EncounterContext? context)
            ? context
            : null;
        NpcShip? responder = waveParticipated
            ? FindResponder(response.EncounterId, distress: true)
            : FindNearbyAssistance(savedContext);
        if (responder == null)
        {
            _distressAcknowledgementsCommunicated.Remove(response.EncounterId);
            return false;
        }

        bool playerInvolved = savedContext?.PlayerInvolved == true;
        Vector3 responsePosition = savedContext?.Position ?? responder.Position;
        if (!playerInvolved && !IsRelevant(responsePosition, responder.Position, false, null))
        return false;

        return QueueLine(
            FactionCombatCommunicationType.DistressResponseAcknowledgement,
            FactionCombatCommunicationPriority.Distress,
            responder,
            savedContext?.Speaker?.Name ?? "the patrol",
            response.EncounterId,
            playerInvolved,
            GetLine(response.FactionId, FactionCombatCommunicationType.DistressResponseAcknowledgement, responder, savedContext?.Speaker, response.EncounterId));
    }

    /// <summary>
    /// Reports one successful Phase 34 escalation. The call and the one
    /// responder acknowledgement are emitted from this single semantic event
    /// so the legacy HUD path cannot duplicate it.
    /// </summary>
    public bool NotifyHeavyEscalation(FactionCombatEscalationResult response)
    {
        if (!response.WaveSpawned || response.SpawnedShipCount <= 0 ||
            string.IsNullOrWhiteSpace(response.EncounterId) ||
            !_escalationsCommunicated.Add(response.EncounterId))
        {
            return false;
        }

        NpcShip? responder = FindResponder(response.EncounterId, distress: false);
        if (responder == null)
        {
            _escalationsCommunicated.Remove(response.EncounterId);
            return false;
        }

        EncounterContext? context = TryGetEncounterContext(response.EncounterId, out EncounterContext? savedContext)
            ? savedContext
            : null;
        NpcShip speaker = context?.Speaker != null && !context.Speaker.IsDestroyed
            ? context.Speaker
            : responder;
        Vector3 contextPosition = context?.Position ?? responder.Position;
        if (!IsRelevant(contextPosition, responder.Position, response.PlayerInvolved, null))
            return false;

        bool emitted = QueueLine(
            FactionCombatCommunicationType.HeavyEscalation,
            FactionCombatCommunicationPriority.Escalation,
            speaker,
            "heavy response",
            response.EncounterId,
            response.PlayerInvolved,
            GetLine(response.FactionId, FactionCombatCommunicationType.HeavyEscalation, speaker, responder, response.EncounterId));

        if (!_escalationAcknowledgementsCommunicated.Add(response.EncounterId))
            return emitted;

        bool acknowledgement = QueueLine(
            FactionCombatCommunicationType.HeavyEscalationResponseAcknowledgement,
            FactionCombatCommunicationPriority.Escalation,
            responder,
            speaker.Name,
            response.EncounterId,
            response.PlayerInvolved,
            GetLine(response.FactionId, FactionCombatCommunicationType.HeavyEscalationResponseAcknowledgement, responder, speaker, response.EncounterId));

        if (!acknowledgement)
            _escalationAcknowledgementsCommunicated.Remove(response.EncounterId);

        return emitted || acknowledgement;
    }

    public bool NotifyCombatDamage(NpcShip? attacker, NpcShip? damagedShip, float damage)
    {
        if (attacker == null || damagedShip == null || attacker == damagedShip ||
            attacker.IsDestroyed || damagedShip.IsDestroyed ||
            float.IsNaN(damage) || float.IsInfinity(damage) ||
            damage < FactionDistressResponseService.MeaningfulDamageThreshold ||
            !IsSupportedFaction(attacker.FactionId) ||
            !NpcFactionCombatTargeting.IsHostileFactionPair(attacker.FactionId, damagedShip.FactionId) ||
            (attacker.FactionCombatTarget != damagedShip && damagedShip.FactionCombatTarget != attacker))
        {
            return false;
        }

        string encounterKey = BuildEncounterKey(attacker, damagedShip);
        if (_ambientEncounterTimes.TryGetValue(encounterKey, out float lastTime) &&
            _simulationTime - lastTime < AmbientEncounterCooldownSeconds)
        {
            return false;
        }

        if (!IsRelevant(attacker.Position, damagedShip.Position, false, null))
            return false;

        bool emitted = QueueLine(
            FactionCombatCommunicationType.AmbientCombatChatter,
            FactionCombatCommunicationPriority.Ambient,
            attacker,
            damagedShip.Name,
            encounterKey,
            playerInvolved: false,
            GetLine(attacker.FactionId, FactionCombatCommunicationType.AmbientCombatChatter, attacker, damagedShip, encounterKey));

        if (emitted)
            _ambientEncounterTimes[encounterKey] = _simulationTime;
        return emitted;
    }

    public bool NotifyDisengagement(FactionCombatDisengagementEvent disengagement)
    {
        if (disengagement.Source == null ||
            disengagement.Reason is not (FactionCombatDisengagementReason.TargetNoLongerHostile or
                FactionCombatDisengagementReason.ExcessivePursuitDistance or
                FactionCombatDisengagementReason.StalePursuit or
                FactionCombatDisengagementReason.EncounterExpired))
        {
            return false;
        }

        TargetLostKey key = new(disengagement.Source, disengagement.Target, disengagement.IsPlayerTarget);
        if (_targetLostTimes.TryGetValue(key, out float previous) &&
            _simulationTime - previous < ReacquisitionCommunicationSuppressionSeconds)
        {
            return false;
        }

        bool relevant = disengagement.IsPlayerTarget ||
            IsRelevant(disengagement.Source.Position, disengagement.TargetPosition, false, null);
        if (!relevant)
            return false;

        _targetLostTimes[key] = _simulationTime;
        _engagementsCommunicated.Remove(new EngagementKey(disengagement.Source, disengagement.Target!));
        _playerWarningsCommunicated.Remove(disengagement.Source);

        return QueueLine(
            FactionCombatCommunicationType.TargetLost,
            FactionCombatCommunicationPriority.Disengagement,
            disengagement.Source,
            disengagement.IsPlayerTarget ? "freelancer" : disengagement.Target?.Name ?? "target",
            $"lost:{disengagement.Source.Name}:{disengagement.Target?.Name ?? "player"}",
            disengagement.IsPlayerTarget,
            GetLine(disengagement.Source.FactionId, FactionCombatCommunicationType.TargetLost, disengagement.Source, disengagement.Target, string.Empty),
            disengagement.Reason);
    }

    public bool TryDequeuePresentation(out FactionCombatCommunicationRequest request)
    {
        if (_queue.Count == 0 || _simulationTime < _nextPresentationTime)
        {
            request = default;
            return false;
        }

        int selectedIndex = 0;
        for (int i = 1; i < _queue.Count; i++)
        {
            if (_queue[i].Priority > _queue[selectedIndex].Priority)
                selectedIndex = i;
        }

        request = _queue[selectedIndex];
        _queue.RemoveAt(selectedIndex);
        _nextPresentationTime = _simulationTime + GlobalPresentationGapSeconds;
        LastPresentedRequest = request;
        return true;
    }

    public IReadOnlyList<FactionCombatCommunicationRequest> GetPendingSnapshot() =>
        _queue.ToArray();

    public void Reset()
    {
        _queue.Clear();
        _engagementsCommunicated.Clear();
        _playerWarningsCommunicated.Clear();
        _distressRequestsCommunicated.Clear();
        _distressAcknowledgementsCommunicated.Clear();
        _escalationsCommunicated.Clear();
        _escalationAcknowledgementsCommunicated.Clear();
        _speakerLastMessageTimes.Clear();
        _speakerLastPriorities.Clear();
        _ambientEncounterTimes.Clear();
        _targetLostTimes.Clear();
        _encounterContexts.Clear();
        _simulationTime = 0f;
        _nextPresentationTime = 0f;
        _droppedMessageCount = 0;
        _hasPlayerContext = false;
        LastPresentedRequest = null;
    }

    private void HandlePlayerTargetAcquired(NpcShip source)
    {
        if (_playerWarningsCommunicated.Contains(source))
            return;

        _playerWarningsCommunicated.Add(source);
        bool queued = QueueLine(
            FactionCombatCommunicationType.PlayerWarning,
            FactionCombatCommunicationPriority.PlayerWarning,
            source,
            "freelancer",
            $"player:{source.Name}",
            playerInvolved: true,
            GetLine(source.FactionId, FactionCombatCommunicationType.PlayerWarning, source, null, string.Empty));

        if (!queued)
            _playerWarningsCommunicated.Remove(source);
    }

    private bool QueueLine(
        FactionCombatCommunicationType type,
        FactionCombatCommunicationPriority priority,
        NpcShip speaker,
        string targetName,
        string encounterId,
        bool playerInvolved,
        LineDefinition line,
        FactionCombatDisengagementReason? disengagementReason = null)
    {
        if (speaker == null || string.IsNullOrWhiteSpace(line.Text) ||
            !CanSpeakerTalk(speaker, priority))
        {
            return false;
        }

        FactionCombatCommunicationRequest request = new(
            type,
            priority,
            _simulationTime,
            FactionManager.NormalizeFactionId(speaker.FactionId),
            GetSpeakerName(speaker),
            targetName ?? string.Empty,
            encounterId ?? string.Empty,
            line.Id,
            $"{GetSpeakerName(speaker)}: {line.Text}",
            playerInvolved,
            disengagementReason);

        if (_queue.Count >= QueueCapacity)
        {
            int lowestIndex = 0;
            for (int i = 1; i < _queue.Count; i++)
            {
                if (_queue[i].Priority < _queue[lowestIndex].Priority)
                    lowestIndex = i;
            }

            if (priority < _queue[lowestIndex].Priority)
            {
                _droppedMessageCount++;
                return false;
            }

            _queue.RemoveAt(lowestIndex);
            _droppedMessageCount++;
        }

        _queue.Add(request);
        _speakerLastMessageTimes[speaker] = _simulationTime;
        _speakerLastPriorities[speaker] = priority;
        try
        {
            _log?.Invoke($"[COMBAT RADIO] {request.Text}");
        }
        catch
        {
            // Diagnostic logging is optional and cannot turn presentation
            // failure into gameplay failure.
        }
        return true;
    }

    private bool CanSpeakerTalk(NpcShip speaker, FactionCombatCommunicationPriority priority)
    {
        if (!_speakerLastMessageTimes.TryGetValue(speaker, out float lastTime))
            return true;

        if (_simulationTime - lastTime >= PerSpeakerCooldownSeconds)
            return true;

        // A newly accepted distress/escalation event may preempt a lower
        // value engagement/chatter line from the same speaker. Equal or
        // lower-value lines still obey the full speaker cooldown.
        return priority >= FactionCombatCommunicationPriority.Distress &&
            _speakerLastPriorities.TryGetValue(speaker, out FactionCombatCommunicationPriority previousPriority) &&
            priority > previousPriority;
    }

    private bool IsRelevant(Vector3 sourcePosition, Vector3 targetPosition, bool playerInvolved, Ship? playerShip)
    {
        if (playerInvolved)
            return true;

        Vector3 playerPosition = playerShip?.Position ?? _playerPosition;
        if (playerShip != null)
            _hasPlayerContext = true;

        if (!_hasPlayerContext)
            return false;

        float radiusSquared = LocalRelevanceRadius * LocalRelevanceRadius;
        return Vector3.DistanceSquared(playerPosition, sourcePosition) <= radiusSquared ||
            Vector3.DistanceSquared(playerPosition, targetPosition) <= radiusSquared;
    }

    private NpcShip? FindResponder(string encounterId, bool distress)
    {
        for (int i = 0; i < _npcShips.Count; i++)
        {
            NpcShip? ship = _npcShips[i];
            if (ship == null || ship.IsDestroyed)
                continue;

            if (distress && ship.IsDistressReinforcement &&
                string.Equals(ship.DistressReinforcementEncounterId, encounterId, StringComparison.Ordinal))
                return ship;

            if (!distress && ship.IsEscalationReinforcement &&
                string.Equals(ship.EscalationReinforcementEncounterId, encounterId, StringComparison.Ordinal))
                return ship;
        }

        return null;
    }

    private NpcShip? FindNearbyAssistance(EncounterContext? context)
    {
        if (context?.Speaker == null)
            return null;

        string factionId = FactionManager.NormalizeFactionId(context.Speaker.FactionId);
        for (int i = 0; i < _npcShips.Count; i++)
        {
            NpcShip? helper = _npcShips[i];
            if (helper == null || helper == context.Speaker || helper.IsDestroyed ||
                !string.Equals(FactionManager.NormalizeFactionId(helper.FactionId), factionId, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (context.Opponent != null && helper.FactionCombatTarget == context.Opponent)
                return helper;

            if (context.PlayerInvolved && helper.HasPlayerTarget)
                return helper;
        }

        return null;
    }

    private bool TryGetEncounterContext(string encounterId, out EncounterContext? context) =>
        _encounterContexts.TryGetValue(encounterId, out context);

    private static string GetSpeakerName(NpcShip speaker) =>
        string.IsNullOrWhiteSpace(speaker.Name)
            ? FactionManager.GetFactionDisplayName(speaker.FactionId)
            : speaker.Name;

    private static string BuildEncounterKey(NpcShip first, NpcShip second)
    {
        string firstName = GetSpeakerName(first);
        string secondName = GetSpeakerName(second);
        return string.Compare(firstName, secondName, StringComparison.OrdinalIgnoreCase) <= 0
            ? $"combat:{firstName}:{secondName}"
            : $"combat:{secondName}:{firstName}";
    }

    private static bool IsSupportedFaction(string? factionId)
    {
        string normalized = FactionManager.NormalizeFactionId(factionId);
        return normalized.Equals(FactionManager.LibertyPolice, StringComparison.OrdinalIgnoreCase) ||
            normalized.Equals(FactionManager.LibertyRogues, StringComparison.OrdinalIgnoreCase);
    }

    private static LineDefinition GetLine(
        string factionId,
        FactionCombatCommunicationType type,
        NpcShip speaker,
        NpcShip? target,
        string encounterId)
    {
        bool police = FactionManager.NormalizeFactionId(factionId)
            .Equals(FactionManager.LibertyPolice, StringComparison.OrdinalIgnoreCase);

        LineDefinition[] pool = police ? GetPoliceLines(type) : GetRogueLines(type);
        if (pool.Length == 0)
            return new LineDefinition("combat_radio_unavailable", string.Empty);

        string seed = $"{factionId}|{type}|{GetSpeakerName(speaker)}|{GetSpeakerName(target ?? speaker)}|{encounterId}";
        int index = StableIndex(seed, pool.Length);
        return pool[index];
    }

    private static LineDefinition GetTraderLine(
        FactionCombatCommunicationType type,
        NpcShip trader,
        int surrenderedQuantity)
    {
        LineDefinition[] pool = type switch
        {
            FactionCombatCommunicationType.TraderDemandCompliance => new[]
            {
                new LineDefinition("trader_comply_01", "All right! Take the shipment and leave us alone."),
                new LineDefinition("trader_comply_02", "Fine. We're dumping the cargo. Don't fire."),
            },
            FactionCombatCommunicationType.TraderDemandRefusal => new[]
            {
                new LineDefinition("trader_refuse_01", "Negative. We are not surrendering our cargo."),
                new LineDefinition("trader_refuse_02", "No chance. We're making a run for it."),
            },
            FactionCombatCommunicationType.TraderDemandDistress => new[]
            {
                new LineDefinition("trader_distress_01", "Mayday! We are under pirate attack!"),
                new LineDefinition("trader_distress_02", "Police, respond! Pirate extortion in progress!"),
            },
            _ => Array.Empty<LineDefinition>()
        };

        if (pool.Length == 0)
            return new LineDefinition("trader_radio_unavailable", string.Empty);

        string seed = $"trader|{type}|{trader?.StableIdentity}|{surrenderedQuantity}";
        return pool[StableIndex(seed, pool.Length)];
    }

    private static LineDefinition[] GetPoliceLines(FactionCombatCommunicationType type) => type switch
    {
        FactionCombatCommunicationType.HostileContact => new[]
        {
            new LineDefinition("police_engage_01", "Rogue vessel detected. Engaging."),
            new LineDefinition("police_engage_02", "Confirmed hostile. Moving to intercept."),
            new LineDefinition("police_engage_03", "Hostile contact identified.")
        },
        FactionCombatCommunicationType.PlayerWarning => new[]
        {
            new LineDefinition("police_player_warning_01", "Pilot, cut your engines and stand down."),
            new LineDefinition("police_player_warning_02", "Hostile pilot identified. Engaging.")
        },
        FactionCombatCommunicationType.DistressRequest => new[]
        {
            new LineDefinition("police_distress_01", "Officer under attack. Requesting assistance."),
            new LineDefinition("police_distress_02", "Hostile engagement. Requesting backup.")
        },
        FactionCombatCommunicationType.DistressResponseAcknowledgement => new[]
        {
            new LineDefinition("police_response_01", "Copy. We're inbound."),
            new LineDefinition("police_response_02", "Reinforcements responding.")
        },
        FactionCombatCommunicationType.HeavyEscalation => new[]
        {
            new LineDefinition("police_escalation_01", "Situation escalating. Heavy units inbound."),
            new LineDefinition("police_escalation_02", "Serious engagement confirmed. Heavy response dispatched.")
        },
        FactionCombatCommunicationType.HeavyEscalationResponseAcknowledgement => new[]
        {
            new LineDefinition("police_heavy_ack_01", "Responding to the engagement."),
            new LineDefinition("police_heavy_ack_02", "We see the fight. Moving in.")
        },
        FactionCombatCommunicationType.AmbientCombatChatter => new[]
        {
            new LineDefinition("police_chatter_01", "Maintain pursuit."),
            new LineDefinition("police_chatter_02", "Target remains hostile."),
            new LineDefinition("police_chatter_03", "Weapons on target.")
        },
        FactionCombatCommunicationType.TargetLost => new[]
        {
            new LineDefinition("police_target_lost_01", "Target lost. Returning to patrol."),
            new LineDefinition("police_target_lost_02", "Pursuit terminated.")
        },
        _ => Array.Empty<LineDefinition>()
    };

    private static LineDefinition[] GetRogueLines(FactionCombatCommunicationType type) => type switch
    {
        FactionCombatCommunicationType.HostileContact => new[]
        {
            new LineDefinition("rogue_engage_01", "Got ourselves a target."),
            new LineDefinition("rogue_engage_02", "Police contact. Weapons free."),
            new LineDefinition("rogue_engage_03", "Let's take them apart.")
        },
        FactionCombatCommunicationType.PlayerWarning => new[]
        {
            new LineDefinition("rogue_player_warning_01", "You picked the wrong fight."),
            new LineDefinition("rogue_player_warning_02", "Let's get this freelancer.")
        },
        FactionCombatCommunicationType.DistressRequest => new[]
        {
            new LineDefinition("rogue_distress_01", "We need fighters over here!"),
            new LineDefinition("rogue_distress_02", "Got trouble. Send backup!")
        },
        FactionCombatCommunicationType.DistressResponseAcknowledgement => new[]
        {
            new LineDefinition("rogue_response_01", "On our way."),
            new LineDefinition("rogue_response_02", "Hang on, we're coming.")
        },
        FactionCombatCommunicationType.HeavyEscalation => new[]
        {
            new LineDefinition("rogue_escalation_01", "Send the heavy fighters."),
            new LineDefinition("rogue_escalation_02", "This one's getting ugly. More ships inbound.")
        },
        FactionCombatCommunicationType.HeavyEscalationResponseAcknowledgement => new[]
        {
            new LineDefinition("rogue_heavy_ack_01", "We see the fight."),
            new LineDefinition("rogue_heavy_ack_02", "Heavy fighter responding.")
        },
        FactionCombatCommunicationType.AmbientCombatChatter => new[]
        {
            new LineDefinition("rogue_chatter_01", "Keep on them."),
            new LineDefinition("rogue_chatter_02", "They're not getting away."),
            new LineDefinition("rogue_chatter_03", "Hit them again.")
        },
        FactionCombatCommunicationType.TargetLost => new[]
        {
            new LineDefinition("rogue_target_lost_01", "Forget it. They're gone."),
            new LineDefinition("rogue_target_lost_02", "Lost the target.")
        },
        _ => Array.Empty<LineDefinition>()
    };

    private static int StableIndex(string value, int count)
    {
        unchecked
        {
            uint hash = 2166136261u;
            for (int i = 0; i < value.Length; i++)
            {
                hash ^= value[i];
                hash *= 16777619u;
            }

            return (int)(hash % (uint)count);
        }
    }
}
