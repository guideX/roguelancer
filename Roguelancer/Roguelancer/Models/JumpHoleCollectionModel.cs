

using Roguelancer.Objects;
using System.Collections.Generic;
namespace Roguelancer.Models {
    /// <summary>
    /// Jump Hole Collection Model
    /// </summary>
    public class JumpHoleCollectionModel {
        /// <summary>
        /// Jump Holes
        /// </summary>
        public List<JumpHole> JumpHoles { get; set; }
        /// <summary>
        /// Entry Point
        /// </summary>
        public JumpHoleCollectionModel() {
            JumpHoles = new List<JumpHole>();
        }
    }
}