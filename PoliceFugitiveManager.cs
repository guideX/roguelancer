#nullable enable

using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;

namespace Roguelancer;

public enum PoliceFugitiveState
{
    None,
    Pursued,
    Evading
}

public enum PoliceHeatLevel
{
    None = 0,
    Pursuit = 1,
    HotPursuit = 2
}

/// <summary>
/// Owns the one bounded current-system fugitive incident. This is an
/// incident state machine, not a second combat AI: TrafficManager remains the
/// owner of NPC movement/targeting and the existing disengagement, distress,
/// escalation, and communication services remain authoritative around it.
/// </summary>
public sealed class PoliceFugitiveManager
{
    public const string PoliceFugitiveHostilityReason = "police fugitive pursuit";
    public const float Heat1AcquisitionRadius = 10_000f;
    public const float Heat2AcquisitionRadius = 14_000f;
    public const float Heat1EscapeDurationSeconds = 15f;
    public const float Heat2EscapeDurationSeconds = 25f;
    public const float ContactLossGraceSeconds = 0.25f;
    public const float AbsoluteIncidentBoundSeconds = 180f;
    public const float HostilityRefreshThresholdSeconds = 8f;

    private readonly ReputationManager _reputationManager;
    private readonly HashSet<NpcShip> _pursuitTargets = new();
    private readonly Dictionary<NpcShip, int> _observedPlayerDamage = new();
    private float _escapeTimer;
    private float _contactLossTimer;
    private float _absoluteIncidentTimer;
    private float _presentationTimer;
    private bool _ownsTemporaryHostility;
    private int _activeContactCount;
    private string _presentationText = string.Empty;

    public PoliceFugitiveManager(ReputationManager reputationManager, Action<string>? presentNotification = null)
    {
        _reputationManager = reputationManager ?? throw new ArgumentNullException(nameof(reputationManager));
        PresentNotification = presentNotification;
    }

    public Action<string>? PresentNotification { get; set; }
    public PoliceFugitiveState State { get; private set; } = PoliceFugitiveState.None;
    public PoliceHeatLevel Heat { get; private set; } = PoliceHeatLevel.None;
    public bool IsActive => State != PoliceFugitiveState.None;
    public bool IsPursued => State == PoliceFugitiveState.Pursued;
    public bool IsEvading => State == PoliceFugitiveState.Evading;
    public int ActiveContactCount => _activeContactCount;
    public int ActivePursuerCount => _activeContactCount;
    public float EscapeProgressSeconds => Math.Max(0f, _escapeTimer);
    public float EscapeDurationSeconds => Heat == PoliceHeatLevel.HotPursuit
        ? Heat2EscapeDurationSeconds
        : Heat1EscapeDurationSeconds;
    public float EscapeRemainingSeconds => IsEvading
        ? Math.Max(0f, EscapeDurationSeconds - _escapeTimer)
        : EscapeDurationSeconds;
    public float AbsoluteIncidentRemainingSeconds => Math.Max(0f, _absoluteIncidentTimer);
    public string StatusText
    {
        get
        {
            if (_presentationTimer > 0f && State == PoliceFugitiveState.Pursued &&
                string.Equals(_presentationText, "POLICE REACQUIRED", StringComparison.Ordinal))
                return _presentationText;

            if (State == PoliceFugitiveState.None && _presentationTimer > 0f && !string.IsNullOrWhiteSpace(_presentationText))
                return _presentationText;

            return State switch
            {
                PoliceFugitiveState.Pursued when Heat == PoliceHeatLevel.HotPursuit => "LIBERTY POLICE — HOT PURSUIT",
                PoliceFugitiveState.Pursued => "LIBERTY POLICE PURSUIT",
                PoliceFugitiveState.Evading => $"POLICE CONTACT LOST | Stay clear: {EscapeRemainingSeconds:0.0}s",
                _ => string.Empty
            };
        }
    }

    public float AcquisitionRadius => Heat == PoliceHeatLevel.HotPursuit
        ? Heat2AcquisitionRadius
        : Heat1AcquisitionRadius;

    /// <summary>
    /// Opens or refreshes the one incident. Refusal/timeout/flight call this
    /// seam; it deliberately does not mutate permanent reputation.
    /// </summary>
    public bool BeginPursuit(Ship? playerShip = null, string reason = "lawful enforcement flight", Action<string>? log = null)
    {
        if (playerShip?.Hull?.IsDestroyed == true)
            return false;

        bool wasActive = IsActive;
        if (!wasActive)
        {
            State = PoliceFugitiveState.Pursued;
            Heat = PoliceHeatLevel.Pursuit;
            _escapeTimer = 0f;
            _contactLossTimer = 0f;
            _absoluteIncidentTimer = AbsoluteIncidentBoundSeconds;
            _activeContactCount = 0;
            _ownsTemporaryHostility = true;
            _reputationManager.TemporaryHostility.RecordHostileAction(
                FactionManager.LibertyPolice,
                PoliceFugitiveHostilityReason,
                TemporaryHostilityManager.MaximumDurationSeconds);
            Present("Suspect is fleeing lawful enforcement.");
            log?.Invoke($"[POLICE FUGITIVE] Heat 1 started ({reason}).");
            return true;
        }

        _absoluteIncidentTimer = AbsoluteIncidentBoundSeconds;
        EnsureTemporaryHostility();
        log?.Invoke($"[POLICE FUGITIVE] Pursuit refreshed ({reason}).");
        return false;
    }

    /// <summary>
    /// Consumes the existing monotonic player-damage attribution. NPC-only
    /// damage never reaches this path, and repeated frame sweeps are ignored.
    /// </summary>
    public bool NotifyPlayerDamage(NpcShip? damagedShip, Ship? playerShip = null, Action<string>? log = null)
    {
        if (damagedShip == null ||
            !string.Equals(FactionManager.NormalizeFactionId(damagedShip.FactionId), FactionManager.LibertyPolice, StringComparison.OrdinalIgnoreCase) ||
            !damagedShip.WasDamagedByPlayer || damagedShip.PlayerDamageSequence <= 0)
        {
            return false;
        }

        if (_observedPlayerDamage.TryGetValue(damagedShip, out int sequence) &&
            sequence >= damagedShip.PlayerDamageSequence)
        {
            return false;
        }

        _observedPlayerDamage[damagedShip] = damagedShip.PlayerDamageSequence;
        if (!IsActive)
        {
            BeginPursuit(playerShip, "player attack on Liberty Police", log);
            return true;
        }

        bool escalated = Heat != PoliceHeatLevel.HotPursuit;
        Heat = PoliceHeatLevel.HotPursuit;
        State = PoliceFugitiveState.Pursued;
        _escapeTimer = 0f;
        _contactLossTimer = 0f;
        _absoluteIncidentTimer = AbsoluteIncidentBoundSeconds;
        EnsureTemporaryHostility();
        if (escalated)
        {
            Present("Officer under fire. Escalating pursuit.");
            log?.Invoke("[POLICE FUGITIVE] Heat escalated to 2: hot pursuit.");
        }

        return true;
    }

    /// <summary>
    /// Runs after normal traffic disengagement and before existing faction
    /// combat acquisition. This lets ordinary Liberty Police traffic acquire
    /// the player without bypassing higher-priority NPC objectives.
    /// </summary>
    public void Update(
        float deltaSeconds,
        Ship? playerShip,
        IReadOnlyList<NpcShip>? npcs,
        Action<string>? log = null)
    {
        float delta = NormalizeDelta(deltaSeconds);
        _presentationTimer = Math.Max(0f, _presentationTimer - delta);

        if (!IsActive)
            return;

        if (playerShip?.Hull?.IsDestroyed == true || playerShip == null)
        {
            Reset(log, "player teardown/death");
            return;
        }

        EnsureTemporaryHostility();
        _absoluteIncidentTimer = Math.Max(0f, _absoluteIncidentTimer - delta);
        AcquireNearbyPolice(playerShip, npcs);
        _activeContactCount = CountActiveContacts(playerShip, npcs);

        if (_activeContactCount > 0)
        {
            _contactLossTimer = 0f;
            if (State == PoliceFugitiveState.Evading)
            {
                State = PoliceFugitiveState.Pursued;
                _escapeTimer = 0f;
                Present("POLICE REACQUIRED");
                log?.Invoke("[POLICE FUGITIVE] Fugitive reacquired; escape progress reset.");
            }

            // The absolute bound is a safety net for a quiet incident. Active
            // contact must never silently clear the pursuit while officers are
            // still engaging the player.
            return;
        }

        _contactLossTimer += delta;
        if (_contactLossTimer < ContactLossGraceSeconds)
            return;

        if (State == PoliceFugitiveState.Pursued)
        {
            State = PoliceFugitiveState.Evading;
            _escapeTimer = 0f;
            Present("POLICE CONTACT LOST");
            log?.Invoke("[POLICE FUGITIVE] All Police contact lost; evasion timer started.");
        }

        _escapeTimer += delta;
        if (_escapeTimer >= EscapeDurationSeconds)
        {
            ResolveEscape(log);
            return;
        }

        // This is intentionally only a final safety valve after contact is
        // gone. Normal gameplay always resolves through the continuous
        // contact-free timer above.
        if (_absoluteIncidentTimer <= 0f)
            ResolveEscape(log);
    }

    public bool IsValidPoliceContact(NpcShip? police, Ship? playerShip)
    {
        if (!IsActive || police == null || playerShip == null || police.IsDestroyed ||
            !IsLibertyPolice(police) || !police.HasPlayerTarget ||
            !police.HasValidPlayerTarget(_reputationManager))
        {
            return false;
        }

        float radius = AcquisitionRadius;
        return Vector3.DistanceSquared(police.Position, playerShip.Position) <= radius * radius;
    }

    public void ResolveEscape(Action<string>? log = null)
    {
        if (!IsActive)
            return;

        ClearPursuitTargets();
        if (_ownsTemporaryHostility)
            _reputationManager.TemporaryHostility.Clear(FactionManager.LibertyPolice);

        State = PoliceFugitiveState.None;
        Heat = PoliceHeatLevel.None;
        _escapeTimer = 0f;
        _contactLossTimer = 0f;
        _absoluteIncidentTimer = 0f;
        _activeContactCount = 0;
        _ownsTemporaryHostility = false;
        _observedPlayerDamage.Clear();
        Present("PURSUIT EVADED");
        log?.Invoke("[POLICE FUGITIVE] Pursuit evaded; transient incident cleared.");
    }

    public void Reset(Action<string>? log = null, string reason = "reset")
    {
        ClearPursuitTargets();
        if (_ownsTemporaryHostility)
            _reputationManager.TemporaryHostility.Clear(FactionManager.LibertyPolice);

        State = PoliceFugitiveState.None;
        Heat = PoliceHeatLevel.None;
        _escapeTimer = 0f;
        _contactLossTimer = 0f;
        _absoluteIncidentTimer = 0f;
        _activeContactCount = 0;
        _ownsTemporaryHostility = false;
        _observedPlayerDamage.Clear();
        _pursuitTargets.Clear();
        _presentationTimer = 0f;
        _presentationText = string.Empty;
        log?.Invoke($"[POLICE FUGITIVE] Cleared ({reason}).");
    }

    private void AcquireNearbyPolice(Ship playerShip, IReadOnlyList<NpcShip>? npcs)
    {
        if (npcs == null || npcs.Count == 0)
            return;

        float radius = AcquisitionRadius;
        float radiusSquared = radius * radius;
        for (int i = 0; i < npcs.Count; i++)
        {
            NpcShip? police = npcs[i];
            if (police == null || police.IsDestroyed || !IsLibertyPolice(police) ||
                police.IsTradeLaneTransit || police.IsMissionHoldPosition ||
                Vector3.DistanceSquared(police.Position, playerShip.Position) > radiusSquared)
            {
                continue;
            }

            if (police.HasPlayerTarget)
            {
                if (police.HasValidPlayerTarget(_reputationManager))
                {
                    police.SetPlayerTarget(playerShip.Position, police.PlayerTargetReason);
                    if (police.PlayerTargetReason == NpcPlayerTargetReason.FugitivePursuit)
                        _pursuitTargets.Add(police);
                }

                continue;
            }

            // Existing faction combat, trader attacks, fleeing, and pirate
            // interceptions retain priority over a newly noticed fugitive.
            if (police.IsTrafficEngaged)
                continue;

            police.SetPlayerTarget(playerShip.Position, NpcPlayerTargetReason.FugitivePursuit);
            _pursuitTargets.Add(police);
        }
    }

    private int CountActiveContacts(Ship playerShip, IReadOnlyList<NpcShip>? npcs)
    {
        if (npcs == null || npcs.Count == 0)
            return 0;

        int count = 0;
        for (int i = 0; i < npcs.Count; i++)
        {
            if (IsValidPoliceContact(npcs[i], playerShip))
                count++;
        }

        return count;
    }

    private void EnsureTemporaryHostility()
    {
        if (_reputationManager.IsHostile(FactionManager.LibertyPolice))
            return;

        if (!_reputationManager.TemporaryHostility.HasReason(
                FactionManager.LibertyPolice,
                PoliceFugitiveHostilityReason) ||
            _reputationManager.GetTemporaryHostilityRemainingSeconds(FactionManager.LibertyPolice) <= HostilityRefreshThresholdSeconds)
        {
            _reputationManager.TemporaryHostility.RecordHostileAction(
                FactionManager.LibertyPolice,
                PoliceFugitiveHostilityReason,
                TemporaryHostilityManager.MaximumDurationSeconds);
        }
    }

    private void ClearPursuitTargets()
    {
        foreach (NpcShip police in _pursuitTargets)
        {
            if (police == null || police.IsDestroyed || !police.HasPlayerTarget ||
                police.PlayerTargetReason != NpcPlayerTargetReason.FugitivePursuit)
            {
                continue;
            }

            police.ClearEncounterState();
        }

        _pursuitTargets.Clear();
    }

    private void Present(string text)
    {
        _presentationText = text ?? string.Empty;
        _presentationTimer = 2f;
        try
        {
            if (!string.IsNullOrWhiteSpace(_presentationText))
                PresentNotification?.Invoke(_presentationText);
        }
        catch
        {
            // HUD/notification presentation must never block pursuit state.
        }
    }

    private static bool IsLibertyPolice(NpcShip ship) =>
        string.Equals(
            FactionManager.NormalizeFactionId(ship?.FactionId),
            FactionManager.LibertyPolice,
            StringComparison.OrdinalIgnoreCase);

    private static float NormalizeDelta(float deltaSeconds) =>
        Math.Max(0f, float.IsNaN(deltaSeconds) || float.IsInfinity(deltaSeconds) ? 0f : deltaSeconds);
}
