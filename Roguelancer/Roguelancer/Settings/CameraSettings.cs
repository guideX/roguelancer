// Roguelancer 0.1 Pre Alpha by Leon Aiossa
// http://www.team-nexgen.com
using Microsoft.Xna.Framework;
using Roguelancer.Functionality;
using Roguelancer.Models;
namespace Roguelancer.Settings {
    /// <summary>
    /// Camera Settings
    /// </summary>
    public class CameraSettings {
        /// <summary>
        /// Camera Settings Model
        /// </summary>
        public CameraSettingsModel Model { get; set; }
        /// <summary>
        /// Entry Point
        /// </summary>
        /// <param name="cameraSettingsIniFile"></param>
        public CameraSettings(string cameraSettingsIniFile) {
            Model = new CameraSettingsModel();
            Model.DesiredPositionOffset = IniFile.ReadINIVector3(cameraSettingsIniFile, "settings", "desired_position_offset_x", "desired_position_offset_y", "desired_position_offset_z");
            Model.Stiffness = IniFile.ReadIniFloat(cameraSettingsIniFile, "settings", "stiffness");
            Model.Damping = IniFile.ReadIniFloat(cameraSettingsIniFile, "settings", "damping");
            Model.Mass = IniFile.ReadIniFloat(cameraSettingsIniFile, "settings", "mass");
            Model.FieldOfView = MathHelper.ToRadians(IniFile.ReadIniFloat(cameraSettingsIniFile, "settings", "field_of_view"));
            Model.NearPlaneDistance = IniFile.ReadIniFloat(cameraSettingsIniFile, "settings", "near_plane_distance");
            Model.ClippingDistance = IniFile.ReadIniFloat(cameraSettingsIniFile, "settings", "clipping_distance");
            Model.LookAtOffset = IniFile.ReadINIVector3(cameraSettingsIniFile, "settings", "look_at_offset_x", "look_at_offset_y", "look_at_offset_z");
            Model.LookAtDivideBy = IniFile.ReadIniFloat(cameraSettingsIniFile, "settings", "look_at_divide_by");
            Model.NewCameraX = IniFile.ReadIniFloat(cameraSettingsIniFile, "settings", "new_camera_x");
            Model.NewCameraY = IniFile.ReadIniFloat(cameraSettingsIniFile, "settings", "new_camera_y");
            Model.ThrustViewAmount = int.Parse(IniFile.ReadINI(cameraSettingsIniFile, "settings", "thrust_view_amount"));
            Model.AspectRatio = IniFile.ReadIniFloat(cameraSettingsIniFile, "settings", "aspect_ratio_1") / IniFile.ReadIniFloat(cameraSettingsIniFile, "settings", "aspect_ratio_2");
        }
    }
}