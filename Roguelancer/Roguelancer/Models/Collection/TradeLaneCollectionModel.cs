using Roguelancer.Objects;
using System.Collections.Generic;
namespace Roguelancer.Models.Collection {
    /// <summary>
    /// Trade Lane Collection Model
    /// </summary>
    public class TradeLaneCollectionModel {
        /// <summary>
        /// Trade Lanes
        /// </summary>
        public List<TradeLaneObject> TradeLanes { get; set; } = new List<TradeLaneObject>();
    }
}