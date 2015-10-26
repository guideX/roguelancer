// Roguelancer 0.1 Pre Alpha by Leon Aiossa
// http://www.team-nexgen.org
using System.Collections.Generic;
namespace Roguelancer.Models {
    /// <summary>
    /// Commodity Model
    /// </summary>
    public class CommodityModel {
        /// <summary>
        /// Commodity Id
        /// </summary>
        public int CommodityId { get; set; }
        /// <summary>
        /// Description
        /// </summary>
        public string Description { get; set; }
        /// <summary>
        /// Body
        /// </summary>
        public string Body { get; set; }
        /// <summary>
        /// Prices
        /// </summary>
        public List<StationPriceModel> Prices { get; set; }
    }
}