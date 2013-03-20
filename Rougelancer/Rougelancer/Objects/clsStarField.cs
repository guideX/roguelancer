// Rougelancer 0.1 Pre Alpha by Leon Aiossa
// http://www.team-nexgen.org
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Content;
using Rougelancer.Functionality;
namespace Rougelancer.Objects {
    public class clsStar {
        public Vector2 lPosition;
        public float lSpeed;
        public Color lColor;
    }
    public class clsStarField {
        SpriteBatch lSpriteBatch;
        clsStar[] lStars;
        System.Random lRandom;
        Texture2D lStarTexture;
        public void Init(clsGraphics _Graphics, Game _Game) {
            lStars = new clsStar[128];
            lRandom = new System.Random();
            for (int i = 0; i < lStars.Length; i++) {
                clsStar _Star = new clsStar();
                _Star.lColor = new Color(lRandom.Next(256), lRandom.Next(256), lRandom.Next(256), 128);
                _Star.lPosition = new Vector2(lRandom.Next(_Game.Window.ClientBounds.Width), lRandom.Next(_Game.Window.ClientBounds.Height));
                _Star.lSpeed = (float)lRandom.NextDouble() * 5 + 2;
                lStars[i] = _Star;
            }
        }
        public void LoadContent(ContentManager _Content, clsGraphics _Graphics) {
            lStarTexture = _Content.Load<Texture2D>("TEXTURES\\STAR");
            lSpriteBatch = _Graphics.ReturnSpriteBatch();
        }
        public void Update(Game _Game, clsCamera _Camera) {
            int _Height = _Game.Window.ClientBounds.Height;
            for (int i = 0; i < lStars.Length; i++) {
                var _Star = lStars[i];
                if ((_Star.lPosition.Y += _Star.lSpeed) > _Height) {
                    _Star.lPosition = new Vector2(lRandom.Next(_Game.Window.ClientBounds.Width), +lRandom.Next(20));
                    _Star.lSpeed = (float)lRandom.NextDouble() * 5 + 2;
                    _Star.lColor = new Color(lRandom.Next(256), lRandom.Next(256), lRandom.Next(256), 128);
                }
            }
        }
        public void Draw(clsGraphics _Graphics) {
            foreach (var _Star in lStars) {
                lSpriteBatch.Draw(lStarTexture, _Star.lPosition, null, _Star.lColor, 0, Vector2.Zero, 1.0f, SpriteEffects.None, 1.0f);
            }
        }
    }
}
