// Rougelancer 0.1 Pre Alpha by Leon Aiossa
// http://www.team-nexgen.org
using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Rougelancer.Particle.ParticleSystem;
namespace Rougelancer.Particle.ParticleSystem {
    public class clsDynamicParticleSystem : ParticleSystem<clsDynamicParticle> {
        protected List<clsIParticleEmitter> emitters;
        protected List<clsIParticleAffector> affectors;
        public clsDynamicParticleSystem(int maxCapacity, Texture2D texture) : base(maxCapacity, texture) {
            emitters = new List<clsIParticleEmitter>();
            affectors = new List<clsIParticleAffector>();
        }
        public virtual void Update(GameTime gameTime) {
            foreach(clsIParticleEmitter emitter in emitters) {
                emitter.Update(gameTime);
            }
            for(int i = 0; i < lLiveParticles.Count; i++) {
                clsDynamicParticle particle = lLiveParticles[i];
                if(particle.RemainingLifetime.HasValue) {
                    if(particle.RemainingLifetime.Value.TotalMilliseconds > 0.0) {
                        particle.RemainingLifetime -= gameTime.ElapsedGameTime;
                    } else {
                        RemoveAt(i);
                        i--;
                        continue;
                    }
                }
                if(particle.Velocity.HasValue) {
                    particle.lPosition += particle.Velocity.Value;
                }
                if(particle.Rotation.HasValue) {
                    particle.lAngle += particle.Rotation.Value;
                }
                if(particle.IsAffectable) {
                    foreach(clsIParticleAffector affector in affectors) {
                        affector.Affect(gameTime, particle);
                    }
                }
            }
        }
        public void AddEmitter(clsIParticleEmitter emitter) {
            emitter.ParticleSystem = this;
            emitters.Add(emitter);
        }
        public void RemoveEmitter(clsIParticleEmitter emitter) {
            emitters.Remove(emitter);
        }
        public void AddAffector(clsIParticleAffector affector) {
            affectors.Add(affector);
        }
        public void AddParticle(Vector3 position, Color color, Vector3? velocity, float? rotation, TimeSpan? lifespan) {
            AddParticle(position, color, velocity, rotation, lifespan, true, 0.0f, 1.0f);
        }
        public void AddParticle(Vector3 position, Color color, Vector3? velocity, float? rotation, TimeSpan? lifespan, bool isAffectable, float angle, float scale) {
            if(lDeadParticles.Count != 0) {
                clsDynamicParticle particle = lDeadParticles.Pop();
                particle.lInitialPosition = particle.lPosition = position;
                particle.InitialVelocity = particle.Velocity = velocity;
                particle.lInitialColor = particle.lColor = color;
                particle.lInitialAngle = particle.lAngle = angle;
                particle.InitialRotation = particle.Rotation = rotation;
                particle.InitialScale = particle.lScale = scale;
                particle.IsAffectable = isAffectable;
                particle.Lifespan = lifespan;
                lLiveParticles.Add(particle);
            }
        }
    }
}