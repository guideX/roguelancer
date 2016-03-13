// Roguelancer 0.1 Pre Alpha by Leon Aiossa
// http://www.team-nexgen.com
using System;
using System.Linq;
using System.Collections.Generic;
using Roguelancer.Models;
using Roguelancer.Interfaces;
using Roguelancer.Enum;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
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
                Model.WorldObject = new Settings.ModelWorldObjects(
                    "bullet",
                    BulletModel.PlayerShip.Model.Position + startupPosition,
                    new Vector3(0f, 0f, 0f),
                    new Settings.SettingsModelObject(
                        modelPath,
                        ModelType.Bullet,
                        true,
                        13
                    ),
                    1,
                    BulletModel.PlayerShip.Model.Up,
                    BulletModel.PlayerShip.Model.Right,
                    BulletModel.PlayerShip.Model.Velocity,
                    BulletModel.PlayerShip.Model.CurrentThrust,
                    BulletModel.PlayerShip.Model.Direction,
                    1.0f,
                    0,
                    0
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
                if (BulletModel.PlayerShip == null) { BulletModel.PlayerShip = game.Objects.Ships.Ships.Where(s => s.PlayerShipControl.UseInput).LastOrDefault(); }
                Model.CurrentThrust = _bulletThrust + BulletModel.PlayerShip.Model.CurrentThrust;
                Model.Rotation = rotationAmount;
                Model.UpdatePosition();
                force = Model.Direction * Model.CurrentThrust * BulletModel.ThrustForce;
                acceleration = force / BulletModel.Mass;
                Model.Velocity += acceleration * elapsed;
                Model.Velocity *= BulletModel.DragFactor;
                Model.Position += Model.Velocity * elapsed;
                if (BulletModel.LimitAltitude) {
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
        /// <summary>
        /// Dispose
        /// </summary>
        public void Dispose(RoguelancerGame game) {
            try {
                Model.Dispose(game);
                BulletModel = new BulletModel();
            } catch {
                throw;
            }
        }
        #endregion
    }
    /// <summary>
    /// Bullets
    /// </summary>
    public class Bullets : IGame {
        #region "private variables"
        /// <summary>
        /// Player Ship
        /// </summary>
        private Ship _playerShip { get; set; }
        /// <summary>
        /// Model
        /// </summary>
        private BulletsModel _model { get; set; }
        #endregion
        #region "public functions"
        /// <summary>
        /// Entry Point
        /// </summary>
        public Model BulletsModel { get; set; }
        /// <summary>
        /// Entry Point
        /// </summary>
        /// <param name="game"></param>
        private ParticleSystemSettingsModel _particleSystemSettings;
        public Bullets(RoguelancerGame game) {
            _model = new BulletsModel();
            _particleSystemSettings = new ParticleSystemSettingsModel();
            _particleSystemSettings.CameraArc = 2;
            _particleSystemSettings.CameraRotation = 0f;
            _particleSystemSettings.CameraDistance = 110;
            _particleSystemSettings.FireRingSystemParticles = 20;
            _particleSystemSettings.SmokePlumeParticles = 20;
            _particleSystemSettings.SmokeRingParticles = 20;
            _particleSystemSettings.Fire = true;
            _particleSystemSettings.Enabled = false;
            _particleSystemSettings.Smoke = true;
            _particleSystemSettings.SmokeRing = true;
            _particleSystemSettings.Explosions = true;
            _particleSystemSettings.Projectiles = true;
            _particleSystemSettings.ExplosionTexture = "Textures\\Explosion";
            _particleSystemSettings.FireTexture = "Textures\\Fire";
            _particleSystemSettings.SmokeTexture = "Textures\\Smoke";
            _model.Bullets = new List<IBullet>();
        }
        /// <summary>
        /// Initialize
        /// </summary>
        /// <param name="game"></param>
        public void Initialize(RoguelancerGame game) {
            _model.AreBulletsAvailable = true;
            _model.RechargeRate = 240;
        }
        /// <summary>
        /// Load Content
        /// </summary>
        /// <param name="game"></param>
        public void LoadContent(RoguelancerGame game) {
            BulletsModel = game.Content.Load<Model>("bullet");
            _playerShip = game.Objects.Ships.Ships.Where(s => s.PlayerShipControl.UseInput).LastOrDefault();
        }
        /// <summary>
        /// Update
        /// </summary>
        /// <param name="game"></param>
        public void Update(RoguelancerGame game) {
            if (game.Input.InputItems.Keys.ControlLeft || game.Input.InputItems.Keys.ControlRight || game.Input.InputItems.Mouse.RightButton) {
                if (_model.AreBulletsAvailable) {
                    game.Camera.Shake(10f, 0f, false);
                    _model.Bullets.Add(new Bullet(_playerShip, game, new Vector3(-100f, -200f, 0f), particleSystemSettings: _particleSystemSettings));
                    _model.Bullets.Add(new Bullet(_playerShip, game, new Vector3(-100f, 700f, 0f), particleSystemSettings: _particleSystemSettings));
                    _model.AreBulletsAvailable = false;
                    _model.WeaponRechargedTime = DateTime.Now.AddMilliseconds(_model.RechargeRate);
                }
            }
            if (!_model.AreBulletsAvailable) {
                if (DateTime.Now >= _model.WeaponRechargedTime) {
                    _model.AreBulletsAvailable = true;
                    _model.WeaponRechargedTime = new DateTime();
                }
            }
            for (int i = 0; i <= _model.Bullets.Count - 1; i++) {
                _model.Bullets[i].Update(game); // Update Bullet
                if (_model.Bullets[i].BulletModel.DeathDate <= DateTime.Now) {
                    _model.Bullets[i].Dispose(game);
                    _model.Bullets.RemoveAt(i); // Remove old bullets
                }
            }
        }
        /// <summary>
        /// Draw
        /// </summary>
        /// <param name="game"></param>
        public void Draw(RoguelancerGame game) {
            foreach (var bullet in _model.Bullets) {
                bullet.Draw(game);
            }
        }
        #endregion
    }
}