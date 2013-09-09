// Roguelancer 0.1 Pre Alpha by Leon Aiossa
// http://www.team-nexgen.org
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Xna.Framework;
using Tranquillity;
using Roguelancer.Particle.ParticleSystem;
namespace Tranquillity {
    public class Fadeout : IParticleAffector {
        public void Affect(GameTime _GameTime, DynamicParticle particle) {
            if(particle.Age.HasValue) {
                particle.color = Color.Lerp(particle.initialColor, new Color(particle.initialColor.R, particle.initialColor.G, particle.initialColor.B, 0), 1.0f - particle.Age.Value);
            }
        }
    }
}
