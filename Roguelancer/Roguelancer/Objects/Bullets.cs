using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Roguelancer.Interfaces;
using Roguelancer.Functionality;
using Roguelancer.Models;
namespace Roguelancer.Objects {
    public class Bullets : IBullets {
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
            _model.Bullets = new List<Bullet>();
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
                _playerShip = game.objects.ships.GetPlayerShip(game);
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
                if (game.input.lInputItems.keys.lControlLeft) {
                    if (_model.AreBulletsAvailable) {
                        _model.Bullets.Add(new Bullet(_playerShip, game, new Vector3(-300f, -200f, 0f), new Vector3(0f, 0f, 0f)));
                        _model.Bullets.Add(new Bullet(_playerShip, game, new Vector3(-300f, 500f, 0f), new Vector3(0f, 0f, 0f)));
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