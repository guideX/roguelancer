// Roguelancer 0.1 Pre Alpha by Leon Aiossa
// http://www.team-nexgen.org
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Xna.Framework;
using Roguelancer.Particle.ParticleSystem;
namespace Roguelancer.Particle.System.Affector {
    public class clsVelocityAffector : clsIParticleAffector{
        public Vector3 VelocityChange { get; set; }
        public clsVelocityAffector(Vector3 velocityChange) {
            VelocityChange = velocityChange;
        }
        public void Affect(GameTime gameTime, clsDynamicParticle particle) {
            if (particle.Velocity.HasValue) {
                particle.Velocity += (float)gameTime.ElapsedGameTime.TotalSeconds * VelocityChange;
            }
        }
    }
}