// Rougelancer 0.1 Pre Alpha by Leon Aiossa
// http://www.team-nexgen.org
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework;
using Rougelancer.Particle.ParticleSystem;
namespace Rougelancer.Particle.System.ParticleSystems {
    public class clsExplosionParticleSystem : clsDynamicParticleSystem {
        public clsExplosionParticleSystem(int maxCapacity, Texture2D texture) : base(maxCapacity, texture) {

        }
        public override void Update(GameTime gameTime) {
            foreach (clsDynamicParticle particle in lLiveParticles) {
                particle.lColor = Color.Lerp(particle.lInitialColor, new Color(1.0f, 1.0f, 1.0f, 0.0f), 1.0f - particle.Age.Value);
                particle.lScale += 0.005f;
            }
            base.Update(gameTime);
        }
    }
}