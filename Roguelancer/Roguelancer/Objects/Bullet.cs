using Roguelancer.Models;
using System;
using Roguelancer.Functionality;
using Microsoft.Xna.Framework;
using Roguelancer.Interfaces;
namespace Roguelancer.Objects {
    public class Bullet : IGame {
        /// <summary>
        /// Model
        /// </summary>
        public GameModel Model { get; set; }
        /// <summary>
        /// Bullet Model
        /// </summary>
        public BulletModel BulletModel { get; set; }
        /// <summary>
        /// Entry Point
        /// </summary>
        /// <param name="texture"></param> 
        public Bullet(Ship playerShipModel, RoguelancerGame game, Vector3 startupPosition, Vector3 startupModelRotation, int deathSeconds = 3, int scale = 3, string modelPath = "bullet") {
            try {
                BulletModel = new BulletModel();
                BulletModel.PlayerShip = playerShipModel;
                BulletModel.DeathDate = DateTime.Now.AddSeconds(deathSeconds);
                Model = new GameModel(game);
                Model.useScale = true;
                Model.particleSystemEnabled = true;
                Model.scale = scale;
                Model.modelMode = Enum.ModelModeEnum.bullet;
                Model.worldObject = new Settings.ModelWorldObjects(
                    BulletModel.PlayerShip.model.position + startupPosition,
                    startupModelRotation,
                    new Settings.SettingsModelObject(
                        modelPath,
                        Settings.ModelType.Bullet,
                        true,
                        13
                    ),
                    1,
                    BulletModel.PlayerShip.model.up,
                    BulletModel.PlayerShip.model.right,
                    BulletModel.PlayerShip.model.velocity,
                    BulletModel.PlayerShip.model.currentThrust,
                    BulletModel.PlayerShip.model.direction
                );
                Initialize(game);
                LoadContent(game);
            } catch {
                throw;
            }
        }
        /// <summary>
        /// Initialize
        /// </summary>
        /// <param name="game"></param>
        public void Initialize(RoguelancerGame game) {
            try {
                Model.Initialize(game);
            } catch {
                throw;
            }
        }
        /// <summary>
        /// Load Content
        /// </summary>
        /// <param name="game"></param>
        public void LoadContent(RoguelancerGame game) {
            try {
                Model.LoadContent(game);
            } catch {
                throw;
            }
        }
        /// <summary>
        /// Update
        /// </summary>
        /// <param name="game"></param>
        public void Update(RoguelancerGame game) {
            try {
                Vector3 force, acceleration;
                var elapsed = (float)game.gameTime.ElapsedGameTime.TotalSeconds;
                var rotationAmount = new Vector2();
                if (BulletModel.PlayerShip == null) {
                    BulletModel.PlayerShip = game.objects.ships.GetPlayerShip(game);
                }
                Model.currentThrust = .5f + BulletModel.PlayerShip.model.currentThrust;
                Model.rotation = rotationAmount;
                Model.UpdatePosition();
                force = Model.direction * Model.currentThrust * BulletModel.ThrustForce;
                acceleration = force / BulletModel.Mass;
                Model.velocity += acceleration * elapsed;
                Model.velocity *= BulletModel.DragFactor;
                Model.position += Model.velocity * elapsed;
                if (BulletModel.LimitAltitude == true) {
                    Model.position.Y = Math.Max(Model.position.Y, Model.minimumAltitude);
                }
                Model.Update(game);
            } catch {
                throw;
            }
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
    }
}