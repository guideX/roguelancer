// Roguelancer 0.1 Pre Alpha by Leon Aiossa
// http://team-nexgen.com
namespace Roguelancer.Models {
    /// <summary>
    /// Player Ship Control Model
    /// </summary>
    public class PlayerShipControlModel {
        /// <summary>
        /// Use Input
        /// </summary>
        public bool UseInput { get; set; }
        /// <summary>
        /// Shake Value
        /// </summary>
        public float ShakeValue { get; set; }
        /// <summary>
        /// Update Direction X
        /// </summary>
        public float UpdateDirectionX { get; set; }
        /// <summary>
        /// Update Direction Y
        /// </summary>
        public float UpdateDirectionY { get; set; }
        /// <summary>
        /// Rotation X Left Add
        /// </summary>
        public const float RotationXLeftAdd = 1.0f;
        /// <summary>
        /// Rotation X Right Add
        /// </summary>
        public const float RotationXRightAdd = -1.0f;
        /// <summary>
        /// Rotation Y Up Add
        /// </summary>
        public const float RotationYUpAdd = -1.0f;
        /// <summary>
        /// Rotation Y Down Add
        /// </summary>
        public const float RotationYDownAdd = 1.0f;
        /// <summary>
        /// Rotation Rate
        /// </summary>
        public const float RotationRate = 1.5f;
        /// <summary>
        /// Mass
        /// </summary>
        public const float Mass = 1.0f;
        /// <summary>
        /// Thrust Force
        /// </summary>
        public const float ThrustForce = 24000.0f;
        /// <summary>
        /// Drag Factor
        /// </summary>
        public const float DragFactor = 0.97f;
        /// <summary>
        /// Max Thrust Amount
        /// </summary>
        public const float MaxThrustAmount = 0.2f;
        /// <summary>
        /// Max Thrust Afterburner Amount
        /// </summary>
        public const float MaxThrustAfterburnerAmount = 0.4f;
        /// <summary>
        /// thrust Add Speed
        /// </summary>
        public const float ThrustAddSpeed = 0.006f;
        /// <summary>
        /// Thrust After Burner Add Amount
        /// </summary>
        public const float ThrustAfterBurnerAddAmount = 0.1f;
        /// <summary>
        /// Thrust Slow Down Speed
        /// </summary>
        public const float ThrustSlowDownSpeed = 0.005f;
        /// <summary>
        /// Thrust Reverse Speed
        /// </summary>
        public const float ThrustReverseSpeed = -0.009f;
        /// <summary>
        /// Max thrust Reverse
        /// </summary>
        public const float MaxThrustReverse = -0.10f;
        /// <summary>
        /// Max Cruise Speed
        /// </summary>
        public const float MaxCruiseSpeed = 1.3f;
        /// <summary>
        /// Limit Altitude
        /// </summary>
        public const bool LimitAltitude = true;
        /// <summary>
        /// Thrust Min Not Zero
        /// </summary>
        public const float ThrustMinNotZero = .00001f;
        /// <summary>
        /// Player Ship Control Model
        /// </summary>
        public PlayerShipControlModel(RoguelancerGame game) {
            ShakeValue = .8f;
            UpdateDirectionX = game.Settings.PlayerShipUpdateDirectionX;
            UpdateDirectionY = game.Settings.PlayerShipUpdateDirectionY;
        }
    }
}