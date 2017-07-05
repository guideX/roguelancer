

using Microsoft.Xna.Framework.Graphics;
using Roguelancer.Enum;
namespace Roguelancer.Models {
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
        public SpriteBatch SpriteBatch { get; set; }
        /// <summary>
        /// Bloom Extract Effect
        /// </summary>
        public Effect BloomExtractEffect { get; set; }
        /// <summary>
        /// Bloom Combine Effect
        /// </summary>
        public Effect BloomCombineEffect { get; set; }
        /// <summary>
        /// Gaussian Blur Effect
        /// </summary>
        public Effect GaussianBlurEffect { get; set; }
        /// <summary>
        /// Scene Render Target
        /// </summary>
        public RenderTarget2D SceneRenderTarget { get; set; }
        /// <summary>
        /// Render Target 1
        /// </summary>
        public RenderTarget2D RenderTarget1 { get; set; }
        /// <summary>
        /// Render Target 2
        /// </summary>
        public RenderTarget2D RenderTarget2 { get; set; }
    }
}