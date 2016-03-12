// Roguelancer 0.1 Pre Alpha by Leon Aiossa
// http://www.team-nexgen.com
using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Roguelancer.Functionality;
using Roguelancer.Interfaces;
namespace Roguelancer.Settings {
    /// <summary>
    /// Game Settings
    /// </summary>
    public class GameSettings : IGameSettings {
        #region "public variables"
        /// <summary>
        /// Commodities Settings
        /// </summary>
        public CommoditiesSettings CommoditiesSettings { get; set; }
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
        #endregion
        #region "public functions"
        public GameSettings() {
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
            SensorTexture = IniFile.ReadINI(_gameSettingsIniFile, "Settings", "SensorTexture");
            MenuBackgroundTexture = IniFile.ReadINI(_gameSettingsIniFile, "Settings", "menu_background");
            CameraSettings = new CameraSettings(_cameraSettingsIniFile);
            ModelSettings = new List<SettingsModelObject>();
            for (var i = 1; i < IniFile.ReadINIInt(_modelSettingsIniFile, "settings", "count", 0) + 1; ++i) {
                ModelSettings.Add(new SettingsModelObject(
                    IniFile.ReadINI(_modelSettingsIniFile, i.ToString().Trim(), "path"),
                    (Enum.ModelType)IniFile.ReadINIInt(_modelSettingsIniFile, i.ToString().Trim(), "type", 0),
                    IniFile.ReadINIBool(_modelSettingsIniFile, i.ToString().Trim(), "enabled", false),
                    i
                ));
            }
            StarSystemSettings = new List<StarSystemSettings>();
            for (var i = 1; i < IniFile.ReadINIInt(_systemsSettingsIniFile, "settings", "count", 0) + 1; ++i) {
                StarSystemSettings.Add(new StarSystemSettings(
                    i,
                    IniFile.ReadINI(_systemsSettingsIniFile, i.ToString().Trim(), "path", ""),
                    _systemIniStartPath,
                    ModelSettings,
                    ModelWorldObjects.Read(i, ModelSettings, _playerIniFile, "settings"),
                    new StarSettings(
                        IniFile.ReadINIBool(_systemsSettingsIniFile, i.ToString(), "starsEnabled", false),
                        IniFile.ReadINIInt(_systemsSettingsIniFile, i.ToString(), "amountOfStarsPerSheet", 0),
                        IniFile.ReadINIInt(_systemsSettingsIniFile, i.ToString(), "maxPositionX", 0),
                        IniFile.ReadINIInt(_systemsSettingsIniFile, i.ToString(), "maxPositionY", 0),
                        IniFile.ReadINIInt(_systemsSettingsIniFile, i.ToString(), "maxSize", 0),
                        IniFile.ReadINIInt(_systemsSettingsIniFile, i.ToString(), "maxPositionIncrementY", 0),
                        IniFile.ReadINIInt(_systemsSettingsIniFile, i.ToString(), "maxPositionStartingY", 0),
                        IniFile.ReadINIInt(_systemsSettingsIniFile, i.ToString(), "numberOfStarSheets", 0)
                    )
                ));
            }
            var ini = rootDir + @"configuration\commodities.ini";
            CommoditiesSettings = new CommoditiesSettings(rootDir + @"configuration\c.ini", rootDir + @"configuration\cs.ini");
        }
        #endregion
    }
}