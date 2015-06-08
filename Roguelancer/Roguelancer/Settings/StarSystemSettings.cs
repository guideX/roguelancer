using System.Collections.Generic;
using Roguelancer.Functionality;
using System;
namespace Roguelancer.Settings {
    public class StarSystemSettings { // SETTINGS FOR EACH STAR SYSTEM
        private string _path { get; set; }
        private int _starSystemId { get; set; }
        public List<ModelWorldObjects> ships { get; set; }
        public List<ModelWorldObjects> stations { get; set; }
        public List<ModelWorldObjects> bullets { get; set; }
        public List<ModelWorldObjects> planets { get; set; }
        public List<ModelWorldObjects> tradeLanes { get; set; }
        public StarSettings starSettings { get; set; }
        public StarSystemSettings(
                int starSystemId,
                string path,
                string systemIniStartPath,
                List<SettingsModelObject> modelSettings,
                ModelWorldObjects player,
                StarSettings _starSettings
            ) {
            ships = new List<ModelWorldObjects>();
            planets = new List<ModelWorldObjects>();
            stations = new List<ModelWorldObjects>();
            player.settingsModelObject.isPlayer = true;
            ships.Add(player);
            for(int i = 1; i < Convert.ToInt32(IniFile.ReadINI(systemIniStartPath + path + @"\ships.ini", "settings", "count", "0")) + 1; ++i) {
                ships.Add(ModelWorldObjects.Read(modelSettings, systemIniStartPath + path + @"\ships.ini", i.ToString().Trim()));
            }
            for(int i = 1; i < Convert.ToInt32(IniFile.ReadINI(systemIniStartPath + path + @"\stations.ini", "settings", "count", "0")) + 1; ++i) {
                stations.Add(ModelWorldObjects.Read(modelSettings, systemIniStartPath + path + @"\stations.ini", i.ToString().Trim()));
            }
            for(int i = 1; i < Convert.ToInt32(IniFile.ReadINI(systemIniStartPath + path + @"\bullets.ini", "settings", "count", "0")) + 1; ++i) {
                stations.Add(ModelWorldObjects.Read(modelSettings, systemIniStartPath + path + @"\bullets.ini", i.ToString().Trim()));
            }
            //game.settings.BulletTexture
            for(int i = 1; i < Convert.ToInt32(IniFile.ReadINI(systemIniStartPath + path + @"\planets.ini", "settings", "count", "0")) + 1; ++i) {
                planets.Add(ModelWorldObjects.Read(modelSettings, systemIniStartPath + path + @"\planets.ini", i.ToString().Trim()));
            }
            starSettings = _starSettings;
        }
    }
}