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
    public class DebugText : IGame {
        /// <summary>
        /// Text
        /// </summary>
        public String Text;
        /// <summary>
        /// Font
        /// </summary>
        private SpriteFont Font;
        /// <summary>
        /// Font Position
        /// </summary>
        private Vector2 FontPosition;
        /// <summary>
        /// Initialize
        /// </summary>
        /// <param name="_Game"></param>
        public void Initialize(RoguelancerGame _Game) {}
        /// <summary>
        /// Load Content
        /// </summary>
        /// <param name="_Game"></param>
        public void LoadContent(RoguelancerGame _Game) {
            try {
                Font = _Game.Content.Load<SpriteFont>("FONTS\\" + _Game.Settings.font);
                FontPosition = new Vector2(_Game.Graphics.graphicsDeviceManager.GraphicsDevice.Viewport.Width / 2, _Game.Graphics.graphicsDeviceManager.GraphicsDevice.Viewport.Height / 2);
            } catch {
                throw;
            }
        }
        /// <summary>
        /// Update
        /// </summary>
        /// <param name="_Game"></param>
        public void Update(RoguelancerGame _Game) {}
        /// <summary>
        /// Draw
        /// </summary>
        /// <param name="_Game"></param>
        public void Draw(RoguelancerGame _Game) {
            try {
                var fontOrigin = Font.MeasureString(Text) / 2;
                _Game.Graphics.SpriteBatch.DrawString(Font, Text, FontPosition, Color.White, 0, fontOrigin, 3.0f, SpriteEffects.None, 0.5f);
            } catch {
                throw;
            }
        }
    }
}