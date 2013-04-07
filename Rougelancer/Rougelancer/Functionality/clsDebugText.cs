// Rougelancer 0.1 Pre Alpha by Leon Aiossa
// http://www.team-nexgen.org
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using Rougelancer.Interfaces;
namespace Rougelancer.Functionality {
    public class clsDebugText : intGame {
        private SpriteBatch lSpriteBatch;
        public String lText = "Rougelancer";
        private SpriteFont lFont;
        private Vector2 lFontPosition;
        public void Initialize(clsGame _Game) {

        }
        public void LoadContent(clsGame _Game) {
            lFont = _Game.Content.Load<SpriteFont>("FONTS\\" + _Game.lSettings.lFont);
            lFontPosition = new Vector2(_Game.lGraphics.lGDM.GraphicsDevice.Viewport.Width / 2, _Game.lGraphics.lGDM.GraphicsDevice.Viewport.Height / 2);
            // lDebugText.LoadContent("LucidaFont", lGraphics.lGDM.GraphicsDevice.Viewport, Content);
        }
        public void Update(clsGame _Game) {
            lSpriteBatch = _Game.lGraphics.lSpriteBatch;
            /*if (_Text == null) {
            } else {
                if (_Text.Length == 0) {
                } else {
                    lText = _Text;
                }
            }*/
        }
        public void Draw(clsGame _Game) {
            Vector2 _FontOrigin = lFont.MeasureString(lText) / 2;
            lSpriteBatch.DrawString(lFont, lText, lFontPosition, Color.White, 0, _FontOrigin, 3.0f, SpriteEffects.None, 0.5f);
        }
    }
}