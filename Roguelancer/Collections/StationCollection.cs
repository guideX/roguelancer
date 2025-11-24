using System.Linq;
using Roguelancer.Interfaces;
using Roguelancer.Models;
using Roguelancer.Objects;
namespace Roguelancer.Collections {
    /// <summary>
    /// Station Collection
    /// </summary>
    public class StationCollection : IGame {
        #region "public properties"
        /// <summary>
        /// Model
        /// </summary>
        public StationCollectionModel Model { get; set; }
        #endregion
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
        public void Initialize(RoguelancerGame game) {
            //var n = 0;
            foreach (var obj in game.Settings.Model.StarSystemSettings[game.CurrentStarSystemId - 1].Model.Stations) {
                var guid = System.Guid.NewGuid().ToString();
                //n++;
                var s = new StationObject(game);
                //s.StationModel.StationID = n;
                s.StationModel.StationGuid = guid;
                
                s.Model.WorldObject = obj;
                //s.DockableObjectModel.StationPrices = game.Settings.Model.StationPriceModels.Where(p => p.StationId == obj.Model.ID).ToList();
                //s.DockableObjectModel.StationPrices = game.Settings.Model.StationPriceModels.Where(p => p.StationGuid == obj.Model.Guid).ToList();
                Model.Stations.Add(s);
            }
            foreach (var station in Model.Stations) {
                station.Initialize(game);
            }
        }
        /// <summary>
        /// Load Content
        /// </summary>
        /// <param name="game"></param>
        public void LoadContent(RoguelancerGame game) {
            foreach (var station in Model.Stations) {
                station.LoadContent(game);
            }
        }
        /// <summary>
        /// Update
        /// </summary>
        /// <param name="game"></param>
        public void Update(RoguelancerGame game) {
            foreach (var station in Model.Stations) {
                station.Update(game);
            }
        }
        /// <summary>
        /// Draw
        /// </summary>
        /// <param name="game"></param>
        public void Draw(RoguelancerGame game) {
            foreach (var station in Model.Stations) {
                station.Draw(game);
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
            Model = new StationCollectionModel();
        }
        #endregion
    }
}