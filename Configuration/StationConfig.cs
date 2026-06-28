using System.Text.Json.Serialization;
using Microsoft.Xna.Framework;
using Roguelancer;

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

        [JsonPropertyName("model_path")]
        public string ModelPath { get; set; } = string.Empty;

        [JsonPropertyName("system_index")]
        public int SystemIndex { get; set; } = 0;

        [JsonPropertyName("faction_id")]
        public string FactionId { get; set; } = FactionManager.NeutralCivilians;

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

        [JsonPropertyName("scale")]
        public float Scale { get; set; } = 10.0f;

        [JsonPropertyName("rotation_speed")]
        public float RotationSpeed { get; set; } = 0.0f;

        [JsonPropertyName("docking_range")]
        public float DockingRange { get; set; } = 500f;

        [JsonPropertyName("docking_point_x")]
        public float DockingPointX { get; set; } = 0f;

        [JsonPropertyName("docking_point_y")]
        public float DockingPointY { get; set; } = 0f;

        [JsonPropertyName("docking_point_z")]
        public float DockingPointZ { get; set; } = 0f;

        [JsonPropertyName("docking_approach_distance")]
        public float DockingApproachDistance { get; set; } = 200f;

        /// <summary>
        /// Helper to get startup position as Vector3
        /// </summary>
        [JsonIgnore]
        public Vector3 StartupPosition => new Vector3(StartupPositionX, StartupPositionY, StartupPositionZ);

        /// <summary>
        /// Helper to get docking point as Vector3 (relative to station)
        /// </summary>
        [JsonIgnore]
        public Vector3 DockingPoint => new Vector3(DockingPointX, DockingPointY, DockingPointZ);
    }
}
