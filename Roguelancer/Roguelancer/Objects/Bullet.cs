using System;
using System.Linq;
using Roguelancer.Models;
using Roguelancer.Functionality;
using Microsoft.Xna.Framework;
using Roguelancer.Interfaces;
using Roguelancer.Particle.System;
namespace Roguelancer.Objects {
    /// <summary>
    /// Bullet
    /// </summary>
    public class Bullet : IBullet {
        #region "public variables"
        /// <summary>
        /// Model
        /// </summary>
        public GameModel Model { get; set; }
        /// <summary>
        /// Bullet Model
        /// </summary>
        public BulletModel BulletModel { get; set; }
        /// <summary>
        /// Bullet Thrust
        /// </summary>
        private float _bulletThrust;
        #endregion
        #region "public functions"
        /// <summary>
        /// Entry Point
        /// </summary>
        /// <param name="texture"></param> 
        public Bullet(Ship playerShipModel, RoguelancerGame game, Vector3 startupPosition, int deathSeconds = 3, int scale = 3, string modelPath = "bullet", float bulletThrust = .5f, ParticleSystemSettingsModel particleSystemSettings = null) {
            try {
                _bulletThrust = bulletThrust;
                BulletModel = new BulletModel();
                BulletModel.PlayerShip = playerShipModel;
                BulletModel.DeathDate = DateTime.Now.AddSeconds(deathSeconds);
                Model = new GameModel(game, particleSystemSettings);
                Model.UseScale = true;
                Model.Scale = scale;
                Model.ModelMode = Enum.ModelModeEnum.Bullet;
                Model.WorldObject = new Settings.ModelWorldObjects(
                    BulletModel.PlayerShip.model.Position + startupPosition,
                    new Vector3(0f, 0f, 0f),
                    new Settings.SettingsModelObject(
                        modelPath,
                        Settings.ModelType.Bullet,
                        true,
                        13
                    ),
                    1,
                    BulletModel.PlayerShip.model.Up,
                    BulletModel.PlayerShip.model.Right,
                    BulletModel.PlayerShip.model.Velocity,
                    BulletModel.PlayerShip.model.CurrentThrust,
                    BulletModel.PlayerShip.model.Direction
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
                var elapsed = (float)game.GameTime.ElapsedGameTime.TotalSeconds;
                var rotationAmount = new Vector2();
                if (BulletModel.PlayerShip == null) {
                    BulletModel.PlayerShip = game.Objects.ships.Ships.Where(s => s.PlayerShipControl.UseInput).LastOrDefault();
                    //BulletModel.PlayerShip = game.Objects.ships.GetPlayerShip(game);
                }
                Model.CurrentThrust = _bulletThrust + BulletModel.PlayerShip.model.CurrentThrust;
                Model.Rotation = rotationAmount;
                Model.UpdatePosition();
                force = Model.Direction * Model.CurrentThrust * BulletModel.ThrustForce;
                acceleration = force / BulletModel.Mass;
                Model.Velocity += acceleration * elapsed;
                Model.Velocity *= BulletModel.DragFactor;
                Model.Position += Model.Velocity * elapsed;
                if (BulletModel.LimitAltitude == true) {
                    Model.Position.Y = Math.Max(Model.Position.Y, Model.MinimumAltitude);
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
            try {
                if (Model != null) {
                    Model.Draw(game);
                }
            } catch {
                throw;
            }
        }
        public void Dispose() {
            try {

            } catch {
                throw;
            }
        }
        #endregion
    }
}