// Rougelancer 0.1 Pre Alpha by Leon Aiossa
// http://www.team-nexgen.org
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Xna.Framework;
using Tranquillity;
using Rougelancer.Particle.ParticleSystem;
namespace Tranquillity {
    public class clsFadeout : clsIParticleAffector {
        public void Affect(GameTime _GameTime, clsDynamicParticle particle) {
            if(particle.Age.HasValue) {
                particle.lColor = Color.Lerp(particle.lInitialColor, new Color(particle.lInitialColor.R, particle.lInitialColor.G, particle.lInitialColor.B, 0), 1.0f - particle.Age.Value);
            }
        }
    }
}
