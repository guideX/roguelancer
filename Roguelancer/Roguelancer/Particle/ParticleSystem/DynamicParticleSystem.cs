// Roguelancer 0.1 Pre Alpha by Leon Aiossa
// http://www.team-nexgen.org
using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Roguelancer.Particle.ParticleSystem;
namespace Roguelancer.Particle.ParticleSystem {
    public class DynamicParticleSystem : ParticleSystem<DynamicParticle> {
        protected List<IParticleEmitter> emitters;
        protected List<IParticleAffector> affectors;
        public DynamicParticleSystem(int maxCapacity, Texture2D texture) : base(maxCapacity, texture) {
            emitters = new List<IParticleEmitter>();
            affectors = new List<IParticleAffector>();
        }
        public virtual void Update(GameTime gameTime) {
            foreach(IParticleEmitter emitter in emitters) {
                emitter.Update(gameTime);
            }
            for(int i = 0; i < liveParticles.Count; i++) {
                DynamicParticle particle = liveParticles[i];
                if(particle.remainingLifetime.HasValue) {
                    if(particle.remainingLifetime.Value.TotalMilliseconds > 0.0) {
                        particle.remainingLifetime -= gameTime.ElapsedGameTime;
                    } else {
                        RemoveAt(i);
                        i--;
                        continue;
                    }
                }
                if(particle.velocity.HasValue) {
                    particle.position += particle.velocity.Value;
                }
                if(particle.Rotation.HasValue) {
                    particle.angle += particle.Rotation.Value;
                }
                if(particle.IsAffectable) {
                    foreach(IParticleAffector affector in affectors) {
                        affector.Affect(gameTime, particle);
                    }
                }
            }
        }
        public void AddEmitter(IParticleEmitter emitter) {
            emitter.ParticleSystem = this;
            emitters.Add(emitter);
        }
        public void RemoveEmitter(IParticleEmitter emitter) {
            emitters.Remove(emitter);
        }
        public void AddAffector(IParticleAffector affector) {
            affectors.Add(affector);
        }
        public void AddParticle(Vector3 position, Color color, Vector3? velocity, float? rotation, TimeSpan? lifespan) {
            AddParticle(position, color, velocity, rotation, lifespan, true, 0.0f, 1.0f);
        }
        public void AddParticle(Vector3 position, Color color, Vector3? velocity, float? rotation, TimeSpan? lifespan, bool isAffectable, float angle, float scale) {
            if(lDeadParticles.Count != 0) {
                DynamicParticle particle = lDeadParticles.Pop();
                particle.initialPosition = particle.position = position;
                particle.initialVelocity = particle.velocity = velocity;
                particle.initialColor = particle.color = color;
                particle.initialAngle = particle.angle = angle;
                particle.InitialRotation = particle.Rotation = rotation;
                particle.initialScale = particle.scale = scale;
                particle.IsAffectable = isAffectable;
                particle.lifespan = lifespan;
                liveParticles.Add(particle);
            }
        }
    }
}