// Roguelancer 0.1 Pre Alpha by Leon Aiossa
// http://www.team-nexgen.com
using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Roguelancer.Enum;
namespace Roguelancer.Bloom {
    /// <summary>
    /// Bloom Component
    /// </summary>
    public class BloomComponent : DrawableGameComponent {
        /// <summary>
        /// Settings
        /// </summary>
        public BloomSettings Settings { get; set; }
        /// <summary>
        /// Show Buffer
        /// </summary>
        public IntermediateBuffer ShowBuffer { get; set; }
        /// <summary>
        /// Sprite Batch
        /// </summary>
        private SpriteBatch _spriteBatch;
        /// <summary>
        /// Bloom Extract Effect
        /// </summary>
        private Effect _bloomExtractEffect;
        /// <summary>
        /// Bloom Combine Effect
        /// </summary>
        private Effect _bloomCombineEffect;
        /// <summary>
        /// Gaussian Blur Effect
        /// </summary>
        private Effect _gaussianBlurEffect;
        /// <summary>
        /// Scene Render Target
        /// </summary>
        private RenderTarget2D _sceneRenderTarget;
        /// <summary>
        /// Render Target 1
        /// </summary>
        private RenderTarget2D _renderTarget1;
        /// <summary>
        /// Render Target 2
        /// </summary>
        private RenderTarget2D _renderTarget2;
        /// <summary>
        /// Bloom Component
        /// </summary>
        /// <param name="game"></param>
        public BloomComponent(Game game) : base(game) {
            ShowBuffer = IntermediateBuffer.iFinalResult;
            if (game == null) {
                throw new ArgumentNullException("game");
            }
        }
        /// <summary>
        /// Load Content
        /// </summary>
        protected override void LoadContent() {
            _spriteBatch = new SpriteBatch(GraphicsDevice);
            _bloomExtractEffect = Game.Content.Load<Effect>("Effects\\BloomExtract");
            _bloomCombineEffect = Game.Content.Load<Effect>("Effects\\BloomCombine");
            _gaussianBlurEffect = Game.Content.Load<Effect>("Effects\\GaussianBlur");
            var pp = GraphicsDevice.PresentationParameters;
            var width = pp.BackBufferWidth;
            var height = pp.BackBufferHeight;
            var format = pp.BackBufferFormat;
            _sceneRenderTarget = new RenderTarget2D(GraphicsDevice, width, height, false, format, pp.DepthStencilFormat, pp.MultiSampleCount, RenderTargetUsage.DiscardContents);
            width /= 2;
            height /= 2;
            _renderTarget1 = new RenderTarget2D(GraphicsDevice, width, height, false, format, DepthFormat.None);
            _renderTarget2 = new RenderTarget2D(GraphicsDevice, width, height, false, format, DepthFormat.None);
        }
        /// <summary>
        /// Unload Content
        /// </summary>
        protected override void UnloadContent() {
            _sceneRenderTarget.Dispose();
            _renderTarget1.Dispose();
            _renderTarget2.Dispose();
        }
        /// <summary>
        /// Begin Draw
        /// </summary>
        public void BeginDraw() {
            if (Visible) {
                GraphicsDevice.SetRenderTarget(_sceneRenderTarget);
            }
        }
        /// <summary>
        /// Draw
        /// </summary>
        /// <param name="gameTime"></param>
        public override void Draw(GameTime gameTime) {
            GraphicsDevice.SamplerStates[1] = SamplerState.LinearClamp;
            _bloomExtractEffect.Parameters["BloomThreshold"].SetValue(Settings.BloomThreshold);
            DrawFullscreenQuad(_sceneRenderTarget, _renderTarget1, _bloomExtractEffect, IntermediateBuffer.iPreBloom);
            SetBlurEffectParameters(1.0f / (float)_renderTarget1.Width, 0);
            DrawFullscreenQuad(_renderTarget1, _renderTarget2, _gaussianBlurEffect, IntermediateBuffer.iBlurredHorizontally);
            SetBlurEffectParameters(0, 1.0f / (float)_renderTarget1.Height);
            DrawFullscreenQuad(_renderTarget2, _renderTarget1, _gaussianBlurEffect, IntermediateBuffer.iBlurredBothWays);
            GraphicsDevice.SetRenderTarget(null);
            var parameters = _bloomCombineEffect.Parameters;
            parameters["BloomIntensity"].SetValue(Settings.BloomIntensity);
            parameters["BaseIntensity"].SetValue(Settings.BaseIntensity);
            parameters["BloomSaturation"].SetValue(Settings.BloomSaturation);
            parameters["BaseSaturation"].SetValue(Settings.BaseSaturation);
            GraphicsDevice.Textures[1] = _sceneRenderTarget;
            var viewport = GraphicsDevice.Viewport;
            DrawFullscreenQuad(_renderTarget1, viewport.Width, viewport.Height, _bloomCombineEffect, IntermediateBuffer.iFinalResult);
        }
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
            if (ShowBuffer < currentBuffer) {
                effect = null;
            }
            _spriteBatch.Begin(0, BlendState.Opaque, null, null, null, effect);
            _spriteBatch.Draw(texture, new Rectangle(0, 0, width, height), Color.White);
            _spriteBatch.End();
        }
        /// <summary>
        /// Set Blur Effect Parameters
        /// </summary>
        /// <param name="dx"></param>
        /// <param name="dy"></param>
        private void SetBlurEffectParameters(float dx, float dy) {
            EffectParameter weightsParameter, offsetsParameter;
            weightsParameter = _gaussianBlurEffect.Parameters["SampleWeights"];
            offsetsParameter = _gaussianBlurEffect.Parameters["SampleOffsets"];
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
            var theta = Settings.BlurAmount;
            return (float)((1.0 / Math.Sqrt(2 * Math.PI * theta)) * Math.Exp(-(n * n) / (2 * theta * theta)));
        }
    }
}