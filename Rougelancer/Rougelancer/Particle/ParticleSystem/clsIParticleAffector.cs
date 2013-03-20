// Rougelancer 0.1 Pre Alpha by Leon Aiossa
// http://www.team-nexgen.org
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Xna.Framework;
namespace Rougelancer.Particle.ParticleSystem {
    public interface clsIParticleAffector {
        void Affect(GameTime gameTime, clsDynamicParticle particle);
    }
}