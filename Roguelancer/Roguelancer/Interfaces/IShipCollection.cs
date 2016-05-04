// Roguelancer 0.1 Pre Alpha by Leon Aiossa
// http://www.team-nexgen.com
using Roguelancer.Models;
using Roguelancer.Objects;
namespace Roguelancer.Interfaces {
    /// <summary>
    /// Ship Collection
    /// </summary>
    public interface IShipCollection : IGame {
        /// <summary>
        /// Ships
        /// </summary>
        ShipCollectionModel Model { get; set; }
        /// <summary>
        /// Reset
        /// </summary>
        /// <param name="game"></param>
        void Reset(RoguelancerGame game);
    }
}