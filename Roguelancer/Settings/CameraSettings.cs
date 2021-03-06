﻿using Microsoft.Xna.Framework;
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
            Model.DesiredPositionOffset = NativeMethods.ReadINIVector3(cameraSettingsIniFile, "settings", "desired_position_offset_x", "desired_position_offset_y", "desired_position_offset_z");
            Model.Stiffness = NativeMethods.ReadINIFloat(cameraSettingsIniFile, "settings", "stiffness");
            Model.Damping = NativeMethods.ReadINIFloat(cameraSettingsIniFile, "settings", "damping");
            Model.Mass = NativeMethods.ReadINIFloat(cameraSettingsIniFile, "settings", "mass");
            Model.FieldOfView = MathHelper.ToRadians(NativeMethods.ReadINIFloat(cameraSettingsIniFile, "settings", "field_of_view"));
            Model.NearPlaneDistance = NativeMethods.ReadINIFloat(cameraSettingsIniFile, "settings", "near_plane_distance");
            Model.ClippingDistance = NativeMethods.ReadINIFloat(cameraSettingsIniFile, "settings", "clipping_distance");
            Model.LookAtOffset = NativeMethods.ReadINIVector3(cameraSettingsIniFile, "settings", "look_at_offset_x", "look_at_offset_y", "look_at_offset_z");
            Model.LookAtDivideBy = NativeMethods.ReadINIFloat(cameraSettingsIniFile, "settings", "look_at_divide_by");
            Model.NewCameraX = NativeMethods.ReadINIFloat(cameraSettingsIniFile, "settings", "new_camera_x");
            Model.NewCameraY = NativeMethods.ReadINIFloat(cameraSettingsIniFile, "settings", "new_camera_y");
            Model.ThrustViewAmount = int.Parse(NativeMethods.ReadINI(cameraSettingsIniFile, "settings", "thrust_view_amount"));
            Model.AspectRatio = NativeMethods.ReadINIFloat(cameraSettingsIniFile, "settings", "aspect_ratio_1") / NativeMethods.ReadINIFloat(cameraSettingsIniFile, "settings", "aspect_ratio_2");
        }
    }
}