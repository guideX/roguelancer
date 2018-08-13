using System.Collections.Generic;
using Roguelancer.Functionality;
using Roguelancer.Models;
namespace Roguelancer.Settings {
    /// <summary>
    /// Star System Settings
    /// </summary>
    public class StarSystemSettings {
        #region "public properties"
        /// <summary>
        /// Model
        /// </summary>
        public StarSystemSettingsModel Model { get; set; }
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
                List<SettingsObjectModel> modelSettings,
                WorldObjectsSettings player,
                StarSettingsModel _starSettings
            ) {
            Model = new StarSystemSettingsModel() {
                Ships = new List<WorldObjectsSettings>(),
                Planets = new List<WorldObjectsSettings>(),
                Stations = new List<WorldObjectsSettings>(),
                TradeLanes = new List<WorldObjectsSettings>(),
                JumpHoles = new List<WorldObjectsSettings>(),
                DockingRings = new List<WorldObjectsSettings>()
            };
            player.Model.SettingsModelObject.IsPlayer = true;
            Model.Ships.Add(player);
            if (System.IO.File.Exists(systemIniStartPath + path + @"\ships.ini"))
                for (var i = 1; i < NativeMethods.ReadINIInt(systemIniStartPath + path + @"\ships.ini", "settings", "count", 0) + 1; ++i)
                    Model.Ships.Add(WorldObjectsSettings.Read(i, modelSettings, systemIniStartPath + path + @"\ships.ini", i.ToString().Trim()));
            if (System.IO.File.Exists(systemIniStartPath + path + @"\stations.ini"))
                for (var i = 1; i < NativeMethods.ReadINIInt(systemIniStartPath + path + @"\stations.ini", "settings", "count", 0) + 1; ++i)
                    Model.Stations.Add(WorldObjectsSettings.Read(i, modelSettings, systemIniStartPath + path + @"\stations.ini", i.ToString().Trim()));
            if (System.IO.File.Exists(systemIniStartPath + path + @"\planets.ini"))
                for (var i = 1; i < NativeMethods.ReadINIInt(systemIniStartPath + path + @"\planets.ini", "settings", "count", 0) + 1; ++i)
                    Model.Planets.Add(WorldObjectsSettings.Read(i, modelSettings, systemIniStartPath + path + @"\planets.ini", i.ToString().Trim()));
            if (System.IO.File.Exists(systemIniStartPath + path + @"\rings.ini"))
                for (var i = 1; i < NativeMethods.ReadINIInt(systemIniStartPath + path + @"\rings.ini", "settings", "count", 0) + 1; ++i)
                    Model.DockingRings.Add(WorldObjectsSettings.Read(i, modelSettings, systemIniStartPath + path + @"\rings.ini", i.ToString().Trim()));
            if (System.IO.File.Exists(systemIniStartPath + path + @"\tradelanes.ini"))
                for (var i = 1; i < NativeMethods.ReadINIInt(systemIniStartPath + path + @"\tradelanes.ini", "settings", "count", 0) + 1; ++i)
                    Model.TradeLanes.Add(WorldObjectsSettings.Read(i, modelSettings, systemIniStartPath + path + @"\tradelanes.ini", i.ToString().Trim()));
            if (System.IO.File.Exists(systemIniStartPath + path + @"\jumpholes.ini"))
                for (var i = 1; i < NativeMethods.ReadINIInt(systemIniStartPath + path + @"\jumpholes.ini", "settings", "count", 0) + 1; ++i)
                    Model.JumpHoles.Add(WorldObjectsSettings.Read(i, modelSettings, systemIniStartPath + path + @"\jumpholes.ini", i.ToString().Trim()));
            Model.StarSettings = _starSettings;
        }
    }
}

/*
 * EDIT BY LIAM:
 * Tuesday March 06th 2018
fddddddddddddd cfcfcfcfcfcfcfcf

gvb ......................
*/