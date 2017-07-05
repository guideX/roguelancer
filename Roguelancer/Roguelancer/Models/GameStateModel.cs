

using Roguelancer.Enum;
namespace Roguelancer.Models {
    /// <summary>
    /// Game State Model
    /// </summary>
    public class GameStateModel {
        /// <summary>
        /// Docked Game State
        /// </summary>
        public DockedGameStateEnum DockedGameState { get; set; }
        /// <summary>
        /// Current Game State
        /// </summary>
        public GameStates CurrentGameState { get; set; }
        /// <summary>
        /// Last Game State
        /// </summary>
        public GameStates LastGameState { get; set; }
    }
}