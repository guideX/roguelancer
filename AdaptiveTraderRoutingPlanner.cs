using Microsoft.Xna.Framework;
using Roguelancer.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Roguelancer;

/// <summary>
/// A read-only, bounded plan for one ordinary ambient trader. The route is
/// still a configured traffic edge; this type only ranks existing edges.
/// </summary>
public sealed class AdaptiveTraderRoutePlan
{
    internal AdaptiveTraderRoutePlan(
        TrafficZoneConfig route,
        Station origin,
        Station destination,
        bool routeTowardEnd,
        IReadOnlyList<AdaptiveTraderCommodityOpportunity> opportunities,
        bool isDefaultRoute)
    {
        Route = route;
        Origin = origin;
        Destination = destination;
        RouteTowardEnd = routeTowardEnd;
        CommodityOpportunities = opportunities ?? Array.Empty<AdaptiveTraderCommodityOpportunity>();
        IsDefaultRoute = isDefaultRoute;
    }

    public TrafficZoneConfig Route { get; }
    public Station Origin { get; }
    public Station Destination { get; }
    public bool RouteTowardEnd { get; }
    public bool IsDefaultRoute { get; }
    public IReadOnlyList<AdaptiveTraderCommodityOpportunity> CommodityOpportunities { get; }
    public AdaptiveTraderCommodityOpportunity BestOpportunity => CommodityOpportunities.FirstOrDefault();
    public Commodity BestCommodity => BestOpportunity?.Commodity;
    public int OpportunityScore => BestOpportunity?.Score ?? int.MinValue;
    public int RouteCostPenalty => BestOpportunity?.RouteCostPenalty ?? 0;

    public string DestinationStationId => Destination == null ? string.Empty : BestOpportunity?.DestinationStationId ?? string.Empty;
}

/// <summary>
/// One legal origin/destination/commodity opportunity evaluated from current
/// market state. It is deliberately a snapshot and has no mutation authority.
/// </summary>
public sealed class AdaptiveTraderCommodityOpportunity
{
    internal AdaptiveTraderCommodityOpportunity(
        Commodity commodity,
        string originStationId,
        string destinationStationId,
        int originStock,
        int exportableQuantity,
        int originBuyPrice,
        int destinationBuyPrice,
        int profitOpportunity,
        int rawDeficit,
        int inboundQuantity,
        int effectiveDeficit,
        int shortageBonus,
        int deficitUsefulnessBonus,
        int routeCostPenalty,
        int diversityBias,
        int score)
    {
        Commodity = commodity;
        OriginStationId = originStationId ?? string.Empty;
        DestinationStationId = destinationStationId ?? string.Empty;
        OriginStock = Math.Max(0, originStock);
        ExportableQuantity = Math.Max(0, exportableQuantity);
        OriginBuyPrice = Math.Max(0, originBuyPrice);
        DestinationBuyPrice = Math.Max(0, destinationBuyPrice);
        ProfitOpportunity = profitOpportunity;
        RawDeficit = Math.Max(0, rawDeficit);
        InboundQuantity = Math.Max(0, inboundQuantity);
        EffectiveDeficit = Math.Max(0, effectiveDeficit);
        ShortageBonus = Math.Max(0, shortageBonus);
        DeficitUsefulnessBonus = Math.Max(0, deficitUsefulnessBonus);
        RouteCostPenalty = Math.Max(0, routeCostPenalty);
        DiversityBias = Math.Max(0, diversityBias);
        Score = score;
    }

    public Commodity Commodity { get; }
    public string CommodityId => Commodity?.Id ?? string.Empty;
    public string OriginStationId { get; }
    public string DestinationStationId { get; }
    public int OriginStock { get; }
    public int ExportableQuantity { get; }
    public int OriginBuyPrice { get; }
    public int DestinationBuyPrice { get; }
    public int ProfitOpportunity { get; }
    public int RawDeficit { get; }
    public int InboundQuantity { get; }
    public int EffectiveDeficit { get; }
    public int ShortageBonus { get; }
    public int DeficitUsefulnessBonus { get; }
    public int RouteCostPenalty { get; }
    public int DiversityBias { get; }
    public int Score { get; }
}

/// <summary>
/// Bounded adaptive routing policy for ordinary economic traffic. It uses
/// only live MarketManager state and configured TraderRoute edges; it never
/// performs pathfinding or mutates stock.
/// </summary>
public sealed class AdaptiveTraderRoutingPlanner
{
    public const int MaximumCandidateDestinations = 8;
    public const int DynamicSelectionThreshold = 25;
    public const int MinimumShipmentOpportunityScore = -1_500;
    public const int MaximumShipmentOpportunityScore = 5_000;
    public const int MinimumProfitOpportunity = -1_000;
    public const int MaximumProfitOpportunity = 4_000;
    public const int MaximumShortageBonus = 250;
    public const int MaximumDeficitUsefulnessBonus = 150;
    public const int MaximumRouteCostPenalty = 250;
    public const int MaximumDiversityBias = 3;

    private const int ShortageBonusFloor = 50;
    private const int DeficitBonusPerUnit = 5;
    private const int RouteCostUnitsPerPoint = 1_000;

    private readonly MarketManager _marketManager;
    private readonly Func<IEnumerable<Station>> _stationsProvider;
    private readonly Func<IEnumerable<TrafficZoneConfig>> _routesProvider;
    private readonly Func<string, Commodity, int> _inboundQuantityResolver;

    public AdaptiveTraderRoutingPlanner(
        MarketManager marketManager,
        Func<IEnumerable<Station>> stationsProvider,
        Func<IEnumerable<TrafficZoneConfig>> routesProvider = null,
        Func<string, Commodity, int> inboundQuantityResolver = null)
    {
        _marketManager = marketManager ?? throw new ArgumentNullException(nameof(marketManager));
        _stationsProvider = stationsProvider ?? (() => Array.Empty<Station>());
        _routesProvider = routesProvider ?? (() => Array.Empty<TrafficZoneConfig>());
        _inboundQuantityResolver = inboundQuantityResolver ?? ((_, _) => 0);
    }

    /// <summary>
    /// Returns at most eight valid route/market candidates. Candidate ranking
    /// is deterministic and keeps the configured default route in the bounded
    /// set whenever it is valid, preserving authored traffic flavor.
    /// </summary>
    public IReadOnlyList<AdaptiveTraderRoutePlan> GetCandidateDestinations(
        NpcShip trader,
        TrafficZoneConfig defaultRoute)
    {
        if (trader == null || defaultRoute == null ||
            !TryResolveRoute(defaultRoute, out Station defaultStart, out Station defaultEnd) ||
            !TryGetRouteGeometry(defaultRoute, out _))
            return Array.Empty<AdaptiveTraderRoutePlan>();

        bool defaultTowardEnd = trader.IsTrafficRouteTowardEnd;
        Station origin = defaultTowardEnd ? defaultStart : defaultEnd;
        string originId = _marketManager.GetStationId(origin);
        if (string.IsNullOrWhiteSpace(originId))
            return Array.Empty<AdaptiveTraderRoutePlan>();

        List<TrafficZoneConfig> routes = new();
        AddRouteIfMissing(routes, defaultRoute);
        foreach (TrafficZoneConfig route in _routesProvider() ?? Array.Empty<TrafficZoneConfig>())
            AddRouteIfMissing(routes, route);

        List<AdaptiveTraderRoutePlan> allValid = new();
        foreach (TrafficZoneConfig route in routes
            .Where(candidate => candidate != null && candidate.BehaviorType == TrafficZoneBehaviorType.TraderRoute)
            .OrderBy(candidate => candidate.Id ?? string.Empty, StringComparer.OrdinalIgnoreCase))
        {
            if (defaultRoute.SystemIndex > 0 && route.SystemIndex > 0 && route.SystemIndex != defaultRoute.SystemIndex)
                continue;

            if (!TryResolveRoute(route, out Station routeStart, out Station routeEnd) ||
                !TryGetRouteGeometry(route, out float routeDistance))
                continue;

            if (!IsRouteAlignedWithStations(route, routeStart, routeEnd))
                continue;

            string routeStartId = _marketManager.GetStationId(routeStart);
            string routeEndId = _marketManager.GetStationId(routeEnd);
            bool towardEnd;
            Station destination;
            if (string.Equals(routeStartId, originId, StringComparison.OrdinalIgnoreCase))
            {
                towardEnd = true;
                destination = routeEnd;
            }
            else if (string.Equals(routeEndId, originId, StringComparison.OrdinalIgnoreCase))
            {
                towardEnd = false;
                destination = routeStart;
            }
            else
            {
                // This route is not a known direct edge from the trader's
                // current origin. No unrestricted graph search is attempted.
                continue;
            }

            if (destination == null ||
                string.Equals(_marketManager.GetStationId(destination), originId, StringComparison.OrdinalIgnoreCase))
                continue;

            List<AdaptiveTraderCommodityOpportunity> opportunities = EvaluateCommodityOpportunities(
                trader,
                origin,
                destination,
                route,
                routeDistance);
            if (opportunities.Count == 0)
                continue;

            allValid.Add(new AdaptiveTraderRoutePlan(
                route,
                origin,
                destination,
                towardEnd,
                opportunities,
                string.Equals(route.Id, defaultRoute.Id, StringComparison.OrdinalIgnoreCase)));
        }

        List<AdaptiveTraderRoutePlan> bounded = allValid
            .OrderByDescending(plan => plan.OpportunityScore)
            .ThenBy(plan => _marketManager.GetStationId(plan.Destination), StringComparer.OrdinalIgnoreCase)
            .ThenBy(plan => plan.Route?.Id ?? string.Empty, StringComparer.OrdinalIgnoreCase)
            .ThenBy(plan => plan.BestCommodity?.Id ?? string.Empty, StringComparer.OrdinalIgnoreCase)
            .Take(MaximumCandidateDestinations)
            .ToList();

        AdaptiveTraderRoutePlan defaultPlan = allValid.FirstOrDefault(plan => plan.IsDefaultRoute);
        if (defaultPlan != null && !bounded.Any(plan => plan.IsDefaultRoute))
        {
            if (bounded.Count == MaximumCandidateDestinations)
                bounded[bounded.Count - 1] = defaultPlan;
            else
                bounded.Add(defaultPlan);
        }

        return bounded
            .OrderByDescending(plan => plan.OpportunityScore)
            .ThenBy(plan => _marketManager.GetStationId(plan.Destination), StringComparer.OrdinalIgnoreCase)
            .ThenBy(plan => plan.Route?.Id ?? string.Empty, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public bool TrySelectDestination(
        NpcShip trader,
        TrafficZoneConfig defaultRoute,
        out AdaptiveTraderRoutePlan selectedPlan)
    {
        selectedPlan = null;
        IReadOnlyList<AdaptiveTraderRoutePlan> candidates = GetCandidateDestinations(trader, defaultRoute);
        if (candidates.Count == 0)
            return false;

        AdaptiveTraderRoutePlan best = candidates[0];
        AdaptiveTraderRoutePlan authoredDefault = candidates.FirstOrDefault(candidate => candidate.IsDefaultRoute);
        if (authoredDefault != null && !best.IsDefaultRoute &&
            best.OpportunityScore < authoredDefault.OpportunityScore + DynamicSelectionThreshold)
        {
            best = authoredDefault;
        }

        selectedPlan = best;
        return true;
    }

    /// <summary>
    /// Shared bounded formula: clamped price signal, bounded shortage and
    /// stock-deficit usefulness, minus route cost, plus a stable identity bias.
    /// Inbound supply only affects the effective deficit used by the two
    /// demand terms; it never changes authoritative stock.
    /// </summary>
    public static int CalculateOpportunityScore(
        int destinationValueMinusOriginValue,
        int shortageBonus,
        int deficitUsefulnessBonus,
        int routeCostPenalty,
        int diversityBias = 0)
    {
        long score = Math.Clamp(destinationValueMinusOriginValue, MinimumProfitOpportunity, MaximumProfitOpportunity);
        score += Math.Clamp(shortageBonus, 0, MaximumShortageBonus);
        score += Math.Clamp(deficitUsefulnessBonus, 0, MaximumDeficitUsefulnessBonus);
        score -= Math.Clamp(routeCostPenalty, 0, MaximumRouteCostPenalty);
        score += Math.Clamp(diversityBias, 0, MaximumDiversityBias);
        return (int)Math.Clamp(score, MinimumShipmentOpportunityScore, MaximumShipmentOpportunityScore);
    }

    private List<AdaptiveTraderCommodityOpportunity> EvaluateCommodityOpportunities(
        NpcShip trader,
        Station origin,
        Station destination,
        TrafficZoneConfig route,
        float routeDistance)
    {
        string originId = _marketManager.GetStationId(origin);
        string destinationId = _marketManager.GetStationId(destination);
        List<StationMarketListing> originListings = _marketManager
            .GetListingsForStation(origin, MarketSurface.Ordinary)
            .Where(listing => listing?.Commodity != null && listing.IsAvailable && listing.BuyPrice > 0 &&
                !listing.Commodity.IsContraband && !listing.Commodity.IsMissionCargo &&
                listing.Stock > listing.MinimumStock)
            .ToList();

        List<AdaptiveTraderCommodityOpportunity> opportunities = new();
        foreach (StationMarketListing originListing in originListings
            .OrderBy(listing => listing.Commodity.Id, StringComparer.OrdinalIgnoreCase))
        {
            StationMarketListing destinationListing = _marketManager.GetListingForCommodity(
                destination,
                originListing.Commodity,
                MarketSurface.Ordinary);
            if (destinationListing == null || !destinationListing.IsAvailable || destinationListing.BuyPrice <= 0 ||
                _marketManager.GetAvailableSupplyCapacity(destination, originListing.Commodity) <= 0)
                continue;

            MarketShortageState shortage = _marketManager.GetShortageState(destination, originListing.Commodity);
            int rawDeficit = Math.Max(0, destinationListing.BaselineStock - destinationListing.Stock);
            int inbound = Math.Max(0, _inboundQuantityResolver(destinationId, originListing.Commodity));
            int effectiveDeficit = Math.Max(0, rawDeficit - inbound);
            int shortageBonus = shortage?.IsShortage == true && effectiveDeficit > 0
                ? Math.Clamp(ShortageBonusFloor + (100 - shortage.StockPercent) * 2, ShortageBonusFloor, MaximumShortageBonus)
                : 0;
            int deficitBonus = Math.Clamp(effectiveDeficit * DeficitBonusPerUnit, 0, MaximumDeficitUsefulnessBonus);
            int routeCostPenalty = CalculateRouteCostPenalty(routeDistance);
            int profit = Math.Clamp(destinationListing.BuyPrice - originListing.BuyPrice,
                MinimumProfitOpportunity,
                MaximumProfitOpportunity);
            int diversity = CalculateDiversityBias(trader, route, originId, destinationId, originListing.Commodity.Id);
            int score = CalculateOpportunityScore(profit, shortageBonus, deficitBonus, routeCostPenalty, diversity);

            opportunities.Add(new AdaptiveTraderCommodityOpportunity(
                originListing.Commodity,
                originId,
                destinationId,
                originListing.Stock,
                Math.Max(0, originListing.Stock - originListing.MinimumStock),
                originListing.BuyPrice,
                destinationListing.BuyPrice,
                profit,
                rawDeficit,
                inbound,
                effectiveDeficit,
                shortageBonus,
                deficitBonus,
                routeCostPenalty,
                diversity,
                score));
        }

        return opportunities
            .OrderByDescending(opportunity => opportunity.Score)
            .ThenBy(opportunity => opportunity.CommodityId, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private bool TryResolveRoute(
        TrafficZoneConfig route,
        out Station routeStart,
        out Station routeEnd)
    {
        routeStart = null;
        routeEnd = null;
        if (route == null || route.BehaviorType != TrafficZoneBehaviorType.TraderRoute ||
            string.IsNullOrWhiteSpace(route.Id) || !TryGetRouteGeometry(route, out _))
            return false;

        List<Station> stations = (_stationsProvider() ?? Array.Empty<Station>())
            .Where(station => station != null && _marketManager.HasMarketConfigForStation(station))
            .ToList();
        if (!string.IsNullOrWhiteSpace(route.OriginStationId) || !string.IsNullOrWhiteSpace(route.DestinationStationId))
        {
            routeStart = FindStationById(stations, route.OriginStationId);
            routeEnd = FindStationById(stations, route.DestinationStationId);
            return routeStart != null && routeEnd != null && !ReferenceEquals(routeStart, routeEnd) &&
                IsRouteAlignedWithStations(route, routeStart, routeEnd);
        }

        routeStart = FindNearestMarketStation(stations, route.RouteStart);
        routeEnd = FindNearestMarketStation(stations, route.RouteEnd);
        return routeStart != null && routeEnd != null && !ReferenceEquals(routeStart, routeEnd) &&
            IsRouteAlignedWithStations(route, routeStart, routeEnd);
    }

    private static bool IsRouteAlignedWithStations(TrafficZoneConfig route, Station routeStart, Station routeEnd)
    {
        if (route?.RouteStart is not Vector3 start || route.RouteEnd is not Vector3 end ||
            routeStart == null || routeEnd == null)
            return false;

        const float maximumEndpointError = 500f;
        return Vector3.DistanceSquared(start, routeStart.Position) <= maximumEndpointError * maximumEndpointError &&
            Vector3.DistanceSquared(end, routeEnd.Position) <= maximumEndpointError * maximumEndpointError;
    }

    private Station FindStationById(IEnumerable<Station> stations, string stationId)
    {
        if (string.IsNullOrWhiteSpace(stationId))
            return null;

        return stations.FirstOrDefault(station => string.Equals(
            _marketManager.GetStationId(station),
            stationId.Trim(),
            StringComparison.OrdinalIgnoreCase));
    }

    private Station FindNearestMarketStation(IEnumerable<Station> stations, Vector3? position)
    {
        if (!position.HasValue || !TradeLaneStateSanitizer.IsFinite(position.Value))
            return null;

        return stations
            .Select(station => (Station: station, Distance: Vector3.Distance(station.Position, position.Value)))
            .Where(candidate => !float.IsNaN(candidate.Distance) && !float.IsInfinity(candidate.Distance) && candidate.Distance <= 500f)
            .OrderBy(candidate => candidate.Distance)
            .ThenBy(candidate => _marketManager.GetStationId(candidate.Station), StringComparer.OrdinalIgnoreCase)
            .Select(candidate => candidate.Station)
            .FirstOrDefault();
    }

    private static bool TryGetRouteGeometry(TrafficZoneConfig route, out float distance)
    {
        distance = 0f;
        if (route?.RouteStart is not Vector3 start || route.RouteEnd is not Vector3 end ||
            !TradeLaneStateSanitizer.IsFinite(start) || !TradeLaneStateSanitizer.IsFinite(end))
            return false;

        distance = Vector3.Distance(start, end);
        return !float.IsNaN(distance) && !float.IsInfinity(distance) && distance > 0f;
    }

    private static int CalculateRouteCostPenalty(float distance)
    {
        if (float.IsNaN(distance) || float.IsInfinity(distance) || distance <= 0f)
            return MaximumRouteCostPenalty;

        return Math.Clamp((int)MathF.Round(distance / RouteCostUnitsPerPoint), 0, MaximumRouteCostPenalty);
    }

    private static int CalculateDiversityBias(
        NpcShip trader,
        TrafficZoneConfig route,
        string originListId,
        string destinationId,
        string commodityId)
    {
        uint hash = StableHash(string.Join("|",
            NpcIdentity.GetStableIdentity(trader),
            route?.Id ?? string.Empty,
            originListId ?? string.Empty,
            destinationId ?? string.Empty,
            commodityId ?? string.Empty));
        return (int)(hash % (MaximumDiversityBias + 1));
    }

    private static void AddRouteIfMissing(List<TrafficZoneConfig> routes, TrafficZoneConfig route)
    {
        if (route == null || string.IsNullOrWhiteSpace(route.Id) || routes.Any(existing =>
            string.Equals(existing?.Id, route.Id, StringComparison.OrdinalIgnoreCase)))
            return;

        routes.Add(route);
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
