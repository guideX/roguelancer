// Roguelancer 0.1 Pre Alpha by Leon Aiossa
// http://team-nexgen.com
using System.Linq;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Roguelancer.Interfaces;
using Roguelancer.Models;
using Roguelancer.Objects.Base;
using Roguelancer.Helpers;
namespace Roguelancer.Objects {
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
            var n = 0;
            foreach (var obj in game.Settings.Model.StarSystemSettings[game.StarSystemId].Stations) {
                n++;
                var s = new Station(game);
                s.StationID = n;
                s.Model.WorldObject = obj;
                s.DockableObjectModel.StationPrices = game.Settings.Model.StationPriceModels.Where(p => p.StationId == obj.ID).ToList();
                Model.Stations.Add(s);
            }
            for (var i = 0; i <= Model.Stations.Count - 1; i++) {
                Model.Stations[i].Initialize(game);
            }
        }
        /// <summary>
        /// Load Content
        /// </summary>
        /// <param name="game"></param>
        public void LoadContent(RoguelancerGame game) {
            for (var i = 0; i <= Model.Stations.Count - 1; i++) {
                Model.Stations[i].LoadContent(game);
            }
        }
        /// <summary>
        /// Update
        /// </summary>
        /// <param name="game"></param>
        public void Update(RoguelancerGame game) {
            for (var i = 0; i <= Model.Stations.Count - 1; i++) {
                Model.Stations[i].Update(game);
            }
        }
        /// <summary>
        /// Draw
        /// </summary>
        /// <param name="game"></param>
        public void Draw(RoguelancerGame game) {
            for (var i = 0; i <= Model.Stations.Count - 1; i++) {
                Model.Stations[i].Draw(game);
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
    /// <summary>
    /// Station
    /// </summary>
    public class Station : DockableObject, IGame, IDockable, ISensorObject {
        #region "public properties"
        /// <summary>
        /// Docked Ships
        /// </summary>
        public List<ISensorObject> DockedShips { get; set; }
        /// <summary>
        /// Space Station ID
        /// </summary>
        public int StationID { get; set; }
        /// <summary>
        /// Game Model
        /// </summary>
        public GameModel Model { get; set; }
        #endregion
        #region "public methods"
        /// <summary>
        /// Entry Point
        /// </summary>
        /// <param name="game"></param>
        public Station(RoguelancerGame game) {
            Reset(game);
            DockedShips = new List<ISensorObject>();
        }
        /// <summary>
        /// Initialize
        /// </summary>
        /// <param name="game"></param>
        public void Initialize(RoguelancerGame game) {
            Model.Initialize(game);
        }
        /// <summary>
        /// Load Content
        /// </summary>
        /// <param name="game"></param>
        public void LoadContent(RoguelancerGame game) {
            Model.LoadContent(game);
        }
        /// <summary>
        /// Update
        /// </summary>
        /// <param name="game"></param>
        public void Update(RoguelancerGame game) {
            Model.UpdatePosition(); // Update Position
            Model.Update(game); // Update
            UpdateDockableObject(game, Model, StationID); // Update Station
        }
        /// <summary>
        /// Draw
        /// </summary>
        /// <param name="game"></param>
        public void Draw(RoguelancerGame game) {
            Model.Draw(game);
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
            Model = new GameModel(game, null);
        }
        #endregion
    }
}