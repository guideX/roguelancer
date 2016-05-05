// Roguelancer 0.1 Pre Alpha by Leon Aiossa
// http://team-nexgen.com
using System.Linq;
using System.Collections.Generic;
using Roguelancer.Functionality;
using Roguelancer.Interfaces;
using Roguelancer.Models;
using Roguelancer.Models.Settings;
namespace Roguelancer.Settings {
    /// <summary>
    /// Game Settings
    /// </summary>
    public class GameSettings : IGameSettings {
        #region "public variables"
        /// <summary>
        /// Model
        /// </summary>
        public GameSettingsModel Model { get; set; }
        #endregion
        #region "public functions"
        public GameSettings() {
            Model = new GameSettingsModel();
            Model.SensorTexture = NativeMethods.ReadINI(Model.GameSettingsIniFile, "Settings", "SensorTexture");
            var b = false;
            if (bool.TryParse(NativeMethods.ReadINI(Model.GameSettingsIniFile, "Settings", "BloomEnabled"), out b)) { Model.BloomEnabled = b; }
            if (bool.TryParse(NativeMethods.ReadINI(Model.GameSettingsIniFile, "Settings", "FullScreen"), out b)) { Model.FullScreen = b; }
            Model.BulletMass = NativeMethods.ReadINIFloat(Model.GameSettingsIniFile, "Bullet", "Mass", 1.0f);
            Model.BulletThrusterForce = NativeMethods.ReadINIFloat(Model.GameSettingsIniFile, "Bullet", "ThrusterForce", 44000.0f);
            Model.BulletDragFactor = NativeMethods.ReadINIFloat(Model.GameSettingsIniFile, "Bullet", "DragFactor", 0.97f);
            Model.BulletRechargeRate = NativeMethods.ReadINIInt(Model.GameSettingsIniFile, "Bullet", "RechargeRate", 240);
            Model.PlayerShipUpdateDirectionX = NativeMethods.ReadINIFloat(Model.GameSettingsIniFile, "PlayerShip", "UpdateDirectionX", 2.0f);
            Model.PlayerShipUpdateDirectionY = NativeMethods.ReadINIFloat(Model.GameSettingsIniFile, "PlayerShip", "UpdateDirectionY", 2.0f);
            Model.PlayerShipShakeValue = NativeMethods.ReadINIFloat(Model.GameSettingsIniFile, "PlayerShip", "ShakeValue", .8f);
            Model.MenuBackgroundTexture = NativeMethods.ReadINI(Model.GameSettingsIniFile, "Settings", "menu_background");
            Model.CameraSettings = new CameraSettings(Model.CameraSettingsIniFile);
            Model.ModelSettings = new List<SettingsModelObject>();
            for (var i = 1; i < NativeMethods.ReadINIInt(Model.ModelSettingsIniFile, "settings", "count", 0) + 1; ++i) {
                Model.ModelSettings.Add(new SettingsModelObject(
                    NativeMethods.ReadINI(Model.ModelSettingsIniFile, i.ToString().Trim(), "path"),
                    (Enum.ModelType)NativeMethods.ReadINIInt(Model.ModelSettingsIniFile, i.ToString().Trim(), "type", 0),
                    NativeMethods.ReadINIBool(Model.ModelSettingsIniFile, i.ToString().Trim(), "enabled", false),
                    i
                ));
            }
            for (var i = 1; i < NativeMethods.ReadINIInt(Model.SystemsSettingsIniFile, "settings", "count", 0) + 1; ++i) {
                Model.StarSystemSettings.Add(new StarSystemSettings(
                    i,
                    NativeMethods.ReadINI(Model.SystemsSettingsIniFile, i.ToString().Trim(), "path", ""),
                    Model.SystemIniStartPath,
                    Model.ModelSettings,
                    ModelWorldObjects.Read(i, Model.ModelSettings, Model.PlayerIniFile, "settings"),
                    new StarSettings(
                        NativeMethods.ReadINIBool(Model.SystemsSettingsIniFile, i.ToString(), "starsEnabled", false),
                        NativeMethods.ReadINIInt(Model.SystemsSettingsIniFile, i.ToString(), "amountOfStarsPerSheet", 0),
                        NativeMethods.ReadINIInt(Model.SystemsSettingsIniFile, i.ToString(), "maxPositionX", 0),
                        NativeMethods.ReadINIInt(Model.SystemsSettingsIniFile, i.ToString(), "maxPositionY", 0),
                        NativeMethods.ReadINIInt(Model.SystemsSettingsIniFile, i.ToString(), "maxSize", 0),
                        NativeMethods.ReadINIInt(Model.SystemsSettingsIniFile, i.ToString(), "maxPositionIncrementY", 0),
                        NativeMethods.ReadINIInt(Model.SystemsSettingsIniFile, i.ToString(), "maxPositionStartingY", 0),
                        NativeMethods.ReadINIInt(Model.SystemsSettingsIniFile, i.ToString(), "numberOfStarSheets", 0)
                    )
                ));
            }
            if (System.IO.File.Exists(Model.CommoditiesSettingsIniFile)) {
                for (var i = 1; i < NativeMethods.ReadINIInt(Model.CommoditiesSettingsIniFile, "Settings", "Count", 0) + 1; ++i) {
                    Model.StationPriceModels.Add(new StationPriceModel() {
                        IsSelling = NativeMethods.ReadINIBool(Model.CommoditiesSettingsIniFile, i.ToString(), "IsSelling", false),
                        Price = NativeMethods.ReadINIDecimal(Model.CommoditiesSettingsIniFile, i.ToString(), "Price", decimal.Zero),
                        Selling = NativeMethods.ReadINIDecimal(Model.CommoditiesSettingsIniFile, i.ToString(), "Selling", decimal.Zero),
                        StarSystemId = NativeMethods.ReadINIInt(Model.CommoditiesSettingsIniFile, i.ToString(), "system_index", 0),
                        StationId = NativeMethods.ReadINIInt(Model.CommoditiesSettingsIniFile, i.ToString(), "station_index", 0),
                        CommoditiesId = NativeMethods.ReadINIInt(Model.CommoditiesSettingsIniFile, i.ToString(), "commodities_index", 0),
                        Qty = NativeMethods.ReadINIInt(Model.CommoditiesSettingsIniFile, i.ToString(), "qty", 0),
                        StationPriceID = i
                    });
                }
            }
            if (System.IO.File.Exists(Model.CommoditiesIniFile)) {
                for (var i = 1; i < NativeMethods.ReadINIInt(Model.CommoditiesIniFile, "Settings", "Count", 0) + 1; ++i) {
                    Model.CommoditiesModels.Add(new CommodityModel() {
                        CommodityId = i,
                        Description = NativeMethods.ReadINI(Model.CommoditiesIniFile, i.ToString(), "Description", ""),
                        Body = NativeMethods.ReadINI(Model.CommoditiesIniFile, i.ToString(), "Body", ""),
                        Prices = Model.StationPriceModels.Where(p => p.CommoditiesId == i).ToList()
                    });
                }
            }
        }
        #endregion
    }
}