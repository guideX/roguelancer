// Roguelancer 0.1 Pre Alpha by Leon Aiossa
// http://www.team-nexgen.org
using Microsoft.Xna.Framework;
using Roguelancer.Settings;
using System.Collections.Generic;
namespace Roguelancer.Interfaces {
    public interface IGameSettings {
        #region "public variables"
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
        #endregion
    }
}