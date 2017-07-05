

using Roguelancer.Particle.ParticleSystem;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework;
namespace Roguelancer.Particle.System.ParticleSystems {
    /// <summary>
    /// Explosion Smoke Particle System
    /// </summary>
    public class ExplosionSmokeParticleSystem : DynamicParticleSystem {
        /// <summary>
        /// Explosion Smoke Particle System
        /// </summary>
        /// <param name="maxCapacity"></param>
        /// <param name="texture"></param>
        public ExplosionSmokeParticleSystem(int maxCapacity, Texture2D texture) : base(maxCapacity, texture) {
        }
        /// <summary>
        /// Update
        /// </summary>
        /// <param name="gameTime"></param>
        public override void Update(GameTime gameTime) {
            foreach (var particle in LiveParticles) {
                particle.Color = Color.Lerp(particle.InitialColor, new Color(1.0f, 1.0f, 1.0f, 0.0f), 1.0f - particle.Age.Value);
                particle.Scale += 0.002f;
            }
            base.Update(gameTime);
        }
    }
}