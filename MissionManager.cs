using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;

namespace Roguelancer
{
    /// <summary>
    /// Authoritative player mission state. Phase 11 intentionally keeps one
    /// active mission and separates objective completion from reward claiming.
    /// </summary>
    public class MissionManager
    {
        private readonly List<Mission> _activeMissions = new();
        private readonly List<Mission> _completedMissions = new();
        private readonly HashSet<NpcShip> _countedHostileKills = new();
        private readonly Random _random = new();
        private readonly PlayerCredits _playerCredits;
        private readonly NotificationManager _notificationManager;
        private ReputationManager _reputationManager;
        private MissionWaypointSystem _waypointSystem;
        private MissionWorldManager _worldManager;

        private static readonly string[] DeliveryTargets =
        {
            "Medical Supplies", "H-Fuel Cells", "Construction Materials", "Food Rations"
        };

        private static readonly string[] DeliveryDestinations =
        {
            "Fort Bush", "Trenton Outpost", "Newark Station", "Rochester Base"
        };

        private static readonly string[] BountyTargets =
        {
            "Rogue Pilot", "Pirate Commander", "Outcast Smuggler", "Corsair Raider"
        };

        public IReadOnlyList<Mission> ActiveMissions => _activeMissions.AsReadOnly();
        public IReadOnlyList<Mission> CompletedMissions => _completedMissions.AsReadOnly();
        public Mission ActiveMission => _activeMissions.FirstOrDefault();
        public Mission UnclaimedCompletedMission => _completedMissions.FirstOrDefault(mission =>
            mission != null && mission.Status == MissionStatus.Completed && !mission.RewardPaid);

        public MissionManager(
            PlayerCredits playerCredits,
            NotificationManager notificationManager,
            ReputationManager reputationManager = null)
        {
            _playerCredits = playerCredits;
            _notificationManager = notificationManager;
            _reputationManager = reputationManager;
        }

        public void SetReputationManager(ReputationManager reputationManager) => _reputationManager = reputationManager;
        public void SetWaypointSystem(MissionWaypointSystem waypointSystem) => _waypointSystem = waypointSystem;
        public void SetWorldManager(MissionWorldManager worldManager) => _worldManager = worldManager;

        public void ClearState()
        {
            foreach (Mission mission in _activeMissions)
                _waypointSystem?.UnregisterMission(mission);

            _activeMissions.Clear();
            _completedMissions.Clear();
            _countedHostileKills.Clear();
            _worldManager?.ClearState();
        }

        public void RestoreState(IEnumerable<Mission> activeMissions, IEnumerable<Mission> completedMissions)
        {
            ClearState();

            Mission restoredActive = activeMissions?.FirstOrDefault(mission => mission != null &&
                mission.Status is MissionStatus.Accepted or MissionStatus.InProgress);
            if (restoredActive != null)
            {
                restoredActive.Status = MissionStatus.InProgress;
                _activeMissions.Add(restoredActive);
                _waypointSystem?.RegisterMission(restoredActive);
            }

            if (completedMissions != null)
            {
                foreach (Mission mission in completedMissions)
                {
                    if (mission == null || mission.RewardPaid || mission.Status == MissionStatus.Rewarded)
                        continue;
                    if (mission.Status == MissionStatus.Available || mission.Status == MissionStatus.InProgress)
                        mission.Status = MissionStatus.Completed;
                    _completedMissions.Add(mission);
                }
            }

            Console.WriteLine($"[MISSION] Restored {_activeMissions.Count} active and {_completedMissions.Count} unclaimed/completed missions");
        }

        /// <summary>Creates the fixed board jobs from static catalog metadata.</summary>
        public List<Mission> CreateBoardMissions(Station originStation = null)
        {
            string faction = originStation?.FactionId ?? FactionManager.LibertyCorporations;
            List<Mission> missions = MissionCatalog.CreateRuntimeMissions("Mission Board", faction);
            return missions;
        }

        /// <summary>
        /// Compatibility generator used by the older navigation smoke tests.
        /// It is not used by the Phase 11 physical board.
        /// </summary>
        public Mission GenerateRandomMission(string factionId = null, Station originStation = null)
        {
            IReadOnlyList<Station> stations = _worldManager?.GetKnownStations() ?? Array.Empty<Station>();
            MissionDifficulty difficulty = (MissionDifficulty)_random.Next(4);
            MissionType type = stations.Count > 0
                ? (MissionType)_random.Next(3, 5)
                : MissionType.Bounty;

            string target;
            string destination;
            string description;
            switch (type)
            {
                case MissionType.Delivery:
                    target = DeliveryTargets[_random.Next(DeliveryTargets.Length)];
                    destination = PickDestination(stations, originStation);
                    description = $"Deliver {target} to {destination}";
                    break;
                case MissionType.Escort:
                    target = "Trade Convoy";
                    destination = PickDestination(stations, originStation);
                    description = $"Escort {target} to {destination}";
                    break;
                default:
                    target = BountyTargets[_random.Next(BountyTargets.Length)];
                    destination = originStation?.Name ?? "Last seen near local traffic lanes";
                    description = $"Destroy {target}";
                    type = MissionType.Bounty;
                    break;
            }

            int difficultyValue = (int)difficulty;
            int reward = type switch
            {
                MissionType.Delivery => 1500 + difficultyValue * 750,
                MissionType.Escort => 2200 + difficultyValue * 850,
                _ => 1800 + difficultyValue * 950
            };

            Mission mission = new Mission(type, difficulty, target, destination, reward, 0f, description, factionId)
            {
                OfferedBy = originStation?.Name ?? FactionManager.GetFactionDisplayName(factionId)
            };
            if (type == MissionType.Bounty) mission.BountyTargetFactionId = FactionManager.LibertyRogues;
            return mission;
        }

        public List<Mission> GenerateJobBoardMissions(int count, string factionId = null, Station originStation = null)
        {
            return CreateBoardMissions(originStation)
                .Take(Math.Clamp(count, 0, MissionCatalog.All.Count))
                .ToList();
        }

        private static string PickDestination(IReadOnlyList<Station> stations, Station origin)
        {
            Station destination = stations?.FirstOrDefault(station => station != null && !ReferenceEquals(station, origin))
                ?? stations?.FirstOrDefault();
            return destination?.Name ?? "Destination unavailable";
        }

        /// <summary>
        /// Validates and accepts exactly one mission. World binding happens
        /// before the authoritative active state is committed.
        /// </summary>
        public bool AcceptMission(Mission mission, Station originStation = null)
        {
            if (mission == null || mission.Status != MissionStatus.Available)
                return RejectAcceptance(mission, "mission is not available");
            if (ActiveMission != null)
                return RejectAcceptance(mission, "finish the active mission first");
            if (mission.Reward <= 0)
                return RejectAcceptance(mission, "reward is invalid");

            if (!string.IsNullOrWhiteSpace(mission.DefinitionId))
            {
                MissionDefinition definition = MissionCatalog.GetById(mission.DefinitionId);
                if (definition == null || definition.Type != mission.Type ||
                    definition.RewardCredits != mission.Reward ||
                    definition.TargetCount != mission.RequiredProgress)
                    return RejectAcceptance(mission, "mission definition is invalid");
            }

            if (mission.Type == MissionType.DestroyHostiles && mission.RequiredProgress <= 0)
                return RejectAcceptance(mission, "hostile target count is invalid");
            if (mission.Type == MissionType.ReachLocation && string.IsNullOrWhiteSpace(mission.TargetLocation))
                return RejectAcceptance(mission, "patrol target metadata is invalid");

            mission.SetOrigin(originStation);
            mission.AcceptedAtUtc = DateTime.UtcNow;
            mission.Status = MissionStatus.Accepted;
            _activeMissions.Add(mission);
            _waypointSystem?.RegisterMission(mission);

            if (_worldManager != null && !_worldManager.TryAcceptMission(mission, out string failureReason))
            {
                _waypointSystem?.UnregisterMission(mission);
                _activeMissions.Remove(mission);
                mission.Status = MissionStatus.Available;
                _worldManager.OnMissionFinished(mission);
                return RejectAcceptance(mission, $"mission unavailable: {failureReason}");
            }

            mission.Status = MissionStatus.InProgress;
            _notificationManager?.ShowMessage($"Mission accepted: {mission.Title}", 3f);
            Console.WriteLine($"[MISSION] Accepted: {mission.GetSummary()} | Origin: {mission.OriginStationName}");
            return true;
        }

        private bool RejectAcceptance(Mission mission, string reason)
        {
            _notificationManager?.ShowMessage(reason, 3f);
            Console.WriteLine($"[MISSION] Rejected: {mission?.Title ?? "<null>"} | Reason: {reason}");
            return false;
        }

        /// <summary>
        /// Transitions a satisfied objective to Completed. Credits are not
        /// changed here; the originating station performs the reward claim.
        /// </summary>
        public void CompleteMission(Mission mission)
        {
            if (mission == null || !ReferenceEquals(ActiveMission, mission) || !mission.IsActive)
                return;

            mission.ObjectiveComplete = true;
            mission.Status = MissionStatus.Completed;
            _activeMissions.Remove(mission);
            _completedMissions.RemoveAll(existing => existing.Id == mission.Id);
            _completedMissions.Add(mission);
            _waypointSystem?.UnregisterMission(mission);
            _worldManager?.OnMissionFinished(mission);
            _notificationManager?.ShowMessage(
                $"Objective complete - return to {mission.OriginStationName} to claim {mission.Reward:N0} CR",
                4f);
            Console.WriteLine($"[MISSION] Objective complete: {mission.Title} | Reward pending: {mission.Reward:N0} CR");
        }

        public bool TryClaimReward(Mission mission, Station station, out string message)
        {
            message = string.Empty;
            if (mission == null || mission.Status != MissionStatus.Completed)
            {
                message = "mission is not complete";
                return false;
            }
            if (mission.RewardPaid || mission.Status == MissionStatus.Rewarded)
            {
                message = "reward already claimed";
                return false;
            }

            string currentStationId = Mission.BuildStationIdentity(station);
            if (!string.Equals(currentStationId, mission.OriginStationId, StringComparison.OrdinalIgnoreCase))
            {
                message = $"return to {mission.OriginStationName} to claim the reward";
                return false;
            }
            if (mission.Reward <= 0 || _playerCredits == null)
            {
                message = "reward transaction is invalid";
                return false;
            }

            mission.RewardPaid = true;
            _playerCredits.AddCredits(mission.Reward);
            mission.Status = MissionStatus.Rewarded;
            _completedMissions.Remove(mission);
            _reputationManager?.AddReputation(
                mission.FactionId,
                0.12f,
                $"Mission rewarded: {mission.Title}");
            _notificationManager?.ShowMessage($"Mission reward received: {mission.Reward:N0} CR", 4f);
            Console.WriteLine($"[MISSION] Rewarded: {mission.Title} | +{mission.Reward:N0} CR");
            message = $"Mission reward received: {mission.Reward:N0} CR";
            return true;
        }

        public bool CanClaimRewardAt(Mission mission, Station station, out string reason)
        {
            reason = string.Empty;
            if (mission == null || mission.Status != MissionStatus.Completed)
            {
                reason = "No completed mission is waiting for payment.";
                return false;
            }
            if (mission.RewardPaid)
            {
                reason = "Reward already claimed.";
                return false;
            }

            if (!string.Equals(Mission.BuildStationIdentity(station), mission.OriginStationId, StringComparison.OrdinalIgnoreCase))
            {
                reason = $"Return to {mission.OriginStationName} to claim the reward.";
                return false;
            }

            return true;
        }

        public void FailMission(Mission mission, string reason)
        {
            if (mission == null || !ReferenceEquals(ActiveMission, mission) || !mission.IsActive)
                return;

            mission.Status = MissionStatus.Failed;
            _activeMissions.Remove(mission);
            _completedMissions.Add(mission);
            _waypointSystem?.UnregisterMission(mission);
            _worldManager?.OnMissionFinished(mission);
            _notificationManager?.ShowMessage($"Mission failed: {reason}", 4f);
            Console.WriteLine($"[MISSION] Failed: {mission.Title} | Reason: {reason}");
        }

        public void Update(float deltaTime, bool playerDestroyed)
        {
            Mission mission = ActiveMission;
            if (mission == null) return;

            mission.ElapsedTime += Math.Max(0f, deltaTime);
            if (mission.IsExpired)
            {
                FailMission(mission, "Time ran out");
                return;
            }
            if (playerDestroyed)
            {
                FailMission(mission, "Ship destroyed");
                return;
            }
            if (mission.ObjectiveComplete)
                CompleteMission(mission);
        }

        public void FailAllActiveMissions(string reason)
        {
            if (ActiveMission != null) FailMission(ActiveMission, reason);
        }

        /// <summary>Legacy name-based hook retained for missile smoke coverage.</summary>
        public void NotifyTargetDestroyed(string targetName)
        {
            if (string.IsNullOrWhiteSpace(targetName)) return;
            Mission mission = ActiveMission;
            if (mission?.Type == MissionType.Bounty &&
                !string.IsNullOrWhiteSpace(mission.Target) &&
                targetName.Contains(mission.Target, StringComparison.OrdinalIgnoreCase))
            {
                mission.ObjectiveComplete = true;
            }
        }

        public bool RecordHostileDestroyed(Mission mission, NpcShip destroyedShip)
        {
            if (mission == null || destroyedShip == null ||
                !ReferenceEquals(ActiveMission, mission) ||
                mission.Type != MissionType.DestroyHostiles ||
                !destroyedShip.WasDamagedByPlayer ||
                !_countedHostileKills.Add(destroyedShip))
                return false;

            mission.CurrentProgress = Math.Min(mission.RequiredProgress, mission.CurrentProgress + 1);
            Console.WriteLine($"[MISSION] Rogue Hunt progress {mission.CurrentProgress}/{mission.RequiredProgress}: {destroyedShip.Name}");
            if (mission.CurrentProgress >= mission.RequiredProgress)
            {
                mission.ObjectiveComplete = true;
                CompleteMission(mission);
            }
            return true;
        }

        /// <summary>Legacy delivery arrival hook; it now waits for reward claim.</summary>
        public void NotifyArrivedAtStation(string stationName)
        {
            Mission mission = ActiveMission;
            if (mission?.Type == MissionType.Delivery &&
                !string.IsNullOrWhiteSpace(stationName) &&
                stationName.Contains(mission.Destination ?? string.Empty, StringComparison.OrdinalIgnoreCase))
            {
                mission.ObjectiveComplete = true;
            }
        }
    }
}
