using System.Text.Json.Serialization;
using System.Text.Json.Serialization;
using Microsoft.Xna.Framework;

namespace Roguelancer.Configuration {
    /// <summary>
    /// Configuration for a jump hole connecting two star systems
    /// </summary>
    public class JumpHoleConfig {
        [JsonPropertyName("name")]
        public string Name { get; set; } = "Jump Hole";

        [JsonPropertyName("description")]
        public string Description { get; set; } = string.Empty;

        [JsonPropertyName("system_index")]
        public int SystemIndex { get; set; } = 1;

        [JsonPropertyName("target_system_index")]
        public int TargetSystemIndex { get; set; } = 2;

        [JsonPropertyName("target_jumphole_name")]
        public string TargetJumpHoleName { get; set; } = string.Empty;

        [JsonPropertyName("position_x")]
        public float PositionX { get; set; } = 0f;

        [JsonPropertyName("position_y")]
        public float PositionY { get; set; } = 0f;

        [JsonPropertyName("position_z")]
        public float PositionZ { get; set; } = 0f;

        [JsonPropertyName("radius")]
        public float Radius { get; set; } = 300f;

        [JsonPropertyName("activation_range")]
        public float ActivationRange { get; set; } = 150f;

        [JsonPropertyName("ring_color_r")]
        public int RingColorR { get; set; } = 100;

        [JsonPropertyName("ring_color_g")]
        public int RingColorG { get; set; } = 180;

        [JsonPropertyName("ring_color_b")]
        public int RingColorB { get; set; } = 255;

        [JsonPropertyName("core_color_r")]
        public int CoreColorR { get; set; } = 200;

        [JsonPropertyName("core_color_g")]
        public int CoreColorG { get; set; } = 220;

        [JsonPropertyName("core_color_b")]
        public int CoreColorB { get; set; } = 255;

        [JsonPropertyName("transit_duration")]
        public float TransitDuration { get; set; } = 4.0f;

        /// <summary>
        /// Helper to get position as Vector3
        /// </summary>
        [JsonIgnore]
        public Vector3 Position => new Vector3(PositionX, PositionY, PositionZ);

        /// <summary>
        /// Helper to get ring color
        /// </summary>
        [JsonIgnore]
        public Color RingColor => new Color(RingColorR, RingColorG, RingColorB);

        /// <summary>
        /// Helper to get core color
        /// </summary>
        [JsonIgnore]
        public Color CoreColor => new Color(CoreColorR, CoreColorG, CoreColorB);
    }
}
