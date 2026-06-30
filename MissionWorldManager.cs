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
        public Station DeliveryDestination { get; set; }
        public Commodity DeliveryCommodity { get; set; }
        public int DeliveryQuantity { get; set; }
        public NpcShip EscortTarget { get; set; }
        public Station EscortDestination { get; set; }
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
        private readonly Dictionary<int, MissionRuntimeState> _runtimeStates = new();

        public MissionWorldManager(
            MissionManager missionManager,
            MissionWaypointSystem waypointSystem,
            Ship playerShip,
            List<NpcShip> npcShips,
            List<SpaceObject> spaceObjects,
            Func<IReadOnlyList<Station>> stationProvider,
            Action<NpcShip> spawnedNpcDestroyedCallback = null)
        {
            _missionManager = missionManager;
            _waypointSystem = waypointSystem;
            _playerShip = playerShip;
            _npcShips = npcShips ?? new List<NpcShip>();
            _spaceObjects = spaceObjects ?? new List<SpaceObject>();
            _stationProvider = stationProvider ?? (() => Array.Empty<Station>());
            _spawnedNpcDestroyedCallback = spawnedNpcDestroyedCallback;
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
                case MissionType.Bounty:
                    return TryBindBountyMission(state, out failureReason);
                case MissionType.Delivery:
                    return TryBindDeliveryMission(state, out failureReason);
                case MissionType.Escort:
                    return TryBindEscortMission(state, out failureReason);
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
        }

        public void OnMissionFinished(Mission mission)
        {
            if (mission == null)
            {
                return;
            }

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

                if (mission.Type == MissionType.Bounty && IsTargetMatch(mission, destroyedShip, state.BountyTarget))
                {
                    Console.WriteLine($"[MISSION] Target destroyed: {destroyedShip.Name} (mission #{mission.Id})");
                    mission.ObjectiveComplete = true;
                    _missionManager?.CompleteMission(mission);
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
                if (mission == null || mission.Status != MissionStatus.Active || mission.Type != MissionType.Delivery)
                {
                    continue;
                }

                Station resolvedStation = state.DeliveryDestination ?? ResolveDeliveryDestination(mission);
                if (resolvedStation == null)
                {
                    continue;
                }

                if (!IsStationMatch(station, resolvedStation, mission.Destination))
                {
                    continue;
                }

                if (!TryRemoveDeliveryCargo(state))
                {
                    FailMission(mission, "mission cargo missing");
                    completedAny = true;
                    continue;
                }

                Console.WriteLine($"[MISSION] Delivery completed at {station.Name} (mission #{mission.Id})");
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

        public void Update(float deltaTime, Action<string> log = null)
        {
            if (_runtimeStates.Count == 0)
            {
                return;
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
                    continue;
                }

                state.EscortDestination = destination;

                if (state.EscortTarget != null && state.EscortTarget.IsDestroyed)
                {
                    FailMission(mission, "escort destroyed");
                    continue;
                }

                NpcShip escort = ResolveEscortTarget(mission, state);
                if (escort == null)
                {
                    if (!TryBindEscortMission(state, out string failureReason))
                    {
                        if (!string.IsNullOrWhiteSpace(failureReason))
                        {
                            FailMission(mission, failureReason);
                        }

                        continue;
                    }

                    escort = state.EscortTarget;
                }

                if (escort == null)
                {
                    continue;
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
                    _missionManager?.CompleteMission(mission);
                }
            }
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

        private static bool IsStationMatch(Station station, Station resolvedStation, string missionDestination)
        {
            if (station == null)
            {
                return false;
            }

            if (resolvedStation != null && ReferenceEquals(resolvedStation, station))
            {
                return true;
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
