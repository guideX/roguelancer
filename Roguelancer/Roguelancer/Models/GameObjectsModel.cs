﻿using Roguelancer.Interfaces;
using Roguelancer.Objects;
using Roguelancer.Particle;
namespace Roguelancer.Models {
    /// <summary>
    /// Game Objects Model
    /// </summary>
    public class GameObjectsModel {
        /// <summary>
        /// Ships
        /// </summary>
        public IShipCollection Ships { get; set; }
        /// <summary>
        /// Bullets
        /// </summary>
        public Bullets Bullets { get; set; }
        /// <summary>
        /// Trade Lanes
        /// </summary>
        public TradeLaneCollection TradeLanes { get; set; }
        /// <summary>
        /// Stations
        /// </summary>
        public StationCollection Stations { get; set; }
        /// <summary>
        /// Planets
        /// </summary>
        public PlanetCollection Planets { get; set; }
        /// <summary>
        /// Stars
        /// </summary>
        public Starfields Stars { get; set; }
        /// <summary>
        /// Jump Holes
        /// </summary>
        public JumpHoleCollection JumpHoles { get; set; }
    }
}