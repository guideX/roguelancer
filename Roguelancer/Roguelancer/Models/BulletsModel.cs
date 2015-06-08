using System;
using System.Collections.Generic;
using Roguelancer.Objects;
namespace Roguelancer.Models {
    public class BulletsModel {
        public List<Bullet> Bullets { get; set; }
        public int RechargeRate { get; set; }
        public DateTime WeaponRechargedTime { get; set; }
        public bool AreBulletsAvailable { get; set; }
        /// <summary>
        /// Player Ship
        /// </summary>
        public Ship PlayerShip { get; set; }
    }
}