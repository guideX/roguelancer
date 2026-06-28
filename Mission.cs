using System;


using Microsoft.Xna.Framework;

namespace Roguelancer
{
    /// <summary>
    /// Types of missions available in the game
    /// </summary>
    public enum MissionType
    {
        Delivery,
        Bounty,
        Escort
    }

    /// <summary>
    /// Mission difficulty / risk level
    /// </summary>
    public enum MissionDifficulty
    {
        Easy,
        Medium,
        Hard,
        Deadly
    }

    /// <summary>
    /// Current status of a mission
    /// </summary>
    public enum MissionStatus
    {
        Available,
        Active,
        Completed,
        Failed
    }

    /// <summary>
    /// Represents a single mission that can be offered, accepted, tracked, and completed
    /// </summary>
    public class Mission
    {
        private static int _nextId = 1;

        public int Id { get; }
        public MissionType Type { get; }
        public MissionDifficulty Difficulty { get; }
        public MissionStatus Status { get; set; }
        public string Target { get; }
        public string Destination { get; }
        public int Reward { get; }
        public float TimeLimit { get; }
        public float ElapsedTime { get; set; }
        public string Description { get; }
        public string OfferedBy { get; set; }
        public string FactionId { get; set; }

        // Progress tracking
        public bool ObjectiveComplete { get; set; }

        // Waypoint / marker tracking (resolved at runtime by MissionWaypointSystem)
        public Vector3? TargetPosition { get; set; }
        public SpaceObject TargetSpaceObject { get; set; }

        public bool IsExpired => TimeLimit > 0 && ElapsedTime >= TimeLimit;
        public float TimeRemaining => TimeLimit > 0 ? Math.Max(0, TimeLimit - ElapsedTime) : -1;

        public Mission(MissionType type, MissionDifficulty difficulty, string target, string destination, int reward, float timeLimit, string description, string factionId = null)
        {
            Id = _nextId++;
            Type = type;
            Difficulty = difficulty;
            Status = MissionStatus.Available;
            Target = target;
            Destination = destination;
            Reward = reward;
            TimeLimit = timeLimit;
            ElapsedTime = 0f;
            Description = description;
            OfferedBy = "";
            FactionId = FactionManager.NormalizeFactionId(factionId);
            ObjectiveComplete = false;
        }

        private Mission(int id, MissionType type, MissionDifficulty difficulty, MissionStatus status, string target, string destination, int reward, float timeLimit, string description, string offeredBy, string factionId, float elapsedTime, bool objectiveComplete)
        {
            Id = id > 0 ? id : _nextId++;
            Type = type;
            Difficulty = difficulty;
            Status = status;
            Target = target ?? string.Empty;
            Destination = destination ?? string.Empty;
            Reward = reward;
            TimeLimit = Math.Max(0f, timeLimit);
            ElapsedTime = Math.Max(0f, elapsedTime);
            Description = description ?? string.Empty;
            OfferedBy = offeredBy ?? string.Empty;
            FactionId = FactionManager.NormalizeFactionId(factionId);
            ObjectiveComplete = objectiveComplete;

            if (_nextId <= Id)
            {
                _nextId = Id + 1;
            }
        }

        /// <summary>
        /// Restore a mission from save data without disturbing the save file's mission identity.
        /// </summary>
        public static Mission CreateRestored(int id, MissionType type, MissionDifficulty difficulty, MissionStatus status, string target, string destination, int reward, float timeLimit, string description, string offeredBy, string factionId, float elapsedTime, bool objectiveComplete)
        {
            return new Mission(id, type, difficulty, status, target, destination, reward, timeLimit, description, offeredBy, factionId, elapsedTime, objectiveComplete);
        }

        /// <summary>
        /// Get a short summary string for UI display
        /// </summary>
        public string GetSummary()
        {
            string diffStr = Difficulty switch
            {
                MissionDifficulty.Easy => "LOW RISK",
                MissionDifficulty.Medium => "MODERATE RISK",
                MissionDifficulty.Hard => "HIGH RISK",
                MissionDifficulty.Deadly => "EXTREME RISK",
                _ => "UNKNOWN"
            };

            string typeStr = Type switch
            {
                MissionType.Delivery => "DELIVERY",
                MissionType.Bounty => "BOUNTY",
                MissionType.Escort => "ESCORT",
                _ => "MISSION"
            };

            return $"[{typeStr}] {Description} | {diffStr} | {Reward:N0} CR";
        }

        /// <summary>
        /// Get a detailed multi-line description for mission info panel
        /// </summary>
        public string GetDetailedDescription()
        {
            string timeStr = TimeLimit > 0 ? $"Time Limit: {TimeLimit:F0}s" : "No Time Limit";
            return $"{Description}\nTarget: {Target}\nDestination: {Destination}\nFaction: {FactionId}\nReward: {Reward:N0} CR\nDifficulty: {Difficulty}\n{timeStr}";
        }
    }
}
