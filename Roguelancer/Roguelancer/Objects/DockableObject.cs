// Roguelancer 0.1 Pre Alpha by Leon Aiossa
// http://www.team-nexgen.com
using System.Linq;
using Roguelancer.Interfaces;
using System.Collections.Generic;
using Roguelancer.Settings;
using System;
using Roguelancer.Models;
using Roguelancer.Enum;
using System.Text;

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
            var _ship = game.Objects.Ships.Ships.Where(s => s.PlayerShipControl.UseInput).LastOrDefault();
            if (_ship == ship) {
                game.GameState.CurrentGameState = Enum.GameStates.Docked;
            }
            ship.Docked = true;
            DockedShips.Add(ship);
            game.DebugText.SetText(game, "Docked at '" + worldObject.Description + "'.", true);
        }
        /// <summary>
        /// Undock
        /// </summary>
        /// <param name="game"></param>
        /// <param name="ship"></param>
        public virtual void UnDock(RoguelancerGame game, Ship ship, ModelWorldObjects worldObject) {
            var _ship = game.Objects.Ships.Ships.Where(s => s.PlayerShipControl.UseInput).LastOrDefault();
            if (_ship == ship) {
                game.GameState.CurrentGameState = Enum.GameStates.Playing;
            }
            ship.Docked = false;
            DockedShips.Remove(ship);
            game.DebugText.SetText(game, "Undocked from '" + worldObject.Description + "'.", true);
        }
        /// <summary>
        /// Commodities for Sale
        /// </summary>
        /// <param name="game"></param>
        /// <returns></returns>
        public virtual void ListCommoditiesForSale(RoguelancerGame game, ModelType modelType, int stationID) {
            switch (modelType) {
                case ModelType.Station:
                    var objs = game.Settings.StationPriceModels.Where(p => p.StationId == stationID).ToList();
                    StringBuilder sb = new StringBuilder();
                    foreach (var obj in objs) {
                        var commodity = game.Settings.CommoditiesModels.Where(c => c.CommodityId == obj.CommoditiesId).FirstOrDefault();
                        sb.AppendLine("Description: " + commodity.Description + ", Price: " + obj.Price.ToString());
                    }
                    game.DebugText.SetText(game, "Station Commodities:" + Environment.NewLine + sb.ToString(), true);
                    break;
                default:
                    break;
            }
        }
    }
}