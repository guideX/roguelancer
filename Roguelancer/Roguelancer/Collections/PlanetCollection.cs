using System.Linq;
using Roguelancer.Collections.Base;
using Roguelancer.Interfaces;
using Roguelancer.Objects;
namespace Roguelancer.Collections {
    /// <summary>
    /// Planet Collection
    /// </summary>
    public class PlanetCollection : CollectionObject<PlanetObject>, IGame {
        #region "public methods"
        /// <summary>
        /// Planet Collection
        /// </summary>
        public PlanetCollection(RoguelancerGame game) {
            Reset(game);
        }
        /// <summary>
        /// Initialize
        /// </summary>
        /// <param name="game"></param>
        public override void Initialize(RoguelancerGame game) {
            var n = 0;
            foreach (var obj in game.Settings.Model.StarSystemSettings[game.CurrentStarSystemId].Model.Planets) {
                n++;
                var s = new PlanetObject(game);
                s.DockableObjectType.ReferenceID = n;
                s.Model.WorldObject = obj;
                s.DockableObjectModel.StationPrices = game.Settings.Model.StationPriceModels.Where(p => p.StationId == obj.Model.ID).ToList();
                Objects.Add(s);
            }
            foreach (var planet in Objects) {
                planet.Initialize(game);
            }
        }
        #endregion
    }
}