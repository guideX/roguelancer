// Rougelancer 0.1 Pre Alpha by Leon Aiossa
// http://www.team-nexgen.org
using Microsoft.Xna.Framework;
using Rougelancer.Functionality;
using Rougelancer.Objects;
using Rougelancer.Particle;
using Rougelancer.Bloom;
using Rougelancer.Particle.System.ParticleSystems;
namespace Rougelancer {
    public class Rougelancer : clsGame {
        public Rougelancer() {
        }
#if WINDOWS
        static class Program {
            static void Main(string[] args) {
                using (Rougelancer game = new Rougelancer()) {
                    game.Run();
                }
            }
        }
#endif
    }
}