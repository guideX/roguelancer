﻿using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Roguelancer.Models;
using Roguelancer.Interfaces;
using Roguelancer.Helpers;
using Roguelancer.Objects;
namespace Roguelancer.Collections {
    /// <summary>
    /// Bullet
    /// </summary>
    public class BulletCollection : IGame {
        #region "public properties"
        /// <summary>
        /// Bullets Model
        /// </summary>
        public Model BulletsModel { get; set; }
        #endregion
        #region "private properties"
        /// <summary>
        /// Model
        /// </summary>
        private BulletsModel _model { get; set; }
        /// <summary>
        /// Particle Systems Settings
        /// </summary>
        private ParticleSystemSettingsModel _particleSystemSettings { get; set; }
        #endregion
        #region "public methods"
        /// <summary>
        /// Entry Point
        /// </summary>
        /// <param name="game"></param>
        public BulletCollection() {
            Reset();
        }
        /// <summary>
        /// Initialize
        /// </summary>
        /// <param name="game"></param>
        public void Initialize(RoguelancerGame game) {
            _model.AreBulletsAvailable = true;
            _model.RechargeRate = game.Settings.Model.BulletRechargeRate;
        }
        /// <summary>
        /// Load Content
        /// </summary>
        /// <param name="game"></param>
        public void LoadContent(RoguelancerGame game) {
            BulletsModel = game.Content.Load<Model>("bullet");
            _model.PlayerShip = ShipHelper.GetPlayerShip(game.Objects.Model); // Get Player Ship
        }
        /// <summary>
        /// Update
        /// </summary>
        /// <param name="game"></param>
        public void Update(RoguelancerGame game) {
            if ((game.Input.InputItems.Keys.LeftControl != null && game.Input.InputItems.Keys.LeftControl.IsKeyDown) || (game.Input.InputItems.Keys.RightControl != null && game.Input.InputItems.Keys.RightControl.IsKeyDown) || game.Input.InputItems.Mouse.RightButton) {
                if (_model.AreBulletsAvailable) {
                    game.Camera.Shake(10f, 0f, false);
                    _model.Bullets.Add(new BulletObject(_model.PlayerShip, game, new Vector3(-100f, -200f, 0f), particleSystemSettings: _particleSystemSettings));
                    _model.Bullets.Add(new BulletObject(_model.PlayerShip, game, new Vector3(-100f, 700f, 0f), particleSystemSettings: _particleSystemSettings));
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
                if (_model.Bullets[i].DeathDate <= DateTime.Now) {
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
            _particleSystemSettings = new ParticleSystemSettingsModel() {
                CameraArc = 2,
                CameraRotation = 0f,
                CameraDistance = 110,
                FireRingSystemParticles = 20,
                SmokePlumeParticles = 20,
                SmokeRingParticles = 20,
                Fire = true,
                Enabled = false,
                Smoke = true,
                SmokeRing = true,
                Explosions = true,
                Projectiles = true,
                ExplosionTexture = "Textures\\Explosion",
                FireTexture = "Textures\\Fire",
                SmokeTexture = "Textures\\Smoke"
            };
            _model.Bullets = new List<IBullet>();
        }
        /// <summary>
        /// Dispose
        /// </summary>
        public void Dispose(RoguelancerGame game) {
            _model = null;
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