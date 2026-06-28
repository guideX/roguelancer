using System.Text.Json.Serialization;
using Roguelancer;

namespace Roguelancer.Configuration
{
    /// <summary>
    /// Defines a patrol group of NPCs within a system.
    /// </summary>
    public class NpcPatrolConfig
    {
        /// <summary>
        /// A descriptive name for this patrol group (e.g., "Fighter Wing Alpha").
        /// </summary>
        [JsonPropertyName("name")]
        public string Name { get; set; }

        /// <summary>
        /// The description of the ship configuration to use for this patrol.
        /// This should match a 'description' in one of the ship JSON files.
        /// </summary>
        [JsonPropertyName("ship_description")]
        public string ShipDescription { get; set; }

        /// <summary>
        /// The faction id assigned to ships spawned by this patrol.
        /// </summary>
        [JsonPropertyName("faction_id")]
        public string FactionId { get; set; } = string.Empty;

        /// <summary>
        /// The number of ships to spawn in this patrol group.
        /// </summary>
        [JsonPropertyName("count")]
        public int Count { get; set; }

        /// <summary>
        /// The center of the patrol area (X-coordinate).
        /// </summary>
        [JsonPropertyName("patrol_center_x")]
        public float PatrolCenterX { get; set; }

        /// <summary>
        /// The center of the patrol area (Y-coordinate).
        /// </summary>
        [JsonPropertyName("patrol_center_y")]
        public float PatrolCenterY { get; set; }

        /// <summary>
        /// The center of the patrol area (Z-coordinate).
        /// </summary>
        [JsonPropertyName("patrol_center_z")]
        public float PatrolCenterZ { get; set; }

        /// <summary>
        /// The radius of the patrol area. Ships will move randomly within this sphere.
        /// </summary>
        [JsonPropertyName("patrol_radius")]
        public float PatrolRadius { get; set; }

        /// <summary>
        /// The speed at which the ships in this patrol move.
        /// </summary>
        [JsonPropertyName("patrol_speed")]
        public float PatrolSpeed { get; set; }

        /// <summary>
        /// The radius around the patrol center where ships will initially spawn.
        /// </summary>
        [JsonPropertyName("spawn_radius")]
        public float SpawnRadius { get; set; }
    }
}
