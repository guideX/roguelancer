// Roguelancer 0.1 Pre Alpha by Leon Aiossa
// http://www.team-nexgen.org
using System;
using Microsoft.Xna.Framework;
using Roguelancer.Particle.System.Emitters;
using Roguelancer.Particle.System.ParticleSystems;
using Roguelancer.Particle.ParticleSystem;
namespace Roguelancer.Particle.System {
    public class Projectile {
        private const float trailParticlesPerSecond = 50;
        private const int numExplosionParticles = 15;
        private const int numExplosionSmokeParticles = 25;
        private const float projectileLifespan = 1.5f;
        private const float sidewaysVelocityRange = 60;
        private const float verticalVelocityRange = 40;
        private const float gravity = 15;
        private ExplosionParticleSystem explosionParticleSystem;
        private ExplosionSmokeParticleSystem explosionSmokeParticleSystem;
        private Vector3 position;
        private Vector3 velocity;
        private float age;
        public TrailEmitter lTrailEmitter { get; set; }
        public Projectile(ExplosionParticleSystem explosionParticleSystem, ExplosionSmokeParticleSystem explosionSmokeParticleSystem, clsProjectileTrailParticleSystem projectileTrailParticleSystem) {
            this.explosionParticleSystem = explosionParticleSystem;
            this.explosionSmokeParticleSystem = explosionSmokeParticleSystem;
            position = Vector3.Zero;
            velocity.X = (float)(clsRandomHelper.Random.NextDouble() - 0.5) * sidewaysVelocityRange;
            velocity.Y = (float)(clsRandomHelper.Random.NextDouble() + 0.5) * verticalVelocityRange;
            velocity.Z = (float)(clsRandomHelper.Random.NextDouble() - 0.5) * sidewaysVelocityRange;
            lTrailEmitter = new TrailEmitter(trailParticlesPerSecond, position);
            projectileTrailParticleSystem.AddEmitter(lTrailEmitter);
        }
        public bool Update(GameTime gameTime) {
            float elapsedTime = (float)gameTime.ElapsedGameTime.TotalSeconds;
            position += velocity * elapsedTime;
            velocity.Y -= elapsedTime * gravity;
            age += elapsedTime;
            lTrailEmitter.Position = position;
            lTrailEmitter.Update(gameTime);
            if(age > projectileLifespan) {
                for(int i = 0; i < numExplosionParticles; i++)
                    explosionParticleSystem.AddParticle(
                        position,
                        clsRandomHelper.ColorBetween(Color.DarkGray, Color.Gray),
                        velocity * 0.01f + new Vector3(clsRandomHelper.FloatBetween(-30, 30), clsRandomHelper.FloatBetween(30, -10), clsRandomHelper.FloatBetween(-30, 30)) * 0.05f,
                        clsRandomHelper.FloatBetween(-0.01f, 0.01f),
                        TimeSpan.FromSeconds(clsRandomHelper.IntBetween(1, 2)),
                        true,
                        clsRandomHelper.FloatBetween(0.0f, MathHelper.Pi),
                        0.1f);

                for(int i = 0; i < numExplosionSmokeParticles; i++)
                    explosionSmokeParticleSystem.AddParticle(
                        position,
                        clsRandomHelper.ColorBetween(Color.LightGray, Color.White),
                        velocity * 0.01f + new Vector3(clsRandomHelper.FloatBetween(-50, 50), clsRandomHelper.FloatBetween(40, -40), clsRandomHelper.FloatBetween(-50, 50)) * 0.05f,
                        clsRandomHelper.FloatBetween(-0.01f, 0.01f),
                        TimeSpan.FromSeconds(1),
                        true,
                        clsRandomHelper.FloatBetween(0.0f, MathHelper.Pi),
                        0.25f);

                return false;
            }
            return true;
        }
    }
}