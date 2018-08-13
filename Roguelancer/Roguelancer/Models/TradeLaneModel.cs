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
        public TradeLaneModel(RoguelancerGame game, WorldObjectsSettings o) {
            Model = new GameModel(game, null, null) {
                WorldObject = o
            };
        }
    }
}