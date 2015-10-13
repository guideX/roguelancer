// Roguelancer Planet
using System.Collections.Generic;
using Roguelancer.Interfaces;
using Roguelancer.Functionality;
using Roguelancer.Settings;
using Roguelancer.Models;
using Roguelancer.Particle.System;
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
                Planet tempPlanet;
                foreach (ModelWorldObjects modelWorldObject in game.Settings.starSystemSettings[0].planets) {
                    tempPlanet = new Planet(game);
                    tempPlanet.Model.WorldObject = modelWorldObject;
                    Planets.Add(tempPlanet);
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
    public class Planet : IGame, IDockable {
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
                Model.ModelMode = Enum.ModelModeEnum.Planet;
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
        /// <summary>
        /// Dock
        /// </summary>
        /// <param name="game"></param>
        /// <param name="ship"></param>
        public void Dock(RoguelancerGame game, Ship ship) {}
        /// <summary>
        /// Un-Doc
        /// </summary>
        /// <param name="game"></param>
        /// <param name="ship"></param>
        public void UnDock(RoguelancerGame game, Ship ship) {}
        #endregion
    }
}