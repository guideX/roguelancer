// Roguelancer 0.1 Pre Alpha by Leon Aiossa
// http://team-nexgen.com
using Roguelancer.Objects;
using System.Collections.Generic;
namespace Roguelancer.Models {
    /// <summary>
    /// Ship Collection Model
    /// </summary>
    public class ShipCollectionModel {
        /// <summary>
        /// Ships
        /// </summary>
        public List<Ship> Ships { get; set; }
        /// <summary>
        /// Ship Collection Model
        /// </summary>
        public ShipCollectionModel() {
            Ships = new List<Ship>();
        }
    }
}