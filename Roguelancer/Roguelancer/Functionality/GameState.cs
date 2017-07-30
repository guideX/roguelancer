using Roguelancer.Enum;
using Roguelancer.Interfaces;
using Roguelancer.Models;
namespace Roguelancer.Functionality {
    /// <summary>
    /// Game State
    /// </summary>
    public class GameState : IGameStates {
        #region "public properties"
        /// <summary>
        /// Game State Model
        /// </summary>
        public GameStateModel Model { get; set; }
        #endregion
        #region "public methods"
        /// <summary>
        /// Entry Point
        /// </summary>
        public GameState(RoguelancerGame game) {
            Model = new GameStateModel();
            Model.CurrentGameState = new GameStatesEnum();
            Model.CurrentGameState = GameStatesEnum.Menu;
            Model.DockedGameState = DockedGameStateEnum.Hanger;
        }
        #endregion
    }
}