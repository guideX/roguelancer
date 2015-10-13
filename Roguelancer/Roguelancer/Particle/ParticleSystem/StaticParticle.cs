// Roguelancer 0.1 Pre Alpha by Leon Aiossa
// http://www.team-nexgen.org
using Microsoft.Xna.Framework;
using Roguelancer.Interfaces;
namespace Roguelancer.Particle.ParticleSystem {
    public class StaticParticle : IParticle {
        public Vector3 Position { get; set; }
        public Color Color { get; set; }
        public float Angle { get; set; }
        public float Scale { get; set; }
    }
}