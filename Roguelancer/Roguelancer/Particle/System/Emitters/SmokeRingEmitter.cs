// Roguelancer 0.1 Pre Alpha by Leon Aiossa
// http://www.team-nexgen.com
using System;
using Microsoft.Xna.Framework;
using Roguelancer.Particle.ParticleSystem;
using Roguelancer.Interfaces;
namespace Roguelancer.Particle.System.Emitters {
    /// <summary>
    /// Smoke Ring Emitter
    /// </summary>
    public class SmokeRingEmitter : IParticleEmitter {
        /// <summary>
        /// Particle System
        /// </summary>
        public DynamicParticleSystem ParticleSystem { get; set; }
        /// <summary>
        /// Emission Rate
        /// </summary>
        public int EmissionRate { get; set; }
        /// <summary>
        /// Position
        /// </summary>
        public Vector3 Position { get; set; }
        /// <summary>
        /// Particles Emitted
        /// </summary>
        private float _particlesEmitted = 0.0f;
        /// <summary>
        /// Smoke Ring Emitter
        /// </summary>
        /// <param name="position"></param>
        /// <param name="emissionRate"></param>
        public SmokeRingEmitter(Vector3 position, int emissionRate) {
            Position = position;
            EmissionRate = emissionRate;
        }
        /// <summary>
        /// Update
        /// </summary>
        /// <param name="gameTime"></param>
        public void Update(GameTime gameTime) {
            _particlesEmitted += (float)gameTime.ElapsedGameTime.TotalSeconds * (float)EmissionRate;
            var emittedCount = (int)_particlesEmitted;
            if(emittedCount > 0) {
                Emit(emittedCount);
                _particlesEmitted -= emittedCount;
            }
        }
        public void Emit(int particlesToEmit) {
            for (var i = 0; i < particlesToEmit; i++) {
                ParticleSystem.AddParticle(
                        RandomPointOnCircle(),
                        Color.White,
                        new Vector3(RandomHelper.FloatBetween(0.0f, -0.5f), RandomHelper.FloatBetween(0.1f, 0.75f), 0.0f),
                        RandomHelper.FloatBetween(-0.01f, 0.1f),
                        TimeSpan.FromSeconds(RandomHelper.IntBetween(2, 5)),
                        true,
                        RandomHelper.FloatBetween(0.0f, MathHelper.TwoPi),
                        RandomHelper.FloatBetween(0.05f, 0.1f));
            }
        }
        /// <summary>
        /// Random Point on Circle
        /// </summary>
        /// <returns></returns>
        private Vector3 RandomPointOnCircle() {
            const float radius = 30;
            const float height = 40;
            double angle = RandomHelper.Rnd.NextDouble() * MathHelper.TwoPi;
            float x = (float)Math.Cos(angle);
            float y = (float)Math.Sin(angle);
            return new Vector3(x * radius, y * radius + height, 0);
        }
    }
}