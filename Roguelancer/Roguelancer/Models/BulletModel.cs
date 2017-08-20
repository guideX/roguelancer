using System;
using Roguelancer.Objects;
namespace Roguelancer.Models {
    /// <summary>
    /// Bullet Model
    /// </summary>
    public class BulletModel {
        /// <summary>
        /// Model
        /// </summary>
        public GameModel Model { get; set; }
        /// <summary>
        /// Player Ship
        /// </summary>
        public ShipObject PlayerShip { get; set; }
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
        /// Bullet Thrust
        /// </summary>
        public float BulletThrust { get; set; }
        /// <summary>
        /// Bullet Model
        /// </summary>
        public BulletModel(RoguelancerGame game) {
            Mass = game.Settings.Model.BulletMass;
            ThrustForce = game.Settings.Model.BulletThrusterForce;
            DragFactor = game.Settings.Model.BulletDragFactor;
        }
    }
}