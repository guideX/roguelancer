// Rougelancer 0.1 Pre Alpha by Leon Aiossa
// http://www.team-nexgen.org
using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
namespace Rougelancer.Bloom {
    public class clsBloomComponent : DrawableGameComponent {
        private SpriteBatch lSpriteBatch;
        private Effect lBloomExtractEffect;
        private Effect lBloomCombineEffect;
        private Effect lGaussianBlurEffect;
        private RenderTarget2D lSceneRenderTarget;
        private RenderTarget2D lRenderTarget1;
        private RenderTarget2D lRenderTarget2;
        private clsBloomSettings lsettings = clsBloomSettings.PresetSettings[0];
        private IntermediateBuffer showBuffer = IntermediateBuffer.iFinalResult;
        public clsBloomSettings lSettings {
            get { return lsettings; }
            set { lsettings = value; }
        }
        public enum IntermediateBuffer {
            iPreBloom,
            iBlurredHorizontally,
            iBlurredBothWays,
            iFinalResult,
        }
        public IntermediateBuffer ShowBuffer {
            get { return showBuffer; }
            set { showBuffer = value; }
        }
        public clsBloomComponent(Game game) : base(game) {
            if(game == null) {
                throw new ArgumentNullException("game");
            }
        }
        protected override void LoadContent() {
            lSpriteBatch = new SpriteBatch(GraphicsDevice);
            lBloomExtractEffect = Game.Content.Load<Effect>("Effects\\BloomExtract");
            lBloomCombineEffect = Game.Content.Load<Effect>("Effects\\BloomCombine");
            lGaussianBlurEffect = Game.Content.Load<Effect>("Effects\\GaussianBlur");
            PresentationParameters pp = GraphicsDevice.PresentationParameters;
            int width = pp.BackBufferWidth;
            int height = pp.BackBufferHeight;
            SurfaceFormat format = pp.BackBufferFormat;
            lSceneRenderTarget = new RenderTarget2D(GraphicsDevice, width, height, false, format, pp.DepthStencilFormat, pp.MultiSampleCount, RenderTargetUsage.DiscardContents);
            width /= 2;
            height /= 2;
            lRenderTarget1 = new RenderTarget2D(GraphicsDevice, width, height, false, format, DepthFormat.None);
            lRenderTarget2 = new RenderTarget2D(GraphicsDevice, width, height, false, format, DepthFormat.None);
        }
        protected override void UnloadContent() {
            lSceneRenderTarget.Dispose();
            lRenderTarget1.Dispose();
            lRenderTarget2.Dispose();
        }
        public void BeginDraw() {
            if(Visible) {
                GraphicsDevice.SetRenderTarget(lSceneRenderTarget);
            }
        }
        public override void Draw(GameTime gameTime) {
            GraphicsDevice.SamplerStates[1] = SamplerState.LinearClamp;
            lBloomExtractEffect.Parameters["BloomThreshold"].SetValue(lSettings.BloomThreshold);
            DrawFullscreenQuad(lSceneRenderTarget, lRenderTarget1, lBloomExtractEffect, IntermediateBuffer.iPreBloom);
            SetBlurEffectParameters(1.0f / (float)lRenderTarget1.Width, 0);
            DrawFullscreenQuad(lRenderTarget1, lRenderTarget2, lGaussianBlurEffect, IntermediateBuffer.iBlurredHorizontally);
            SetBlurEffectParameters(0, 1.0f / (float)lRenderTarget1.Height);
            DrawFullscreenQuad(lRenderTarget2, lRenderTarget1, lGaussianBlurEffect, IntermediateBuffer.iBlurredBothWays);
            GraphicsDevice.SetRenderTarget(null);
            EffectParameterCollection parameters = lBloomCombineEffect.Parameters;
            parameters["BloomIntensity"].SetValue(lSettings.BloomIntensity);
            parameters["BaseIntensity"].SetValue(lSettings.BaseIntensity);
            parameters["BloomSaturation"].SetValue(lSettings.BloomSaturation);
            parameters["BaseSaturation"].SetValue(lSettings.BaseSaturation);
            GraphicsDevice.Textures[1] = lSceneRenderTarget;
            Viewport viewport = GraphicsDevice.Viewport;
            DrawFullscreenQuad(lRenderTarget1, viewport.Width, viewport.Height, lBloomCombineEffect, IntermediateBuffer.iFinalResult);
        }
        void DrawFullscreenQuad(Texture2D texture, RenderTarget2D renderTarget, Effect effect, IntermediateBuffer currentBuffer) {
            GraphicsDevice.SetRenderTarget(renderTarget);
            DrawFullscreenQuad(texture, renderTarget.Width, renderTarget.Height, effect, currentBuffer);
        }
        void DrawFullscreenQuad(Texture2D texture, int width, int height, Effect effect, IntermediateBuffer currentBuffer) {
            if(showBuffer < currentBuffer) {
                effect = null;
            }
            lSpriteBatch.Begin(0, BlendState.Opaque, null, null, null, effect);
            lSpriteBatch.Draw(texture, new Rectangle(0, 0, width, height), Color.White);
            lSpriteBatch.End();
        }
        void SetBlurEffectParameters(float dx, float dy) {
            EffectParameter weightsParameter, offsetsParameter;
            weightsParameter = lGaussianBlurEffect.Parameters["SampleWeights"];
            offsetsParameter = lGaussianBlurEffect.Parameters["SampleOffsets"];
            int sampleCount = weightsParameter.Elements.Count;
            float[] sampleWeights = new float[sampleCount];
            Vector2[] sampleOffsets = new Vector2[sampleCount];
            sampleWeights[0] = ComputeGaussian(0);
            sampleOffsets[0] = new Vector2(0);
            float totalWeights = sampleWeights[0];
            for(int i = 0; i < sampleCount / 2; i++) {
                float weight = ComputeGaussian(i + 1);
                sampleWeights[i * 2 + 1] = weight;
                sampleWeights[i * 2 + 2] = weight;
                totalWeights += weight * 2;
                float sampleOffset = i * 2 + 1.5f;
                Vector2 delta = new Vector2(dx, dy) * sampleOffset;
                sampleOffsets[i * 2 + 1] = delta;
                sampleOffsets[i * 2 + 2] = -delta;
            }
            for(int i = 0; i < sampleWeights.Length; i++) {
                sampleWeights[i] /= totalWeights;
            }
            weightsParameter.SetValue(sampleWeights);
            offsetsParameter.SetValue(sampleOffsets);
        }
        float ComputeGaussian(float n) {
            float theta = lSettings.BlurAmount;
            return (float)((1.0 / Math.Sqrt(2 * Math.PI * theta)) * Math.Exp(-(n * n) / (2 * theta * theta)));
        }
    }
}