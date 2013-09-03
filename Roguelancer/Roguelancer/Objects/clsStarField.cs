// Rougelancer 0.1 Pre Alpha by Leon Aiossa
// http://www.team-nexgen.org
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Content;
using Roguelancer.Functionality;
namespace Rougelancer.Objects {
    public class clsStar {
        public Vector2 lPosition;
        public float lSpeed;
        public Color lColor;
    }
    public class clsStarField {
        SpriteBatch lSpriteBatch;
        clsStar[] lStars1;
        clsStar[] lStars2;
        //clsStar[] lStars3;
        //clsStar[] lStars4;
        System.Random lRandom;
        Texture2D lStarTexture;
        public void Init(RoguelancerGame game) {
            lStars1 = new clsStar[128];
            lStars2 = new clsStar[128];
            //lStars3 = new clsStar[128];
            //lStars4 = new clsStar[128];
            lRandom = new System.Random();
            for (int i = 0; i < lStars1.Length; i++) {
                clsStar _Star = new clsStar();
                _Star.lColor = new Color(lRandom.Next(256), lRandom.Next(256), lRandom.Next(256), 128);
                _Star.lPosition = new Vector2(lRandom.Next(game.Window.ClientBounds.Width), lRandom.Next(game.Window.ClientBounds.Height));
                _Star.lSpeed = (float)lRandom.NextDouble() * 5 + 2;
                lStars1[i] = _Star;
            }
            for(int i = 0; i < lStars2.Length; i++) {
                clsStar _Star = new clsStar();
                _Star.lColor = new Color(lRandom.Next(256), lRandom.Next(256), lRandom.Next(256), 128);
                _Star.lPosition = new Vector2(lRandom.Next(game.Window.ClientBounds.Width), lRandom.Next(game.Window.ClientBounds.Height));
                _Star.lSpeed = (float)lRandom.NextDouble() * 5 + 2;
                lStars2[i] = _Star;
            }
        }
        public void LoadContent(RoguelancerGame game) {
            lStarTexture = game.Content.Load<Texture2D>("TEXTURES\\STAR");
            lSpriteBatch = game.graphics.lSpriteBatch;
        }
        public void Update(RoguelancerGame game) {
            int _Height1 = game.Window.ClientBounds.Height;
            //var screenCenter = new Vector2(game.GraphicsDevice.Viewport.Bounds.Width / 2, game.GraphicsDevice.Viewport.Bounds.Height / 2);
            for (int i = 0; i < lStars1.Length; i++) {
                var _Star = lStars1[i];
                if ((_Star.lPosition.Y += _Star.lSpeed) > _Height1) {
                    _Star.lPosition = new Vector2(lRandom.Next(game.Window.ClientBounds.Width), +lRandom.Next(20));
                    _Star.lSpeed = (float)lRandom.NextDouble() * 5 + 2;
                    _Star.lColor = new Color(lRandom.Next(256), lRandom.Next(256), lRandom.Next(256), 128);
                }
            }
            for(int i = 0; i < lStars2.Length; i++) {
                var _Star = lStars2[i];
                if((_Star.lPosition.Y -= _Star.lSpeed) <= 0) {
                    _Star.lPosition = new Vector2(lRandom.Next(game.Window.ClientBounds.Width), lRandom.Next(20));
                    _Star.lSpeed = (float)lRandom.NextDouble() * 5 + 2;
                    _Star.lColor = new Color(lRandom.Next(256), lRandom.Next(256), lRandom.Next(256), 128);
                }
            }
        }
        public void Draw(RoguelancerGame game) {
            foreach (var _Star in lStars1) {
                lSpriteBatch.Draw(lStarTexture, _Star.lPosition, null, _Star.lColor, 0, Vector2.Zero, 1.0f, SpriteEffects.None, 1.0f);
            }
            foreach(var _Star in lStars2) {
                lSpriteBatch.Draw(lStarTexture, _Star.lPosition, null, _Star.lColor, 0, Vector2.Zero, 1.0f, SpriteEffects.None, 1.0f);
            }
        }
    }
}