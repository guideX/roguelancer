using System.Collections.Generic;
using Roguelancer.Interfaces;
using Roguelancer.Models;
using Roguelancer.Objects.Base;
using Roguelancer.Enum;
namespace Roguelancer.Objects {
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
            foreach (var obj in game.Settings.Model.StarSystemSettings[game.CurrentStarSystemId].Planets) {
                n++;
                var s = new PlanetObject(game);
                s.StationID = n;
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
    /// <summary>
    /// Planet
    /// </summary>
    public class PlanetObject : DockableObject, IGame, IDockable, ISensorObject {
        #region "public properties"
        /// <summary>
        /// Model
        /// </summary>
        public GameModel Model { get; set; }
        /// <summary>
        /// Docked Ships
        /// </summary>
        public List<ISensorObject> DockedShips { get; set; }
        /// <summary>
        /// Space Station ID
        /// </summary>
        public int StationID { get; set; }
        #endregion
        #region "public methods"
        /// <summary>
        /// Planet
        /// </summary>
        /// <param name="game"></param>
        public PlanetObject(RoguelancerGame game) {
            Reset(game);
            DockedShips = new List<ISensorObject>();
            //Model = new GameModel(game, null);
        }
        /// <summary>
        /// Initialize
        /// </summary>
        /// <param name="game"></param>
        public void Initialize(RoguelancerGame game) {
            Model.Initialize(game);
            Initialize(game, Model, StationID); // Initialize Dockable Object
        }
        /// <summary>
        /// Load Content
        /// </summary>
        /// <param name="game"></param>
        public void LoadContent(RoguelancerGame game) {
            Model.LoadContent(game);
            LoadContent(game, Model, StationID); // Dockable Object Load Content
        }
        /// <summary>
        /// Update
        /// </summary>
        /// <param name="game"></param>
        public void Update(RoguelancerGame game) {
            if (game.GameState.Model.CurrentGameState == GameStatesEnum.Playing) {
                Model.UpdatePosition(); // Update Position
                Model.Update(game); // Update
            }
            Update(game, Model, StationID); // Update Dockable Object Station Stuff
        }
        /// <summary>
        /// Draw
        /// </summary>
        /// <param name="game"></param>
        public void Draw(RoguelancerGame game) {
            if (game.GameState.Model.CurrentGameState == GameStatesEnum.Playing) {
                Model.Draw(game);
            }
            Draw(game, Model, StationID); // Draw Dockable Object Station Stuff
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
