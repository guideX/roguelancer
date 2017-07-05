

using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework;
using Roguelancer.Particle.ParticleSystem;
namespace Roguelancer.Particle.System.ParticleSystems {
    /// <summary>
    /// Smoke Particles System
    /// </summary>
    public class SmokeParticleSystem : DynamicParticleSystem {
        /// <summary>
        /// Smoke Particle System
        /// </summary>
        /// <param name="maxCapacity"></param>
        /// <param name="texture"></param>
        public SmokeParticleSystem(int maxCapacity, Texture2D texture) : base(maxCapacity, texture) {
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