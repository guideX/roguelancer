// Roguelancer 0.1 Pre Alpha by Leon Aiossa
// http://team-nexgen.com
using Roguelancer.Objects;
using System.Collections.Generic;
namespace Roguelancer.Models {
    /// <summary>
    /// Planet Collection Model
    /// </summary>
    public class PlanetCollectionModel {
        /// <summary>
        /// Planets
        /// </summary>
        public List<Planet> Planets { get; set; }
        /// <summary>
        /// Planet Collection Model
        /// </summary>
        public PlanetCollectionModel() {
            Planets = new List<Planet>();
        }
    }
}