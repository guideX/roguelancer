// Roguelancer 0.1 Pre Alpha by Leon Aiossa
// http://team-nexgen.com
using Roguelancer.Objects;
using Roguelancer.Settings;
using System.Collections.Generic;
namespace Roguelancer.Interfaces {
    /// <summary>
    /// IDockable
    /// </summary>
    public interface IDockable : IGame {
        /// <summary>
        /// Docked Ships
        /// </summary>
        List<ISensorObject> DockedShips { get; set; }
        /// <summary>
        /// Dock
        /// </summary>
        /// <param name="game"></param>
        /// <param name="ship"></param>
        void Dock(RoguelancerGame game, Ship ship, ModelWorldObjects worldObject);
        /// <summary>
        /// UnDoc
        /// </summary>
        /// <param name="game"></param>
        /// <param name="ship"></param>
        void UnDock(RoguelancerGame game, Ship ship, ModelWorldObjects worldObject);
        /// <summary>
        /// Commodities for Sale
        /// </summary>
        /// <param name="game"></param>
        /// <param name="id"></param>
        /// <param name="modelType"></param>
        /// <returns></returns>
        //List<StationPriceModel> CommoditiesForSale(RoguelancerGame game, int id, ModelType modelType);
    }
}