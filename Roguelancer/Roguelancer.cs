using Microsoft.Xna.Framework;
namespace Roguelancer {
    public class Roguelancer : Game {
        public Roguelancer() {
        }
#if WINDOWS
        static class Program {
            static void Main(string[] args) {
                using(var game = new RoguelancerGame()) {
                    game.Run();
                    var blah = "";
                    if (blah == "") {
                    }
                }
            }
        }
#endif
    }
}