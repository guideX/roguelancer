using System;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework;
using Roguelancer.Objects;
using System.Collections.Generic;
namespace Roguelancer.Models {
    public class BulletModel {
        /// <summary>
        /// Player Ship
        /// </summary>
        public Ship PlayerShip { get; set; }
        /// <summary>
        /// Mass
        /// </summary>
        public float Mass { get; set; }
        /// <summary>
        /// Thrust Force
        /// </summary>
        public float ThrustForce { get; set; }
        /// <summary>
        /// Drag Factor
        /// </summary>
        public float DragFactor { get; set; }
        /// <summary>
        /// Bullet Death Date
        /// </summary>
        public DateTime DeathDate { get; set; }
        /// <summary>
        /// Limit Altitude
        /// </summary>
        public bool LimitAltitude { get; set; }
        /// <summary>
        /// Bullet Model
        /// </summary>
        public BulletModel() {
            Mass = 1.0f;
            ThrustForce = 44000.0f;
            DragFactor = 0.97f;
        }
    }
}