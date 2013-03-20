// Rougelancer 0.1 Pre Alpha by Leon Aiossa
// http://www.team-nexgen.org
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Xna.Framework;
using Rougelancer.Particle.ParticleSystem;
namespace Rougelancer.Particle.System.Emitters {
    public class clsSmokePlumeEmitter : clsIParticleEmitter {
        public clsDynamicParticleSystem ParticleSystem { get; set; }
        public int EmissionRate { get; set; }
        public Vector3 Position { get; set; }
        private float particlesEmitted = 0.0f;
        public clsSmokePlumeEmitter(Vector3 position, int emissionRate) {
            Position = position;
            EmissionRate = emissionRate;
        }
        public void Update(GameTime gameTime) {
            particlesEmitted += (float)gameTime.ElapsedGameTime.TotalSeconds * (float)EmissionRate;
            int emittedCount = (int)particlesEmitted;
            if(emittedCount > 0) {
                Emit(emittedCount);
                particlesEmitted -= emittedCount;
            }
        }
        public void Emit(int particlesToEmit) {
            for(int i = 0; i < particlesToEmit; i++) {
                ParticleSystem.AddParticle(Position, Color.White, new Vector3(clsRandomHelper.FloatBetween(0.0f, -1.0f), clsRandomHelper.FloatBetween(0.2f, 1.5f), 0.0f), clsRandomHelper.FloatBetween(-0.01f, 0.1f), TimeSpan.FromSeconds(clsRandomHelper.IntBetween(3, 5)), true, 0.0f, clsRandomHelper.FloatBetween(0.05f, 0.1f));
            }
        }
    }
}