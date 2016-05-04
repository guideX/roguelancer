// Roguelancer 0.1 Pre Alpha by Leon Aiossa
// http://www.team-nexgen.com
using System;
using System.Collections.Generic;
using Roguelancer.Interfaces;
using Roguelancer.Models;
namespace Roguelancer.Objects {
    public class PlanetCollection : IGame {
        #region "public variables"
        /// <summary>
        /// Planets
        /// </summary>
        public List<Planet> Planets { get; set; }
        #endregion
        #region "public functions"
        /// <summary>
        /// Planet Collection
        /// </summary>
        public PlanetCollection() {
            Planets = new List<Planet>();
        }
        /// <summary>
        /// Initialize
        /// </summary>
        /// <param name="game"></param>
        public void Initialize(RoguelancerGame game) {
            foreach (var modelWorldObject in game.Settings.StarSystemSettings[game.StarSystemId].Planets) {
                var planet = new Planet(game);
                planet.Model.WorldObject = modelWorldObject;
                Planets.Add(planet);
            }
            for (int i = 0; i <= Planets.Count - 1; i++) {
                Planets[i].Initialize(game);
            }
        }
        /// <summary>
        /// Load Content
        /// </summary>
        /// <param name="game"></param>
        public void LoadContent(RoguelancerGame game) {
            for (int i = 0; i <= Planets.Count - 1; i++) {
                Planets[i].LoadContent(game);
            }
        }
        /// <summary>
        /// Upate
        /// </summary>
        /// <param name="game"></param>
        public void Update(RoguelancerGame game) {
            for (int i = 0; i <= Planets.Count - 1; i++) {
                Planets[i].Update(game);
            }
        }
        /// <summary>
        /// Draw
        /// </summary>
        /// <param name="game"></param>
        public void Draw(RoguelancerGame game) {
            for (int i = 0; i <= Planets.Count - 1; i++) {
                Planets[i].Draw(game);
            }
        }
        /// <summary>
        /// Dispose
        /// </summary>
        public void Dispose() {
        }
        #endregion
    }
    /// <summary>
    /// Planet
    /// </summary>
    public class Planet : DockableObject, IGame, IDockable, ISensorObject {
        #region "public variables"
        /// <summary>
        /// Model
        /// </summary>
        public GameModel Model { get; set; }
        #endregion
        #region "public functions"
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
            //Model.ModelMode = Enum.ModelModeEnum.Planet;
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
        public void Dispose() {
        }
        #endregion
    }
}