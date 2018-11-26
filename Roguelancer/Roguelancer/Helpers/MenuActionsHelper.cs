using Roguelancer.Enum;
namespace Roguelancer.Helpers {
    /// <summary>
    /// Menu Actions Helper
    /// </summary>
    public static class MenuActionsHelper {
        /// <summary>
        /// Start Playing Menu
        /// </summary>
        /// <param name="game"></param>
        public static void StartPlaying(RoguelancerGame game) {
            game.GameState.Model.LastGameState = game.GameState.Model.CurrentGameState;
            game.GameState.Model.CurrentGameState = GameStatesEnum.Playing;
        }
        /// <summary>
        /// Exit Menu
        /// </summary>
        /// <param name="game"></param>
        public static void ExitMenu(RoguelancerGame game) {
            game.GameState.Model.LastGameState = game.GameState.Model.CurrentGameState;
            game.GameState.Model.CurrentGameState = GameStatesEnum.Menu;
        }
        /// <summary>
        /// Goto Menu
        /// </summary>
        /// <param name="game"></param>
        public static void GotoMenu(RoguelancerGame game) {
            game.GameState.Model.LastGameState = game.GameState.Model.CurrentGameState;
            game.GameState.Model.CurrentGameState = GameStatesEnum.Menu;
        }
    }
}