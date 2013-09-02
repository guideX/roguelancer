// Roguelancer 0.1 Pre Alpha by Leon Aiossa
// http://www.team-nexgen.org
using Microsoft.Xna.Framework;
using Roguelancer.Functionality;
using Roguelancer.Objects;
using Roguelancer.Particle;
using Roguelancer.Bloom;
using Roguelancer.Particle.System.ParticleSystems;
namespace Roguelancer {
    public class Roguelancer : Game {
        public Roguelancer() {
        }
#if WINDOWS
        static class Program {
            static void Main(string[] args) {
                using(RoguelancerGame _Game = new RoguelancerGame()) {
                    _Game.Run();
                }
            }
        }
#endif
    }
}