using System.Collections.Generic;
using Roguelancer.Functionality;
using Roguelancer.Interfaces;
using Roguelancer.Settings;
using Roguelancer.Models;
using Roguelancer.Particle.System;
namespace Roguelancer.Objects {
    public class StationCollection : IGame {
        #region "public variables"
        /// <summary>
        /// Stations
        /// </summary>
        public List<Station> Stations { get; set; }
        #endregion
        #region "public functions"
        /// <summary>
        /// Station Collection
        /// </summary>
        public StationCollection() {
            try {
                Stations = new List<Station>();
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
                Station tempStation;
                foreach (ModelWorldObjects modelWorldObject in game.Settings.StarSystemSettings[0].stations) {
                    tempStation = new Station(game);
                    tempStation.model.WorldObject = modelWorldObject;
                    Stations.Add(tempStation);
                }
                for (int i = 0; i <= Stations.Count - 1; i++) {
                    Stations[i].Initialize(game);
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
                for (int i = 0; i <= Stations.Count - 1; i++) {
                    Stations[i].LoadContent(game);
                }
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
                for (int i = 0; i <= Stations.Count - 1; i++) {
                    Stations[i].Update(game);
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
                for (int i = 0; i <= Stations.Count - 1; i++) {
                    Stations[i].Draw(game);
                }
            } catch {
                throw;
            }
        }
        #endregion
    }
    public class Station : IGame, IDockable {
        #region "public variables"
        /// <summary>
        /// Game Model
        /// </summary>
        public GameModel model;
        #endregion
        #region "public functions"
        /// <summary>
        /// Entry Point
        /// </summary>
        /// <param name="game"></param>
        public Station(RoguelancerGame game) {
            try {
                model = new GameModel(game, null);
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
                //model.ModelMode = Enum.ModelModeEnum.Station;
                model.Initialize(game);
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
                model.LoadContent(game);
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
                model.UpdatePosition();
                model.Update(game);
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
                model.Draw(game);
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
        /// Undock
        /// </summary>
        /// <param name="game"></param>
        /// <param name="ship"></param>
        public void UnDock(RoguelancerGame game, Ship ship) {}
        #endregion
    }
}