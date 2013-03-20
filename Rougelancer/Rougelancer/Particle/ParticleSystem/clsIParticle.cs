// Rougelancer 0.1 Pre Alpha by Leon Aiossa
// http://www.team-nexgen.org
using Microsoft.Xna.Framework;
namespace Rougelancer.Particle.ParticleSystem {
    public interface clsIParticle {
        Vector3 lPosition { get; set; }
        Color lColor { get; set; }
        float lAngle { get; set; }
        float lScale { get; set; }
    }
}