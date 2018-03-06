/*using System.Linq;
using Roguelancer.Interfaces;
using Roguelancer.Models;
using Roguelancer.Objects;
namespace Roguelancer.Collections {
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
            foreach (var obj in game.Settings.Model.StarSystemSettings[game.CurrentStarSystemId].Model.TradeLanes.ToList()) {
                n++;
                var t = new TradeLaneObject(game);
                for (var i = 0; i <= 8 - 1; i++) {
                    var o = obj.Clone();
                    var p = o.Model.StartupPosition;
                    p.X = o.Model.StartupPosition.X;
                    p.Y = o.Model.StartupPosition.Y + (200000 * i);
                    p.Z = o.Model.StartupPosition.Z;// - (200000 * i);
                    o.Model.StartupPosition = p;
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
}*/