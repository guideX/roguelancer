using Roguelancer.Interfaces;
using System.Collections.Generic;
namespace Roguelancer.Models {
    /// <summary>
    /// Station Model
    /// </summary>
    public class StationModel {
        /// <summary>
        /// Docked Ships
        /// </summary>
        public List<ISensorObject> DockedShips { get; set; } = new List<ISensorObject>();
        /// <summary>
        /// Space Station ID
        /// </summary>
        public int StationID { get; set; }
    }
}
