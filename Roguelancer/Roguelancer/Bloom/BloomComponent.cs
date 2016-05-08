// Roguelancer 0.1 Pre Alpha by Leon Aiossa
// http://team-nexgen.com
using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Roguelancer.Enum;
using Roguelancer.Models.Bloom;
namespace Roguelancer.Bloom {
    /// <summary>
    /// Bloom Component
    /// </summary>
    public class BloomComponent : DrawableGameComponent {
        #region "public properties"
        /// <summary>
        /// Bloom Component Model
        /// </summary>
        public BloomComponentModel Model { get; set; }
        #endregion
        #region "public methods"
        /// <summary>
        /// Bloom Component
        /// </summary>
        /// <param name="game"></param>
        public BloomComponent(Game game) : base(game) {
            Model = new BloomComponentModel();
            Model.ShowBuffer = IntermediateBuffer.iFinalResult;
            if (game == null) {
                throw new ArgumentNullException("game");
            }
        }
        /// <summary>
        /// Load Content
        /// </summary>
        protected override void LoadContent() {
            Model.SpriteBatch = new SpriteBatch(GraphicsDevice);
            Model.BloomExtractEffect = Game.Content.Load<Effect>(@"Effects\BloomExtract");
            Model.BloomCombineEffect = Game.Content.Load<Effect>(@"Effects\BloomCombine");
            Model.GaussianBlurEffect = Game.Content.Load<Effect>(@"Effects\GaussianBlur");
            var pp = GraphicsDevice.PresentationParameters;
            var width = pp.BackBufferWidth;
            var height = pp.BackBufferHeight;
            var format = pp.BackBufferFormat;
            Model.SceneRenderTarget = new RenderTarget2D(GraphicsDevice, width, height, false, format, pp.DepthStencilFormat, pp.MultiSampleCount, RenderTargetUsage.DiscardContents);
            width /= 2;
            height /= 2;
            Model.RenderTarget1 = new RenderTarget2D(GraphicsDevice, width, height, false, format, DepthFormat.None);
            Model.RenderTarget2 = new RenderTarget2D(GraphicsDevice, width, height, false, format, DepthFormat.None);
        }
        /// <summary>
        /// Unload Content
        /// </summary>
        protected override void UnloadContent() {
            Model.SceneRenderTarget.Dispose();
            Model.RenderTarget1.Dispose();
            Model.RenderTarget2.Dispose();
        }
        /// <summary>
        /// Begin Draw
        /// </summary>
        public void BeginDraw() {
            if (Visible) {
                GraphicsDevice.SetRenderTarget(Model.SceneRenderTarget);
            }
        }
        /// <summary>
        /// Draw
        /// </summary>
        /// <param name="gameTime"></param>
        public override void Draw(GameTime gameTime) {
            GraphicsDevice.SamplerStates[1] = SamplerState.LinearClamp;
            Model.BloomExtractEffect.Parameters["BloomThreshold"].SetValue(Model.Settings.BloomThreshold);
            DrawFullscreenQuad(Model.SceneRenderTarget, Model.RenderTarget1, Model.BloomExtractEffect, IntermediateBuffer.iPreBloom);
            SetBlurEffectParameters(1.0f / (float)Model.RenderTarget1.Width, 0);
            DrawFullscreenQuad(Model.RenderTarget1, Model.RenderTarget2, Model.GaussianBlurEffect, IntermediateBuffer.iBlurredHorizontally);
            SetBlurEffectParameters(0, 1.0f / (float)Model.RenderTarget1.Height);
            DrawFullscreenQuad(Model.RenderTarget2, Model.RenderTarget1, Model.GaussianBlurEffect, IntermediateBuffer.iBlurredBothWays);
            GraphicsDevice.SetRenderTarget(null);
            var parameters = Model.BloomCombineEffect.Parameters;
            parameters["BloomIntensity"].SetValue(Model.Settings.BloomIntensity);
            parameters["BaseIntensity"].SetValue(Model.Settings.BaseIntensity);
            parameters["BloomSaturation"].SetValue(Model.Settings.BloomSaturation);
            parameters["BaseSaturation"].SetValue(Model.Settings.BaseSaturation);
            GraphicsDevice.Textures[1] = Model.SceneRenderTarget;
            var viewport = GraphicsDevice.Viewport;
            DrawFullscreenQuad(Model.RenderTarget1, viewport.Width, viewport.Height, Model.BloomCombineEffect, IntermediateBuffer.iFinalResult);
        }
        #endregion
        #region "private methods"
        /// <summary>
        /// Draw Full Screen Quad
        /// </summary>
        /// <param name="texture"></param>
        /// <param name="renderTarget"></param>
        /// <param name="effect"></param>
        /// <param name="currentBuffer"></param>
        private void DrawFullscreenQuad(Texture2D texture, RenderTarget2D renderTarget, Effect effect, IntermediateBuffer currentBuffer) {
            GraphicsDevice.SetRenderTarget(renderTarget);
            DrawFullscreenQuad(texture, renderTarget.Width, renderTarget.Height, effect, currentBuffer);
        }
        /// <summary>
        /// Draw Full Screen Quad
        /// </summary>
        /// <param name="texture"></param>
        /// <param name="width"></param>
        /// <param name="height"></param>
        /// <param name="effect"></param>
        /// <param name="currentBuffer"></param>
        private void DrawFullscreenQuad(Texture2D texture, int width, int height, Effect effect, IntermediateBuffer currentBuffer) {
            if (Model.ShowBuffer < currentBuffer) {
                effect = null;
            }
            Model.SpriteBatch.Begin(0, BlendState.Opaque, null, null, null, effect);
            Model.SpriteBatch.Draw(texture, new Rectangle(0, 0, width, height), Color.White);
            Model.SpriteBatch.End();
        }
        /// <summary>
        /// Set Blur Effect Parameters
        /// </summary>
        /// <param name="dx"></param>
        /// <param name="dy"></param>
        private void SetBlurEffectParameters(float dx, float dy) {
            EffectParameter weightsParameter, offsetsParameter;
            weightsParameter = Model.GaussianBlurEffect.Parameters["SampleWeights"];
            offsetsParameter = Model.GaussianBlurEffect.Parameters["SampleOffsets"];
            int sampleCount = weightsParameter.Elements.Count;
            var sampleWeights = new float[sampleCount];
            var sampleOffsets = new Vector2[sampleCount];
            sampleWeights[0] = ComputeGaussian(0);
            sampleOffsets[0] = new Vector2(0);
            var totalWeights = sampleWeights[0];
            for (int i = 0; i < sampleCount / 2; i++) {
                var weight = ComputeGaussian(i + 1);
                sampleWeights[i * 2 + 1] = weight;
                sampleWeights[i * 2 + 2] = weight;
                totalWeights += weight * 2;
                var sampleOffset = i * 2 + 1.5f;
                var delta = new Vector2(dx, dy) * sampleOffset;
                sampleOffsets[i * 2 + 1] = delta;
                sampleOffsets[i * 2 + 2] = -delta;
            }
            for (int i = 0; i < sampleWeights.Length; i++) {
                sampleWeights[i] /= totalWeights;
            }
            weightsParameter.SetValue(sampleWeights);
            offsetsParameter.SetValue(sampleOffsets);
        }
        /// <summary>
        /// Compute Gaussian
        /// </summary>
        /// <param name="n"></param>
        /// <returns></returns>
        private float ComputeGaussian(float n) {
            var theta = Model.Settings.BlurAmount;
            return (float)((1.0 / Math.Sqrt(2 * Math.PI * theta)) * Math.Exp(-(n * n) / (2 * theta * theta)));
        }
        #endregion
    }
}