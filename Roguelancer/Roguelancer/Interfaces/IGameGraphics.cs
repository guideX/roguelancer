// Roguelancer 0.1 Pre Alpha by Leon Aiossa
// http://www.team-nexgen.com
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
namespace Roguelancer.Interfaces {
    /// <summary>
    /// Game Graphics
    /// </summary>
    public interface IGameGraphics : IGame {
        #region "public variables"
        /// <summary>
        /// Graphics Device Manager
        /// </summary>
        GraphicsDeviceManager GraphicsDeviceManager { get; set; }
        /// <summary>
        /// Sprite Batch
        /// </summary>
        SpriteBatch SpriteBatch { get; set; }
        #endregion
    }
}