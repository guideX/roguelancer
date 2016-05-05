// Roguelancer 0.1 Pre Alpha by Leon Aiossa
// http://team-nexgen.com
using Microsoft.Xna.Framework;
namespace Roguelancer.Models {
    /// <summary>
    /// Camera Settings Model
    /// </summary>
    public class CameraSettingsModel {
        /// <summary>
        /// Desired Position
        /// </summary>
        public Vector3 DesiredPositionOffset { get; set; }
        /// <summary>
        /// Stiffness
        /// </summary>
        public float Stiffness { get; set; }
        /// <summary>
        /// Damping
        /// </summary>
        public float Damping { get; set; }
        /// <summary>
        /// Mass
        /// </summary>
        public float Mass { get; set; }
        /// <summary>
        /// Field of View
        /// </summary>
        public float FieldOfView { get; set; }
        /// <summary>
        /// Near Plane Distance
        /// </summary>
        public float NearPlaneDistance { get; set; }
        /// <summary>
        /// Clipping Distance
        /// </summary>
        public float ClippingDistance { get; set; }
        /// <summary>
        /// Look at Distance
        /// </summary>
        public Vector3 LookAtOffset { get; set; }
        /// <summary>
        /// Look at Divide by
        /// </summary>
        public float LookAtDivideBy { get; set; }
        /// <summary>
        /// New Camera X
        /// </summary>
        public float NewCameraX { get; set; }
        /// <summary>
        /// New Camera Y
        /// </summary>
        public float NewCameraY { get; set; }
        /// <summary>
        /// Thrust View Amount
        /// </summary>
        public int ThrustViewAmount { get; set; }
        /// <summary>
        /// Aspect Ratio
        /// </summary>
        public float AspectRatio { get; set; }
    }
}