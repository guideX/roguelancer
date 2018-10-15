using System.Linq;
using Roguelancer.Collections.Base;
using Roguelancer.Enum;
using Roguelancer.Interfaces;
using Roguelancer.Objects;
namespace Roguelancer.Collections {
    /// <summary>
    /// Station Collection
    /// </summary>
    public class StationCollection : CollectionObject<StationObject>, IGame {
        #region "public methods"
        /// <summary>
        /// Station Collection
        /// </summary>
        public StationCollection(RoguelancerGame game) {
            Reset(game);
        }
        /// <summary>
        /// Initialize
        /// </summary>
        /// <param name="game"></param>
        public override void Initialize(RoguelancerGame game) {
            var n = 0;
            foreach (var obj in game.Settings.Model.StarSystemSettings[game.CurrentStarSystemId].Model.Stations) {
                n++;
                var s = new StationObject(game);
                s.DockableObjectType.ReferenceID = n;
                s.DockableObjectType.ObjectType = ModelTypeEnum.Station;
                s.Model.WorldObject = obj;
                s.DockableObjectModel.StationPrices = game.Settings.Model.StationPriceModels.Where(p => p.StationId == obj.Model.ID).ToList();
                Objects.Add(s);
            }
            foreach (var station in Objects) {
                station.Initialize(game);
            }
        }
        #endregion
    }
}