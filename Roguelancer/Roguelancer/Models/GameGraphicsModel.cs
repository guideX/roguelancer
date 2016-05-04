// Roguelancer 0.1 Pre Alpha by Leon Aiossa
// http://www.team-nexgen.com
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
namespace Roguelancer.Models {
    /// <summary>
    /// Game Graphics Model
    /// </summary>
    public class GameGraphicsModel {
        /// <summary>
        /// Graphics Device Manager
        /// </summary>
        public GraphicsDeviceManager GraphicsDeviceManager { get; set; }
        /// <summary>
        /// Sprite Batch
        /// </summary>
        public SpriteBatch SpriteBatch { get; set; }
    }
}