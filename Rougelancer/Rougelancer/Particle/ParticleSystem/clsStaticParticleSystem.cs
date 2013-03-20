// Rougelancer 0.1 Pre Alpha by Leon Aiossa
// http://www.team-nexgen.org
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
namespace Rougelancer.Particle.ParticleSystem {
    public class clsStaticParticleSystem : ParticleSystem<clsStaticParticle> {
        public clsStaticParticleSystem(int maxCapacity, Texture2D texture) : base(maxCapacity, texture) {
        }
        public void AddParticle(Vector3 position, Color color) {
            AddParticle(position, color, 0.0f, 1.0f);
        }
        public void AddParticle(Vector3 position, Color color, float angle, float scale) {
            if(lDeadParticles.Count != 0) {
                clsStaticParticle particle = lDeadParticles.Pop();
                particle.lPosition = position;
                particle.lColor = color;
                particle.lAngle = angle;
                particle.lScale = scale;
                lLiveParticles.Add(particle);
            }
        }
    }
}