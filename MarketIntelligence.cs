using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Roguelancer;

public enum MarketObservationAgeBand
{
    Current,
    Recent,
    Stale
}

public sealed class MarketObservation
{
    public string StationId { get; internal set; } = string.Empty;
    public string StationName { get; internal set; } = string.Empty;
    public int SystemIndex { get; internal set; }
    public Vector3 StationPosition { get; internal set; }
    public Commodity Commodity { get; internal set; }
    public int Stock { get; internal set; }
    public int BuyPrice { get; internal set; }
    public int SellPrice { get; internal set; }
    public int BaselineStock { get; internal set; }
    public int DemandLevel { get; internal set; }
    public string MarketCondition { get; internal set; } = string.Empty;
    public long ObservedAtMilliseconds { get; internal set; }
    public string Source { get; internal set; } = string.Empty;

    public long GetAgeMilliseconds(long currentMilliseconds)
    {
        if (currentMilliseconds <= ObservedAtMilliseconds) return 0L;
        return currentMilliseconds - ObservedAtMilliseconds;
    }

    public MarketObservationAgeBand GetAgeBand(long currentMilliseconds)
    {
        long age = GetAgeMilliseconds(currentMilliseconds);
        if (age < MarketIntelligence.CurrentThresholdMilliseconds) return MarketObservationAgeBand.Current;
        if (age < MarketIntelligence.RecentThresholdMilliseconds) return MarketObservationAgeBand.Recent;
        return MarketObservationAgeBand.Stale;
    }

    public string GetAgeLabel(long currentMilliseconds) => GetAgeBand(currentMilliseconds).ToString().ToUpperInvariant();
}

public sealed class MarketKnowledgeStation
{
    private readonly Dictionary<string, MarketObservation> _observations = new(StringComparer.OrdinalIgnoreCase);

    internal MarketKnowledgeStation(string stationId, string stationName, int systemIndex, Vector3 position)
    {
        StationId = stationId;
        StationName = stationName ?? string.Empty;
        SystemIndex = systemIndex;
        StationPosition = position;
    }

    public string StationId { get; }
    public string StationName { get; }
    public int SystemIndex { get; }
    public Vector3 StationPosition { get; }
    public IReadOnlyList<MarketObservation> Observations => _observations.Values.ToList();

    internal void SetObservation(MarketObservation observation)
    {
        if (observation?.Commodity == null || string.IsNullOrWhiteSpace(observation.Commodity.Id)) return;
        _observations[observation.Commodity.Id] = observation;
    }

    public bool TryGetObservation(string commodityId, out MarketObservation observation) =>
        _observations.TryGetValue(commodityId ?? string.Empty, out observation);
}

public sealed class MarketMissionIntel
{
    public string StationId { get; internal set; } = string.Empty;
    public string StationName { get; internal set; } = string.Empty;
    public string CommodityId { get; internal set; } = string.Empty;
    public string Condition { get; internal set; } = string.Empty;
    public int Quantity { get; internal set; }
    public int Reward { get; internal set; }
}

/// <summary>
/// Player-owned last-known market facts. It never writes to MarketManager and
/// only refreshes from a station when the player legitimately visits or uses
/// that station's market.
/// </summary>
public sealed class MarketIntelligence
{
    public const long CurrentThresholdMilliseconds = 15L * 60L * 1000L;
    public const long RecentThresholdMilliseconds = 2L * 60L * 60L * 1000L;

    private readonly MarketManager _marketManager;
    private readonly Dictionary<string, MarketKnowledgeStation> _knownStations = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<MarketMissionIntel> _missionIntel = new();
    private Station _currentStation;

    public MarketIntelligence(MarketManager marketManager)
    {
        _marketManager = marketManager ?? throw new ArgumentNullException(nameof(marketManager));
    }

    public MarketManager MarketManager => _marketManager;
    public long ElapsedMilliseconds => _marketManager.ElapsedMilliseconds;
    public Station CurrentStation => _currentStation;
    public IReadOnlyList<MarketMissionIntel> MissionIntel => _missionIntel.AsReadOnly();
    public IReadOnlyList<MarketKnowledgeStation> KnownStations => _knownStations.Values.ToList();

    public void SetCurrentStation(Station station)
    {
        _currentStation = station;
        if (station != null) ObserveStation(station, "CurrentStation");
    }

    public void ClearCurrentStation()
    {
        _currentStation = null;
    }

    public void RefreshCurrentStation()
    {
        if (_currentStation != null) ObserveStation(_currentStation, "CurrentStation");
    }

    public bool ObserveStation(Station station, string source = "Visited")
    {
        string stationId = _marketManager.GetStationId(station);
        if (station == null || string.IsNullOrWhiteSpace(stationId)) return false;

        MarketKnowledgeStation knowledge = new(
            stationId,
            station.Name,
            station.Config?.SystemIndex ?? 0,
            station.Position);

        foreach (StationMarketListing listing in _marketManager.GetListingsForStation(station) ?? new List<StationMarketListing>())
        {
            Commodity commodity = listing?.Commodity;
            if (!IsValidObservation(commodity, listing)) continue;

            knowledge.SetObservation(new MarketObservation
            {
                StationId = stationId,
                StationName = station.Name ?? string.Empty,
                SystemIndex = station.Config?.SystemIndex ?? 0,
                StationPosition = station.Position,
                Commodity = commodity,
                Stock = Math.Max(0, listing.Stock),
                BuyPrice = Math.Max(0, listing.BuyPrice),
                SellPrice = Math.Max(0, listing.SellPrice),
                BaselineStock = Math.Max(0, listing.BaselineStock),
                DemandLevel = Math.Max(0, listing.DemandLevel),
                MarketCondition = listing.MarketCondition ?? string.Empty,
                ObservedAtMilliseconds = _marketManager.ElapsedMilliseconds,
                Source = string.IsNullOrWhiteSpace(source) ? "Visited" : source
            });
        }

        if (knowledge.Observations.Count == 0) return false;
        _knownStations[stationId] = knowledge;
        return true;
    }

    public bool IsStationKnown(Station station) => station != null && IsStationKnown(_marketManager.GetStationId(station));

    public bool IsStationKnown(string stationIdOrName)
    {
        string stationId = ResolveStationId(stationIdOrName);
        return !string.IsNullOrWhiteSpace(stationId) && _knownStations.ContainsKey(stationId);
    }

    public bool TryGetObservation(string stationIdOrName, string commodityId, out MarketObservation observation)
    {
        observation = null;
        string stationId = ResolveStationId(stationIdOrName);
        return !string.IsNullOrWhiteSpace(stationId) &&
            _knownStations.TryGetValue(stationId, out MarketKnowledgeStation station) &&
            station.TryGetObservation(commodityId, out observation);
    }

    public bool TryGetKnownStation(string stationIdOrName, out MarketKnowledgeStation station)
    {
        station = null;
        string stationId = ResolveStationId(stationIdOrName);
        return !string.IsNullOrWhiteSpace(stationId) && _knownStations.TryGetValue(stationId, out station);
    }

    public void RecordMissionIntel(Mission mission)
    {
        if (mission == null || mission.Type is not (MissionType.FreightContract or MissionType.ExportContract)) return;

        Commodity commodity = _marketManager.ResolveCommodity(mission.CommodityId);
        if (commodity == null || commodity.IsMissionCargo || string.IsNullOrWhiteSpace(commodity.Id)) return;

        AddMissionIntel(mission.Destination, commodity.Id, "SHORTAGE", mission.RequiredQuantity, mission.Reward);
        if (mission.Type == MissionType.ExportContract)
            AddMissionIntel(mission.OriginStationName, commodity.Id, "SURPLUS", mission.RequiredQuantity, mission.Reward);
    }

    public List<SaveMarketIntelligenceData> CaptureState()
    {
        return _knownStations.Values
            .OrderBy(station => station.StationId, StringComparer.OrdinalIgnoreCase)
            .SelectMany(station => station.Observations
                .OrderBy(observation => observation.Commodity?.Id, StringComparer.OrdinalIgnoreCase)
                .Select(observation => new SaveMarketIntelligenceData
                {
                    StationId = observation.StationId,
                    StationName = observation.StationName,
                    SystemIndex = observation.SystemIndex,
                    StationPosition = SaveVector3Data.From(observation.StationPosition),
                    CommodityId = observation.Commodity?.Id ?? string.Empty,
                    Stock = observation.Stock,
                    BuyPrice = observation.BuyPrice,
                    SellPrice = observation.SellPrice,
                    BaselineStock = observation.BaselineStock,
                    DemandLevel = observation.DemandLevel,
                    MarketCondition = observation.MarketCondition,
                    ObservedAtMilliseconds = Math.Max(0L, observation.ObservedAtMilliseconds),
                    Source = observation.Source
                }))
            .ToList();
    }

    public void RestoreState(IEnumerable<SaveMarketIntelligenceData> state)
    {
        _knownStations.Clear();
        if (state == null) return;

        foreach (SaveMarketIntelligenceData saved in state)
        {
            if (saved == null || !_marketManager.IsKnownStationId(saved.StationId)) continue;
            Commodity commodity = _marketManager.ResolveCommodity(saved.CommodityId);
            if (!IsValidCommodity(commodity) || commodity.IsMissionCargo) continue;
            if (saved.BuyPrice < 0 || saved.SellPrice < 0 || saved.Stock < 0 || saved.BaselineStock < 0) continue;

            string stationId = _marketManager.GetStationIdByName(saved.StationId);
            stationId = string.IsNullOrWhiteSpace(stationId) ? saved.StationId : stationId;
            if (!_knownStations.TryGetValue(stationId, out MarketKnowledgeStation station))
            {
                station = new MarketKnowledgeStation(
                    stationId,
                    saved.StationName,
                    Math.Max(0, saved.SystemIndex),
                    saved.StationPosition?.ToVector3(Vector3.Zero) ?? Vector3.Zero);
                _knownStations[stationId] = station;
            }

            station.SetObservation(new MarketObservation
            {
                StationId = stationId,
                StationName = string.IsNullOrWhiteSpace(saved.StationName) ? station.StationName : saved.StationName,
                SystemIndex = Math.Max(0, saved.SystemIndex),
                StationPosition = saved.StationPosition?.ToVector3(station.StationPosition) ?? station.StationPosition,
                Commodity = commodity,
                Stock = saved.Stock,
                BuyPrice = saved.BuyPrice,
                SellPrice = saved.SellPrice,
                BaselineStock = saved.BaselineStock,
                DemandLevel = Math.Max(0, saved.DemandLevel),
                MarketCondition = saved.MarketCondition ?? string.Empty,
                ObservedAtMilliseconds = Math.Max(0L, saved.ObservedAtMilliseconds),
                Source = string.IsNullOrWhiteSpace(saved.Source) ? "Visited" : saved.Source
            });
        }
    }

    public void Clear()
    {
        _knownStations.Clear();
        _missionIntel.Clear();
        _currentStation = null;
    }

    private void AddMissionIntel(string stationName, string commodityId, string condition, int quantity, int reward)
    {
        string stationId = _marketManager.GetStationIdByName(stationName);
        if (string.IsNullOrWhiteSpace(stationId)) return;

        if (_missionIntel.Any(existing =>
            string.Equals(existing.StationId, stationId, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(existing.CommodityId, commodityId, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(existing.Condition, condition, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        _missionIntel.Add(new MarketMissionIntel
        {
            StationId = stationId,
            StationName = stationName ?? string.Empty,
            CommodityId = commodityId ?? string.Empty,
            Condition = condition,
            Quantity = Math.Max(0, quantity),
            Reward = Math.Max(0, reward)
        });
    }

    private string ResolveStationId(string stationIdOrName)
    {
        if (string.IsNullOrWhiteSpace(stationIdOrName)) return string.Empty;
        string canonicalId = _marketManager.GetStationIdByName(stationIdOrName);
        return string.IsNullOrWhiteSpace(canonicalId) && _marketManager.IsKnownStationId(stationIdOrName)
            ? stationIdOrName
            : canonicalId;
    }

    private static bool IsValidObservation(Commodity commodity, StationMarketListing listing) =>
        IsValidCommodity(commodity) && listing != null && listing.IsAvailable && listing.BuyPrice > 0 &&
        listing.SellPrice > 0 && listing.BaselineStock > 0 && listing.Stock >= 0;

    private static bool IsValidCommodity(Commodity commodity) =>
        commodity != null && !string.IsNullOrWhiteSpace(commodity.Id) &&
        !string.IsNullOrWhiteSpace(commodity.Name) && commodity.VolumePerUnit > 0;
}
