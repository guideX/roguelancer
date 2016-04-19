// Roguelancer 0.1 Pre Alpha by Leon Aiossa
// http://www.team-nexgen.com
using Microsoft.Xna.Framework;
using Roguelancer.Models;
using Roguelancer.Settings;
using System.Collections.Generic;
namespace Roguelancer.Interfaces {
    /// <summary>
    /// Game Settings
    /// </summary>
    public interface IGameSettings {
        #region "public variables"

        /// <summary>
        /// Commodities Models
        /// </summary>
        List<CommodityModel> CommoditiesModels { get; set; }
        /// <summary>
        /// Station Price models
        /// </summary>
        List<StationPriceModel> StationPriceModels { get; set; }
        /// <summary>
        /// Sensor Texture
        /// </summary>
        string SensorTexture { get; set; }
        /// <summary>
        /// Menu Background Texture
        /// </summary>
        string MenuBackgroundTexture { get; set; }
        /// <summary>
        /// Menu Text
        /// </summary>
        string MenuText { get; set; }
        /// <summary>
        /// Camera Settings
        /// </summary>
        CameraSettings CameraSettings { get; set; }
        /// <summary>
        /// Font
        /// </summary>
        string Font { get; set; }
        /// <summary>
        /// Font Small
        /// </summary>
        string FontSmall { get; set; }
        /// <summary>
        /// Resolution
        /// </summary>
        Vector2 Resolution { get; set; }
        /// <summary>
        /// Model Settings
        /// </summary>
        List<SettingsModelObject> ModelSettings { get; set; }
        /// <summary>
        /// Star System Settings
        /// </summary>
        List<StarSystemSettings> StarSystemSettings { get; set; }
        /// <summary>
        /// Bloom Enabled
        /// </summary>
        bool BloomEnabled { get; set; }
        #endregion
    }
}