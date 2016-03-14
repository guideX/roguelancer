using Roguelancer.Enum;
namespace Roguelancer.Interfaces {
    /// <summary>
    /// Game States
    /// </summary>
    public interface IGameStates {
        /// <summary>
        /// Docked Game State Enum
        /// </summary>
        DockedGameStateEnum DockedGameState { get; set; }
        /// <summary>
        /// Current Game State
        /// </summary>
        GameStates CurrentGameState { get; set; }
        /// <summary>
        /// Last Game State
        /// </summary>
        GameStates LastGameState { get; set; }
    }
}