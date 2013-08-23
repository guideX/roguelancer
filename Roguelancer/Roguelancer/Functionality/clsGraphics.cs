// Roguelancer 0.1 Pre Alpha by Leon Aiossa
// http://www.team-nexgen.org
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Roguelancer.Interfaces;
namespace Roguelancer.Functionality {
    public class clsGraphics : IGame {
        public GraphicsDeviceManager graphicsDeviceManager;
        public SpriteBatch lSpriteBatch;
        public clsGraphics(clsGame _Game) {
            graphicsDeviceManager = new GraphicsDeviceManager(_Game);
        }
        public void Initialize(clsGame _Game) {
            graphicsDeviceManager.PreferredBackBufferWidth = (int)_Game.settings.resolution.X;
            graphicsDeviceManager.PreferredBackBufferHeight = (int)_Game.settings.resolution.Y;
            graphicsDeviceManager.IsFullScreen = true;
        }
        public void LoadContent(clsGame _Game) {
            lSpriteBatch = new SpriteBatch(graphicsDeviceManager.GraphicsDevice);
        }
        public void Update(clsGame _Game) {
        }
        public void Draw(clsGame _Game) {
            graphicsDeviceManager.GraphicsDevice.Clear(Color.Black);
            graphicsDeviceManager.GraphicsDevice.DepthStencilState = DepthStencilState.Default;
        }
        public float ScreenDimensions() {
            return (float)graphicsDeviceManager.GraphicsDevice.Viewport.Width / graphicsDeviceManager.GraphicsDevice.Viewport.Height;
        }
        public int ReturnBackBufferHeight() {
            return graphicsDeviceManager.PreferredBackBufferHeight;
        }
        public int ReturnBackBufferWidth() {
            return graphicsDeviceManager.PreferredBackBufferWidth;
        }
        public void BeginSpriteBatch() {
            lSpriteBatch.Begin(SpriteSortMode.BackToFront, BlendState.NonPremultiplied, SamplerState.AnisotropicWrap, DepthStencilState.Default, RasterizerState.CullNone);
        }
        public void EndSpriteBatch() {
            lSpriteBatch.End();
        }
    }
}