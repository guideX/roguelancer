using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Xna.Framework;
using Roguelancer.Functionality;
namespace Roguelancer.Settings {
    public class CameraSettings {
        public Vector3 desiredPositionOffset { get; set; }
        public float stiffness { get; set; }
        public float damping { get; set; }
        public float mass { get; set; }
        public float fieldOfView { get; set; }
        public float nearPlaneDistance { get; set; }
        public float clippingDistance { get; set; }
        public Vector3 lookAtOffset { get; set; }
        public float lookAtDivideBy { get; set; }
        public float newCameraX { get; set; }
        public float newCameraY { get; set; }
        public int thrustViewAmount { get; set; }
        public float aspectRatio { get; set; }
        public CameraSettings(string cameraSettingsIniFile) {
            int temp;
            desiredPositionOffset = IniFile.ReadINIVector3(cameraSettingsIniFile, "settings", "desired_position_offset_x", "desired_position_offset_y", "desired_position_offset_z");
            stiffness = IniFile.ReadIniFloat(cameraSettingsIniFile, "settings", "stiffness");
            damping = IniFile.ReadIniFloat(cameraSettingsIniFile, "settings", "damping");
            mass = IniFile.ReadIniFloat(cameraSettingsIniFile, "settings", "mass");
            fieldOfView = MathHelper.ToRadians(IniFile.ReadIniFloat(cameraSettingsIniFile, "settings", "field_of_view"));
            nearPlaneDistance = IniFile.ReadIniFloat(cameraSettingsIniFile, "settings", "near_plane_distance");
            clippingDistance = IniFile.ReadIniFloat(cameraSettingsIniFile, "settings", "clipping_distance");
            lookAtOffset = IniFile.ReadINIVector3(cameraSettingsIniFile, "settings", "look_at_offset_x", "look_at_offset_y", "look_at_offset_z");
            lookAtDivideBy = IniFile.ReadIniFloat(cameraSettingsIniFile, "settings", "look_at_divide_by");
            newCameraX = IniFile.ReadIniFloat(cameraSettingsIniFile, "settings", "new_camera_x");
            newCameraY = IniFile.ReadIniFloat(cameraSettingsIniFile, "settings", "new_camera_y");
            int.TryParse(IniFile.ReadINI(cameraSettingsIniFile, "settings", "thrust_view_amount"), out temp);
            thrustViewAmount = temp;
            aspectRatio = IniFile.ReadIniFloat(cameraSettingsIniFile, "settings", "aspect_ratio_1") / IniFile.ReadIniFloat(cameraSettingsIniFile, "settings", "aspect_ratio_2");
        }
    }
}