// Roguelancer 0.1 Pre Alpha by Leon Aiossa
// http://team-nexgen.com
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Roguelancer.Objects;
namespace Roguelancer.Models {
    /// <summary>
    /// Hud Object Model
    /// </summary>
    public class HudObjectModel {
        /// <summary>
        /// Player Ship
        /// </summary>
        public Ship PlayerShip { get; set; }
        /// <summary>
        /// Update Order Int
        /// </summary>
        public int UpdateOrderInt { get; set; }
        /// <summary>
        /// Font
        /// </summary>
        public SpriteFont Font { get; set; }
        /// <summary>
        /// Sensor
        /// </summary>
        public Texture2D Sensor { get; set; }
        /// <summary>
        /// Screen Rectangle
        /// </summary>
        public Rectangle ScreenRectangle { get; set; }
        /// <summary>
        /// Model
        /// </summary>
        public HudModel Model;
        /// <summary>
        /// Hud Object Model
        /// </summary>
        public HudObjectModel() {
            Model = new HudModel();
        }
    }
}