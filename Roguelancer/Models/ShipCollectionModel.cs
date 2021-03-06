﻿using Roguelancer.Objects;
using System.Collections.Generic;
namespace Roguelancer.Models {
    /// <summary>
    /// Ship Collection Model
    /// </summary>
    public class ShipCollectionModel {
        /// <summary>
        /// Ships
        /// </summary>
        public List<ShipObject> Ships { get; set; }
        /// <summary>
        /// Ship Collection Model
        /// </summary>
        public ShipCollectionModel() {
            Ships = new List<ShipObject>();
        }
    }
}