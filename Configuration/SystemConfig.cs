using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Roguelancer.Configuration {
    /// <summary>
    /// Configuration for a star system
    /// </summary>
    public class SystemConfig {
        [JsonPropertyName("description")]
        public string Description { get; set; } = string.Empty;

        [JsonPropertyName("path")]
        public string Path { get; set; } = string.Empty;

        [JsonPropertyName("sun_position_x")]
        public float SunPositionX { get; set; } = 5000f;

        [JsonPropertyName("sun_position_y")]
        public float SunPositionY { get; set; } = 2000f;

        [JsonPropertyName("sun_position_z")]
        public float SunPositionZ { get; set; } = 3000f;

        [JsonPropertyName("sun_scale")]
        public float SunScale { get; set; } = 500f;

        [JsonPropertyName("sun_intensity")]
        public float SunIntensity { get; set; } = 2.0f;

        [JsonPropertyName("sun_rotation_speed")]
        public float SunRotationSpeed { get; set; } = 0.05f;

        /// <summary>
        /// A list of NPC patrols defined for this system.
        /// </summary>
        [JsonPropertyName("npc_patrols")]
        public List<NpcPatrolConfig> NpcPatrols { get; set; } = new List<NpcPatrolConfig>();
    }
}
