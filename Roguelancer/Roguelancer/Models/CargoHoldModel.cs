// Roguelancer 0.1 Pre Alpha by Leon Aiossa
// http://team-nexgen.com
using System.Collections.Generic;
namespace Roguelancer.Models {
    /// <summary>
    /// Cargo Hold Model
    /// </summary>
    public class CargoHoldModel {
        /// <summary>
        /// Commodities
        /// </summary>
        public List<CommodityModel> Commodities { get; set; }
        /// <summary>
        /// Entry Point
        /// </summary>
        public CargoHoldModel() {
            Commodities = new List<CommodityModel>();
        }
    }
}