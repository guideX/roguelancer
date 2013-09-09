// Roguelancer 0.1 Pre Alpha by Leon Aiossa
// http://www.team-nexgen.org
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Xna.Framework;
using Roguelancer.Particle.ParticleSystem;
namespace Roguelancer.Particle.System.Affectors {
    public class Decelerate : IParticleAffector {
        public void Affect(GameTime gameTime, DynamicParticle particle) {
            if(particle.Age.HasValue && particle.velocity.HasValue && particle.initialVelocity.HasValue) {
                particle.velocity = Vector3.Lerp(particle.initialVelocity.Value, Vector3.Zero, 1.0f - particle.Age.Value);
            }
        }
    }
}