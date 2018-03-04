using System.Linq;
using Roguelancer.Interfaces;
using Roguelancer.Models;
using Roguelancer.Objects;
namespace Roguelancer.Collections {
    /// <summary>
    /// Docking Ring Collection
    /// </summary>
    public class DockingRingCollection : IGame {
        #region "public properties"
        /// <summary>
        /// Docking Ring Model
        /// </summary>
        public DockingRingCollectionModel Model { get; set; }
        #endregion
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
        public void Initialize(RoguelancerGame game) {
            var n = 0;
            foreach (var obj in game.Settings.Model.StarSystemSettings[game.CurrentStarSystemId].Model.Planets) {
                n++;
                var s = new PlanetObject(game);
                s.StationModel.StationID = n;
                s.Model.WorldObject = obj;
                s.DockableObjectModel.StationPrices = game.Settings.Model.StationPriceModels.Where(p => p.StationId == obj.Model.ID).ToList();
                Model.DockingRings.Add(s);
            }
            foreach (var dockingRing in Model.DockingRings) {
                dockingRing.Initialize(game);
            }
        }
        /// <summary>
        /// Load Content
        /// </summary>
        /// <param name="game"></param>
        public void LoadContent(RoguelancerGame game) {
            foreach (var dockingRing in Model.DockingRings) {
                dockingRing.LoadContent(game);
            }
        }
        /// <summary>
        /// Upate
        /// </summary>
        /// <param name="game"></param>
        public void Update(RoguelancerGame game) {
            foreach (var dockingRing in Model.DockingRings) {
                dockingRing.Update(game);
            }
        }
        /// <summary>
        /// Draw
        /// </summary>
        /// <param name="game"></param>
        public void Draw(RoguelancerGame game) {
            foreach (var dockingRing in Model.DockingRings) {
                dockingRing.Draw(game);
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
            Model = new DockingRingCollectionModel();
        }
        #endregion
    }
}