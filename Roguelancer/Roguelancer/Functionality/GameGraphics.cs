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
    public class GameGraphics : IGame {
        public GraphicsDeviceManager graphicsDeviceManager;
        public SpriteBatch lSpriteBatch;
        public GameGraphics(RoguelancerGame _Game) {
            graphicsDeviceManager = new GraphicsDeviceManager(_Game);
            graphicsDeviceManager.PreferredBackBufferWidth = (int)_Game.settings.resolution.X;
            graphicsDeviceManager.PreferredBackBufferHeight = (int)_Game.settings.resolution.Y;
            graphicsDeviceManager.IsFullScreen = true;
        }
        public void Initialize(RoguelancerGame _Game) {
        }
        public void LoadContent(RoguelancerGame _Game) {
            lSpriteBatch = new SpriteBatch(graphicsDeviceManager.GraphicsDevice);
        }
        public void Update(RoguelancerGame _Game) {
        }
        public void Draw(RoguelancerGame _Game) {
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