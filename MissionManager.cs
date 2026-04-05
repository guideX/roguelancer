using System;
using System.Collections.Generic;
using System.Linq;

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

        // Mission generation data
        private static readonly string[] DeliveryTargets = {
            "Medical Supplies", "H-Fuel Cells", "Luxury Goods",
            "Construction Materials", "Military Hardware", "Food Rations",
            "Side Arms", "Engine Components", "Boron", "Diamonds"
        };

        private static readonly string[] DeliveryDestinations = {
            "Manhattan", "Rotor Nexus", "P887 Station",
            "Rochester Base", "Norfolk Shipyard", "West Point",
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

        public MissionManager(PlayerCredits playerCredits, NotificationManager notificationManager)
        {
            _playerCredits = playerCredits;
            _notificationManager = notificationManager;
        }

        /// <summary>
        /// Generate a single random mission
        /// </summary>
        public Mission GenerateRandomMission()
        {
            MissionType type = (MissionType)_random.Next(3);
            MissionDifficulty difficulty = (MissionDifficulty)_random.Next(4);

            int baseReward = difficulty switch
            {
                MissionDifficulty.Easy => _random.Next(500, 2000),
                MissionDifficulty.Medium => _random.Next(2000, 5000),
                MissionDifficulty.Hard => _random.Next(5000, 15000),
                MissionDifficulty.Deadly => _random.Next(15000, 50000),
                _ => 1000
            };

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

            switch (type)
            {
                case MissionType.Delivery:
                    target = DeliveryTargets[_random.Next(DeliveryTargets.Length)];
                    destination = DeliveryDestinations[_random.Next(DeliveryDestinations.Length)];
                    description = $"Deliver {target} to {destination}";
                    break;
                case MissionType.Bounty:
                    target = BountyTargets[_random.Next(BountyTargets.Length)];
                    destination = "Last seen near " + DeliveryDestinations[_random.Next(DeliveryDestinations.Length)];
                    description = $"Destroy {target}";
                    break;
                case MissionType.Escort:
                    target = EscortTargets[_random.Next(EscortTargets.Length)];
                    destination = DeliveryDestinations[_random.Next(DeliveryDestinations.Length)];
                    description = $"Escort {target} to {destination}";
                    break;
                default:
                    target = "Unknown";
                    destination = "Unknown";
                    description = "Unknown mission";
                    break;
            }

            return new Mission(type, difficulty, target, destination, baseReward, timeLimit, description);
        }

        /// <summary>
        /// Generate multiple random missions for a job board
        /// </summary>
        public List<Mission> GenerateJobBoardMissions(int count)
        {
            var missions = new List<Mission>();
            for (int i = 0; i < count; i++)
            {
                missions.Add(GenerateRandomMission());
            }
            return missions;
        }

        /// <summary>
        /// Accept and assign a mission to the player
        /// </summary>
        public bool AcceptMission(Mission mission)
        {
            if (mission.Status != MissionStatus.Available) return false;

            mission.Status = MissionStatus.Active;
            _activeMissions.Add(mission);
            _notificationManager?.ShowMessage($"Mission accepted: {mission.Description}", 3f);
            Console.WriteLine($"[MISSION] Accepted: {mission.GetSummary()}");
            return true;
        }

        /// <summary>
        /// Complete a mission and reward the player
        /// </summary>
        public void CompleteMission(Mission mission)
        {
            if (mission.Status != MissionStatus.Active) return;

            mission.Status = MissionStatus.Completed;
            _activeMissions.Remove(mission);
            _completedMissions.Add(mission);

            _playerCredits.AddCredits(mission.Reward);
            _notificationManager?.ShowMessage($"Mission complete! +{mission.Reward:N0} CR", 4f);
            Console.WriteLine($"[MISSION] Completed: {mission.Description} | Reward: {mission.Reward:N0} CR");
        }

        /// <summary>
        /// Fail a mission (player died, time ran out, etc.)
        /// </summary>
        public void FailMission(Mission mission, string reason)
        {
            if (mission.Status != MissionStatus.Active) return;

            mission.Status = MissionStatus.Failed;
            _activeMissions.Remove(mission);
            _completedMissions.Add(mission);

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
            foreach (var mission in _activeMissions)
            {
                if (mission.Type == MissionType.Bounty && mission.Status == MissionStatus.Active)
                {
                    if (targetName.Contains(mission.Target, StringComparison.OrdinalIgnoreCase))
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
            foreach (var mission in _activeMissions)
            {
                if (mission.Type == MissionType.Delivery && mission.Status == MissionStatus.Active)
                {
                    if (stationName.Contains(mission.Destination, StringComparison.OrdinalIgnoreCase))
                    {
                        mission.ObjectiveComplete = true;
                        Console.WriteLine($"[MISSION] Delivery arrived at: {stationName}");
                    }
                }
            }
        }
    }
}
