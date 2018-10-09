using Roguelancer.Enum;
using Roguelancer.Helpers;
using Roguelancer.Models;
namespace Roguelancer.Actions {
    /// <summary>
    /// Menu Actions
    /// </summary>
    public class MenuActions {
        /// <summary>
        /// Roguelancer Game
        /// </summary>
        private RoguelancerGame _game;
        /// <summary>
        /// Menu Actions
        /// </summary>
        public MenuActions(RoguelancerGame game) {
            _game = game;
        }
        /// <summary>
        /// Start Playing Menu
        /// </summary>
        /// <param name="game"></param>
        public void StartPlaying() {
            _game.GameState.Model.LastGameState = _game.GameState.Model.CurrentGameState;
            _game.GameState.Model.CurrentGameState = GameStatesEnum.Playing;
        }
        /// <summary>
        /// Exit Menu
        /// </summary>
        /// <param name="game"></param>
        public void ExitMenu() {
            _game.GameState.Model.LastGameState = _game.GameState.Model.CurrentGameState;
            _game.GameState.Model.CurrentGameState = GameStatesEnum.Menu;
        }
        /// <summary>
        /// Goto Menu
        /// </summary>
        /// <param name="game"></param>
        public void GotoMenu() {
            _game.GameState.Model.LastGameState = _game.GameState.Model.CurrentGameState;
            _game.GameState.Model.CurrentGameState = GameStatesEnum.Menu;
        }
    }
}