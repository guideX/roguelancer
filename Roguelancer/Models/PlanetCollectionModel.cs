﻿using Roguelancer.Objects;
using System.Collections.Generic;
namespace Roguelancer.Models {
    /// <summary>
    /// Planet Collection Model
    /// </summary>
    public class PlanetCollectionModel {
        /// <summary>
        /// Planets
        /// </summary>
        public List<PlanetObject> Planets { get; set; }
        /// <summary>
        /// Planet Collection Model
        /// </summary>
        public PlanetCollectionModel() {
            Planets = new List<PlanetObject>();
        }
    }
}