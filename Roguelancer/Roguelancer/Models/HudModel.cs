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
    /// Hud Object Common
    /// </summary>
    public class HudObjectCommon {

    }
    /// <summary>
    /// Hud Object Ship
    /// </summary>
    public class HudSensorObject {
        /// <summary>
        /// Common
        /// </summary>
        //public HudObjectCommon Common { get; set; }

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
    /// <summary>
    /// Hud Object Station
    /// </summary>
    //public class HudObjectStation {
        /// <summary>
        /// Common
        /// </summary>
        //public HudObjectCommon Common { get; set; }
        /// <summary>
        /// Station
        /// </summary>
        //public ISensorObject Station { get; set; }
    //}
    /// <summary>
    /// Hud Object Docking Ring
    /// </summary>
    //public class HudObjectTradelaneRing {
        /// <summary>
        /// Common
        /// </summary>
        //public HudObjectCommon Common { get; set; }
        /// <summary>
        /// Trade Lane
        /// </summary>
        //public TradeLane Tradelane { get; set; }
    //}
}