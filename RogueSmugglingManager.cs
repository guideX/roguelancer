using Microsoft.Xna.Framework;
using Roguelancer.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Roguelancer;

public enum RogueSmugglingShipmentSettlement
{
    Active,
    Delivered,
    Lost
}

/// <summary>
/// One real Phase 68 criminal logistics movement. Its manifest is the same
/// mutable cargo authority used by the existing NPC shipment and scanner
/// paths; the only new state here is the criminal shipment lifecycle.
/// </summary>
public sealed class RogueSmugglingShipment
{
    internal RogueSmugglingShipment(
        NpcShip carrier,
        string routeId,
        string originStationId,
        string destinationStationId,
        string originStationName,
        string destinationStationName,
        bool destinationIsRouteEnd,
        TraderCargoManifest manifest,
        Station originMarket,
        Station destinationMarket)
    {
        Carrier = carrier;
        ShipmentIdentity = NpcIdentity.GetStableIdentity(carrier);
        RouteId = routeId ?? string.Empty;
        OriginStationId = originStationId ?? string.Empty;
        DestinationStationId = destinationStationId ?? string.Empty;
        OriginStationName = originStationName ?? string.Empty;
        DestinationStationName = destinationStationName ?? string.Empty;
        DestinationIsRouteEnd = destinationIsRouteEnd;
        Manifest = manifest;
        OriginMarket = originMarket;
        DestinationMarket = destinationMarket;
    }

    public NpcShip Carrier { get; internal set; }
    public string ShipmentIdentity { get; }
    public string RouteId { get; }
    public string OriginStationId { get; }
    public string DestinationStationId { get; }
    public string OriginStationName { get; }
    public string DestinationStationName { get; }
    public bool DestinationIsRouteEnd { get; }
    public TraderCargoManifest Manifest { get; }
    internal Station OriginMarket { get; }
    internal Station DestinationMarket { get; }
    public RogueSmugglingShipmentSettlement Settlement { get; internal set; } = RogueSmugglingShipmentSettlement.Active;
    public int InitialQuantity => Manifest?.InitialQuantity ?? 0;
    public int RemainingQuantity => Manifest?.RemainingQuantity ?? 0;
    public int RemovedQuantity => Manifest?.RemovedQuantity ?? 0;
    public int InitialManifestValue => Manifest?.Stacks.Sum(stack =>
        Math.Max(0, stack.InitialQuantity) * Math.Max(0, stack.Commodity?.BasePrice ?? 0)) ?? 0;
    public int RemainingManifestValue => Manifest?.Stacks.Sum(stack =>
        Math.Max(0, stack.Quantity) * Math.Max(0, stack.Commodity?.BasePrice ?? 0)) ?? 0;
}

/// <summary>
/// Schedules a very small number of Rogue carriers on a configured trader
/// route. It owns shipment accounting only; stock remains in MarketManager,
/// movement remains in NpcShip/TrafficManager, and physical loot remains in
/// LootManager.
/// </summary>
public sealed class RogueSmugglingManager
{
    public const int MaximumActiveShipments = 2;
    public const int MinimumShipmentQuantity = 2;
    public const int MaximumShipmentQuantity = 8;
    public const int MaximumCommodityTypes = 2;
    public const int MaximumSavedShipments = 2;
    public const float SpawnIntervalSeconds = 45f;
    public const float MaximumTransitLifetimeSeconds = 480f;

    private sealed class PendingShipment
    {
        public SaveRogueSmugglingShipmentData Data { get; init; }
    }

    private readonly MarketManager _marketManager;
    private readonly Func<IEnumerable<Station>> _stationsProvider;
    private readonly Func<IEnumerable<TrafficZoneConfig>> _routesProvider;
    private readonly Dictionary<NpcShip, RogueSmugglingShipment> _shipments = new();
    private readonly Dictionary<string, RogueSmugglingShipmentSettlement> _settlementHistory = new(StringComparer.Ordinal);
    private readonly Dictionary<string, PendingShipment> _pendingRebind = new(StringComparer.Ordinal);
    private Func<TrafficZoneConfig, string, NpcShip> _spawnCarrier;
    private Action<NpcShip, string> _retireCarrier;
    private bool _rebindMode;
    private float _spawnTimer = SpawnIntervalSeconds;

    public RogueSmugglingManager(
        MarketManager marketManager,
        Func<IEnumerable<Station>> stationsProvider,
        Func<IEnumerable<TrafficZoneConfig>> routesProvider)
    {
        _marketManager = marketManager ?? throw new ArgumentNullException(nameof(marketManager));
        _stationsProvider = stationsProvider ?? (() => Array.Empty<Station>());
        _routesProvider = routesProvider ?? (() => Array.Empty<TrafficZoneConfig>());
    }

    public MarketManager MarketManager => _marketManager;
    public int ActiveShipmentCount => _shipments.Values.Count(shipment => shipment?.Settlement == RogueSmugglingShipmentSettlement.Active);
    public IReadOnlyList<RogueSmugglingShipment> ActiveShipments => _shipments.Values
        .Where(shipment => shipment?.Settlement == RogueSmugglingShipmentSettlement.Active)
        .OrderBy(shipment => shipment.ShipmentIdentity, StringComparer.Ordinal)
        .ToList();

    public void ConfigureRuntime(
        Func<TrafficZoneConfig, string, NpcShip> spawnCarrier,
        Action<NpcShip, string> retireCarrier)
    {
        _spawnCarrier = spawnCarrier;
        _retireCarrier = retireCarrier;
    }

    public void Update(float deltaSeconds, Action<string> log = null)
    {
        if (float.IsNaN(deltaSeconds) || float.IsInfinity(deltaSeconds) || deltaSeconds <= 0f)
            return;

        if (_rebindMode)
            return;

        _spawnTimer = Math.Max(0f, _spawnTimer - Math.Min(deltaSeconds, 60f));
        if (_spawnTimer > 0f || ActiveShipmentCount >= MaximumActiveShipments)
            return;

        _spawnTimer = SpawnIntervalSeconds;
        TrafficZoneConfig route = GetEligibleRoutes().FirstOrDefault();
        if (route == null || _spawnCarrier == null)
            return;

        string identityHint = $"rogue-smuggler:{route.Id}:{_marketManager.ElapsedMilliseconds}";
        NpcShip carrier = null;
        try
        {
            carrier = _spawnCarrier(route, identityHint);
        }
        catch (Exception ex)
        {
            log?.Invoke($"[SMUGGLING] Carrier spawn failed: {ex.Message}");
        }

        if (carrier == null)
            return;

        if (!TryAttachCarrier(carrier, route, out RogueSmugglingShipment shipment))
        {
            log?.Invoke($"[SMUGGLING] Carrier {carrier.Name} could not receive a canonical contraband manifest on route {route.Id}.");
            _retireCarrier?.Invoke(carrier, "smuggling manifest unavailable");
            return;
        }

        log?.Invoke($"[SMUGGLING] Reserved {shipment.InitialQuantity} contraband units for {carrier.Name}: {shipment.OriginStationId} -> {shipment.DestinationStationId}.");
    }

    /// <summary>
    /// Reconstructs saved carriers after TrafficManager has recreated its
    /// route zones. No source debit occurs during this path.
    /// </summary>
    public void TryRestorePendingCarriers(Action<string> log = null)
    {
        if (!_rebindMode || _spawnCarrier == null)
            return;

        foreach (PendingShipment pending in _pendingRebind.Values.ToList())
        {
            SaveRogueSmugglingShipmentData state = pending?.Data;
            TrafficZoneConfig route = FindRoute(state?.RouteId);
            if (state == null || route == null || string.IsNullOrWhiteSpace(state.ShipmentIdentity))
                continue;

            NpcShip carrier = null;
            try
            {
                carrier = _spawnCarrier(route, state.ShipmentIdentity);
            }
            catch (Exception ex)
            {
                log?.Invoke($"[SMUGGLING] Saved carrier restore failed: {ex.Message}");
            }

            if (carrier == null)
                continue;

            if (TryAttachCarrier(carrier, route, out RogueSmugglingShipment restored))
            {
                _pendingRebind.Remove(state.ShipmentIdentity);
                log?.Invoke($"[SMUGGLING] Restored saved carrier {restored.ShipmentIdentity} with {restored.RemainingQuantity} contraband units.");
            }
            else
            {
                _retireCarrier?.Invoke(carrier, "saved smuggling manifest rejected");
            }
        }
    }

    public bool TryAttachCarrier(
        NpcShip carrier,
        TrafficZoneConfig route,
        out RogueSmugglingShipment shipment)
    {
        shipment = null;
        if (carrier == null || route == null || carrier.IsDestroyed ||
            route.BehaviorType != TrafficZoneBehaviorType.TraderRoute ||
            !route.IsRogueSmugglingRoute || _shipments.ContainsKey(carrier) ||
            ActiveShipmentCount >= MaximumActiveShipments)
            return false;

        string identity = NpcIdentity.GetStableIdentity(carrier);
        if (_rebindMode && _pendingRebind.TryGetValue(identity, out PendingShipment pending))
        {
            if (!TryRestorePendingShipment(carrier, route, pending.Data, out shipment))
                return false;

            carrier.MarkRogueSmuggler(shipment.ShipmentIdentity);
            _shipments[carrier] = shipment;
            return true;
        }

        if (_rebindMode)
            return false;

        if (!TryResolveRouteStations(route, carrier, out Station origin, out Station destination, out bool towardEnd) ||
            !TryBuildAndReserveManifest(origin, destination, carrier, route, out TraderCargoManifest manifest))
            return false;

        if (!carrier.ConfigureTrafficRouteEndpoints(route.RouteStart, route.RouteEnd, towardEnd))
        {
            RestoreOriginStock(origin, manifest);
            return false;
        }

        shipment = new RogueSmugglingShipment(
            carrier,
            route.Id,
            _marketManager.GetStationId(origin),
            _marketManager.GetStationId(destination),
            origin.Name,
            destination.Name,
            towardEnd,
            manifest,
            origin,
            destination);
        carrier.MarkRogueSmuggler(shipment.ShipmentIdentity);
        carrier.TrafficLifetimeSeconds = MaximumTransitLifetimeSeconds;
        _shipments[carrier] = shipment;
        return true;
    }

    public void NotifyRouteEndpointReached(NpcShip carrier, bool reachedRouteEnd, Action<string> log = null)
    {
        if (!TryGetShipment(carrier, out RogueSmugglingShipment shipment) ||
            shipment.DestinationIsRouteEnd != reachedRouteEnd)
            return;

        TryDeliver(carrier, log);
    }

    public bool TryDeliver(NpcShip carrier, Action<string> log = null)
    {
        if (!TryGetShipment(carrier, out RogueSmugglingShipment shipment))
            return false;

        Station destination = FindMarketStation(shipment.DestinationStationId);
        List<TraderCargoStack> remaining = shipment.Manifest.Snapshot()
            .Where(stack => stack?.Commodity?.IsContraband == true && stack.Quantity > 0)
            .ToList();
        if (destination == null || remaining.Count == 0)
        {
            log?.Invoke($"[SMUGGLING] Delivery rejected for {shipment.ShipmentIdentity}: destination={(destination != null)}, remaining={remaining.Sum(stack => stack.Quantity)}.");
            MarkLost(carrier, "invalid smuggling destination or empty manifest", remove: true);
            return false;
        }

        // Preflight every stack so the receipt is all-or-nothing. The
        // destination can consume stock between arrivals, so a full receiver
        // simply keeps the carrier in normal route movement for a later tick.
        foreach (TraderCargoStack stack in remaining)
        {
            if (!_marketManager.CanReceiveCriminalSupply(destination, stack.Commodity, stack.Quantity, out _))
            {
                log?.Invoke($"[SMUGGLING] Delivery waiting for destination capacity at {shipment.DestinationStationId} ({stack.Commodity.Id} x{stack.Quantity}).");
                return false;
            }
        }

        int delivered = 0;
        List<(Commodity Commodity, int Quantity)> applied = new();
        foreach (TraderCargoStack stack in remaining)
        {
            if (!_marketManager.TryAddCriminalSupply(
                    destination,
                    stack.Commodity,
                    stack.Quantity,
                    out int accepted,
                    out _) || accepted != stack.Quantity)
            {
                log?.Invoke($"[SMUGGLING] Delivery rejected while applying {stack.Commodity.Id} x{stack.Quantity} at {shipment.DestinationStationId}.");
                foreach ((Commodity Commodity, int Quantity) rollback in applied)
                    _marketManager.TryRemoveCriminalSupply(destination, rollback.Commodity, rollback.Quantity, 0, out _);
                // Preflight makes this unreachable in the single-threaded
                // simulation. The rollback keeps the receipt atomic even if
                // a future market authority adds a second validation layer.
                return false;
            }

            int removed = stack.Remove(accepted);
            applied.Add((stack.Commodity, removed));
            delivered += removed;
        }

        shipment.Settlement = RogueSmugglingShipmentSettlement.Delivered;
        _settlementHistory[shipment.ShipmentIdentity] = shipment.Settlement;
        _shipments.Remove(carrier);
        carrier.ClearRogueSmuggler();
        _retireCarrier?.Invoke(carrier, "smuggling delivery settled");
        log?.Invoke($"[SMUGGLING] Delivered {delivered} contraband units from {shipment.OriginStationName} to {shipment.DestinationStationName}.");
        return true;
    }

    public bool TryGetShipment(NpcShip carrier, out RogueSmugglingShipment shipment)
    {
        shipment = null;
        return carrier != null && _shipments.TryGetValue(carrier, out shipment) &&
            shipment?.Settlement == RogueSmugglingShipmentSettlement.Active;
    }

    public bool TryGetShipmentByIdentity(string shipmentIdentity, out RogueSmugglingShipment shipment)
    {
        shipment = null;
        if (string.IsNullOrWhiteSpace(shipmentIdentity))
            return false;

        shipment = ActiveShipments.FirstOrDefault(candidate =>
            string.Equals(candidate.ShipmentIdentity, shipmentIdentity.Trim(), StringComparison.Ordinal));
        return shipment != null;
    }

    public bool TryGetSettlementByIdentity(string shipmentIdentity, out RogueSmugglingShipmentSettlement settlement)
    {
        settlement = RogueSmugglingShipmentSettlement.Active;
        return !string.IsNullOrWhiteSpace(shipmentIdentity) &&
            _settlementHistory.TryGetValue(shipmentIdentity.Trim(), out settlement);
    }

    public TraderCargoManifest GetManifest(NpcShip carrier) =>
        TryGetShipment(carrier, out RogueSmugglingShipment shipment) ? shipment.Manifest : null;

    public bool TryGetManifestSnapshot(NpcShip carrier, out NpcCargoManifestSnapshot snapshot)
    {
        snapshot = null;
        if (!TryGetShipment(carrier, out RogueSmugglingShipment shipment))
            return false;

        snapshot = new NpcCargoManifestSnapshot(
            hasRegisteredCargo: true,
            shipment.Manifest.Stacks.Select(stack =>
                new NpcCargoManifestStackSnapshot(stack.Commodity, stack.Quantity, isStolen: false)));
        return true;
    }

    public bool TryGetRouteInfo(NpcShip carrier, out PlayerTargetScanRouteInfo routeInfo)
    {
        routeInfo = null;
        if (!TryGetShipment(carrier, out RogueSmugglingShipment shipment))
            return false;

        routeInfo = new PlayerTargetScanRouteInfo(
            shipment.RouteId,
            shipment.OriginStationId,
            shipment.OriginStationName,
            shipment.DestinationStationId,
            shipment.DestinationStationName,
            routeRisk: 0,
            routeRiskLabel: "LOW",
            securityEscortCount: 0);
        return true;
    }

    public void NotifyCarrierDestroyed(NpcShip carrier)
    {
        if (!TryGetShipment(carrier, out RogueSmugglingShipment shipment))
            return;

        shipment.Settlement = RogueSmugglingShipmentSettlement.Lost;
        _settlementHistory[shipment.ShipmentIdentity] = shipment.Settlement;
    }

    public void NotifyCarrierDespawned(NpcShip carrier, string reason = null)
    {
        if (!TryGetShipment(carrier, out RogueSmugglingShipment shipment))
            return;

        MarkLost(carrier, reason ?? "carrier despawned", remove: true);
    }

    private void MarkLost(NpcShip carrier, string reason, bool remove)
    {
        if (!_shipments.TryGetValue(carrier, out RogueSmugglingShipment shipment))
            return;

        shipment.Settlement = RogueSmugglingShipmentSettlement.Lost;
        _settlementHistory[shipment.ShipmentIdentity] = shipment.Settlement;
        if (remove)
        {
            _shipments.Remove(carrier);
            carrier?.ClearRogueSmuggler();
        }
    }

    /// <summary>
    /// Returns remaining lost cargo to the existing destruction/salvage path.
    /// LootManager consumes these quantities as real CargoPods, then calls the
    /// finalizer so a lost manifest cannot be reused.
    /// </summary>
    public IReadOnlyList<SalvageDrop> GetDestructionSalvage(NpcShip carrier)
    {
        if (!_shipments.TryGetValue(carrier, out RogueSmugglingShipment shipment) ||
            shipment.Settlement != RogueSmugglingShipmentSettlement.Lost)
            return null;

        return shipment.Manifest.Snapshot()
            .Where(stack => stack?.Commodity?.IsContraband == true && stack.Quantity > 0)
            .Take(CombatSalvageService.MaxLiveSalvageObjects)
            .Select((stack, index) => new SalvageDrop(
                stack.Commodity.Id,
                stack.Quantity,
                index,
                CombatSalvageTier.Standard))
            .ToList();
    }

    public int ConsumeDestructionSalvage(NpcShip carrier, string commodityId, int quantity)
    {
        if (!_shipments.TryGetValue(carrier, out RogueSmugglingShipment shipment) ||
            shipment.Settlement != RogueSmugglingShipmentSettlement.Lost || quantity <= 0)
            return 0;

        TraderCargoStack stack = shipment.Manifest.Snapshot().FirstOrDefault(candidate =>
            string.Equals(candidate.Commodity?.Id, commodityId, StringComparison.OrdinalIgnoreCase));
        return stack?.Remove(quantity) ?? 0;
    }

    public void FinalizeDestroyedCarrier(NpcShip carrier)
    {
        if (!_shipments.TryGetValue(carrier, out RogueSmugglingShipment shipment) ||
            shipment.Settlement != RogueSmugglingShipmentSettlement.Lost)
            return;

        foreach (TraderCargoStack stack in shipment.Manifest.Snapshot())
            stack.Remove(stack.Quantity);
        _shipments.Remove(carrier);
        carrier?.ClearRogueSmuggler();
    }

    public void ResetForWorldTeardown(bool restoreOriginStock)
    {
        foreach (RogueSmugglingShipment shipment in _shipments.Values.ToList())
        {
            bool preservedForRebind = _rebindMode && _pendingRebind.ContainsKey(shipment.ShipmentIdentity);
            if (restoreOriginStock && !preservedForRebind &&
                shipment.Settlement == RogueSmugglingShipmentSettlement.Active)
            {
                Station origin = shipment.OriginMarket ?? FindMarketStation(shipment.OriginStationId);
                RestoreOriginStock(origin, shipment.Manifest);
            }

            shipment.Settlement = RogueSmugglingShipmentSettlement.Lost;
            shipment.Carrier?.ClearRogueSmuggler();
        }

        _shipments.Clear();
        _settlementHistory.Clear();
        _spawnTimer = SpawnIntervalSeconds;
        if (!_rebindMode)
            _pendingRebind.Clear();
    }

    public void Reset() => ResetForWorldTeardown(restoreOriginStock: true);

    public List<SaveRogueSmugglingShipmentData> CaptureState()
    {
        return ActiveShipments
            .Take(MaximumSavedShipments)
            .Select(shipment => new SaveRogueSmugglingShipmentData
            {
                ShipmentIdentity = shipment.ShipmentIdentity,
                RouteId = shipment.RouteId,
                OriginStationId = shipment.OriginStationId,
                DestinationStationId = shipment.DestinationStationId,
                OriginStationName = shipment.OriginStationName,
                DestinationStationName = shipment.DestinationStationName,
                InitialQuantity = shipment.InitialQuantity,
                RemainingQuantity = shipment.RemainingQuantity,
                RouteTowardEnd = shipment.DestinationIsRouteEnd,
                Settlement = shipment.Settlement,
                Position = SaveVector3Data.From(shipment.Carrier?.Position ?? Vector3.Zero),
                Velocity = SaveVector3Data.From(shipment.Carrier?.Velocity ?? Vector3.Zero),
                TrafficAgeSeconds = shipment.Carrier?.TrafficAgeSeconds ?? 0f,
                Stacks = shipment.Manifest.Stacks.Select(stack => new SaveRogueSmugglingStackData
                {
                    CommodityId = stack.Commodity?.Id ?? string.Empty,
                    InitialQuantity = stack.InitialQuantity,
                    RemainingQuantity = stack.Quantity
                }).ToList()
            })
            .ToList();
    }

    public void PrepareForSaveLoadRebind(IEnumerable<SaveRogueSmugglingShipmentData> states)
    {
        _pendingRebind.Clear();
        foreach (SaveRogueSmugglingShipmentData state in (states ?? Array.Empty<SaveRogueSmugglingShipmentData>()).Take(MaximumSavedShipments))
        {
            if (state == null || string.IsNullOrWhiteSpace(state.ShipmentIdentity) ||
                state.Settlement != RogueSmugglingShipmentSettlement.Active ||
                state.Stacks == null || state.Stacks.Count == 0)
                continue;

            _pendingRebind[state.ShipmentIdentity.Trim()] = new PendingShipment { Data = state };
        }

        _rebindMode = _pendingRebind.Count > 0;
    }

    public void CompleteRebind()
    {
        foreach (PendingShipment pending in _pendingRebind.Values.ToList())
        {
            SaveRogueSmugglingShipmentData state = pending?.Data;
            Station origin = FindMarketStation(state?.OriginStationId);
            if (origin == null)
                continue;

            foreach (SaveRogueSmugglingStackData stack in state.Stacks ?? new List<SaveRogueSmugglingStackData>())
            {
                Commodity commodity = _marketManager.ResolveCommodity(stack?.CommodityId);
                if (commodity?.IsContraband == true && stack.RemainingQuantity > 0)
                    _marketManager.TryAddCriminalSupply(origin, commodity, stack.RemainingQuantity, out _, out _);
            }
        }

        _pendingRebind.Clear();
        _rebindMode = false;
    }

    private bool TryRestorePendingShipment(
        NpcShip carrier,
        TrafficZoneConfig route,
        SaveRogueSmugglingShipmentData state,
        out RogueSmugglingShipment shipment)
    {
        shipment = null;
        if (state == null || !string.Equals(route.Id, state.RouteId, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(state.ShipmentIdentity, NpcIdentity.GetStableIdentity(carrier), StringComparison.Ordinal))
            return false;

        Station origin = FindMarketStation(state.OriginStationId);
        Station destination = FindMarketStation(state.DestinationStationId);
        if (origin == null || destination == null ||
            !string.Equals(_marketManager.GetStationId(origin), state.OriginStationId, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(_marketManager.GetStationId(destination), state.DestinationStationId, StringComparison.OrdinalIgnoreCase))
            return false;

        List<TraderCargoStack> stacks = new();
        foreach (SaveRogueSmugglingStackData savedStack in state.Stacks.Take(MaximumCommodityTypes))
        {
            Commodity commodity = _marketManager.ResolveCommodity(savedStack?.CommodityId);
            int initial = Math.Clamp(savedStack?.InitialQuantity ?? 0, 0, MaximumShipmentQuantity);
            int remaining = Math.Clamp(savedStack?.RemainingQuantity ?? 0, 0, initial);
            if (commodity?.IsContraband == true && initial > 0)
                stacks.Add(new TraderCargoStack(commodity, initial, remaining));
        }

        int initialQuantity = stacks.Sum(stack => stack.InitialQuantity);
        int remainingQuantity = stacks.Sum(stack => stack.Quantity);
        if (stacks.Count == 0 || initialQuantity > MaximumShipmentQuantity ||
            (state.InitialQuantity > 0 && state.InitialQuantity != initialQuantity) ||
            remainingQuantity != Math.Max(0, state.RemainingQuantity))
            return false;

        if (!carrier.ConfigureTrafficRouteEndpoints(route.RouteStart, route.RouteEnd, state.RouteTowardEnd))
            return false;

        carrier.RestoreTrafficRouteState(
            state.Position?.ToVector3(carrier.Position) ?? carrier.Position,
            state.Velocity?.ToVector3(Vector3.Zero) ?? Vector3.Zero,
            state.RouteTowardEnd,
            state.TrafficAgeSeconds);
        carrier.TrafficLifetimeSeconds = MaximumTransitLifetimeSeconds;
        shipment = new RogueSmugglingShipment(
            carrier,
            route.Id,
            _marketManager.GetStationId(origin),
            _marketManager.GetStationId(destination),
            origin.Name,
            destination.Name,
            state.RouteTowardEnd,
            new TraderCargoManifest(stacks, preserveEmptyStacks: true),
            origin,
            destination);
        return true;
    }

    private bool TryBuildAndReserveManifest(
        Station origin,
        Station destination,
        NpcShip carrier,
        TrafficZoneConfig route,
        out TraderCargoManifest manifest)
    {
        manifest = null;
        if (origin == null || destination == null || !route.IsRogueSmugglingRoute ||
            !BlackMarketPolicy.IsEligibleHostFaction(_marketManager.GetMarketFactionId(origin)) ||
            !BlackMarketPolicy.IsEligibleHostFaction(_marketManager.GetMarketFactionId(destination)))
            return false;

        uint seed = StableHash($"{NpcIdentity.GetStableIdentity(carrier)}|{route.Id}|{_marketManager.GetStationId(origin)}|{_marketManager.GetStationId(destination)}");
        List<StationMarketListing> candidates = _marketManager.GetBlackMarketListingsForStation(origin)
            .Where(listing => listing?.Commodity?.IsContraband == true && listing.IsAvailable &&
                listing.Stock > listing.MinimumStock &&
                _marketManager.GetAvailableCriminalSupplyCapacity(destination, listing.Commodity) > 0 &&
                _marketManager.CanReceiveCriminalSupply(destination, listing.Commodity, 1, out _))
            .OrderBy(listing => listing.Commodity.Id, StringComparer.OrdinalIgnoreCase)
            .Take(MaximumCommodityTypes)
            .ToList();
        if (candidates.Count == 0)
            return false;

        List<(Commodity Commodity, int Quantity)> requested = new();
        int desiredTotal = MinimumShipmentQuantity + (int)(seed % (uint)(MaximumShipmentQuantity - MinimumShipmentQuantity + 1));
        for (int index = 0; index < candidates.Count && requested.Count < MaximumCommodityTypes; index++)
        {
            StationMarketListing listing = candidates[index];
            int quantity = index == 0
                ? desiredTotal
                : 1 + (int)((seed >> (index * 5)) % 2u);
            int sourceAvailable = Math.Max(0, listing.Stock - listing.MinimumStock);
            int destinationCapacity = Math.Max(0, _marketManager.GetAvailableCriminalSupplyCapacity(destination, listing.Commodity));
            quantity = Math.Min(quantity, Math.Min(sourceAvailable, destinationCapacity));
            while (quantity >= 1 &&
                   !_marketManager.CanReceiveCriminalSupply(destination, listing.Commodity, quantity, out _))
                quantity--;
            if (quantity <= 0)
                continue;

            requested.Add((listing.Commodity, quantity));
            if (requested.Sum(entry => entry.Quantity) >= desiredTotal)
                break;
        }

        if (requested.Count == 0 || requested.Sum(entry => entry.Quantity) < MinimumShipmentQuantity)
            return false;

        List<(Commodity Commodity, int Quantity)> reserved = new();
        foreach ((Commodity Commodity, int Quantity) entry in requested)
        {
            if (!_marketManager.TryRemoveCriminalSupply(origin, entry.Commodity, entry.Quantity, minimumRemainingStock: 0, out _))
            {
                RestoreOriginStock(origin, reserved);
                return false;
            }

            reserved.Add(entry);
        }

        manifest = new TraderCargoManifest(reserved.Select(entry => new TraderCargoStack(entry.Commodity, entry.Quantity)));
        return manifest.InitialQuantity > 0;
    }

    private bool TryResolveRouteStations(
        TrafficZoneConfig route,
        NpcShip carrier,
        out Station origin,
        out Station destination,
        out bool towardEnd)
    {
        origin = FindMarketStation(route.OriginStationId);
        destination = FindMarketStation(route.DestinationStationId);
        towardEnd = true;
        if (origin == null || destination == null ||
            !route.RouteStart.HasValue || !route.RouteEnd.HasValue)
            return false;

        towardEnd = Vector3.DistanceSquared(carrier.Position, route.RouteStart.Value) <=
            Vector3.DistanceSquared(carrier.Position, route.RouteEnd.Value);
        if (!towardEnd)
        {
            (origin, destination) = (destination, origin);
        }

        return !ReferenceEquals(origin, destination);
    }

    private IEnumerable<TrafficZoneConfig> GetEligibleRoutes() =>
        (_routesProvider?.Invoke() ?? Array.Empty<TrafficZoneConfig>())
            .Where(route => route != null && route.IsRogueSmugglingRoute &&
                route.BehaviorType == TrafficZoneBehaviorType.TraderRoute &&
                !string.IsNullOrWhiteSpace(route.OriginStationId) &&
                !string.IsNullOrWhiteSpace(route.DestinationStationId))
            .OrderBy(route => route.Id ?? string.Empty, StringComparer.OrdinalIgnoreCase);

    private TrafficZoneConfig FindRoute(string routeId) =>
        GetEligibleRoutes().FirstOrDefault(route =>
            string.Equals(route.Id, routeId, StringComparison.OrdinalIgnoreCase));

    private Station FindMarketStation(string stationId)
    {
        if (string.IsNullOrWhiteSpace(stationId))
            return null;

        return (_stationsProvider?.Invoke() ?? Array.Empty<Station>())
            .Where(station => station != null && _marketManager.HasMarketConfigForStation(station))
            .FirstOrDefault(station => string.Equals(
                _marketManager.GetStationId(station), stationId.Trim(), StringComparison.OrdinalIgnoreCase));
    }

    private void RestoreOriginStock(Station origin, TraderCargoManifest manifest)
    {
        if (origin == null || manifest == null)
            return;

        RestoreOriginStock(origin, manifest.Snapshot()
            .Where(stack => stack?.Commodity != null && stack.Quantity > 0)
            .Select(stack => (stack.Commodity, stack.Quantity)));
    }

    private void RestoreOriginStock(Station origin, IEnumerable<(Commodity Commodity, int Quantity)> stacks)
    {
        foreach ((Commodity Commodity, int Quantity) stack in stacks ?? Enumerable.Empty<(Commodity Commodity, int Quantity)>())
        {
            if (stack.Commodity?.IsContraband == true && stack.Quantity > 0)
                _marketManager.TryAddCriminalSupply(origin, stack.Commodity, stack.Quantity, out _, out _);
        }
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
