// Roguelancer 0.1 Pre Alpha by Leon Aiossa
// http://www.team-nexgen.org
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework;
using Roguelancer.Particle.ParticleSystem;
using Roguelancer.Interfaces;
using Roguelancer.Functionality;
namespace Roguelancer.Particle.System.ParticleSystems {
    public class ExplosionParticleSystem : DynamicParticleSystem, IGame {
        public ExplosionParticleSystem(int maxCapacity, Texture2D texture) : base(maxCapacity, texture) {
        }
        public void Initialize(RoguelancerGame game) {
        }
        public void LoadContent(RoguelancerGame game) {
        }
        public override void Update(GameTime gameTime) {
            foreach (DynamicParticle particle in liveParticles) {
                particle.color = Color.Lerp(particle.initialColor, new Color(1.0f, 1.0f, 1.0f, 0.0f), 1.0f - particle.Age.Value);
                particle.scale += 0.005f;
            }
            base.Update(gameTime);
        }
        public void Update(RoguelancerGame game) {
        }
        public void Draw(RoguelancerGame game) {
        }
    }
}