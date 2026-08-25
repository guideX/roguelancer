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
        // Transient encounter binding is deliberately not serialized. A
        // restored active defense mission starts unbound and reconstructs its
        // Rogue group once; a live encounter that has been defeated must not
        // be mistaken for that load/rebind case.
        public bool TradeLaneDefenseEncounterBound { get; set; }
        public bool EscortUnderAttackLogged { get; set; }
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
        private readonly Dictionary<int, MissionRuntimeState> _runtimeStates = new();

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
            Action<NpcShip> retiredNpcCallback = null)
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
                CleanupMissionTransientNpcs(state);
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
        }

        public void OnMissionFinished(Mission mission)
        {
            if (mission == null)
            {
                return;
            }

            if (_runtimeStates.TryGetValue(mission.Id, out MissionRuntimeState state))
                CleanupMissionTransientNpcs(state);
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
                    (mission.Type is MissionType.CourierDelivery or MissionType.ExportContract
                        ? ResolveCourierDestination(mission)
                        : ResolveDeliveryDestination(mission));
                if (resolvedStation == null)
                {
                    continue;
                }

                string expectedIdentity = mission.Type is MissionType.CourierDelivery or MissionType.FreightContract or MissionType.ExportContract
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
            if (state?.Mission == null || state.Mission.Type != MissionType.TradeLaneDefense)
                return;

            foreach (NpcShip attacker in state.MissionHostiles.ToList())
            {
                if (attacker == null)
                    continue;

                attacker.ClearEncounterState();
                _npcShips.Remove(attacker);
                _spaceObjects.Remove(attacker);
                _retiredNpcCallback?.Invoke(attacker);
            }

            state.MissionHostiles.Clear();
            state.TradeLaneDefenseEncounterBound = false;
            state.Mission.DefenseAttackersRemaining = 0;
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
