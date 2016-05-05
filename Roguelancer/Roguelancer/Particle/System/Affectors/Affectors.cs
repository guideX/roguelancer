// Roguelancer 0.1 Pre Alpha by Leon Aiossa
// http://team-nexgen.com
using Microsoft.Xna.Framework;
using Roguelancer.Particle.ParticleSystem;
using Roguelancer.Interfaces;
namespace Roguelancer.Particle.System.Affectors {
    /// <summary>
    /// Decelerate
    /// </summary>
    public class Decelerate : IParticleAffector {
        /// <summary>
        /// Affect
        /// </summary>
        /// <param name="gameTime"></param>
        /// <param name="particle"></param>
        public void Affect(GameTime gameTime, DynamicParticle particle) {
            if(particle.Age.HasValue && particle.Velocity.HasValue && particle.InitialVelocity.HasValue) {
                particle.Velocity = Vector3.Lerp(particle.InitialVelocity.Value, Vector3.Zero, 1.0f - particle.Age.Value);
            }
        }
    }
}