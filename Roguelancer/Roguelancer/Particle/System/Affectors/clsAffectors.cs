// Roguelancer 0.1 Pre Alpha by Leon Aiossa
// http://www.team-nexgen.org
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Xna.Framework;
using Roguelancer.Particle.ParticleSystem;
namespace Roguelancer.Particle.System.Affectors {
    public class clsDecelerate : clsIParticleAffector {
        public void Affect(GameTime gameTime, clsDynamicParticle particle) {
            if(particle.Age.HasValue && particle.Velocity.HasValue && particle.InitialVelocity.HasValue) {
                particle.Velocity = Vector3.Lerp(particle.InitialVelocity.Value, Vector3.Zero, 1.0f - particle.Age.Value);
            }
        }
    }
}