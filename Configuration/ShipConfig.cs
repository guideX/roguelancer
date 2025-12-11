using System.Text.Json.Serialization;
using Microsoft.Xna.Framework;

namespace Roguelancer.Configuration {
    /// <summary>
    /// Configuration for a ship (NPC or player)
    /// </summary>
    public class ShipConfig {
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

        [JsonPropertyName("initial_model_up_x")]
        public float InitialModelUpX { get; set; } = 0f;

        [JsonPropertyName("initial_model_up_y")]
        public float InitialModelUpY { get; set; } = 1f;

        [JsonPropertyName("initial_model_up_z")]
        public float InitialModelUpZ { get; set; } = 0f;

        [JsonPropertyName("initial_model_right_x")]
        public float InitialModelRightX { get; set; } = 1f;

        [JsonPropertyName("initial_model_right_y")]
        public float InitialModelRightY { get; set; } = 0f;

        [JsonPropertyName("initial_model_right_z")]
        public float InitialModelRightZ { get; set; } = 0f;

        [JsonPropertyName("initial_velocity_x")]
        public float InitialVelocityX { get; set; } = 0f;

        [JsonPropertyName("initial_velocity_y")]
        public float InitialVelocityY { get; set; } = 0f;

        [JsonPropertyName("initial_velocity_z")]
        public float InitialVelocityZ { get; set; } = 0f;

        [JsonPropertyName("initial_current_thrust")]
        public float InitialCurrentThrust { get; set; } = 0f;

        [JsonPropertyName("initial_direction_x")]
        public float InitialDirectionX { get; set; } = 0f;

        [JsonPropertyName("initial_direction_y")]
        public float InitialDirectionY { get; set; } = 0f;

        [JsonPropertyName("initial_direction_z")]
        public float InitialDirectionZ { get; set; } = -1f;

        [JsonPropertyName("cargo_space")]
        public int CargoSpace { get; set; } = 50;

        [JsonPropertyName("max_energy")]
        public float MaxEnergy { get; set; } = 200f;

        [JsonPropertyName("energy_regen_rate")]
        public float EnergyRegenRate { get; set; } = 50f;

        [JsonPropertyName("energy_regen_delay")]
        public float EnergyRegenDelay { get; set; } = 2f;

        [JsonPropertyName("patrol_center_x")]
        public float? PatrolCenterX { get; set; }

        [JsonPropertyName("patrol_center_y")]
        public float? PatrolCenterY { get; set; }

        [JsonPropertyName("patrol_center_z")]
        public float? PatrolCenterZ { get; set; }

        [JsonPropertyName("patrol_radius")]
        public float? PatrolRadius { get; set; }

        [JsonPropertyName("patrol_speed")]
        public float? PatrolSpeed { get; set; }

        // Model correction - applied to fix model orientation issues
        [JsonPropertyName("model_correction_rotation_x")]
        public float ModelCorrectionRotationX { get; set; } = 0f;

        [JsonPropertyName("model_correction_rotation_y")]
        public float ModelCorrectionRotationY { get; set; } = 0f;

        [JsonPropertyName("model_correction_rotation_z")]
        public float ModelCorrectionRotationZ { get; set; } = 0f;

        /// <summary>
        /// Helper to get startup position as Vector3
        /// </summary>
        [JsonIgnore]
        public Vector3 StartupPosition => new Vector3(StartupPositionX, StartupPositionY, StartupPositionZ);

        /// <summary>
        /// Helper to get initial velocity as Vector3
        /// </summary>
        [JsonIgnore]
        public Vector3 InitialVelocity => new Vector3(InitialVelocityX, InitialVelocityY, InitialVelocityZ);

        /// <summary>
        /// Helper to get initial direction as Vector3
        /// </summary>
        [JsonIgnore]
        public Vector3 InitialDirection => new Vector3(InitialDirectionX, InitialDirectionY, InitialDirectionZ);

        /// <summary>
        /// Helper to get patrol center as Vector3 (if configured)
        /// </summary>
        [JsonIgnore]
        public Vector3? PatrolCenter => PatrolCenterX.HasValue && PatrolCenterY.HasValue && PatrolCenterZ.HasValue 
            ? new Vector3(PatrolCenterX.Value, PatrolCenterY.Value, PatrolCenterZ.Value) 
            : null;

        /// <summary>
        /// Helper to get model correction rotation as Matrix
        /// </summary>
        [JsonIgnore]
        public Matrix ModelCorrectionRotation {
            get {
                Matrix rotX = Matrix.CreateRotationX(ModelCorrectionRotationX);
                Matrix rotY = Matrix.CreateRotationY(ModelCorrectionRotationY);
                Matrix rotZ = Matrix.CreateRotationZ(ModelCorrectionRotationZ);
                return rotX * rotY * rotZ;
            }
        }
    }
}
