// Roguelancer 0.1 Pre Alpha by Leon Aiossa
// http://www.team-nexgen.org
using Roguelancer.Functionality;
using Roguelancer.Models;
namespace Roguelancer.Interfaces {
    public interface IPlayerShipControl : IGame {
        /// <summary>
        /// Use Input
        /// </summary>
        bool UseInput { get; set; }
        /// <summary>
        /// Update Model
        /// </summary>
        /// <param name="model"></param>
        /// <param name="game"></param>
        void UpdateModel(GameModel model, RoguelancerGame game);
    }
}