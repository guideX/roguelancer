﻿// Rougelancer 0.1 Pre Alpha by Leon Aiossa
// http://www.team-nexgen.org
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
namespace Rougelancer.Functionality {
    public class clsGraphics {
        public GraphicsDeviceManager lGDM;
        public SpriteBatch lSpriteBatch;
        public void Init(Game _Game, int _WindowWidth, int _WindowHeight) {
            lGDM = new GraphicsDeviceManager(_Game);
            lGDM.PreferredBackBufferWidth = _WindowWidth;
            lGDM.PreferredBackBufferHeight = _WindowHeight;
            lGDM.IsFullScreen = false;
        }
        public void LoadContent() {
            lSpriteBatch = new SpriteBatch(lGDM.GraphicsDevice);
        }
        public float ScreenDimensions() {
            return (float)lGDM.GraphicsDevice.Viewport.Width / lGDM.GraphicsDevice.Viewport.Height;
        }
        public int ReturnBackBufferHeight() {
            return lGDM.PreferredBackBufferHeight;
        }
        public int ReturnBackBufferWidth() {
            return lGDM.PreferredBackBufferWidth;
        }
        public void Draw() {
            lGDM.GraphicsDevice.Clear(Color.Black);
            lGDM.GraphicsDevice.DepthStencilState = DepthStencilState.Default;
        }
        public void BeginSpriteBatch() {
            lSpriteBatch.Begin(SpriteSortMode.BackToFront, BlendState.NonPremultiplied, SamplerState.AnisotropicWrap, DepthStencilState.Default, RasterizerState.CullNone);
        }
        public void EndSpriteBatch() {
            lSpriteBatch.End();
        }
    }
}