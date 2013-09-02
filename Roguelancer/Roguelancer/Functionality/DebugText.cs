// Roguelancer 0.1 Pre Alpha by Leon Aiossa
// http://www.team-nexgen.org
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using Roguelancer.Interfaces;
namespace Roguelancer.Functionality {
    public class DebugText : IGame {
        private SpriteBatch spriteBatch;
        public String text;
        private SpriteFont font;
        private Vector2 fontPosition;
        public void Initialize(RoguelancerGame _Game) {

        }
        public void LoadContent(RoguelancerGame _Game) {
            font = _Game.Content.Load<SpriteFont>("FONTS\\" + _Game.settings.font);
            fontPosition = new Vector2(_Game.graphics.graphicsDeviceManager.GraphicsDevice.Viewport.Width / 2, _Game.graphics.graphicsDeviceManager.GraphicsDevice.Viewport.Height / 2);
        }
        public void Update(RoguelancerGame _Game) {
            spriteBatch = _Game.graphics.lSpriteBatch;
        }
        public void Draw(RoguelancerGame _Game) {
            Vector2 _FontOrigin = font.MeasureString(text) / 2;
            spriteBatch.DrawString(font, text, fontPosition, Color.White, 0, _FontOrigin, 3.0f, SpriteEffects.None, 0.5f);
        }
    }
}