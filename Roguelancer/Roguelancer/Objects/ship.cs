﻿// Roguelancer 0.1 Pre Alpha by Leon Aiossa
// http://www.team-nexgen.org
using System.Linq;
using System.Collections.Generic;
using Roguelancer.Functionality;
using Roguelancer.Interfaces;
using Roguelancer.Settings;
using Roguelancer.Models;
using Roguelancer.Enum;
namespace Roguelancer.Objects {
    /// <summary>
    /// Ship Collection
    /// </summary>
    public class ShipCollection : IShipCollection {
        #region "public variables"
        /// <summary>
        /// Ships
        /// </summary>
        public List<Ship> Ships { get; set; }
        #endregion
        #region "public functions"
        /// <summary>
        /// Entry Point
        /// </summary>
        /// <param name="game"></param>
        public ShipCollection(RoguelancerGame game) {
            try {
                Reset(game);
            } catch {
                throw;
            }
        }
        /// <summary>
        /// Initialize
        /// </summary>
        /// <param name="game"></param>
        public void Initialize(RoguelancerGame game) {
            foreach (Ship _ship in Ships) {
                _ship.Initialize(game);
            }
        }
        /// <summary>
        /// Load Content
        /// </summary>
        /// <param name="game"></param>
        public void LoadContent(RoguelancerGame game) {
            try {
                foreach (Ship _ship in Ships) {
                    _ship.LoadContent(game);
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
                foreach (Ship _ship in Ships) {
                    _ship.Update(game);
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
                foreach (Ship _ship in Ships) {
                    _ship.Draw(game);
                }
            } catch {
                throw;
            }
        }
        /// <summary>
        /// Reset
        /// </summary>
        /// <param name="game"></param>
        public void Reset(RoguelancerGame game) {
            try {
                Ships = new List<Ship>();
                var playerShip = new Ship(game);
                Ship tempShip;
                playerShip.model.WorldObject = game.Settings.StarSystemSettings[0].ships.Where(s => s.SettingsModelObject.modelType == ModelType.Ship && s.SettingsModelObject.isPlayer == true).FirstOrDefault();
                playerShip.PlayerShipControl.UseInput = true;
                Ships.Add(playerShip);
                foreach (ModelWorldObjects modelWorldObject in game.Settings.StarSystemSettings[0].ships.Where(s => s.SettingsModelObject.isPlayer == false).ToList()) {
                    tempShip = new Ship(game);
                    tempShip.model = new GameModel(game, null);
                    tempShip.model.WorldObject = ModelWorldObjects.Clone(modelWorldObject);
                    tempShip.PlayerShipControl.UseInput = false;
                    //tempShip.model.ModelMode = Enum.ModelModeEnum.Ship;
                    Ships.Add(Ship.Clone(tempShip, game));
                }
            } catch {
                throw;
            }
        }
        #endregion
    }
    public class Ship : IGame {
        #region "public variables"
        /// <summary>
        /// Game Model
        /// </summary>
        public GameModel model;
        /// <summary>
        /// Player Ship Control
        /// </summary>
        public IPlayerShipControl PlayerShipControl;
        #endregion
        #region "public functions"
        /// <summary>
        /// Ship
        /// </summary>
        /// <param name="game"></param>
        public Ship(RoguelancerGame game) {
            try {
                model = new GameModel(game, null);
                PlayerShipControl = new PlayerShipControl();
            } catch {
                throw;
            }
        }
        /// <summary>
        /// Clone
        /// </summary>
        /// <param name="oldShip"></param>
        /// <param name="game"></param>
        /// <returns></returns>
        public static Ship Clone(Ship oldShip, RoguelancerGame game) {
            try {
                Ship ship;
                ship = new Ship(game);
                ship.PlayerShipControl = oldShip.PlayerShipControl;
                ship.model = oldShip.model;
                return ship;
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
                model.Initialize(game);
                //model.ModelMode = Enum.ModelModeEnum.Ship;
                if (PlayerShipControl.UseInput) {
                    PlayerShipControl = new PlayerShipControl();
                    PlayerShipControl.Initialize(game);
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
                model.LoadContent(game);
                if (PlayerShipControl.UseInput) {
                    PlayerShipControl.LoadContent(game);
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
                if (game.GameState.CurrentGameState == GameStates.Playing) {
                    if (PlayerShipControl.UseInput) {
                        PlayerShipControl.UpdateModel(model, game);
                        if (game.Input.InputItems.Toggles.ToggleCamera == false) {
                            model.Update(game);
                        }
                    } else {
                        model.Update(game);
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
                model.Draw(game);
                if (PlayerShipControl.UseInput) {
                    PlayerShipControl.Draw(game);
                }
            } catch {
                throw;
            }
        }
        #endregion
    }
}