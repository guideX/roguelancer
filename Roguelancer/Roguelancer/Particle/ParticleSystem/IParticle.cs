// Roguelancer 0.1 Pre Alpha by Leon Aiossa
// http://www.team-nexgen.org
using Microsoft.Xna.Framework;
namespace Roguelancer.Particle.ParticleSystem {
    public interface IParticle {
        Vector3 position { get; set; }
        Color color { get; set; }
        float angle { get; set; }
        float scale { get; set; }
    }
}