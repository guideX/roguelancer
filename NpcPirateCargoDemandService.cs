using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Roguelancer;

public enum NpcPirateDemandState
{
    None,
    DemandPresented,
    Complying,
    Refusing,
    Resolved
}

/// <summary>
/// One bounded commodity stack carried by a demanding pirate after the player
/// complies. Cargo taken through a demand is stolen by definition, so the haul
/// stores only the physical commodity and quantity. No original-owner
/// provenance, theft timestamp, victim identity, or theft count is retained.
/// </summary>
public sealed class PirateDemandHaulEntry
{
    public string CommodityId { get; init; } = string.Empty;
    public string CommodityName { get; init; } = string.Empty;
    public int Quantity { get; internal set; }

    /// <summary>
    /// Pirate-held demand cargo is always stolen. This is a provenance fact,
    /// not a legality classification; contraband remains contraband.
    /// </summary>
    public CargoProvenance Provenance => CargoProvenance.Stolen;
}

public sealed class NpcPirateCargoDemandResult
{
    public NpcPirateDemandState State { get; init; }
    public NpcShip Demander { get; init; }
    public string DemanderIdentity { get; init; } = string.Empty;
    public IReadOnlyDictionary<string, int> DemandedQuantities { get; init; }
        = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
    public int SurrenderedQuantity { get; init; }
    public string ResolutionReason { get; init; } = string.Empty;
    public Ship PlayerShip { get; init; }
}

/// <summary>
/// Owns the bounded, transient NPC-to-player piracy-demand interaction. An
/// eligible Liberty Rogue confronts the player and demands real player-owned
/// cargo. Compliance removes the exact requested quantities through the
/// existing CargoHold authority and records them in a bounded pirate haul
/// that drops as physical stolen pods when the demander is destroyed.
/// Refusal or timeout escalates through existing NPC combat targeting.
///
/// This service adds no parallel inventory, mission, criminal-state, or
/// combat system. It reuses CargoHold for player cargo, ReputationManager
/// for relationship gating, TrafficManager for flee behavior, and
/// LootManager for physical haul drops.
/// </summary>
public sealed class NpcPirateCargoDemandService
{
    public const float DemandRange = 3000f;
    public const float DemandControlRange = 4500f;
    public const float ResponseWindowSeconds = 5f;
    public const float PlayerCooldownSeconds = 30f;
    public const int MinimumCommodityTypes = 1;
    public const int MaximumCommodityTypes = 3;
    public const int MinimumDemandPercentage = 25;
    public const int MaximumDemandPercentage = 75;
    public const Keys ComplyKey = Keys.Enter;
    public const Keys RefuseKey = Keys.N;

    private sealed class ActiveDemand
    {
        public NpcShip Demander { get; init; }
        public string DemanderIdentity { get; init; } = string.Empty;
        public Ship PlayerShip { get; init; }
        public float RemainingSeconds { get; set; }
        public Dictionary<string, int> RequestedQuantities { get; init; }
            = new(StringComparer.OrdinalIgnoreCase);
    }

    private readonly IReadOnlyList<NpcShip> _npcShips;
    private readonly ReputationManager _reputationManager;
    private readonly Func<Ship, bool> _isPlayerDocked;
    private readonly Func<bool> _isPoliceInteractionActive;
    private readonly Func<NpcShip, bool> _isMissionOwned;
    private readonly Func<NpcShip, Vector3, bool> _markFleeing;
    private readonly Func<NpcShip, string, int, int> _spawnStolenCargo;
    private readonly Action<string> _log;
    private readonly Action<string> _notify;
    private readonly Dictionary<NpcShip, List<PirateDemandHaulEntry>> _pirateHauls = new();
    private readonly HashSet<string> _resolvedDemanderIdentities = new(StringComparer.Ordinal);
    private ActiveDemand _activeDemand;
    private float _playerCooldownRemainingSeconds;
    private float _presentationRemainingSeconds;
    private string _presentationText = string.Empty;

    public NpcPirateCargoDemandService(
        IReadOnlyList<NpcShip> npcShips,
        ReputationManager reputationManager,
        Func<Ship, bool> isPlayerDocked = null,
        Func<bool> isPoliceInteractionActive = null,
        Func<NpcShip, bool> isMissionOwned = null,
        Func<NpcShip, Vector3, bool> markFleeing = null,
        Func<NpcShip, string, int, int> spawnStolenCargo = null,
        Action<string> log = null,
        Action<string> notify = null)
    {
        _npcShips = npcShips ?? throw new ArgumentNullException(nameof(npcShips));
        _reputationManager = reputationManager ?? throw new ArgumentNullException(nameof(reputationManager));
        _isPlayerDocked = isPlayerDocked ?? (_ => false);
        _isPoliceInteractionActive = isPoliceInteractionActive ?? (() => false);
        _isMissionOwned = isMissionOwned ?? (_ => false);
        _markFleeing = markFleeing;
        _spawnStolenCargo = spawnStolenCargo;
        _log = log;
        _notify = notify;
    }

    public NpcPirateDemandState CurrentState => _activeDemand == null
        ? NpcPirateDemandState.None
        : NpcPirateDemandState.DemandPresented;
    public bool HasActiveDemand => _activeDemand != null;
    public NpcShip ActiveDemander => _activeDemand?.Demander;
    public float ResponseRemainingSeconds => Math.Max(0f, _activeDemand?.RemainingSeconds ?? 0f);
    public string HudText => _presentationRemainingSeconds > 0f ? _presentationText : string.Empty;
    public NpcPirateCargoDemandResult LastResult { get; private set; }
    public float PlayerCooldownRemainingSeconds => Math.Max(0f, _playerCooldownRemainingSeconds);
    public IReadOnlyDictionary<string, int> ActiveDemandQuantities => _activeDemand?.RequestedQuantities
        ?? new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
    public event Action<NpcPirateCargoDemandResult> DemandResolved;

    /// <summary>
    /// Attempts to initiate a cargo demand against the player. Called by the
    /// game update loop; all eligibility, range, relationship, and cooldown
    /// gates are evaluated here. Cargo is enumerated only when a demand is
    /// actually created.
    /// </summary>
    public bool TryInitiateDemand(Ship playerShip, out string failureReason)
    {
        failureReason = string.Empty;
        if (_activeDemand != null)
        {
            failureReason = "another pirate cargo demand is already active";
            return false;
        }

        if (_playerCooldownRemainingSeconds > 0f)
        {
            failureReason = "player piracy-demand cooldown is active";
            return false;
        }

        if (!IsPlayerEligible(playerShip, out string playerFailure))
        {
            failureReason = playerFailure;
            return false;
        }

        NpcShip demander = SelectEligibleDemander(playerShip, out string demanderFailure);
        if (demander == null)
        {
            failureReason = demanderFailure;
            return false;
        }

        Dictionary<string, int> eligibleQuantities = GetEligiblePlayerQuantities(playerShip);
        if (eligibleQuantities.Count == 0)
        {
            failureReason = "player has no eligible cargo to demand";
            return false;
        }

        List<(Commodity Commodity, int Quantity)> selected = SelectDemandQuantities(demander, eligibleQuantities);
        if (selected.Count == 0)
        {
            failureReason = "no demandable cargo could be selected";
            return false;
        }

        string identity = GetDemanderIdentity(demander);
        _activeDemand = new ActiveDemand
        {
            Demander = demander,
            DemanderIdentity = identity,
            PlayerShip = playerShip,
            RemainingSeconds = ResponseWindowSeconds,
            RequestedQuantities = selected.ToDictionary(
                entry => entry.Commodity.Name,
                entry => entry.Quantity,
                StringComparer.OrdinalIgnoreCase)
        };

        string demandText = FormatDemandText(selected);
        SetPresentation(demandText, ResponseWindowSeconds + 2f);
        _notify?.Invoke(demandText);
        _log?.Invoke($"[NPC PIRACY] {demander.Name} ({identity}) demands cargo: {demandText}");
        return true;
    }

    /// <summary>
    /// Advances the active demand. Counts down the response window, revalidates
    /// the demander and player, and resolves timeout as refusal.
    /// </summary>
    public void Update(float deltaSeconds, Ship playerShip)
    {
        float delta = NormalizeDelta(deltaSeconds);
        _presentationRemainingSeconds = Math.Max(0f, _presentationRemainingSeconds - delta);
        _playerCooldownRemainingSeconds = Math.Max(0f, _playerCooldownRemainingSeconds - delta);
        if (_activeDemand == null)
            return;

        ActiveDemand demand = _activeDemand;
        NpcShip demander = demand.Demander;
        if (demander == null || demander.IsDestroyed ||
            !string.Equals(GetDemanderIdentity(demander), demand.DemanderIdentity, StringComparison.Ordinal) ||
            !_npcShips.Contains(demander))
        {
            ResolveDestroyedOrInvalid(demand, "demander destroyed or despawned");
            return;
        }

        if (demander.IsTradeLaneTransit || demander.IsTrafficEngaged ||
            demander.FactionCombatTarget != null || _isMissionOwned(demander))
        {
            ResolveRefusal(demand, "demander state changed before response");
            return;
        }

        Ship currentPlayer = playerShip ?? demand.PlayerShip;
        if (currentPlayer == null || currentPlayer.Hull?.IsDestroyed == true ||
            _isPlayerDocked(currentPlayer) || currentPlayer.IsTradeLaneTransit ||
            _isPoliceInteractionActive() ||
            Vector3.DistanceSquared(currentPlayer.Position, demander.Position) >
                DemandControlRange * DemandControlRange)
        {
            ResolveRefusal(demand, "player left demand-control range");
            return;
        }

        if (demander.WasDamagedByPlayer)
        {
            ResolveRefusal(demand, "player attacked the demander");
            return;
        }

        demand.RemainingSeconds = Math.Max(0f, demand.RemainingSeconds - delta);
        if (demand.RemainingSeconds > 0f)
            return;

        ResolveRefusal(demand, "response window expired");
    }

    /// <summary>
    /// Handles comply/refuse key input for the active demand. Returns true when
    /// the input was consumed by this interaction.
    /// </summary>
    public bool HandleInput(KeyboardState current, KeyboardState previous, Ship playerShip)
    {
        if (_activeDemand == null)
            return false;

        bool complyPressed = current.IsKeyDown(ComplyKey) && previous.IsKeyUp(ComplyKey);
        bool refusePressed = current.IsKeyDown(RefuseKey) && previous.IsKeyUp(RefuseKey);
        if (!complyPressed && !refusePressed)
            return false;

        if (complyPressed)
            TryComply(playerShip, out _);
        else
            TryRefuse(playerShip, out _);
        return true;
    }

    /// <summary>
    /// Player complies with the demand. Revalidates the encounter, removes the
    /// exact requested quantities from the player's CargoHold, records them in
    /// the pirate's bounded haul, and ends the interaction peacefully.
    /// </summary>
    public bool TryComply(Ship playerShip, out NpcPirateCargoDemandResult result)
    {
        result = null;
        if (_activeDemand == null)
            return false;

        ActiveDemand demand = _activeDemand;
        NpcShip demander = demand.Demander;
        if (demander == null || demander.IsDestroyed || !_npcShips.Contains(demander) ||
            !string.Equals(GetDemanderIdentity(demander), demand.DemanderIdentity, StringComparison.Ordinal))
        {
            ResolveDestroyedOrInvalid(demand, "demander no longer valid");
            return false;
        }

        Ship currentPlayer = playerShip ?? demand.PlayerShip;
        if (currentPlayer == null || currentPlayer.Hull?.IsDestroyed == true)
        {
            ResolveRefusal(demand, "player is no longer valid");
            return false;
        }

        int totalRemoved = 0;
        List<PirateDemandHaulEntry> haulEntries = new();
        foreach (KeyValuePair<string, int> requested in demand.RequestedQuantities)
        {
            Commodity commodity = CommodityCatalog.GetByName(requested.Key) ?? CommodityCatalog.GetById(requested.Key);
            if (commodity == null)
                continue;

            int available = currentPlayer.CargoHold.GetSellableCommodityQuantity(requested.Key);
            int quantity = Math.Min(requested.Value, available);
            if (quantity <= 0)
                continue;

            // Compliance transfers ownership to the pirate. The exact removed
            // quantity becomes stolen pirate haul regardless of the player's
            // previous clean/stolen provenance, and no original-owner state is
            // retained. Removal prefers stolen units first so the player's
            // remaining clean/stolen buckets stay mathematically correct.
            if (!currentPlayer.CargoHold.RemoveSellableCommodity(commodity, quantity, preferStolen: true))
                continue;

            haulEntries.Add(new PirateDemandHaulEntry
            {
                CommodityId = commodity.Id ?? commodity.Name,
                CommodityName = commodity.Name,
                Quantity = quantity
            });
            totalRemoved += quantity;
        }

        if (haulEntries.Count > 0)
        {
            _pirateHauls[demander] = haulEntries;
        }

        ApplyFlee(demand, currentPlayer);
        Complete(demand, NpcPirateDemandState.Complying, totalRemoved,
            $"player complied; {totalRemoved} units transferred to pirate haul");
        SetPresentation($"Cargo surrendered: {totalRemoved} units", 4f);
        _notify?.Invoke($"Pirate cargo demand complied: {totalRemoved} units surrendered");
        result = LastResult;
        return true;
    }

    /// <summary>
    /// Player refuses the demand. Removes no cargo, ends the interaction, and
    /// escalates through existing NPC combat targeting.
    /// </summary>
    public bool TryRefuse(Ship playerShip, out NpcPirateCargoDemandResult result)
    {
        result = null;
        if (_activeDemand == null)
            return false;

        ActiveDemand demand = _activeDemand;
        NpcShip demander = demand.Demander;
        if (demander == null || demander.IsDestroyed)
        {
            ResolveDestroyedOrInvalid(demand, "demander no longer valid");
            return false;
        }

        EscalateToCombat(demand, playerShip ?? demand.PlayerShip);
        Complete(demand, NpcPirateDemandState.Refusing, 0, "player refused the demand");
        SetPresentation("PIRATE DEMAND REFUSED — ESCALATING", 4f);
        _notify?.Invoke("Pirate cargo demand refused");
        result = LastResult;
        return true;
    }

    /// <summary>
    /// Notifies the service that an NPC was destroyed. If it was the active
    /// demander, the interaction is cancelled and its haul is dropped as
    /// physical stolen pods through existing loot authority.
    /// </summary>
    public void NotifyNpcDestroyed(NpcShip destroyedShip)
    {
        if (_activeDemand?.Demander == destroyedShip)
        {
            _activeDemand = null;
            _presentationText = string.Empty;
            _presentationRemainingSeconds = 0f;
            _log?.Invoke($"[NPC PIRACY] Demander {destroyedShip?.Name} destroyed; demand cancelled.");
        }

        // A single idempotent drop handles both the immediate-death race after
        // compliance and ordinary destruction. An uncommitted active demand
        // never has a haul, so a demander that dies before commit drops nothing.
        if (destroyedShip != null)
            DropPirateHaul(destroyedShip);
    }

    /// <summary>
    /// Cancels the active demand without removing cargo or escalating.
    /// Used for dock, system transition, and player death.
    /// </summary>
    public void CancelActiveDemand(string reason = "transient demand cancelled")
    {
        if (_activeDemand == null)
            return;

        _log?.Invoke($"[NPC PIRACY] {_activeDemand.Demander?.Name ?? "Pirate"}: {reason}.");
        _activeDemand = null;
        _presentationText = string.Empty;
        _presentationRemainingSeconds = 0f;
    }

    /// <summary>
    /// Resets all runtime state. Called on save/load and system transition.
    /// No demand state is persisted.
    /// </summary>
    public void Reset()
    {
        _activeDemand = null;
        _pirateHauls.Clear();
        _resolvedDemanderIdentities.Clear();
        _playerCooldownRemainingSeconds = 0f;
        LastResult = null;
        _presentationText = string.Empty;
        _presentationRemainingSeconds = 0f;
    }

    /// <summary>
    /// Returns a read-only snapshot of the pirate's current haul for the
    /// player target scan system.
    /// </summary>
    public bool TryGetHaulSnapshot(NpcShip demander, out NpcCargoManifestSnapshot snapshot)
    {
        snapshot = null;
        if (demander == null || demander.IsDestroyed || !_pirateHauls.TryGetValue(demander, out List<PirateDemandHaulEntry> haul))
            return false;

        snapshot = new NpcCargoManifestSnapshot(
            hasRegisteredCargo: true,
            haul.Select(entry => new NpcCargoManifestStackSnapshot(
                CommodityCatalog.GetById(entry.CommodityId) ?? CommodityCatalog.GetByName(entry.CommodityName),
                entry.Quantity,
                isStolen: true)));
        return true;
    }

    /// <summary>
    /// Returns the total haul quantity currently carried by a pirate.
    /// </summary>
    public int GetPirateHaulQuantity(NpcShip demander)
    {
        if (demander == null || !_pirateHauls.TryGetValue(demander, out List<PirateDemandHaulEntry> haul))
            return 0;
        return haul.Sum(entry => entry.Quantity);
    }

    private bool IsPlayerEligible(Ship playerShip, out string failureReason)
    {
        failureReason = string.Empty;
        if (playerShip == null || playerShip.Hull?.IsDestroyed == true)
        {
            failureReason = "player ship is invalid or destroyed";
            return false;
        }
        if (_isPlayerDocked(playerShip))
        {
            failureReason = "player is docked";
            return false;
        }
        if (playerShip.IsTradeLaneTransit)
        {
            failureReason = "player is in trade-lane transit";
            return false;
        }
        if (_isPoliceInteractionActive())
        {
            failureReason = "player is already resolving a Police interaction";
            return false;
        }
        return true;
    }

    private NpcShip SelectEligibleDemander(Ship playerShip, out string failureReason)
    {
        failureReason = string.Empty;
        float rangeSquared = DemandRange * DemandRange;
        NpcShip best = null;
        float bestDistanceSquared = float.MaxValue;
        string bestIdentity = string.Empty;

        for (int i = 0; i < _npcShips.Count; i++)
        {
            NpcShip npc = _npcShips[i];
            if (!IsEligibleDemander(npc))
                continue;

            float distanceSquared = Vector3.DistanceSquared(playerShip.Position, npc.Position);
            if (distanceSquared > rangeSquared)
                continue;

            string identity = GetDemanderIdentity(npc);
            if (best == null ||
                distanceSquared < bestDistanceSquared ||
                (distanceSquared == bestDistanceSquared &&
                 string.CompareOrdinal(identity, bestIdentity) < 0))
            {
                best = npc;
                bestDistanceSquared = distanceSquared;
                bestIdentity = identity;
            }
        }

        if (best == null)
        {
            failureReason = "no eligible Liberty Rogue demander in range";
            return null;
        }

        return best;
    }

    private bool IsEligibleDemander(NpcShip npc)
    {
        if (npc == null || npc.IsDestroyed)
            return false;
        if (!npc.FactionId.Equals(FactionManager.LibertyRogues, StringComparison.OrdinalIgnoreCase))
            return false;
        if (npc.IsTradeLaneTransit || npc.IsTrafficEngaged || npc.FactionCombatTarget != null)
            return false;
        if (_isMissionOwned(npc) || npc.IsMissionHoldPosition)
            return false;
        if (_resolvedDemanderIdentities.Contains(GetDemanderIdentity(npc)))
            return false;
        if (_reputationManager.IsAllied(npc.FactionId) || _reputationManager.IsFriendly(npc.FactionId))
            return false;
        return true;
    }

    private Dictionary<string, int> GetEligiblePlayerQuantities(Ship playerShip)
    {
        Dictionary<string, int> eligible = new(StringComparer.OrdinalIgnoreCase);
        if (playerShip?.CargoHold == null)
            return eligible;

        foreach (KeyValuePair<string, int> entry in playerShip.CargoHold.GetAllCommodities())
        {
            if (entry.Value <= 0)
                continue;
            Commodity commodity = CommodityCatalog.GetByName(entry.Key) ?? CommodityCatalog.GetById(entry.Key);
            if (commodity == null || commodity.IsMissionCargo)
                continue;
            int sellable = playerShip.CargoHold.GetSellableCommodityQuantity(entry.Key);
            if (sellable > 0)
                eligible[entry.Key] = sellable;
        }
        return eligible;
    }

    private static List<(Commodity Commodity, int Quantity)> SelectDemandQuantities(
        NpcShip demander,
        Dictionary<string, int> eligibleQuantities)
    {
        List<KeyValuePair<string, int>> sorted = eligibleQuantities
            .OrderBy(entry => entry.Key, StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (sorted.Count == 0)
            return new List<(Commodity, int)>();

        string identity = GetDemanderIdentity(demander);
        uint seed = StableHash($"{identity}|demand-selection");
        int typeCount = Math.Clamp(
            MinimumCommodityTypes + (int)(seed % (uint)(MaximumCommodityTypes - MinimumCommodityTypes + 1)),
            MinimumCommodityTypes,
            Math.Min(MaximumCommodityTypes, sorted.Count));

        List<(Commodity Commodity, int Quantity)> selected = new();
        HashSet<string> used = new(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < typeCount && selected.Count < typeCount; i++)
        {
            int index = (int)((seed + (uint)(i * 7919)) % (uint)sorted.Count);
            int guard = 0;
            while (guard < sorted.Count &&
                   used.Contains(sorted[index].Key))
            {
                index = (index + 1) % sorted.Count;
                guard++;
            }
            if (guard >= sorted.Count)
                break;

            KeyValuePair<string, int> entry = sorted[index];
            used.Add(entry.Key);
            Commodity commodity = CommodityCatalog.GetByName(entry.Key) ?? CommodityCatalog.GetById(entry.Key);
            if (commodity == null)
                continue;

            int percentage = MinimumDemandPercentage +
                (int)(StableHash($"{identity}|{commodity.Id}|quantity") %
                      (uint)(MaximumDemandPercentage - MinimumDemandPercentage + 1));
            int quantity = Math.Clamp(
                (int)Math.Ceiling(entry.Value * (percentage / 100f)),
                1,
                entry.Value);
            selected.Add((commodity, quantity));
        }
        return selected;
    }

    private void EscalateToCombat(ActiveDemand demand, Ship playerShip)
    {
        if (demand?.Demander == null || demand.Demander.IsDestroyed)
            return;

        demand.Demander.SetPlayerTarget(
            playerShip?.Position ?? demand.Demander.Position,
            NpcPlayerTargetReason.FactionDisposition);
        _log?.Invoke($"[NPC PIRACY] {demand.Demander.Name} escalating to combat against player.");
    }

    private void ApplyFlee(ActiveDemand demand, Ship playerShip)
    {
        if (demand?.Demander == null || demand.Demander.IsDestroyed)
            return;

        if (_markFleeing != null &&
            _markFleeing(demand.Demander, playerShip?.Position ?? demand.Demander.Position))
            return;

        Vector3 away = demand.Demander.Position - (playerShip?.Position ?? demand.Demander.Position - Vector3.Forward);
        if (away.LengthSquared() < 0.01f)
            away = Vector3.Forward;
        else
            away.Normalize();
        demand.Demander.SetEncounterState(
            TrafficEncounterState.Fleeing,
            playerShip?.Position ?? demand.Demander.Position,
            demand.Demander.Position + away * Math.Max(2500f, demand.Demander.TrafficCruiseSpeed * 8f));
    }

    private void DropPirateHaul(NpcShip demander)
    {
        if (demander == null || !_pirateHauls.TryGetValue(demander, out List<PirateDemandHaulEntry> haul))
            return;

        // Remove the entry before spawning so a repeated destruction
        // notification cannot duplicate physical pods for one haul line.
        _pirateHauls.Remove(demander);
        int dropped = 0;
        foreach (PirateDemandHaulEntry entry in haul)
        {
            if (entry.Quantity <= 0 || _spawnStolenCargo == null)
                continue;
            dropped += _spawnStolenCargo(demander, entry.CommodityId, entry.Quantity);
        }
        _log?.Invoke($"[NPC PIRACY] Pirate haul dropped: {haul.Sum(e => e.Quantity)} units ({dropped} podded).");
    }

    private void ResolveDestroyedOrInvalid(ActiveDemand demand, string reason)
    {
        DropPirateHaul(demand.Demander);
        Complete(demand, NpcPirateDemandState.Resolved, 0, reason);
        _presentationText = string.Empty;
        _presentationRemainingSeconds = 0f;
    }

    private void ResolveRefusal(ActiveDemand demand, string reason)
    {
        EscalateToCombat(demand, demand.PlayerShip);
        Complete(demand, NpcPirateDemandState.Refusing, 0, reason);
    }

    private void Complete(
        ActiveDemand demand,
        NpcPirateDemandState state,
        int surrendered,
        string reason)
    {
        _resolvedDemanderIdentities.Add(demand.DemanderIdentity);
        _playerCooldownRemainingSeconds = PlayerCooldownSeconds;
        LastResult = new NpcPirateCargoDemandResult
        {
            State = state,
            Demander = demand.Demander,
            DemanderIdentity = demand.DemanderIdentity,
            DemandedQuantities = new Dictionary<string, int>(demand.RequestedQuantities, StringComparer.OrdinalIgnoreCase),
            SurrenderedQuantity = surrendered,
            ResolutionReason = reason ?? string.Empty,
            PlayerShip = demand.PlayerShip
        };
        _activeDemand = null;
        DemandResolved?.Invoke(LastResult);
        _log?.Invoke($"[NPC PIRACY] Demand resolved: {state}; surrendered={surrendered}; reason={reason}.");
    }

    private static string FormatDemandText(List<(Commodity Commodity, int Quantity)> selected)
    {
        string commodities = string.Join(", ", selected.Select(entry =>
            $"{entry.Commodity?.Name ?? "Cargo"} x{entry.Quantity}"));
        return $"LIBERTY ROGUES DEMAND CARGO: {commodities} | [{ComplyKey}] Comply [{RefuseKey}] Refuse ({ResponseWindowSeconds:0}s)";
    }

    private static string GetDemanderIdentity(NpcShip demander) => NpcIdentity.GetStableIdentity(demander);

    private static float NormalizeDelta(float delta) =>
        float.IsNaN(delta) || float.IsInfinity(delta) ? 0f : Math.Clamp(delta, 0f, 60f);

    private void SetPresentation(string text, float seconds)
    {
        _presentationText = text ?? string.Empty;
        _presentationRemainingSeconds = Math.Max(0f, seconds);
    }

    private static uint StableHash(string value)
    {
        unchecked
        {
            uint hash = 2166136261u;
            foreach (char character in value ?? string.Empty)
            {
                hash ^= character;
                hash *= 16777619u;
            }
            return hash;
        }
    }
}
