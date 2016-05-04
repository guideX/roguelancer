// Roguelancer 0.1 Pre Alpha by Leon Aiossa
// http://www.team-nexgen.com
using Microsoft.Xna.Framework.Graphics;
using Roguelancer.Enum;
using Roguelancer.Models.Bloom;
namespace Roguelancer.Interfaces {
    /// <summary>
    /// Bloom Component
    /// </summary>
    public interface IBloomComponent {
        /// <summary>
        /// Bloom Component Model
        /// </summary>
        BloomComponentModel Model { get; set; }
        /// <summary>
        /// Draw Full Screen Quad
        /// </summary>
        /// <param name="texture"></param>
        /// <param name="width"></param>
        /// <param name="height"></param>
        /// <param name="effect"></param>
        /// <param name="currentBuffer"></param>
        void DrawFullscreenQuad(Texture2D texture, int width, int height, Effect effect, IntermediateBuffer currentBuffer);
        /// <summary>
        /// Set Blur Effect Parameters
        /// </summary>
        /// <param name="dx"></param>
        /// <param name="dy"></param>
        void SetBlurEffectParameters(float dx, float dy);
        /// <summary>
        /// Compute Gaussian
        /// </summary>
        /// <param name="n"></param>
        /// <returns></returns>
        float ComputeGaussian(float n);
    }
}