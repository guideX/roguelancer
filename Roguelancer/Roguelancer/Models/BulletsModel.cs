// Roguelancer 0.1 Pre Alpha by Leon Aiossa
// http://team-nexgen.com
using System;
using System.Collections.Generic;
using Roguelancer.Interfaces;
using Roguelancer.Objects;
namespace Roguelancer.Models {
    /// <summary>
    /// Bullets Model
    /// </summary>
    public class BulletsModel {
        /// <summary>
        /// Player Ship
        /// </summary>
        public Ship PlayerShip { get; set; }
        /// <summary>
        /// Bullets
        /// </summary>
        public List<IBullet> Bullets { get; set; }
        /// <summary>
        /// Recharge Rate
        /// </summary>
        public int RechargeRate { get; set; }
        /// <summary>
        /// Weapons Recharged Time
        /// </summary>
        public DateTime WeaponRechargedTime { get; set; }
        /// <summary>
        /// Are Bullets Available
        /// </summary>
        public bool AreBulletsAvailable { get; set; }
    }
}