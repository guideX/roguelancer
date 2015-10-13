// Roguelancer 0.1 Pre Alpha by Leon Aiossa
// http://www.team-nexgen.org
using System.Collections;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Roguelancer.Interfaces;
namespace Roguelancer.Particle.ParticleSystem {
    public abstract class ParticleSystem<T> : IParticleSystem where T : IParticle, new() {
        protected List<T> liveParticles;
        protected Stack<T> lDeadParticles;
        public bool Enabled { get; set; }
        private Texture2D lTexture;
        private Vector2 lTextureOrigin;
        public Texture2D Texture {
            get {
                return lTexture;
            }
        }
        public Vector2 TextureOrigin {
            get {
                return lTextureOrigin;
            }
        }
        public IParticle this[int index] {
            get {
                return liveParticles[index];
            }
        }
        public int Capacity {
            get {
                return liveParticles.Capacity;
            }
        }
        public int ParticleCount {
            get {
                return liveParticles.Count;
            }
        }
        public ParticleSystem(int maxCapacity, Texture2D texture) {
            liveParticles = new List<T>(maxCapacity);
            lDeadParticles = new Stack<T>(maxCapacity);
            for(int i = 0; i < maxCapacity; i++) {
                lDeadParticles.Push(new T());
            }
            this.lTexture = texture;
            this.lTextureOrigin = new Vector2(Texture.Width / 2.0f, Texture.Height / 2.0f);
            Enabled = true;
        }
        public bool RemoveAt(int index) {
            if(index < liveParticles.Count) {
                lDeadParticles.Push(liveParticles[index]);
                liveParticles[index] = liveParticles[liveParticles.Count - 1];
                liveParticles.RemoveAt(liveParticles.Count - 1);
                return true;
            } else {
                return false;
            }
        }
        public void Clear() {
            for(int i = liveParticles.Count - 1; i >= 0; i--) {
                lDeadParticles.Push(liveParticles[i]);
                liveParticles.RemoveAt(i);
            }
        }
    }
}