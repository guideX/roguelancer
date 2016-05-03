// Roguelancer 0.1 Pre Alpha by Leon Aiossa
// http://www.team-nexgen.com
using Microsoft.Xna.Framework;
using Roguelancer.Interfaces;
namespace Roguelancer.Models.Particle {
    /// <summary>
    /// Static Particle
    /// </summary>
    public class StaticParticleModel : IParticle {
        /// <summary>
        /// Position
        /// </summary>
        public Vector3 Position { get; set; }
        /// <summary>
        /// Color
        /// </summary>
        public Color Color { get; set; }
        /// <summary>
        /// Angle
        /// </summary>
        public float Angle { get; set; }
        /// <summary>
        /// Scale
        /// </summary>
        public float Scale { get; set; }
    }
}