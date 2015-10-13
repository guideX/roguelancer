using System;
using System.Linq;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Roguelancer.Interfaces;
using Roguelancer.Functionality;
using Roguelancer.Models;
using Roguelancer.Particle.System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;

namespace Roguelancer.Objects {
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
            try {
                _model = new BulletsModel();
                _particleSystemSettings = new ParticleSystemSettingsModel();
                _particleSystemSettings.FireRingSystemParticles = 20;
                _particleSystemSettings.SmokePlumeParticles = 20;
                _particleSystemSettings.SmokeRingParticles = 20;
                //_particleSystemSettings.CameraArc = 2;
                //_particleSystemSettings.CameraRotation = 0f;
                _particleSystemSettings.CameraDistance = 110;
                _particleSystemSettings.Fire = true;
                _particleSystemSettings.Enabled = true;
                _particleSystemSettings.Smoke = true;
                _particleSystemSettings.SmokeRing = true;
                _particleSystemSettings.Explosions = true;
                _particleSystemSettings.Projectiles = true;
                _particleSystemSettings.ExplosionTexture = "Textures\\Explosion";
                _particleSystemSettings.FireTexture = "Textures\\Fire";
                _particleSystemSettings.SmokeTexture = "Textures\\Smoke";
                _model.Bullets = new ObservableCollection<IBullet>();
                _model.Bullets.CollectionChanged += Bullets_CollectionChanged;
            } catch {
                throw;
            }
        }
        /// <summary>
        /// Bullets Collection Changed
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Bullets_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e) {
            try {
                if (e.Action == NotifyCollectionChangedAction.Remove) { 
                    foreach (var bullet in _model.Bullets) {
                        
                    }
                }
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
                _model.AreBulletsAvailable = true;
                _model.RechargeRate = 40;
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
                BulletsModel = game.Content.Load<Model>("bullet");
                _playerShip = game.Objects.ships.Ships.Where(s => s.PlayerShipControl.UseInput).LastOrDefault();
                //_playerShip = game.Objects.ships.GetPlayerShip(game);
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
                        _model.Bullets.RemoveAt(i); // Remove old bullets
                    }
                }
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
                foreach (var bullet in _model.Bullets) {
                    bullet.Draw(game);
                }
            } catch {
                throw;
            }
        }
        #endregion
    }
}