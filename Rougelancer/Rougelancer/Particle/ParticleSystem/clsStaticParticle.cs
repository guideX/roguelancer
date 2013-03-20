// Rougelancer 0.1 Pre Alpha by Leon Aiossa
// http://www.team-nexgen.org
using Microsoft.Xna.Framework;
namespace Rougelancer.Particle.ParticleSystem {
    public class clsStaticParticle : clsIParticle {
        public Vector3 lPosition { get; set; }
        public Color lColor { get; set; }
        public float lAngle { get; set; }
        public float lScale { get; set; }
    }
}
