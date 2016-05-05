// Roguelancer 0.1 Pre Alpha by Leon Aiossa
// http://team-nexgen.com
using Microsoft.Xna.Framework;
using Roguelancer.Particle.ParticleSystem;
using Roguelancer.Interfaces;
namespace Roguelancer.Particle.System.Affector {
    /// <summary>
    /// Velocity Affector
    /// </summary>
    public class VelocityAffector : IParticleAffector{
        /// <summary>
        /// Velocity Change
        /// </summary>
        public Vector3 VelocityChange { get; set; }
        /// <summary>
        /// Velocity Affector
        /// </summary>
        /// <param name="velocityChange"></param>
        public VelocityAffector(Vector3 velocityChange) {
            VelocityChange = velocityChange;
        }
        /// <summary>
        /// Affector
        /// </summary>
        /// <param name="gameTime"></param>
        /// <param name="particle"></param>
        public void Affect(GameTime gameTime, DynamicParticle particle) {
            if (particle.Velocity.HasValue) {
                particle.Velocity += (float)gameTime.ElapsedGameTime.TotalSeconds * VelocityChange;
            }
        }
    }
}