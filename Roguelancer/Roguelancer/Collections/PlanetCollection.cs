using Roguelancer.Interfaces;
using Roguelancer.Models;
using Roguelancer.Objects;

namespace Roguelancer.Collections {
    /// <summary>
    /// Planet Collection
    /// </summary>
    public class PlanetCollection : IGame {
        #region "public properties"
        /// <summary>
        /// Planet Collection Model
        /// </summary>
        public PlanetCollectionModel Model { get; set; }
        #endregion
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
        public void Initialize(RoguelancerGame game) {
            var n = 0;
            foreach (var obj in game.Settings.Model.StarSystemSettings[game.CurrentStarSystemId].Model.Planets) {
                n++;
                var s = new PlanetObject(game);
                s.StationModel.StationID = n;
                s.Model.WorldObject = obj;
                //s.DockableObjectModel.StationPrices = game.Settings.Model.StationPriceModels.Where(p => p.StationId == obj.ID).ToList();
                Model.Planets.Add(s);
            }
            foreach (var planet in Model.Planets) {
                planet.Initialize(game);
            }
        }
        /// <summary>
        /// Load Content
        /// </summary>
        /// <param name="game"></param>
        public void LoadContent(RoguelancerGame game) {
            foreach (var planet in Model.Planets) {
                planet.LoadContent(game);
            }
        }
        /// <summary>
        /// Upate
        /// </summary>
        /// <param name="game"></param>
        public void Update(RoguelancerGame game) {
            foreach (var planet in Model.Planets) {
                planet.Update(game);
            }
        }
        /// <summary>
        /// Draw
        /// </summary>
        /// <param name="game"></param>
        public void Draw(RoguelancerGame game) {
            foreach (var planet in Model.Planets) {
                planet.Draw(game);
            }
        }
        /// <summary>
        /// Dispose
        /// </summary>
        public void Dispose(RoguelancerGame game) {
            Model = null;
        }
        /// <summary>
        /// Reset
        /// </summary>
        /// <param name="game"></param>
        public void Reset(RoguelancerGame game) {
            Model = new PlanetCollectionModel();
        }
        #endregion
    }
}