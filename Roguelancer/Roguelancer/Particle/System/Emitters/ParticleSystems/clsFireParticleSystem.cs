// Roguelancer 0.1 Pre Alpha by Leon Aiossa
// http://www.team-nexgen.org
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework;
using Roguelancer.Particle.ParticleSystem;
namespace Roguelancer.Particle.System.ParticleSystems {
    public class clsFireParticleSystem : clsDynamicParticleSystem {
        private float particlesEmitted = 0.0f;
        public int EmissionRate { get; set; }
        public clsFireParticleSystem (int maxCapacity, Texture2D texture) : base(maxCapacity, texture) {}
        public override void Update(GameTime gameTime) {
            EmitParticles(gameTime);
            foreach (clsDynamicParticle particle in lLiveParticles) {
                particle.lColor = Color.Lerp(particle.lInitialColor, new Color(1.0f, 1.0f, 1.0f, 0.0f), 1.0f - particle.Age.Value);
                particle.lScale += 0.00001f;
            }
            base.Update(gameTime);
        }
        private void EmitParticles(GameTime gameTime) {
            particlesEmitted += (float)gameTime.ElapsedGameTime.TotalSeconds * (float)EmissionRate;
            int emittedCount = (int)particlesEmitted;
            if (emittedCount > 0) {
                Color _Color;
                for (int i = 0; i < emittedCount; i++) {
                    _Color = new Color((float)clsRandomHelper.Random.NextDouble(), (float)clsRandomHelper.Random.NextDouble(), (float)clsRandomHelper.Random.NextDouble());
                    AddParticle(RandomPointOnCircle(), _Color, clsRandomHelper.Vector3Between(new Vector3(-0.25f, 0.0f, 0.0f), new Vector3(0.25f, 1.0f, 0.0f)), clsRandomHelper.FloatBetween(-0.01f, 0.1f), TimeSpan.FromSeconds(clsRandomHelper.IntBetween(1, 2)), true, clsRandomHelper.FloatBetween(0.0f, MathHelper.TwoPi), clsRandomHelper.FloatBetween(0.05f, 0.075f));
                }
                particlesEmitted -= emittedCount;
            }
        }
        Vector3 RandomPointOnCircle() {
            const float radius = 5;
            const float height = 40;
            double angle = clsRandomHelper.Random.NextDouble() * MathHelper.TwoPi;
            float x = (float)Math.Cos(angle);
            float y = (float)Math.Sin(angle);
            return new Vector3(x * radius, y * radius + height, -20);
        }
    }
}