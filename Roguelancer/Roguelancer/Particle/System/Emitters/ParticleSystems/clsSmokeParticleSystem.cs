// Roguelancer 0.1 Pre Alpha by Leon Aiossa
// http://www.team-nexgen.org
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework;
using Roguelancer.Particle.ParticleSystem;
namespace Roguelancer.Particle.System.ParticleSystems {
    public class clsSmokeParticleSystem : clsDynamicParticleSystem {
        public clsSmokeParticleSystem (int _MaxCapacity, Texture2D _Texture) : base(_MaxCapacity, _Texture) {
        }
        public override void Update(GameTime _GameTime) {
            foreach (clsDynamicParticle _Particle in lLiveParticles) {
                _Particle.lColor = Color.Lerp(_Particle.lInitialColor, new Color(1.0f, 1.0f, 1.0f, 0.0f), 1.0f - _Particle.Age.Value);
                _Particle.lScale += 0.002f;
            }
            base.Update(_GameTime);
        }
    }
}