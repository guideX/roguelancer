// Roguelancer 0.1 Pre Alpha by Leon Aiossa
// http://www.team-nexgen.com
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework;
using Roguelancer.Particle.ParticleSystem;
namespace Roguelancer.Particle.System.ParticleSystems {
    public class SmokeParticleSystem : DynamicParticleSystem {
        public SmokeParticleSystem (int maxCapacity, Texture2D texture) : base(maxCapacity, texture) {
        }
        public override void Update(GameTime gameTime) {
            foreach (var particle in LiveParticles) {
                particle.Color = Color.Lerp(particle.InitialColor, new Color(1.0f, 1.0f, 1.0f, 0.0f), 1.0f - particle.Age.Value);
                particle.Scale += 0.002f;
            }
            base.Update(gameTime);
        }
    }
}