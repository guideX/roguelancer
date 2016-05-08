// Roguelancer 0.1 Pre Alpha by Leon Aiossa
// http://team-nexgen.com
using System;
using System.Collections.Generic;
using Roguelancer.Interfaces;
using Roguelancer.Models;
using Roguelancer.Objects.Base;
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
            foreach (var modelWorldObject in game.Settings.Model.StarSystemSettings[game.StarSystemId].Planets) {
                var planet = new Planet(game);
                planet.Model.WorldObject = modelWorldObject;
                Model.Planets.Add(planet);
            }
            for (int i = 0; i <= Model.Planets.Count - 1; i++) {
                Model.Planets[i].Initialize(game);
            }
        }
        /// <summary>
        /// Load Content
        /// </summary>
        /// <param name="game"></param>
        public void LoadContent(RoguelancerGame game) {
            for (int i = 0; i <= Model.Planets.Count - 1; i++) {
                Model.Planets[i].LoadContent(game);
            }
        }
        /// <summary>
        /// Upate
        /// </summary>
        /// <param name="game"></param>
        public void Update(RoguelancerGame game) {
            for (int i = 0; i <= Model.Planets.Count - 1; i++) {
                Model.Planets[i].Update(game);
            }
        }
        /// <summary>
        /// Draw
        /// </summary>
        /// <param name="game"></param>
        public void Draw(RoguelancerGame game) {
            for (int i = 0; i <= Model.Planets.Count - 1; i++) {
                Model.Planets[i].Draw(game);
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
    public class Planet : DockableObject, IGame, IDockable, ISensorObject {
        #region "public properties"
        /// <summary>
        /// Model
        /// </summary>
        public GameModel Model { get; set; }
        /// <summary>
        /// Docked Ships
        /// </summary>
        public List<ISensorObject> DockedShips { get; set; }
        #endregion
        #region "public methods"
        /// <summary>
        /// Planet
        /// </summary>
        /// <param name="game"></param>
        public Planet(RoguelancerGame game) {
            Model = new GameModel(game, null);
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
            Model.UpdatePosition();
            Model.Update(game);
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
        }
        #endregion
    }
}