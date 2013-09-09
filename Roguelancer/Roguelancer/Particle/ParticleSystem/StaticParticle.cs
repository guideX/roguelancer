// Roguelancer 0.1 Pre Alpha by Leon Aiossa
// http://www.team-nexgen.org
using Microsoft.Xna.Framework;
namespace Roguelancer.Particle.ParticleSystem {
    public class StaticParticle : IParticle {
        public Vector3 position { get; set; }
        public Color color { get; set; }
        public float angle { get; set; }
        public float scale { get; set; }
    }
}
