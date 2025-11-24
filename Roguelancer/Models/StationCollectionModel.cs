using Roguelancer.Objects;
using System.Collections.Generic;
namespace Roguelancer.Models {
    /// <summary>
    /// Station Collection Model
    /// </summary>
    public class StationCollectionModel {
        /// <summary>
        /// Stations
        /// </summary>
        public List<StationObject> Stations { get; set; }
        /// <summary>
        /// Station Collection Model
        /// </summary>
        public StationCollectionModel() {
            Stations = new List<StationObject>();
        }
    }
}