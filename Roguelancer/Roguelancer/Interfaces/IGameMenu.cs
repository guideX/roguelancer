// Roguelancer 0.1 Pre Alpha by Leon Aiossa
// http://team-nexgen.com
using Roguelancer.Models;
namespace Roguelancer.Interfaces {
    /// <summary>
    /// Game Menu
    /// </summary>
    public interface IGameMenu : IGame {
        /// <summary>
        /// Model
        /// </summary>
        GameMenuModel Model { get; set; }
    }
}