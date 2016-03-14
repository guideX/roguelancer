using Microsoft.Xna.Framework.Graphics;
using Roguelancer.Enum;
namespace Roguelancer.Models.Bloom {
    /// <summary>
    /// Bloom Component Model
    /// </summary>
    public class BloomComponentModel {
        /// <summary>
        /// Settings
        /// </summary>
        public BloomSettingsModel Settings { get; set; }
        /// <summary>
        /// Show Buffer
        /// </summary>
        public IntermediateBuffer ShowBuffer { get; set; }
        /// <summary>
        /// Sprite Batch
        /// </summary>
        public SpriteBatch SpriteBatch;
        /// <summary>
        /// Bloom Extract Effect
        /// </summary>
        public Effect BloomExtractEffect;
        /// <summary>
        /// Bloom Combine Effect
        /// </summary>
        public Effect BloomCombineEffect;
        /// <summary>
        /// Gaussian Blur Effect
        /// </summary>
        public Effect GaussianBlurEffect;
        /// <summary>
        /// Scene Render Target
        /// </summary>
        public RenderTarget2D SceneRenderTarget;
        /// <summary>
        /// Render Target 1
        /// </summary>
        public RenderTarget2D RenderTarget1;
        /// <summary>
        /// Render Target 2
        /// </summary>
        public RenderTarget2D RenderTarget2;
    }
}
