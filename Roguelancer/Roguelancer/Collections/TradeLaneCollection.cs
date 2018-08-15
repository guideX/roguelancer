using System.Linq;
using Roguelancer.Collections.Base;
using Roguelancer.Interfaces;
using Roguelancer.Models;
using Roguelancer.Objects;
namespace Roguelancer.Collections {
    /// <summary>
    /// Trade Lane Collection
    /// </summary>
    public class TradeLaneCollection : CollectionObject<TradeLaneObject>, IGame {
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
        public override void Initialize(RoguelancerGame game) {
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
                Objects.Add(t);
            }
            for (var i = 0; i <= Objects.Count - 1; i++) {
                Objects[i].Initialize(game);
            }
        }
        #endregion
    }
}