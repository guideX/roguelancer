// Roguelancer 0.1 Pre Alpha by Leon Aiossa
// http://www.team-nexgen.com
using System.Linq;
using System.Collections.Generic;
using Roguelancer.Interfaces;
using Roguelancer.Models;
using Roguelancer.Settings;
using System;
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
            _tradeLanes = new List<TradeLane>(); // Create new List of Trade Lanes
        }
        /// <summary>
        /// Load Content
        /// </summary>
        /// <param name="game"></param>
        public void LoadContent(RoguelancerGame game) {
            foreach (var r in game.Settings.StarSystemSettings[game.StarSystemId].TradeLanes.ToList()) {
                var n = new TradeLane(game);
                n.WorldObject = r;
                _tradeLanes.Add(n);
            }
            foreach (var item in _tradeLanes) {
                item.LoadContent(game);
            }
        }
        /// <summary>
        /// Initialize
        /// </summary>
        /// <param name="game"></param>
        public void Initialize(RoguelancerGame game) {
            foreach (var item in _tradeLanes) {
                item.Initialize(game);
            }
        }
        /// <summary>
        /// Update
        /// </summary>
        /// <param name="game"></param>
        public void Update(RoguelancerGame game) {
            foreach (var item in _tradeLanes) {
                item.Update(game);
            }
        }
        /// <summary>
        /// Draw
        /// </summary>
        /// <param name="game"></param>
        public void Draw(RoguelancerGame game) {
            foreach (var item in _tradeLanes) {
                item.Draw(game);
            }
        }
        /// <summary>
        /// Reset
        /// </summary>
        /// <param name="game"></param>
        public void Reset(RoguelancerGame game) {
            foreach (var item in _tradeLanes) {
                item.Reset(game);
            }
        }
        /// <summary>
        /// Dispose
        /// </summary>
        public void Dispose() {
        }
    }
    /// <summary>
    /// Trade Lane
    /// </summary>
    public class TradeLane : DockableObject, IGame, IDockable, ISensorObject {
        /// <summary>
        /// World Object
        /// </summary>
        public ModelWorldObjects WorldObject { get; set; }
        /// <summary>
        /// Trade Lane Rings
        /// </summary>
        public List<TradeLaneRing> TradeLaneRings { get; set; }
        /// <summary>
        /// Model
        /// </summary>
        public GameModel Model {
            get {
                throw new NotImplementedException();
            }
            set {
                throw new NotImplementedException();
            }
        }
        /// <summary>
        /// Entry Point
        /// </summary>
        /// <param name="game"></param>
        public TradeLane(RoguelancerGame game) {
            TradeLaneRings = new List<TradeLaneRing>();
        }
        /// <summary>
        /// Load Content
        /// </summary>
        /// <param name="game"></param>
        public void LoadContent(RoguelancerGame game) {
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
        }
        /// <summary>
        /// Initialize
        /// </summary>
        /// <param name="game"></param>
        public void Initialize(RoguelancerGame game) {
            foreach (var r in TradeLaneRings) {
                r.Initialize(game);
            }
        }
        /// <summary>
        /// Update
        /// </summary>
        /// <param name="game"></param>
        public void Update(RoguelancerGame game) {
            foreach (var r in TradeLaneRings) {
                r.Update(game);
            }
        }
        /// <summary>
        /// Draw
        /// </summary>
        /// <param name="game"></param>
        public void Draw(RoguelancerGame game) {
            foreach (var r in TradeLaneRings) {
                r.Draw(game);
            }
        }
        /// <summary>
        /// Reset
        /// </summary>
        /// <param name="game"></param>
        public void Reset(RoguelancerGame game) {
            foreach (var r in TradeLaneRings) {
                r.Reset(game);
            }
        }
        /// <summary>
        /// Dispose
        /// </summary>
        public void Dispose() {
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
            Models = new List<IGameModel>();
        }
        /// <summary>
        /// Load Content
        /// </summary>
        /// <param name="game"></param>
        public void LoadContent(RoguelancerGame game) {
            foreach (var r in Models) {
                r.LoadContent(game);
            }
        }
        /// <summary>
        /// Initialize
        /// </summary>
        /// <param name="game"></param>
        public void Initialize(RoguelancerGame game) {
            foreach (var r in Models) {
                r.Initialize(game);
            }
        }
        /// <summary>
        /// Update
        /// </summary>
        /// <param name="game"></param>
        public void Update(RoguelancerGame game) {
            foreach (var r in Models) {
                r.Update(game);
            }
        }
        /// <summary>
        /// Draw
        /// </summary>
        /// <param name="game"></param>
        public void Draw(RoguelancerGame game) {
            foreach (var r in Models) {
                r.Draw(game);
            }
        }
        /// <summary>
        /// Reset
        /// </summary>
        /// <param name="game"></param>
        public void Reset(RoguelancerGame game) {
            foreach (var r in Models) {
                //r.Reset(game);
            }
        }
        /// <summary>
        /// Dispose
        /// </summary>
        public void Dispose() {
        }
    }
}