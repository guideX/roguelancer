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
        #region "public variables"
        /// <summary>
        /// Model
        /// </summary>
        public StationCollectionModel Model { get; set; }
        #endregion
        #region "public functions"
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
            foreach (var obj in game.Settings.StarSystemSettings[game.StarSystemId].Stations) {
                n++;
                var s = new Station(game);
                s.StationID = n;
                s.Model.WorldObject = obj;
                s.StationPrices = game.Settings.StationPriceModels.Where(p => p.StationId == obj.ID).ToList();
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
        #region "public variables"
        /// <summary>
        /// Space Station ID
        /// </summary>
        public int StationID { get; set; }
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
            Model.UpdatePosition();
            Model.Update(game);
            if (game.GameState.Model.CurrentGameState == Enum.GameStates.Docked && game.GameState.Model.DockedGameState == Enum.DockedGameStateEnum.Commodities) {
                if (game.Input.InputItems.Keys.One) {
                    if (0 < StationPrices.Count) { PurchaseCommodity(game, StationPrices[0].CommoditiesId, 1); }
                }
                if (game.Input.InputItems.Keys.Two) {
                    if (1 < StationPrices.Count) { PurchaseCommodity(game, StationPrices[1].CommoditiesId, 1); }
                }
                if (game.Input.InputItems.Keys.Three) {
                    if (2 < StationPrices.Count) { PurchaseCommodity(game, StationPrices[2].CommoditiesId, 1); }
                }
            }
            if (game.GameState.Model.CurrentGameState == Enum.GameStates.Docked && game.Input.InputItems.Keys.C && game.GameState.Model.DockedGameState != Enum.DockedGameStateEnum.Commodities) {
                game.GameState.Model.DockedGameState = Enum.DockedGameStateEnum.Commodities;
                ListCommoditiesForSale(game, Enum.ModelType.Station, StationID);
            }
            if (game.GameState.Model.CurrentGameState == Enum.GameStates.Docked && game.Input.InputItems.Keys.U) {
                game.Input.InputItems.Keys.U = false;
                var ship = ShipHelper.GetPlayerShip(game);
                if (ship.Docked) {
                    var distance = (int)Vector3.Distance(ship.Model.Position, Model.Position) / HudObject.DivisionDistanceValue;
                    if (distance < HudObject.DockDistanceAccept) {
                        UnDock(game, ship, Model.WorldObject);
                    }
                }
            }
            if (game.GameState.Model.CurrentGameState == Enum.GameStates.Playing && game.Input.InputItems.Keys.D) { // DOCK
                game.Input.InputItems.Keys.D = false;
                var ship = ShipHelper.GetPlayerShip(game);
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