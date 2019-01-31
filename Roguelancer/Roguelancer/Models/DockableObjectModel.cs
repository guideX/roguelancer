using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Roguelancer.Interfaces;
using System;
using System.Collections.Generic;
namespace Roguelancer.Models {
    /// <summary>
    /// Dockable Object Model
    /// </summary>
    public class DockableObjectModel {
        /// <summary>
        /// Destination Rectangle
        /// </summary>
        public Rectangle DestinationRectangle { get; set; }
        /// <summary>
        /// Background Texture
        /// </summary>
        public Texture2D BackgroundTexture { get; set; }
        /// <summary>
        /// Guid
        /// </summary>
        public string ID { get; set; }
        /// <summary>
        /// Docked Ships
        /// </summary>
        public List<ISensorObject> DockedShips { get; set; }
        /// <summary>
        /// Commodities
        /// </summary>
        public List<StationPriceModel> StationPrices { get; set; }
        /// <summary>
        /// Dockable Object Model
        /// </summary>
        public DockableObjectModel() {
            ID = Guid.NewGuid().ToString(); // Create new ID
            DockedShips = new List<ISensorObject>();
        }
    }
}