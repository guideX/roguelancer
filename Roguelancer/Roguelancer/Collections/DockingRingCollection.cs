using System.Collections.Generic;
using System.Linq;
using Roguelancer.Collections.Base;
using Roguelancer.Interfaces;
using Roguelancer.Objects;
namespace Roguelancer.Collections {
    /// <summary>
    /// Docking Ring Collection
    /// </summary>
    public class DockingRingCollection : CollectionObject<DockingRingObject>, IGame {
        #region "public methods"
        /// <summary>
        /// Docking Ring Collection
        /// </summary>
        public DockingRingCollection(RoguelancerGame game) {
            Reset(game);
        }
        /// <summary>
        /// Initialize
        /// </summary>
        /// <param name="game"></param>
        public override void Initialize(RoguelancerGame game) {
            var n = 0;
            foreach (var obj in game.Settings.Model.StarSystemSettings[game.CurrentStarSystemId].Model.DockingRings) {
                n++;
                var s = new DockingRingObject(game);
                s.DockableObjectType.ReferenceID = n;
                s.Model.WorldObject = obj;
                s.DockableObjectModel.StationPrices = game.Settings.Model.StationPriceModels.Where(p => p.StationId == obj.Model.ID).ToList();
                Objects.Add(s);
            }
            foreach (var dockingRing in Objects) dockingRing.Initialize(game);
        }
        #endregion
    }
}