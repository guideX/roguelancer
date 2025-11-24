using System;
using Microsoft.Xna.Framework;
using Roguelancer.Models;
using Roguelancer.Interfaces;
using Roguelancer.Helpers;
using Roguelancer.Settings;
using Roguelancer.Enum;
namespace Roguelancer.Objects {
    /// <summary>
    /// Bullet
    /// </summary>
    public class BulletObject : IBullet {
        #region "public properties"
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
        #endregion
        #region "public methods"
        /// <summary>
        /// Entry Point
        /// </summary>
        /// <param name="texture"></param> 
        public BulletObject(ShipObject playerShipModel, RoguelancerGame game, Vector3 startupPosition, int deathSeconds = 3, int scale = 3, string modelPath = "bullet", float bulletThrust = .5f, ParticleSystemSettingsModel particleSystemSettings = null) {
            Reset(playerShipModel, game, startupPosition, deathSeconds, scale, modelPath, bulletThrust, particleSystemSettings);
            Mass = game.Settings.Model.BulletMass;
            ThrustForce = game.Settings.Model.BulletThrusterForce;
            DragFactor = game.Settings.Model.BulletDragFactor;
        }
        /// <summary>
        /// Initialize
        /// </summary>
        /// <param name="game"></param>
        public void Initialize(RoguelancerGame game) {
            Model.Initialize(game);
        }
        /// <summary>
        /// Load Content
        /// </summary>
        /// <param name="game"></param>
        public void LoadContent(RoguelancerGame game) {
            Model.LoadContent(game);
        }
        /// <summary>
        /// Update
        /// </summary>
        /// <param name="game"></param>
        public void Update(RoguelancerGame game) {
            Vector3 force, acceleration;
            var elapsed = (float)game.GameTime.ElapsedGameTime.TotalSeconds;
            var rotationAmount = new Vector2();
            if (PlayerShip == null) { PlayerShip = ShipHelper.GetPlayerShip(game.Objects.Model); }
            Model.CurrentThrust = BulletThrust + PlayerShip.Model.CurrentThrust;
            Model.Rotation = rotationAmount;
            Model.UpdatePosition();
            force = Model.Direction * Model.CurrentThrust * ThrustForce;
            acceleration = force / Mass;
            Model.Velocity += acceleration * elapsed;
            Model.Velocity *= DragFactor;
            Model.Position += Model.Velocity * elapsed;
            if (LimitAltitude) {
                Model.Position.Y = Math.Max(Model.Position.Y, Model.MinimumAltitude);
            }
            Model.Update(game);
        }
        /// <summary>
        /// Draw
        /// </summary>
        /// <param name="game"></param>
        public void Draw(RoguelancerGame game) {
            if (Model != null) {
                Model.Draw(game);
            }
        }
        /// <summary>
        /// Dispose
        /// </summary>
        public void Dispose(RoguelancerGame game) {
            Model.Dispose(game);
            //BulletModel = new BulletModel(game);
        }
        /// <summary>
        /// Reset
        /// </summary>
        /// <param name="game"></param>
        public void Reset(ShipObject playerShipModel, RoguelancerGame game, Vector3 startupPosition, int deathSeconds = 3, int scale = 3, string modelPath = "bullet", float bulletThrust = .5f, ParticleSystemSettingsModel particleSystemSettings = null) {
            //BulletModel = new BulletModel(game) {
            BulletThrust = bulletThrust;
            PlayerShip = playerShipModel;
            DeathDate = DateTime.Now.AddSeconds(deathSeconds);
            Model = new GameModel(game, particleSystemSettings, null, ModelTypeEnum.Bullet, "Bullet") {
                WorldObject = new WorldObjectsSettings(
                "bullet",
                "",
                PlayerShip.Model.Position + startupPosition,
                new Vector3(0f, 0f, 0f),
                new SettingsObjectModel(
                    modelPath,
                    ModelTypeEnum.Bullet,
                    true,
                    13,
                    scale
                ),
                1,
                PlayerShip.Model.Up,
                PlayerShip.Model.Right,
                PlayerShip.Model.Velocity,
                PlayerShip.Model.CurrentThrust,
                PlayerShip.Model.Direction,
                0,
                0,
                false,
                null,
                null
            )
            };
            //Initialize(game);
            LoadContent(game);
        }
        /// <summary>
        /// Reset
        /// </summary>
        /// <param name="game"></param>
        public void Reset(RoguelancerGame game) {
        }
        #endregion
    }
}