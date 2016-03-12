// Roguelancer 0.1 Pre Alpha by Leon Aiossa
// http://www.team-nexgen.com
using Microsoft.Xna.Framework;
namespace Roguelancer {
    public class Roguelancer : Game {
        public Roguelancer() {
        }
#if WINDOWS
        static class Program {
            static void Main(string[] args) {
                using(RoguelancerGame game = new RoguelancerGame()) {
                    game.Run();
                }
            }
        }
#endif
    }
}