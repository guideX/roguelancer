// Roguelancer 0.1 Pre Alpha by Leon Aiossa
// http://www.team-nexgen.org
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
            try {
                Planets = new List<Planet>();
            } catch {
                throw;
            }
        }
        /// <summary>
        /// Initialize
        /// </summary>
        /// <param name="game"></param>
        public void Initialize(RoguelancerGame game) {
            try {
                foreach (var modelWorldObject in game.Settings.StarSystemSettings[game.StarSystemId].planets) {
                    var planet = new Planet(game);
                    planet.Model.WorldObject = modelWorldObject;
                    Planets.Add(planet);
                }
                for (int i = 0; i <= Planets.Count - 1; i++) {
                    Planets[i].Initialize(game);
                }
            } catch {
                throw;
            }
        }
        /// <summary>
        /// Load Content
        /// </summary>
        /// <param name="game"></param>
        public void LoadContent(RoguelancerGame game) {
            try {
                for (int i = 0; i <= Planets.Count - 1; i++) {
                    Planets[i].LoadContent(game);
                }
            } catch {
                throw;
            }
        }
        /// <summary>
        /// Upate
        /// </summary>
        /// <param name="game"></param>
        public void Update(RoguelancerGame game) {
            try {
                for (int i = 0; i <= Planets.Count - 1; i++) {
                    Planets[i].Update(game);
                }
            } catch {
                throw;
            }
        }
        /// <summary>
        /// Draw
        /// </summary>
        /// <param name="game"></param>
        public void Draw(RoguelancerGame game) {
            try {
                for (int i = 0; i <= Planets.Count - 1; i++) {
                    Planets[i].Draw(game);
                }
            } catch {
                throw;
            }
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
            try {
                Model = new GameModel(game, null);
            } catch {
                throw;
            }
        }
        /// <summary>
        /// Initialize
        /// </summary>
        /// <param name="game"></param>
        public void Initialize(RoguelancerGame game) {
            try {
                //Model.ModelMode = Enum.ModelModeEnum.Planet;
                Model.Initialize(game);
            } catch {
                throw;
            }
        }
        /// <summary>
        /// Load Content
        /// </summary>
        /// <param name="game"></param>
        public void LoadContent(RoguelancerGame game) {
            try {
                Model.LoadContent(game);
            } catch {
                throw;
            }
        }
        /// <summary>
        /// Update
        /// </summary>
        /// <param name="game"></param>
        public void Update(RoguelancerGame game) {
            try {
                Model.UpdatePosition();
                Model.Update(game);
            } catch {
                throw;
            }
        }
        /// <summary>
        /// Draw
        /// </summary>
        /// <param name="game"></param>
        public void Draw(RoguelancerGame game) {
            try {
                Model.Draw(game);
            } catch {
                throw;
            }
        }
        #endregion
    }
}