

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Roguelancer.Models;
namespace Roguelancer.Particle.ParticleSystem {
    /// <summary>
    /// Static Particle System
    /// </summary>
    public class StaticParticleSystem : ParticleSystem<StaticParticleModel> {
        /// <summary>
        /// Static Particle System
        /// </summary>
        /// <param name="maxCapacity"></param>
        /// <param name="texture"></param>
        public StaticParticleSystem(int maxCapacity, Texture2D texture) : base(maxCapacity, texture) {
        }
        /// <summary>
        /// Add Particle
        /// </summary>
        /// <param name="position"></param>
        /// <param name="color"></param>
        public void AddParticle(Vector3 position, Color color) {
            AddParticle(position, color, 0.0f, 1.0f);
        }
        /// <summary>
        /// Add Particle
        /// </summary>
        /// <param name="position"></param>
        /// <param name="color"></param>
        /// <param name="angle"></param>
        /// <param name="scale"></param>
        public void AddParticle(Vector3 position, Color color, float angle, float scale) {
            if(DeadParticles.Count != 0) {
                var particle = DeadParticles.Pop();
                particle.Position = position;
                particle.Color = color;
                particle.Angle = angle;
                particle.Scale = scale;
                LiveParticles.Add(particle);
            }
        }
    }
}