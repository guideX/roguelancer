// Roguelancer 0.1 Pre Alpha by Leon Aiossa
// http://team-nexgen.com
using System;
using System.Text;
using System.Linq;
using Roguelancer.Settings;
using Roguelancer.Models;
using Roguelancer.Enum;
using Roguelancer.Helpers;
namespace Roguelancer.Objects.Base {
    /// <summary>
    /// Dockable Object
    /// </summary>
    public abstract class DockableObject {
        /// <summary>
        /// Dockable Object Model
        /// </summary>
        public DockableObjectModel DockableObjectModel { get; set; }
        /// <summary>
        /// Dockable Object
        /// </summary>
        public DockableObject() {
            DockableObjectModel = new DockableObjectModel();
        }
        /// <summary>
        /// Dock
        /// </summary>
        /// <param name="game"></param>
        /// <param name="ship"></param>
        public virtual void Dock(RoguelancerGame game, Ship ship, ModelWorldObjects worldObject) {
            var _ship = ShipHelper.GetPlayerShip(game); // Get Player Ship
            if (_ship == ship) { // If Docking Ship is Player Ship
                game.GameState.Model.CurrentGameState = Enum.GameStates.Docked; // Set Current Game State to Docked
            }
            ship.Docked = true; // Set Docked to True
            DockableObjectModel.DockedShips.Add(ship); // Add to Docked Ships
            DebugTextHelper.SetText(game, "Docked at '" + worldObject.Description + "'.", true); // Set Debug Text
        }
        /// <summary>
        /// Undock
        /// </summary>
        /// <param name="game"></param>
        /// <param name="ship"></param>
        public virtual void UnDock(RoguelancerGame game, Ship ship, ModelWorldObjects worldObject) {
            var playerShip = ShipHelper.GetPlayerShip(game); // Get Player Ship
            if (playerShip == ship) {
                game.GameState.Model.CurrentGameState = Enum.GameStates.Playing;
            }
            ship.Docked = false;
            DockableObjectModel.DockedShips.Remove(ship);
            DebugTextHelper.SetText(game, "Undocked from '" + worldObject.Description + "'.", true);
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
                    foreach (var obj in game.Settings.Model.StationPriceModels.Where(p => p.StationId == stationID).ToList()) {
                        n++;
                        var commodity = game.Settings.Model.CommoditiesModels.Where(c => c.CommodityId == obj.CommoditiesId).FirstOrDefault();
                        sb.AppendLine("[" + n.ToString () + "] Description: " + commodity.Description + ", Price: " + obj.Price.ToString());
                    }
                    if (game.GameState.Model.CurrentGameState == Enum.GameStates.Docked && game.GameState.Model.DockedGameState == Enum.DockedGameStateEnum.Commodities) {
                        DebugTextHelper.SetText(game, "Station Commodities:" + Environment.NewLine + sb.ToString() + Environment.NewLine, true);
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
            var stationPrices = DockableObjectModel.StationPrices.Where(p => p.CommoditiesId == commodityID);
            if (stationPrices.Any()) {
                var commodity = game.Settings.Model.CommoditiesModels.Where(c => c.CommodityId == commodityID).FirstOrDefault();
                var stationPrice = DockableObjectModel.StationPrices.FirstOrDefault();
                var ship = ShipHelper.GetPlayerShip(game); // Get Player Ship
                if (ship.ShipModel.Money * qty >= stationPrice.Price * qty) {
                    if (stationPrice.Qty >= qty) {
                        ship.ShipModel.Money = ship.ShipModel.Money - stationPrice.Price;
                        for (int i = 1; i <= qty; i++) {
                            ship.ShipModel.CargoHold.Commodities.Add(commodity);
                            stationPrice.Qty = stationPrice.Qty - 1;
                        }
                        DebugTextHelper.SetText(game, qty.ToString() + "Item(s) purchased", true);
                    } else {
                        DebugTextHelper.SetText(game, "Not enough qty", true);
                    }
                } else {
                    DebugTextHelper.SetText(game, "Not enough money", true);
                }
            } else {
                DebugTextHelper.SetText(game, "Item not available", true);
            }
        }
    }
}