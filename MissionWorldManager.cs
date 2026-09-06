using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Roguelancer
{
    /// <summary>
    /// Minimal runtime binding layer that connects missions to world objects, cargo, and station docking.
    /// </summary>
    public sealed class MissionRuntimeState
    {
        public Mission Mission { get; set; }
        public NpcShip BountyTarget { get; set; }
        public Station ReachLocationStation { get; set; }
        public HashSet<NpcShip> MissionHostiles { get; } = new();
        public Station DeliveryDestination { get; set; }
        public Commodity DeliveryCommodity { get; set; }
        public int DeliveryQuantity { get; set; }
        public NpcShip EscortTarget { get; set; }
        public Station EscortDestination { get; set; }
        public TradeLane TargetTradeLane { get; set; }
        public TradeLane ConvoyTradeLane { get; set; }
        public Station ConvoyDestination { get; set; }
        public List<NpcShip> ConvoyShips { get; } = new();
        public HashSet<NpcShip> ConvoyHostiles { get; } = new();
        public TradeLane RaidTradeLane { get; set; }
        public Station RaidDestination { get; set; }
        public List<NpcShip> RaidShips { get; } = new();
        // Transient encounter binding is deliberately not serialized. A
        // restored active defense mission starts unbound and reconstructs its
        // Rogue group once; a live encounter that has been defeated must not
        // be mistaken for that load/rebind case.
        public bool TradeLaneDefenseEncounterBound { get; set; }
        public bool EscortUnderAttackLogged { get; set; }
        public bool ConvoyWarningLogged { get; set; }
        public bool ConvoyAttackLogged { get; set; }
    }

    public sealed class MissionWorldManager
    {
        private readonly MissionManager _missionManager;
        private readonly MissionWaypointSystem _waypointSystem;
        private readonly Ship _playerShip;
        private readonly List<NpcShip> _npcShips;
        private readonly List<SpaceObject> _spaceObjects;
        private readonly Func<IReadOnlyList<Station>> _stationProvider;
        private readonly Action<NpcShip> _spawnedNpcDestroyedCallback;
        private readonly MarketManager _marketManager;
        private readonly MarketIntelligence _marketIntelligence;
        private readonly Func<IReadOnlyList<TradeLane>> _tradeLaneProvider;
        private readonly Action<Vector3, MissionDifficulty, string> _securityResponseCallback;
        private readonly Action<NpcShip> _retiredNpcCallback;
        private readonly Action<NpcShip> _missionNpcRegisteredCallback;
        private readonly Action<NpcShip> _ejectNpcFromTradeLaneCallback;
        private readonly Action<int> _releaseMissionCargoAttributionCallback;
        private readonly Dictionary<int, MissionRuntimeState> _runtimeStates = new();
        private const int MaximumMissionNpcPopulation = 64;

        public MissionWorldManager(
            MissionManager missionManager,
            MissionWaypointSystem waypointSystem,
            Ship playerShip,
            List<NpcShip> npcShips,
            List<SpaceObject> spaceObjects,
            Func<IReadOnlyList<Station>> stationProvider,
            Action<NpcShip> spawnedNpcDestroyedCallback = null,
            MarketManager marketManager = null,
            MarketIntelligence marketIntelligence = null,
            Func<IReadOnlyList<TradeLane>> tradeLaneProvider = null,
            Action<Vector3, MissionDifficulty, string> securityResponseCallback = null,
            Action<NpcShip> retiredNpcCallback = null,
            Action<NpcShip> missionNpcRegisteredCallback = null,
            Action<NpcShip> ejectNpcFromTradeLaneCallback = null,
            Action<int> releaseMissionCargoAttributionCallback = null)
        {
            _missionManager = missionManager;
            _waypointSystem = waypointSystem;
            _playerShip = playerShip;
            _npcShips = npcShips ?? new List<NpcShip>();
            _spaceObjects = spaceObjects ?? new List<SpaceObject>();
            _stationProvider = stationProvider ?? (() => Array.Empty<Station>());
            _spawnedNpcDestroyedCallback = spawnedNpcDestroyedCallback;
            _marketManager = marketManager;
            _marketIntelligence = marketIntelligence;
            _tradeLaneProvider = tradeLaneProvider ?? (() => Array.Empty<TradeLane>());
            _securityResponseCallback = securityResponseCallback;
            _retiredNpcCallback = retiredNpcCallback;
            _missionNpcRegisteredCallback = missionNpcRegisteredCallback;
            _ejectNpcFromTradeLaneCallback = ejectNpcFromTradeLaneCallback;
            _releaseMissionCargoAttributionCallback = releaseMissionCargoAttributionCallback;
        }

        public bool TryAcceptMission(Mission mission, out string failureReason)
        {
            failureReason = string.Empty;

            if (mission == null)
            {
                failureReason = "mission was null";
                return false;
            }

            MissionRuntimeState state = GetOrCreateState(mission);

            switch (mission.Type)
            {
                case MissionType.ReachLocation:
                    return TryBindReachLocationMission(state, out failureReason);
                case MissionType.DestroyHostiles:
                    return TryBindDestroyHostilesMission(state, out failureReason);
                case MissionType.Bounty:
                    return TryBindBountyMission(state, out failureReason);
                case MissionType.Delivery:
                    return TryBindDeliveryMission(state, out failureReason);
                case MissionType.CourierDelivery:
                    return TryBindCourierMission(state, out failureReason);
                case MissionType.FreightContract:
                    return TryBindFreightMission(state, out failureReason);
                case MissionType.ExportContract:
                    return TryBindExportMission(state, out failureReason);
                case MissionType.Escort:
                    return TryBindEscortMission(state, out failureReason);
                case MissionType.TradeLaneDisruption:
                    return TryBindTradeLaneDisruptionMission(state, out failureReason);
                case MissionType.TradeLaneDefense:
                    return TryBindTradeLaneDefenseMission(state, out failureReason);
                case MissionType.ConvoyEscort:
                    return TryBindConvoyEscortMission(state, out failureReason);
                case MissionType.ConvoyRaid:
                    return TryBindConvoyRaidMission(state, out failureReason);
                case MissionType.ContrabandSmuggling:
                    return TryBindSmugglingMission(state, out failureReason);
                default:
                    failureReason = "unsupported mission type";
                    return false;
            }
        }

        public void RebindActiveMissions(IEnumerable<Mission> missions)
        {
            if (missions == null)
            {
                return;
            }

            foreach (Mission mission in missions)
            {
                if (mission == null || mission.Status != MissionStatus.Active)
                {
                    continue;
                }

                RebindMission(mission);
            }
        }

        public void ClearState()
        {
            foreach (MissionRuntimeState state in _runtimeStates.Values.ToList())
            {
                CleanupMissionTransientNpcs(state);
                _releaseMissionCargoAttributionCallback?.Invoke(state.Mission?.Id ?? 0);
            }
            _runtimeStates.Clear();
        }

        public IReadOnlyList<Station> GetKnownStations()
        {
            IReadOnlyList<Station> stations = _stationProvider?.Invoke() ?? Array.Empty<Station>();
            if (stations == null || stations.Count == 0)
            {
                return Array.Empty<Station>();
            }

            return stations.Where(station => station != null).ToList();
        }

        public IReadOnlyList<TradeLane> GetTradeLanes()
        {
            IReadOnlyList<TradeLane> lanes = _tradeLaneProvider?.Invoke() ?? Array.Empty<TradeLane>();
            return lanes == null ? Array.Empty<TradeLane>() : lanes.Where(lane => lane != null).ToList();
        }

        public void RebindMission(Mission mission)
        {
            if (mission == null)
            {
                return;
            }

            MissionRuntimeState state = GetOrCreateState(mission);

            if (mission.Type == MissionType.Bounty)
            {
                if (state.BountyTarget != null &&
                    (state.BountyTarget.IsDestroyed || !_npcShips.Contains(state.BountyTarget)))
                {
                    state.BountyTarget = null;
                }

                state.BountyTarget ??= ResolveExistingBountyTarget(mission);
                if (state.BountyTarget == null && _playerShip != null)
                {
                    TryBindBountyMission(state, out _);
                }
                else if (state.BountyTarget != null)
                {
                    mission.TargetSpaceObject = state.BountyTarget;
                    mission.TargetPosition = state.BountyTarget.Position;
                }
            }
            else if (mission.Type == MissionType.ReachLocation)
            {
                TryBindReachLocationMission(state, out _);
            }
            else if (mission.Type == MissionType.DestroyHostiles)
            {
                TryBindDestroyHostilesMission(state, out _);
            }
            else if (mission.Type == MissionType.Delivery)
            {
                state.DeliveryDestination ??= ResolveDeliveryDestination(mission);
                state.DeliveryCommodity ??= ResolveDeliveryCommodity(mission.Target);
                state.DeliveryQuantity = state.DeliveryQuantity > 0 ? state.DeliveryQuantity : 1;

                if (state.DeliveryDestination != null)
                {
                    mission.TargetSpaceObject = state.DeliveryDestination;
                    mission.TargetPosition = state.DeliveryDestination.Position;
                }
            }
            else if (mission.Type == MissionType.CourierDelivery)
            {
                state.DeliveryDestination ??= ResolveCourierDestination(mission);
                state.DeliveryCommodity ??= ResolveCourierCommodity(mission);
                state.DeliveryQuantity = mission.PackageQuantity > 0 ? mission.PackageQuantity : state.DeliveryQuantity;

                if (state.DeliveryDestination != null)
                {
                    mission.DestinationStationId = Mission.BuildStationIdentity(state.DeliveryDestination);
                    mission.TargetSpaceObject = state.DeliveryDestination;
                    mission.TargetPosition = state.DeliveryDestination.Position;
                }

                if (state.DeliveryCommodity != null && _playerShip?.CargoHold != null)
                {
                    mission.MissionCargoLoaded = _playerShip.CargoHold.HasMissionCargo(
                        mission.Id,
                        mission.PackageId,
                        state.DeliveryQuantity);
                }

                if (!mission.MissionCargoLoaded)
                {
                    FailMission(mission, "mission package missing after load");
                }
            }
            else if (mission.Type == MissionType.FreightContract)
            {
                state.DeliveryDestination ??= ResolveCourierDestination(mission);
                state.DeliveryCommodity ??= CommodityCatalog.GetByIdOrName(mission.CommodityId);
                state.DeliveryQuantity = mission.RequiredQuantity;

                if (state.DeliveryDestination != null)
                {
                    mission.DestinationStationId = Mission.BuildStationIdentity(state.DeliveryDestination);
                    mission.TargetSpaceObject = state.DeliveryDestination;
                    mission.TargetPosition = state.DeliveryDestination.Position;
                }
            }
            else if (mission.Type == MissionType.ExportContract)
            {
                state.DeliveryDestination ??= ResolveCourierDestination(mission);
                state.DeliveryCommodity ??= CommodityCatalog.GetByIdOrName(mission.CommodityId);
                state.DeliveryQuantity = mission.RequiredQuantity;

                if (state.DeliveryDestination != null)
                {
                    mission.DestinationStationId = Mission.BuildStationIdentity(state.DeliveryDestination);
                    mission.TargetSpaceObject = state.DeliveryDestination;
                    mission.TargetPosition = state.DeliveryDestination.Position;
                }
            }
            else if (mission.Type == MissionType.ContrabandSmuggling)
            {
                TryBindSmugglingMission(state, out _);
            }
            else if (mission.Type == MissionType.Escort)
            {
                if (state.EscortTarget != null &&
                    (state.EscortTarget.IsDestroyed || !_npcShips.Contains(state.EscortTarget)))
                {
                    state.EscortTarget = null;
                }

                state.EscortDestination ??= ResolveEscortDestination(mission);

                if (state.EscortDestination == null)
                {
                    FailMission(mission, "destination unavailable");
                    return;
                }

                if (state.EscortTarget == null)
                {
                    if (!TryBindEscortMission(state, out string failureReason))
                    {
                        FailMission(mission, string.IsNullOrWhiteSpace(failureReason) ? "escort binding unavailable" : failureReason);
                        return;
                    }
                }

                if (state.EscortTarget != null)
                {
                    mission.TargetSpaceObject = state.EscortTarget;
                    mission.TargetPosition = state.EscortTarget.Position;
                }
            }
            else if (mission.Type == MissionType.TradeLaneDisruption)
            {
                TryBindTradeLaneDisruptionMission(state, out _);
            }
            else if (mission.Type == MissionType.TradeLaneDefense)
            {
                TryBindTradeLaneDefenseMission(state, out _);
            }
            else if (mission.Type == MissionType.ConvoyEscort)
            {
                TryBindConvoyEscortMission(state, out _);
            }
            else if (mission.Type == MissionType.ConvoyRaid)
            {
                TryBindConvoyRaidMission(state, out _);
            }
        }

        public void OnMissionFinished(Mission mission)
        {
            if (mission == null)
            {
                return;
            }

            if (_runtimeStates.TryGetValue(mission.Id, out MissionRuntimeState state))
                CleanupMissionTransientNpcs(state);
            _releaseMissionCargoAttributionCallback?.Invoke(mission.Id);
            _runtimeStates.Remove(mission.Id);
        }

        public void NotifyNpcDestroyed(NpcShip destroyedShip)
        {
            if (destroyedShip == null)
            {
                return;
            }

            foreach (MissionRuntimeState state in _runtimeStates.Values)
            {
                Mission mission = state.Mission;
                if (mission == null || mission.Status != MissionStatus.Active)
                {
                    continue;
                }

                if (mission.Type == MissionType.DestroyHostiles &&
                    state.MissionHostiles.Remove(destroyedShip))
                {
                    if (destroyedShip.WasDamagedByPlayer)
                    {
                        _missionManager?.RecordHostileDestroyed(mission, destroyedShip);
                    }
                    else
                    {
                        _missionManager?.FailMission(
                            mission,
                            "mission target was destroyed without player attribution");
                    }
                    return;
                }

                if (mission.Type == MissionType.TradeLaneDefense &&
                    state.MissionHostiles.Contains(destroyedShip))
                {
                    // Keep destroyed mission ships in the bounded ownership
                    // set until the mission finishes. The game-level
                    // destruction callback removes them from targetable
                    // systems, while retaining ownership here lets the
                    // completion cleanup remove every transient reference.
                    UpdateDefenseAttackerCount(state);
                    return;
                }

                if (mission.Type == MissionType.ConvoyEscort)
                {
                    if (state.ConvoyHostiles.Contains(destroyedShip))
                    {
                        mission.ConvoyAttackersRemaining = Math.Max(0, mission.ConvoyAttackersRemaining - 1);
                        if (mission.ConvoyEncounterActivated && mission.ConvoyAttackersRemaining == 0)
                        {
                            mission.ConvoyEncounterResolved = true;
                            mission.ConvoyStage = ConvoyEscortStage.Escorting;
                        }
                        return;
                    }

                    int convoyIndex = FindConvoyShipIndex(state, destroyedShip);
                    if (convoyIndex >= 0 && (mission.ConvoyDestroyedMask & (1 << convoyIndex)) == 0)
                    {
                        mission.ConvoyDestroyedMask |= 1 << convoyIndex;
                        mission.ConvoyDestroyedCount = Math.Min(mission.ConvoyShipCount, mission.ConvoyDestroyedCount + 1);
                        mission.ConvoySurvivors = Math.Max(0, mission.ConvoyShipCount - mission.ConvoyDestroyedCount);
                        _missionManager?.ShowNotification("Convoy ship lost", 3f);
                        if (mission.ConvoySurvivors < mission.ConvoyRequiredSurvivors)
                        {
                            mission.ConvoyStage = ConvoyEscortStage.Failed;
                            FailMission(mission, "all required convoy ships were destroyed");
                        }
                        return;
                    }
                }

                if (mission.Type == MissionType.ConvoyRaid)
                {
                    int raidIndex = FindRaidShipIndex(state, destroyedShip);
                    if (raidIndex >= 0 && (mission.RaidDestroyedMask & (1 << raidIndex)) == 0)
                    {
                        mission.RaidDestroyedMask |= 1 << raidIndex;
                        mission.RaidDestroyedCount = Math.Min(mission.RaidShipCount, mission.RaidDestroyedCount + 1);
                        _missionManager?.ShowNotification("Transport destroyed — recover its cargo", 3f);
                    }
                    return;
                }

                if (mission.Type == MissionType.Bounty && IsTargetMatch(mission, destroyedShip, state.BountyTarget))
                {
                    if (!destroyedShip.WasDamagedByPlayer)
                    {
                        return;
                    }

                    Console.WriteLine($"[MISSION] Target destroyed: {destroyedShip.Name} (mission #{mission.Id})");
                    mission.ObjectiveComplete = true;
                    return;
                }

                if (mission.Type == MissionType.Escort && IsEscortMatch(mission, destroyedShip, state.EscortTarget))
                {
                    Console.WriteLine($"[MISSION] Escort destroyed: {destroyedShip.Name} (mission #{mission.Id})");
                    FailMission(mission, "escort destroyed");
                    return;
                }
            }
        }

        public bool NotifyStationDocked(Station station)
        {
            if (station == null)
            {
                return false;
            }

            bool completedAny = false;

            foreach (MissionRuntimeState state in _runtimeStates.Values.ToList())
            {
                Mission mission = state.Mission;
                if (mission == null || mission.Status != MissionStatus.Active || !Mission.IsDeliveryType(mission.Type))
                {
                    continue;
                }

                Station resolvedStation = state.DeliveryDestination ??
                    (mission.Type is MissionType.CourierDelivery or MissionType.ExportContract or MissionType.ContrabandSmuggling
                        ? ResolveCourierDestination(mission)
                        : ResolveDeliveryDestination(mission));
                if (resolvedStation == null)
                {
                    continue;
                }

                string expectedIdentity = mission.Type is MissionType.CourierDelivery or MissionType.FreightContract or MissionType.ExportContract or MissionType.ContrabandSmuggling
                    ? mission.DestinationStationId
                    : string.Empty;
                if (!IsStationMatch(station, resolvedStation, mission.Destination, expectedIdentity))
                {
                    continue;
                }

                if (mission.Type == MissionType.FreightContract)
                {
                    if (!TryCompleteFreightDelivery(state, station))
                        continue;

                    completedAny = true;
                    continue;
                }

                if (mission.Type == MissionType.ExportContract)
                {
                    if (!TryCompleteExportDelivery(state, station))
                        continue;

                    completedAny = true;
                    continue;
                }

                if (mission.Type == MissionType.ContrabandSmuggling)
                {
                    if (!TryCompleteSmugglingDelivery(state, station))
                        continue;

                    completedAny = true;
                    continue;
                }

                bool removed = mission.Type == MissionType.CourierDelivery
                    ? TryRemoveCourierCargo(state)
                    : TryRemoveDeliveryCargo(state);
                if (!removed)
                {
                    FailMission(mission, mission.Type == MissionType.CourierDelivery
                        ? "mission package missing or corrupt"
                        : "mission cargo missing");
                    completedAny = true;
                    continue;
                }

                Console.WriteLine($"[MISSION] Delivery completed at {station.Name} (mission #{mission.Id})");
                if (mission.Type == MissionType.CourierDelivery)
                {
                    mission.MissionCargoLoaded = false;
                    mission.DeliveredQuantity = state.DeliveryQuantity;
                    _missionManager?.ShowNotification($"Destination reached: {station.Name}", 3f);
                    _missionManager?.ShowNotification("Cargo delivered", 3f);
                }
                mission.ObjectiveComplete = true;
                _missionManager?.CompleteMission(mission);
                completedAny = true;
            }

            foreach (MissionRuntimeState state in _runtimeStates.Values.ToList())
            {
                Mission mission = state.Mission;
                if (mission == null || mission.Status != MissionStatus.Active || mission.Type != MissionType.Escort)
                {
                    continue;
                }

                Station destination = state.EscortDestination ?? ResolveEscortDestination(mission);
                if (destination == null)
                {
                    FailMission(mission, "destination unavailable");
                    completedAny = true;
                    continue;
                }

                NpcShip escort = ResolveEscortTarget(mission, state);
                if (escort == null)
                {
                    continue;
                }

                if (!IsEscortAtDestination(escort, destination))
                {
                    continue;
                }

                Console.WriteLine($"[MISSION] Escort reached destination: {escort.Name} -> {destination.Name} (mission #{mission.Id})");
                mission.ObjectiveComplete = true;
                _missionManager?.CompleteMission(mission);
                completedAny = true;
            }

            return completedAny;
        }

        public void Update(float deltaTime, Action<string> log = null, int currentSystemIndex = 0)
        {
            if (_runtimeStates.Count == 0)
            {
                return;
            }

            // The Phase 11 manager permits one active mission, so the common
            // ReachLocation/DestroyHostiles path can iterate the dictionary
            // directly. Completion/failure is deferred until after iteration
            // to avoid both mutation-during-enumeration and a per-frame
            // Values.ToList allocation.
            Mission pendingCompletion = null;
            Mission pendingFailureMission = null;
            string pendingFailure = string.Empty;
            foreach (MissionRuntimeState state in _runtimeStates.Values)
            {
                Mission mission = state.Mission;
                if (mission == null || mission.Status != MissionStatus.Active)
                {
                    continue;
                }

                if (mission.Type == MissionType.TradeLaneDefense)
                {
                    bool correctSystem = mission.TargetSystemIndex <= 0 ||
                        currentSystemIndex <= 0 ||
                        mission.TargetSystemIndex == currentSystemIndex;
                    if (!correctSystem)
                        break;

                    if (!TryBindTradeLaneDefenseMission(state, out string defenseBindFailure))
                    {
                        pendingFailureMission = mission;
                        pendingFailure = defenseBindFailure;
                        break;
                    }

                    UpdateTradeLaneDefenseMission(
                        state,
                        TradeLaneStateSanitizer.Elapsed(deltaTime),
                        out bool defenseComplete,
                        out string defenseFailure);
                    if (!string.IsNullOrWhiteSpace(defenseFailure))
                    {
                        pendingFailureMission = mission;
                        pendingFailure = defenseFailure;
                    }
                    else if (defenseComplete)
                    {
                        pendingCompletion = mission;
                    }
                    break;
                }

                if (mission.Type == MissionType.TradeLaneDisruption)
                {
                    bool correctSystem = mission.TargetSystemIndex <= 0 ||
                        currentSystemIndex <= 0 ||
                        mission.TargetSystemIndex == currentSystemIndex;
                    if (!correctSystem)
                        break;

                    TradeLane currentTargetLane = GetTradeLanes().FirstOrDefault(candidate =>
                        string.Equals(candidate.LaneId, mission.TargetLaneId, StringComparison.OrdinalIgnoreCase));
                    if (state.TargetTradeLane == null ||
                        !ReferenceEquals(state.TargetTradeLane, currentTargetLane) ||
                        !string.Equals(state.TargetTradeLane.LaneId, mission.TargetLaneId, StringComparison.OrdinalIgnoreCase))
                    {
                        if (!TryBindTradeLaneDisruptionMission(state, out string bindFailure))
                        {
                            pendingFailureMission = mission;
                            pendingFailure = bindFailure;
                            break;
                        }
                    }

                    TradeLaneDisruptionInfo disruption = null;
                    if (state.TargetTradeLane != null)
                        state.TargetTradeLane.TryGetDisruptionInfo(mission.TargetRingIndex, out disruption);

                    bool qualified = disruption != null &&
                        disruption.State == TradeLaneDisruptionState.Disrupted &&
                        disruption.Source == TradeLaneDisruptionSource.Player &&
                        string.Equals(disruption.LaneId, mission.TargetLaneId, StringComparison.OrdinalIgnoreCase) &&
                        string.Equals(disruption.SegmentId, mission.TargetSegmentId, StringComparison.OrdinalIgnoreCase);

                    if (qualified)
                    {
                        if (!mission.PlayerDisruptionObserved)
                        {
                            mission.PlayerDisruptionObserved = true;
                            mission.LastQualifiedDisruptionAtSeconds = disruption.OccurredAtSeconds;
                            _missionManager?.RecordTradeLaneDisruption(mission, disruption);
                        }

                        if (!mission.SecurityResponseTriggered)
                        {
                            _securityResponseCallback?.Invoke(
                                mission.TargetPosition ?? Vector3.Zero,
                                mission.Difficulty,
                                mission.Id.ToString());
                            mission.SecurityResponseTriggered = true;
                        }

                        mission.HoldProgressSeconds = Math.Min(
                            Math.Max(0f, mission.HoldDurationSeconds),
                            mission.HoldProgressSeconds + TradeLaneStateSanitizer.Elapsed(deltaTime));
                        if (mission.HoldProgressSeconds >= mission.HoldDurationSeconds)
                        {
                            mission.ObjectiveComplete = true;
                            pendingCompletion = mission;
                        }
                    }
                    else if (disruption == null || disruption.State != TradeLaneDisruptionState.Disrupted)
                    {
                        mission.PlayerDisruptionObserved = false;
                        mission.LastQualifiedDisruptionAtSeconds = -1d;
                        mission.HoldProgressSeconds = 0f;
                    }
                    break;
                }

                if (mission.Type == MissionType.ReachLocation)
                {
                    bool correctSystem = mission.TargetSystemIndex <= 0 ||
                        currentSystemIndex <= 0 ||
                        mission.TargetSystemIndex == currentSystemIndex;
                    if (correctSystem &&
                        mission.TargetPosition.HasValue &&
                        Vector3.Distance(_playerShip.Position, mission.TargetPosition.Value) <= mission.ObjectiveRadius)
                    {
                        Console.WriteLine($"[MISSION] Reach location complete: {mission.TargetLocation} (mission #{mission.Id})");
                        mission.ObjectiveComplete = true;
                        pendingCompletion = mission;
                    }
                    break;
                }

                if (mission.Type == MissionType.DestroyHostiles)
                {
                    if (mission.RequiredProgress <= 0)
                    {
                        pendingFailureMission = mission;
                        pendingFailure = "hostile target metadata became invalid";
                    }
                    break;
                }

                if (mission.Type == MissionType.ConvoyEscort)
                {
                    bool correctSystem = mission.TargetSystemIndex <= 0 ||
                        currentSystemIndex <= 0 ||
                        mission.TargetSystemIndex == currentSystemIndex;
                    if (!correctSystem)
                        break;

                    if (!TryBindConvoyEscortMission(state, out string convoyBindFailure))
                    {
                        pendingFailureMission = mission;
                        pendingFailure = convoyBindFailure;
                        break;
                    }

                    UpdateConvoyEscortMission(
                        state,
                        TradeLaneStateSanitizer.Elapsed(deltaTime),
                        log,
                        out bool convoyComplete,
                        out string convoyFailure);
                    if (!string.IsNullOrWhiteSpace(convoyFailure))
                    {
                        pendingFailureMission = mission;
                        pendingFailure = convoyFailure;
                    }
                    else if (convoyComplete)
                    {
                        pendingCompletion = mission;
                    }
                    break;
                }

                if (mission.Type == MissionType.ConvoyRaid)
                {
                    bool correctSystem = mission.TargetSystemIndex <= 0 ||
                        currentSystemIndex <= 0 ||
                        mission.TargetSystemIndex == currentSystemIndex;
                    if (!correctSystem)
                        break;

                    if (!TryBindConvoyRaidMission(state, out string raidBindFailure))
                    {
                        pendingFailureMission = mission;
                        pendingFailure = raidBindFailure;
                        break;
                    }

                    UpdateConvoyRaidMission(
                        state,
                        TradeLaneStateSanitizer.Elapsed(deltaTime),
                        log,
                        out bool raidComplete,
                        out string raidFailure);
                    if (!string.IsNullOrWhiteSpace(raidFailure))
                    {
                        pendingFailureMission = mission;
                        pendingFailure = raidFailure;
                    }
                    else if (raidComplete)
                    {
                        pendingCompletion = mission;
                    }
                    break;
                }

                if (mission.Type != MissionType.Escort)
                {
                    continue;
                }

                Station destination = state.EscortDestination ?? ResolveEscortDestination(mission);
                if (destination == null)
                {
                    pendingFailureMission = mission;
                    pendingFailure = "destination unavailable";
                    break;
                }

                state.EscortDestination = destination;

                if (state.EscortTarget != null && state.EscortTarget.IsDestroyed)
                {
                    pendingFailureMission = mission;
                    pendingFailure = "escort destroyed";
                    break;
                }

                NpcShip escort = ResolveEscortTarget(mission, state);
                if (escort == null)
                {
                    if (!TryBindEscortMission(state, out string failureReason))
                    {
                        pendingFailureMission = mission;
                        pendingFailure = failureReason;
                        break;
                    }

                    escort = state.EscortTarget;
                }

                if (escort == null)
                {
                    break;
                }

                if (escort.IsTrafficEngaged)
                {
                    if (!state.EscortUnderAttackLogged)
                    {
                        state.EscortUnderAttackLogged = true;
                        log?.Invoke($"[MISSION] Escort under attack: {escort.Name} (mission #{mission.Id})");
                    }
                }
                else
                {
                    state.EscortUnderAttackLogged = false;
                }

                if (IsEscortAtDestination(escort, destination))
                {
                    Console.WriteLine($"[MISSION] Escort reached destination: {escort.Name} -> {destination.Name} (mission #{mission.Id})");
                    mission.ObjectiveComplete = true;
                    pendingCompletion = mission;
                }
                break;
            }

            if (!string.IsNullOrWhiteSpace(pendingFailure))
            {
                FailMission(pendingFailureMission, pendingFailure);
            }
            else if (pendingCompletion != null)
            {
                _missionManager?.CompleteMission(pendingCompletion);
            }
        }

        private bool TryBindReachLocationMission(MissionRuntimeState state, out string failureReason)
        {
            failureReason = string.Empty;
            Mission mission = state.Mission;
            if (mission == null)
            {
                failureReason = "mission was null";
                return false;
            }

            if (!mission.TargetPosition.HasValue)
            {
                state.ReachLocationStation = FindStation(mission.OriginStationName);
                if (state.ReachLocationStation != null)
                {
                    mission.TargetSystemIndex = mission.TargetSystemIndex > 0
                        ? mission.TargetSystemIndex
                        : state.ReachLocationStation.Config?.SystemIndex ?? 0;
                    mission.TargetPosition = state.ReachLocationStation.Position + Vector3.Right * 1500f;
                    mission.TargetSpaceObject = state.ReachLocationStation;
                }
                else if (_playerShip != null)
                {
                    // Developer station sessions have no Station object. The
                    // player-space marker remains deterministic and saveable.
                    mission.TargetPosition = _playerShip.Position + Vector3.Right * 1500f;
                }
            }

            if (!mission.TargetPosition.HasValue)
            {
                failureReason = "patrol marker position could not be resolved";
                return false;
            }

            return true;
        }

        private bool TryBindTradeLaneDisruptionMission(MissionRuntimeState state, out string failureReason)
        {
            failureReason = string.Empty;
            Mission mission = state?.Mission;
            if (mission == null)
            {
                failureReason = "mission was null";
                return false;
            }

            if (mission.Type != MissionType.TradeLaneDisruption ||
                !string.Equals(mission.FactionId, FactionManager.LibertyRogues, StringComparison.OrdinalIgnoreCase))
            {
                failureReason = "trade-lane disruption mission faction is invalid";
                return false;
            }

            TradeLane lane = GetTradeLanes().FirstOrDefault(candidate =>
                string.Equals(candidate.LaneId, mission.TargetLaneId, StringComparison.OrdinalIgnoreCase));
            if (lane == null)
            {
                failureReason = "target trade lane is unavailable in this system";
                return false;
            }

            if (mission.TargetRingIndex <= 0 ||
                mission.TargetRingIndex >= lane.ForwardRings.Count - 1 ||
                mission.TargetRingIndex >= lane.ReverseRings.Count ||
                !string.Equals(
                    mission.TargetSegmentId,
                    $"{lane.LaneId}:ring:{mission.TargetRingIndex}",
                    StringComparison.OrdinalIgnoreCase))
            {
                failureReason = "target trade-lane segment identity is invalid";
                return false;
            }

            if (mission.TargetSystemIndex > 0 && lane.Config?.SystemIndex > 0 &&
                mission.TargetSystemIndex != lane.Config.SystemIndex)
            {
                failureReason = "target trade lane belongs to another system";
                return false;
            }

            TradelaneRing targetRing = lane.ForwardRings[mission.TargetRingIndex];
            if (targetRing == null || !TradeLaneStateSanitizer.IsFinite(targetRing.Position))
            {
                failureReason = "target trade-lane ring position is invalid";
                return false;
            }

            state.TargetTradeLane = lane;
            mission.TargetPosition = targetRing.Position;
            mission.TargetSpaceObject = targetRing;
            mission.TargetLocation = lane.Config?.Name ?? lane.LaneId;
            return true;
        }

        private bool TryBindConvoyEscortMission(MissionRuntimeState state, out string failureReason)
        {
            failureReason = string.Empty;
            Mission mission = state?.Mission;
            if (mission == null)
            {
                failureReason = "mission was null";
                return false;
            }
            if (mission.ConvoyShipCount is < 2 or > 4 || mission.ConvoyRequiredSurvivors < 1 ||
                mission.ConvoyAttackForceSize is < 2 or > 6 || string.IsNullOrWhiteSpace(mission.ConvoyRouteLaneId) ||
                FactionManager.NormalizeFactionId(mission.ConvoyFactionId) != FactionManager.LibertyCorporations ||
                FactionManager.NormalizeFactionId(mission.ConvoyHostileFactionId) != FactionManager.LibertyRogues)
            {
                failureReason = "convoy metadata is outside the bounded mission range";
                return false;
            }

            TradeLane lane = GetTradeLanes().FirstOrDefault(candidate =>
                string.Equals(candidate?.LaneId, mission.ConvoyRouteLaneId, StringComparison.OrdinalIgnoreCase));
            IReadOnlyList<TradelaneRing> route = lane?.GetRouteRings(mission.ConvoyRouteDirection);
            Station destination = GetKnownStations().FirstOrDefault(candidate =>
                string.Equals(Mission.BuildStationIdentity(candidate), mission.DestinationStationId, StringComparison.OrdinalIgnoreCase));
            if (lane == null || route == null || route.Count < 3 || destination == null)
            {
                failureReason = "convoy route or destination is unavailable";
                return false;
            }
            string expectedRouteId = $"{lane.LaneId}:{mission.ConvoyRouteDirection.ToString().ToLowerInvariant()}:{route.Count}";
            string expectedSegmentId = $"{lane.LaneId}:escort:{mission.ConvoyRouteDirection.ToString().ToLowerInvariant()}";
            if (!string.Equals(mission.ConvoyRouteId, expectedRouteId, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(mission.ConvoyRouteSegmentId, expectedSegmentId, StringComparison.OrdinalIgnoreCase))
            {
                failureReason = "convoy route identity is invalid";
                return false;
            }
            if (mission.TargetSystemIndex > 0 && lane.Config.SystemIndex > 0 &&
                mission.TargetSystemIndex != lane.Config.SystemIndex)
            {
                failureReason = "convoy route belongs to another system";
                return false;
            }
            if (!lane.CanUseRoute(mission.ConvoyRouteDirection) && !mission.ConvoyRouteStarted)
            {
                failureReason = "convoy route is unavailable before activation";
                return false;
            }

            int encounterIndex = Math.Clamp(mission.ConvoyEncounterRingIndex, 1, route.Count - 2);
            mission.ConvoyEncounterRingIndex = encounterIndex;
            mission.TargetSystemIndex = mission.TargetSystemIndex > 0
                ? mission.TargetSystemIndex
                : lane.Config.SystemIndex;
            mission.DestinationStationId = Mission.BuildStationIdentity(destination);
            mission.ConvoyRendezvousPosition ??= lane.GetRingTravelPosition(route[0], mission.ConvoyRouteDirection) -
                lane.GetTravelForward(mission.ConvoyRouteDirection) * 900f;
            mission.ConvoyEncounterPosition ??= lane.GetRingTravelPosition(route[encounterIndex], mission.ConvoyRouteDirection);
            mission.ConvoyDestinationPosition ??= destination.Position;
            mission.TargetPosition = mission.ConvoyStage == ConvoyEscortStage.Rendezvous
                ? mission.ConvoyRendezvousPosition
                : mission.ConvoyEncounterPosition;
            state.ConvoyTradeLane = lane;
            state.ConvoyDestination = destination;

            if (state.ConvoyShips.Count == 0)
            {
                for (int i = 0; i < mission.ConvoyShipCount; i++)
                {
                    if ((mission.ConvoyDestroyedMask & (1 << i)) != 0 ||
                        (mission.ConvoyArrivedMask & (1 << i)) != 0)
                        continue;

                    string name = GetConvoyShipName(mission, i);
                    NpcShip existing = _npcShips.FirstOrDefault(candidate =>
                        candidate != null && string.Equals(candidate.Name, name, StringComparison.OrdinalIgnoreCase));
                    if (existing != null)
                    {
                        state.ConvoyShips.Add(existing);
                        _missionNpcRegisteredCallback?.Invoke(existing);
                    }
                    else if (!SpawnConvoyShip(state, i))
                    {
                        // A population cap can leave a partial convoy. A
                        // single surviving merchant remains a viable contract.
                        mission.ConvoyDestroyedMask |= 1 << i;
                        mission.ConvoyDestroyedCount = Math.Min(mission.ConvoyShipCount, mission.ConvoyDestroyedCount + 1);
                    }
                }
            }

            UpdateConvoyCounts(mission);
            if (state.ConvoyShips.Count == 0 || mission.ConvoySurvivors < mission.ConvoyRequiredSurvivors)
            {
                failureReason = "no viable convoy ships could be spawned";
                return false;
            }

            if (mission.ConvoyStage == ConvoyEscortStage.EncounterActive &&
                !mission.ConvoyEncounterResolved && state.ConvoyHostiles.Count == 0 &&
                mission.ConvoyEncounterSpawnAttempted && mission.ConvoyAttackersRemaining > 0)
            {
                SpawnConvoyAttackers(state, out _);
            }

            return true;
        }

        private bool TryBindConvoyRaidMission(MissionRuntimeState state, out string failureReason)
        {
            failureReason = string.Empty;
            Mission mission = state?.Mission;
            if (mission == null)
            {
                failureReason = "mission was null";
                return false;
            }

            if (mission.RaidShipCount is < 2 or > 4 || mission.RaidRequiredQuantity is < 1 or > 20 ||
                mission.RaidCargoAllocation.Count != mission.RaidShipCount ||
                mission.RaidCargoAllocation.Any(quantity => quantity <= 0) ||
                mission.RaidTotalAllocatedQuantity != mission.RaidCargoAllocation.Sum() ||
                mission.RaidTotalAllocatedQuantity < mission.RaidRequiredQuantity ||
                mission.RaidTotalAllocatedQuantity > 40 ||
                FactionManager.NormalizeFactionId(mission.FactionId) != FactionManager.LibertyRogues ||
                FactionManager.NormalizeFactionId(mission.RaidConvoyFactionId) != FactionManager.LibertyCorporations ||
                string.IsNullOrWhiteSpace(mission.RaidRouteId) ||
                string.IsNullOrWhiteSpace(mission.RaidRouteLaneId) ||
                string.IsNullOrWhiteSpace(mission.RaidRouteSegmentId) ||
                string.IsNullOrWhiteSpace(mission.RaidCommodityId))
            {
                failureReason = "cargo-interdiction metadata is outside the bounded mission range";
                return false;
            }

            Commodity commodity = CommodityCatalog.GetByIdOrName(mission.RaidCommodityId);
            TradeLane lane = GetTradeLanes().FirstOrDefault(candidate =>
                string.Equals(candidate?.LaneId, mission.RaidRouteLaneId, StringComparison.OrdinalIgnoreCase));
            IReadOnlyList<TradelaneRing> route = lane?.GetRouteRings(mission.RaidRouteDirection);
            Station destination = GetKnownStations().FirstOrDefault(candidate =>
                string.Equals(Mission.BuildStationIdentity(candidate), mission.DestinationStationId, StringComparison.OrdinalIgnoreCase));
            if (commodity == null || commodity.IsMissionCargo || commodity.IsContraband || commodity.VolumePerUnit <= 0 ||
                lane == null || lane.IsBroken || route == null || route.Count < 3 || destination == null ||
                !lane.CanUseRoute(mission.RaidRouteDirection))
            {
                failureReason = "cargo-interdiction route, cargo, or destination is unavailable";
                return false;
            }

            string expectedRouteId = $"{lane.LaneId}:{mission.RaidRouteDirection.ToString().ToLowerInvariant()}:{route.Count}";
            string expectedSegmentId = $"{lane.LaneId}:raid:{mission.RaidRouteDirection.ToString().ToLowerInvariant()}";
            if (!string.Equals(mission.RaidRouteId, expectedRouteId, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(mission.RaidRouteSegmentId, expectedSegmentId, StringComparison.OrdinalIgnoreCase))
            {
                failureReason = "cargo-interdiction route identity is invalid";
                return false;
            }

            if (mission.TargetSystemIndex > 0 && lane.Config?.SystemIndex > 0 &&
                mission.TargetSystemIndex != lane.Config.SystemIndex)
            {
                failureReason = "cargo-interdiction route belongs to another system";
                return false;
            }

            int interceptionIndex = Math.Clamp(mission.RaidInterceptionRingIndex, 1, route.Count - 2);
            TradelaneRing interceptionRing = route[interceptionIndex];
            TradelaneRing exitRing = lane.GetExitRing(mission.RaidRouteDirection);
            if (interceptionRing == null || exitRing == null ||
                !TradeLaneStateSanitizer.IsFinite(interceptionRing.Position) ||
                !TradeLaneStateSanitizer.IsFinite(destination.Position))
            {
                failureReason = "cargo-interdiction route positions are invalid";
                return false;
            }

            mission.TargetSystemIndex = mission.TargetSystemIndex > 0
                ? mission.TargetSystemIndex
                : lane.Config?.SystemIndex ?? destination.Config?.SystemIndex ?? 0;
            mission.DestinationStationId = Mission.BuildStationIdentity(destination);
            mission.RaidInterceptionRingIndex = interceptionIndex;
            mission.RaidInterceptionPosition ??= lane.GetRingTravelPosition(interceptionRing, mission.RaidRouteDirection);
            mission.RaidDestinationPosition ??= destination.Position;
            mission.TargetPosition = mission.RaidStage == ConvoyRaidStage.EnRoute
                ? mission.RaidInterceptionPosition
                : mission.RaidDestinationPosition;
            state.RaidTradeLane = lane;
            state.RaidDestination = destination;

            if (state.RaidShips.Count == 0)
            {
                for (int i = 0; i < mission.RaidShipCount; i++)
                {
                    if ((mission.RaidDestroyedMask & (1 << i)) != 0 ||
                        (mission.RaidEscapedMask & (1 << i)) != 0)
                        continue;

                    string name = GetRaidShipName(mission, i);
                    NpcShip existing = _npcShips.FirstOrDefault(candidate =>
                        candidate != null && !candidate.IsDestroyed &&
                        string.Equals(candidate.Name, name, StringComparison.OrdinalIgnoreCase));
                    if (existing != null)
                    {
                        state.RaidShips.Add(existing);
                        _missionNpcRegisteredCallback?.Invoke(existing);
                    }
                    else if (!SpawnRaidShip(state, i, route))
                    {
                        CleanupMissionTransientNpcs(state);
                        failureReason = "the full commercial convoy could not be spawned";
                        return false;
                    }
                }
            }

            if (state.RaidShips.Count == 0)
            {
                failureReason = "no viable convoy transports could be spawned";
                return false;
            }

            return true;
        }

        private bool SpawnRaidShip(MissionRuntimeState state, int index, IReadOnlyList<TradelaneRing> route)
        {
            Mission mission = state?.Mission;
            if (mission == null || _playerShip == null || state.RaidTradeLane == null || route == null ||
                route.Count < 3 || _npcShips.Count >= MaximumMissionNpcPopulation)
                return false;

            Vector3 routeStart = state.RaidTradeLane.GetRingTravelPosition(route[0], mission.RaidRouteDirection);
            Vector3 routeEnd = state.RaidTradeLane.GetRingTravelPosition(route[route.Count - 1], mission.RaidRouteDirection);
            Vector3[] offsets =
            {
                new Vector3(-280f, 80f, 160f),
                new Vector3(280f, -80f, -160f),
                new Vector3(-520f, -120f, -240f),
                new Vector3(520f, 120f, 240f)
            };
            Vector3 spawnPosition = routeStart + offsets[index % offsets.Length];
            NpcShip transport = new NpcShip(
                GetRaidShipName(mission, index),
                spawnPosition,
                routeStart,
                400f,
                0f,
                mission.RaidConvoyFactionId);
            transport.Model = _playerShip.Model;
            transport.ModelPath = _playerShip.ModelPath;
            transport.ConfigureTrafficBehavior(
                TrafficZoneBehaviorType.TraderRoute,
                $"mission-convoy-raid-{mission.Id}",
                routeStart,
                300f,
                GetConvoyCruiseSpeed(mission.Difficulty),
                12000f,
                routeStart,
                routeEnd);
            transport.SetLoadout(NpcEquipmentLoadoutFactory.CreateForNpc(
                mission.RaidShipArchetype,
                mission.RaidConvoyFactionId,
                transport.ModelPath,
                TrafficZoneBehaviorType.TraderRoute,
                MapMissionLoadoutTier(mission.Difficulty)));
            transport.SetMissionHoldPosition(false);
            transport.OnDestroyed += npc => _spawnedNpcDestroyedCallback?.Invoke(npc);
            _npcShips.Add(transport);
            _spaceObjects.Add(transport);
            state.RaidShips.Add(transport);
            _missionNpcRegisteredCallback?.Invoke(transport);
            return true;
        }

        private void UpdateConvoyRaidMission(
            MissionRuntimeState state,
            float deltaTime,
            Action<string> log,
            out bool complete,
            out string failureReason)
        {
            complete = false;
            failureReason = string.Empty;
            Mission mission = state?.Mission;
            TradeLane lane = state?.RaidTradeLane;
            Station destination = state?.RaidDestination;
            if (mission == null || lane == null || destination == null || _playerShip == null ||
                !mission.RaidInterceptionPosition.HasValue || !mission.RaidDestinationPosition.HasValue)
            {
                failureReason = "cargo-interdiction binding became invalid";
                return;
            }

            if (_playerShip.CargoHold != null)
                mission.RaidCargoRecoveredQuantity = Math.Clamp(
                    _playerShip.CargoHold.GetMissionCargoQuantity(mission.Id),
                    0,
                    mission.RaidTotalAllocatedQuantity);
            mission.CurrentProgress = Math.Min(mission.RaidRequiredQuantity, mission.RaidCargoRecoveredQuantity);

            if (!mission.RaidInterceptionActivated &&
                (Vector3.Distance(_playerShip.Position, mission.RaidInterceptionPosition.Value) <= 2500f ||
                 state.RaidShips.Any(ship => ship != null && !ship.IsDestroyed &&
                     Vector3.Distance(ship.Position, mission.RaidInterceptionPosition.Value) <= 1800f)))
            {
                mission.RaidInterceptionActivated = true;
                mission.RaidStage = ConvoyRaidStage.InterceptionActive;
                _missionManager?.ShowNotification("Convoy intercepted — destroy transports and recover cargo", 4f);
                log?.Invoke($"[MISSION] Convoy raid interception started (mission #{mission.Id}).");
            }

            UpdateRaidRouteProgress(state);
            UpdateRaidEscapes(state);
            mission.RaidRemainingPossibleQuantity = Math.Max(
                0,
                mission.RaidTotalAllocatedQuantity -
                mission.RaidCargoRecoveredQuantity -
                mission.RaidCargoLostQuantity);

            if (mission.RaidCargoRecoveredQuantity >= mission.RaidRequiredQuantity)
            {
                mission.RaidStage = ConvoyRaidStage.Successful;
                mission.ObjectiveComplete = true;
                complete = true;
                return;
            }

            if (mission.RaidCargoRecoveredQuantity + mission.RaidRemainingPossibleQuantity < mission.RaidRequiredQuantity)
            {
                failureReason = "not enough convoy cargo remains recoverable";
                return;
            }

            if (mission.RaidInterceptionActivated && mission.RaidStage == ConvoyRaidStage.EnRoute)
                mission.RaidStage = ConvoyRaidStage.InterceptionActive;

            if (mission.RaidCargoRecoveredQuantity > 0 &&
                mission.RaidCargoRecoveredQuantity < mission.RaidRequiredQuantity)
                mission.RaidStage = ConvoyRaidStage.CargoRecovery;
        }

        private void UpdateRaidRouteProgress(MissionRuntimeState state)
        {
            Mission mission = state?.Mission;
            TradeLane lane = state?.RaidTradeLane;
            Station destination = state?.RaidDestination;
            if (mission == null || lane == null || destination == null)
                return;

            IReadOnlyList<TradelaneRing> route = lane.GetRouteRings(mission.RaidRouteDirection);
            TradelaneRing exit = lane.GetExitRing(mission.RaidRouteDirection);
            if (route == null || route.Count < 3 || exit == null)
                return;

            int furthest = mission.RaidRouteRingIndex;
            NpcShip leader = null;
            for (int i = 0; i < state.RaidShips.Count; i++)
            {
                NpcShip ship = state.RaidShips[i];
                int shipIndex = FindRaidShipIndex(state, ship);
                if (ship == null || shipIndex < 0 || ship.IsDestroyed ||
                    (mission.RaidDestroyedMask & (1 << shipIndex)) != 0 ||
                    (mission.RaidEscapedMask & (1 << shipIndex)) != 0)
                    continue;

                int routeIndex = ship.IsTradeLaneTransit && ship.TradeLaneRingIndex >= 0
                    ? mission.RaidRouteDirection == TradeLaneDirection.Forward
                        ? ship.TradeLaneRingIndex
                        : route.Count - 1 - ship.TradeLaneRingIndex
                    : -1;
                if (routeIndex >= 0)
                    furthest = Math.Max(furthest, Math.Clamp(routeIndex, 0, route.Count - 1));

                bool atExit = !ship.IsTradeLaneTransit &&
                    Vector3.Distance(ship.Position, lane.GetRingTravelPosition(exit, mission.RaidRouteDirection)) <=
                    Math.Max(900f, lane.Config.RingSpacing * 1.5f);
                if (atExit && !IsRouteEndDestination(ship, destination))
                {
                    ship.ConfigureTrafficBehavior(
                        TrafficZoneBehaviorType.TraderRoute,
                        $"mission-convoy-raid-{mission.Id}",
                        ship.Position,
                        300f,
                        GetConvoyCruiseSpeed(mission.Difficulty),
                        12000f,
                        ship.Position,
                        destination.Position);
                    furthest = route.Count - 1;
                }

                if (leader == null)
                    leader = ship;
            }

            mission.RaidRouteRingIndex = Math.Clamp(furthest, -1, route.Count - 1);
            if (leader != null)
                mission.TargetPosition = mission.RaidInterceptionActivated ? leader.Position : mission.RaidInterceptionPosition;
        }

        private void UpdateRaidEscapes(MissionRuntimeState state)
        {
            Mission mission = state?.Mission;
            Station destination = state?.RaidDestination;
            if (mission == null || destination == null)
                return;

            for (int i = 0; i < state.RaidShips.Count; i++)
            {
                NpcShip ship = state.RaidShips[i];
                int index = FindRaidShipIndex(state, ship);
                if (ship == null || index < 0 || ship.IsDestroyed ||
                    (mission.RaidDestroyedMask & (1 << index)) != 0 ||
                    (mission.RaidEscapedMask & (1 << index)) != 0 ||
                    Vector3.Distance(ship.Position, destination.Position) > 3000f)
                    continue;

                mission.RaidEscapedMask |= 1 << index;
                mission.RaidEscapedCount = Math.Min(mission.RaidShipCount, mission.RaidEscapedCount + 1);
                mission.RaidCargoLostQuantity = Math.Min(
                    mission.RaidTotalAllocatedQuantity,
                    mission.RaidCargoLostQuantity + mission.RaidCargoAllocation[index]);
                _ejectNpcFromTradeLaneCallback?.Invoke(ship);
                _npcShips.Remove(ship);
                _spaceObjects.Remove(ship);
                _retiredNpcCallback?.Invoke(ship);
            }
        }

        public MissionCargoDrop GetMissionCargoDrop(NpcShip destroyedShip)
        {
            if (destroyedShip == null)
                return null;

            foreach (MissionRuntimeState state in _runtimeStates.Values)
            {
                Mission mission = state?.Mission;
                if (mission == null || mission.Status != MissionStatus.Active || !mission.IsConvoyRaidMission())
                    continue;

                int sourceIndex = FindRaidShipIndex(state, destroyedShip);
                if (sourceIndex < 0 || (mission.RaidDestroyedMask & (1 << sourceIndex)) == 0 ||
                    (mission.RaidCargoReleasedMask & (1 << sourceIndex)) != 0 ||
                    (mission.RaidEscapedMask & (1 << sourceIndex)) != 0 ||
                    sourceIndex >= mission.RaidCargoAllocation.Count)
                    continue;

                return new MissionCargoDrop
                {
                    MissionId = mission.Id,
                    SourceIndex = sourceIndex,
                    CommodityId = mission.RaidCommodityId,
                    Quantity = mission.RaidCargoAllocation[sourceIndex],
                    SourceNpcName = destroyedShip.Name ?? string.Empty,
                    SourcePosition = destroyedShip.Position
                };
            }

            return null;
        }

        public void NotifyMissionCargoPodSpawned(CargoPod pod)
        {
            if (pod == null || !pod.IsMissionCargo || !_runtimeStates.TryGetValue(pod.MissionId, out MissionRuntimeState state))
                return;
            Mission mission = state.Mission;
            int index = pod.MissionCargoSourceIndex;
            if (mission == null || !mission.IsConvoyRaidMission() || index < 0 || index >= mission.RaidShipCount ||
                (mission.RaidCargoReleasedMask & (1 << index)) != 0)
                return;

            mission.RaidCargoReleasedMask |= 1 << index;
            mission.RaidCargoReleasedQuantity = Math.Min(
                mission.RaidTotalAllocatedQuantity,
                mission.RaidCargoReleasedQuantity + pod.Quantity);
        }

        public void NotifyMissionCargoPodCollected(CargoPod pod, int quantity)
        {
            if (pod == null || quantity <= 0 || !pod.IsMissionCargo ||
                !_runtimeStates.TryGetValue(pod.MissionId, out MissionRuntimeState state))
                return;
            if (state.Mission?.IsConvoyRaidMission() == true && _playerShip.CargoHold != null)
            {
                state.Mission.RaidCargoRecoveredQuantity = Math.Clamp(
                    _playerShip.CargoHold.GetMissionCargoQuantity(pod.MissionId),
                    0,
                    state.Mission.RaidTotalAllocatedQuantity);
            }
        }

        public void NotifyMissionCargoPodExpired(CargoPod pod, int quantity)
        {
            if (pod == null || quantity <= 0 || !pod.IsMissionCargo ||
                !_runtimeStates.TryGetValue(pod.MissionId, out MissionRuntimeState state))
                return;
            Mission mission = state.Mission;
            if (mission?.IsConvoyRaidMission() != true)
                return;
            mission.RaidCargoLostQuantity = Math.Min(
                mission.RaidTotalAllocatedQuantity,
                mission.RaidCargoLostQuantity + quantity);
        }

        public void NotifyMissionCargoDropUnavailable(MissionCargoDrop drop)
        {
            if (drop == null || drop.Quantity <= 0 || !_runtimeStates.TryGetValue(drop.MissionId, out MissionRuntimeState state))
                return;
            Mission mission = state.Mission;
            if (mission?.IsConvoyRaidMission() != true || drop.SourceIndex < 0 || drop.SourceIndex >= mission.RaidShipCount ||
                (mission.RaidCargoReleasedMask & (1 << drop.SourceIndex)) != 0)
                return;

            mission.RaidCargoReleasedMask |= 1 << drop.SourceIndex;
            mission.RaidCargoLostQuantity = Math.Min(
                mission.RaidTotalAllocatedQuantity,
                mission.RaidCargoLostQuantity + drop.Quantity);
        }

        private bool SpawnConvoyShip(MissionRuntimeState state, int index)
        {
            Mission mission = state?.Mission;
            if (mission == null || _playerShip == null || state.ConvoyTradeLane == null ||
                !mission.ConvoyRendezvousPosition.HasValue || _npcShips.Count >= MaximumMissionNpcPopulation)
                return false;

            Vector3 anchor = mission.ConvoyRendezvousPosition.Value;
            Vector3[] offsets =
            {
                new Vector3(-260f, 90f, 180f),
                new Vector3(260f, -70f, -180f),
                new Vector3(-520f, -120f, -220f),
                new Vector3(520f, 140f, 220f)
            };
            NpcShip convoyShip = new NpcShip(
                GetConvoyShipName(mission, index),
                anchor + offsets[index % offsets.Length],
                anchor,
                400f,
                0f,
                mission.ConvoyFactionId);
            convoyShip.Model = _playerShip.Model;
            convoyShip.ModelPath = _playerShip.ModelPath;
            convoyShip.ConfigureTrafficBehavior(
                TrafficZoneBehaviorType.TraderRoute,
                $"mission-convoy-escort-{mission.Id}",
                anchor,
                300f,
                GetConvoyCruiseSpeed(mission.Difficulty),
                12000f);
            convoyShip.SetLoadout(NpcEquipmentLoadoutFactory.CreateForNpc(
                mission.ConvoyShipArchetype,
                mission.ConvoyFactionId,
                convoyShip.ModelPath,
                TrafficZoneBehaviorType.TraderRoute,
                MapMissionLoadoutTier(mission.Difficulty)));
            convoyShip.SetMissionHoldPosition(true, anchor + offsets[index % offsets.Length]);
            convoyShip.OnDestroyed += npc => _spawnedNpcDestroyedCallback?.Invoke(npc);
            _npcShips.Add(convoyShip);
            _spaceObjects.Add(convoyShip);
            state.ConvoyShips.Add(convoyShip);
            _missionNpcRegisteredCallback?.Invoke(convoyShip);
            return true;
        }

        private void UpdateConvoyEscortMission(
            MissionRuntimeState state,
            float deltaTime,
            Action<string> log,
            out bool complete,
            out string failureReason)
        {
            complete = false;
            failureReason = string.Empty;
            Mission mission = state?.Mission;
            TradeLane lane = state?.ConvoyTradeLane;
            Station destination = state?.ConvoyDestination;
            if (mission == null || lane == null || destination == null || _playerShip == null)
            {
                failureReason = "convoy escort binding became invalid";
                return;
            }

            UpdateConvoyCounts(mission);
            if (mission.ConvoySurvivors < mission.ConvoyRequiredSurvivors)
            {
                failureReason = "all required convoy ships were destroyed";
                return;
            }

            if (mission.ConvoyRouteStarted && lane.IsBroken)
            {
                failureReason = "convoy route became unrecoverably invalid";
                return;
            }

            if (mission.ConvoyStage == ConvoyEscortStage.Rendezvous)
            {
                mission.TargetPosition = mission.ConvoyRendezvousPosition;
                if (!mission.ConvoyRendezvousPosition.HasValue ||
                    Vector3.Distance(_playerShip.Position, mission.ConvoyRendezvousPosition.Value) > mission.ConvoyRendezvousRadius)
                    return;

                ActivateConvoyRoute(state);
                log?.Invoke($"[MISSION] Convoy rendezvous reached (mission #{mission.Id}).");
            }

            if (mission.ConvoyStage == ConvoyEscortStage.EncounterActive &&
                !mission.ConvoyEncounterResolved && state.ConvoyHostiles.Count == 0)
            {
                if (mission.ConvoyEncounterSpawnAttempted && mission.ConvoyAttackersRemaining <= 0)
                {
                    mission.ConvoyEncounterResolved = true;
                    mission.ConvoyStage = ConvoyEscortStage.Escorting;
                    _missionManager?.ShowNotification("Rogue interceptors destroyed; convoy moving again", 3f);
                }
                else
                {
                    SpawnConvoyAttackers(state, out _);
                }
            }

            UpdateConvoyRouteProgress(state);
            if (!mission.ConvoyEncounterActivated && mission.ConvoyRouteStarted &&
                mission.ConvoyEncounterPosition.HasValue &&
                state.ConvoyShips.Any(ship => ship != null && !ship.IsDestroyed &&
                    Vector3.Distance(ship.Position, mission.ConvoyEncounterPosition.Value) <= 1800f))
            {
                mission.ConvoyEncounterActivated = true;
                mission.ConvoyEncounterSpawnAttempted = true;
                mission.ConvoyStage = ConvoyEscortStage.EncounterActive;
                if (!SpawnConvoyAttackers(state, out _))
                {
                    mission.ConvoyEncounterResolved = true;
                    mission.ConvoyStage = ConvoyEscortStage.Escorting;
                }
                else if (!state.ConvoyHostiles.Any(attacker => attacker != null && !attacker.IsDestroyed))
                {
                    mission.ConvoyEncounterResolved = true;
                    mission.ConvoyStage = ConvoyEscortStage.Escorting;
                }
                else
                {
                    _missionManager?.ShowNotification("Rogue interceptors detected", 3f);
                    log?.Invoke($"[MISSION] Convoy interception started (mission #{mission.Id}).");
                }
            }

            if (mission.ConvoyEncounterActivated)
            {
                mission.ConvoyAttackersRemaining = state.ConvoyHostiles.Count(attacker =>
                    attacker != null && !attacker.IsDestroyed && _npcShips.Contains(attacker));
                if (mission.ConvoyAttackersRemaining == 0 && !mission.ConvoyEncounterResolved)
                {
                    mission.ConvoyEncounterResolved = true;
                    mission.ConvoyStage = ConvoyEscortStage.Escorting;
                }
            }

            if (mission.ConvoyEncounterActivated && !mission.ConvoyEncounterResolved)
            {
                mission.ConvoyStage = ConvoyEscortStage.EncounterActive;
            }

            UpdateConvoyAbandonment(mission, state, deltaTime, out failureReason);
            if (!string.IsNullOrWhiteSpace(failureReason))
                return;

            if (mission.ConvoyEncounterResolved)
            {
                UpdateConvoyArrival(state);
                UpdateConvoyCounts(mission);
                int expectedArrivals = Math.Max(0, mission.ConvoyShipCount - mission.ConvoyDestroyedCount);
                if (mission.ConvoySurvivors >= mission.ConvoyRequiredSurvivors &&
                    mission.ConvoyArrivedCount >= expectedArrivals)
                {
                    mission.ConvoyStage = ConvoyEscortStage.Successful;
                    mission.ObjectiveComplete = true;
                    complete = true;
                }
                else if (mission.ConvoyArrivedCount > 0)
                {
                    mission.ConvoyStage = ConvoyEscortStage.ApproachingDestination;
                }
            }
        }

        private void ActivateConvoyRoute(MissionRuntimeState state)
        {
            Mission mission = state?.Mission;
            TradeLane lane = state?.ConvoyTradeLane;
            if (mission == null || lane == null)
                return;

            IReadOnlyList<TradelaneRing> route = lane.GetRouteRings(mission.ConvoyRouteDirection);
            if (route == null || route.Count < 3)
                return;
            Vector3 routeStart = lane.GetRingTravelPosition(route[0], mission.ConvoyRouteDirection);
            Vector3 routeEnd = lane.GetRingTravelPosition(route[route.Count - 1], mission.ConvoyRouteDirection);
            for (int i = 0; i < state.ConvoyShips.Count; i++)
            {
                NpcShip convoyShip = state.ConvoyShips[i];
                if (convoyShip == null || convoyShip.IsDestroyed ||
                    (mission.ConvoyDestroyedMask & (1 << i)) != 0 ||
                    (mission.ConvoyArrivedMask & (1 << i)) != 0)
                    continue;
                convoyShip.SetMissionHoldPosition(false);
                convoyShip.ConfigureTrafficBehavior(
                    TrafficZoneBehaviorType.TraderRoute,
                    $"mission-convoy-escort-{mission.Id}",
                    routeStart,
                    300f,
                    GetConvoyCruiseSpeed(mission.Difficulty),
                    12000f,
                    routeStart,
                    routeEnd);
            }

            mission.ConvoyRouteStarted = true;
            mission.ConvoyRouteRingIndex = 0;
            mission.ConvoyStage = ConvoyEscortStage.Escorting;
            mission.TargetPosition = state.ConvoyShips.FirstOrDefault(ship => ship != null && !ship.IsDestroyed)?.Position ?? routeStart;
            _missionManager?.ShowNotification("Rendezvous with convoy — escort to destination", 3f);
        }

        private bool SpawnConvoyAttackers(MissionRuntimeState state, out string failureReason)
        {
            failureReason = string.Empty;
            Mission mission = state?.Mission;
            if (mission == null || !mission.ConvoyEncounterPosition.HasValue)
            {
                failureReason = "convoy interception position is unavailable";
                return false;
            }
            if (state.ConvoyHostiles.Any(attacker => attacker != null && !attacker.IsDestroyed && _npcShips.Contains(attacker)))
            {
                mission.ConvoyAttackersRemaining = state.ConvoyHostiles.Count(attacker =>
                    attacker != null && !attacker.IsDestroyed && _npcShips.Contains(attacker));
                return true;
            }

            int requestedForce = Math.Clamp(mission.ConvoyAttackForceSize, 2, 6);
            int requested = mission.ConvoyEncounterSpawnAttempted && mission.ConvoyAttackersRemaining > 0
                ? Math.Clamp(mission.ConvoyAttackersRemaining, 1, requestedForce)
                : requestedForce;
            Vector3 anchor = mission.ConvoyEncounterPosition.Value;
            Vector3[] offsets =
            {
                new Vector3(-850f, 120f, 260f),
                new Vector3(850f, -80f, -220f),
                new Vector3(-420f, -160f, -760f),
                new Vector3(470f, 200f, 720f),
                new Vector3(-700f, 240f, -560f),
                new Vector3(690f, -220f, 540f)
            };
            for (int i = 0; i < requested && _npcShips.Count < MaximumMissionNpcPopulation; i++)
            {
                NpcShip attacker = new NpcShip(
                    GetConvoyAttackerName(mission, i),
                    anchor + offsets[i],
                    anchor,
                    700f,
                    0f,
                    mission.ConvoyHostileFactionId);
                attacker.Model = _playerShip.Model;
                attacker.ModelPath = _playerShip.ModelPath;
                attacker.ConfigureTrafficBehavior(
                    TrafficZoneBehaviorType.PirateAmbush,
                    $"mission-convoy-escort-{mission.Id}",
                    anchor,
                    1200f,
                    220f,
                    12000f);
                attacker.SetLoadout(NpcEquipmentLoadoutFactory.CreateForNpc(
                    "Rogue",
                    mission.ConvoyHostileFactionId,
                    attacker.ModelPath,
                    TrafficZoneBehaviorType.PirateAmbush,
                    MapMissionLoadoutTier(mission.Difficulty)));
                attacker.OnDestroyed += npc => _spawnedNpcDestroyedCallback?.Invoke(npc);
                _npcShips.Add(attacker);
                _spaceObjects.Add(attacker);
                state.ConvoyHostiles.Add(attacker);
                _missionNpcRegisteredCallback?.Invoke(attacker);
            }

            mission.ConvoyAttackersRemaining = state.ConvoyHostiles.Count;
            mission.ConvoyAttackForceSize = requestedForce;
            return true;
        }

        private void UpdateConvoyRouteProgress(MissionRuntimeState state)
        {
            Mission mission = state?.Mission;
            TradeLane lane = state?.ConvoyTradeLane;
            Station destination = state?.ConvoyDestination;
            if (mission == null || lane == null || destination == null)
                return;

            IReadOnlyList<TradelaneRing> route = lane.GetRouteRings(mission.ConvoyRouteDirection);
            if (route == null || route.Count < 3)
                return;
            TradelaneRing exit = lane.GetExitRing(mission.ConvoyRouteDirection);
            int furthest = mission.ConvoyRouteRingIndex;
            for (int i = 0; i < state.ConvoyShips.Count; i++)
            {
                NpcShip ship = state.ConvoyShips[i];
                int convoyIndex = FindConvoyShipIndex(state, ship);
                if (ship == null || convoyIndex < 0 || ship.IsDestroyed ||
                    (mission.ConvoyDestroyedMask & (1 << convoyIndex)) != 0 ||
                    (mission.ConvoyArrivedMask & (1 << convoyIndex)) != 0)
                    continue;

                int routeIndex = ship.IsTradeLaneTransit && ship.TradeLaneRingIndex >= 0
                    ? mission.ConvoyRouteDirection == TradeLaneDirection.Forward
                        ? ship.TradeLaneRingIndex
                        : route.Count - 1 - ship.TradeLaneRingIndex
                    : -1;
                if (routeIndex >= 0)
                    furthest = Math.Max(furthest, Math.Clamp(routeIndex, 0, route.Count - 1));

                bool atExit = !ship.IsTradeLaneTransit && exit != null &&
                    Vector3.Distance(ship.Position, lane.GetRingTravelPosition(exit, mission.ConvoyRouteDirection)) <=
                    Math.Max(900f, lane.Config.RingSpacing * 1.5f);
                if (atExit && !IsRouteEndDestination(ship, destination))
                {
                    ship.ConfigureTrafficBehavior(
                        TrafficZoneBehaviorType.TraderRoute,
                        $"mission-convoy-escort-{mission.Id}",
                        ship.Position,
                        300f,
                        GetConvoyCruiseSpeed(mission.Difficulty),
                        12000f,
                        ship.Position,
                        destination.Position);
                    furthest = route.Count - 1;
                }

                // Combat exits and other ordinary NPC lifecycle transitions
                // can leave a ship outside lane transit while retaining the
                // route's old direction. Once the bounded mission encounter
                // is resolved, repair only that mission-owned destination
                // leg; lane traversal itself remains authoritative.
                if (mission.ConvoyEncounterResolved && !ship.IsTradeLaneTransit)
                {
                    Vector3 toDestination = destination.Position - ship.Position;
                    bool movingAway = toDestination.LengthSquared() > 1f &&
                        Vector3.Dot(ship.Velocity, toDestination) < 0f;
                    bool stalled = ship.Velocity.LengthSquared() < 1f;
                    if (!IsRouteEndDestination(ship, destination) || movingAway || stalled)
                    {
                        ship.ConfigureTrafficBehavior(
                            TrafficZoneBehaviorType.TraderRoute,
                            $"mission-convoy-escort-{mission.Id}",
                            ship.Position,
                            300f,
                            GetConvoyCruiseSpeed(mission.Difficulty),
                            12000f,
                            ship.Position,
                            destination.Position);
                    }
                }
            }

            mission.ConvoyRouteRingIndex = Math.Clamp(furthest, -1, route.Count - 1);
            NpcShip leader = null;
            for (int i = 0; i < state.ConvoyShips.Count; i++)
            {
                NpcShip candidate = state.ConvoyShips[i];
                int convoyIndex = FindConvoyShipIndex(state, candidate);
                if (candidate != null && convoyIndex >= 0 && !candidate.IsDestroyed &&
                    (mission.ConvoyDestroyedMask & (1 << convoyIndex)) == 0 &&
                    (mission.ConvoyArrivedMask & (1 << convoyIndex)) == 0)
                {
                    leader = candidate;
                    break;
                }
            }
            if (leader != null)
                mission.TargetPosition = leader.Position;
        }

        private void UpdateConvoyArrival(MissionRuntimeState state)
        {
            Mission mission = state?.Mission;
            Station destination = state?.ConvoyDestination;
            if (mission == null || destination == null)
                return;

            for (int i = 0; i < state.ConvoyShips.Count; i++)
            {
                NpcShip ship = state.ConvoyShips[i];
                int convoyIndex = FindConvoyShipIndex(state, ship);
                if (ship == null || convoyIndex < 0 || ship.IsDestroyed ||
                    (mission.ConvoyDestroyedMask & (1 << convoyIndex)) != 0 ||
                    (mission.ConvoyArrivedMask & (1 << convoyIndex)) != 0)
                    continue;
                if (Vector3.Distance(ship.Position, destination.Position) > mission.ConvoyArrivalRadius)
                    continue;

                mission.ConvoyArrivedMask |= 1 << convoyIndex;
                mission.ConvoyArrivedCount = Math.Min(mission.ConvoyShipCount, mission.ConvoyArrivedCount + 1);
                ship.SetMissionHoldPosition(false);
                _ejectNpcFromTradeLaneCallback?.Invoke(ship);
                _npcShips.Remove(ship);
                _spaceObjects.Remove(ship);
                _retiredNpcCallback?.Invoke(ship);
            }
        }

        private void UpdateConvoyAbandonment(Mission mission, MissionRuntimeState state, float deltaTime, out string failureReason)
        {
            failureReason = string.Empty;
            if (mission == null || state == null || !mission.ConvoyRouteStarted || _playerShip == null)
                return;
            if (state.ConvoyShips.Any(ship => ship != null && !ship.IsDestroyed && ship.IsTradeLaneTransit) ||
                _playerShip.IsTradeLaneTransit)
            {
                mission.ConvoyAbandonmentProgressSeconds = 0f;
                state.ConvoyWarningLogged = false;
                return;
            }

            float nearestDistance = float.MaxValue;
            for (int i = 0; i < state.ConvoyShips.Count; i++)
            {
                NpcShip ship = state.ConvoyShips[i];
                int convoyIndex = FindConvoyShipIndex(state, ship);
                if (convoyIndex < 0 || (mission.ConvoyArrivedMask & (1 << convoyIndex)) != 0)
                    continue;
                if (ship != null && !ship.IsDestroyed)
                    nearestDistance = Math.Min(nearestDistance, Vector3.Distance(_playerShip.Position, ship.Position));
            }
            if (nearestDistance <= mission.ConvoyAbandonmentRadius)
            {
                mission.ConvoyAbandonmentProgressSeconds = 0f;
                state.ConvoyWarningLogged = false;
                return;
            }

            mission.ConvoyAbandonmentProgressSeconds = Math.Min(
                mission.ConvoyAbandonmentGraceSeconds,
                mission.ConvoyAbandonmentProgressSeconds + deltaTime);
            if (!state.ConvoyWarningLogged)
            {
                state.ConvoyWarningLogged = true;
                _missionManager?.ShowNotification("Stay with the convoy", 3f);
            }
            if (mission.ConvoyAbandonmentProgressSeconds >= mission.ConvoyAbandonmentGraceSeconds)
                failureReason = "convoy abandoned by player";
        }

        private static bool IsRouteEndDestination(NpcShip ship, Station destination)
        {
            return ship?.TrafficRouteEnd.HasValue == true && destination != null &&
                Vector3.DistanceSquared(ship.TrafficRouteEnd.Value, destination.Position) <= 100f * 100f;
        }

        private void UpdateConvoyCounts(Mission mission)
        {
            if (mission == null)
                return;
            int arrived = CountConvoyArrivals(mission);
            mission.ConvoyArrivedCount = arrived;
            mission.ConvoySurvivors = Math.Max(0, mission.ConvoyShipCount - mission.ConvoyDestroyedCount);
        }

        private static int CountConvoyArrivals(Mission mission)
        {
            if (mission == null)
                return 0;
            int count = 0;
            int mask = mission.ConvoyArrivedMask;
            while (mask != 0)
            {
                count += mask & 1;
                mask >>= 1;
            }
            return count;
        }

        private static int FindConvoyShipIndex(MissionRuntimeState state, NpcShip ship)
        {
            if (state?.Mission == null || ship == null)
                return -1;
            for (int i = 0; i < state.Mission.ConvoyShipCount; i++)
            {
                if (string.Equals(ship.Name, GetConvoyShipName(state.Mission, i), StringComparison.OrdinalIgnoreCase))
                    return i;
            }
            return state.ConvoyShips.IndexOf(ship);
        }

        private static string GetConvoyShipName(Mission mission, int index) =>
            $"[MISSION] Convoy Escort {mission?.Id ?? 0} Ship {index + 1}";

        private static string GetConvoyAttackerName(Mission mission, int index) =>
            $"[MISSION] Convoy Escort Rogue {mission?.Id ?? 0} {index + 1}";

        private static int FindRaidShipIndex(MissionRuntimeState state, NpcShip ship)
        {
            if (state?.Mission == null || ship == null)
                return -1;
            for (int i = 0; i < state.Mission.RaidShipCount; i++)
            {
                if (string.Equals(ship.Name, GetRaidShipName(state.Mission, i), StringComparison.OrdinalIgnoreCase))
                    return i;
            }
            return state.RaidShips.IndexOf(ship);
        }

        private static string GetRaidShipName(Mission mission, int index) =>
            $"[MISSION] Convoy Raid {mission?.Id ?? 0} Transport {index + 1}";

        private static float GetConvoyCruiseSpeed(MissionDifficulty difficulty) => difficulty switch
        {
            MissionDifficulty.Hard or MissionDifficulty.Deadly => 180f,
            MissionDifficulty.Medium => 150f,
            _ => 120f
        };

        public IReadOnlyList<NpcShip> GetConvoyShips(Mission mission)
        {
            return mission != null && _runtimeStates.TryGetValue(mission.Id, out MissionRuntimeState state)
                ? state.ConvoyShips.ToList()
                : Array.Empty<NpcShip>();
        }

        public IReadOnlyList<NpcShip> GetConvoyRaidShips(Mission mission)
        {
            return mission != null && _runtimeStates.TryGetValue(mission.Id, out MissionRuntimeState state)
                ? state.RaidShips.ToList()
                : Array.Empty<NpcShip>();
        }

        public IReadOnlyList<NpcShip> GetConvoyAttackers(Mission mission)
        {
            return mission != null && _runtimeStates.TryGetValue(mission.Id, out MissionRuntimeState state)
                ? state.ConvoyHostiles.Where(attacker => attacker != null && !attacker.IsDestroyed && _npcShips.Contains(attacker)).ToList()
                : Array.Empty<NpcShip>();
        }

        public int GetConvoySurvivorCount(Mission mission)
        {
            if (mission == null)
                return 0;
            UpdateConvoyCounts(mission);
            return mission.ConvoySurvivors;
        }

        public NpcShip GetConvoyCombatTarget(NpcShip npc)
        {
            if (npc == null || npc.IsDestroyed)
                return null;
            foreach (MissionRuntimeState state in _runtimeStates.Values)
            {
                Mission mission = state.Mission;
                if (mission == null || mission.Status != MissionStatus.Active ||
                    mission.Type != MissionType.ConvoyEscort ||
                    mission.ConvoyStage != ConvoyEscortStage.EncounterActive ||
                    !state.ConvoyHostiles.Contains(npc))
                    continue;

                NpcShip best = null;
                float bestDistance = float.MaxValue;
                for (int i = 0; i < state.ConvoyShips.Count; i++)
                {
                    NpcShip convoy = state.ConvoyShips[i];
                    int convoyIndex = FindConvoyShipIndex(state, convoy);
                    if (convoyIndex < 0 || (mission.ConvoyArrivedMask & (1 << convoyIndex)) != 0)
                        continue;
                    if (convoy == null || convoy.IsDestroyed)
                        continue;
                    float distance = Vector3.DistanceSquared(npc.Position, convoy.Position);
                    if (distance <= mission.ConvoyAbandonmentRadius * mission.ConvoyAbandonmentRadius && distance < bestDistance)
                    {
                        bestDistance = distance;
                        best = convoy;
                    }
                }
                return best;
            }
            return null;
        }

        private bool TryBindTradeLaneDefenseMission(MissionRuntimeState state, out string failureReason)
        {
            failureReason = string.Empty;
            Mission mission = state?.Mission;
            if (mission == null)
            {
                failureReason = "mission was null";
                return false;
            }

            if (mission.Type != MissionType.TradeLaneDefense ||
                !string.Equals(
                    FactionManager.NormalizeFactionId(mission.FactionId),
                    FactionManager.LibertyPolice,
                    StringComparison.OrdinalIgnoreCase))
            {
                failureReason = "trade-lane defense mission faction is invalid";
                return false;
            }

            TradeLane lane = GetTradeLanes().FirstOrDefault(candidate =>
                string.Equals(candidate.LaneId, mission.TargetLaneId, StringComparison.OrdinalIgnoreCase));
            if (lane == null)
            {
                failureReason = "target trade lane is unavailable in this system";
                return false;
            }

            if (mission.TargetRingIndex <= 0 ||
                mission.TargetRingIndex >= lane.ForwardRings.Count - 1 ||
                mission.TargetRingIndex >= lane.ReverseRings.Count ||
                !string.Equals(
                    mission.TargetSegmentId,
                    $"{lane.LaneId}:ring:{mission.TargetRingIndex}",
                    StringComparison.OrdinalIgnoreCase))
            {
                failureReason = "target trade-lane segment identity is invalid";
                return false;
            }

            if (mission.TargetSystemIndex > 0 && lane.Config?.SystemIndex > 0 &&
                mission.TargetSystemIndex != lane.Config.SystemIndex)
            {
                failureReason = "target trade lane belongs to another system";
                return false;
            }

            TradelaneRing targetRing = lane.ForwardRings[mission.TargetRingIndex];
            if (targetRing == null || !TradeLaneStateSanitizer.IsFinite(targetRing.Position))
            {
                failureReason = "target trade-lane ring position is invalid";
                return false;
            }

            state.TargetTradeLane = lane;
            mission.TargetPosition = targetRing.Position;
            mission.TargetSpaceObject = targetRing;
            mission.TargetLocation = lane.Config?.Name ?? lane.LaneId;
            mission.DefenseAttackForceSize = mission.DefenseAttackForceSize > 0
                ? Math.Clamp(mission.DefenseAttackForceSize, 2, 4)
                : Math.Clamp(mission.TargetCount > 0 ? mission.TargetCount : GetTradeLaneDefenseAttackForceSize(mission.Difficulty), 2, 4);
            mission.TargetCount = mission.DefenseAttackForceSize;
            mission.RequiredProgress = mission.DefenseAttackForceSize;
            mission.DefenseActivationRadius = mission.DefenseActivationRadius > 0f
                ? Math.Clamp(mission.DefenseActivationRadius, 250f, 5000f)
                : 1200f;
            mission.DefenseFailureHoldSeconds = mission.DefenseFailureHoldSeconds > 0f
                ? Math.Clamp(mission.DefenseFailureHoldSeconds, 1f, 30f)
                : GetTradeLaneDefenseHoldSeconds(lane);
            if (!Enum.IsDefined(typeof(TradeLaneDefenseStage), mission.DefenseStage))
                mission.DefenseStage = TradeLaneDefenseStage.EnRoute;

            if ((mission.DefenseStage == TradeLaneDefenseStage.AttackActive ||
                 mission.DefenseStage == TradeLaneDefenseStage.AwaitingRecovery) &&
                state.MissionHostiles.Count == 0 &&
                !state.TradeLaneDefenseEncounterBound)
            {
                if (!SpawnTradeLaneDefenseAttackers(state, out failureReason))
                    return false;
            }

            UpdateDefenseAttackerCount(state);
            return true;
        }

        private void UpdateTradeLaneDefenseMission(
            MissionRuntimeState state,
            float deltaTime,
            out bool complete,
            out string failureReason)
        {
            complete = false;
            failureReason = string.Empty;
            Mission mission = state?.Mission;
            TradeLane lane = state?.TargetTradeLane;
            if (mission == null || lane == null || _playerShip == null)
            {
                failureReason = "defense encounter binding became invalid";
                return;
            }

            UpdateDefenseAttackerCount(state);
            if (mission.DefenseStage == TradeLaneDefenseStage.EnRoute)
            {
                bool inDefenseArea = mission.TargetPosition.HasValue &&
                    Vector3.Distance(_playerShip.Position, mission.TargetPosition.Value) <= mission.DefenseActivationRadius;
                if (!inDefenseArea)
                    return;

                // A stale/pre-existing disruption is not a hostile mission
                // failure. Wait for the shared lane to be operational before
                // the defense encounter officially begins.
                if (lane.GetDisruptionState(mission.TargetRingIndex) != TradeLaneDisruptionState.Operational)
                    return;

                if (!SpawnTradeLaneDefenseAttackers(state, out failureReason))
                    return;
            }

            if (mission.DefenseStage != TradeLaneDefenseStage.AttackActive &&
                mission.DefenseStage != TradeLaneDefenseStage.AwaitingRecovery)
            {
                return;
            }

            UpdateDefenseAttackerCount(state);
            lane.TryGetDisruptionInfo(mission.TargetRingIndex, out TradeLaneDisruptionInfo disruption);
            bool hostileMissionDisruption = IsMissionDefenseDisruption(state, disruption);
            bool genuinelyDisrupted = disruption != null &&
                disruption.State == TradeLaneDisruptionState.Disrupted &&
                hostileMissionDisruption;

            if (genuinelyDisrupted)
            {
                mission.DefenseStage = TradeLaneDefenseStage.AwaitingRecovery;
                mission.DefenseFailureHoldProgressSeconds = Math.Min(
                    mission.DefenseFailureHoldSeconds,
                    mission.DefenseFailureHoldProgressSeconds + deltaTime);
                if (mission.DefenseFailureHoldProgressSeconds >= mission.DefenseFailureHoldSeconds)
                {
                    mission.DefenseStage = TradeLaneDefenseStage.Failed;
                    failureReason = "Liberty Rogues held the trade lane offline";
                }
                return;
            }

            mission.DefenseFailureHoldProgressSeconds = 0f;
            if (disruption == null || disruption.State != TradeLaneDisruptionState.Disrupted)
                mission.DefenseStage = TradeLaneDefenseStage.AttackActive;

            if (mission.DefenseAttackersRemaining <= 0 &&
                lane.GetDisruptionState(mission.TargetRingIndex) == TradeLaneDisruptionState.Operational)
            {
                mission.DefenseStage = TradeLaneDefenseStage.Successful;
                mission.ObjectiveComplete = true;
                complete = true;
            }
            else if (mission.DefenseAttackersRemaining <= 0)
            {
                mission.DefenseStage = TradeLaneDefenseStage.AwaitingRecovery;
            }
        }

        private bool SpawnTradeLaneDefenseAttackers(MissionRuntimeState state, out string failureReason)
        {
            failureReason = string.Empty;
            Mission mission = state?.Mission;
            if (mission == null || state.TargetTradeLane == null || _playerShip == null)
            {
                failureReason = "defense spawn binding is unavailable";
                return false;
            }

            if (state.MissionHostiles.Count > 0)
            {
                mission.DefenseStage = TradeLaneDefenseStage.AttackActive;
                mission.DefenseActivationStarted = true;
                state.TradeLaneDefenseEncounterBound = true;
                return true;
            }

            const int maximumMissionNpcPopulation = 64;
            int requested = Math.Clamp(
                mission.DefenseAttackForceSize > 0
                    ? mission.DefenseAttackForceSize
                    : GetTradeLaneDefenseAttackForceSize(mission.Difficulty),
                2,
                4);
            Vector3 anchor = mission.TargetPosition ?? state.TargetTradeLane.ForwardRings[mission.TargetRingIndex].Position;
            Vector3[] offsets =
            {
                new Vector3(-850f, 120f, 260f),
                new Vector3(850f, -80f, -220f),
                new Vector3(-420f, -160f, -760f),
                new Vector3(470f, 200f, 720f)
            };

            for (int i = 0; i < requested && _npcShips.Count < maximumMissionNpcPopulation; i++)
            {
                Vector3 spawnPosition = anchor + offsets[i];
                NpcShip attacker = new NpcShip(
                    $"[MISSION] Trade-Lane Defense Rogue {i + 1}",
                    spawnPosition,
                    anchor,
                    900f,
                    0f,
                    FactionManager.LibertyRogues);
                attacker.ConfigureTrafficBehavior(
                    TrafficZoneBehaviorType.PirateAmbush,
                    $"mission-trade-lane-defense-{mission.Id}",
                    anchor,
                    1200f,
                    220f,
                    12000f);
                attacker.OnDestroyed += npc => _spawnedNpcDestroyedCallback?.Invoke(npc);
                attacker.Model = _playerShip.Model;
                attacker.SetLoadout(NpcEquipmentLoadoutFactory.CreateForNpc(
                    attacker.Name,
                    attacker.FactionId,
                    attacker.ModelPath,
                    TrafficZoneBehaviorType.PirateAmbush,
                    MapMissionLoadoutTier(mission.Difficulty)));
                _npcShips.Add(attacker);
                _spaceObjects.Add(attacker);
                state.MissionHostiles.Add(attacker);
                mission.TargetSpaceObject ??= attacker;
            }

            UpdateDefenseAttackerCount(state);
            if (mission.DefenseAttackersRemaining < requested)
            {
                int spawned = mission.DefenseAttackersRemaining;
                CleanupMissionTransientNpcs(state);
                failureReason = $"the full Rogue defense force could not be spawned ({spawned}/{requested})";
                return false;
            }

            mission.DefenseAttackForceSize = requested;
            mission.TargetCount = requested;
            mission.RequiredProgress = requested;
            mission.DefenseStage = TradeLaneDefenseStage.AttackActive;
            mission.DefenseActivationStarted = true;
            state.TradeLaneDefenseEncounterBound = true;
            Console.WriteLine($"[MISSION] Trade-lane defense attack started with {mission.DefenseAttackersRemaining}/{requested} Rogue ships (mission #{mission.Id})");
            return true;
        }

        private void UpdateDefenseAttackerCount(MissionRuntimeState state)
        {
            if (state?.Mission == null || state.Mission.Type != MissionType.TradeLaneDefense)
                return;

            state.MissionHostiles.RemoveWhere(attacker =>
                attacker == null || !_npcShips.Contains(attacker));
            state.Mission.DefenseAttackersRemaining = state.MissionHostiles.Count(attacker =>
                attacker != null && !attacker.IsDestroyed && _npcShips.Contains(attacker));
        }

        private static bool IsMissionDefenseDisruption(
            MissionRuntimeState state,
            TradeLaneDisruptionInfo disruption)
        {
            return state?.Mission != null && disruption != null &&
                disruption.Source == TradeLaneDisruptionSource.Npc &&
                state.MissionHostiles.Any(attacker => attacker != null && !attacker.IsDestroyed &&
                    string.Equals(attacker.Name, disruption.SourceName, StringComparison.OrdinalIgnoreCase));
        }

        public TradeLaneAttackTarget GetTradeLaneAttackTarget(NpcShip npc)
        {
            if (npc == null || npc.IsDestroyed)
                return null;

            foreach (MissionRuntimeState state in _runtimeStates.Values)
            {
                Mission mission = state.Mission;
                if (mission == null || mission.Status != MissionStatus.Active ||
                    mission.Type != MissionType.TradeLaneDefense ||
                    mission.DefenseStage != TradeLaneDefenseStage.AttackActive ||
                    !state.MissionHostiles.Contains(npc) || state.TargetTradeLane == null ||
                    mission.TargetRingIndex < 0 || mission.TargetRingIndex >= state.TargetTradeLane.ForwardRings.Count)
                {
                    continue;
                }

                if (mission.TargetPosition.HasValue &&
                    Vector3.DistanceSquared(npc.Position, mission.TargetPosition.Value) >
                    mission.DefenseActivationRadius * mission.DefenseActivationRadius * 9f)
                {
                    return null;
                }

                return new TradeLaneAttackTarget
                {
                    Lane = state.TargetTradeLane,
                    RingIndex = mission.TargetRingIndex,
                    Position = state.TargetTradeLane.ForwardRings[mission.TargetRingIndex].Position,
                    SourceName = npc.Name ?? string.Empty
                };
            }

            return null;
        }

        public int GetTradeLaneDefenseAttackerCount(Mission mission)
        {
            if (mission == null || !_runtimeStates.TryGetValue(mission.Id, out MissionRuntimeState state))
                return 0;
            UpdateDefenseAttackerCount(state);
            return mission.DefenseAttackersRemaining;
        }

        private void CleanupMissionTransientNpcs(MissionRuntimeState state)
        {
            if (state?.Mission == null ||
                (state.Mission.Type != MissionType.TradeLaneDefense &&
                 state.Mission.Type != MissionType.ConvoyEscort &&
                 state.Mission.Type != MissionType.ConvoyRaid))
                return;

            IEnumerable<NpcShip> ownedShips = state.Mission.Type == MissionType.ConvoyEscort
                ? state.ConvoyShips.Concat(state.ConvoyHostiles).Distinct().ToList()
                : state.Mission.Type == MissionType.ConvoyRaid
                    ? state.RaidShips.Distinct().ToList()
                    : state.MissionHostiles.ToList();
            foreach (NpcShip attacker in ownedShips)
            {
                if (attacker == null)
                    continue;

                attacker.ClearEncounterState();
                attacker.SetMissionHoldPosition(false);
                _ejectNpcFromTradeLaneCallback?.Invoke(attacker);
                _npcShips.Remove(attacker);
                _spaceObjects.Remove(attacker);
                _retiredNpcCallback?.Invoke(attacker);
            }

            state.MissionHostiles.Clear();
            state.ConvoyShips.Clear();
            state.ConvoyHostiles.Clear();
            state.RaidShips.Clear();
            state.TradeLaneDefenseEncounterBound = false;
            state.Mission.DefenseAttackersRemaining = 0;
            state.Mission.ConvoyAttackersRemaining = 0;
        }

        private static int GetTradeLaneDefenseAttackForceSize(MissionDifficulty difficulty) => difficulty switch
        {
            MissionDifficulty.Medium => 3,
            MissionDifficulty.Hard => 4,
            MissionDifficulty.Deadly => 4,
            _ => 2
        };

        private static float GetTradeLaneDefenseHoldSeconds(TradeLane lane)
        {
            float recovery = TradeLaneStateSanitizer.Positive(
                lane?.Config?.DisruptionRecoverySeconds ?? 0f,
                TradeLane.DefaultRecoveryDurationSeconds);
            return Math.Min(MissionManager.TradeLaneDefenseHoldSeconds, Math.Max(1f, recovery * 0.75f));
        }

        private bool TryBindDestroyHostilesMission(MissionRuntimeState state, out string failureReason)
        {
            failureReason = string.Empty;
            Mission mission = state.Mission;
            if (mission == null)
            {
                failureReason = "mission was null";
                return false;
            }
            if (mission.RequiredProgress <= 0 || mission.RequiredProgress > 12)
            {
                failureReason = "hostile target count is outside the bounded prototype range";
                return false;
            }
            if (_playerShip == null)
            {
                failureReason = "player ship not available";
                return false;
            }

            Vector3 anchor = mission.TargetPosition ?? _playerShip.Position + _playerShip.Forward * 1100f;
            mission.TargetPosition = anchor;
            int remaining = Math.Max(0, mission.RequiredProgress - mission.CurrentProgress);
            for (int i = 0; i < remaining; i++)
            {
                Vector3 offset = new Vector3((i - 1) * 260f, (i % 2) * 140f, (i % 3) * 180f);
                NpcShip target = new NpcShip(
                    $"[MISSION] Rogue Hunt target {mission.CurrentProgress + i + 1}",
                    anchor + offset,
                    anchor,
                    700f,
                    0f,
                    FactionManager.LibertyRogues);
                target.ConfigureTrafficBehavior(
                    TrafficZoneBehaviorType.PirateAmbush,
                    $"mission-rogue-hunt-{mission.Id}",
                    anchor,
                    700f,
                    140f,
                    10000f);
                target.OnDestroyed += npc => _spawnedNpcDestroyedCallback?.Invoke(npc);
                target.Model = _playerShip.Model;
                target.SetLoadout(NpcEquipmentLoadoutFactory.CreateForNpc(
                    target.Name,
                    target.FactionId,
                    target.ModelPath,
                    TrafficZoneBehaviorType.PirateAmbush,
                    MapMissionLoadoutTier(mission.Difficulty)));
                _npcShips.Add(target);
                _spaceObjects.Add(target);
                state.MissionHostiles.Add(target);
                mission.TargetSpaceObject ??= target;
            }

            Console.WriteLine($"[MISSION] Rogue Hunt bound {state.MissionHostiles.Count} mission targets (mission #{mission.Id})");
            return true;
        }

        private Station FindStation(string stationName)
        {
            if (string.IsNullOrWhiteSpace(stationName)) return null;
            return GetKnownStations().FirstOrDefault(station =>
                string.Equals(station.Name, stationName, StringComparison.OrdinalIgnoreCase));
        }

        private bool TryBindBountyMission(MissionRuntimeState state, out string failureReason)
        {
            failureReason = string.Empty;

            Mission mission = state.Mission;
            if (mission == null)
            {
                failureReason = "mission was null";
                return false;
            }

            NpcShip existingTarget = ResolveExistingBountyTarget(mission);
            if (existingTarget != null)
            {
                state.BountyTarget = existingTarget;
                mission.TargetSpaceObject = existingTarget;
                mission.TargetPosition = existingTarget.Position;
                Console.WriteLine($"[MISSION] Bounty target resolved: {existingTarget.Name} (mission #{mission.Id})");
                return true;
            }

            if (_playerShip == null)
            {
                failureReason = "player ship not available";
                return false;
            }

            Vector3 spawnPosition = GetBountySpawnPosition();
            string targetName = string.IsNullOrWhiteSpace(mission.Target) ? $"Bounty Target {mission.Id}" : mission.Target.Trim();
            string factionId = DetermineBountyFaction(mission, targetName);

            NpcShip target = new NpcShip(
                $"[BOUNTY] {targetName}",
                spawnPosition,
                spawnPosition,
                1f,
                0f,
                factionId);

            target.ConfigureTrafficBehavior(
                TrafficZoneBehaviorType.PirateAmbush,
                $"mission-bounty-{mission.Id}",
                spawnPosition,
                1500f,
                180f,
                20000f);
            target.OnDestroyed += npc => _spawnedNpcDestroyedCallback?.Invoke(npc);
            target.SetLoadout(NpcEquipmentLoadoutFactory.CreateForNpc(
                target.Name,
                target.FactionId,
                target.ModelPath,
                TrafficZoneBehaviorType.PirateAmbush,
                MapMissionLoadoutTier(mission.Difficulty)));

            _npcShips.Add(target);
            _spaceObjects.Add(target);

            state.BountyTarget = target;
            mission.TargetSpaceObject = target;
            mission.TargetPosition = target.Position;

            Console.WriteLine($"[MISSION] Bounty target spawned: {target.Name} at {target.Position:F1} (mission #{mission.Id})");
            return true;
        }

        private bool TryBindDeliveryMission(MissionRuntimeState state, out string failureReason)
        {
            failureReason = string.Empty;

            Mission mission = state.Mission;
            if (mission == null)
            {
                failureReason = "mission was null";
                return false;
            }

            Station destination = ResolveDeliveryDestination(mission);
            if (destination == null)
            {
                failureReason = $"destination '{mission.Destination}' could not be resolved";
                return false;
            }

            Commodity commodity = ResolveDeliveryCommodity(mission.Target);
            if (commodity == null)
            {
                failureReason = $"cargo target '{mission.Target}' could not be resolved";
                return false;
            }

            int quantity = 1;
            if (_playerShip?.CargoHold == null)
            {
                failureReason = "player cargo hold unavailable";
                return false;
            }

            if (!_playerShip.CargoHold.CanFit(commodity, quantity))
            {
                failureReason = $"not enough cargo space for {commodity.Name}";
                return false;
            }

            if (!_playerShip.CargoHold.AddCommodity(commodity, quantity))
            {
                failureReason = $"failed to assign mission cargo '{commodity.Name}'";
                return false;
            }

            state.DeliveryDestination = destination;
            state.DeliveryCommodity = commodity;
            state.DeliveryQuantity = quantity;
            mission.TargetSpaceObject = destination;
            mission.TargetPosition = destination.Position;

            Console.WriteLine($"[MISSION] Delivery cargo assigned: {commodity.Name} x{quantity} -> {destination.Name} (mission #{mission.Id})");
            return true;
        }

        private bool TryBindCourierMission(MissionRuntimeState state, out string failureReason)
        {
            failureReason = string.Empty;
            Mission mission = state.Mission;
            if (mission == null)
            {
                failureReason = "mission was null";
                return false;
            }

            Station destination = ResolveCourierDestination(mission);
            if (destination == null)
            {
                failureReason = $"destination '{mission.Destination}' could not be resolved";
                return false;
            }

            Commodity commodity = ResolveCourierCommodity(mission);
            if (commodity == null)
            {
                failureReason = $"package '{mission.PackageId}' could not be resolved";
                return false;
            }

            int quantity = mission.PackageQuantity;
            if (quantity <= 0)
            {
                failureReason = "package quantity must be positive";
                return false;
            }

            int authoritativeVolume = commodity.VolumePerUnit * quantity;
            if (mission.PackageVolume > 0 && mission.PackageVolume != authoritativeVolume)
            {
                failureReason = $"package volume metadata does not match {commodity.Name}";
                return false;
            }

            if (_playerShip?.CargoHold == null)
            {
                failureReason = "player cargo hold unavailable";
                return false;
            }

            if (!_playerShip.CargoHold.CanFit(commodity, quantity))
            {
                failureReason = $"not enough cargo space for {commodity.Name} x{quantity}";
                return false;
            }

            if (!_playerShip.CargoHold.AddMissionCargo(mission.Id, commodity, quantity))
            {
                failureReason = $"failed to reserve mission package '{commodity.Name}'";
                return false;
            }

            mission.DestinationStationId = Mission.BuildStationIdentity(destination);
            mission.PackageId = commodity.Id;
            mission.PackageVolume = authoritativeVolume;
            mission.MissionCargoLoaded = true;
            mission.DeliveredQuantity = 0;
            state.DeliveryDestination = destination;
            state.DeliveryCommodity = commodity;
            state.DeliveryQuantity = quantity;
            mission.TargetSpaceObject = destination;
            mission.TargetPosition = destination.Position;

            Console.WriteLine($"[MISSION] Courier package loaded: {commodity.Name} x{quantity} -> {destination.Name} (mission #{mission.Id})");
            return true;
        }

        private bool TryBindFreightMission(MissionRuntimeState state, out string failureReason)
        {
            failureReason = string.Empty;
            Mission mission = state?.Mission;
            if (mission == null || _marketManager == null)
            {
                failureReason = "freight market authority is unavailable";
                return false;
            }

            Station destination = ResolveCourierDestination(mission);
            Commodity commodity = CommodityCatalog.GetByIdOrName(mission.CommodityId);
            if (destination == null)
            {
                failureReason = $"destination '{mission.Destination}' could not be resolved";
                return false;
            }

            if (commodity == null || commodity.IsMissionCargo || commodity.IsContraband ||
                commodity.VolumePerUnit <= 0 || mission.RequiredQuantity <= 0)
            {
                failureReason = "freight commodity metadata is invalid";
                return false;
            }

            if (_marketManager.GetListingForCommodity(destination, commodity) == null)
            {
                failureReason = $"{commodity.Name} is not traded at {destination.Name}";
                return false;
            }

            mission.DestinationStationId = Mission.BuildStationIdentity(destination);
            mission.RequiredProgress = mission.RequiredQuantity;
            state.DeliveryDestination = destination;
            state.DeliveryCommodity = commodity;
            state.DeliveryQuantity = mission.RequiredQuantity;
            mission.TargetSpaceObject = destination;
            mission.TargetPosition = destination.Position;
            return true;
        }

        private bool TryBindSmugglingMission(MissionRuntimeState state, out string failureReason)
        {
            failureReason = string.Empty;
            Mission mission = state?.Mission;
            Commodity commodity = CommodityCatalog.GetByIdOrName(mission?.CommodityId);
            Station destination = ResolveCourierDestination(mission);
            if (mission == null || destination == null || commodity == null ||
                !commodity.IsContraband || commodity.IsMissionCargo || commodity.VolumePerUnit <= 0 ||
                mission.RequiredQuantity <= 0 || mission.IssuedCargoQuantity != mission.RequiredQuantity ||
                _playerShip?.CargoHold == null ||
                !_playerShip.CargoHold.HasMissionCargo(mission.Id, commodity.Id, mission.RequiredQuantity))
            {
                failureReason = "smuggling cargo or destination metadata is invalid";
                return false;
            }

            mission.DestinationStationId = Mission.BuildStationIdentity(destination);
            mission.TargetSpaceObject = destination;
            mission.TargetPosition = destination.Position;
            mission.RequiredProgress = mission.RequiredQuantity;
            state.DeliveryDestination = destination;
            state.DeliveryCommodity = commodity;
            state.DeliveryQuantity = mission.RequiredQuantity;
            return true;
        }

        private bool TryBindExportMission(MissionRuntimeState state, out string failureReason)
        {
            failureReason = string.Empty;
            Mission mission = state?.Mission;
            if (mission == null || _marketManager == null)
            {
                failureReason = "export market authority is unavailable";
                return false;
            }

            Station destination = ResolveCourierDestination(mission);
            Commodity commodity = CommodityCatalog.GetByIdOrName(mission.CommodityId);
            if (destination == null)
            {
                failureReason = $"destination '{mission.Destination}' could not be resolved";
                return false;
            }

            if (commodity == null || commodity.IsMissionCargo || commodity.IsContraband ||
                commodity.VolumePerUnit <= 0 || mission.RequiredQuantity <= 0 ||
                mission.IssuedCargoQuantity != mission.RequiredQuantity ||
                _playerShip?.CargoHold == null ||
                !_playerShip.CargoHold.HasMissionCargo(mission.Id, commodity.Id, mission.RequiredQuantity))
            {
                failureReason = "issued export cargo is missing or invalid";
                return false;
            }

            if (_marketManager.GetListingForCommodity(destination, commodity) == null)
            {
                failureReason = $"{commodity.Name} is not traded at {destination.Name}";
                return false;
            }

            mission.DestinationStationId = Mission.BuildStationIdentity(destination);
            mission.RequiredProgress = mission.RequiredQuantity;
            state.DeliveryDestination = destination;
            state.DeliveryCommodity = commodity;
            state.DeliveryQuantity = mission.RequiredQuantity;
            mission.TargetSpaceObject = destination;
            mission.TargetPosition = destination.Position;
            return true;
        }

        private bool TryBindEscortMission(MissionRuntimeState state, out string failureReason)
        {
            failureReason = string.Empty;

            Mission mission = state.Mission;
            if (mission == null)
            {
                failureReason = "mission was null";
                return false;
            }

            Station destination = ResolveEscortDestination(mission);
            if (destination == null)
            {
                failureReason = $"destination '{mission.Destination}' could not be resolved";
                return false;
            }

            state.EscortDestination = destination;

            NpcShip escort = ResolveEscortTarget(mission, state);
            if (escort == null)
            {
                if (_playerShip == null)
                {
                    failureReason = "player ship not available";
                    return false;
                }

                Vector3 spawnPosition = GetEscortSpawnPosition(destination);
                string escortName = GetEscortDisplayName(mission);
                string factionId = DetermineEscortFaction(mission);

                escort = new NpcShip(
                    $"[ESCORT] {escortName}",
                    spawnPosition,
                    spawnPosition,
                    1f,
                    0f,
                    factionId);

                escort.ConfigureTrafficBehavior(
                    TrafficZoneBehaviorType.TraderRoute,
                    $"mission-escort-{mission.Id}",
                    spawnPosition,
                    900f,
                    Math.Max(120f, mission.Difficulty switch
                    {
                        MissionDifficulty.Easy => 150f,
                        MissionDifficulty.Medium => 165f,
                        MissionDifficulty.Hard => 180f,
                        MissionDifficulty.Deadly => 200f,
                        _ => 150f
                    }),
                    22000f,
                    spawnPosition,
                    destination.Position);
                escort.OnDestroyed += npc => _spawnedNpcDestroyedCallback?.Invoke(npc);
                escort.Model = _playerShip?.Model;
                escort.SetLoadout(NpcEquipmentLoadoutFactory.CreateForNpc(
                    escort.Name,
                    escort.FactionId,
                    escort.ModelPath,
                    TrafficZoneBehaviorType.TraderRoute,
                    MapMissionLoadoutTier(mission.Difficulty)));
                _npcShips.Add(escort);
                _spaceObjects.Add(escort);
                Console.WriteLine($"[MISSION] Escort spawned: {escort.Name} -> {destination.Name} (mission #{mission.Id})");
            }
            else
            {
                Console.WriteLine($"[MISSION] Escort resolved: {escort.Name} -> {destination.Name} (mission #{mission.Id})");
                if (escort.Model == null && _playerShip?.Model != null)
                {
                    escort.Model = _playerShip.Model;
                }

                if (escort.TrafficBehavior != TrafficZoneBehaviorType.TraderRoute)
                {
                    escort.ConfigureTrafficBehavior(
                        TrafficZoneBehaviorType.TraderRoute,
                        $"mission-escort-{mission.Id}",
                        escort.Position,
                        900f,
                        Math.Max(120f, mission.Difficulty switch
                        {
                            MissionDifficulty.Easy => 150f,
                            MissionDifficulty.Medium => 165f,
                            MissionDifficulty.Hard => 180f,
                            MissionDifficulty.Deadly => 200f,
                            _ => 150f
                        }),
                        22000f,
                        escort.Position,
                        destination.Position);
                }
            }

            state.EscortTarget = escort;
            state.EscortUnderAttackLogged = false;
            mission.TargetSpaceObject = escort;
            mission.TargetPosition = escort.Position;
            return true;
        }

        private void FailMission(Mission mission, string reason)
        {
            Console.WriteLine($"[MISSION] Failed: {mission.Description} | Reason: {reason}");
            _missionManager?.FailMission(mission, reason);
        }

        private bool TryRemoveDeliveryCargo(MissionRuntimeState state)
        {
            Mission mission = state.Mission;
            Commodity commodity = state.DeliveryCommodity ?? ResolveDeliveryCommodity(mission?.Target);
            if (commodity == null || _playerShip?.CargoHold == null)
            {
                return false;
            }

            int quantity = state.DeliveryQuantity > 0 ? state.DeliveryQuantity : 1;
            int currentQuantity = _playerShip.CargoHold.GetCommodityQuantity(commodity.Name);
            if (currentQuantity < quantity)
            {
                return false;
            }

            return _playerShip.CargoHold.RemoveCommodity(commodity, quantity);
        }

        private bool TryRemoveCourierCargo(MissionRuntimeState state)
        {
            Mission mission = state.Mission;
            Commodity commodity = state.DeliveryCommodity ?? ResolveCourierCommodity(mission);
            if (mission == null || commodity == null || _playerShip?.CargoHold == null)
            {
                return false;
            }

            int quantity = mission.PackageQuantity > 0 ? mission.PackageQuantity : state.DeliveryQuantity;
            return _playerShip.CargoHold.RemoveMissionCargo(mission.Id, commodity, quantity);
        }

        private bool TryCompleteFreightDelivery(MissionRuntimeState state, Station station)
        {
            Mission mission = state?.Mission;
            Commodity commodity = state?.DeliveryCommodity ?? CommodityCatalog.GetByIdOrName(mission?.CommodityId);
            CargoHold cargo = _playerShip?.CargoHold;
            int quantity = mission?.RequiredQuantity ?? 0;
            if (mission == null || commodity == null || cargo == null || quantity <= 0 ||
                !cargo.HasMissionCargo(mission.Id, commodity.Id, quantity) ||
                cargo.GetMissionCargoQuantity(mission.Id) != quantity)
            {
                return false;
            }

            string marketFailure = string.Empty;
            if (_marketManager == null ||
                !_marketManager.CanAddSupply(station, commodity, quantity, out marketFailure) ||
                _missionManager == null)
            {
                if (!string.IsNullOrWhiteSpace(marketFailure))
                    Console.WriteLine($"[MISSION] Freight delivery held: {marketFailure}");
                return false;
            }

            if (!_missionManager.CanPayFreightReward(mission, out string rewardPreflightFailure))
            {
                Console.WriteLine($"[MISSION] Freight delivery held: {rewardPreflightFailure}");
                return false;
            }

            if (!cargo.RemoveMissionCargo(mission.Id, commodity, quantity))
                return false;

            if (!_marketManager.TryAddSupply(station, commodity, quantity, out string addFailure))
            {
                // The preflight above should make this unreachable in the
                // single-threaded game loop. Restore the exact protected stack
                // if the market authority rejects the commit defensively.
                cargo.AddMissionCargo(mission.Id, commodity, quantity);
                cargo.RegisterFreightReservation(mission.Id, commodity, quantity);
                Console.WriteLine($"[MISSION] Freight delivery rolled back: {addFailure}");
                return false;
            }

            _marketIntelligence?.ObserveStation(station, "CurrentStation");

            mission.DeliveredQuantity = quantity;
            mission.ObjectiveComplete = true;
            if (!_missionManager.CompleteFreightMission(mission, out string rewardFailure))
            {
                cargo.AddMissionCargo(mission.Id, commodity, quantity);
                cargo.RegisterFreightReservation(mission.Id, commodity, quantity);
                Console.WriteLine($"[MISSION] Freight reward failed after delivery: {rewardFailure}");
                return false;
            }

            Console.WriteLine($"[MISSION] Freight delivered: {commodity.Name} x{quantity} -> {station.Name} (mission #{mission.Id})");
            return true;
        }

        private bool TryCompleteExportDelivery(MissionRuntimeState state, Station station)
        {
            Mission mission = state?.Mission;
            Commodity commodity = state?.DeliveryCommodity ?? CommodityCatalog.GetByIdOrName(mission?.CommodityId);
            CargoHold cargo = _playerShip?.CargoHold;
            int quantity = mission?.RequiredQuantity ?? 0;
            if (mission == null || commodity == null || cargo == null || quantity <= 0 ||
                mission.IssuedCargoQuantity != quantity ||
                !cargo.HasMissionCargo(mission.Id, commodity.Id, quantity) ||
                cargo.GetMissionCargoQuantity(mission.Id) != quantity)
            {
                return false;
            }

            string marketFailure = string.Empty;
            if (_marketManager == null ||
                !_marketManager.CanAddSupply(station, commodity, quantity, out marketFailure) ||
                _missionManager == null)
            {
                if (!string.IsNullOrWhiteSpace(marketFailure))
                    Console.WriteLine($"[MISSION] Export delivery held: {marketFailure}");
                return false;
            }

            if (!_missionManager.CanPayExportReward(mission, out string rewardPreflightFailure))
            {
                Console.WriteLine($"[MISSION] Export delivery held: {rewardPreflightFailure}");
                return false;
            }

            if (!cargo.RemoveMissionCargo(mission.Id, commodity, quantity))
                return false;

            if (!_marketManager.TryAddSupply(station, commodity, quantity, out string addFailure))
            {
                cargo.AddMissionCargo(mission.Id, commodity, quantity);
                Console.WriteLine($"[MISSION] Export delivery rolled back: {addFailure}");
                return false;
            }

            _marketIntelligence?.ObserveStation(station, "CurrentStation");

            mission.DeliveredQuantity = quantity;
            mission.MissionCargoLoaded = false;
            mission.ObjectiveComplete = true;
            if (!_missionManager.CompleteExportMission(mission, out string rewardFailure))
            {
                _marketManager.TryRemoveSupply(station, commodity, quantity, 0, out _);
                cargo.AddMissionCargo(mission.Id, commodity, quantity);
                mission.MissionCargoLoaded = true;
                mission.DeliveredQuantity = 0;
                Console.WriteLine($"[MISSION] Export reward failed after delivery: {rewardFailure}");
                return false;
            }

            Console.WriteLine($"[MISSION] Export delivered: {commodity.Name} x{quantity} -> {station.Name} (mission #{mission.Id})");
            return true;
        }

        private bool TryCompleteSmugglingDelivery(MissionRuntimeState state, Station station)
        {
            Mission mission = state?.Mission;
            Commodity commodity = state?.DeliveryCommodity ?? CommodityCatalog.GetByIdOrName(mission?.CommodityId);
            CargoHold cargo = _playerShip?.CargoHold;
            int quantity = mission?.RequiredQuantity ?? 0;
            if (mission == null || commodity == null || cargo == null || quantity <= 0 ||
                mission.IssuedCargoQuantity != quantity ||
                !cargo.HasMissionCargo(mission.Id, commodity.Id, quantity) ||
                cargo.GetMissionCargoQuantity(mission.Id) != quantity ||
                _missionManager == null)
            {
                return false;
            }

            if (!_missionManager.CompleteSmugglingMission(mission, station, out string completionFailure))
            {
                if (!string.IsNullOrWhiteSpace(completionFailure))
                    Console.WriteLine($"[MISSION] Smuggling delivery held: {completionFailure}");
                return false;
            }

            Console.WriteLine($"[MISSION] Smuggling delivered: {commodity.Name} x{quantity} -> {station.Name} (mission #{mission.Id})");
            return true;
        }

        private MissionRuntimeState GetOrCreateState(Mission mission)
        {
            if (_runtimeStates.TryGetValue(mission.Id, out MissionRuntimeState existing))
            {
                existing.Mission = mission;
                return existing;
            }

            MissionRuntimeState state = new MissionRuntimeState
            {
                Mission = mission,
                DeliveryQuantity = 1
            };
            _runtimeStates[mission.Id] = state;
            return state;
        }

        private NpcShip ResolveExistingBountyTarget(Mission mission)
        {
            if (mission == null || string.IsNullOrWhiteSpace(mission.Target))
            {
                return null;
            }

            return _npcShips.FirstOrDefault(npc =>
                npc != null &&
                !npc.IsDestroyed &&
                npc.Name != null &&
                npc.Name.IndexOf(mission.Target, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        private Station ResolveDeliveryDestination(Mission mission)
        {
            return ResolveDeliveryDestination(mission?.Destination);
        }

        public Station ResolveDeliveryDestination(string destination)
        {
            if (string.IsNullOrWhiteSpace(destination))
            {
                return null;
            }

            destination = destination.Trim();
            string alias = destination.ToLowerInvariant() switch
            {
                "manhattan" => "Fort Bush",
                "rotor nexus" => "Trenton Outpost",
                "p887 station" => "Newark Station",
                "newark" => "Newark Station",
                "west point" => "West Point Military Academy",
                "buffalo" => "Buffalo Base",
                "norfolk" => "Norfolk Shipyard",
                _ => destination
            };

            IReadOnlyList<Station> stations = _stationProvider?.Invoke() ?? Array.Empty<Station>();
            if (stations == null || stations.Count == 0)
            {
                return null;
            }

            Station exact = stations.FirstOrDefault(station =>
                station != null &&
                station.Name != null &&
                (station.Name.Equals(destination, StringComparison.OrdinalIgnoreCase) ||
                 station.Name.Equals(alias, StringComparison.OrdinalIgnoreCase)));
            if (exact != null)
            {
                return exact;
            }

            return stations.FirstOrDefault(station =>
                station != null &&
                station.Name != null &&
                (station.Name.IndexOf(destination, StringComparison.OrdinalIgnoreCase) >= 0 ||
                 destination.IndexOf(station.Name, StringComparison.OrdinalIgnoreCase) >= 0 ||
                 station.Name.IndexOf(alias, StringComparison.OrdinalIgnoreCase) >= 0 ||
                 alias.IndexOf(station.Name, StringComparison.OrdinalIgnoreCase) >= 0));
        }

        private Commodity ResolveDeliveryCommodity(string missionTarget)
        {
            if (string.IsNullOrWhiteSpace(missionTarget))
            {
                return null;
            }

            string normalized = missionTarget.Trim().ToLowerInvariant();
            normalized = normalized.Replace(" cells", string.Empty);
            normalized = normalized.Replace("-", string.Empty);
            normalized = normalized.Replace(" ", string.Empty);

            string commodityId = normalized switch
            {
                "medicalsupplies" => "medical-supplies",
                "hfuel" => "h-fuel",
                "luxurygoods" => "luxury-goods",
                "constructionmaterials" => "construction-materials",
                "militaryhardware" => "side-arms",
                "foodrations" => "food-rations",
                "sidearms" => "side-arms",
                "enginecomponents" => "engine-components",
                "boron" => "boron",
                "diamonds" => "diamonds",
                "consumergoods" => "consumer-goods",
                _ => null
            };

            Commodity commodity = CommodityCatalog.GetById(commodityId);
            if (commodity != null)
            {
                return commodity;
            }

            return CommodityCatalog.GetByName(missionTarget);
        }

        private Station ResolveCourierDestination(Mission mission)
        {
            IReadOnlyList<Station> stations = _stationProvider?.Invoke() ?? Array.Empty<Station>();
            if (!string.IsNullOrWhiteSpace(mission?.DestinationStationId))
            {
                Station byIdentity = stations.FirstOrDefault(station =>
                    station != null && string.Equals(
                        Mission.BuildStationIdentity(station),
                        mission.DestinationStationId,
                        StringComparison.OrdinalIgnoreCase));
                if (byIdentity != null)
                {
                    return byIdentity;
                }
            }

            return ResolveDeliveryDestination(mission?.Destination);
        }

        private static Commodity ResolveCourierCommodity(Mission mission)
        {
            return CommodityCatalog.GetByIdOrName(mission?.PackageId);
        }

        private Station ResolveEscortDestination(Mission mission)
        {
            return ResolveDeliveryDestination(mission?.Destination);
        }

        private NpcShip ResolveEscortTarget(Mission mission, MissionRuntimeState state)
        {
            if (state?.EscortTarget != null &&
                !state.EscortTarget.IsDestroyed &&
                _npcShips.Contains(state.EscortTarget))
            {
                return state.EscortTarget;
            }

            if (mission?.TargetSpaceObject is NpcShip boundEscort &&
                !boundEscort.IsDestroyed &&
                _npcShips.Contains(boundEscort))
            {
                state.EscortTarget = boundEscort;
                return boundEscort;
            }

            NpcShip existingEscort = ResolveExistingEscortTarget(mission);
            if (existingEscort != null)
            {
                state.EscortTarget = existingEscort;
                mission.TargetSpaceObject = existingEscort;
                mission.TargetPosition = existingEscort.Position;
            }

            return existingEscort;
        }

        private NpcShip ResolveExistingEscortTarget(Mission mission)
        {
            if (mission == null || string.IsNullOrWhiteSpace(mission.Target))
            {
                return null;
            }

            string escortName = mission.Target.Trim();
            return _npcShips.FirstOrDefault(npc =>
                npc != null &&
                !npc.IsDestroyed &&
                npc.Name != null &&
                npc.Name.IndexOf(escortName, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        private static bool IsEscortMatch(Mission mission, NpcShip destroyedShip, NpcShip boundEscort)
        {
            if (mission == null || destroyedShip == null)
            {
                return false;
            }

            if (boundEscort != null && ReferenceEquals(boundEscort, destroyedShip))
            {
                return true;
            }

            if (!string.IsNullOrWhiteSpace(mission.Target) &&
                destroyedShip.Name != null &&
                destroyedShip.Name.IndexOf(mission.Target, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }

            return false;
        }

        private bool IsEscortAtDestination(NpcShip escort, Station destination)
        {
            if (escort == null || destination == null)
            {
                return false;
            }

            float completionRange = Math.Max(1500f, destination.DockingRange * 1.5f);
            return Vector3.DistanceSquared(escort.Position, destination.Position) <= completionRange * completionRange;
        }

        private Vector3 GetEscortSpawnPosition(Station destination)
        {
            Vector3 forward = _playerShip?.Forward ?? Vector3.Forward;
            Vector3 right = _playerShip?.Right ?? Vector3.Right;
            Vector3 playerPosition = _playerShip?.Position ?? Vector3.Zero;

            if (forward.LengthSquared() < 0.0001f)
            {
                forward = Vector3.Forward;
            }

            if (right.LengthSquared() < 0.0001f)
            {
                right = Vector3.Right;
            }

            Vector3 spawn = playerPosition + forward * 2500f + right * 600f;
            if (destination != null)
            {
                Vector3 toDestination = destination.Position - spawn;
                if (toDestination.LengthSquared() > 0.0001f)
                {
                    toDestination.Normalize();
                    spawn += Vector3.Cross(toDestination, Vector3.Up) * 250f;
                }
            }

            return spawn;
        }

        private string GetEscortDisplayName(Mission mission)
        {
            if (mission == null)
            {
                return "Escort Convoy";
            }

            return string.IsNullOrWhiteSpace(mission.Target)
                ? mission.GetEscortShipName()
                : $"{mission.Target.Trim()} {mission.Id}";
        }

        private static string DetermineEscortFaction(Mission mission)
        {
            if (mission != null && !string.IsNullOrWhiteSpace(mission.FactionId))
            {
                return FactionManager.NormalizeFactionId(mission.FactionId);
            }

            return FactionManager.LibertyCorporations;
        }

        private static NpcLoadoutTier MapMissionLoadoutTier(MissionDifficulty difficulty)
        {
            return difficulty switch
            {
                MissionDifficulty.Easy => NpcLoadoutTier.Low,
                MissionDifficulty.Medium => NpcLoadoutTier.Standard,
                MissionDifficulty.Hard => NpcLoadoutTier.High,
                MissionDifficulty.Deadly => NpcLoadoutTier.High,
                _ => NpcLoadoutTier.Standard
            };
        }

        private static bool IsTargetMatch(Mission mission, NpcShip destroyedShip, NpcShip boundTarget)
        {
            if (mission == null || destroyedShip == null)
            {
                return false;
            }

            if (boundTarget != null && ReferenceEquals(boundTarget, destroyedShip))
            {
                return true;
            }

            if (!string.IsNullOrWhiteSpace(mission.Target) &&
                destroyedShip.Name != null &&
                destroyedShip.Name.IndexOf(mission.Target, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }

            return false;
        }

        private static bool IsStationMatch(Station station, Station resolvedStation, string missionDestination, string expectedIdentity = "")
        {
            if (station == null)
            {
                return false;
            }

            if (resolvedStation != null && ReferenceEquals(resolvedStation, station))
            {
                return true;
            }

            if (!string.IsNullOrWhiteSpace(expectedIdentity) &&
                string.Equals(Mission.BuildStationIdentity(station), expectedIdentity, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            // Courier missions carry an exact destination identity. Do not
            // fall back to a same-name station in another system.
            if (!string.IsNullOrWhiteSpace(expectedIdentity))
            {
                return false;
            }

            if (!string.IsNullOrWhiteSpace(missionDestination) &&
                station.Name != null &&
                station.Name.IndexOf(missionDestination, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }

            return false;
        }

        private Vector3 GetBountySpawnPosition()
        {
            Vector3 forward = _playerShip?.Forward ?? Vector3.Forward;
            Vector3 right = _playerShip?.Right ?? Vector3.Right;
            Vector3 up = _playerShip?.Up ?? Vector3.Up;
            Vector3 playerPosition = _playerShip?.Position ?? Vector3.Zero;

            if (forward.LengthSquared() < 0.0001f)
            {
                forward = Vector3.Forward;
            }

            if (right.LengthSquared() < 0.0001f)
            {
                right = Vector3.Right;
            }

            if (up.LengthSquared() < 0.0001f)
            {
                up = Vector3.Up;
            }

            return playerPosition + forward * 8500f + right * 1800f + up * 250f;
        }

        private static string DetermineBountyFaction(Mission mission, string targetName)
        {
            if (mission != null && !string.IsNullOrWhiteSpace(mission.BountyTargetFactionId))
            {
                return FactionManager.NormalizeFactionId(mission.BountyTargetFactionId);
            }

            if (string.IsNullOrWhiteSpace(targetName))
            {
                return FactionManager.LibertyRogues;
            }

            string lower = targetName.ToLowerInvariant();
            if (lower.Contains("pirate") || lower.Contains("rogue") || lower.Contains("outcast") || lower.Contains("corsair") ||
                lower.Contains("hacker") || lower.Contains("xeno") || lower.Contains("nomad"))
            {
                return FactionManager.LibertyRogues;
            }

            return FactionManager.LibertyRogues;
        }
    }
}
