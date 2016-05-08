// Roguelancer 0.1 Pre Alpha by Leon Aiossa
// http://team-nexgen.com
using Roguelancer.Objects;
using System.Collections.Generic;
namespace Roguelancer.Models {
    /// <summary>
    /// Trade Lane Collection Model
    /// </summary>
    public class TradeLaneCollectionModel {
        /// <summary>
        /// Trade Lanes
        /// </summary>
        public List<TradeLane> TradeLanes;
        /// <summary>
        /// Entry Point
        /// </summary>
        public TradeLaneCollectionModel() {
            TradeLanes = new List<TradeLane>();
        }
    }
}