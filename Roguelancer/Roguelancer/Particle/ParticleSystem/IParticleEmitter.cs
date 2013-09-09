// Roguelancer 0.1 Pre Alpha by Leon Aiossa
// http://www.team-nexgen.org
using Microsoft.Xna.Framework;
namespace Roguelancer.Particle.ParticleSystem {
    public interface IParticleEmitter {
        DynamicParticleSystem ParticleSystem { get; set; }
        void Update(GameTime gameTime);
        void Emit(int particlesToEmit);
    }
}