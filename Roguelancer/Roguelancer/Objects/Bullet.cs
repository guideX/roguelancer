﻿// Roguelancer 0.1 Pre Alpha by Leon Aiossa
// http://www.team-nexgen.com
using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Roguelancer.Models;
using Roguelancer.Interfaces;
using Roguelancer.Enum;
using Roguelancer.Helpers;
namespace Roguelancer.Objects {
    /// <summary>
    /// Bullets
    /// </summary>
    public class Bullets : IGame {
        #region "private variables"
        /// <summary>
        /// Model
        /// </summary>
        private BulletsModel _model { get; set; }
        #endregion
        #region "public functions"
        /// <summary>
        /// Bullets Model
        /// </summary>
        public Model BulletsModel { get; set; }
        /// <summary>
        /// Particle Systems Settings
        /// </summary>
        private ParticleSystemSettingsModel _particleSystemSettings;
        /// <summary>
        /// Entry Point
        /// </summary>
        /// <param name="game"></param>
        public Bullets(RoguelancerGame game) {
            Reset();
        }
        /// <summary>
        /// Initialize
        /// </summary>
        /// <param name="game"></param>
        public void Initialize(RoguelancerGame game) {
            _model.AreBulletsAvailable = true;
            _model.RechargeRate = game.Settings.BulletRechargeRate;
        }
        /// <summary>
        /// Load Content
        /// </summary>
        /// <param name="game"></param>
        public void LoadContent(RoguelancerGame game) {
            BulletsModel = game.Content.Load<Model>("bullet");
            _model.PlayerShip = ShipHelper.GetPlayerShip(game); // Get Player Ship
        }
        /// <summary>
        /// Update
        /// </summary>
        /// <param name="game"></param>
        public void Update(RoguelancerGame game) {
            if (game.Input.InputItems.Keys.ControlLeft || game.Input.InputItems.Keys.ControlRight || game.Input.InputItems.Mouse.RightButton) {
                if (_model.AreBulletsAvailable) {
                    game.Camera.Shake(10f, 0f, false);
                    _model.Bullets.Add(new Bullet(_model.PlayerShip, game, new Vector3(-100f, -200f, 0f), particleSystemSettings: _particleSystemSettings));
                    _model.Bullets.Add(new Bullet(_model.PlayerShip, game, new Vector3(-100f, 700f, 0f), particleSystemSettings: _particleSystemSettings));
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
        /// <summary>
        /// Reset
        /// </summary>
        public void Reset() {
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
        /// Dispose
        /// </summary>
        public void Dispose(RoguelancerGame game) {
            _model = null;
        }
        #endregion
    }
    /// <summary>
    /// Bullet
    /// </summary>
    public class Bullet : IBullet {
        #region "public variables"
        /// <summary>
        /// Bullet Model
        /// </summary>
        public BulletModel BulletModel { get; set; }
        #endregion
        #region "public functions"
        /// <summary>
        /// Entry Point
        /// </summary>
        /// <param name="texture"></param> 
        public Bullet(Ship playerShipModel, RoguelancerGame game, Vector3 startupPosition, int deathSeconds = 3, int scale = 3, string modelPath = "bullet", float bulletThrust = .5f, ParticleSystemSettingsModel particleSystemSettings = null) {
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
            if (BulletModel.PlayerShip == null) { BulletModel.PlayerShip = ShipHelper.GetPlayerShip(game); }
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
        public void Reset(Ship playerShipModel, RoguelancerGame game, Vector3 startupPosition, int deathSeconds = 3, int scale = 3, string modelPath = "bullet", float bulletThrust = .5f, ParticleSystemSettingsModel particleSystemSettings = null) {
            BulletModel = new BulletModel(game);
            BulletModel.BulletThrust = bulletThrust;
            BulletModel.PlayerShip = playerShipModel;
            BulletModel.DeathDate = DateTime.Now.AddSeconds(deathSeconds);
            BulletModel.Model = new GameModel(game, particleSystemSettings);
            BulletModel.Model.UseScale = true;
            BulletModel.Model.Scale = scale;
            BulletModel.Model.WorldObject = new Settings.ModelWorldObjects(
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
        }
        #endregion
    }
}