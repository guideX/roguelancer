using System;
using System.Collections.Generic;
using Roguelancer.Interfaces;
using Roguelancer.Models;
using Roguelancer.Settings;

namespace Roguelancer.Objects {
    /// <summary>
    /// Trade Lane Collection
    /// </summary>
    public class TradeLaneCollection : IGame {
        /// <summary>
        /// Trade Lanes
        /// </summary>
        private List<TradeLane> _tradeLanes;
        /// <summary>
        /// Trade Lane Collection
        /// </summary>
        public TradeLaneCollection() {
            try {
                _tradeLanes = new List<TradeLane>(); // Create new List of Trade Lanes
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
                foreach(var item in _tradeLanes) {
                    item.LoadContent(game);
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
                foreach (var item in _tradeLanes) {
                    item.Initialize(game);
                }
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
                foreach (var item in _tradeLanes) {
                    item.Update(game);
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
                foreach (var item in _tradeLanes) {
                    item.Draw(game);
                }
            } catch {
                throw;
            }
        }
        /// <summary>
        /// Reset
        /// </summary>
        /// <param name="game"></param>
        public void Reset(RoguelancerGame game) {
            try {
                foreach (var item in _tradeLanes) {
                    item.Reset(game);
                }
            } catch {
                throw;
            }
        }
    }
    /// <summary>
    /// Trade Lane
    /// </summary>
    public class TradeLane : IGame {
        /// <summary>
        /// Trade Lane Rings
        /// </summary>
        public List<TradeLaneRing> TradeLaneRings { get; set; }
        /// <summary>
        /// Entry Point
        /// </summary>
        /// <param name="game"></param>
        public TradeLane(RoguelancerGame game) {
            try {
                TradeLaneRings = new List<TradeLaneRing>();
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
                foreach (var ring in TradeLaneRings) {
                    ring.LoadContent(game);
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
                foreach (var ring in TradeLaneRings) {
                    ring.Initialize(game);
                }
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
                foreach (var ring in TradeLaneRings) {
                    ring.Update(game);
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
                foreach (var ring in TradeLaneRings) {
                    ring.Draw(game);
                }
            } catch {
                throw;
            }
        }
        /// <summary>
        /// Reset
        /// </summary>
        /// <param name="game"></param>
        public void Reset(RoguelancerGame game) {
            try {
                foreach (var ring in TradeLaneRings) {
                    ring.Reset(game);
                }
            } catch {
                throw;
            }
        }
    }
    /// <summary>
    /// Trade Lane Ring
    /// </summary>
    public class TradeLaneRing : IGame, IDockable { // : IGame {
        /// <summary>
        /// Game Models
        /// </summary>
        public List<IGameModel> Models { get; set; }
        /// <summary>
        /// Entry Point
        /// </summary>
        /// <param name="game"></param>
        public ModelWorldObjects Object { get; set; }
        public TradeLaneRing(RoguelancerGame game) {
            try {
                Models = new List<IGameModel>();
            } catch {
                throw;
            }
        }
        public void LoadContent(RoguelancerGame game) {
            try {
                for (var i = 0; i <= 8 - 1; i++) {
                    var model = new GameModel(game, null);
                    if (i != 0) {
                        var pos = Object.StartupPosition;
                        pos.X = Object.StartupPosition.X + (300 * i);
                        pos.Y = Object.StartupPosition.Y;
                        pos.Y = Object.StartupPosition.Z;
                        Object.StartupPosition = pos;
                    }
                

                    model.WorldObject = Object;
                    //model.WorldObject = new Settings.ModelWorldObjects(
                    //new Microsoft.Xna.Framework.Vector3(0, 0, 0)
                    //);
                    //model.ModelMode = Enum.ModelModeEnum.TradeLaneRing;
                    //Models.Add(model);
                    //game.Settings.StarSystemSettings[0].tradeLanes;
                    /*
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
    */
                }
                foreach (var model in Models) {
                    model.LoadContent(game);
                }
            } catch {
                throw;
            }
        }
        public void Initialize(RoguelancerGame game) {
            try {
                foreach (var model in Models) {
                    model.Initialize(game);
                }
            } catch {
                throw;
            }
        }
        public void Update(RoguelancerGame game) {
            try {
                foreach (var model in Models) {
                    model.Update(game);
                }
            } catch {
                throw;
            }
        }
        public void Draw(RoguelancerGame game) {
            try {
                foreach (var model in Models) {
                    model.Draw(game);
                }
            } catch {
                throw;
            }
        }
        public void Reset(RoguelancerGame game) {
            try {
                foreach (var model in Models) {
                    //model.Reset(game);
                }
            } catch {
                throw;
            }
        }

        public void Dock(RoguelancerGame game, Ship ship) {
            throw new NotImplementedException();
        }

        public void UnDock(RoguelancerGame game, Ship ship) {
            throw new NotImplementedException();
        }
    }
}