using Microsoft.Xna.Framework;
using System.Collections.Generic;
using Roguelancer.Objects;
namespace Roguelancer.Models {
    /// <summary>
    /// Hud Model
    /// </summary>
    public class HudModel {
        /// <summary>
        /// Ships
        /// </summary>
        public List<HudObjectShip> Ships { get; set; }
        /// <summary>
        /// Stations
        /// </summary>
        //public List<HudObjectStation> Stations { get; set; }
        /// <summary>
        /// Docking Rings
        /// </summary>
        //public List<HudObjectTradelaneRing> Tradelanes { get; set; }
        /// <summary>
        /// Hud Model
        /// </summary>
        public HudModel() {
            try {
                Ships = new List<HudObjectShip>();
                //Stations = new List<HudObjectStation>();
                //Tradelanes = new List<HudObjectTradelaneRing>();
            } catch {
                throw;
            }
        }
    }
    /// <summary>
    /// Hud Object Common
    /// </summary>
    public class HudObjectCommon {
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
    }
    /// <summary>
    /// Hud Object Ship
    /// </summary>
    public class HudObjectShip {
        /// <summary>
        /// Common
        /// </summary>
        public HudObjectCommon Common { get; set; }
        /// <summary>
        /// Ship
        /// </summary>
        public Ship Ship { get; set; }
    }
    /// <summary>
    /// Hud Object Station
    /// </summary>
    public class HudObjectStation {
        /// <summary>
        /// Common
        /// </summary>
        public HudObjectCommon Common { get; set; }
        /// <summary>
        /// Station
        /// </summary>
        public Station Station { get; set; }
    }
    /// <summary>
    /// Hud Object Docking Ring
    /// </summary>
    public class HudObjectTradelaneRing {
        /// <summary>
        /// Common
        /// </summary>
        public HudObjectCommon Common { get; set; }
        /// <summary>
        /// Trade Lane
        /// </summary>
        public TradeLane Tradelane { get; set; }
    }
}