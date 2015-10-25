// Roguelancer 0.1 Pre Alpha by Leon Aiossa
// http://www.team-nexgen.org
using Roguelancer.Functionality;
namespace Roguelancer.Interfaces {
    public interface IGameObjects : IGame {
        void Reset(RoguelancerGame game);
    }
}