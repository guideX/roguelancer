// Roguelancer 0.1 Pre Alpha by Leon Aiossa
// http://www.team-nexgen.com
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Roguelancer.Interfaces;
namespace Roguelancer.Functionality {
    /// <summary>
    /// Game Graphics
    /// </summary>
    public class GameGraphics : IGameGraphics {
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
            GraphicsDeviceManager = new GraphicsDeviceManager(game);
            GraphicsDeviceManager.PreferredBackBufferWidth = (int)game.Settings.Resolution.X;
            GraphicsDeviceManager.PreferredBackBufferHeight = (int)game.Settings.Resolution.Y;
            GraphicsDeviceManager.IsFullScreen = game.Settings.FullScreen;
        }
        /// <summary>
        /// Initialize
        /// </summary>
        /// <param name="game"></param>
        public void Initialize(RoguelancerGame game) { }
        /// <summary>
        /// Load Content
        /// </summary>
        /// <param name="game"></param>
        public void LoadContent(RoguelancerGame game) {
            SpriteBatch = new SpriteBatch(GraphicsDeviceManager.GraphicsDevice);
        }
        /// <summary>
        /// Update
        /// </summary>
        /// <param name="game"></param>
        public void Update(RoguelancerGame game) { }
        /// <summary>
        /// Draw
        /// </summary>
        /// <param name="game"></param>
        public void Draw(RoguelancerGame game) {
            GraphicsDeviceManager.GraphicsDevice.DepthStencilState = DepthStencilState.Default;
        }
        #endregion
    }
}