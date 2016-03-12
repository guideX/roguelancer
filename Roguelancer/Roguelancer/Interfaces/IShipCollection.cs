// Roguelancer 0.1 Pre Alpha by Leon Aiossa
// http://www.team-nexgen.com
using Roguelancer.Functionality;
using Roguelancer.Objects;
using System.Collections.Generic;
namespace Roguelancer.Interfaces {
    /// <summary>
    /// Ship Collection
    /// </summary>
    public interface IShipCollection : IGame {
        /// <summary>
        /// Ships
        /// </summary>
        List<Ship> Ships { get; set; }
        /// <summary>
        /// Reset
        /// </summary>
        /// <param name="game"></param>
        void Reset(RoguelancerGame game);
    }
}