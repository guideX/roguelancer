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
        /// Bullet Model
        /// </summary>
        public BulletModel BulletModel { get; set; }
        #endregion
        #region "public methods"
        /// <summary>
        /// Entry Point
        /// </summary>
        /// <param name="texture"></param> 
        public BulletObject(ShipObject playerShipModel, RoguelancerGame game, Vector3 startupPosition, int deathSeconds = 3, int scale = 3, string modelPath = "bullet", float bulletThrust = .5f, ParticleSystemSettingsModel particleSystemSettings = null) {
            Reset(playerShipModel, game, startupPosition, deathSeconds, scale, modelPath, bulletThrust, particleSystemSettings);
        }
        /// <summary>
        /// Initialize
        /// </summary>
        /// <param name="game"></param>
        public void Initialize(RoguelancerGame game) {
            BulletModel.Model.Initialize(game);
        }
        /// <summary>
        /// Load Content
        /// </summary>
        /// <param name="game"></param>
        public void LoadContent(RoguelancerGame game) {
            BulletModel.Model.LoadContent(game);
        }
        /// <summary>
        /// Update
        /// </summary>
        /// <param name="game"></param>
        public void Update(RoguelancerGame game) {
            Vector3 force, acceleration;
            var elapsed = (float)game.GameTime.ElapsedGameTime.TotalSeconds;
            var rotationAmount = new Vector2();
            if (BulletModel.PlayerShip == null) { BulletModel.PlayerShip = ShipHelper.GetPlayerShip(game.Objects.Model); }
            BulletModel.Model.CurrentThrust = BulletModel.BulletThrust + BulletModel.PlayerShip.Model.CurrentThrust;
            BulletModel.Model.Rotation = rotationAmount;
            BulletModel.Model.UpdatePosition();
            force = BulletModel.Model.Direction * BulletModel.Model.CurrentThrust * BulletModel.ThrustForce;
            acceleration = force / BulletModel.Mass;
            BulletModel.Model.Velocity += acceleration * elapsed;
            BulletModel.Model.Velocity *= BulletModel.DragFactor;
            BulletModel.Model.Position += BulletModel.Model.Velocity * elapsed;
            if (BulletModel.LimitAltitude) {
                BulletModel.Model.Position.Y = Math.Max(BulletModel.Model.Position.Y, BulletModel.Model.MinimumAltitude);
            }
            BulletModel.Model.Update(game);
        }
        /// <summary>
        /// Draw
        /// </summary>
        /// <param name="game"></param>
        public void Draw(RoguelancerGame game) {
            if (BulletModel.Model != null) {
                BulletModel.Model.Draw(game);
            }
        }
        /// <summary>
        /// Dispose
        /// </summary>
        public void Dispose(RoguelancerGame game) {
            BulletModel.Model.Dispose(game);
            BulletModel = new BulletModel(game);
        }
        /// <summary>
        /// Reset
        /// </summary>
        /// <param name="game"></param>
        public void Reset(ShipObject playerShipModel, RoguelancerGame game, Vector3 startupPosition, int deathSeconds = 3, int scale = 3, string modelPath = "bullet", float bulletThrust = .5f, ParticleSystemSettingsModel particleSystemSettings = null) {
            BulletModel = new BulletModel(game) {
                BulletThrust = bulletThrust,
                PlayerShip = playerShipModel,
                DeathDate = DateTime.Now.AddSeconds(deathSeconds),
                Model = new GameModel(game, particleSystemSettings, null)
            };
            //BulletModel.Model.UseScale = true;
            //BulletModel.Model.Scale = scale;
            BulletModel.Model.WorldObject = new WorldObjectsSettings(
                "bullet",
                "",
                BulletModel.PlayerShip.Model.Position + startupPosition,
                new Vector3(0f, 0f, 0f),
                new SettingsObjectModel(
                    modelPath,
                    ModelType.Bullet,
                    true,
                    13,
                    scale
                ),
                1,
                BulletModel.PlayerShip.Model.Up,
                BulletModel.PlayerShip.Model.Right,
                BulletModel.PlayerShip.Model.Velocity,
                BulletModel.PlayerShip.Model.CurrentThrust,
                BulletModel.PlayerShip.Model.Direction,
                0,
                0,
                false
            );
            Initialize(game);
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