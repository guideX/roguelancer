// Roguelancer 0.1 Pre Alpha by Leon Aiossa
// http://www.team-nexgen.com
using Roguelancer.Models;
namespace Roguelancer.Interfaces {
    /// <summary>
    /// Sensor Object
    /// </summary>
    public interface ISensorObject : IGame {
        /// <summary>
        /// Game Model
        /// </summary>
        GameModel Model { get; set; }
    }
}