// Roguelancer 0.1 Pre Alpha by Leon Aiossa
// http://team-nexgen.com
using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Roguelancer.Interfaces;
namespace Roguelancer.Particle.ParticleSystem {
    /// <summary>
    /// Dynamic Particle System
    /// </summary>
    public class DynamicParticleSystem : ParticleSystem<DynamicParticle> {
        /// <summary>
        /// Emitters
        /// </summary>
        protected List<IParticleEmitter> emitters;
        /// <summary>
        /// Affectors
        /// </summary>
        protected List<IParticleAffector> affectors;
        /// <summary>
        /// Dynamic Particle System
        /// </summary>
        /// <param name="maxCapacity"></param>
        /// <param name="texture"></param>
        public DynamicParticleSystem(int maxCapacity, Texture2D texture) : base(maxCapacity, texture) {
            emitters = new List<IParticleEmitter>();
            affectors = new List<IParticleAffector>();
        }
        /// <summary>
        /// Update
        /// </summary>
        /// <param name="gameTime"></param>
        public virtual void Update(GameTime gameTime) {
            foreach(var emitter in emitters) {
                emitter.Update(gameTime);
            }
            for(int i = 0; i < LiveParticles.Count; i++) {
                var particle = LiveParticles[i];
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
                    particle.Position += particle.Velocity.Value;
                }
                if(particle.Rotation.HasValue) {
                    particle.Angle += particle.Rotation.Value;
                }
                if(particle.IsAffectable) {
                    foreach(var affector in affectors) {
                        affector.Affect(gameTime, particle);
                    }
                }
            }
        }
        /// <summary>
        /// Add Emitter
        /// </summary>
        /// <param name="emitter"></param>
        public void AddEmitter(IParticleEmitter emitter) {
            emitter.ParticleSystem = this;
            emitters.Add(emitter);
        }
        /// <summary>
        /// Remove Emitter
        /// </summary>
        /// <param name="emitter"></param>
        public void RemoveEmitter(IParticleEmitter emitter) {
            emitters.Remove(emitter);
        }
        /// <summary>
        /// Add Affector
        /// </summary>
        /// <param name="affector"></param>
        public void AddAffector(IParticleAffector affector) {
            affectors.Add(affector);
        }
        /// <summary>
        /// Add Particle
        /// </summary>
        /// <param name="position"></param>
        /// <param name="color"></param>
        /// <param name="velocity"></param>
        /// <param name="rotation"></param>
        /// <param name="lifespan"></param>
        public void AddParticle(Vector3 position, Color color, Vector3? velocity, float? rotation, TimeSpan? lifespan) {
            AddParticle(position, color, velocity, rotation, lifespan, true, 0.0f, 1.0f);
        }
        /// <summary>
        /// Add Particle
        /// </summary>
        /// <param name="position"></param>
        /// <param name="color"></param>
        /// <param name="velocity"></param>
        /// <param name="rotation"></param>
        /// <param name="lifespan"></param>
        /// <param name="isAffectable"></param>
        /// <param name="angle"></param>
        /// <param name="scale"></param>
        public void AddParticle(Vector3 position, Color color, Vector3? velocity, float? rotation, TimeSpan? lifespan, bool isAffectable, float angle, float scale) {
            if(DeadParticles.Count != 0) {
                var particle = DeadParticles.Pop();
                particle.InitialPosition = particle.Position = position;
                particle.InitialVelocity = particle.Velocity = velocity;
                particle.InitialColor = particle.Color = color;
                particle.InitialAngle = particle.Angle = angle;
                particle.InitialRotation = particle.Rotation = rotation;
                particle.InitialScale = particle.Scale = scale;
                particle.IsAffectable = isAffectable;
                particle.lifespan = lifespan;
                LiveParticles.Add(particle);
            }
        }
    }
}