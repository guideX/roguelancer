// Roguelancer 0.1 Pre Alpha by Leon Aiossa
// http://www.team-nexgen.org
using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Roguelancer.Interfaces;
namespace Roguelancer.Functionality {
    /// <summary>
    /// Debug Text
    /// </summary>
    public class DebugText : IDebugText {
        #region "public variables"
        /// <summary>
        /// Text
        /// </summary>
        public string Text { get; set; }
        #endregion
        #region "private variables"
        /// <summary>
        /// Font
        /// </summary>
        private SpriteFont _font;
        /// <summary>
        /// Font Position
        /// </summary>
        private Vector2 _fontPosition;
        /// <summary>
        /// Initialize
        /// </summary>
        /// <param name="game"></param>
        #endregion
        #region "public functions"
        public void Initialize(RoguelancerGame game) {}
        /// <summary>
        /// Load Content
        /// </summary>
        /// <param name="game"></param>
        public void LoadContent(RoguelancerGame game) {
            try {
                _font = game.Content.Load<SpriteFont>("FONTS\\" + game.Settings.Font);
                _fontPosition = new Vector2(game.Graphics.GraphicsDeviceManager.GraphicsDevice.Viewport.Width / 2, game.Graphics.GraphicsDeviceManager.GraphicsDevice.Viewport.Height / 2);
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
                var fontOrigin = _font.MeasureString(Text) / 2;
                game.Graphics.SpriteBatch.DrawString(_font, Text, _fontPosition, Color.White, 0, fontOrigin, 3.0f, SpriteEffects.None, 0.5f);
            } catch {
                throw;
            }
        }
        #endregion
    }
}