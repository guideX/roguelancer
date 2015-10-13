using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Roguelancer.Interfaces;
using Roguelancer.Functionality;
using Roguelancer.Models;
namespace Roguelancer.Objects {
    /// <summary>
    /// Bullets
    /// </summary>
    public class Bullets : IGame {
        /// <summary>
        /// Player Ship
        /// </summary>
        private Ship _playerShip { get; set; }
        /// <summary>
        /// Model
        /// </summary>
        private BulletsModel _model { get; set; }
        /// <summary>
        /// Entry Point
        /// </summary>
        public Model BulletsModel { get; set; }
        public Bullets(RoguelancerGame game) {
            _model = new BulletsModel();
            _model.Bullets = new List<IBullet>();
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
                _playerShip = game.Objects.ships.GetPlayerShip(game);
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
                        _model.Bullets.Add(new Bullet(_playerShip, game, new Vector3(-100f, -200f, 0f)));
                        _model.Bullets.Add(new Bullet(_playerShip, game, new Vector3(-100f, 700f, 0f)));
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
    }
}