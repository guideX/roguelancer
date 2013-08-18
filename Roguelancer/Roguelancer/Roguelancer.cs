// Roguelancer 0.1 Pre Alpha by Leon Aiossa
// http://www.team-nexgen.org
using Microsoft.Xna.Framework;
using Roguelancer.Functionality;
using Roguelancer.Objects;
using Roguelancer.Particle;
using Roguelancer.Bloom;
using Roguelancer.Particle.System.ParticleSystems;
namespace Roguelancer {
    public class Roguelancer : clsGame {
        public Roguelancer() {
        }
#if WINDOWS
        static class Program {
            static void Main(string[] args) {
                using (Roguelancer game = new Roguelancer()) {
                    game.Run();
                }
            }
        }
#endif
    }
}