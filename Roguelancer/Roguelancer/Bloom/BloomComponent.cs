// Roguelancer 0.1 Pre Alpha by Leon Aiossa
// http://www.team-nexgen.org
using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
namespace Roguelancer.Bloom {
    public class BloomComponent : DrawableGameComponent {
        private SpriteBatch _spriteBatch;
        private Effect _bloomExtractEffect;
        private Effect _bloomCombineEffect;
        private Effect _gaussianBlurEffect;
        private RenderTarget2D _sceneRenderTarget;
        private RenderTarget2D _renderTarget1;
        private RenderTarget2D _renderTarget2;
        private BloomSettings _settings = BloomSettings.PresetSettings[0];
        private IntermediateBuffer _showBuffer = IntermediateBuffer.iFinalResult;
        public BloomSettings Settings {
            get { return _settings; }
            set { _settings = value; }
        }
        public enum IntermediateBuffer {
            iPreBloom,
            iBlurredHorizontally,
            iBlurredBothWays,
            iFinalResult,
        }
        public IntermediateBuffer ShowBuffer {
            get { return _showBuffer; }
            set { _showBuffer = value; }
        }
        public BloomComponent(Game game)
            : base(game) {
            if (game == null) {
                throw new ArgumentNullException("game");
            }
        }
        protected override void LoadContent() {
            _spriteBatch = new SpriteBatch(GraphicsDevice);
            _bloomExtractEffect = Game.Content.Load<Effect>("Effects\\BloomExtract");
            _bloomCombineEffect = Game.Content.Load<Effect>("Effects\\BloomCombine");
            _gaussianBlurEffect = Game.Content.Load<Effect>("Effects\\GaussianBlur");
            PresentationParameters pp = GraphicsDevice.PresentationParameters;
            int width = pp.BackBufferWidth;
            int height = pp.BackBufferHeight;
            SurfaceFormat format = pp.BackBufferFormat;
            _sceneRenderTarget = new RenderTarget2D(GraphicsDevice, width, height, false, format, pp.DepthStencilFormat, pp.MultiSampleCount, RenderTargetUsage.DiscardContents);
            width /= 2;
            height /= 2;
            _renderTarget1 = new RenderTarget2D(GraphicsDevice, width, height, false, format, DepthFormat.None);
            _renderTarget2 = new RenderTarget2D(GraphicsDevice, width, height, false, format, DepthFormat.None);
        }
        protected override void UnloadContent() {
            _sceneRenderTarget.Dispose();
            _renderTarget1.Dispose();
            _renderTarget2.Dispose();
        }
        public void BeginDraw() {
            if (Visible) {
                GraphicsDevice.SetRenderTarget(_sceneRenderTarget);
            }
        }
        public override void Draw(GameTime gameTime) {
            GraphicsDevice.SamplerStates[1] = SamplerState.LinearClamp;
            _bloomExtractEffect.Parameters["BloomThreshold"].SetValue(_settings.BloomThreshold);
            DrawFullscreenQuad(_sceneRenderTarget, _renderTarget1, _bloomExtractEffect, IntermediateBuffer.iPreBloom);
            SetBlurEffectParameters(1.0f / (float)_renderTarget1.Width, 0);
            DrawFullscreenQuad(_renderTarget1, _renderTarget2, _gaussianBlurEffect, IntermediateBuffer.iBlurredHorizontally);
            SetBlurEffectParameters(0, 1.0f / (float)_renderTarget1.Height);
            DrawFullscreenQuad(_renderTarget2, _renderTarget1, _gaussianBlurEffect, IntermediateBuffer.iBlurredBothWays);
            GraphicsDevice.SetRenderTarget(null);
            EffectParameterCollection parameters = _bloomCombineEffect.Parameters;
            parameters["BloomIntensity"].SetValue(_settings.BloomIntensity);
            parameters["BaseIntensity"].SetValue(_settings.BaseIntensity);
            parameters["BloomSaturation"].SetValue(_settings.BloomSaturation);
            parameters["BaseSaturation"].SetValue(_settings.BaseSaturation);
            GraphicsDevice.Textures[1] = _sceneRenderTarget;
            Viewport viewport = GraphicsDevice.Viewport;
            DrawFullscreenQuad(_renderTarget1, viewport.Width, viewport.Height, _bloomCombineEffect, IntermediateBuffer.iFinalResult);
        }
        void DrawFullscreenQuad(Texture2D texture, RenderTarget2D renderTarget, Effect effect, IntermediateBuffer currentBuffer) {
            GraphicsDevice.SetRenderTarget(renderTarget);
            DrawFullscreenQuad(texture, renderTarget.Width, renderTarget.Height, effect, currentBuffer);
        }
        void DrawFullscreenQuad(Texture2D texture, int width, int height, Effect effect, IntermediateBuffer currentBuffer) {
            if (_showBuffer < currentBuffer) {
                effect = null;
            }
            _spriteBatch.Begin(0, BlendState.Opaque, null, null, null, effect);
            _spriteBatch.Draw(texture, new Rectangle(0, 0, width, height), Color.White);
            _spriteBatch.End();
        }
        void SetBlurEffectParameters(float dx, float dy) {
            EffectParameter weightsParameter, offsetsParameter;
            weightsParameter = _gaussianBlurEffect.Parameters["SampleWeights"];
            offsetsParameter = _gaussianBlurEffect.Parameters["SampleOffsets"];
            int sampleCount = weightsParameter.Elements.Count;
            float[] sampleWeights = new float[sampleCount];
            Vector2[] sampleOffsets = new Vector2[sampleCount];
            sampleWeights[0] = ComputeGaussian(0);
            sampleOffsets[0] = new Vector2(0);
            float totalWeights = sampleWeights[0];
            for (int i = 0; i < sampleCount / 2; i++) {
                float weight = ComputeGaussian(i + 1);
                sampleWeights[i * 2 + 1] = weight;
                sampleWeights[i * 2 + 2] = weight;
                totalWeights += weight * 2;
                float sampleOffset = i * 2 + 1.5f;
                Vector2 delta = new Vector2(dx, dy) * sampleOffset;
                sampleOffsets[i * 2 + 1] = delta;
                sampleOffsets[i * 2 + 2] = -delta;
            }
            for (int i = 0; i < sampleWeights.Length; i++) {
                sampleWeights[i] /= totalWeights;
            }
            weightsParameter.SetValue(sampleWeights);
            offsetsParameter.SetValue(sampleOffsets);
        }
        float ComputeGaussian(float n) {
            float theta = _settings.BlurAmount;
            return (float)((1.0 / Math.Sqrt(2 * Math.PI * theta)) * Math.Exp(-(n * n) / (2 * theta * theta)));
        }
    }
}