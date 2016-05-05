// Roguelancer 0.1 Pre Alpha by Leon Aiossa
// http://team-nexgen.com
using System;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework;
using Roguelancer.Particle.ParticleSystem;
namespace Roguelancer.Particle.System.ParticleSystems {
    /// <summary>
    /// Fire Particle System
    /// </summary>
    public class FireParticleSystem : DynamicParticleSystem {
        private float particlesEmitted = 0.0f;
        public int emissionRate { get; set; }
        public FireParticleSystem (int maxCapacity, Texture2D texture) : base(maxCapacity, texture) {}
        public override void Update(GameTime gameTime) {
            EmitParticles(gameTime);
            foreach (var particle in LiveParticles) {
                particle.Color = Color.Lerp(particle.InitialColor, new Color(1.0f, 1.0f, 1.0f, 0.0f), 1.0f - particle.Age.Value);
                particle.Scale += 0.00001f;
            }
            base.Update(gameTime);
        }
        /// <summary>
        /// Emit Particles
        /// </summary>
        /// <param name="gameTime"></param>
        private void EmitParticles(GameTime gameTime) {
            particlesEmitted += (float)gameTime.ElapsedGameTime.TotalSeconds * (float)emissionRate;
            var emittedCount = (int)particlesEmitted;
            if (emittedCount > 0) {
                for (var i = 0; i < emittedCount; i++) {
                    var c = new Color((float)RandomHelper.Rnd.NextDouble(), (float)RandomHelper.Rnd.NextDouble(), (float)RandomHelper.Rnd.NextDouble());
                    AddParticle(RandomPointOnCircle(), c, RandomHelper.Vector3Between(new Vector3(-0.25f, 0.0f, 0.0f), new Vector3(0.25f, 1.0f, 0.0f)), RandomHelper.FloatBetween(-0.01f, 0.1f), TimeSpan.FromSeconds(RandomHelper.IntBetween(1, 2)), true, RandomHelper.FloatBetween(0.0f, MathHelper.TwoPi), RandomHelper.FloatBetween(0.05f, 0.075f));
                }
                particlesEmitted -= emittedCount;
            }
        }
        /// <summary>
        /// Random Point On Circle
        /// </summary>
        /// <returns></returns>
        private Vector3 RandomPointOnCircle() {
            const float radius = 5;
            const float height = 40;
            var angle = RandomHelper.Rnd.NextDouble() * MathHelper.TwoPi;
            float x = (float)Math.Cos(angle);
            float y = (float)Math.Sin(angle);
            return new Vector3(x * radius, y * radius + height, -20);
        }
    }
}