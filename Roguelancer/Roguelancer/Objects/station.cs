// Roguelancer 0.1 Pre Alpha by Leon Aiossa
// http://www.team-nexgen.com
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
                var n = 0;
                foreach (var obj in game.Settings.StarSystemSettings[game.StarSystemId].Stations) {
                    n++;
                    var s = new Station(game);
                    s.StationID = n;
                    s.Model.WorldObject = obj;
                    s.StationPrices = game.Settings.StationPriceModels.Where(p => p.StarSystemId == game.StarSystemId && p.StationId == obj.ID).ToList();
                    Stations.Add(s);
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
    public class Station : DockableObject, IGame, IDockable, ISensorObject {
        /// <summary>
        /// Space Station ID
        /// </summary>
        public int StationID { get; set; }
        #region "public variables"
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
            Model = new GameModel(game, null);
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
            Model.UpdatePosition();
            Model.Update(game);
            if (game.GameState.CurrentGameState == Enum.GameStates.Docked && game.Input.InputItems.Keys.C && game.GameState.DockedGameState != Enum.DockedGameStateEnum.Commodities) {
                game.GameState.DockedGameState = Enum.DockedGameStateEnum.Commodities;
                ListCommoditiesForSale(game, Enum.ModelType.Station, StationID);
            }
            if (game.GameState.CurrentGameState == Enum.GameStates.Docked && game.Input.InputItems.Keys.U) {
                game.Input.InputItems.Keys.U = false;
                var ship = game.Objects.Ships.Ships.Where(s => s.PlayerShipControl.UseInput).LastOrDefault();
                if (ship.Docked) {
                    var distance = (int)Vector3.Distance(ship.Model.Position, Model.Position) / HudObject.DivisionDistanceValue;
                    if (distance < HudObject.DockDistanceAccept) {
                        UnDock(game, ship, Model.WorldObject);
                    }
                }
            }
            if (game.GameState.CurrentGameState == Enum.GameStates.Playing && game.Input.InputItems.Keys.D) { // DOCK
                game.Input.InputItems.Keys.D = false;
                var ship = game.Objects.Ships.Ships.Where(s => s.PlayerShipControl.UseInput).LastOrDefault();
                if (!ship.Docked) {
                    var distance = (int)Vector3.Distance(ship.Model.Position, Model.Position) / HudObject.DivisionDistanceValue;
                    if (distance < HudObject.DockDistanceAccept) {
                        Dock(game, ship, Model.WorldObject);
                        ship.Model.Velocity = new Vector3(0f, 0f, 0f);
                        ship.Model.CurrentThrust = 0f;
                    }
                }
            }
        }
        /// <summary>
        /// Draw
        /// </summary>
        /// <param name="game"></param>
        public void Draw(RoguelancerGame game) {
            Model.Draw(game);
        }
        #endregion
    }
}