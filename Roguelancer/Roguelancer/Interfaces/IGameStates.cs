// Roguelancer 0.1 Pre Alpha by Leon Aiossa
// http://team-nexgen.com
using Roguelancer.Models;
namespace Roguelancer.Interfaces {
    /// <summary>
    /// Game States
    /// </summary>
    public interface IGameStates {
        /// <summary>
        /// Game State Model
        /// </summary>
        GameStateModel Model { get; set; }
    }
}