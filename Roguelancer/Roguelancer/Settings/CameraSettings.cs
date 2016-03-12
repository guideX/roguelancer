// Roguelancer 0.1 Pre Alpha by Leon Aiossa
// http://www.team-nexgen.com
using Microsoft.Xna.Framework;
using Roguelancer.Functionality;
namespace Roguelancer.Settings {
    /// <summary>
    /// Camera Settings
    /// </summary>
    public class CameraSettings {
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
        /// <summary>
        /// Entry Point
        /// </summary>
        /// <param name="cameraSettingsIniFile"></param>
        public CameraSettings(string cameraSettingsIniFile) {
            DesiredPositionOffset = IniFile.ReadINIVector3(cameraSettingsIniFile, "settings", "desired_position_offset_x", "desired_position_offset_y", "desired_position_offset_z");
            Stiffness = IniFile.ReadIniFloat(cameraSettingsIniFile, "settings", "stiffness");
            Damping = IniFile.ReadIniFloat(cameraSettingsIniFile, "settings", "damping");
            Mass = IniFile.ReadIniFloat(cameraSettingsIniFile, "settings", "mass");
            FieldOfView = MathHelper.ToRadians(IniFile.ReadIniFloat(cameraSettingsIniFile, "settings", "field_of_view"));
            NearPlaneDistance = IniFile.ReadIniFloat(cameraSettingsIniFile, "settings", "near_plane_distance");
            ClippingDistance = IniFile.ReadIniFloat(cameraSettingsIniFile, "settings", "clipping_distance");
            LookAtOffset = IniFile.ReadINIVector3(cameraSettingsIniFile, "settings", "look_at_offset_x", "look_at_offset_y", "look_at_offset_z");
            LookAtDivideBy = IniFile.ReadIniFloat(cameraSettingsIniFile, "settings", "look_at_divide_by");
            NewCameraX = IniFile.ReadIniFloat(cameraSettingsIniFile, "settings", "new_camera_x");
            NewCameraY = IniFile.ReadIniFloat(cameraSettingsIniFile, "settings", "new_camera_y");
            ThrustViewAmount = int.Parse(IniFile.ReadINI(cameraSettingsIniFile, "settings", "thrust_view_amount"));
            AspectRatio = IniFile.ReadIniFloat(cameraSettingsIniFile, "settings", "aspect_ratio_1") / IniFile.ReadIniFloat(cameraSettingsIniFile, "settings", "aspect_ratio_2");
        }
    }
}