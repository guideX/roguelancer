using Microsoft.Xna.Framework;
using Roguelancer.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Roguelancer;

public enum EconomicShipmentSettlement
{
    Active,
    Delivered,
    Lost
}

/// <summary>
/// One bounded physical ambient-trader shipment. The manifest is the same
/// mutable cargo object consumed by piracy and exposed to target scanning.
/// </summary>
public sealed class EconomicShipment
{
    internal EconomicShipment(
        NpcShip trader,
        string routeId,
        string originStationId,
        string destinationStationId,
        string originStationName,
        string destinationStationName,
        bool destinationIsRouteEnd,
        TraderCargoManifest manifest)
    {
        Trader = trader;
        TraderIdentity = NpcIdentity.GetStableIdentity(trader);
        RouteId = routeId ?? string.Empty;
        OriginStationId = originStationId ?? string.Empty;
        DestinationStationId = destinationStationId ?? string.Empty;
        OriginStationName = originStationName ?? string.Empty;
        DestinationStationName = destinationStationName ?? string.Empty;
        DestinationIsRouteEnd = destinationIsRouteEnd;
        Manifest = manifest;
    }

    public NpcShip Trader { get; internal set; }
    public string TraderIdentity { get; }
    public string RouteId { get; }
    public string OriginStationId { get; }
    public string DestinationStationId { get; }
    public string OriginStationName { get; }
    public string DestinationStationName { get; }
    public bool DestinationIsRouteEnd { get; }
    public TraderCargoManifest Manifest { get; }
    public EconomicShipmentSettlement Settlement { get; internal set; } = EconomicShipmentSettlement.Active;
    public int InitialQuantity => Manifest?.InitialQuantity ?? 0;
    public int RemainingQuantity => Manifest?.RemainingQuantity ?? 0;
    public int RemovedQuantity => Manifest?.RemovedQuantity ?? 0;
}

/// <summary>
/// Owns the only production manifest for ordinary ambient trader cargo and
/// performs the two market mutations that bookend its physical journey.
/// </summary>
public sealed class EconomicShipmentManager
{
    public const int MaximumActiveShipments = 64;
    public const int MinimumShipmentQuantity = 2;
    public const int MaximumShipmentQuantity = 12;
    public const int MaximumCommodityTypes = 3;
    private const float EndpointStationMatchDistance = 500f;

    private sealed class PendingShipment
    {
        public SaveEconomicShipmentData Data { get; init; }
    }

    private readonly MarketManager _marketManager;
    private readonly Func<IEnumerable<Station>> _stationsProvider;
    private readonly Func<NpcShip, bool> _isMissionOwned;
    private readonly Dictionary<NpcShip, EconomicShipment> _active = new();
    private readonly Dictionary<string, PendingShipment> _pendingRebind = new(StringComparer.Ordinal);
    private AdaptiveTraderRoutingPlanner _routingPlanner;
    private Func<IEnumerable<TrafficZoneConfig>> _routesProvider;
    private bool _rebindMode;

    public EconomicShipmentManager(
        MarketManager marketManager,
        Func<IEnumerable<Station>> stationsProvider,
        Func<NpcShip, bool> isMissionOwned = null,
        Func<IEnumerable<TrafficZoneConfig>> routesProvider = null)
    {
        _marketManager = marketManager ?? throw new ArgumentNullException(nameof(marketManager));
        _stationsProvider = stationsProvider ?? (() => Array.Empty<Station>());
        _isMissionOwned = isMissionOwned ?? (_ => false);
        SetRouteProvider(routesProvider);
    }

    public MarketManager MarketManager => _marketManager;
    public int ActiveShipmentCount => _active.Count;
    public IReadOnlyList<EconomicShipment> ActiveShipments => _active.Values.ToList();

    /// <summary>
    /// Installs the current system's configured TraderRoute view. The traffic
    /// manager owns this view; the shipment manager only ranks its edges.
    /// </summary>
    public void SetRouteProvider(Func<IEnumerable<TrafficZoneConfig>> routesProvider)
    {
        _routesProvider = routesProvider ?? (() => Array.Empty<TrafficZoneConfig>());
        _routingPlanner = new AdaptiveTraderRoutingPlanner(
            _marketManager,
            _stationsProvider,
            _routesProvider,
            GetInboundQuantity);
    }

    public AdaptiveTraderRoutingPlanner RoutingPlanner => _routingPlanner;

    /// <summary>
    /// Derives advisory inbound supply from active physical manifests. Lost,
    /// delivered, and destroyed shipments therefore disappear immediately;
    /// piracy changes the value naturally when the shared manifest shrinks.
    /// Pending save snapshots are included only during the short rebind window.
    /// </summary>
    public int GetInboundQuantity(string destinationStationId, Commodity commodity)
    {
        if (string.IsNullOrWhiteSpace(destinationStationId) || commodity == null)
            return 0;

        long inbound = _active.Values
            .Where(shipment => shipment?.Settlement == EconomicShipmentSettlement.Active &&
                string.Equals(shipment.DestinationStationId, destinationStationId.Trim(), StringComparison.OrdinalIgnoreCase))
            .Sum(shipment => (long)(shipment.Manifest?.Stacks
                .Where(stack => string.Equals(stack.Commodity?.Id, commodity.Id, StringComparison.OrdinalIgnoreCase))
                .Sum(stack => Math.Max(0, stack.Quantity)) ?? 0));

        if (_rebindMode)
        {
            inbound += (_pendingRebind.Values ?? Enumerable.Empty<PendingShipment>())
                .Where(pending => pending?.Data != null &&
                    !_active.Values.Any(active => string.Equals(active?.TraderIdentity, pending.Data.TraderIdentity, StringComparison.Ordinal)) &&
                    string.Equals(pending.Data.DestinationStationId, destinationStationId.Trim(), StringComparison.OrdinalIgnoreCase))
                .Sum(pending => (long)(pending.Data?.Stacks ?? new List<SaveEconomicShipmentStackData>())
                    .Where(stack => string.Equals(stack?.CommodityId, commodity.Id, StringComparison.OrdinalIgnoreCase))
                    .Sum(stack => Math.Max(0, stack?.RemainingQuantity ?? 0)));
        }

        return (int)Math.Clamp(inbound, 0L, int.MaxValue);
    }

    public int GetInboundQuantity(Station destination, Commodity commodity) =>
        GetInboundQuantity(_marketManager.GetStationId(destination), commodity);

    public int GetInboundQuantity(string destinationStationId, string commodityId) =>
        GetInboundQuantity(destinationStationId, _marketManager.ResolveCommodity(commodityId));

    public bool TryGetRouteInfo(NpcShip trader, out PlayerTargetScanRouteInfo routeInfo)
    {
        routeInfo = null;
        if (!TryGetShipment(trader, out EconomicShipment shipment))
            return false;

        routeInfo = new PlayerTargetScanRouteInfo(
            shipment.RouteId,
            shipment.OriginStationId,
            shipment.OriginStationName,
            shipment.DestinationStationId,
            shipment.DestinationStationName);
        return true;
    }

    public bool TryGetShipment(NpcShip trader, out EconomicShipment shipment)
    {
        shipment = null;
        return trader != null && _active.TryGetValue(trader, out shipment) &&
            shipment?.Settlement == EconomicShipmentSettlement.Active;
    }

    public TraderCargoManifest GetManifest(NpcShip trader) =>
        TryGetShipment(trader, out EconomicShipment shipment) ? shipment.Manifest : null;

    public bool TryGetManifestSnapshot(NpcShip trader, out NpcCargoManifestSnapshot snapshot)
    {
        snapshot = null;
        if (!TryGetShipment(trader, out EconomicShipment shipment))
            return false;

        snapshot = new NpcCargoManifestSnapshot(
            hasRegisteredCargo: true,
            shipment.Manifest?.Stacks.Select(stack =>
                new NpcCargoManifestStackSnapshot(stack.Commodity, stack.Quantity)));
        return true;
    }

    /// <summary>
    /// Attaches a trader after the NPC itself has been created. No stock is
    /// touched until the NPC exists, so population-cap and model failures do
    /// not create orphan reservations.
    /// </summary>
    public bool TryAttachTrader(NpcShip trader, TrafficZoneConfig route, out EconomicShipment shipment)
    {
        shipment = null;
        if (trader == null || route == null || trader.IsDestroyed ||
            trader.TrafficBehavior != TrafficZoneBehaviorType.TraderRoute ||
            _isMissionOwned(trader) || _active.ContainsKey(trader) ||
            _active.Count >= MaximumActiveShipments)
            return false;

        string traderIdentity = NpcIdentity.GetStableIdentity(trader);
        bool hasPendingShipment = _pendingRebind.TryGetValue(traderIdentity, out PendingShipment pending);
        if (_rebindMode && !hasPendingShipment)
            return false;

        AdaptiveTraderRoutePlan plan = null;
        TrafficZoneConfig selectedRoute = route;
        bool towardEnd;
        Station routeStart;
        Station routeEnd;

        if (_rebindMode && hasPendingShipment)
        {
            selectedRoute = FindConfiguredRoute(pending.Data?.RouteId) ??
                (string.Equals(route.Id, pending.Data?.RouteId, StringComparison.OrdinalIgnoreCase) ? route : null);
            if (selectedRoute == null || !TryResolveRouteStations(trader, selectedRoute, out routeStart, out routeEnd))
                return false;

            towardEnd = pending.Data.RouteTowardEnd;
        }
        else
        {
            if (!_routingPlanner.TrySelectDestination(trader, route, out plan) || plan == null)
                return false;

            selectedRoute = plan.Route;
            routeStart = plan.RouteTowardEnd ? plan.Origin : plan.Destination;
            routeEnd = plan.RouteTowardEnd ? plan.Destination : plan.Origin;
            towardEnd = plan.RouteTowardEnd;
        }

        Station origin = towardEnd ? routeStart : routeEnd;
        Station destination = towardEnd ? routeEnd : routeStart;
        string originId = _marketManager.GetStationId(origin);
        string destinationId = _marketManager.GetStationId(destination);
        if (string.IsNullOrWhiteSpace(originId) || string.IsNullOrWhiteSpace(destinationId) ||
            string.Equals(originId, destinationId, StringComparison.OrdinalIgnoreCase))
            return false;

        if (_rebindMode && hasPendingShipment)
        {
            if (!TryRestorePendingShipment(trader, selectedRoute, origin, destination, pending.Data, towardEnd, out shipment))
                return false;

            _pendingRebind.Remove(traderIdentity);
            _active[trader] = shipment;
            return true;
        }

        if (plan == null || !TryBuildAndReserveManifest(trader, selectedRoute, origin, destination, plan, out TraderCargoManifest manifest))
            return false;

        if (!trader.ConfigureTrafficRouteEndpoints(selectedRoute.RouteStart, selectedRoute.RouteEnd, towardEnd))
        {
            RollbackManifest(origin, manifest);
            return false;
        }

        shipment = new EconomicShipment(
            trader,
            selectedRoute.Id,
            originId,
            destinationId,
            origin.Name,
            destination.Name,
            destinationIsRouteEnd: towardEnd,
            manifest);
        _active[trader] = shipment;
        return true;
    }

    public void NotifyRouteEndpointReached(NpcShip trader, bool reachedRouteEnd, Action<string> log = null)
    {
        if (!TryGetShipment(trader, out EconomicShipment shipment) ||
            shipment.DestinationIsRouteEnd != reachedRouteEnd)
            return;

        TryDeliver(trader, log);
    }

    public bool TryDeliver(NpcShip trader, Action<string> log = null)
    {
        if (!TryGetShipment(trader, out EconomicShipment shipment))
            return false;

        Station destination = FindMarketStation(shipment.DestinationStationId);
        if (destination == null)
        {
            MarkLost(trader);
            return false;
        }

        int delivered = 0;
        foreach (TraderCargoStack stack in shipment.Manifest.Snapshot())
        {
            int remaining = stack.Quantity;
            if (remaining <= 0)
                continue;

            int accepted = Math.Min(remaining, _marketManager.GetAvailableSupplyCapacity(destination, stack.Commodity));
            if (accepted > 0 && _marketManager.TryAddSupply(destination, stack.Commodity, accepted, out _))
            {
                stack.Remove(accepted);
                delivered += accepted;
            }
        }

        int overflowLost = shipment.Manifest.RemainingQuantity;
        foreach (TraderCargoStack stack in shipment.Manifest.Snapshot())
            stack.Remove(stack.Quantity);

        shipment.Settlement = EconomicShipmentSettlement.Delivered;
        _active.Remove(trader);
        log?.Invoke($"[ECONOMY] Delivered {delivered} units from {shipment.OriginStationName} to {shipment.DestinationStationName}; overflow/loss={overflowLost}.");
        return true;
    }

    public void NotifyTraderDestroyed(NpcShip trader)
    {
        if (TryGetShipment(trader, out EconomicShipment shipment))
            shipment.Settlement = EconomicShipmentSettlement.Lost;
    }

    private void MarkLost(NpcShip trader)
    {
        if (!_active.TryGetValue(trader, out EconomicShipment shipment))
            return;

        shipment.Settlement = EconomicShipmentSettlement.Lost;
        _active.Remove(trader);
    }

    public void FinalizeDestroyedTrader(NpcShip trader)
    {
        if (!_active.TryGetValue(trader, out EconomicShipment shipment))
            return;

        foreach (TraderCargoStack stack in shipment.Manifest.Snapshot())
            stack.Remove(stack.Quantity);
        shipment.Settlement = EconomicShipmentSettlement.Lost;
        _active.Remove(trader);
    }

    public IReadOnlyList<SalvageDrop> GetDestructionSalvage(NpcShip trader)
    {
        if (!_active.TryGetValue(trader, out EconomicShipment shipment) ||
            shipment.Settlement != EconomicShipmentSettlement.Lost)
            return null;

        return shipment.Manifest.Snapshot()
            .Where(stack => stack.Quantity > 0 && stack.Commodity != null)
            .Take(CombatSalvageService.MaxLiveSalvageObjects)
            .Select((stack, index) => new SalvageDrop(
                stack.Commodity.Id,
                stack.Quantity,
                index,
                CombatSalvageTier.Standard))
            .ToList();
    }

    public int ConsumeDestructionSalvage(NpcShip trader, string commodityId, int quantity)
    {
        if (!_active.TryGetValue(trader, out EconomicShipment shipment) ||
            shipment.Settlement != EconomicShipmentSettlement.Lost || quantity <= 0)
            return 0;

        TraderCargoStack stack = shipment.Manifest.Snapshot().FirstOrDefault(candidate =>
            string.Equals(candidate.Commodity?.Id, commodityId, StringComparison.OrdinalIgnoreCase));
        return stack?.Remove(quantity) ?? 0;
    }

    public void NotifyTraderDespawned(NpcShip trader, string reason)
    {
        if (!_active.TryGetValue(trader, out EconomicShipment shipment))
            return;

        shipment.Settlement = EconomicShipmentSettlement.Lost;
        _active.Remove(trader);
    }

    /// <summary>
    /// A deliberate system teardown refunds reservations to the same origin
    /// market. Normal far/lifetime cleanup uses NotifyTraderDespawned and is a
    /// genuine lost shipment; teardown is a world reset, not delivery.
    /// </summary>
    public void ResetForWorldTeardown(bool restoreOriginStock)
    {
        foreach (EconomicShipment shipment in _active.Values.ToList())
        {
            if (restoreOriginStock && shipment.Settlement == EconomicShipmentSettlement.Active)
            {
                Station origin = FindMarketStation(shipment.OriginStationId);
                foreach (TraderCargoStack stack in shipment.Manifest.Snapshot())
                {
                    if (origin != null && stack.Quantity > 0)
                        _marketManager.TryAddSupply(origin, stack.Commodity, stack.Quantity, out _);
                }
            }

            shipment.Settlement = EconomicShipmentSettlement.Lost;
        }

        _active.Clear();
    }

    public void Reset()
    {
        _active.Clear();
        _pendingRebind.Clear();
        _rebindMode = false;
    }

    public List<SaveEconomicShipmentData> CaptureState()
    {
        return _active.Values
            .Where(shipment => shipment?.Settlement == EconomicShipmentSettlement.Active && shipment.Manifest != null)
            .Take(MaximumActiveShipments)
            .Select(shipment => new SaveEconomicShipmentData
            {
                TraderIdentity = shipment.TraderIdentity,
                RouteId = shipment.RouteId,
                OriginStationId = shipment.OriginStationId,
                DestinationStationId = shipment.DestinationStationId,
                OriginStationName = shipment.OriginStationName,
                DestinationStationName = shipment.DestinationStationName,
                InitialQuantity = shipment.InitialQuantity,
                RemainingQuantity = shipment.RemainingQuantity,
                RouteTowardEnd = shipment.DestinationIsRouteEnd,
                Position = SaveVector3Data.From(shipment.Trader?.Position ?? Vector3.Zero),
                Velocity = SaveVector3Data.From(shipment.Trader?.Velocity ?? Vector3.Zero),
                TrafficAgeSeconds = shipment.Trader?.TrafficAgeSeconds ?? 0f,
                Stacks = shipment.Manifest.Stacks.Select(stack => new SaveEconomicShipmentStackData
                {
                    CommodityId = stack.Commodity?.Id ?? string.Empty,
                    InitialQuantity = stack.InitialQuantity,
                    RemainingQuantity = stack.Quantity
                }).ToList()
            })
            .ToList();
    }

    /// <summary>
    /// Enables a save/load rebind. Existing reservations are refunded by the
    /// world teardown, then matching reconstructed NPCs consume these snapshots
    /// without debiting origin stock a second time. Unmatched snapshots are
    /// refunded when CompleteRebind is called after traffic reconstruction.
    /// </summary>
    public void PrepareForSaveLoadRebind(IEnumerable<SaveEconomicShipmentData> states)
    {
        _pendingRebind.Clear();
        foreach (SaveEconomicShipmentData state in states ?? Array.Empty<SaveEconomicShipmentData>())
        {
            if (state == null || string.IsNullOrWhiteSpace(state.TraderIdentity) ||
                state.Stacks == null || state.Stacks.Count == 0)
                continue;

            _pendingRebind[state.TraderIdentity] = new PendingShipment { Data = state };
        }

        // An older save or a save taken after all shipments settled has no
        // active accounting to protect. Let newly spawned ambient traders
        // create fresh shipments normally in that case.
        _rebindMode = _pendingRebind.Count > 0;
    }

    public void CompleteRebind()
    {
        foreach (PendingShipment pending in _pendingRebind.Values.ToList())
        {
            SaveEconomicShipmentData state = pending.Data;
            Station origin = FindMarketStation(state.OriginStationId);
            if (origin == null)
                continue;

            foreach (SaveEconomicShipmentStackData stack in state.Stacks ?? new List<SaveEconomicShipmentStackData>())
            {
                Commodity commodity = _marketManager.ResolveCommodity(stack.CommodityId);
                if (commodity != null && stack.RemainingQuantity > 0)
                    _marketManager.TryAddSupply(origin, commodity, stack.RemainingQuantity, out _);
            }
        }

        _pendingRebind.Clear();
        _rebindMode = false;
    }

    private bool TryRestorePendingShipment(
        NpcShip trader,
        TrafficZoneConfig route,
        Station origin,
        Station destination,
        SaveEconomicShipmentData state,
        bool towardEnd,
        out EconomicShipment shipment)
    {
        shipment = null;
        string originId = _marketManager.GetStationId(origin);
        string destinationId = _marketManager.GetStationId(destination);
        if (!string.Equals(route.Id, state.RouteId, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(originId, state.OriginStationId, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(destinationId, state.DestinationStationId, StringComparison.OrdinalIgnoreCase) ||
            state.RouteTowardEnd != towardEnd)
            return false;

        List<TraderCargoStack> stacks = new();
        foreach (SaveEconomicShipmentStackData savedStack in state.Stacks ?? new List<SaveEconomicShipmentStackData>())
        {
            Commodity commodity = _marketManager.ResolveCommodity(savedStack?.CommodityId);
            int initial = Math.Clamp(savedStack?.InitialQuantity ?? 0, 0, MaximumShipmentQuantity);
            int remaining = Math.Clamp(savedStack?.RemainingQuantity ?? 0, 0, initial);
            if (commodity == null || initial <= 0)
                continue;
            stacks.Add(new TraderCargoStack(commodity, initial, remaining));
        }

        int initialQuantity = stacks.Sum(stack => stack.InitialQuantity);
        int remainingQuantity = stacks.Sum(stack => stack.Quantity);
        if (stacks.Count == 0 || stacks.Count > MaximumCommodityTypes || initialQuantity <= 0 ||
            initialQuantity > MaximumShipmentQuantity ||
            (state.InitialQuantity > 0 && initialQuantity != state.InitialQuantity) ||
            remainingQuantity != Math.Max(0, state.RemainingQuantity))
            return false;

        shipment = new EconomicShipment(
            trader,
            route.Id,
            originId,
            destinationId,
            origin.Name,
            destination.Name,
            towardEnd,
            new TraderCargoManifest(stacks, preserveEmptyStacks: true));
        if (!trader.ConfigureTrafficRouteEndpoints(route.RouteStart, route.RouteEnd, towardEnd))
        {
            shipment = null;
            return false;
        }

        trader.RestoreTrafficRouteState(
            state.Position?.ToVector3(trader.Position) ?? trader.Position,
            state.Velocity?.ToVector3(Vector3.Zero) ?? Vector3.Zero,
            state.RouteTowardEnd,
            state.TrafficAgeSeconds);
        return true;
    }

    private bool TryBuildAndReserveManifest(
        NpcShip trader,
        TrafficZoneConfig route,
        Station origin,
        Station destination,
        AdaptiveTraderRoutePlan plan,
        out TraderCargoManifest manifest)
    {
        manifest = null;
        if (plan == null || plan.Origin == null || plan.Destination == null ||
            !ReferenceEquals(plan.Origin, origin) || !ReferenceEquals(plan.Destination, destination))
            return false;

        List<(StationMarketListing Listing, int Score)> candidates = plan.CommodityOpportunities
            .Where(opportunity => opportunity?.Commodity != null)
            .Select(opportunity =>
            {
                StationMarketListing listing = _marketManager.GetListingForCommodity(origin, opportunity.Commodity);
                return (Listing: listing, Score: opportunity.Score);
            })
            .Where(candidate => candidate.Listing != null && candidate.Listing.IsAvailable && candidate.Listing.BuyPrice > 0 &&
                !candidate.Listing.Commodity.IsContraband && !candidate.Listing.Commodity.IsMissionCargo &&
                candidate.Listing.Stock > candidate.Listing.MinimumStock &&
                _marketManager.GetAvailableSupplyCapacity(destination, candidate.Listing.Commodity) > 0)
            .OrderByDescending(candidate => candidate.Score)
            .ThenBy(candidate => candidate.Listing.Commodity.Id, StringComparer.OrdinalIgnoreCase)
            .Take(MaximumCommodityTypes)
            .ToList();

        if (candidates.Count == 0)
            return false;

        uint seed = StableHash($"{NpcIdentity.GetStableIdentity(trader)}|{route.Id}|{_marketManager.GetStationId(origin)}|{_marketManager.GetStationId(destination)}");
        int desiredTotal = MinimumShipmentQuantity + (int)(seed % (uint)(MaximumShipmentQuantity - MinimumShipmentQuantity + 1));
        AdaptiveTraderCommodityOpportunity bestOpportunity = plan.BestOpportunity;
        if (bestOpportunity != null && bestOpportunity.RawDeficit > 0)
        {
            // In-transit supply is advisory, but it bounds another relief
            // shipment so a high live price cannot create a shortage convoy.
            int remainingNeed = bestOpportunity.EffectiveDeficit > 0
                ? bestOpportunity.EffectiveDeficit
                : MinimumShipmentQuantity;
            desiredTotal = Math.Min(desiredTotal, Math.Clamp(remainingNeed, MinimumShipmentQuantity, MaximumShipmentQuantity));
        }

        int typeCount = Math.Min(candidates.Count,
            Math.Min(desiredTotal, Math.Clamp(1 + (int)((seed >> 8) % MaximumCommodityTypes), 1, MaximumCommodityTypes)));
        if (typeCount <= 0)
            return false;

        List<(Commodity Commodity, int Quantity)> requested = candidates
            .Take(typeCount)
            .Select(candidate => (candidate.Listing.Commodity, 1))
            .ToList();

        int remainingToAllocate = desiredTotal - requested.Count;
        for (int i = 0; i < remainingToAllocate; i++)
        {
            int index = (int)((seed + (uint)(i * 3571) + 17u) % (uint)requested.Count);
            (Commodity Commodity, int Quantity) selected = requested[index];
            StationMarketListing listing = candidates.First(candidate =>
                string.Equals(candidate.Listing.Commodity.Id, selected.Commodity.Id, StringComparison.OrdinalIgnoreCase)).Listing;
            int available = Math.Max(0, listing.Stock - listing.MinimumStock);
            if (selected.Quantity < available)
                requested[index] = (selected.Commodity, selected.Quantity + 1);
        }

        requested = requested
            .Where(entry => entry.Quantity > 0)
            .Select(entry => (entry.Commodity, Quantity: Math.Min(entry.Quantity, MaximumShipmentQuantity)))
            .ToList();
        if (requested.Count == 0 || requested.Sum(entry => entry.Quantity) <= 0)
            return false;

        List<(Commodity Commodity, int Quantity)> reserved = new();
        foreach ((Commodity Commodity, int Quantity) entry in requested)
        {
            if (!_marketManager.CanRemoveSupply(origin, entry.Commodity, entry.Quantity, 0, out _))
            {
                RollbackReservation(origin, reserved);
                return false;
            }

            if (!_marketManager.TryRemoveSupply(origin, entry.Commodity, entry.Quantity, 0, out _))
            {
                RollbackReservation(origin, reserved);
                return false;
            }

            reserved.Add(entry);
        }

        manifest = new TraderCargoManifest(reserved.Select(entry => new TraderCargoStack(entry.Commodity, entry.Quantity)));
        return true;
    }

    private void RollbackManifest(Station origin, TraderCargoManifest manifest)
    {
        foreach (TraderCargoStack stack in manifest?.Snapshot() ?? Array.Empty<TraderCargoStack>())
        {
            if (stack?.Commodity != null && stack.Quantity > 0)
                _marketManager.TryAddSupply(origin, stack.Commodity, stack.Quantity, out _);
        }
    }

    private void RollbackReservation(Station origin, IEnumerable<(Commodity Commodity, int Quantity)> reserved)
    {
        foreach ((Commodity Commodity, int Quantity) entry in reserved ?? Enumerable.Empty<(Commodity Commodity, int Quantity)>())
            _marketManager.TryAddSupply(origin, entry.Commodity, entry.Quantity, out _);
    }

    private bool TryResolveRouteStations(
        NpcShip trader,
        TrafficZoneConfig route,
        out Station routeStart,
        out Station routeEnd)
    {
        bool hasExplicitOrigin = !string.IsNullOrWhiteSpace(route.OriginStationId);
        bool hasExplicitDestination = !string.IsNullOrWhiteSpace(route.DestinationStationId);
        if (hasExplicitOrigin || hasExplicitDestination)
        {
            routeStart = FindMarketStation(route.OriginStationId);
            routeEnd = FindMarketStation(route.DestinationStationId);
            return routeStart != null && routeEnd != null && !ReferenceEquals(routeStart, routeEnd);
        }

        // Coordinate inference is deliberately exact-ish and bounded. It
        // reuses actual station world positions; it never fabricates endpoints.
        routeStart = FindNearestMarketStation(route.RouteStart);
        routeEnd = FindNearestMarketStation(route.RouteEnd);
        return routeStart != null && routeEnd != null && !ReferenceEquals(routeStart, routeEnd);
    }

    private Station FindMarketStation(string stationId)
    {
        if (string.IsNullOrWhiteSpace(stationId))
            return null;

        return (_stationsProvider() ?? Array.Empty<Station>())
            .Where(station => station != null && _marketManager.HasMarketConfigForStation(station))
            .FirstOrDefault(station => string.Equals(
                _marketManager.GetStationId(station), stationId.Trim(), StringComparison.OrdinalIgnoreCase));
    }

    private TrafficZoneConfig FindConfiguredRoute(string routeId)
    {
        if (string.IsNullOrWhiteSpace(routeId))
            return null;

        return (_routesProvider() ?? Array.Empty<TrafficZoneConfig>())
            .Where(route => route != null && route.BehaviorType == TrafficZoneBehaviorType.TraderRoute)
            .OrderBy(route => route.Id ?? string.Empty, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(route => string.Equals(route.Id, routeId.Trim(), StringComparison.OrdinalIgnoreCase));
    }

    private Station FindNearestMarketStation(Vector3? position)
    {
        if (!position.HasValue || !TradeLaneStateSanitizer.IsFinite(position.Value))
            return null;

        return (_stationsProvider() ?? Array.Empty<Station>())
            .Where(station => station != null && _marketManager.HasMarketConfigForStation(station))
            .Select(station => (Station: station, Distance: Vector3.Distance(station.Position, position.Value)))
            .Where(candidate => candidate.Distance <= EndpointStationMatchDistance)
            .OrderBy(candidate => candidate.Distance)
            .ThenBy(candidate => _marketManager.GetStationId(candidate.Station), StringComparer.OrdinalIgnoreCase)
            .Select(candidate => candidate.Station)
            .FirstOrDefault();
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
