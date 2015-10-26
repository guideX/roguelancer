// Roguelancer 0.1 Pre Alpha by Leon Aiossa
// http://www.team-nexgen.org
using System.Linq;
using System.Collections.Generic;
using Roguelancer.Interfaces;
using Roguelancer.Models;
using Microsoft.Xna.Framework;
namespace Roguelancer.Objects {
    /// <summary>
    /// Station Collection
    /// </summary>
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
                foreach (var obj in game.Settings.StarSystemSettings[game.StarSystemId].stations) {
                    tempStation = new Station(game);
                    tempStation.Model.WorldObject = obj;
                    Stations.Add(tempStation);
                }
                for (var i = 0; i <= Stations.Count - 1; i++) {
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
                for (var i = 0; i <= Stations.Count - 1; i++) {
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
                for (var i = 0; i <= Stations.Count - 1; i++) {
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
                for (var i = 0; i <= Stations.Count - 1; i++) {
                    Stations[i].Draw(game);
                }
            } catch {
                throw;
            }
        }
        #endregion
    }
    /// <summary>
    /// Station
    /// </summary>
    public class Station : IGame, IDockable, ISensorObject {
        #region "public variables"
        /// <summary>
        /// Docked Ships
        /// </summary>
        public List<ISensorObject> DockedShips { get; set; }
        /// <summary>
        /// Description
        /// </summary>
        //public string Description { get; set; }
        /// <summary>
        /// Game Model
        /// </summary>
        public GameModel Model { get; set; }
        #endregion
        #region "public functions"
        /// <summary>
        /// Entry Point
        /// </summary>
        /// <param name="game"></param>
        public Station(RoguelancerGame game) {
            try {
                Model = new GameModel(game, null);
                DockedShips = new List<ISensorObject>();
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
                if (game.Input.InputItems.Keys.U) {
                    var ship = game.Objects.Ships.Ships.Where(s => s.PlayerShipControl.UseInput).LastOrDefault();
                    if (ship.Docked) {
                        var distance = (int)Vector3.Distance(ship.Model.Position, Model.Position) / HudObject.DivisionDistanceValue;
                        if (distance < HudObject.DockDistanceAccept) {
                            UnDock(game, ship);
                        }
                    }
                }
                if (game.Input.InputItems.Keys.D) {
                    var ship = game.Objects.Ships.Ships.Where(s => s.PlayerShipControl.UseInput).LastOrDefault();
                    if (!ship.Docked) {
                        var distance = (int)Vector3.Distance(ship.Model.Position, Model.Position) / HudObject.DivisionDistanceValue;
                        if (distance < HudObject.DockDistanceAccept) {
                            Dock(game, ship);
                        }
                    }
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
        public void Dock(RoguelancerGame game, Ship ship) {
            try {
                ship.Docked = true;
                DockedShips.Add(ship);
                game.DebugText.SetText(game, "Docked at '" + Model.WorldObject.Description + "'. " + ((int)Vector3.Distance(ship.Model.Position, Model.Position) / HudObject.DivisionDistanceValue).ToString(), true);
            } catch {
                throw;
            }
        }
        /// <summary>
        /// Undock
        /// </summary>
        /// <param name="game"></param>
        /// <param name="ship"></param>
        public void UnDock(RoguelancerGame game, Ship ship) {
            try {
                ship.Docked = false;
                DockedShips.Remove(ship);
            } catch {
                throw;
            }
        }
        #endregion
    }
}