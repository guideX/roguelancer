// Rougelancer 0.1 Pre Alpha by Leon Aiossa
// http://www.team-nexgen.org
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
namespace Rougelancer.Functionality {
    public class clsDebugText {
        private SpriteBatch lSpriteBatch;
        private String lText = "Rougelancer";
        private SpriteFont lFont;
        private Vector2 lFontPosition;
        public void LoadContent(string _FontName, Viewport _ViewPort, ContentManager _Content) {
            lFont = _Content.Load<SpriteFont>("FONTS\\" + _FontName);
            lFontPosition = new Vector2(_ViewPort.Width / 2, _ViewPort.Height / 2);
        }
        public void Update(string _Text, SpriteBatch _SpriteBatch) {
            lSpriteBatch = _SpriteBatch;
            if (_Text == null) {
            } else {
                if (_Text.Length == 0) {
                } else {
                    lText = _Text;
                }
            }
        }
        public void Draw() {
            Vector2 _FontOrigin = lFont.MeasureString(lText) / 2;
            lSpriteBatch.DrawString(lFont, lText, lFontPosition, Color.White, 0, _FontOrigin, 3.0f, SpriteEffects.None, 0.5f);
        }
    }
}