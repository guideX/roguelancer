

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