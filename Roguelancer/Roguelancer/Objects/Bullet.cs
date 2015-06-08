using Roguelancer.Models;
using System;
using Roguelancer.Functionality;
using Microsoft.Xna.Framework;
namespace Roguelancer.Objects {
    public class Bullet {
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
        public Bullet(RoguelancerGame game) {
            BulletModel = new BulletModel();
        }
        /// <summary>
        /// Initialize
        /// </summary>
        /// <param name="game"></param>
        public void Initialize(RoguelancerGame game) {
            Model.Initialize(game);
            Model.modelMode = Enum.ModelModeEnum.bullet;
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
            float elapsed = (float)game.gameTime.ElapsedGameTime.TotalSeconds;
            Vector2 rotationAmount = new Vector2();
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