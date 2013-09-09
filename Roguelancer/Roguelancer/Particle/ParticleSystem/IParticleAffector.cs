// Roguelancer 0.1 Pre Alpha by Leon Aiossa
// http://www.team-nexgen.org
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Xna.Framework;
namespace Roguelancer.Particle.ParticleSystem {
    public interface IParticleAffector {
        void Affect(GameTime gameTime, DynamicParticle particle);
    }
}