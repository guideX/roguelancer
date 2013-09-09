// Roguelancer 0.1 Pre Alpha by Leon Aiossa
// http://www.team-nexgen.org
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Roguelancer.Particle.ParticleSystem;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework;
namespace Roguelancer.Particle.System.ParticleSystems {
    public class ExplosionSmokeParticleSystem : DynamicParticleSystem {
        public ExplosionSmokeParticleSystem(int maxCapacity, Texture2D texture) : base(maxCapacity, texture) {
        }
        public override void Update(GameTime gameTime) {
            foreach (DynamicParticle particle in liveParticles) {
                particle.color = Color.Lerp(particle.initialColor, new Color(1.0f, 1.0f, 1.0f, 0.0f), 1.0f - particle.Age.Value);
                particle.scale += 0.002f;
            }
            base.Update(gameTime);
        }
    }
}