using System.Text.Json.Serialization;
using Microsoft.Xna.Framework;

namespace Roguelancer.Configuration {
    /// <summary>
    /// Configuration for a tradelane route connecting two points in a system.
    /// Rings are auto-generated along the path between start and end positions.
    /// </summary>
    public class TradelaneConfig {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("name")]
        public string Name { get; set; } = "Tradelane";

        [JsonPropertyName("description")]
        public string Description { get; set; } = string.Empty;

        [JsonPropertyName("system_index")]
        public int SystemIndex { get; set; } = 1;

        [JsonPropertyName("start_position_x")]
        public float StartPositionX { get; set; } = 0f;

        [JsonPropertyName("start_position_y")]
        public float StartPositionY { get; set; } = 0f;

        [JsonPropertyName("start_position_z")]
        public float StartPositionZ { get; set; } = 0f;

        [JsonPropertyName("end_position_x")]
        public float EndPositionX { get; set; } = 0f;

        [JsonPropertyName("end_position_y")]
        public float EndPositionY { get; set; } = 0f;

        [JsonPropertyName("end_position_z")]
        public float EndPositionZ { get; set; } = 0f;

        [JsonPropertyName("ring_spacing")]
        public float RingSpacing { get; set; } = 500f;

        [JsonPropertyName("ring_scale")]
        public float RingScale { get; set; } = 5.0f;

        [JsonPropertyName("activation_range")]
        public float ActivationRange { get; set; } = 200f;

        [JsonPropertyName("travel_speed")]
        public float TravelSpeed { get; set; } = 1500f;

        [JsonPropertyName("ring_color_r")]
        public int RingColorR { get; set; } = 100;

        [JsonPropertyName("ring_color_g")]
        public int RingColorG { get; set; } = 200;

        [JsonPropertyName("ring_color_b")]
        public int RingColorB { get; set; } = 255;

        [JsonPropertyName("model_path")]
        public string ModelPath { get; set; } = "BASES/TRACK_RING/TRACK_RING";

        /// <summary>
        /// Vertical offset between the top and bottom ring at each position.
        /// Top ring travels forward (start->end), bottom ring travels reverse (end->start).
        /// </summary>
        [JsonPropertyName("ring_vertical_offset")]
        public float RingVerticalOffset { get; set; } = 30f;

        /// <summary>
        /// Small stable lateral separation used by the two traffic streams.
        /// It keeps opposing ships readable without creating two unrelated
        /// highways.
        /// </summary>
        [JsonPropertyName("traffic_lateral_offset")]
        public float TrafficLateralOffset { get; set; } = 24f;

        /// <summary>
        /// Hostile infrastructure damage required to disrupt one ring.
        /// </summary>
        [JsonPropertyName("disruption_damage_threshold")]
        public float DisruptionDamageThreshold { get; set; } = 100f;

        /// <summary>
        /// Transient ring recovery duration. Phase 47 intentionally keeps
        /// disruption out of save data.
        /// </summary>
        [JsonPropertyName("disruption_recovery_seconds")]
        public float DisruptionRecoverySeconds { get; set; } = 45f;

        /// <summary>
        /// Extended range at which the player can initiate docking with a tradelane ring.
        /// </summary>
        [JsonPropertyName("docking_range")]
        public float DockingRange { get; set; } = 600f;

        /// <summary>
        /// Range at which the ship begins auto-orienting toward the ring opening.
        /// </summary>
        [JsonPropertyName("auto_orient_range")]
        public float AutoOrientRange { get; set; } = 500f;

        /// <summary>
        /// Helper to get start position as Vector3
        /// </summary>
        [JsonIgnore]
        public Vector3 StartPosition => new Vector3(StartPositionX, StartPositionY, StartPositionZ);

        /// <summary>
        /// Helper to get end position as Vector3
        /// </summary>
        [JsonIgnore]
        public Vector3 EndPosition => new Vector3(EndPositionX, EndPositionY, EndPositionZ);

        [JsonIgnore]
        public string StableId => string.IsNullOrWhiteSpace(Id)
            ? NormalizeId(Name)
            : Id.Trim();

        /// <summary>
        /// Helper to get ring energy color
        /// </summary>
        [JsonIgnore]
        public Color RingColor => new Color(RingColorR, RingColorG, RingColorB);

        private static string NormalizeId(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return "tradelane";

            var chars = new System.Text.StringBuilder(value.Length);
            bool previousWasSeparator = false;
            foreach (char character in value.Trim().ToLowerInvariant())
            {
                if (char.IsLetterOrDigit(character))
                {
                    chars.Append(character);
                    previousWasSeparator = false;
                }
                else if (!previousWasSeparator)
                {
                    chars.Append('_');
                    previousWasSeparator = true;
                }
            }

            return chars.ToString().Trim('_');
        }
    }
}
