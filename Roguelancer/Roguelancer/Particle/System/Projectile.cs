// Roguelancer 0.1 Pre Alpha by Leon Aiossa
// http://www.team-nexgen.com
using System;
using Microsoft.Xna.Framework;
using Roguelancer.Particle.System.Emitters;
using Roguelancer.Particle.System.ParticleSystems;
using Roguelancer.Particle.ParticleSystem;
namespace Roguelancer.Particle.System {
    /// <summary>
    /// Projectile
    /// </summary>
    public class Projectile {
        /// <summary>
        /// Trail Particles Per Second
        /// </summary>
        private const float _trailParticlesPerSecond = 50;
        /// <summary>
        /// Num Explosion Particles
        /// </summary>
        private const int _numExplosionParticles = 15;
        /// <summary>
        /// Num Explosion Particles
        /// </summary>
        private const int _numExplosionSmokeParticles = 25;
        /// <summary>
        /// Project Lifespan
        /// </summary>
        private const float _projectileLifespan = 1.5f;
        /// <summary>
        /// Sideways Velocity Range
        /// </summary>
        private const float _sidewaysVelocityRange = 60;
        /// <summary>
        /// Vertical Velocity Range
        /// </summary>
        private const float _verticalVelocityRange = 40;
        /// <summary>
        /// Gravity
        /// </summary>
        private const float _gravity = 15;
        /// <summary>
        /// Explosion Particle System
        /// </summary>
        private ExplosionParticleSystem _explosionParticleSystem;
        /// <summary>
        /// Explosion Smoke Particle System
        /// </summary>
        private ExplosionSmokeParticleSystem _explosionSmokeParticleSystem;
        /// <summary>
        /// Position
        /// </summary>
        private Vector3 _position;
        /// <summary>
        /// Velocity
        /// </summary>
        private Vector3 _velocity;
        /// <summary>
        /// Age
        /// </summary>
        private float _age;
        /// <summary>
        /// Trail Emitter
        /// </summary>
        public TrailEmitter TrailEmitter { get; set; }
        /// <summary>
        /// Entrypoint
        /// </summary>
        /// <param name="explosionParticleSystem"></param>
        /// <param name="explosionSmokeParticleSystem"></param>
        /// <param name="projectileTrailParticleSystem"></param>
        public Projectile(ExplosionParticleSystem explosionParticleSystem, ExplosionSmokeParticleSystem explosionSmokeParticleSystem, ProjectileTrailParticleSystem projectileTrailParticleSystem) {
            _explosionParticleSystem = explosionParticleSystem;
            _explosionSmokeParticleSystem = explosionSmokeParticleSystem;
            _position = Vector3.Zero;
            _velocity.X = (float)(RandomHelper.Rnd.NextDouble() - 0.5) * _sidewaysVelocityRange;
            _velocity.Y = (float)(RandomHelper.Rnd.NextDouble() + 0.5) * _verticalVelocityRange;
            _velocity.Z = (float)(RandomHelper.Rnd.NextDouble() - 0.5) * _sidewaysVelocityRange;
            TrailEmitter = new TrailEmitter(_trailParticlesPerSecond, _position);
            projectileTrailParticleSystem.AddEmitter(TrailEmitter);
        }
        /// <summary>
        /// Update
        /// </summary>
        /// <param name="gameTime"></param>
        /// <returns></returns>
        public bool Update(GameTime gameTime) {
            float elapsedTime = (float)gameTime.ElapsedGameTime.TotalSeconds;
            _position += _velocity * elapsedTime;
            _velocity.Y -= elapsedTime * _gravity;
            _age += elapsedTime;
            TrailEmitter.Position = _position;
            TrailEmitter.Update(gameTime);
            if (_age > _projectileLifespan) {
                for (var i = 0; i < _numExplosionParticles; i++)
                    _explosionParticleSystem.AddParticle(
                        _position,
                        RandomHelper.ColorBetween(Color.DarkGray, Color.Gray).Value,
                        _velocity * 0.01f + new Vector3(RandomHelper.FloatBetween(-30, 30), RandomHelper.FloatBetween(30, -10), RandomHelper.FloatBetween(-30, 30)) * 0.05f,
                        RandomHelper.FloatBetween(-0.01f, 0.01f),
                        TimeSpan.FromSeconds(RandomHelper.IntBetween(1, 2)),
                        true,
                        RandomHelper.FloatBetween(0.0f, MathHelper.Pi),
                        0.1f);

                for (var i = 0; i < _numExplosionSmokeParticles; i++)
                    _explosionSmokeParticleSystem.AddParticle(
                        _position,
                        RandomHelper.ColorBetween(Color.LightGray, Color.White).Value,
                        _velocity * 0.01f + new Vector3(RandomHelper.FloatBetween(-50, 50), RandomHelper.FloatBetween(40, -40), RandomHelper.FloatBetween(-50, 50)) * 0.05f,
                        RandomHelper.FloatBetween(-0.01f, 0.01f),
                        TimeSpan.FromSeconds(1),
                        true,
                        RandomHelper.FloatBetween(0.0f, MathHelper.Pi),
                        0.25f);

                return false;
            }
            return true;
        }
    }
}