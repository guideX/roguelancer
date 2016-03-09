// Roguelancer 0.1 Pre Alpha by Leon Aiossa
// http://www.team-nexgen.org
using System.Linq;
using Roguelancer.Interfaces;
using System.Collections.Generic;
using Roguelancer.Settings;
using System;
using Roguelancer.Models;
namespace Roguelancer.Objects {
    /// <summary>
    /// Dockable Object
    /// </summary>
    public abstract class DockableObject {
        /// <summary>
        /// Guid
        /// </summary>
        public virtual string ID { get; set; }
        /// <summary>
        /// Docked Ships
        /// </summary>
        public virtual List<ISensorObject> DockedShips { get; set; }
        /// <summary>
        /// Commodities
        /// </summary>
        public virtual List<StationPriceModel> StationPrices { get; set; }
        /// <summary>
        /// Dockable Object
        /// </summary>
        public DockableObject() {
            ID = Guid.NewGuid().ToString();
        }
        /// <summary>
        /// Dock
        /// </summary>
        /// <param name="game"></param>
        /// <param name="ship"></param>
        public virtual void Dock(RoguelancerGame game, Ship ship, ModelWorldObjects worldObject) {
            try {
                var _ship = game.Objects.Ships.Ships.Where(s => s.PlayerShipControl.UseInput).LastOrDefault();
                if (_ship == ship) {
                    game.GameState.CurrentGameState = Enum.GameStates.Docked;
                }
                ship.Docked = true;
                DockedShips.Add(ship);
                game.DebugText.SetText(game, "Docked at '" + worldObject.Description + "'.", true);
            } catch {
                throw;
            }
        }
        /// <summary>
        /// Undock
        /// </summary>
        /// <param name="game"></param>
        /// <param name="ship"></param>
        public virtual void UnDock(RoguelancerGame game, Ship ship, ModelWorldObjects worldObject) {
            try {
                var _ship = game.Objects.Ships.Ships.Where(s => s.PlayerShipControl.UseInput).LastOrDefault();
                if (_ship == ship) {
                    game.GameState.CurrentGameState = Enum.GameStates.Playing;
                }
                ship.Docked = false;
                DockedShips.Remove(ship);
                game.DebugText.SetText(game, "Undocked from '" + worldObject.Description + "'.", true);
            } catch {
                throw;
            }
        }
        /// <summary>
        /// Commodities for Sale
        /// </summary>
        /// <param name="game"></param>
        /// <returns></returns>
        //public virtual List<StationPriceModel> CommoditiesForSale(RoguelancerGame game, ModelType modelType) {
            //try {
            //switch (modelType) {
            //case ModelType.Station:
            //return game.Settings.CommoditiesSettings.StationPriceModels.Where(p => p.StarSystemId == game.StarSystemId && p.StationId == stationID).ToList();
            //default:
            //return null;
            //}
            //} catch {
            //throw;
            //}
        //}
    }
}