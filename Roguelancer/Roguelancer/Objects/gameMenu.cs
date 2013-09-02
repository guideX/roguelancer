using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Xna.Framework.Graphics;
using Roguelancer.Interfaces;
using Roguelancer.Functionality;
using Microsoft.Xna.Framework;
namespace Roguelancer.Objects {
    public class GameMenu : IGame {
        private Texture2D backgroundTexture;
        private int screenHeight;
        private int screenWidth;
        private Rectangle screenRectangle;
        public GameMenu() {
        }
        public void Initialize(RoguelancerGame _Game) {}
        public void LoadContent(RoguelancerGame _Game) {
            backgroundTexture = _Game.Content.Load<Texture2D>(_Game.settings.menuBackgroundTexture);
            screenWidth = _Game.graphics.graphicsDeviceManager.GraphicsDevice.PresentationParameters.BackBufferWidth;
            screenHeight = _Game.graphics.graphicsDeviceManager.GraphicsDevice.PresentationParameters.BackBufferHeight;

        }
        public void Update(RoguelancerGame _Game) {

        }
        public void Draw(RoguelancerGame _Game) {
            screenRectangle = new Rectangle(0, 0, screenWidth, screenHeight);
            _Game.graphics.lSpriteBatch.Draw(backgroundTexture, screenRectangle, Color.White);
        }
    }
}
