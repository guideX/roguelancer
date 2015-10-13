// Roguelancer 0.1 Pre Alpha by Leon Aiossa
// http://www.team-nexgen.org
using Microsoft.Xna.Framework;
using Roguelancer.Particle.ParticleSystem;
using Roguelancer.Interfaces;
namespace Roguelancer.Particle.System.Affector {
    public class clsVelocityAffector : IParticleAffector{
        public Vector3 VelocityChange { get; set; }
        public clsVelocityAffector(Vector3 velocityChange) {
            VelocityChange = velocityChange;
        }
        public void Affect(GameTime gameTime, DynamicParticle particle) {
            if (particle.Velocity.HasValue) {
                particle.Velocity += (float)gameTime.ElapsedGameTime.TotalSeconds * VelocityChange;
            }
        }
    }
}