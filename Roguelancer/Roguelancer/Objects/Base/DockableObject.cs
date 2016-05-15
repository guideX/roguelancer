// Roguelancer 0.1 Pre Alpha by Leon Aiossa
// http://team-nexgen.com
using System;
using System.Text;
using System.Linq;
using Microsoft.Xna.Framework;
using Roguelancer.Settings;
using Roguelancer.Models;
using Roguelancer.Enum;
using Roguelancer.Helpers;
using Roguelancer.Interfaces;
using Microsoft.Xna.Framework.Graphics;
namespace Roguelancer.Objects.Base {
    /// <summary>
    /// Dockable Object
    /// </summary>
    public abstract class DockableObject : IDockableObject {
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
            DebugTextHelper.SetText(game, "Docked at '" + worldObject.Description + "'." + Environment.NewLine + " " + Environment.NewLine + "[H] = Hangar " + Environment.NewLine + "[B] = Bar " + Environment.NewLine + " [E] = Equipment Dealer " + Environment.NewLine + " [C] = Commodities " + Environment.NewLine + " [S] = Ship Dealer", true); // Set Debug Text
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
                        sb.AppendLine("[" + n.ToString () + "] Description: " + commodity.Description + ", Price: " + obj.Price.ToString() + Environment.NewLine);
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
                        System.Threading.Thread.Sleep(1000);
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
        /// <summary>
        /// Initialize
        /// </summary>
        /// <param name="game"></param>
        /// <param name="Model"></param>
        /// <param name="stationID"></param>
        public virtual void Initialize(RoguelancerGame game, GameModel Model, int? stationID) {

        }
        /// <summary>
        /// Load Content
        /// </summary>
        /// <param name="game"></param>
        /// <param name="Model"></param>
        /// <param name="stationID"></param>
        public virtual void LoadContent(RoguelancerGame game, GameModel Model, int? stationID) {
            DockableObjectModel.BackgroundTexture = game.Content.Load<Texture2D>(@"Menus\allpanels");
        }
        /// <summary>
        /// Update
        /// </summary>
        /// <param name="game"></param>
        /// <param name="Model"></param>
        /// <param name="stationID"></param>
        public virtual void Update(RoguelancerGame game, GameModel Model, int? stationID) {
            switch (game.GameState.Model.CurrentGameState) { // Current Game State
                case Enum.GameStates.Docked: // Docked
                    switch (game.GameState.Model.DockedGameState) { // Docked Game State
                        case Enum.DockedGameStateEnum.Hanger: // Hanger
                            // TODO
                            // OPTION = LAUNCH
                            break;
                        case Enum.DockedGameStateEnum.EquipmentDealer: // Equipment Dealer
                            // TODO
                            break;
                        case Enum.DockedGameStateEnum.Bar: // Bar
                            // TODO
                            // OPTION1 = Talk to Bar Person
                            // OPTION2 = Purchase information from Bar Person
                            // OPTION3 = Purchase starchart from Bar Person
                            break;
                        case Enum.DockedGameStateEnum.Commodities: // Commodities
                            if (game.Input.InputItems.Keys.One) { if (0 < DockableObjectModel.StationPrices.Count) { PurchaseCommodity(game, DockableObjectModel.StationPrices[0].CommoditiesId, 1); } }
                            if (game.Input.InputItems.Keys.Two) { if (1 < DockableObjectModel.StationPrices.Count) { PurchaseCommodity(game, DockableObjectModel.StationPrices[1].CommoditiesId, 1); } }
                            if (game.Input.InputItems.Keys.Three) { if (2 < DockableObjectModel.StationPrices.Count) { PurchaseCommodity(game, DockableObjectModel.StationPrices[2].CommoditiesId, 1); } }
                            if (game.Input.InputItems.Keys.Four) { if (3 < DockableObjectModel.StationPrices.Count) { PurchaseCommodity(game, DockableObjectModel.StationPrices[3].CommoditiesId, 1); } }
                            if (game.Input.InputItems.Keys.Five) { if (4 < DockableObjectModel.StationPrices.Count) { PurchaseCommodity(game, DockableObjectModel.StationPrices[4].CommoditiesId, 1); } }
                            if (game.Input.InputItems.Keys.Six) { if (5 < DockableObjectModel.StationPrices.Count) { PurchaseCommodity(game, DockableObjectModel.StationPrices[5].CommoditiesId, 1); } }
                            if (game.Input.InputItems.Keys.Seven) { if (6 < DockableObjectModel.StationPrices.Count) { PurchaseCommodity(game, DockableObjectModel.StationPrices[6].CommoditiesId, 1); } }
                            if (game.Input.InputItems.Keys.Eight) { if (7 < DockableObjectModel.StationPrices.Count) { PurchaseCommodity(game, DockableObjectModel.StationPrices[7].CommoditiesId, 1); } }
                            if (game.Input.InputItems.Keys.Nine) { if (8 < DockableObjectModel.StationPrices.Count) { PurchaseCommodity(game, DockableObjectModel.StationPrices[8].CommoditiesId, 1); } }
                            break;
                        case Enum.DockedGameStateEnum.ShipDealer:
                            // TODO
                            // OPTION1 = Purchase Ship 1
                            // OPTION2 = Purchase Ship 2
                            // Etc
                            break;
                    }
                    if (game.Input.InputItems.Keys.U) {
                        game.Input.InputItems.Keys.U = false;
                        var ship = ShipHelper.GetPlayerShip(game);
                        if (ship.Docked) {
                            var distance = (int)Vector3.Distance(ship.Model.Position, Model.Position) / HudObject.DivisionDistanceValue;
                            if (distance < HudObject.DockDistanceAccept) {
                                UnDock(game, ship, Model.WorldObject);
                            }
                        }
                    }
                    if (game.Input.InputItems.Keys.C) {
                        game.GameState.Model.DockedGameState = Enum.DockedGameStateEnum.Commodities;
                        ListCommoditiesForSale(game, Enum.ModelType.Station, stationID.Value);
                    }
                    break;
                case Enum.GameStates.Playing:
                    if (game.Input.InputItems.Keys.D) { // DOCK
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
                    break;
            }
        }
        /// <summary>
        /// Draw
        /// </summary>
        /// <param name="game"></param>
        /// <param name="Model"></param>
        /// <param name="stationID"></param>
        public virtual void Draw(RoguelancerGame game, GameModel Model, int? stationID) {
            switch (game.GameState.Model.CurrentGameState) {
                case GameStates.Docked:
                    switch (game.GameState.Model.DockedGameState) {
                        case DockedGameStateEnum.Commodities:
                            var rect = new Rectangle(0, 0, game.GraphicsDevice.PresentationParameters.BackBufferWidth, game.GraphicsDevice.PresentationParameters.BackBufferHeight);
                            game.Graphics.Model.SpriteBatch.Draw(DockableObjectModel.BackgroundTexture, rect, Color.White);
                            break;
                    }
                    break;
            }
        }
    }
}