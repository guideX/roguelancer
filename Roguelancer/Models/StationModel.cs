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
        /// Station Guid
        /// </summary>
        public string StationGuid { get; set; }
        /// <summary>
        /// Destination System
        /// </summary>
        public int? DestinationSystem { get; set; }
        /// <summary>
        /// Jump Hole ID
        /// </summary>
        public int? JumpHoleID { get; set; }
        /// <summary>
        /// Destination Jump Hole ID
        /// </summary>
        public int? JumpHoleTarget { get; set; }
    }
}
