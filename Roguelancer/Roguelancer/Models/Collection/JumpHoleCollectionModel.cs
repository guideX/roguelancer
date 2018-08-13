using Roguelancer.Objects;
using System.Collections.Generic;
namespace Roguelancer.Models.Collection {
    /// <summary>
    /// Jump Hole Collection Model
    /// </summary>
    public class JumpHoleCollectionModel {
        /// <summary>
        /// Jump Holes
        /// </summary>
        public List<JumpHoleObject> JumpHoles { get; set; } = new List<JumpHoleObject>();
    }
}