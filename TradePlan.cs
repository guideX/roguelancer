using System;
using System.Collections.Generic;
using System.Linq;

namespace Roguelancer;

public enum TradePlanStage
{
    GoToSource,
    AcquireCommodity,
    GoToDestination,
    SellCommodity,
    Complete
}

/// <summary>
/// A player-created trading intention. This is deliberately separate from
/// Mission and never reserves cargo, station stock, or a reward.
/// </summary>
public sealed class TradePlan
{
    internal TradePlan() { }

    public string SourceStationId { get; internal set; } = string.Empty;
    public string SourceStationName { get; internal set; } = string.Empty;
    public int SourceSystemIndex { get; internal set; }
    public string DestinationStationId { get; internal set; } = string.Empty;
    public string DestinationStationName { get; internal set; } = string.Empty;
    public int DestinationSystemIndex { get; internal set; }
    public string CommodityId { get; internal set; } = string.Empty;
    public string CommodityName { get; internal set; } = string.Empty;

    public int SourceBuyPriceSnapshot { get; internal set; }
    public int DestinationSellPriceSnapshot { get; internal set; }
    public long SourceObservedAtMilliseconds { get; internal set; }
    public long DestinationObservedAtMilliseconds { get; internal set; }
    public MarketObservationAgeBand SourceAgeBandSnapshot { get; internal set; }
    public MarketObservationAgeBand DestinationAgeBandSnapshot { get; internal set; }

    public float RouteDistanceUnits { get; internal set; }
    public int RouteHops { get; internal set; }
    public int OpportunityScore { get; internal set; }
    public int SuggestedQuantity { get; internal set; }
    public int InitialOrdinaryQuantity { get; internal set; }
    public int AcquiredQuantity { get; internal set; }
    public int PurchasedQuantity { get; internal set; }
    public int SoldQuantity { get; internal set; }
    public long PurchasedCost { get; internal set; }
    public long SoldProceeds { get; internal set; }
    public int AverageSourcePurchasePrice { get; internal set; }
    public int ActualSourceBuyPrice { get; internal set; }
    public int ActualDestinationSellPrice { get; internal set; }
    public int CurrentKnownSpread { get; internal set; }
    public TradePlanStage Stage { get; internal set; }
    public string WarningMessage { get; internal set; } = string.Empty;
    public string LastMarketChangeMessage { get; internal set; } = string.Empty;

    public bool CargoAcquired { get; internal set; }
    public bool HasAmbiguousProvenance { get; internal set; }
    public bool IsComplete => Stage == TradePlanStage.Complete;
    public bool SourceReached => Stage != TradePlanStage.GoToSource;
    public bool DestinationReached => Stage is TradePlanStage.SellCommodity or TradePlanStage.Complete;
    public long ProjectedGrossSpread => (long)DestinationSellPriceSnapshot - SourceBuyPriceSnapshot;
    public long ProjectedGrossResult => SafeMultiply(ProjectedGrossSpread, Math.Max(0, SuggestedQuantity));
    public bool HasMaterialPriceChange =>
        IsMaterialPriceChange(SourceBuyPriceSnapshot, ActualSourceBuyPrice) ||
        IsMaterialPriceChange(DestinationSellPriceSnapshot, ActualDestinationSellPrice);
    public bool HasExactRealizedMargin => !HasAmbiguousProvenance && PurchasedQuantity > 0 && SoldQuantity > 0;
    public long RealizedGrossMargin => HasExactRealizedMargin
        ? SoldProceeds - AllocateCost(PurchasedCost, PurchasedQuantity, SoldQuantity)
        : 0L;

    private static long SafeMultiply(long left, long right)
    {
        if (left <= 0 || right <= 0) return 0L;
        return left > long.MaxValue / right ? long.MaxValue : left * right;
    }

    private static long AllocateCost(long totalCost, int purchasedQuantity, int soldQuantity)
    {
        if (totalCost <= 0 || purchasedQuantity <= 0 || soldQuantity <= 0) return 0L;
        long boundedSold = Math.Min((long)purchasedQuantity, soldQuantity);
        return totalCost > long.MaxValue / boundedSold
            ? long.MaxValue
            : totalCost * boundedSold / purchasedQuantity;
    }

    internal static bool IsMaterialPriceChange(int previous, int current)
    {
        if (previous <= 0 || current <= 0 || previous == current) return false;
        long difference = Math.Abs((long)current - previous);
        return difference >= TradePlanPresentation.MaterialChangeMinimumCredits ||
            difference * 100L >= (long)previous * 5L;
    }

    public string NextStationId => Stage switch
    {
        TradePlanStage.GoToSource => SourceStationId,
        TradePlanStage.GoToDestination => DestinationStationId,
        _ => string.Empty
    };

    public string NextStationName => Stage switch
    {
        TradePlanStage.GoToSource => SourceStationName,
        TradePlanStage.GoToDestination => DestinationStationName,
        _ => string.Empty
    };

    public int NextStationSystemIndex => Stage switch
    {
        TradePlanStage.GoToSource => SourceSystemIndex,
        TradePlanStage.GoToDestination => DestinationSystemIndex,
        _ => 0
    };
}

/// <summary>Authoritative details for one normal commodity transaction.</summary>
public sealed class CommodityTransaction
{
    public string StationId { get; internal set; } = string.Empty;
    public string CommodityId { get; internal set; } = string.Empty;
    public int Quantity { get; internal set; }
    public int UnitPrice { get; internal set; }
    public bool IsPurchase { get; internal set; }
}

/// <summary>
/// Owns the one active player trade plan. It reads MarketIntelligence for
/// remembered facts and observes authoritative dealer/docking events, but it
/// is never a mission and never mutates the economy merely by being planned.
/// </summary>
public sealed class TradePlanManager
{
    private readonly MarketManager _marketManager;
    private readonly MarketIntelligence _marketIntelligence;
    private readonly MarketRouteAuthority _routeAuthority;
    private readonly CargoHold _cargoHold;
    private readonly PlayerCredits _playerCredits;

    public TradePlanManager(
        MarketManager marketManager,
        MarketIntelligence marketIntelligence,
        MarketRouteAuthority routeAuthority,
        CargoHold cargoHold,
        PlayerCredits playerCredits)
    {
        _marketManager = marketManager ?? throw new ArgumentNullException(nameof(marketManager));
        _marketIntelligence = marketIntelligence ?? throw new ArgumentNullException(nameof(marketIntelligence));
        _routeAuthority = routeAuthority ?? new MarketRouteAuthority();
        _cargoHold = cargoHold;
        _playerCredits = playerCredits;
    }

    public TradePlan ActivePlan { get; private set; }
    public TradePlan LastCompletedPlan { get; private set; }
    public TradePlanNavigationState NavigationState { get; private set; }
    public MarketIntelligence MarketIntelligence => _marketIntelligence;
    public MarketRouteAuthority RouteAuthority => _routeAuthority;

    public event Action<TradePlan> PlanChanged;

    public string NextNavigationStationId => ActivePlan?.NextStationId ?? string.Empty;

    public bool TryPlanNavigation(int currentSystemIndex, out TradePlanNavigationState state, out string failureReason)
    {
        bool success = TradePlanNavigation.TryPlanNextLeg(
            ActivePlan,
            currentSystemIndex,
            _marketIntelligence,
            _routeAuthority,
            out state,
            out failureReason);
        NavigationState = state;
        return success;
    }

    public void ClearNavigationState() => NavigationState = null;

    public int GetTradableQuantity(string commodityIdOrName)
    {
        Commodity commodity = _marketManager.ResolveCommodity(commodityIdOrName);
        return commodity == null || _cargoHold == null
            ? 0
            : _cargoHold.GetSellableCommodityQuantity(commodity.Name);
    }

    public bool TryCreatePlan(MarketOpportunity opportunity, out string message)
    {
        message = string.Empty;
        if (opportunity == null)
        {
            message = "No market opportunity selected.";
            return false;
        }

        if (opportunity.Type != MarketOpportunityType.TradeRoute ||
            string.IsNullOrWhiteSpace(opportunity.OriginStationId) ||
            string.IsNullOrWhiteSpace(opportunity.DestinationStationId))
        {
            message = "Exact source and destination quotes are required for a trade route.";
            return false;
        }

        if (string.Equals(opportunity.OriginStationId, opportunity.DestinationStationId, StringComparison.OrdinalIgnoreCase))
        {
            message = "A trade route needs two different stations.";
            return false;
        }

        Commodity commodity = _marketManager.ResolveCommodity(opportunity.CommodityId);
        if (!IsTradeableCommodity(commodity))
        {
            message = "Mission-only or invalid commodities cannot become trade plans.";
            return false;
        }

        if (!_marketIntelligence.TryGetKnownStation(opportunity.OriginStationId, out MarketKnowledgeStation sourceStation) ||
            !_marketIntelligence.TryGetKnownStation(opportunity.DestinationStationId, out MarketKnowledgeStation destinationStation) ||
            !sourceStation.TryGetObservation(commodity.Id, out MarketObservation sourceObservation) ||
            !destinationStation.TryGetObservation(commodity.Id, out MarketObservation destinationObservation))
        {
            message = "Both exact market observations must be known before plotting this route.";
            return false;
        }

        if (sourceObservation.BuyPrice <= 0 || destinationObservation.SellPrice <= 0 || sourceObservation.Stock <= 0)
        {
            message = "The remembered source stock or destination sell quote is unavailable.";
            return false;
        }

        long spread = (long)destinationObservation.SellPrice - sourceObservation.BuyPrice;
        if (spread <= 0)
        {
            message = "The remembered route has no positive gross spread.";
            return false;
        }

        if (!_routeAuthority.TryGetRoute(sourceStation, destinationStation, out MarketRouteMetric route) ||
            route == null || !route.IsReachable || route.DistanceUnits <= 0f)
        {
            message = "The existing navigation route authority cannot reach that station pair.";
            return false;
        }

        TradePlan candidate = new()
        {
            SourceStationId = sourceStation.StationId,
            SourceStationName = sourceStation.StationName,
            SourceSystemIndex = sourceStation.SystemIndex,
            DestinationStationId = destinationStation.StationId,
            DestinationStationName = destinationStation.StationName,
            DestinationSystemIndex = destinationStation.SystemIndex,
            CommodityId = commodity.Id,
            CommodityName = commodity.Name,
            SourceBuyPriceSnapshot = sourceObservation.BuyPrice,
            DestinationSellPriceSnapshot = destinationObservation.SellPrice,
            SourceObservedAtMilliseconds = Math.Max(0L, sourceObservation.ObservedAtMilliseconds),
            DestinationObservedAtMilliseconds = Math.Max(0L, destinationObservation.ObservedAtMilliseconds),
            SourceAgeBandSnapshot = sourceObservation.GetAgeBand(_marketManager.ElapsedMilliseconds),
            DestinationAgeBandSnapshot = destinationObservation.GetAgeBand(_marketManager.ElapsedMilliseconds),
            RouteDistanceUnits = route.DistanceUnits,
            RouteHops = Math.Max(0, route.JumpCount),
            OpportunityScore = Math.Max(0, opportunity.Score),
            Stage = TradePlanStage.GoToSource
        };

        candidate.InitialOrdinaryQuantity = GetTradableQuantity(commodity.Id);
        candidate.SuggestedQuantity = CalculateSuggestedQuantity(sourceObservation, commodity, candidate.InitialOrdinaryQuantity);
        candidate.CurrentKnownSpread = ClampToInt(spread);

        Station currentStation = _marketIntelligence.CurrentStation;
        string currentStationId = _marketManager.GetStationId(currentStation);
        if (string.Equals(currentStationId, candidate.SourceStationId, StringComparison.OrdinalIgnoreCase))
        {
            candidate.Stage = candidate.InitialOrdinaryQuantity > 0
                ? TradePlanStage.GoToDestination
                : TradePlanStage.AcquireCommodity;
            candidate.CargoAcquired = candidate.InitialOrdinaryQuantity > 0;
            candidate.AcquiredQuantity = candidate.InitialOrdinaryQuantity;
            candidate.ActualSourceBuyPrice = sourceObservation.BuyPrice;
            candidate.HasAmbiguousProvenance = candidate.InitialOrdinaryQuantity > 0;
        }

        // Validate the complete candidate before replacing an existing plan so
        // failed selections are atomic and do not disturb player state.
        NavigationState = null;
        ActivePlan = candidate;
        LastCompletedPlan = null;
        UpdateGuidance();
        PlanChanged?.Invoke(ActivePlan);

        message = ActivePlan.SourceAgeBandSnapshot == MarketObservationAgeBand.Stale &&
            ActivePlan.DestinationAgeBandSnapshot == MarketObservationAgeBand.Stale
            ? "Trade plan created. WARNING: Both market quotes are stale."
            : "Trade plan created from remembered market intelligence.";
        return true;
    }

    public bool CancelActivePlan(out string message)
    {
        if (ActivePlan == null)
        {
            message = "No active trade plan.";
            return false;
        }

        ActivePlan = null;
        NavigationState = null;
        PlanChanged?.Invoke(null);
        message = "Trade plan cancelled. Cargo, credits, markets, and missions unchanged.";
        return true;
    }

    /// <summary>
    /// Called only at a legitimate docking boundary. This refreshes the local
    /// observation through the same player-visit authority as the dealer.
    /// </summary>
    public bool NotifyDocked(Station station, out string guidanceMessage)
    {
        guidanceMessage = string.Empty;
        if (ActivePlan == null || station == null) return false;

        string stationId = _marketManager.GetStationId(station);
        if (string.IsNullOrWhiteSpace(stationId)) return false;

        bool isSource = string.Equals(stationId, ActivePlan.SourceStationId, StringComparison.OrdinalIgnoreCase);
        bool isDestination = string.Equals(stationId, ActivePlan.DestinationStationId, StringComparison.OrdinalIgnoreCase);
        if (!isSource && !isDestination) return false;

        int previousRelevantPrice = 0;
        if (_marketIntelligence.TryGetObservation(stationId, ActivePlan.CommodityId, out MarketObservation previousObservation))
        {
            previousRelevantPrice = isSource ? previousObservation.BuyPrice : previousObservation.SellPrice;
        }

        _marketIntelligence.ObserveStation(station, "TradePlanArrival");
        if (_marketIntelligence.TryGetObservation(stationId, ActivePlan.CommodityId, out MarketObservation observation))
        {
            if (isSource) ActivePlan.ActualSourceBuyPrice = observation.BuyPrice;
            if (isDestination) ActivePlan.ActualDestinationSellPrice = observation.SellPrice;

            int currentRelevantPrice = isSource ? observation.BuyPrice : observation.SellPrice;
            if (previousRelevantPrice > 0 && currentRelevantPrice > 0 &&
                TradePlan.IsMaterialPriceChange(previousRelevantPrice, currentRelevantPrice))
            {
                string stationName = isSource ? ActivePlan.SourceStationName : ActivePlan.DestinationStationName;
                bool improved = isSource
                    ? currentRelevantPrice < previousRelevantPrice
                    : currentRelevantPrice > previousRelevantPrice;
                ActivePlan.LastMarketChangeMessage = improved
                    ? $"MARKET IMPROVED - {stationName} now {(isSource ? "sells" : "pays")} {currentRelevantPrice:N0} CR"
                    : $"MARKET UPDATE: {ActivePlan.CommodityName} now {currentRelevantPrice:N0} CR at {stationName}";
            }
        }

        int ordinaryQuantity = GetTradableQuantity(ActivePlan.CommodityId);
        if (isSource)
        {
            ActivePlan.AcquiredQuantity = Math.Max(ActivePlan.AcquiredQuantity, ordinaryQuantity);
            ActivePlan.CargoAcquired |= ordinaryQuantity > 0;
            ActivePlan.Stage = ActivePlan.CargoAcquired
                ? TradePlanStage.GoToDestination
                : TradePlanStage.AcquireCommodity;
            guidanceMessage = ActivePlan.CargoAcquired
                ? $"Source reached. Cargo available: {ordinaryQuantity:N0} {ActivePlan.CommodityName}. Destination is ready to plot."
                : $"Source reached. Buy {ActivePlan.CommodityName} through the normal commodity trader.";
        }
        else if (isDestination)
        {
            ActivePlan.Stage = TradePlanStage.SellCommodity;
            guidanceMessage = ordinaryQuantity > 0
                ? $"Destination reached. Sell ordinary {ActivePlan.CommodityName} cargo through the normal trader."
                : "Destination reached. No tradable cargo aboard.";
        }

        NavigationState = null;
        UpdateGuidance();
        PlanChanged?.Invoke(ActivePlan);
        return true;
    }

    /// <summary>Called after the dealer commits a normal buy or sale.</summary>
    public bool ObserveTransaction(CommodityTransaction transaction, out string message)
    {
        message = string.Empty;
        if (ActivePlan == null || transaction == null || transaction.Quantity <= 0 ||
            !string.Equals(transaction.CommodityId, ActivePlan.CommodityId, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        string sourceId = ActivePlan.SourceStationId;
        string destinationId = ActivePlan.DestinationStationId;
        bool atSource = string.Equals(transaction.StationId, sourceId, StringComparison.OrdinalIgnoreCase);
        bool atDestination = string.Equals(transaction.StationId, destinationId, StringComparison.OrdinalIgnoreCase);

        if (transaction.IsPurchase && atSource)
        {
            ActivePlan.PurchasedQuantity = SafeAdd(ActivePlan.PurchasedQuantity, transaction.Quantity);
            if (transaction.UnitPrice > 0)
            {
                ActivePlan.PurchasedCost = SafeAdd(ActivePlan.PurchasedCost, SafeMultiply(transaction.UnitPrice, transaction.Quantity));
                ActivePlan.AverageSourcePurchasePrice = ActivePlan.PurchasedQuantity > 0
                    ? (int)Math.Clamp(ActivePlan.PurchasedCost / ActivePlan.PurchasedQuantity, 0L, int.MaxValue)
                    : 0;
            }
            else
            {
                ActivePlan.HasAmbiguousProvenance = true;
            }
            ActivePlan.CargoAcquired = GetTradableQuantity(ActivePlan.CommodityId) > 0;
            ActivePlan.AcquiredQuantity = Math.Max(ActivePlan.AcquiredQuantity, GetTradableQuantity(ActivePlan.CommodityId));
            if (ActivePlan.CargoAcquired)
            {
                ActivePlan.Stage = TradePlanStage.GoToDestination;
                message = $"Purchased {transaction.Quantity:N0} {ActivePlan.CommodityName}. Cargo: {GetTradableQuantity(ActivePlan.CommodityId):N0}. Next: {ActivePlan.DestinationStationName}.";
            }
        }
        else if (!transaction.IsPurchase && atDestination)
        {
            ActivePlan.SoldQuantity = SafeAdd(ActivePlan.SoldQuantity, transaction.Quantity);
            if (transaction.UnitPrice > 0)
            {
                ActivePlan.SoldProceeds = SafeAdd(ActivePlan.SoldProceeds, SafeMultiply(transaction.UnitPrice, transaction.Quantity));
            }
            else
            {
                ActivePlan.HasAmbiguousProvenance = true;
            }
            if (ActivePlan.CargoAcquired && GetTradableQuantity(ActivePlan.CommodityId) <= 0)
            {
                ActivePlan.Stage = TradePlanStage.Complete;
                ActivePlan.WarningMessage = string.Empty;
                LastCompletedPlan = ActivePlan;
                TradePlan completed = ActivePlan;
                ActivePlan = null;
                NavigationState = null;
                PlanChanged?.Invoke(completed);
                message = BuildCompletionMessage(completed);
                return true;
            }

            string margin = ActivePlan.HasExactRealizedMargin
                ? $" Trade margin: {TradePlanPresentation.FormatSigned(ActivePlan.RealizedGrossMargin)} CR."
                : string.Empty;
            message = $"Sold {transaction.Quantity:N0} {ActivePlan.CommodityName} at {ActivePlan.DestinationStationName}. Remaining cargo: {GetTradableQuantity(ActivePlan.CommodityId):N0}.{margin}";
        }
        else if (transaction.IsPurchase)
        {
            ActivePlan.HasAmbiguousProvenance = true;
            return false;
        }
        else
        {
            return false;
        }

        NavigationState = null;
        UpdateGuidance();
        PlanChanged?.Invoke(ActivePlan);
        return true;
    }

    public List<string> GetDisplayLines()
    {
        return ActivePlan == null ? new List<string>() : GetPresentation(0).HudLines.ToList();
    }

    public List<string> GetCompactDisplayLines(int maxLines = 4)
    {
        return GetDisplayLines().Take(Math.Max(0, maxLines)).ToList();
    }

    public TradePlanPresentationState GetPresentation(int currentSystemIndex = 0, Func<int, string> systemNameResolver = null)
    {
        if (ActivePlan == null) return TradePlanPresentationState.Empty;
        return TradePlanPresentation.Build(
            ActivePlan,
            NavigationState,
            _marketIntelligence,
            _routeAuthority,
            currentSystemIndex,
            systemNameResolver,
            GetTradableQuantity(ActivePlan.CommodityId));
    }

    public TradePlanPresentationState GetCompletedPresentation(Func<int, string> systemNameResolver = null)
    {
        if (LastCompletedPlan == null) return TradePlanPresentationState.Empty;
        return TradePlanPresentation.Build(
            LastCompletedPlan,
            null,
            _marketIntelligence,
            _routeAuthority,
            LastCompletedPlan.DestinationSystemIndex,
            systemNameResolver,
            0);
    }

    public string ConsumeMarketUpdateMessage()
    {
        if (ActivePlan == null || string.IsNullOrWhiteSpace(ActivePlan.LastMarketChangeMessage)) return string.Empty;
        string message = ActivePlan.LastMarketChangeMessage;
        ActivePlan.LastMarketChangeMessage = string.Empty;
        return message;
    }

    public SaveTradePlanData CaptureState()
    {
        if (ActivePlan == null) return null;
        return new SaveTradePlanData
        {
            SourceStationId = ActivePlan.SourceStationId,
            SourceStationName = ActivePlan.SourceStationName,
            SourceSystemIndex = ActivePlan.SourceSystemIndex,
            DestinationStationId = ActivePlan.DestinationStationId,
            DestinationStationName = ActivePlan.DestinationStationName,
            DestinationSystemIndex = ActivePlan.DestinationSystemIndex,
            CommodityId = ActivePlan.CommodityId,
            CommodityName = ActivePlan.CommodityName,
            SourceBuyPrice = ActivePlan.SourceBuyPriceSnapshot,
            DestinationSellPrice = ActivePlan.DestinationSellPriceSnapshot,
            SourceObservedAtMilliseconds = ActivePlan.SourceObservedAtMilliseconds,
            DestinationObservedAtMilliseconds = ActivePlan.DestinationObservedAtMilliseconds,
            Stage = ActivePlan.Stage,
            RouteDistanceUnits = ActivePlan.RouteDistanceUnits,
            RouteHops = ActivePlan.RouteHops,
            OpportunityScore = ActivePlan.OpportunityScore,
            SuggestedQuantity = ActivePlan.SuggestedQuantity,
            InitialOrdinaryQuantity = ActivePlan.InitialOrdinaryQuantity,
            AcquiredQuantity = ActivePlan.AcquiredQuantity,
            PurchasedQuantity = ActivePlan.PurchasedQuantity,
            SoldQuantity = ActivePlan.SoldQuantity,
            PurchasedCost = ActivePlan.PurchasedCost,
            SoldProceeds = ActivePlan.SoldProceeds,
            AverageSourcePurchasePrice = ActivePlan.AverageSourcePurchasePrice,
            ActualSourceBuyPrice = ActivePlan.ActualSourceBuyPrice,
            ActualDestinationSellPrice = ActivePlan.ActualDestinationSellPrice,
            CargoAcquired = ActivePlan.CargoAcquired,
            HasAmbiguousProvenance = ActivePlan.HasAmbiguousProvenance
        };
    }

    public void RestoreState(SaveTradePlanData saved)
    {
        ActivePlan = null;
        LastCompletedPlan = null;
        NavigationState = null;
        if (saved == null || !IsValidSavedPlan(saved))
        {
            PlanChanged?.Invoke(null);
            return;
        }

        Commodity commodity = _marketManager.ResolveCommodity(saved.CommodityId);
        ActivePlan = new TradePlan
        {
            SourceStationId = saved.SourceStationId,
            SourceStationName = saved.SourceStationName ?? saved.SourceStationId,
            SourceSystemIndex = saved.SourceSystemIndex > 0
                ? saved.SourceSystemIndex
                : ResolveSavedStationSystem(saved.SourceStationId),
            DestinationStationId = saved.DestinationStationId,
            DestinationStationName = saved.DestinationStationName ?? saved.DestinationStationId,
            DestinationSystemIndex = saved.DestinationSystemIndex > 0
                ? saved.DestinationSystemIndex
                : ResolveSavedStationSystem(saved.DestinationStationId),
            CommodityId = commodity.Id,
            CommodityName = commodity.Name,
            SourceBuyPriceSnapshot = saved.SourceBuyPrice,
            DestinationSellPriceSnapshot = saved.DestinationSellPrice,
            SourceObservedAtMilliseconds = saved.SourceObservedAtMilliseconds,
            DestinationObservedAtMilliseconds = saved.DestinationObservedAtMilliseconds,
            SourceAgeBandSnapshot = GetSavedAgeBand(saved.SourceObservedAtMilliseconds),
            DestinationAgeBandSnapshot = GetSavedAgeBand(saved.DestinationObservedAtMilliseconds),
            Stage = saved.Stage,
            RouteDistanceUnits = saved.RouteDistanceUnits,
            RouteHops = saved.RouteHops,
            OpportunityScore = Math.Max(0, saved.OpportunityScore),
            SuggestedQuantity = Math.Max(0, saved.SuggestedQuantity),
            InitialOrdinaryQuantity = Math.Max(0, saved.InitialOrdinaryQuantity),
            AcquiredQuantity = Math.Max(0, saved.AcquiredQuantity),
            PurchasedQuantity = Math.Max(0, saved.PurchasedQuantity),
            SoldQuantity = Math.Max(0, saved.SoldQuantity),
            PurchasedCost = Math.Max(0L, saved.PurchasedCost),
            SoldProceeds = Math.Max(0L, saved.SoldProceeds),
            AverageSourcePurchasePrice = Math.Max(0, saved.AverageSourcePurchasePrice),
            ActualSourceBuyPrice = Math.Max(0, saved.ActualSourceBuyPrice),
            ActualDestinationSellPrice = Math.Max(0, saved.ActualDestinationSellPrice),
            CargoAcquired = saved.CargoAcquired,
            HasAmbiguousProvenance = saved.HasAmbiguousProvenance
        };
        UpdateGuidance();
        PlanChanged?.Invoke(ActivePlan);
    }

    public void Clear()
    {
        ActivePlan = null;
        LastCompletedPlan = null;
        NavigationState = null;
        PlanChanged?.Invoke(null);
    }

    private void UpdateGuidance()
    {
        if (ActivePlan == null) return;

        int sourcePrice = ActivePlan.ActualSourceBuyPrice > 0
            ? ActivePlan.ActualSourceBuyPrice
            : ActivePlan.SourceBuyPriceSnapshot;
        int destinationPrice = ActivePlan.ActualDestinationSellPrice > 0
            ? ActivePlan.ActualDestinationSellPrice
            : ActivePlan.DestinationSellPriceSnapshot;
        ActivePlan.CurrentKnownSpread = ClampToInt((long)destinationPrice - sourcePrice);

        bool sourceChanged = TradePlan.IsMaterialPriceChange(
            ActivePlan.SourceBuyPriceSnapshot,
            ActivePlan.ActualSourceBuyPrice);
        bool destinationChanged = TradePlan.IsMaterialPriceChange(
            ActivePlan.DestinationSellPriceSnapshot,
            ActivePlan.ActualDestinationSellPrice);
        if ((sourceChanged || destinationChanged) && ActivePlan.CurrentKnownSpread <= 0)
        {
            ActivePlan.WarningMessage = "MARKET CHANGED - ROUTE NO LONGER PROFITABLE";
        }
        else if (sourceChanged || destinationChanged)
        {
            ActivePlan.WarningMessage = $"MARKET CHANGED - CURRENT SPREAD: {FormatSigned(ActivePlan.CurrentKnownSpread)} CR/unit";
        }
        else
        {
            ActivePlan.WarningMessage = string.Empty;
        }
    }

    private MarketObservationAgeBand GetCurrentAgeBand(string stationId, string commodityId)
    {
        return _marketIntelligence.TryGetObservation(stationId, commodityId, out MarketObservation observation)
            ? observation.GetAgeBand(_marketManager.ElapsedMilliseconds)
            : MarketObservationAgeBand.Stale;
    }

    private int CalculateSuggestedQuantity(MarketObservation source, Commodity commodity, int existingOrdinaryQuantity)
    {
        if (source == null || commodity == null || commodity.VolumePerUnit <= 0 || source.BuyPrice <= 0 || _cargoHold == null || _playerCredits == null)
            return 0;

        long volumeBound = Math.Max(0L, existingOrdinaryQuantity) + Math.Max(0L, _cargoHold.AvailableCapacity) / commodity.VolumePerUnit;
        long creditBound = Math.Max(0L, existingOrdinaryQuantity) + Math.Max(0L, _playerCredits.Credits) / source.BuyPrice;
        long bounded = Math.Min(Math.Max(0L, source.Stock), Math.Min(volumeBound, creditBound));
        return (int)Math.Clamp(bounded, 0L, int.MaxValue);
    }

    private bool IsValidSavedPlan(SaveTradePlanData saved)
    {
        if (string.IsNullOrWhiteSpace(saved.SourceStationId) || string.IsNullOrWhiteSpace(saved.DestinationStationId) ||
            string.Equals(saved.SourceStationId, saved.DestinationStationId, StringComparison.OrdinalIgnoreCase) ||
            !_marketManager.IsKnownStationId(saved.SourceStationId) || !_marketManager.IsKnownStationId(saved.DestinationStationId) ||
            string.IsNullOrWhiteSpace(saved.CommodityId) || !Enum.IsDefined(saved.Stage) || saved.Stage == TradePlanStage.Complete ||
            saved.SourceBuyPrice <= 0 || saved.DestinationSellPrice <= 0 || saved.SourceObservedAtMilliseconds < 0 ||
            saved.DestinationObservedAtMilliseconds < 0 || saved.RouteDistanceUnits <= 0f || float.IsNaN(saved.RouteDistanceUnits) ||
            float.IsInfinity(saved.RouteDistanceUnits) || saved.RouteHops < 0 || saved.SuggestedQuantity < 0 ||
            saved.InitialOrdinaryQuantity < 0 || saved.AcquiredQuantity < 0 || saved.PurchasedQuantity < 0 || saved.SoldQuantity < 0)
        {
            return false;
        }

        Commodity commodity = _marketManager.ResolveCommodity(saved.CommodityId);
        return IsTradeableCommodity(commodity);
    }

    private static bool IsTradeableCommodity(Commodity commodity) =>
        commodity != null && !commodity.IsMissionCargo && !string.IsNullOrWhiteSpace(commodity.Id) &&
        !string.IsNullOrWhiteSpace(commodity.Name) && commodity.VolumePerUnit > 0;

    private static int SafeAdd(int left, int right)
    {
        long sum = (long)Math.Max(0, left) + Math.Max(0, right);
        return (int)Math.Clamp(sum, 0L, int.MaxValue);
    }

    private static long SafeAdd(long left, long right)
    {
        if (left < 0 || right < 0) return 0L;
        return left > long.MaxValue - right ? long.MaxValue : left + right;
    }

    private static long SafeMultiply(int left, int right)
    {
        if (left <= 0 || right <= 0) return 0L;
        return left > long.MaxValue / right ? long.MaxValue : (long)left * right;
    }

    private static string BuildCompletionMessage(TradePlan completed)
    {
        if (completed == null) return "TRADE ROUTE COMPLETE";
        string margin = completed.HasExactRealizedMargin
            ? $" | Gross margin: {TradePlanPresentation.FormatSigned(completed.RealizedGrossMargin)} CR"
            : " | Gross margin: unavailable (cargo provenance mixed)";
        return $"TRADE ROUTE COMPLETE - {completed.SoldQuantity:N0} {completed.CommodityName} delivered{margin}";
    }

    private static int ClampToInt(long value) => (int)Math.Clamp(value, int.MinValue, int.MaxValue);

    private static string FormatSigned(int value) => value >= 0 ? $"+{value:N0}" : value.ToString("N0");

    private int ResolveSavedStationSystem(string stationId)
    {
        return _marketIntelligence.TryGetKnownStation(stationId, out MarketKnowledgeStation station)
            ? Math.Max(0, station.SystemIndex)
            : 0;
    }

    private MarketObservationAgeBand GetSavedAgeBand(long observedAt)
    {
        return observedAt > _marketManager.ElapsedMilliseconds
            ? MarketObservationAgeBand.Current
            : new MarketObservation
            {
                ObservedAtMilliseconds = observedAt
            }.GetAgeBand(_marketManager.ElapsedMilliseconds);
    }
}

/// <summary>
/// Resolves the next trade-plan station from the live current-system station
/// collection. Actual route construction remains GotoAutopilot's authority.
/// </summary>
public static partial class TradePlanNavigation
{
    public static bool TryResolveNextStation(
        TradePlan plan,
        IEnumerable<Station> currentSystemStations,
        MarketManager marketManager,
        out Station station,
        out string failureReason)
    {
        station = null;
        failureReason = string.Empty;
        if (plan == null || marketManager == null || string.IsNullOrWhiteSpace(plan.NextStationId))
        {
            failureReason = "trade plan has no flight destination";
            return false;
        }

        station = (currentSystemStations ?? Enumerable.Empty<Station>()).FirstOrDefault(candidate =>
            candidate != null && string.Equals(marketManager.GetStationId(candidate), plan.NextStationId, StringComparison.OrdinalIgnoreCase));
        if (station != null) return true;

        failureReason = "destination station is not loaded in the current system";
        return false;
    }
}
