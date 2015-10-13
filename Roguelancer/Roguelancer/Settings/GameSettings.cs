using Roguelancer.Functionality;
using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
namespace Roguelancer.Settings {
    public enum ModelType { // THE TYPE OF MODEL
        Ship = 1,
        Planet = 2,
        Station = 3,
        TradeLanes = 4, 
        Bullet = 5
    }
    public class GameSettings {
        //public string BulletTexture { get; set; }
        public string menuBackgroundTexture;
        public string menuText;
        public CameraSettings cameraSettings;
        public string groundTexture = "";
        public string font = "LucidaFont";
        public Vector2 resolution = new Vector2(1280, 1024);
        public List<SettingsModelObject> modelSettings { get; set; }
        public List<StarSystemSettings> starSystemSettings { get; set; }
        private string appPath;
        private string directoryCorrection;
        private string gameSettingsIniFile;
        private string modelSettingsIniFile;
        private string systemsSettingsIniFile;
        private string playerIniFile;
        private string systemIniStartPath;
        private string cameraSettingsIniFile;
        private string bulletSettingsIniFile;
        public GameSettings() {
            try {
                LoadSettings();
                cameraSettings = new CameraSettings(cameraSettingsIniFile);
                LoadObjects();
                LoadStarSystem();
            } catch (Exception ex) {
                throw ex;
            }
        }
        //public void Initialize(RoguelancerGame game) { }
        //private void LoadContent(RoguelancerGame game) { }
        //public void Update(RoguelancerGame game) { }
        //public void Draw(RoguelancerGame game) { }
        private void LoadSettings() {
            appPath = System.IO.Directory.GetCurrentDirectory();
            directoryCorrection = @"\..\..\..\";
            gameSettingsIniFile = appPath + directoryCorrection + @"configuration\settings\settings.ini";
            modelSettingsIniFile = appPath + directoryCorrection + @"configuration\models.ini";
            systemsSettingsIniFile = appPath + directoryCorrection + @"configuration\systems\systems.ini";
            playerIniFile = appPath + directoryCorrection + @"configuration\player\settings.ini";
            bulletSettingsIniFile = appPath + directoryCorrection + @"configuration\objects\bullets.ini";
            cameraSettingsIniFile = appPath + directoryCorrection + @"configuration\camera.ini";
            systemIniStartPath = appPath + directoryCorrection + @"configuration\systems\";
            menuBackgroundTexture = IniFile.ReadINI(gameSettingsIniFile, "Settings", "menu_background");
            //BulletTexture = IniFile.ReadINI(gameSettingsIniFile, "Settings", "bullet_texture");
            //BulletTexture = @"Earth";
            menuText = "Roguelancer" + Environment.NewLine + Environment.NewLine + "10 = Play Game" + Environment.NewLine + "F9 = Return to menu" + Environment.NewLine + "ESC = Quit";
        }
        private void LoadObjects() {
            modelSettings = new List<SettingsModelObject>();
            int count = Convert.ToInt32(IniFile.ReadINI(modelSettingsIniFile, "settings", "count", "0"));
            for(int i = 1; i < count + 1; ++i) {
                modelSettings.Add(new SettingsModelObject(
                    IniFile.ReadINI(modelSettingsIniFile, i.ToString().Trim(), "path"),
                    //IniFile.ReadINIVector3(modelSettingsIniFile, i.ToString(), "model_scaling_x", "model_scaling_y", "model_scaling_z"),
                    (ModelType)Convert.ToInt32(IniFile.ReadINI(modelSettingsIniFile, i.ToString().Trim(), "type", "0")),
                    Convert.ToBoolean(IniFile.ReadINI(modelSettingsIniFile, i.ToString().Trim(), "enabled", "false")),
                    i
                ));
            }
        }
        private ModelWorldObjects LoadPlayer() {
            return ModelWorldObjects.Read(modelSettings, playerIniFile, "settings");
        }
        public void LoadStarSystem() {
            starSystemSettings = new List<StarSystemSettings>();
            int systemCount = Convert.ToInt32(IniFile.ReadINI(systemsSettingsIniFile, "settings", "count", "0"));
            for(int i = 1; i < systemCount + 1; ++i) {
                starSystemSettings.Add(new StarSystemSettings(
                    i,
                    IniFile.ReadINI(systemsSettingsIniFile, i.ToString().Trim(), "path", ""),
                    systemIniStartPath,
                    modelSettings,
                    LoadPlayer(),
                    new StarSettings(
                        Convert.ToBoolean(IniFile.ReadINI(systemsSettingsIniFile, i.ToString(), "starsEnabled", "")),
                        Convert.ToInt32(IniFile.ReadINI(systemsSettingsIniFile, i.ToString(), "amountOfStarsPerSheet", "0")),
                        Convert.ToInt32(IniFile.ReadINI(systemsSettingsIniFile, i.ToString(), "maxPositionX", "0")),
                        Convert.ToInt32(IniFile.ReadINI(systemsSettingsIniFile, i.ToString(), "maxPositionY", "0")),
                        Convert.ToInt32(IniFile.ReadINI(systemsSettingsIniFile, i.ToString(), "maxSize", "0")),
                        Convert.ToInt32(IniFile.ReadINI(systemsSettingsIniFile, i.ToString(), "maxPositionIncrementY", "0")),
                        Convert.ToInt32(IniFile.ReadINI(systemsSettingsIniFile, i.ToString(), "maxPositionStartingY", "0")),
                        Convert.ToInt32(IniFile.ReadINI(systemsSettingsIniFile, i.ToString(), "numberOfStarSheets", "0"))
                    )
                ));
            }
        }
    }
}