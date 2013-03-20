// Rougelancer 0.1 Pre Alpha by Leon Aiossa
// http://www.team-nexgen.org
using Microsoft.Xna.Framework;
namespace Rougelancer.Particle.ParticleSystem {
    public interface clsIParticleEmitter {
        clsDynamicParticleSystem ParticleSystem { get; set; }
        void Update(GameTime gameTime);
        void Emit(int particlesToEmit);
    }
}