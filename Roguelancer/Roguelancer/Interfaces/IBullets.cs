using Roguelancer.Functionality;
namespace Roguelancer.Interfaces {
    interface IBullets : IGame {
        void Shoot(RoguelancerGame game);
    }
}