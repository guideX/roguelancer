// Roguelancer 0.1 Pre Alpha by Leon Aiossa
// http://www.team-nexgen.org
using System.Collections;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
namespace Roguelancer.Particle.ParticleSystem {
    public abstract class ParticleSystem<T> : clsIParticleSystem where T : clsIParticle, new() {
        protected List<T> lLiveParticles;
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
        public clsIParticle this[int index] {
            get {
                return lLiveParticles[index];
            }
        }
        public int Capacity {
            get {
                return lLiveParticles.Capacity;
            }
        }
        public int ParticleCount {
            get {
                return lLiveParticles.Count;
            }
        }
        public ParticleSystem(int maxCapacity, Texture2D texture) {
            lLiveParticles = new List<T>(maxCapacity);
            lDeadParticles = new Stack<T>(maxCapacity);
            for(int i = 0; i < maxCapacity; i++) {
                lDeadParticles.Push(new T());
            }
            this.lTexture = texture;
            this.lTextureOrigin = new Vector2(Texture.Width / 2.0f, Texture.Height / 2.0f);
            Enabled = true;
        }
        public bool RemoveAt(int index) {
            if(index < lLiveParticles.Count) {
                lDeadParticles.Push(lLiveParticles[index]);
                lLiveParticles[index] = lLiveParticles[lLiveParticles.Count - 1];
                lLiveParticles.RemoveAt(lLiveParticles.Count - 1);
                return true;
            } else {
                return false;
            }
        }
        public void Clear() {
            for(int i = lLiveParticles.Count - 1; i >= 0; i--) {
                lDeadParticles.Push(lLiveParticles[i]);
                lLiveParticles.RemoveAt(i);
            }
        }
    }
}