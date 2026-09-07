using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Roguelancer;

public enum PiracyDemandState
{
    None,
    DemandIssued,
    Complying,
    RefusingFleeing,
    Resolved
}

public sealed class TraderCargoStack
{
    public Commodity Commodity { get; }
    public int InitialQuantity { get; }
    public int Quantity { get; private set; }

    public TraderCargoStack(Commodity commodity, int quantity)
    {
        Commodity = commodity;
        InitialQuantity = Math.Max(0, quantity);
        Quantity = InitialQuantity;
    }

    internal TraderCargoStack(Commodity commodity, int initialQuantity, int remainingQuantity)
    {
        Commodity = commodity;
        InitialQuantity = Math.Max(0, initialQuantity);
        Quantity = Math.Clamp(remainingQuantity, 0, InitialQuantity);
    }

    internal int Remove(int quantity)
    {
        int removed = Math.Min(Math.Max(0, quantity), Quantity);
        Quantity -= removed;
        return removed;
    }

    internal void SetRemainingQuantity(int quantity)
    {
        Quantity = Math.Clamp(quantity, 0, InitialQuantity);
    }
}

public sealed class TraderCargoManifest
{
    private readonly List<TraderCargoStack> _stacks;

    internal TraderCargoManifest(IEnumerable<TraderCargoStack> stacks, bool preserveEmptyStacks = false)
    {
        _stacks = stacks?.Where(stack => stack?.Commodity != null &&
            (preserveEmptyStacks ? stack.InitialQuantity > 0 : stack.Quantity > 0)).ToList() ?? new List<TraderCargoStack>();
    }

    public IReadOnlyList<TraderCargoStack> Stacks => _stacks;
    public int CommodityTypeCount => _stacks.Count(stack => stack.Quantity > 0);
    public int RemainingQuantity => _stacks.Sum(stack => Math.Max(0, stack.Quantity));
    public int InitialQuantity => _stacks.Sum(stack => Math.Max(0, stack.InitialQuantity));
    public int RemovedQuantity => Math.Max(0, InitialQuantity - RemainingQuantity);

    internal IReadOnlyList<TraderCargoStack> Snapshot() => _stacks.ToList();
}

public sealed class PiracyDemandResult
{
    public PiracyDemandState State { get; init; }
    public NpcShip Target { get; init; }
    public string TargetIdentity { get; init; } = string.Empty;
    public int SurrenderedQuantity { get; init; }
    public int RemainingManifestQuantity { get; init; }
    public string ResolutionReason { get; init; } = string.Empty;
    public FactionDistressResponseResult DistressResponse { get; init; }
    public Ship PlayerShip { get; init; }
}

/// <summary>
/// Owns the bounded, transient piracy-demand interaction. Trader cargo is
/// reconstructed from stable NPC information and only becomes player cargo
/// when LootManager creates a normal physical CargoPod and CargoHold picks it
/// up. This service has no credits, pirate-wallet, boarding, or hidden reward
/// counter authority.
/// </summary>
public sealed class PirateCargoDemandService
{
    public const float DemandRange = 3500f;
    public const float DemandControlRange = 5000f;
    public const float ResponseWindowSeconds = 3f;
    public const float ReputationPenalty = -0.03f;
    public const int MinimumManifestQuantity = 2;
    public const int MaximumManifestQuantity = 12;
    public const int MinimumCommodityTypes = 1;
    public const int MaximumCommodityTypes = 3;

    private sealed class ActiveDemand
    {
        public NpcShip Target { get; init; }
        public string TargetIdentity { get; init; } = string.Empty;
        public Ship PlayerShip { get; init; }
        public float RemainingSeconds { get; set; }
    }

    private readonly IReadOnlyList<NpcShip> _npcShips;
    private readonly ReputationManager _reputationManager;
    private readonly Func<NpcShip, bool> _isMissionOwned;
    private readonly Func<NpcShip, string, int, int> _spawnCargo;
    private readonly Func<NpcShip, Vector3, bool> _markFleeing;
    private readonly Func<NpcShip, Ship, string, FactionDistressResponseResult> _processDistress;
    private readonly Action<FactionDistressResponseResult, NpcShip, Ship> _onPoliceResponse;
    private readonly FactionCombatCommunicationService _communication;
    private readonly Action<string> _log;
    private readonly Func<NpcShip, TraderCargoManifest> _manifestResolver;
    // Standalone Phase 56 smoke harnesses do not construct the live market
    // owner. Keep their source-compatible deterministic fallback, while the
    // game always injects EconomicShipmentManager as the sole manifest owner.
    private readonly Dictionary<NpcShip, TraderCargoManifest> _manifests = new();
    private readonly HashSet<string> _resolvedTargetIdentities = new(StringComparer.Ordinal);
    private ActiveDemand _activeDemand;
    private float _presentationRemainingSeconds;
    private string _presentationText = string.Empty;

    public PirateCargoDemandService(
        IReadOnlyList<NpcShip> npcShips,
        ReputationManager reputationManager,
        Func<NpcShip, bool> isMissionOwned = null,
        Func<NpcShip, string, int, int> spawnCargo = null,
        Func<NpcShip, Vector3, bool> markFleeing = null,
        Func<NpcShip, Ship, string, FactionDistressResponseResult> processDistress = null,
        Action<FactionDistressResponseResult, NpcShip, Ship> onPoliceResponse = null,
        FactionCombatCommunicationService communication = null,
        Action<string> log = null,
        Func<NpcShip, TraderCargoManifest> manifestResolver = null)
    {
        _npcShips = npcShips ?? throw new ArgumentNullException(nameof(npcShips));
        _reputationManager = reputationManager ?? throw new ArgumentNullException(nameof(reputationManager));
        _isMissionOwned = isMissionOwned ?? (_ => false);
        _spawnCargo = spawnCargo;
        _markFleeing = markFleeing;
        _processDistress = processDistress;
        _onPoliceResponse = onPoliceResponse;
        _communication = communication;
        _log = log;
        _manifestResolver = manifestResolver;
    }

    public PiracyDemandState CurrentState => _activeDemand == null
        ? PiracyDemandState.None
        : PiracyDemandState.DemandIssued;
    public bool HasActiveDemand => _activeDemand != null;
    public NpcShip ActiveTarget => _activeDemand?.Target;
    public float ResponseRemainingSeconds => Math.Max(0f, _activeDemand?.RemainingSeconds ?? 0f);
    public string HudText => _presentationRemainingSeconds > 0f ? _presentationText : string.Empty;
    public PiracyDemandResult LastResult { get; private set; }
    public int ResolvedTargetCount => _resolvedTargetIdentities.Count;
    public event Action<PiracyDemandResult> DemandResolved;

    public bool TryGetManifest(NpcShip trader, out TraderCargoManifest manifest)
    {
        manifest = null;
        if (trader == null || !IsEligibleTrader(trader, includeActiveDemand: true))
            return false;

        manifest = GetOrCreateManifest(trader);
        return manifest != null;
    }

    /// <summary>
    /// Read-only scanner seam for the same manifest used by cargo demand.
    /// Unlike the interaction eligibility gate, this remains queryable after
    /// a resolved demand causes a trader to flee, so a later rescan reports
    /// the manifest quantity that actually remains.
    /// </summary>
    public bool TryGetAuthoritativeManifestSnapshot(
        NpcShip trader,
        out NpcCargoManifestSnapshot snapshot)
    {
        snapshot = null;
        if (trader == null || trader.IsDestroyed || !_npcShips.Contains(trader) ||
            trader.TrafficBehavior != TrafficZoneBehaviorType.TraderRoute ||
            _isMissionOwned(trader) || !IsEligibleFaction(trader.FactionId))
        {
            return false;
        }

        TraderCargoManifest manifest = GetOrCreateManifest(trader);
        if (manifest == null)
            return false;

        snapshot = new NpcCargoManifestSnapshot(
            hasRegisteredCargo: true,
            manifest?.Stacks.Select(stack =>
                new NpcCargoManifestStackSnapshot(stack.Commodity, stack.Quantity)));
        return true;
    }

    public bool TryIssueDemand(Ship playerShip, NpcShip target, out string failureReason)
    {
        failureReason = string.Empty;
        if (_activeDemand != null)
        {
            failureReason = "another cargo demand is already active";
            return false;
        }

        if (playerShip == null || playerShip.Hull?.IsDestroyed == true)
        {
            failureReason = "player ship is invalid";
            return false;
        }

        if (!IsEligibleTrader(target, includeActiveDemand: false))
        {
            failureReason = GetEligibilityFailure(target, playerShip);
            return false;
        }

        float distance = Vector3.Distance(playerShip.Position, target.Position);
        if (distance > DemandRange)
        {
            failureReason = $"target is out of demand range ({DemandRange:0} m)";
            return false;
        }

        TraderCargoManifest manifest = GetOrCreateManifest(target);
        if (manifest == null || manifest.RemainingQuantity <= 0)
        {
            failureReason = "target has no cargo to surrender";
            return false;
        }

        string identity = GetTargetIdentity(target);
        _activeDemand = new ActiveDemand
        {
            Target = target,
            TargetIdentity = identity,
            PlayerShip = playerShip,
            RemainingSeconds = ResponseWindowSeconds
        };
        _reputationManager.AdjustReputationDirect(
            target.FactionId,
            ReputationPenalty,
            ReputationChangeReason.PiracyDemand);
        SetPresentation("CARGO DEMAND SENT", 3f);
        _log?.Invoke($"[PIRACY] Cargo demand issued against {target.Name} ({identity}).");
        return true;
    }

    public void Update(float deltaSeconds, Ship playerShip)
    {
        float delta = NormalizeDelta(deltaSeconds);
        _presentationRemainingSeconds = Math.Max(0f, _presentationRemainingSeconds - delta);
        if (_activeDemand == null)
            return;

        ActiveDemand demand = _activeDemand;
        NpcShip target = demand.Target;
        if (target == null || target.IsDestroyed ||
            !string.Equals(GetTargetIdentity(target), demand.TargetIdentity, StringComparison.Ordinal) ||
            !_npcShips.Contains(target))
        {
            ResolveDestroyedOrInvalid(demand, "target destroyed or despawned");
            return;
        }

        if (!IsEligibleTrader(target, includeActiveDemand: true) || target.IsTradeLaneTransit ||
            target.IsTrafficEngaged || _isMissionOwned(target))
        {
            ResolveRefusal(demand, "target state changed before response");
            return;
        }

        Ship currentPlayer = playerShip ?? demand.PlayerShip;
        if (currentPlayer == null || currentPlayer.Hull?.IsDestroyed == true ||
            Vector3.DistanceSquared(currentPlayer.Position, target.Position) > DemandControlRange * DemandControlRange)
        {
            ResolveRefusal(demand, "player left demand-control range");
            return;
        }

        demand.RemainingSeconds = Math.Max(0f, demand.RemainingSeconds - delta);
        if (demand.RemainingSeconds > 0f)
            return;

        if (ShouldComply(target, currentPlayer))
            ResolveCompliance(demand);
        else
            ResolveRefusal(demand, "trader refused the demand");
    }

    public void NotifyNpcDestroyed(NpcShip target)
    {
        if (_activeDemand?.Target == target)
            ResolveDestroyedOrInvalid(_activeDemand, "target destroyed before response");

        if (target != null)
            _manifests.Remove(target);
    }

    public void CancelActiveDemand(string reason = "transient demand cancelled")
    {
        if (_activeDemand == null)
            return;

        _log?.Invoke($"[PIRACY] {_activeDemand.Target?.Name ?? "Trader"}: {reason}.");
        _activeDemand = null;
        _presentationText = string.Empty;
        _presentationRemainingSeconds = 0f;
    }

    public void Reset()
    {
        _activeDemand = null;
        _manifests.Clear();
        _resolvedTargetIdentities.Clear();
        LastResult = null;
        _presentationText = string.Empty;
        _presentationRemainingSeconds = 0f;
    }

    public bool IsEligibleTrader(NpcShip target, bool includeActiveDemand)
    {
        if (target == null || target.IsDestroyed || target.TrafficBehavior != TrafficZoneBehaviorType.TraderRoute ||
            target.IsTradeLaneTransit || _isMissionOwned(target) ||
            !IsEligibleFaction(target.FactionId) || target.IsMissionHoldPosition ||
            target.IsTrafficEngaged || target.FactionCombatTarget != null)
            return false;

        if (!includeActiveDemand && _resolvedTargetIdentities.Contains(GetTargetIdentity(target)))
            return false;

        return true;
    }

    private void ResolveCompliance(ActiveDemand demand)
    {
        TraderCargoManifest manifest = GetOrCreateManifest(demand.Target);
        int before = manifest?.RemainingQuantity ?? 0;
        int surrenderTarget = CalculateSurrenderQuantity(demand.Target, before);
        int surrendered = 0;

        if (manifest != null && _spawnCargo != null)
        {
            foreach (TraderCargoStack stack in manifest.Snapshot())
            {
                if (surrendered >= surrenderTarget || stack.Quantity <= 0)
                    break;

                int requested = Math.Min(stack.Quantity, surrenderTarget - surrendered);
                int released = Math.Clamp(_spawnCargo(demand.Target, stack.Commodity.Id, requested), 0, requested);
                if (released <= 0)
                    continue;

                int removed = stack.Remove(released);
                surrendered += removed;
            }
        }

        bool distressCalled = HasNearbyPolice(demand.Target);
        FactionDistressResponseResult distress = ProcessDistressIfRequested(demand, distressCalled);
        _communication?.NotifyPiracyDemandResponse(
            demand.Target,
            PiracyDemandState.Complying,
            surrendered,
            distressCalled,
            demand.PlayerShip);

        ApplyFlee(demand, demand.PlayerShip);
        Complete(demand, PiracyDemandState.Complying, surrendered,
            "trader complied and attempted to leave", distress);
        SetPresentation($"Cargo surrendered: {surrendered} units", 4f);
    }

    private void ResolveRefusal(ActiveDemand demand, string reason)
    {
        // Refusal is a local distress event only when an actual nearby Police
        // witness exists. This keeps ambient response bounded and avoids
        // creating a Police encounter from a remote/isolated interaction.
        bool distressCalled = HasNearbyPolice(demand.Target);
        FactionDistressResponseResult distress = ProcessDistressIfRequested(demand, distressCalled);
        _communication?.NotifyPiracyDemandResponse(
            demand.Target,
            PiracyDemandState.RefusingFleeing,
            0,
            distressCalled,
            demand.PlayerShip);

        ApplyFlee(demand, demand.PlayerShip);
        Complete(demand, PiracyDemandState.RefusingFleeing, 0, reason, distress);
        SetPresentation("TRADER REFUSED — FLEEING", 4f);
    }

    private void ResolveDestroyedOrInvalid(ActiveDemand demand, string reason)
    {
        Complete(demand, PiracyDemandState.Resolved, 0, reason, default);
        _presentationText = string.Empty;
        _presentationRemainingSeconds = 0f;
    }

    private void Complete(
        ActiveDemand demand,
        PiracyDemandState state,
        int surrendered,
        string reason,
        FactionDistressResponseResult distress)
    {
        TraderCargoManifest manifest = demand.Target == null ? null : GetOrCreateManifest(demand.Target);
        _resolvedTargetIdentities.Add(demand.TargetIdentity);
        LastResult = new PiracyDemandResult
        {
            State = state,
            Target = demand.Target,
            TargetIdentity = demand.TargetIdentity,
            SurrenderedQuantity = surrendered,
            RemainingManifestQuantity = manifest?.RemainingQuantity ?? 0,
            ResolutionReason = reason ?? string.Empty,
            DistressResponse = distress,
            PlayerShip = demand.PlayerShip
        };
        _activeDemand = null;
        DemandResolved?.Invoke(LastResult);
        _log?.Invoke($"[PIRACY] Demand resolved: {state}; surrendered={surrendered}; reason={reason}.");
    }

    private FactionDistressResponseResult ProcessDistressIfRequested(ActiveDemand demand, bool requested)
    {
        if (!requested || _processDistress == null || demand?.Target == null || demand.PlayerShip == null)
            return default;

        FactionDistressResponseResult response = _processDistress(
            demand.Target,
            demand.PlayerShip,
            $"piracy:{demand.TargetIdentity}");
        if ((response.WaveSpawned || response.AssistedShipCount > 0) && _onPoliceResponse != null)
            _onPoliceResponse(response, demand.Target, demand.PlayerShip);
        return response;
    }

    private void ApplyFlee(ActiveDemand demand, Ship playerShip)
    {
        if (demand?.Target == null || demand.Target.IsDestroyed)
            return;

        if (_markFleeing != null && _markFleeing(demand.Target, playerShip?.Position ?? demand.Target.Position))
            return;

        Vector3 away = demand.Target.Position - (playerShip?.Position ?? demand.Target.Position - Vector3.Forward);
        if (away.LengthSquared() < 0.01f)
            away = Vector3.Forward;
        else
            away.Normalize();
        demand.Target.SetEncounterState(
            TrafficEncounterState.Fleeing,
            playerShip?.Position ?? demand.Target.Position,
            demand.Target.Position + away * Math.Max(2500f, demand.Target.TrafficCruiseSpeed * 8f));
    }

    private TraderCargoManifest GetOrCreateManifest(NpcShip trader)
    {
        if (trader == null)
            return null;

        if (_manifestResolver != null)
            return _manifestResolver(trader);

        if (_manifests.TryGetValue(trader, out TraderCargoManifest existing))
            return existing;

        TraderCargoManifest created = BuildDeterministicManifest(trader);
        _manifests[trader] = created;
        return created;
    }

    private static TraderCargoManifest BuildDeterministicManifest(NpcShip trader)
    {
        List<Commodity> legalCommodities = CommodityCatalog.All
            .Where(commodity => commodity != null && !commodity.IsContraband && !commodity.IsMissionCargo && commodity.VolumePerUnit > 0)
            .ToList();
        if (legalCommodities.Count == 0)
            return new TraderCargoManifest(Array.Empty<TraderCargoStack>());

        string route = string.Join("|",
            trader.TrafficRouteStart?.ToString() ?? string.Empty,
            trader.TrafficRouteEnd?.ToString() ?? string.Empty);
        uint seed = StableHash($"{trader.StableIdentity}|{trader.FactionId}|{trader.ModelPath}|{route}");
        int total = MinimumManifestQuantity + (int)(seed % (uint)(MaximumManifestQuantity - MinimumManifestQuantity + 1));
        int typeCount = Math.Clamp(1 + (int)((seed >> 8) % (uint)MaximumCommodityTypes), MinimumCommodityTypes, Math.Min(MaximumCommodityTypes, total));
        List<TraderCargoStack> stacks = new();
        HashSet<int> used = new();
        for (int i = 0; i < typeCount; i++)
        {
            int index = (int)((seed + (uint)(i * 7919)) % (uint)legalCommodities.Count);
            while (!used.Add(index))
                index = (index + 1) % legalCommodities.Count;
            stacks.Add(new TraderCargoStack(legalCommodities[index], 1));
        }

        int remainder = total - typeCount;
        for (int i = 0; i < remainder; i++)
        {
            int stackIndex = (int)((seed + (uint)(i * 3571) + 17u) % (uint)stacks.Count);
            TraderCargoStack selected = stacks[stackIndex];
            stacks[stackIndex] = new TraderCargoStack(selected.Commodity, selected.Quantity + 1);
        }

        return new TraderCargoManifest(stacks);
    }

    private bool ShouldComply(NpcShip target, Ship playerShip)
    {
        float hullPercentage = target.Hull?.MaxHull > 0f
            ? Math.Clamp(target.Hull.CurrentHull / target.Hull.MaxHull, 0f, 1f)
            : 0f;
        float shieldPercentage = target.Shields?.MaxShields > 0f
            ? Math.Clamp(target.Shields.CurrentShields / target.Shields.MaxShields, 0f, 1f)
            : 0f;
        float score = target.FactionId.Equals(FactionManager.LibertyCorporations, StringComparison.OrdinalIgnoreCase)
            ? 0.42f
            : 0.50f;
        score += (1f - hullPercentage) * 0.40f;
        score += (1f - shieldPercentage) * 0.18f;
        if (target.WasDamagedByPlayer)
            score += 0.15f;
        if (_reputationManager.IsFactionCurrentlyHostile(target.FactionId))
            score -= 0.20f;
        if (playerShip?.Loadout?.GetMountedGuns().Any() == true)
            score += 0.05f;
        if (HasNearbyPolice(target))
            score -= 0.25f;

        float threshold = 0.40f + (StableHash($"{GetTargetIdentity(target)}|response") % 31u) / 100f;
        return Math.Clamp(score, 0f, 1f) >= threshold;
    }

    private static int CalculateSurrenderQuantity(NpcShip target, int remaining)
    {
        if (remaining <= 0)
            return 0;
        int percentage = 25 + (int)(StableHash($"{GetTargetIdentity(target)}|quantity") % 51u);
        return Math.Clamp((int)Math.Ceiling(remaining * (percentage / 100f)), 1, remaining);
    }

    private bool HasNearbyPolice(NpcShip target)
    {
        const float radius = FactionDistressResponseService.LocalContextRadius;
        float radiusSquared = radius * radius;
        for (int i = 0; i < _npcShips.Count; i++)
        {
            NpcShip police = _npcShips[i];
            if (police == null || police.IsDestroyed ||
                !police.FactionId.Equals(FactionManager.LibertyPolice, StringComparison.OrdinalIgnoreCase))
                continue;
            if (Vector3.DistanceSquared(police.Position, target.Position) <= radiusSquared)
                return true;
        }
        return false;
    }

    private bool IsEligibleFaction(string factionId) =>
        factionId.Equals(FactionManager.LibertyCorporations, StringComparison.OrdinalIgnoreCase) ||
        factionId.Equals(FactionManager.NeutralCivilians, StringComparison.OrdinalIgnoreCase);

    private string GetEligibilityFailure(NpcShip target, Ship playerShip)
    {
        if (target == null)
            return "no eligible trader target";
        if (target.IsDestroyed)
            return "target is destroyed";
        if (target.IsTradeLaneTransit)
            return "trade-lane traffic cannot be hailed";
        if (target.TrafficBehavior != TrafficZoneBehaviorType.TraderRoute)
            return "target is not an ordinary trader";
        if (_isMissionOwned(target))
            return "mission-owned traffic is protected";
        if (!IsEligibleFaction(target.FactionId))
            return "target faction is not eligible for free-roam extortion";
        if (target.IsTrafficEngaged || target.FactionCombatTarget != null)
            return "target is already in combat";
        if (_resolvedTargetIdentities.Contains(GetTargetIdentity(target)))
            return "this trader has already resolved a cargo demand";
        if (playerShip != null && Vector3.Distance(playerShip.Position, target.Position) > DemandRange)
            return $"target is out of demand range ({DemandRange:0} m)";
        return "target is not eligible for a cargo demand";
    }

    private static string GetTargetIdentity(NpcShip target) => NpcIdentity.GetStableIdentity(target);

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
