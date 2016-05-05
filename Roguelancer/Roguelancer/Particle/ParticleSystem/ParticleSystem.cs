// Roguelancer 0.1 Pre Alpha by Leon Aiossa
// http://team-nexgen.com
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Roguelancer.Interfaces;
namespace Roguelancer.Particle.ParticleSystem {
    /// <summary>
    /// Particle System
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public abstract class ParticleSystem<T> : IParticleSystem where T : IParticle, new() {
        /// <summary>
        /// Live Particles
        /// </summary>
        protected List<T> LiveParticles;
        /// <summary>
        /// Dead Particles
        /// </summary>
        protected Stack<T> DeadParticles;
        /// <summary>
        /// Enabled
        /// </summary>
        public bool Enabled { get; set; }
        /// <summary>
        /// Texture
        /// </summary>
        private Texture2D _texture;
        /// <summary>
        /// Texture Origin
        /// </summary>
        private Vector2 _textureOrigin;
        /// <summary>
        /// Texture
        /// </summary>
        public Texture2D Texture {
            get {
                return _texture;
            }
        }
        /// <summary>
        /// Texture Origin
        /// </summary>
        public Vector2 TextureOrigin {
            get {
                return _textureOrigin;
            }
        }
        /// <summary>
        /// This
        /// </summary>
        /// <param name="index"></param>
        /// <returns></returns>
        public IParticle this[int index] {
            get {
                return LiveParticles[index];
            }
        }
        /// <summary>
        /// Capacity
        /// </summary>
        public int Capacity {
            get {
                return LiveParticles.Capacity;
            }
        }
        /// <summary>
        /// Particle Count
        /// </summary>
        public int ParticleCount {
            get {
                return LiveParticles.Count;
            }
        }
        /// <summary>
        /// Particle System
        /// </summary>
        /// <param name="maxCapacity"></param>
        /// <param name="texture"></param>
        public ParticleSystem(int maxCapacity, Texture2D texture) {
            LiveParticles = new List<T>(maxCapacity);
            DeadParticles = new Stack<T>(maxCapacity);
            for (var i = 0; i < maxCapacity; i++) {
                DeadParticles.Push(new T());
            }
            _texture = texture;
            _textureOrigin = new Vector2(Texture.Width / 2.0f, Texture.Height / 2.0f);
            Enabled = true;
        }
        /// <summary>
        /// Remove At
        /// </summary>
        /// <param name="index"></param>
        /// <returns></returns>
        public bool RemoveAt(int index) {
            if (index < LiveParticles.Count) {
                DeadParticles.Push(LiveParticles[index]);
                LiveParticles[index] = LiveParticles[LiveParticles.Count - 1];
                LiveParticles.RemoveAt(LiveParticles.Count - 1);
                return true;
            } else {
                return false;
            }
        }
        /// <summary>
        /// Clear
        /// </summary>
        public void Clear() {
            for (var i = LiveParticles.Count - 1; i >= 0; i--) {
                DeadParticles.Push(LiveParticles[i]);
                LiveParticles.RemoveAt(i);
            }
        }
    }
}