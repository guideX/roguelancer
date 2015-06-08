using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Roguelancer.Interfaces;
using Roguelancer.Functionality;
using Roguelancer.Models;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
namespace Roguelancer.Objects {
    public class Bullets : IBullets {
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
            _model.AreBulletsAvailable = true;
            _model.RechargeRate = 40;
        }
        /// <summary>
        /// Shoot
        /// </summary>
        public void Shoot(RoguelancerGame game) {
            if (BulletsModel == null) {
                BulletsModel = game.Content.Load<Model>("bullet");
            }
            if (_model.AreBulletsAvailable) {
                if (_model.PlayerShip == null) {
                    _model.PlayerShip = game.objects.ships.GetPlayerShip(game);
                }
                var b = new Bullet(game);
                var b2 = new Bullet(game);
                b.Model = new GameModel(game);
                b.Model.worldObject = new Settings.ModelWorldObjects(
                    _model.PlayerShip.model.position + new Vector3(-300f, -300f, 0f),
                    new Vector3(0f, 0f, 0f),
                    new Settings.SettingsModelObject(
                        "bullet",
                        Settings.ModelType.Bullet,
                        true,
                        13
                    ),
                    1,
                    _model.PlayerShip.model.up,
                    _model.PlayerShip.model.right,
                    _model.PlayerShip.model.velocity,
                    _model.PlayerShip.model.currentThrust,
                    _model.PlayerShip.model.direction
                );
                b.Model.useScale = true;
                b.Model.particleSystemEnabled = true;
                b.Model.scale = 3;
                b.Initialize(game);
                b.LoadContent(game);
                b.BulletModel.DeathDate = DateTime.Now.AddSeconds(3);
                b2.Model = new GameModel(game);
                b2.Model.worldObject = new Settings.ModelWorldObjects(
                    _model.PlayerShip.model.position + new Vector3(300f, -300f, 0f),
                    b.Model.worldObject.startupModelRotation,
                    b.Model.worldObject.settingsModelObject,
                    b.Model.worldObject.starSystemId,
                    b.Model.worldObject.initialModelUp,
                    b.Model.worldObject.initialModelRight,
                    b.Model.worldObject.initialVelocity,
                    b.Model.worldObject.initialCurrentThrust,
                    b.Model.worldObject.initialDirection
                );
                b2.Model.particleSystemEnabled = b.Model.particleSystemEnabled;
                b2.Model.useScale = b.Model.useScale;
                b2.Model.scale = b.Model.scale;
                b2.Initialize(game);
                b2.LoadContent(game);
                b2.BulletModel.DeathDate = b.BulletModel.DeathDate;
                _model.Bullets.Add(b);
                _model.Bullets.Add(b2);
                _model.AreBulletsAvailable = false;
                _model.WeaponRechargedTime = DateTime.Now.AddMilliseconds(_model.RechargeRate);
            }
        }
        /// <summary>
        /// Initialize
        /// </summary>
        /// <param name="game"></param>
        public void Initialize(RoguelancerGame game) {
        }
        /// <summary>
        /// Load Content
        /// </summary>
        /// <param name="game"></param>
        public void LoadContent(RoguelancerGame game) {
        }
        /// <summary>
        /// Update
        /// </summary>
        /// <param name="game"></param>
        public void Update(RoguelancerGame game) {
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
    }
}