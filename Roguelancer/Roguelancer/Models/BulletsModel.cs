using System;
using System.Collections.Generic;
using Roguelancer.Objects;
using Roguelancer.Interfaces;
namespace Roguelancer.Models {
    public class BulletsModel {
        public List<IBullet> Bullets { get; set; }
        public int RechargeRate { get; set; }
        public DateTime WeaponRechargedTime { get; set; }
        public bool AreBulletsAvailable { get; set; }
        /// <summary>
        /// Player Ship
        /// </summary>
        //public Ship PlayerShip { get; set; }
    }
}