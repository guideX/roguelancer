using System.Text.Json.Serialization;
using Microsoft.Xna.Framework;

namespace Roguelancer.Configuration {
    /// <summary>
    /// Configuration for a space station
    /// </summary>
    public class StationConfig {
        [JsonPropertyName("description")]
        public string Description { get; set; } = string.Empty;

        [JsonPropertyName("description_long")]
        public string DescriptionLong { get; set; } = string.Empty;

        [JsonPropertyName("model_index")]
        public int ModelIndex { get; set; } = 0;

        [JsonPropertyName("system_index")]
        public int SystemIndex { get; set; } = 0;

        [JsonPropertyName("startup_position_x")]
        public float StartupPositionX { get; set; } = 0f;

        [JsonPropertyName("startup_position_y")]
        public float StartupPositionY { get; set; } = 0f;

        [JsonPropertyName("startup_position_z")]
        public float StartupPositionZ { get; set; } = 0f;

        [JsonPropertyName("startup_model_rotation_x")]
        public float StartupModelRotationX { get; set; } = 0f;

        [JsonPropertyName("startup_model_rotation_y")]
        public float StartupModelRotationY { get; set; } = 0f;

        [JsonPropertyName("startup_model_rotation_z")]
        public float StartupModelRotationZ { get; set; } = 0f;

        [JsonPropertyName("radius")]
        public float Radius { get; set; } = 400f;

        /// <summary>
        /// Helper to get startup position as Vector3
        /// </summary>
        [JsonIgnore]
        public Vector3 StartupPosition => new Vector3(StartupPositionX, StartupPositionY, StartupPositionZ);
    }
}
