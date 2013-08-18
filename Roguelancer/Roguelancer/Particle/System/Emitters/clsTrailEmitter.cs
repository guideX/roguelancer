// Roguelancer 0.1 Pre Alpha by Leon Aiossa
// http://www.team-nexgen.org
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Xna.Framework;
using Roguelancer.Particle.ParticleSystem;
namespace Roguelancer.Particle.System.Emitters {
    public class clsTrailEmitter : clsIParticleEmitter {
        private float timeBetweenParticles;
        private Vector3 previousPosition;
        private float timeLeftOver;
        public clsDynamicParticleSystem ParticleSystem { get; set; }
        public Vector3 Position { get; set; }
        public clsTrailEmitter(float particlesPerSecond, Vector3 initialPosition) {
            timeBetweenParticles = 1.0f / particlesPerSecond;
            previousPosition = initialPosition;
        }
        public void Update(GameTime gameTime) {
            if(gameTime == null)
                throw new ArgumentNullException("gameTime");
            float elapsedTime = (float)gameTime.ElapsedGameTime.TotalSeconds;
            if(elapsedTime > 0) {
                Vector3 velocity = (Position - previousPosition) / elapsedTime;
                float timeToSpend = timeLeftOver + elapsedTime;
                float currentTime = -timeLeftOver;
                while(timeToSpend > timeBetweenParticles) {
                    currentTime += timeBetweenParticles;
                    timeToSpend -= timeBetweenParticles;
                    float mu = currentTime / elapsedTime;
                    Vector3 position = Vector3.Lerp(previousPosition, Position, mu);
                    ParticleSystem.AddParticle(
                        position,
                        clsRandomHelper.ColorBetween(new Color(64, 96, 128, 255), new Color(255, 255, 255, 128)),
                        velocity * 0.01f,
                        clsRandomHelper.FloatBetween(-0.05f, 0.5f),
                        TimeSpan.FromSeconds(clsRandomHelper.FloatBetween(1.0f, 2.0f)),
                        true,
                        0.0f,
                        clsRandomHelper.FloatBetween(0.005f, 0.015f));
                }
                timeLeftOver = timeToSpend;
            }
            previousPosition = Position;
        }
        public void Emit(int particlesToEmit) {
        }
    }
}