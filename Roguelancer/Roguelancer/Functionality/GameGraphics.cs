using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Roguelancer.Interfaces;
using Roguelancer.Models;
namespace Roguelancer.Functionality {
    /// <summary>
    /// Game Graphics
    /// </summary>
    public class GameGraphics : IGameGraphics {
        #region "public properties"
        /// <summary>
        /// Game Graphics Model
        /// </summary>
        public GameGraphicsModel Model { get; set; }
        #endregion
        #region "public methods"
        /// <summary>
        /// Game Graphics
        /// </summary>
        /// <param name="game"></param>
        public GameGraphics(RoguelancerGame game) {
            Model = new GameGraphicsModel() {
                GraphicsDeviceManager = new GraphicsDeviceManager(game) {
                    PreferredBackBufferWidth = (int)game.Settings.Model.Resolution.X,
                    PreferredBackBufferHeight = (int)game.Settings.Model.Resolution.Y,
                    IsFullScreen = game.Settings.Model.FullScreen
                }
            };
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