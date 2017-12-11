using System;
using System.Text;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Roguelancer.Models;
using Roguelancer.Enum;
using Roguelancer.Helpers;
using Roguelancer.Interfaces;
namespace Roguelancer.Objects.Base {
    /// <summary>
    /// Dockable Object
    /// </summary>
    public abstract class DockableObject : IDockableObject {
        #region "variables"
        /// <summary>
        /// Dockable Object Model
        /// </summary>
        public DockableObjectModel DockableObjectModel { get; set; }
        #endregion
        #region "methods"
        /// <summary>
        /// Dockable Object
        /// </summary>
        public DockableObject() {
            DockableObjectModel = new DockableObjectModel();
        }
        /// <summary>
        /// Initialize
        /// </summary>
        /// <param name="game"></param>
        /// <param name="Model"></param>
        /// <param name="stationID"></param>
        public virtual void Initialize(RoguelancerGame game, GameModel Model, int? stationID) {
            DockableObjectModel.DestinationRectangle = new Rectangle(0, 0, game.GraphicsDevice.PresentationParameters.BackBufferWidth, game.GraphicsDevice.PresentationParameters.BackBufferHeight);
        }
        /// <summary>
        /// Load Content
        /// </summary>
        /// <param name="game"></param>
        /// <param name="Model"></param>
        /// <param name="stationID"></param>
        public virtual void LoadContent(RoguelancerGame game, GameModel Model, int? stationID) {
            DockableObjectModel.BackgroundTexture = game.Content.Load<Texture2D>(@"Menus\allpanels");
            foreach (var obj in game.Settings.Model.StationPriceModels.Where(p => p.StationId == stationID).ToList()) {
                obj.Image = game.Content.Load<Texture2D>(obj.ImagePath);
                obj.ImageContainer = game.Content.Load<Texture2D>(obj.ImagePathContainer);
            }
        }
        /// <summary>
        /// Update
        /// </summary>
        /// <param name="game"></param>
        /// <param name="Model"></param>
        /// <param name="stationID"></param>
        public virtual void Update(RoguelancerGame game, GameModel Model, int? stationID) {
            var playerShip = ShipHelper.GetPlayerShip(game);
            switch (game.GameState.Model.CurrentGameState) { // Current Game State
                case Enum.GameStatesEnum.Docked: // Docked
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
                            if (game.Input.InputItems.Keys.D1.IsKeyDown) { if (0 < DockableObjectModel.StationPrices.Count) { PurchaseCommodity(game, DockableObjectModel.StationPrices[0].CommoditiesId, 1); } }
                            if (game.Input.InputItems.Keys.D2.IsKeyDown) { if (1 < DockableObjectModel.StationPrices.Count) { PurchaseCommodity(game, DockableObjectModel.StationPrices[1].CommoditiesId, 1); } }
                            if (game.Input.InputItems.Keys.D3.IsKeyDown) { if (2 < DockableObjectModel.StationPrices.Count) { PurchaseCommodity(game, DockableObjectModel.StationPrices[2].CommoditiesId, 1); } }
                            if (game.Input.InputItems.Keys.D4.IsKeyDown) { if (3 < DockableObjectModel.StationPrices.Count) { PurchaseCommodity(game, DockableObjectModel.StationPrices[3].CommoditiesId, 1); } }
                            if (game.Input.InputItems.Keys.D5.IsKeyDown) { if (4 < DockableObjectModel.StationPrices.Count) { PurchaseCommodity(game, DockableObjectModel.StationPrices[4].CommoditiesId, 1); } }
                            if (game.Input.InputItems.Keys.D6.IsKeyDown) { if (5 < DockableObjectModel.StationPrices.Count) { PurchaseCommodity(game, DockableObjectModel.StationPrices[5].CommoditiesId, 1); } }
                            if (game.Input.InputItems.Keys.D7.IsKeyDown) { if (6 < DockableObjectModel.StationPrices.Count) { PurchaseCommodity(game, DockableObjectModel.StationPrices[6].CommoditiesId, 1); } }
                            if (game.Input.InputItems.Keys.D8.IsKeyDown) { if (7 < DockableObjectModel.StationPrices.Count) { PurchaseCommodity(game, DockableObjectModel.StationPrices[7].CommoditiesId, 1); } }
                            if (game.Input.InputItems.Keys.D9.IsKeyDown) { if (8 < DockableObjectModel.StationPrices.Count) { PurchaseCommodity(game, DockableObjectModel.StationPrices[8].CommoditiesId, 1); } }
                            switch (game.GameState.Model.CurrentGameState) {
                                case GameStatesEnum.Docked:
                                    switch (game.GameState.Model.DockedGameState) {
                                        case DockedGameStateEnum.Commodities:
                                            var n = 0;
                                            var nn1 = 100;
                                            var nn2 = 150;
                                            foreach (var obj in game.Settings.Model.StationPriceModels.Where(p => p.StationId == stationID).ToList()) {
                                                var color = Color.White;
                                                obj.ImageRect = new Rectangle(4 + nn1, 4 + n + nn2, 53, 48);
                                                obj.ImageContainerRect = new Rectangle(1 + nn1, 1 + n + nn2, 277, 55);
                                                n = n + 50;
                                            }
                                            break;
                                    }
                                    break;
                            }
                            break;
                        case Enum.DockedGameStateEnum.ShipDealer:
                            // TODO
                            // OPTION1 = Purchase Ship 1
                            // OPTION2 = Purchase Ship 2
                            // Etc
                            break;
                    }
                    if (game.Settings.Model.KeyAssignments.Undock.FindKeyBoardStatus(game.Input.InputItems.Keys).IsKeyDown) {
                        if (playerShip.Docked) {
                            var distance = (int)Vector3.Distance(playerShip.Model.Position, Model.Position) / HudObject.DivisionDistanceValue;
                            UnDock(game, playerShip, Model);
                        }
                    }
                    if (game.Input.InputItems.Keys.C.IsKeyDown) {
                        game.GameState.Model.DockedGameState = Enum.DockedGameStateEnum.Commodities;
                        ListCommoditiesForSale(game, Enum.ModelType.Station, stationID.Value);
                    }
                    if (game.Input.InputItems.Keys.H.IsKeyDown) {
                        game.GameState.Model.DockedGameState = Enum.DockedGameStateEnum.Hanger;
                    }
                    if (game.Input.InputItems.Keys.E.IsKeyDown) {
                        game.GameState.Model.DockedGameState = Enum.DockedGameStateEnum.Hanger;
                    }
                    break;
                case Enum.GameStatesEnum.Playing:
                    var dock = game.Settings.Model.KeyAssignments.Dock.FindKeyBoardStatus(game.Input.InputItems.Keys);
                    if (dock.WasKeyPressed) {
                        dock.IsKeyDown = false;
                        if (!playerShip.Docked) {
                            var distance = (int)Vector3.Distance(playerShip.Model.Position, playerShip.ShipModel.PlayerShipControl.Model.CurrentTarget.Position) / HudObject.DivisionDistanceValue;
                            if (distance < HudObject.DockDistanceAccept) {
                                Dock(game, playerShip, playerShip.ShipModel.PlayerShipControl.Model.CurrentTarget);
                                game.Input.InputItems.Toggles.Cruise = false;
                            } else if (distance < HudObject.DockDistanceAccept * 2) {
                                if (playerShip.ShipModel.PlayerShipControl.Model.CurrentTarget != null) {
                                    playerShip.GoingToObject = playerShip.ShipModel.PlayerShipControl.Model.CurrentTarget.GetStation(); // Set Docket To
                                    DebugTextHelper.SetText(game, "Automatic Docking Initiated", true);
                                    playerShip.ShipModel.PlayerShipControl.Model.UseAutoDock = true;
                                } else {
                                    DebugTextHelper.SetText(game, "Nothing targetted.", true);
                                }
                            } else {
                                game.Input.InputItems.Toggles.Cruise = false;
                                playerShip.Model.Velocity = Vector3.Zero;
                                DebugTextHelper.SetText(game, "Dock failed, destination is too far. " + distance.ToString(), true);
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
                case GameStatesEnum.Docked:
                    switch (game.GameState.Model.DockedGameState) {
                        case DockedGameStateEnum.Commodities:
                            game.Graphics.Model.SpriteBatch.Draw(DockableObjectModel.BackgroundTexture, DockableObjectModel.DestinationRectangle, Color.White);
                            foreach (var obj in game.Settings.Model.StationPriceModels.Where(p => p.StationId == stationID).ToList()) {
                                var color = Color.White;
                                game.Graphics.Model.SpriteBatch.Draw(obj.Image, obj.ImageRect, color);
                                game.Graphics.Model.SpriteBatch.Draw(obj.ImageContainer, obj.ImageContainerRect, color);
                            }
                            break;
                    }
                    break;
            }
        }
        /// <summary>
        /// Dock
        /// </summary>
        /// <param name="game"></param>
        /// <param name="ship"></param>
        public virtual void Dock(RoguelancerGame game, ShipObject ship, GameModel dockTo) {
            if (!ship.Docked) {
                var playerShip = ShipHelper.GetPlayerShip(game); // Get Player Ship
                ship.Model.Velocity = new Vector3(0f, 0f, 0f);
                ship.Model.CurrentThrust = 0f;
                ship.DockedTo = dockTo; // Set Docket To
                ship.Docked = true; // Set Docked Value
                DockableObjectModel.DockedShips.Add(ship); // Add to Docked Ships
                if (playerShip == ship) { // If Docking Ship is Player Ship
                    game.GameState.Model.CurrentGameState = Enum.GameStatesEnum.Docked; // Set Current Game State to Docked
                    DebugTextHelper.SetText(game, "Docking To " + dockTo.WorldObject.Description, true);
                }
            }
        }
        /// <summary>
        /// Undock
        /// </summary>
        /// <param name="game"></param>
        /// <param name="ship"></param>
        public virtual void UnDock(RoguelancerGame game, ShipObject ship, GameModel undockFrom) {
            if (ship.Docked) {
                var playerShip = ShipHelper.GetPlayerShip(game); // Get Player Ship
                if (playerShip == ship) game.GameState.Model.CurrentGameState = Enum.GameStatesEnum.Playing;
                ship.Docked = false;
                ship.DockedTo = null;
                DockableObjectModel.DockedShips.Remove(ship);
                DebugTextHelper.SetText(game, "Undocked from '" + undockFrom.WorldObject.Description + "'.", true);
            }
        }
        /// <summary>
        /// Commodities for Sale
        /// </summary>
        /// <param name="game"></param>
        /// <returns></returns>
        public virtual void ListCommoditiesForSale(RoguelancerGame game, ModelType modelType, int stationID) {
            switch (modelType) {
                case ModelType.Planet:
                case ModelType.Station:
                    var sb = new StringBuilder();
                    var n = 0;
                    foreach (var obj in game.Settings.Model.StationPriceModels.Where(p => p.StationId == stationID).ToList()) {
                        n++;
                        var commodity = game.Settings.Model.CommoditiesModels.Where(c => c.CommodityId == obj.CommoditiesId).FirstOrDefault();
                        sb.AppendLine("[" + n.ToString() + "] Description: " + commodity.Description + ", Price: " + obj.Price.ToString() + Environment.NewLine);
                    }
                    if (game.GameState.Model.CurrentGameState == Enum.GameStatesEnum.Docked && game.GameState.Model.DockedGameState == Enum.DockedGameStateEnum.Commodities) {
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
        #endregion
    }
}