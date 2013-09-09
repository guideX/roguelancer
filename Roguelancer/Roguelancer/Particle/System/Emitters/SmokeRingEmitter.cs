// Roguelancer 0.1 Pre Alpha by Leon Aiossa
// http://www.team-nexgen.org
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Xna.Framework;
using Roguelancer.Particle.ParticleSystem;
namespace Roguelancer.Particle.System.Emitters {
    public class SmokeRingEmitter : IParticleEmitter {
        public DynamicParticleSystem ParticleSystem { get; set; }
        public int EmissionRate { get; set; }
        public Vector3 Position { get; set; }
        private float particlesEmitted = 0.0f;
        public SmokeRingEmitter(Vector3 position, int emissionRate) {
            Position = position;
            EmissionRate = emissionRate;
        }
        public void Update(GameTime gameTime) {
            particlesEmitted += (float)gameTime.ElapsedGameTime.TotalSeconds * (float)EmissionRate;
            int emittedCount = (int)particlesEmitted;
            if(emittedCount > 0) {
                Emit(emittedCount);
                particlesEmitted -= emittedCount;
            }
        }
        public void Emit(int particlesToEmit) {
            for(int i = 0; i < particlesToEmit; i++) {
                ParticleSystem.AddParticle(
                        RandomPointOnCircle(),
                        Color.White,
                        new Vector3(clsRandomHelper.FloatBetween(0.0f, -0.5f), clsRandomHelper.FloatBetween(0.1f, 0.75f), 0.0f),
                        clsRandomHelper.FloatBetween(-0.01f, 0.1f),
                        TimeSpan.FromSeconds(clsRandomHelper.IntBetween(2, 5)),
                        true,
                        clsRandomHelper.FloatBetween(0.0f, MathHelper.TwoPi),
                        clsRandomHelper.FloatBetween(0.05f, 0.1f));
            }
        }
        Vector3 RandomPointOnCircle() {
            const float radius = 30;
            const float height = 40;
            double angle = clsRandomHelper.Random.NextDouble() * MathHelper.TwoPi;
            float x = (float)Math.Cos(angle);
            float y = (float)Math.Sin(angle);
            return new Vector3(x * radius, y * radius + height, 0);
        }
    }
}
