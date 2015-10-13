// Roguelancer 0.1 Pre Alpha by Leon Aiossa
// http://www.team-nexgen.org
using Microsoft.Xna.Framework;
using Roguelancer.Particle.ParticleSystem;
using Roguelancer.Interfaces;
namespace Roguelancer.Particle.System.Affectors {
    public class Decelerate : IParticleAffector {
        public void Affect(GameTime gameTime, DynamicParticle particle) {
            if(particle.Age.HasValue && particle.Velocity.HasValue && particle.InitialVelocity.HasValue) {
                particle.Velocity = Vector3.Lerp(particle.InitialVelocity.Value, Vector3.Zero, 1.0f - particle.Age.Value);
            }
        }
    }
}