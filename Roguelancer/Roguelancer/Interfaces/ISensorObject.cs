// Roguelancer 0.1 Pre Alpha by Leon Aiossa
// http://www.team-nexgen.org
using Roguelancer.Models;
namespace Roguelancer.Interfaces {
    public interface ISensorObject : IGame {
        #region "public variables"
        /// <summary>
        /// Description
        /// </summary>
        string Description { get; set; }
        /// <summary>
        /// Game Model
        /// </summary>
        GameModel Model { get; set; }
        #endregion
    }
}