// Roguelancer 0.1 Pre Alpha by Leon Aiossa
// http://team-nexgen.com
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework;
using Roguelancer.Particle.ParticleSystem;
namespace Roguelancer.Particle.System.ParticleSystems {
    /// <summary>
    /// Projectile Trail Particle System
    /// </summary>
    public class ProjectileTrailParticleSystem : DynamicParticleSystem {
        /// <summary>
        /// Projectile Trail Particle System
        /// </summary>
        /// <param name="maxCapacity"></param>
        /// <param name="texture"></param>
        public ProjectileTrailParticleSystem (int maxCapacity, Texture2D texture) : base(maxCapacity, texture) {
        }
        /// <summary>
        /// Update
        /// </summary>
        /// <param name="gameTime"></param>
        public override void Update(GameTime gameTime) {
            foreach (var particle in LiveParticles) {
                particle.Color = Color.Lerp(particle.InitialColor, new Color(1.0f, 1.0f, 1.0f, 0.0f), 1.0f - particle.Age.Value);
                particle.Scale += 0.001f;
            }
            base.Update(gameTime);
        }
    }
}