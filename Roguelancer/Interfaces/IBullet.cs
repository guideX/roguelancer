using System;
using Roguelancer.Models;
using Roguelancer.Objects;
namespace Roguelancer.Interfaces {
    /// <summary>
    /// Bullet
    /// </summary>
    public interface IBullet : IGame {
        /// <summary>
        /// Model
        /// </summary>
        GameModel Model { get; set; }
        /// <summary>
        /// Player Ship
        /// </summary>
        ShipObject PlayerShip { get; set; }
        /// <summary>
        /// Mass
        /// </summary>
        float Mass { get; set; }
        /// <summary>
        /// Thrust Force
        /// </summary>
        float ThrustForce { get; set; }
        /// <summary>
        /// Drag Factor
        /// </summary>
        float DragFactor { get; set; }
        /// <summary>
        /// Bullet Death Date
        /// </summary>
        DateTime DeathDate { get; set; }
        /// <summary>
        /// Limit Altitude
        /// </summary>
        bool LimitAltitude { get; set; }
        /// <summary>
        /// Bullet Thrust
        /// </summary>
        float BulletThrust { get; set; }
    }
}