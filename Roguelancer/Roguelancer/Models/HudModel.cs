using Microsoft.Xna.Framework;
using System.Collections.Generic;
using Roguelancer.Interfaces;
namespace Roguelancer.Models {
    /// <summary>
    /// Hud Model
    /// </summary>
    public class HudModel {
        /// <summary>
        /// Ships
        /// </summary>
        public List<HudSensorObject> SensorObjects { get; set; }
        /// <summary>
        /// Hud Model
        /// </summary>
        public HudModel() {
            try {
                SensorObjects = new List<HudSensorObject>();
            } catch {
                throw;
            }
        }
    }
    /// <summary>
    /// Hud Object Ship
    /// </summary>
    public class HudSensorObject {
        /// <summary>
        /// Distance
        /// </summary>
        public double Distance { get; set; }
        /// <summary>
        /// Font Position
        /// </summary>
        public Vector2 FontPosition;
        /// <summary>
        /// Text
        /// </summary>
        public string Text { get; set; }
        /// <summary>
        /// Ship
        /// </summary>
        public ISensorObject Obj { get; set; }
    }
}