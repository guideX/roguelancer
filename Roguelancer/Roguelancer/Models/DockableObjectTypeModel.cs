using Roguelancer.Enum;
using Roguelancer.Interfaces;
using System.Collections.Generic;
namespace Roguelancer.Models {
    /// <summary>
    /// Station Model
    /// </summary>
    public class DockableObjectTypeModel {
        /// <summary>
        /// Docked Ships
        /// </summary>
        public List<ISensorObject> DockedShips { get; set; } = new List<ISensorObject>();
        /// <summary>
        /// Space Station ID
        /// </summary>
        //public int StationID { get; set; }
        public int ReferenceID { get; set; }
        /// <summary>
        /// Object Type
        /// </summary>
        public ModelTypeEnum ObjectType { get; set; }
    }
}
