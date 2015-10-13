// Roguelancer 0.1 Pre Alpha by Leon Aiossa
// http://www.team-nexgen.org
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Roguelancer.Interfaces;
namespace Roguelancer.Functionality {
    /// <summary>
    /// Game Graphics
    /// </summary>
    public class GameGraphics : IGame {
        #region "public variables"
        /// <summary>
        /// Graphics Device Manager
        /// </summary>
        public GraphicsDeviceManager GraphicsDeviceManager { get; set; }
        /// <summary>
        /// Sprite Batch
        /// </summary>
        public SpriteBatch SpriteBatch { get; set; }
        #endregion
        #region "public functions"
        /// <summary>
        /// Game Graphics
        /// </summary>
        /// <param name="game"></param>
        public GameGraphics(RoguelancerGame game) {
            try {
                GraphicsDeviceManager = new GraphicsDeviceManager(game);
                GraphicsDeviceManager.PreferredBackBufferWidth = (int)game.Settings.resolution.X;
                GraphicsDeviceManager.PreferredBackBufferHeight = (int)game.Settings.resolution.Y;
                GraphicsDeviceManager.IsFullScreen = false;
            } catch {
                throw;
            }
        }
        /// <summary>
        /// Initialize
        /// </summary>
        /// <param name="game"></param>
        public void Initialize(RoguelancerGame game) {}
        /// <summary>
        /// Load Content
        /// </summary>
        /// <param name="game"></param>
        public void LoadContent(RoguelancerGame game) {
            try {
                SpriteBatch = new SpriteBatch(GraphicsDeviceManager.GraphicsDevice);
            } catch {
                throw;
            }
        }
        /// <summary>
        /// Update
        /// </summary>
        /// <param name="game"></param>
        public void Update(RoguelancerGame game) {}
        /// <summary>
        /// Draw
        /// </summary>
        /// <param name="game"></param>
        public void Draw(RoguelancerGame game) {
            try {
                GraphicsDeviceManager.GraphicsDevice.DepthStencilState = DepthStencilState.Default;
            } catch {
                throw;
            }
        }
        #endregion
    }
}