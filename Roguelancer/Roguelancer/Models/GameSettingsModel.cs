using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Roguelancer.Settings;
namespace Roguelancer.Models {
    /// <summary>
    /// Game Settings Model
    /// </summary>
    public class GameSettingsModel {
        /// <summary>
        /// Key Assignments
        /// </summary>
        public KeyAssignmentsModel KeyAssignments { get; set; }
        /// <summary>
        /// Key Assignments Cache
        /// </summary>
        //public KeyAssignmentsCacheModel KeyAssignmentsCache { get; set; }
        /// <summary>
        /// Commodities Models
        /// </summary>
        public List<CommodityModel> CommoditiesModels { get; set; }
        /// <summary>
        /// Station Price models
        /// </summary>
        public List<StationPriceModel> StationPriceModels { get; set; }
        /// <summary>
        /// Menu Background Texture
        /// </summary>
        public string MenuBackgroundTexture { get; set; }
        /// <summary>
        /// Menu Text
        /// </summary>
        public string MenuText { get; set; }
        /// <summary>
        /// Camera Settings
        /// </summary>
        public CameraSettings CameraSettings { get; set; }
        /// <summary>
        /// Font Small
        /// </summary>
        public string FontSmall { get; set; }
        /// <summary>
        /// Font
        /// </summary>
        public string Font { get; set; }
        /// <summary>
        /// Resolution
        /// </summary>
        public Vector2 Resolution { get; set; }
        /// <summary>
        /// Model Settings
        /// </summary>
        public List<SettingsModelObject> ModelSettings { get; set; }
        /// <summary>
        /// Star System Settings
        /// </summary>
        public List<StarSystemSettings> StarSystemSettings { get; set; }
        /// <summary>
        /// Sensor Texture
        /// </summary>
        public string SensorTexture { get; set; }
        /// <summary>
        /// Bloom Enabled
        /// </summary>
        public bool BloomEnabled { get; set; }
        /// <summary>
        /// Bullet Mass
        /// </summary>
        public float BulletMass { get; set; }
        /// <summary>
        /// Bullet Thruster Force
        /// </summary>
        public float BulletThrusterForce { get; set; }
        /// <summary>
        /// Bullet Drag Factor
        /// </summary>
        public float BulletDragFactor { get; set; }
        /// <summary>
        /// Bullet Recharge Rate
        /// </summary>
        public int BulletRechargeRate { get; set; }
        /// <summary>
        /// Player Ship Update Direction Y
        /// </summary>
        //public float PlayerShipUpdateDirectionY { get; set; }
        /// <summary>
        /// Player Ship Shake Value
        /// </summary>
        public float PlayerShipShakeValue { get; set; }
        /// <summary>
        /// Full Screen
        /// </summary>
        public bool FullScreen { get; set; }
        /// <summary>
        /// Game Settings Ini File
        /// </summary>
        public string GameSettingsIniFile;
        /// <summary>
        /// Model Settings Ini File
        /// </summary>
        public string ModelSettingsIniFile;
        /// <summary>
        /// System Settings Ini File
        /// </summary>
        public string SystemsSettingsIniFile;
        /// <summary>
        /// Player Ini File
        /// </summary>
        public string PlayerIniFile;
        /// <summary>
        /// Camera Settings Ini File
        /// </summary>
        public string CameraSettingsIniFile;
        /// <summary>
        /// Bullet Settings Ini File
        /// </summary>
        public string BulletSettingsIniFile;
        /// <summary>
        /// System Ini Start Path
        /// </summary>
        public string SystemIniStartPath;
        /// <summary>
        /// Commodities Ini File
        /// </summary>
        public string CommoditiesIniFile;
        /// <summary>
        /// Commodities Settings Ini File
        /// </summary>
        public string CommoditiesSettingsIniFile;
        /// <summary>
        /// Game Settings Model
        /// </summary>
        public GameSettingsModel(RoguelancerGame game) {
            StarSystemSettings = new List<StarSystemSettings>();
            StationPriceModels = new List<StationPriceModel>();
            CommoditiesModels = new List<CommodityModel>();
            MenuText = "";
            Resolution = new Vector2(1280, 1024);
            var rootDir = System.IO.Directory.GetCurrentDirectory() + @"\..\..\..\";
            GameSettingsIniFile = rootDir + @"configuration\settings\settings.ini";
            ModelSettingsIniFile = rootDir + @"configuration\models.ini";
            SystemsSettingsIniFile = rootDir + @"configuration\systems\systems.ini";
            PlayerIniFile = rootDir + @"configuration\player\settings.ini";
            BulletSettingsIniFile = rootDir + @"configuration\objects\bullets.ini";
            CameraSettingsIniFile = rootDir + @"configuration\camera.ini";
            SystemIniStartPath = rootDir + @"configuration\systems\";
            CommoditiesSettingsIniFile = rootDir + @"configuration\commodities_settings.ini";
            CommoditiesIniFile = rootDir + @"configuration\commodities.ini";
            KeyAssignments = new KeyAssignmentsModel();
            //KeyAssignmentsCache = new KeyAssignmentsCacheModel();
        }
    }
}