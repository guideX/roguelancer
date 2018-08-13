using Roguelancer.Objects;
using System.Collections.Generic;
namespace Roguelancer.Models.Collection {
    /// <summary>
    /// Planet Collection Model
    /// </summary>
    public class PlanetCollectionModel {
        /// <summary>
        /// Planets
        /// </summary>
        public List<PlanetObject> Planets { get; set; } = new List<PlanetObject>();
    }
}