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
                    var sb = new StringBuilder();
                    var n = 0;
                    foreach (var obj in game.Settings.StationPriceModels.Where(p => p.StationId == stationID).ToList()) {
                        n++;
                        var commodity = game.Settings.CommoditiesModels.Where(c => c.CommodityId == obj.CommoditiesId).FirstOrDefault();
                        sb.AppendLine("[" + n.ToString () + "] Description: " + commodity.Description + ", Price: " + obj.Price.ToString());
                    }
                    if (game.GameState.CurrentGameState == Enum.GameStates.Docked && game.GameState.DockedGameState == Enum.DockedGameStateEnum.Commodities) {
                        game.DebugText.SetText(game, "Station Commodities:" + Environment.NewLine + sb.ToString() + Environment.NewLine, true);
                    }
                    break;
                default:
                    break;
            }
        }
        /// <summary>
        /// Purchase Commodity
        /// </summary>
        /// <param name="game"></param>
        /// <param name="model"></param>
        /// <param name="qty"></param>
        public virtual void PurchaseCommodity(RoguelancerGame game, int commodityID, int qty) {
            var stationPrices = StationPrices.Where(p => p.CommoditiesId == commodityID);
            if (stationPrices.Any()) {
                var commodity = game.Settings.CommoditiesModels.Where(c => c.CommodityId == commodityID).FirstOrDefault();
                var stationPrice = StationPrices.FirstOrDefault();
                var ship = game.Objects.Ships.Ships.Where(s => s.PlayerShipControl.UseInput).LastOrDefault();
                if (ship.Money * qty >= stationPrice.Price * qty) {
                    if (stationPrice.Qty >= qty) {
                        ship.Money = ship.Money - stationPrice.Price;
                        for (int i = 1; i <= qty; i++) {
                            ship.CargoHold.Commodities.Add(commodity);
                            stationPrice.Qty = stationPrice.Qty - 1;
                        }
                        game.DebugText.SetText(game, qty.ToString() + "Item(s) purchased", true);
                    } else {
                        game.DebugText.SetText(game, "Not enough qty", true);
                    }
                } else {
                    game.DebugText.SetText(game, "Not enough money", true);
                }
            } else {
                game.DebugText.SetText(game, "Item not available", true);
            }
        }
    }
}