// Roguelancer 0.1 Pre Alpha by Leon Aiossa
// http://team-nexgen.com
using Roguelancer.Settings;
namespace Roguelancer.Models {
    /// <summary>
    /// Trade Lane Model
    /// </summary>
    public class TradeLaneModel {
        /// <summary>
        /// Model
        /// </summary>
        public GameModel Model { get; set; }
        /// <summary>
        /// Trade Lane Model
        /// </summary>
        /// <param name="game"></param>
        public TradeLaneModel(RoguelancerGame game, ModelWorldObjects o) {
            Model = new GameModel(game, null) {
                WorldObject = o
            };
        }
    }
}