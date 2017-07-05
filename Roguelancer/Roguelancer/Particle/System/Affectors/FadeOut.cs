

using Microsoft.Xna.Framework;
using Roguelancer.Particle.ParticleSystem;
using Roguelancer.Interfaces;
namespace Roguelancer.Particle.System.Affectors {
    /// <summary>
    /// Fadeout
    /// </summary>
    public class Fadeout : IParticleAffector {
        /// <summary>
        /// Affect
        /// </summary>
        /// <param name="gameTime"></param>
        /// <param name="particle"></param>
        public void Affect(GameTime gameTime, DynamicParticle particle) {
            if(particle.Age.HasValue) {
                particle.Color = Color.Lerp(particle.InitialColor, new Color(particle.InitialColor.R, particle.InitialColor.G, particle.InitialColor.B, 0), 1.0f - particle.Age.Value);
            }
        }
    }
}
