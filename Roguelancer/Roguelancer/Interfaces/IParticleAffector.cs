// Roguelancer 0.1 Pre Alpha by Leon Aiossa
// http://www.team-nexgen.com
using Microsoft.Xna.Framework;
using Roguelancer.Particle.ParticleSystem;
namespace Roguelancer.Interfaces {
    /// <summary>
    /// Particle Affector
    /// </summary>
    public interface IParticleAffector {
        /// <summary>
        /// Affect
        /// </summary>
        /// <param name="gameTime"></param>
        /// <param name="particle"></param>
        void Affect(GameTime gameTime, DynamicParticle particle);
    }
}