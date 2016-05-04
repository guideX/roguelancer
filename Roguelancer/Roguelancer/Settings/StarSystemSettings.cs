// Roguelancer 0.1 Pre Alpha by Leon Aiossa
// http://www.team-nexgen.com
using System.Collections.Generic;
using Roguelancer.Functionality;
namespace Roguelancer.Settings {
    /// <summary>
    /// Star System Settings
    /// </summary>
    public class StarSystemSettings {
        #region "private variables"
        /// <summary>
        /// Path
        /// </summary>
        private string _path { get; set; }
        /// <summary>
        /// Star System ID
        /// </summary>
        private int _starSystemId { get; set; }
        #endregion
        #region "public variables"
        /// <summary>
        /// Ships
        /// </summary>
        public List<ModelWorldObjects> Ships { get; set; }
        /// <summary>
        /// Stations
        /// </summary>
        public List<ModelWorldObjects> Stations { get; set; }
        /// <summary>
        /// Bullets
        /// </summary>
        public List<ModelWorldObjects> Bullets { get; set; }
        /// <summary>
        /// Planets
        /// </summary>
        public List<ModelWorldObjects> Planets { get; set; }
        /// <summary>
        /// Trade Lanes
        /// </summary>
        public List<ModelWorldObjects> TradeLanes { get; set; }
        /// <summary>
        /// Jump Holes
        /// </summary>
        public List<ModelWorldObjects> JumpHoles { get; set; }
        /// <summary>
        /// Star Settings
        /// </summary>
        public StarSettings StarSettings { get; set; }
        #endregion
        /// <summary>
        /// Star System Settings
        /// </summary>
        /// <param name="starSystemId"></param>
        /// <param name="path"></param>
        /// <param name="systemIniStartPath"></param>
        /// <param name="modelSettings"></param>
        /// <param name="player"></param>
        /// <param name="_starSettings"></param>
        public StarSystemSettings(
                int starSystemId,
                string path,
                string systemIniStartPath,
                List<SettingsModelObject> modelSettings,
                ModelWorldObjects player,
                StarSettings _starSettings
            ) {
            Ships = new List<ModelWorldObjects>();
            Planets = new List<ModelWorldObjects>();
            Stations = new List<ModelWorldObjects>();
            TradeLanes = new List<ModelWorldObjects>();
            JumpHoles = new List<ModelWorldObjects>();
            player.SettingsModelObject.isPlayer = true;
            Ships.Add(player);
            for (var i = 1; i < IniFile.ReadINIInt(systemIniStartPath + path + @"\ships.ini", "settings", "count", 0) + 1; ++i) {
                Ships.Add(ModelWorldObjects.Read(i, modelSettings, systemIniStartPath + path + @"\ships.ini", i.ToString().Trim()));
            }
            for (var i = 1; i < IniFile.ReadINIInt(systemIniStartPath + path + @"\stations.ini", "settings", "count", 0) + 1; ++i) {
                Stations.Add(ModelWorldObjects.Read(i, modelSettings, systemIniStartPath + path + @"\stations.ini", i.ToString().Trim()));
            }
            for (var i = 1; i < IniFile.ReadINIInt(systemIniStartPath + path + @"\bullets.ini", "settings", "count", 0) + 1; ++i) {
                Bullets.Add(ModelWorldObjects.Read(i, modelSettings, systemIniStartPath + path + @"\bullets.ini", i.ToString().Trim()));
            }
            for (var i = 1; i < IniFile.ReadINIInt(systemIniStartPath + path + @"\planets.ini", "settings", "count", 0) + 1; ++i) {
                Planets.Add(ModelWorldObjects.Read(i, modelSettings, systemIniStartPath + path + @"\planets.ini", i.ToString().Trim()));
            }
            for (var i = 1; i < IniFile.ReadINIInt(systemIniStartPath + path + @"\tradelanes.ini", "settings", "count", 0) + 1; ++i) {
                TradeLanes.Add(ModelWorldObjects.Read(i, modelSettings, systemIniStartPath + path + @"\tradelanes.ini", i.ToString().Trim()));
            }
            for (var i = 1; i < IniFile.ReadINIInt(systemIniStartPath + path + @"\jumpholes.ini", "settings", "count", 0) + 1; ++i) {
                JumpHoles.Add(ModelWorldObjects.Read(i, modelSettings, systemIniStartPath + path + @"\jumpholes.ini", i.ToString().Trim()));
            }
            StarSettings = _starSettings;
        }
    }
}