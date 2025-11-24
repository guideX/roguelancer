using Roguelancer.Models;
namespace Roguelancer.Interfaces {
    /// <summary>
    /// Player Ship Control
    /// </summary>
    public interface IPlayerShipControl {
        /// <summary>
        /// Update Model
        /// </summary>
        /// <param name="model"></param>
        /// <param name="game"></param>
        void UpdateModel(GameModel model, RoguelancerGame game);
        /// <summary>
        /// Player Ship Model
        /// </summary>
        PlayerShipControlModel Model { get; set; }
    }
}