

using System;
using Microsoft.Xna.Framework;
using Roguelancer.Particle.ParticleSystem;
using Roguelancer.Interfaces;
namespace Roguelancer.Particle.System.Emitters {
    /// <summary>
    /// Trail Emitter
    /// </summary>
    public class TrailEmitter : IParticleEmitter {
        /// <summary>
        /// Time Between Particles
        /// </summary>
        private float _timeBetweenParticles;
        /// <summary>
        /// Previous Position
        /// </summary>
        private Vector3 _previousPosition;
        /// <summary>
        /// Time Left Over
        /// </summary>
        private float _timeLeftOver;
        /// <summary>
        /// Particle System
        /// </summary>
        public DynamicParticleSystem ParticleSystem { get; set; }
        /// <summary>
        /// Position
        /// </summary>
        public Vector3 Position { get; set; }
        /// <summary>
        /// Trail Emmiter
        /// </summary>
        /// <param name="particlesPerSecond"></param>
        /// <param name="initialPosition"></param>
        public TrailEmitter(float particlesPerSecond, Vector3 initialPosition) {
            _timeBetweenParticles = 1.0f / particlesPerSecond;
            _previousPosition = initialPosition;
        }
        /// <summary>
        /// Update
        /// </summary>
        /// <param name="gameTime"></param>
        public void Update(GameTime gameTime) {
            if (gameTime == null) {
                throw new ArgumentNullException("gameTime");
            }
            var elapsedTime = (float)gameTime.ElapsedGameTime.TotalSeconds;
            if (elapsedTime > 0) {
                var velocity = (Position - _previousPosition) / elapsedTime;
                var timeToSpend = _timeLeftOver + elapsedTime;
                var currentTime = - _timeLeftOver;
                while(timeToSpend > _timeBetweenParticles) {
                    currentTime += _timeBetweenParticles;
                    timeToSpend -= _timeBetweenParticles;
                    var mu = currentTime / elapsedTime;
                    var position = Vector3.Lerp(_previousPosition, Position, mu);
                    ParticleSystem.AddParticle(
                        position,
                        RandomHelper.ColorBetween(new Color(64, 96, 128, 255), new Color(255, 255, 255, 128)),
                        velocity * 0.01f,
                        RandomHelper.FloatBetween(-0.05f, 0.5f),
                        TimeSpan.FromSeconds(RandomHelper.FloatBetween(1.0f, 2.0f)),
                        true,
                        0.0f,
                        RandomHelper.FloatBetween(0.005f, 0.015f));
                }
                _timeLeftOver = timeToSpend;
            }
            _previousPosition = Position;
        }
        /// <summary>
        /// Emit
        /// </summary>
        /// <param name="particlesToEmit"></param>
        public void Emit(int particlesToEmit) {
        }
    }
}