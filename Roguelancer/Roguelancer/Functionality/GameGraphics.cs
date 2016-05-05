// Roguelancer 0.1 Pre Alpha by Leon Aiossa
// http://team-nexgen.com
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Roguelancer.Interfaces;
using Roguelancer.Models;
namespace Roguelancer.Functionality {
    /// <summary>
    /// Game Graphics
    /// </summary>
    public class GameGraphics : IGameGraphics {
        #region "public variables"
        /// <summary>
        /// Game Graphics Model
        /// </summary>
        public GameGraphicsModel Model { get; set; }
        #endregion
        #region "public functions"
        /// <summary>
        /// Game Graphics
        /// </summary>
        /// <param name="game"></param>
        public GameGraphics(RoguelancerGame game) {
            Model = new GameGraphicsModel();
            Model.GraphicsDeviceManager = new GraphicsDeviceManager(game);
            Model.GraphicsDeviceManager.PreferredBackBufferWidth = (int)game.Settings.Model.Resolution.X;
            Model.GraphicsDeviceManager.PreferredBackBufferHeight = (int)game.Settings.Model.Resolution.Y;
            Model.GraphicsDeviceManager.IsFullScreen = game.Settings.Model.FullScreen;
        }
        /// <summary>
        /// Load Content
        /// </summary>
        /// <param name="game"></param>
        public void LoadContent(RoguelancerGame game) {
            Model.SpriteBatch = new SpriteBatch(Model.GraphicsDeviceManager.GraphicsDevice);
        }
        /// <summary>
        /// Draw
        /// </summary>
        /// <param name="game"></param>
        public void Draw(RoguelancerGame game) {
            Model.GraphicsDeviceManager.GraphicsDevice.DepthStencilState = DepthStencilState.Default;
        }
        /// <summary>
        /// Dispose
        /// </summary>
        /// <param name="game"></param>
        public void Dispose(RoguelancerGame game) {
            Model.SpriteBatch.Dispose();
            Model.SpriteBatch = null;
        }
        #endregion
    }
}