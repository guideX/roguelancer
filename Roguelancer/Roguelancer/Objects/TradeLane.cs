// Roguelancer 0.1 Pre Alpha by Leon Aiossa
// http://www.team-nexgen.org
using System.Linq;
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
                foreach (var r in game.Settings.StarSystemSettings[game.StarSystemId].tradeLanes.ToList()) {
                    var n = new TradeLane(game);
                    n.WorldObject = r;
                    _tradeLanes.Add(n);
                }
                foreach (var item in _tradeLanes) {
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
        /// World Object
        /// </summary>
        public ModelWorldObjects WorldObject { get; set; }
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
                for (var i = 0; i <= 8 - 1; i++) {
                    var ring = new TradeLaneRing(game);
                    var model = new GameModel(game, null);
                    if (i != 0) {
                        var newModelWorldObject = ModelWorldObjects.Clone(WorldObject);
                        var pos = newModelWorldObject.StartupPosition;
                        pos.X = newModelWorldObject.StartupPosition.X;
                        pos.Y = newModelWorldObject.StartupPosition.Y + (100000 * i);
                        pos.Y = newModelWorldObject.StartupPosition.Z;
                        newModelWorldObject.StartupPosition = pos;
                        model.WorldObject = newModelWorldObject;
                        model.UseScale = true;
                        model.Scale = 200f;
                        ring.Models.Add(model);
                        TradeLaneRings.Add(ring);
                    }
                }
                foreach (var r in TradeLaneRings) {
                    r.LoadContent(game);
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
                foreach (var r in TradeLaneRings) {
                    r.Initialize(game);
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
                foreach (var r in TradeLaneRings) {
                    r.Update(game);
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
                foreach (var r in TradeLaneRings) {
                    r.Draw(game);
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
                foreach (var r in TradeLaneRings) {
                    r.Reset(game);
                }
            } catch {
                throw;
            }
        }
    }
    /// <summary>
    /// Trade Lane Ring
    /// </summary>
    public class TradeLaneRing : IGame {
        /// <summary>
        /// Game Models
        /// </summary>
        public List<IGameModel> Models { get; set; }
        /// <summary>
        /// Entry Point
        /// </summary>
        /// <param name="game"></param>
        public TradeLaneRing(RoguelancerGame game) {
            try {
                Models = new List<IGameModel>();
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

                foreach (var r in Models) {
                    r.LoadContent(game);
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
                foreach (var r in Models) {
                    r.Initialize(game);
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
                foreach (var r in Models) {
                    r.Update(game);
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
                foreach (var r in Models) {
                    r.Draw(game);
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
                foreach (var r in Models) {
                    //r.Reset(game);
                }
            } catch {
                throw;
            }
        }
    }
}