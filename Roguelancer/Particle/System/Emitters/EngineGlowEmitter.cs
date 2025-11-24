using System;
using Microsoft.Xna.Framework;
using Roguelancer.Interfaces;
using Roguelancer.Particle.ParticleSystem;

namespace Roguelancer.Particle.System.Emitters {
    /// <summary>
    /// Engine Glow Emitter
    /// Continuously emits engine glow particles based on ship thrust
    /// </summary>
    public class EngineGlowEmitter : IParticleEmitter {
        public DynamicParticleSystem ParticleSystem { get; set; }
        
        /// <summary>
        /// Position of the engine
        /// </summary>
        public Vector3 Position { get; set; }
        
        /// <summary>
        /// Direction the engine is facing (opposite of ship direction)
        /// </summary>
        public Vector3 Direction { get; set; }
        
        /// <summary>
        /// Current thrust level (0.0 to 1.0+)
        /// </summary>
        public float Thrust { get; set; }
        
        /// <summary>
        /// Engine color
        /// </summary>
        public Color EngineColor { get; set; }
        
        /// <summary>
        /// Is this an afterburner?
        /// </summary>
        public bool IsAfterburner { get; set; }
        
        /// <summary>
        /// Base emission rate (particles per second)
        /// </summary>
        public int BaseEmissionRate { get; set; }
        
        private float particlesEmitted = 0.0f;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="position">Engine position</param>
        /// <param name="direction">Engine direction</param>
        /// <param name="engineColor">Color of the engine glow</param>
        /// <param name="baseEmissionRate">Base particles per second</param>
        public EngineGlowEmitter(Vector3 position, Vector3 direction, Color engineColor, int baseEmissionRate = 60) {
            Position = position;
            Direction = direction;
            EngineColor = engineColor;
            BaseEmissionRate = baseEmissionRate;
            Thrust = 0.0f;
            IsAfterburner = false;
        }

        /// <summary>
        /// Update the emitter
        /// </summary>
        /// <param name="gameTime">Game time</param>
        public void Update(GameTime gameTime) {
            // Only emit if there's thrust
            if (Thrust <= 0) return;

            // Calculate emission rate based on thrust
            var emissionRate = BaseEmissionRate * Math.Abs(Thrust);
            
            // Afterburner gets 2x emission rate
            if (IsAfterburner) {
                emissionRate *= 2.0f;
            }

            particlesEmitted += (float)gameTime.ElapsedGameTime.TotalSeconds * emissionRate;
            int emittedCount = (int)particlesEmitted;
            
            if (emittedCount > 0) {
                Emit(emittedCount);
                particlesEmitted -= emittedCount;
            }
        }

        /// <summary>
        /// Emit particles
        /// </summary>
        /// <param name="particlesToEmit">Number of particles to emit</param>
        public void Emit(int particlesToEmit) {
            if (ParticleSystem == null) return;

            for (int i = 0; i < particlesToEmit; i++) {
                // Calculate velocity based on thrust and direction
                var velocity = -Direction * Math.Abs(Thrust) * RandomHelper.FloatBetween(1.0f, 2.0f);
                
                // Add some spread to the particles
                velocity += new Vector3(
                    RandomHelper.FloatBetween(-0.5f, 0.5f),
                    RandomHelper.FloatBetween(-0.5f, 0.5f),
                    RandomHelper.FloatBetween(-0.5f, 0.5f)
                ) * Math.Abs(Thrust) * 0.5f;

                // Determine color based on type and thrust
                Color particleColor;
                float scale;
                TimeSpan lifetime;

                if (IsAfterburner) {
                    // Afterburner: bright blue-white with longer trail
                    particleColor = Color.Lerp(
                        new Color(120, 180, 255),
                        new Color(220, 240, 255),
                        RandomHelper.FloatBetween(0f, 1f)
                    );
                    scale = RandomHelper.FloatBetween(0.2f, 0.5f) * Math.Abs(Thrust);
                    lifetime = TimeSpan.FromSeconds(RandomHelper.FloatBetween(0.2f, 0.5f));
                } else {
                    // Normal thrust: orange/yellow glow
                    var thrustIntensity = Math.Min(Math.Abs(Thrust) * 2.0f, 1.0f);
                    particleColor = Color.Lerp(
                        new Color(EngineColor.R / 3, EngineColor.G / 3, EngineColor.B / 3),
                        EngineColor,
                        thrustIntensity
                    );
                    scale = RandomHelper.FloatBetween(0.1f, 0.3f) * Math.Abs(Thrust);
                    lifetime = TimeSpan.FromSeconds(RandomHelper.FloatBetween(0.1f, 0.3f));
                }

                // Add position variation for organic look
                var positionOffset = new Vector3(
                    RandomHelper.FloatBetween(-0.3f, 0.3f),
                    RandomHelper.FloatBetween(-0.3f, 0.3f),
                    RandomHelper.FloatBetween(-0.3f, 0.3f)
                ) * Math.Abs(Thrust) * 0.5f;

                ParticleSystem.AddParticle(
                    Position + positionOffset,
                    particleColor,
                    velocity,
                    RandomHelper.FloatBetween(-0.1f, 0.1f),
                    lifetime,
                    true,
                    RandomHelper.FloatBetween(0.0f, MathHelper.TwoPi),
                    scale
                );
            }
        }
    }
}
