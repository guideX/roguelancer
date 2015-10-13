// Roguelancer 0.1 Pre Alpha by Leon Aiossa
// http://www.team-nexgen.org
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
namespace Roguelancer.Particle.ParticleSystem {
    public class StaticParticleSystem : ParticleSystem<StaticParticle> {
        public StaticParticleSystem(int maxCapacity, Texture2D texture) : base(maxCapacity, texture) {
        }
        public void AddParticle(Vector3 position, Color color) {
            AddParticle(position, color, 0.0f, 1.0f);
        }
        public void AddParticle(Vector3 position, Color color, float angle, float scale) {
            if(lDeadParticles.Count != 0) {
                StaticParticle particle = lDeadParticles.Pop();
                particle.Position = position;
                particle.Color = color;
                particle.Angle = angle;
                particle.Scale = scale;
                liveParticles.Add(particle);
            }
        }
    }
}