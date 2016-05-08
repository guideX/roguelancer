// Roguelancer 0.1 Pre Alpha by Leon Aiossa
// http://team-nexgen.com
using System.Linq;
using Roguelancer.Interfaces;
using Roguelancer.Models;
using Roguelancer.Objects.Base;
using System.Collections.Generic;
using Roguelancer.Settings;
namespace Roguelancer.Objects {
    /// <summary>
    /// Trade Lane Collection
    /// </summary>
    public class TradeLaneCollection : IGame {
        #region "public properties"
        /// <summary>
        /// Trade Lane Collection Model
        /// </summary>
        public TradeLaneCollectionModel Model { get; set; }
        #endregion
        #region "public methods"
        /// <summary>
        /// Trade Lane Collection
        /// </summary>
        public TradeLaneCollection(RoguelancerGame game) {
            Reset(game);
        }
        /// <summary>
        /// Initialize
        /// </summary>
        /// <param name="game"></param>
        public void Initialize(RoguelancerGame game) {
            var n = 0;
            foreach (var obj in game.Settings.Model.StarSystemSettings[game.StarSystemId].TradeLanes.ToList()) {
                n++;
                var t = new TradeLane(game);
                for (var i = 0; i <= 8 - 1; i++) {
                    var o = ModelWorldObjects.Clone(obj);
                    var p = o.StartupPosition;
                    p.X = o.StartupPosition.X;
                    p.Y = o.StartupPosition.Y;
                    p.Z = o.StartupPosition.Z - (200000 * i);
                    o.StartupPosition = p;
                    var tradeLaneModel = new TradeLaneModel(game, o);
                    t.Models.Add(tradeLaneModel);
                }
                Model.TradeLanes.Add(t);
            }
            for (var i = 0; i <= Model.TradeLanes.Count - 1; i++) {
                Model.TradeLanes[i].Initialize(game);
            }
        }
        /// <summary>
        /// Load Content
        /// </summary>
        /// <param name="game"></param>
        public void LoadContent(RoguelancerGame game) {
            for (var i = 0; i <= Model.TradeLanes.Count - 1; i++) {
                Model.TradeLanes[i].LoadContent(game);
            }
        }
        /// <summary>
        /// Update
        /// </summary>
        /// <param name="game"></param>
        public void Update(RoguelancerGame game) {
            for (var i = 0; i <= Model.TradeLanes.Count - 1; i++) {
                Model.TradeLanes[i].Update(game);
            }
        }
        /// <summary>
        /// Draw
        /// </summary>
        /// <param name="game"></param>
        public void Draw(RoguelancerGame game) {
            for (var i = 0; i <= Model.TradeLanes.Count - 1; i++) {
                Model.TradeLanes[i].Draw(game);
            }
        }
        /// <summary>
        /// Reset
        /// </summary>
        /// <param name="game"></param>
        public void Reset(RoguelancerGame game) {
            Model = new TradeLaneCollectionModel();
        }
        /// <summary>
        /// Dispose
        /// </summary>
        public void Dispose(RoguelancerGame game) {
            for (var i = 0; i <= Model.TradeLanes.Count - 1; i++) {
                Model.TradeLanes[i].Dispose(game);
            }
            Model = null;
        }
        #endregion
    }
    /// <summary>
    /// Trade Lane
    /// </summary>
    public class TradeLane : DockableObject, IGame, IDockable, ITradeLaneSensorObject {
        #region "public properties"
        /// <summary>
        /// Docked Ships
        /// </summary>
        public List<ISensorObject> DockedShips { get; set; }
        /// <summary>
        /// Game Model
        /// </summary>
        public List<TradeLaneModel> Models { get; set; }
        #endregion
        #region "public methods"
        /// <summary>
        /// Entry Point
        /// </summary>
        /// <param name="game"></param>
        public TradeLane(RoguelancerGame game) {
            Reset(game);
        }
        /// <summary>
        /// Initialize
        /// </summary>
        /// <param name="game"></param>
        public void Initialize(RoguelancerGame game) {
            for (var i = 0; i <= Models.Count - 1; i++) {
                Models[i].Model.Initialize(game);
            }
        }
        /// <summary>
        /// Load Content
        /// </summary>
        /// <param name="game"></param>
        public void LoadContent(RoguelancerGame game) {
            for (var i = 0; i <= Models.Count - 1; i++) {
                Models[i].Model.LoadContent(game);
            }
        }
        /// <summary>
        /// Update
        /// </summary>
        /// <param name="game"></param>
        public void Update(RoguelancerGame game) {
            for (var i = 0; i <= Models.Count - 1; i++) {
                Models[i].Model.UpdatePosition();
                Models[i].Model.Update(game);
            }
        }
        /// <summary>
        /// Draw
        /// </summary>
        /// <param name="game"></param>
        public void Draw(RoguelancerGame game) {
            for (var i = 0; i <= Models.Count - 1; i++) {
                Models[i].Model.Draw(game);
            }
        }
        /// <summary>
        /// Reset
        /// </summary>
        /// <param name="game"></param>
        public void Reset(RoguelancerGame game) {
            Models = new List<TradeLaneModel>();
        }
        /// <summary>
        /// Dispose
        /// </summary>
        public void Dispose(RoguelancerGame game) {
            for (var i = 0; i <= Models.Count - 1; i++) {
                Models[i].Model.Dispose(game);
            }
        }
        #endregion
    }
}