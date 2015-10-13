// Roguelancer 0.1 Pre Alpha by Leon Aiossa
// http://www.team-nexgen.org
using Microsoft.Xna.Framework;
using Roguelancer.Particle.ParticleSystem;
namespace Roguelancer.Interfaces {
    /// <summary>
    /// Particle Emitter
    /// </summary>
    public interface IParticleEmitter {
        /// <summary>
        /// Particle System
        /// </summary>
        DynamicParticleSystem ParticleSystem { get; set; }
        /// <summary>
        /// Update
        /// </summary>
        /// <param name="gameTime"></param>
        void Update(GameTime gameTime);
        /// <summary>
        /// Emit
        /// </summary>
        /// <param name="particlesToEmit"></param>
        void Emit(int particlesToEmit);
    }
}