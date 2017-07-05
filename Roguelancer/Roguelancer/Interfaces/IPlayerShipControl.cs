

using Roguelancer.Models;
namespace Roguelancer.Interfaces {
    /// <summary>
    /// Player Ship Control
    /// </summary>
    public interface IPlayerShipControl {
        /// <summary>
        /// Initialize
        /// </summary>
        /// <param name="game"></param>
        //void Initialize(RoguelancerGame game);
        /// <summary>
        /// Load Content
        /// </summary>
        /// <param name="game"></param>
        //void LoadContent(RoguelancerGame game);
        /// <summary>
        /// Update
        /// </summary>
        /// <param name="game"></param>
        //void Update(RoguelancerGame game);
        /// <summary>
        /// Use Input
        /// </summary>
        //bool UseInput { get; set; }
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