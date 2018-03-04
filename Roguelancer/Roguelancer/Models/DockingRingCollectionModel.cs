using Roguelancer.Objects;
using System.Collections.Generic;
namespace Roguelancer.Models {
    /// <summary>
    /// Docking Ring Collection Model
    /// </summary>
    public class DockingRingCollectionModel {
        /// <summary>
        /// Docking Rings
        /// </summary>
        public List<DockingRingObject> DockingRings { get; set; } = new List<DockingRingObject>()
    }
}