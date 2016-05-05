// Roguelancer 0.1 Pre Alpha by Leon Aiossa
// http://www.team-nexgen.com
using System;
using System.Linq;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Roguelancer.Functionality;
using Roguelancer.Interfaces;
using Roguelancer.Models;
namespace Roguelancer.Settings {
    /// <summary>
    /// Game Settings
    /// </summary>
    public class GameSettings : IGameSettings {
        #region "public variables"
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
        public float BulletMass { get; set; }
        public float BulletThrusterForce { get; set; }
        public float BulletDragFactor { get; set; }
        public int BulletRechargeRate { get; set; }
        public float PlayerShipUpdateDirectionX { get; set; }
        public float PlayerShipUpdateDirectionY { get; set; }
        public float PlayerShipShakeValue { get; set; }
        /// <summary>
        /// Full Screen
        /// </summary>
        public bool FullScreen { get; set; }
        #endregion
        #region "private variables"
        /// <summary>
        /// Game Settings Ini File
        /// </summary>
        private string _gameSettingsIniFile;
        /// <summary>
        /// Model Settings Ini File
        /// </summary>
        private string _modelSettingsIniFile;
        /// <summary>
        /// System Settings Ini File
        /// </summary>
        private string _systemsSettingsIniFile;
        /// <summary>
        /// Player Ini File
        /// </summary>
        private string _playerIniFile;
        /// <summary>
        /// Camera Settings Ini File
        /// </summary>
        private string _cameraSettingsIniFile;
        /// <summary>
        /// Bullet Settings Ini File
        /// </summary>
        private string _bulletSettingsIniFile;
        /// <summary>
        /// System Ini Start Path
        /// </summary>
        private string _systemIniStartPath;
        /// <summary>
        /// Commodities Ini File
        /// </summary>
        private string _commoditiesIniFile;
        /// <summary>
        /// Commodities Settings Ini File
        /// </summary>
        private string _commoditiesSettingsIniFile;
        #endregion
        #region "public functions"
        public GameSettings() {
            StationPriceModels = new List<StationPriceModel>();
            CommoditiesModels = new List<CommodityModel>();
            MenuText = "Roguelancer" + Environment.NewLine + Environment.NewLine + "10 = Play Game" + Environment.NewLine + "F9 = Return to menu" + Environment.NewLine + "ESC = Quit";
            Font = "LucidaFont";
            FontSmall = "LucidiaFontSmall";
            Resolution = new Vector2(1280, 1024);
            var rootDir = System.IO.Directory.GetCurrentDirectory() + @"\..\..\..\";
            _gameSettingsIniFile = rootDir + @"configuration\settings\settings.ini";
            _modelSettingsIniFile = rootDir + @"configuration\models.ini";
            _systemsSettingsIniFile = rootDir + @"configuration\systems\systems.ini";
            _playerIniFile = rootDir + @"configuration\player\settings.ini";
            _bulletSettingsIniFile = rootDir + @"configuration\objects\bullets.ini";
            _cameraSettingsIniFile = rootDir + @"configuration\camera.ini";
            _systemIniStartPath = rootDir + @"configuration\systems\";
            _commoditiesSettingsIniFile = rootDir + @"configuration\commodities_settings.ini";
            _commoditiesIniFile = rootDir + @"configuration\commodities.ini";
            SensorTexture = NativeMethods.ReadINI(_gameSettingsIniFile, "Settings", "SensorTexture");
            var b = false;
            if (bool.TryParse(NativeMethods.ReadINI(_gameSettingsIniFile, "Settings", "BloomEnabled"), out b)) { BloomEnabled = b; }
            if (bool.TryParse(NativeMethods.ReadINI(_gameSettingsIniFile, "Settings", "FullScreen"), out b)) { FullScreen = b; }
            BulletMass = NativeMethods.ReadINIFloat(_gameSettingsIniFile, "Settings", "BulletMass", 1.0f);
            BulletThrusterForce = NativeMethods.ReadINIFloat(_gameSettingsIniFile, "Settings", "BulletThrusterForce", 44000.0f);
            BulletDragFactor = NativeMethods.ReadINIFloat(_gameSettingsIniFile, "Settings", "BulletDragFactor", 0.97f);
            BulletRechargeRate = NativeMethods.ReadINIInt(_gameSettingsIniFile, "Settings", "BulletRechargeRate", 240);
            PlayerShipUpdateDirectionX = NativeMethods.ReadINIFloat(_gameSettingsIniFile, "PlayerShip", "UpdateDirectionX", 2.0f);
            PlayerShipUpdateDirectionY = NativeMethods.ReadINIFloat(_gameSettingsIniFile, "PlayerShip", "UpdateDirectionY", 2.0f);
            PlayerShipShakeValue = NativeMethods.ReadINIFloat(_gameSettingsIniFile, "PlayerShip", "ShakeValue", .8f);
            MenuBackgroundTexture = NativeMethods.ReadINI(_gameSettingsIniFile, "Settings", "menu_background");
            CameraSettings = new CameraSettings(_cameraSettingsIniFile);
            ModelSettings = new List<SettingsModelObject>();
            for (var i = 1; i < NativeMethods.ReadINIInt(_modelSettingsIniFile, "settings", "count", 0) + 1; ++i) {
                ModelSettings.Add(new SettingsModelObject(
                    NativeMethods.ReadINI(_modelSettingsIniFile, i.ToString().Trim(), "path"),
                    (Enum.ModelType)NativeMethods.ReadINIInt(_modelSettingsIniFile, i.ToString().Trim(), "type", 0),
                    NativeMethods.ReadINIBool(_modelSettingsIniFile, i.ToString().Trim(), "enabled", false),
                    i
                ));
            }
            StarSystemSettings = new List<StarSystemSettings>();
            for (var i = 1; i < NativeMethods.ReadINIInt(_systemsSettingsIniFile, "settings", "count", 0) + 1; ++i) {
                StarSystemSettings.Add(new StarSystemSettings(
                    i,
                    NativeMethods.ReadINI(_systemsSettingsIniFile, i.ToString().Trim(), "path", ""),
                    _systemIniStartPath,
                    ModelSettings,
                    ModelWorldObjects.Read(i, ModelSettings, _playerIniFile, "settings"),
                    new StarSettings(
                        NativeMethods.ReadINIBool(_systemsSettingsIniFile, i.ToString(), "starsEnabled", false),
                        NativeMethods.ReadINIInt(_systemsSettingsIniFile, i.ToString(), "amountOfStarsPerSheet", 0),
                        NativeMethods.ReadINIInt(_systemsSettingsIniFile, i.ToString(), "maxPositionX", 0),
                        NativeMethods.ReadINIInt(_systemsSettingsIniFile, i.ToString(), "maxPositionY", 0),
                        NativeMethods.ReadINIInt(_systemsSettingsIniFile, i.ToString(), "maxSize", 0),
                        NativeMethods.ReadINIInt(_systemsSettingsIniFile, i.ToString(), "maxPositionIncrementY", 0),
                        NativeMethods.ReadINIInt(_systemsSettingsIniFile, i.ToString(), "maxPositionStartingY", 0),
                        NativeMethods.ReadINIInt(_systemsSettingsIniFile, i.ToString(), "numberOfStarSheets", 0)
                    )
                ));
            }
            if (System.IO.File.Exists(_commoditiesSettingsIniFile)) {
                for (var i = 1; i < NativeMethods.ReadINIInt(_commoditiesSettingsIniFile, "Settings", "Count", 0) + 1; ++i) {
                    StationPriceModels.Add(new StationPriceModel() {
                        IsSelling = NativeMethods.ReadINIBool(_commoditiesSettingsIniFile, i.ToString(), "IsSelling", false),
                        Price = NativeMethods.ReadINIDecimal(_commoditiesSettingsIniFile, i.ToString(), "Price", decimal.Zero),
                        Selling = NativeMethods.ReadINIDecimal(_commoditiesSettingsIniFile, i.ToString(), "Selling", decimal.Zero),
                        StarSystemId = NativeMethods.ReadINIInt(_commoditiesSettingsIniFile, i.ToString(), "system_index", 0),
                        StationId = NativeMethods.ReadINIInt(_commoditiesSettingsIniFile, i.ToString(), "station_index", 0),
                        CommoditiesId = NativeMethods.ReadINIInt(_commoditiesSettingsIniFile, i.ToString(), "commodities_index", 0),
                        Qty = NativeMethods.ReadINIInt(_commoditiesSettingsIniFile, i.ToString(), "qty", 0),
                        StationPriceID = i
                    });
                }
            }
            if (System.IO.File.Exists(_commoditiesIniFile)) {
                for (var i = 1; i < NativeMethods.ReadINIInt(_commoditiesIniFile, "Settings", "Count", 0) + 1; ++i) {
                    CommoditiesModels.Add(new CommodityModel() {
                        CommodityId = i,
                        Description = NativeMethods.ReadINI(_commoditiesIniFile, i.ToString(), "Description", ""),
                        Body = NativeMethods.ReadINI(_commoditiesIniFile, i.ToString(), "Body", ""),
                        Prices = StationPriceModels.Where(p => p.CommoditiesId == i).ToList()
                    });
                }
            }
        }
        #endregion
    }
}