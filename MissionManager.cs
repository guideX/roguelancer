using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;

namespace Roguelancer
{
    /// <summary>
    /// Manages all missions: generation, tracking, completion, and failure
    /// </summary>
    public class MissionManager
    {
        private List<Mission> _activeMissions = new();
        private List<Mission> _completedMissions = new();
        private Random _random = new();
        private PlayerCredits _playerCredits;
        private NotificationManager _notificationManager;
        private MissionWaypointSystem _waypointSystem;
        private ReputationManager _reputationManager;
        private MissionWorldManager _worldManager;

        // Mission generation data
        private static readonly string[] DeliveryTargets = {
            "Medical Supplies", "H-Fuel Cells", "Luxury Goods",
            "Construction Materials", "Military Hardware", "Food Rations",
            "Side Arms", "Engine Components", "Boron", "Diamonds"
        };

        private static readonly string[] DeliveryDestinations = {
            "Fort Bush", "Trenton Outpost", "Newark Station",
            "Rochester Base", "Norfolk Shipyard", "West Point Military Academy",
            "Detroit Munitions", "Battleship Missouri", "Buffalo Base"
        };

        private static readonly string[] BountyTargets = {
            "Rogue Pilot", "Pirate Commander", "Lane Hacker Scout",
            "Outcast Smuggler", "Corsair Raider", "Xeno Operative",
            "Rogue Wingman", "Junker Scavenger", "Nomad Drone"
        };

        private static readonly string[] EscortTargets = {
            "Trade Convoy", "Diplomatic Shuttle", "Supply Freighter",
            "Research Vessel", "Refugee Transport", "Mining Barge"
        };

        public IReadOnlyList<Mission> ActiveMissions => _activeMissions.AsReadOnly();
        public IReadOnlyList<Mission> CompletedMissions => _completedMissions.AsReadOnly();

        public MissionManager(PlayerCredits playerCredits, NotificationManager notificationManager, ReputationManager reputationManager = null)
        {
            _playerCredits = playerCredits;
            _notificationManager = notificationManager;
            _reputationManager = reputationManager;
        }

        public void SetReputationManager(ReputationManager reputationManager)
        {
            _reputationManager = reputationManager;
        }

        /// <summary>
        /// Set the waypoint system reference for automatic registration/unregistration
        /// </summary>
        public void SetWaypointSystem(MissionWaypointSystem waypointSystem)
        {
            _waypointSystem = waypointSystem;
        }

        public void SetWorldManager(MissionWorldManager worldManager)
        {
            _worldManager = worldManager;
        }

        /// <summary>
        /// Clear all mission state and unregister any active waypoints.
        /// </summary>
        public void ClearState()
        {
            foreach (var mission in _activeMissions)
            {
                _waypointSystem?.UnregisterMission(mission);
            }

            _activeMissions.Clear();
            _completedMissions.Clear();
            _worldManager?.ClearState();
        }

        /// <summary>
        /// Restore saved mission state.
        /// </summary>
        public void RestoreState(IEnumerable<Mission> activeMissions, IEnumerable<Mission> completedMissions)
        {
            ClearState();

            if (activeMissions != null)
            {
                foreach (var mission in activeMissions)
                {
                    if (mission == null)
                    {
                        continue;
                    }

                    mission.Status = MissionStatus.Active;
                    _activeMissions.Add(mission);
                    _waypointSystem?.RegisterMission(mission);
                }
            }

            if (completedMissions != null)
            {
                foreach (var mission in completedMissions)
                {
                    if (mission == null)
                    {
                        continue;
                    }

                    if (mission.Status == MissionStatus.Active)
                    {
                        mission.Status = MissionStatus.Completed;
                    }

                    _completedMissions.Add(mission);
                }
            }

            Console.WriteLine($"[MISSION] Restored {_activeMissions.Count} active and {_completedMissions.Count} completed missions");
        }

        /// <summary>
        /// Generate a single random mission
        /// </summary>
        public Mission GenerateRandomMission(string factionId = null, Station originStation = null)
        {
            MissionDifficulty difficulty = (MissionDifficulty)_random.Next(4);
            IReadOnlyList<Station> loadedStations = _worldManager?.GetKnownStations() ?? Array.Empty<Station>();
            bool canGenerateDelivery = loadedStations.Count > 0;
            MissionType type = PickMissionType(canGenerateDelivery);
            int baseReward = GetBaseReward(type, difficulty);

            float timeLimit = difficulty switch
            {
                MissionDifficulty.Easy => 0,
                MissionDifficulty.Medium => _random.Next(120, 300),
                MissionDifficulty.Hard => _random.Next(90, 180),
                MissionDifficulty.Deadly => _random.Next(60, 120),
                _ => 0
            };

            string target;
            string destination;
            string description;
            int reward = baseReward;
            string offeredBy = originStation?.Name ?? FactionManager.GetFactionDisplayName(factionId);

            switch (type)
            {
                case MissionType.Delivery:
                    target = PickDeliveryCargo(difficulty);
                    Station deliveryOrigin = originStation ?? loadedStations[_random.Next(loadedStations.Count)];
                    Station deliveryDestination = PickDeliveryDestination(deliveryOrigin, loadedStations);
                    destination = deliveryDestination?.Name ?? "Destination unavailable";
                    description = $"Deliver {target} to {destination}";
                    reward += CalculateDeliveryRewardBonus(difficulty, deliveryOrigin, deliveryDestination);
                    break;
                case MissionType.Bounty:
                    target = PickBountyTarget(difficulty);
                    Station bountyLocation = loadedStations.Count > 0
                        ? loadedStations[_random.Next(loadedStations.Count)]
                        : null;
                    destination = bountyLocation != null
                        ? $"Last seen near {bountyLocation.Name}"
                        : "Last seen near local traffic lanes";
                    description = $"Destroy {target}";
                    reward += CalculateBountyRewardBonus(difficulty, target);
                    break;
                case MissionType.Escort:
                    target = EscortTargets[_random.Next(EscortTargets.Length)];
                    Station escortDestination = loadedStations.Count > 0
                        ? loadedStations[_random.Next(loadedStations.Count)]
                        : null;
                    destination = escortDestination?.Name ?? "Destination unavailable";
                    description = $"Escort {target} to {destination}";
                    reward += CalculateEscortRewardBonus(difficulty, escortDestination);
                    break;
                default:
                    target = "Unknown";
                    destination = "Unknown";
                    description = "Unknown mission";
                    break;
            }

            Mission mission = new Mission(type, difficulty, target, destination, reward, timeLimit, description, factionId)
            {
                OfferedBy = offeredBy
            };

            if (mission.Type == MissionType.Bounty)
            {
                mission.BountyTargetFactionId = FactionManager.LibertyRogues;
            }

            return mission;
        }

        /// <summary>
        /// Generate multiple random missions for a job board
        /// </summary>
        public List<Mission> GenerateJobBoardMissions(int count, string factionId = null, Station originStation = null)
        {
            var missions = new List<Mission>();
            for (int i = 0; i < count; i++)
            {
                missions.Add(GenerateRandomMission(factionId, originStation));
            }
            return missions;
        }

        private MissionType PickMissionType(bool canGenerateDelivery)
        {
            List<MissionType> allowedTypes = new();
            if (canGenerateDelivery)
            {
                allowedTypes.Add(MissionType.Delivery);
            }

            allowedTypes.Add(MissionType.Bounty);
            allowedTypes.Add(MissionType.Escort);

            return allowedTypes[_random.Next(allowedTypes.Count)];
        }

        private string PickDeliveryCargo(MissionDifficulty difficulty)
        {
            string[] cargoPool = difficulty switch
            {
                MissionDifficulty.Easy => new[] { "Medical Supplies", "Food Rations", "H-Fuel Cells" },
                MissionDifficulty.Medium => new[] { "Construction Materials", "Engine Components", "Luxury Goods" },
                MissionDifficulty.Hard => new[] { "Military Hardware", "Side Arms", "Diamonds" },
                MissionDifficulty.Deadly => new[] { "Military Hardware", "Side Arms", "Diamonds" },
                _ => DeliveryTargets
            };

            return cargoPool[_random.Next(cargoPool.Length)];
        }

        private Station PickDeliveryDestination(Station originStation, IReadOnlyList<Station> stations)
        {
            if (stations == null || stations.Count == 0)
            {
                return null;
            }

            List<Station> candidates = stations.Where(station => station != null && !ReferenceEquals(station, originStation)).ToList();
            if (candidates.Count == 0)
            {
                candidates = stations.Where(station => station != null).ToList();
            }

            if (candidates.Count == 0)
            {
                return null;
            }

            return candidates[_random.Next(candidates.Count)];
        }

        private string PickBountyTarget(MissionDifficulty difficulty)
        {
            string[] targetPool = difficulty switch
            {
                MissionDifficulty.Easy => new[] { "Rogue Pilot", "Lane Hacker Scout", "Junker Scavenger" },
                MissionDifficulty.Medium => new[] { "Pirate Commander", "Outcast Smuggler", "Corsair Raider" },
                MissionDifficulty.Hard => new[] { "Rogue Wingman", "Nomad Drone", "Xeno Operative" },
                MissionDifficulty.Deadly => new[] { "Pirate Warlord", "Rogue Enforcer", "Corsair Marauder" },
                _ => BountyTargets
            };

            return targetPool[_random.Next(targetPool.Length)];
        }

        private int CalculateDeliveryRewardBonus(MissionDifficulty difficulty, Station origin, Station destination)
        {
            int difficultyBonus = difficulty switch
            {
                MissionDifficulty.Easy => 0,
                MissionDifficulty.Medium => 75,
                MissionDifficulty.Hard => 180,
                MissionDifficulty.Deadly => 350,
                _ => 0
            };

            if (origin == null || destination == null)
            {
                return difficultyBonus;
            }

            float distance = Vector3.Distance(origin.Position, destination.Position);
            int distanceBonus = (int)Math.Clamp(distance / 2400f * 90f, 75f, 900f);
            return difficultyBonus + distanceBonus;
        }

        private int CalculateBountyRewardBonus(MissionDifficulty difficulty, string target)
        {
            int difficultyBonus = difficulty switch
            {
                MissionDifficulty.Easy => 150,
                MissionDifficulty.Medium => 300,
                MissionDifficulty.Hard => 600,
                MissionDifficulty.Deadly => 1100,
                _ => 150
            };

            int targetBonus = 0;
            if (!string.IsNullOrWhiteSpace(target))
            {
                string lower = target.ToLowerInvariant();
                if (lower.Contains("warlord") || lower.Contains("marauder") || lower.Contains("enforcer"))
                {
                    targetBonus = 400;
                }
                else if (lower.Contains("commander") || lower.Contains("raider"))
                {
                    targetBonus = 200;
                }
                else if (lower.Contains("smuggler") || lower.Contains("scout") || lower.Contains("wingman"))
                {
                    targetBonus = 100;
                }
            }

            return difficultyBonus + targetBonus;
        }

        private int CalculateEscortRewardBonus(MissionDifficulty difficulty, Station destination)
        {
            int difficultyBonus = difficulty switch
            {
                MissionDifficulty.Easy => 100,
                MissionDifficulty.Medium => 225,
                MissionDifficulty.Hard => 450,
                MissionDifficulty.Deadly => 850,
                _ => 100
            };

            return destination != null ? difficultyBonus + 100 : difficultyBonus;
        }

        private int GetBaseReward(MissionType type, MissionDifficulty difficulty)
        {
            return type switch
            {
                MissionType.Delivery => difficulty switch
                {
                    MissionDifficulty.Easy => _random.Next(500, 1100),
                    MissionDifficulty.Medium => _random.Next(1100, 2200),
                    MissionDifficulty.Hard => _random.Next(2200, 4200),
                    MissionDifficulty.Deadly => _random.Next(4200, 8000),
                    _ => _random.Next(500, 1200)
                },
                MissionType.Bounty => difficulty switch
                {
                    MissionDifficulty.Easy => _random.Next(900, 1700),
                    MissionDifficulty.Medium => _random.Next(1800, 3300),
                    MissionDifficulty.Hard => _random.Next(3300, 6200),
                    MissionDifficulty.Deadly => _random.Next(6200, 12000),
                    _ => _random.Next(1000, 2000)
                },
                MissionType.Escort => difficulty switch
                {
                    MissionDifficulty.Easy => _random.Next(1000, 1900),
                    MissionDifficulty.Medium => _random.Next(2000, 3600),
                    MissionDifficulty.Hard => _random.Next(3600, 6800),
                    MissionDifficulty.Deadly => _random.Next(6800, 13000),
                    _ => _random.Next(1000, 2000)
                },
                _ => _random.Next(500, 1500)
            };
        }

        /// <summary>
        /// Accept and assign a mission to the player
        /// </summary>
        public bool AcceptMission(Mission mission)
        {
            if (mission == null || mission.Status != MissionStatus.Available) return false;

            mission.FactionId = FactionManager.NormalizeFactionId(mission.FactionId);
            mission.Status = MissionStatus.Active;
            _activeMissions.Add(mission);
            _waypointSystem?.RegisterMission(mission);

            if (_worldManager != null && !_worldManager.TryAcceptMission(mission, out string failureReason))
            {
                _waypointSystem?.UnregisterMission(mission);
                _activeMissions.Remove(mission);
                mission.Status = MissionStatus.Available;
                _worldManager?.OnMissionFinished(mission);
                _notificationManager?.ShowMessage($"Mission unavailable: {failureReason}", 3f);
                Console.WriteLine($"[MISSION] Rejected: {mission.GetSummary()} | Reason: {failureReason}");
                return false;
            }

            _notificationManager?.ShowMessage($"Mission accepted: {mission.Description}", 3f);
            Console.WriteLine($"[MISSION] Accepted: {mission.GetSummary()}");
            return true;
        }

        /// <summary>
        /// Complete a mission and reward the player
        /// </summary>
        public void CompleteMission(Mission mission)
        {
            if (mission == null || mission.Status != MissionStatus.Active) return;

            mission.Status = MissionStatus.Completed;
            _activeMissions.Remove(mission);
            _completedMissions.Add(mission);
            _waypointSystem?.UnregisterMission(mission);

            _playerCredits?.AddCredits(mission.Reward);
            _notificationManager?.ShowMessage($"Mission complete! +{mission.Reward:N0} CR", 4f);
            Console.WriteLine($"[MISSION] Completed: {mission.Description} | Reward: {mission.Reward:N0} CR");
            _worldManager?.OnMissionFinished(mission);

            if (_reputationManager != null)
            {
                string factionId = FactionManager.NormalizeFactionId(mission.FactionId);
                _reputationManager.AddReputation(factionId, 0.12f, $"Mission completed: {mission.Description}");
            }
        }

        /// <summary>
        /// Fail a mission (player died, time ran out, etc.)
        /// </summary>
        public void FailMission(Mission mission, string reason)
        {
            if (mission == null || mission.Status != MissionStatus.Active) return;

            mission.Status = MissionStatus.Failed;
            _activeMissions.Remove(mission);
            _completedMissions.Add(mission);
            _waypointSystem?.UnregisterMission(mission);
            _worldManager?.OnMissionFinished(mission);

            _notificationManager?.ShowMessage($"Mission failed: {reason}", 4f);
            Console.WriteLine($"[MISSION] Failed: {mission.Description} | Reason: {reason}");
        }

        /// <summary>
        /// Update all active missions (track time, check conditions)
        /// </summary>
        public void Update(float deltaTime, bool playerDestroyed)
        {
            // Iterate backwards so we can remove during iteration
            for (int i = _activeMissions.Count - 1; i >= 0; i--)
            {
                var mission = _activeMissions[i];

                // Update elapsed time
                mission.ElapsedTime += deltaTime;

                // Check for time expiration
                if (mission.IsExpired)
                {
                    FailMission(mission, "Time ran out");
                    continue;
                }

                // Check for player death
                if (playerDestroyed)
                {
                    FailMission(mission, "Ship destroyed");
                    continue;
                }

                // Check if objective has been marked complete
                if (mission.ObjectiveComplete)
                {
                    CompleteMission(mission);
                }
            }
        }

        /// <summary>
        /// Fail all active missions (e.g., player dies)
        /// </summary>
        public void FailAllActiveMissions(string reason)
        {
            for (int i = _activeMissions.Count - 1; i >= 0; i--)
            {
                FailMission(_activeMissions[i], reason);
            }
        }

        /// <summary>
        /// Mark a bounty mission as complete if the target name matches
        /// </summary>
        public void NotifyTargetDestroyed(string targetName)
        {
            if (string.IsNullOrWhiteSpace(targetName))
            {
                return;
            }

            foreach (var mission in _activeMissions)
            {
                if (mission.Type == MissionType.Bounty && mission.Status == MissionStatus.Active)
                {
                    if (!string.IsNullOrWhiteSpace(mission.Target) &&
                        targetName.Contains(mission.Target, StringComparison.OrdinalIgnoreCase))
                    {
                        mission.ObjectiveComplete = true;
                        Console.WriteLine($"[MISSION] Bounty target destroyed: {targetName}");
                    }
                }
            }
        }

        /// <summary>
        /// Check if player arrived at a delivery destination
        /// </summary>
        public void NotifyArrivedAtStation(string stationName)
        {
            if (string.IsNullOrWhiteSpace(stationName))
            {
                return;
            }

            foreach (var mission in _activeMissions)
            {
                if (mission.Type == MissionType.Delivery && mission.Status == MissionStatus.Active)
                {
                    if (!string.IsNullOrWhiteSpace(mission.Destination) &&
                        stationName.Contains(mission.Destination, StringComparison.OrdinalIgnoreCase))
                    {
                        mission.ObjectiveComplete = true;
                        Console.WriteLine($"[MISSION] Delivery arrived at: {stationName}");
                    }
                }
            }
        }
    }
}
