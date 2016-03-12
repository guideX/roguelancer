// Roguelancer 0.1 Pre Alpha by Leon Aiossa
// http://www.team-nexgen.com
using Roguelancer.Enum;
namespace Roguelancer.Functionality {
    /// <summary>
    /// Game State
    /// </summary>
    public class GameState {
        #region "public variables"
        /// <summary>
        /// Current Game State
        /// </summary>
        public GameStates CurrentGameState { get; set; }
        /// <summary>
        /// Last Game State
        /// </summary>
        public GameStates LastGameState { get; set; }
        #endregion
        #region "public functions"
        /// <summary>
        /// Entry Point
        /// </summary>
        public GameState() {
            CurrentGameState = new GameStates();
            CurrentGameState = GameStates.Menu;
        }
        #endregion
    }
}