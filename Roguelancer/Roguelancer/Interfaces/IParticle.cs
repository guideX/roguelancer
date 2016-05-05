// Roguelancer 0.1 Pre Alpha by Leon Aiossa
// http://team-nexgen.com
using Microsoft.Xna.Framework;
namespace Roguelancer.Interfaces {
    /// <summary>
    /// Particle
    /// </summary>
    public interface IParticle {
        /// <summary>
        /// Position
        /// </summary>
        Vector3 Position { get; set; }
        /// <summary>
        /// Color
        /// </summary>
        Color Color { get; set; }
        /// <summary>
        /// Angle
        /// </summary>
        float Angle { get; set; }
        /// <summary>
        /// Scale
        /// </summary>
        float Scale { get; set; }
    }
}