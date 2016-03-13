// Roguelancer 0.1 Pre Alpha by Leon Aiossa
// http://www.team-nexgen.com
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
        /// <summary>
        /// Ships
        /// </summary>
        public List<Ship> Ships { get; set; }
        #region "public functions"
        /// <summary>
        /// Entry Point
        /// </summary>
        /// <param name="game"></param>
        public ShipCollection(RoguelancerGame game) {
            Reset(game);
        }
        /// <summary>
        /// Initialize
        /// </summary>
        /// <param name="game"></param>
        public void Initialize(RoguelancerGame game) {
            foreach (var _ship in Ships) {
                _ship.Initialize(game);
            }
        }
        /// <summary>
        /// Load Content
        /// </summary>
        /// <param name="game"></param>
        public void LoadContent(RoguelancerGame game) {
            foreach (var _ship in Ships) {
                _ship.LoadContent(game);
            }
        }
        /// <summary>
        /// Update
        /// </summary>
        /// <param name="game"></param>
        public void Update(RoguelancerGame game) {
            foreach (var _ship in Ships) {
                _ship.Update(game);
            }
        }
        /// <summary>
        /// Draw
        /// </summary>
        /// <param name="game"></param>
        public void Draw(RoguelancerGame game) {
            foreach (var _ship in Ships) {
                _ship.Draw(game);
            }
        }
        /// <summary>
        /// Reset
        /// </summary>
        /// <param name="game"></param>
        public void Reset(RoguelancerGame game) {
            Ships = new List<Ship>();
            var playerShip = new Ship(game);
            Ship tempShip;
            playerShip.Model.WorldObject = game.Settings.StarSystemSettings[game.StarSystemId].Ships.Where(s => s.SettingsModelObject.modelType == ModelType.Ship && s.SettingsModelObject.isPlayer).FirstOrDefault();
            playerShip.PlayerShipControl.UseInput = true;
            Ships.Add(playerShip);
            foreach (var modelWorldObject in game.Settings.StarSystemSettings[game.StarSystemId].Ships.Where(s => !s.SettingsModelObject.isPlayer).ToList()) {
                tempShip = new Ship(game);
                tempShip.Model = new GameModel(game, null);
                tempShip.Model.WorldObject = ModelWorldObjects.Clone(modelWorldObject);
                tempShip.PlayerShipControl.UseInput = false;
                Ships.Add(Ship.Clone(tempShip, game));
            }
        }
        #endregion
    }
    /// <summary>
    /// Ship
    /// </summary>
    public class Ship : IGame, ISensorObject, IDockableShip {
        #region "public variables"
        /// <summary>
        /// Hard Points
        /// </summary>
        //public List<HardPoint> HardPoints { get; set; }
        /// <summary>
        /// Docked
        /// </summary>
        public bool Docked { get; set; }
        /// <summary>
        /// Game Model
        /// </summary>
        public GameModel Model { get; set; }
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
            Model = new GameModel(game, null);
            PlayerShipControl = new PlayerShipControl();
            //HardPoints = new List<HardPoint>();
        }
        /// <summary>
        /// Clone
        /// </summary>
        /// <param name="oldShip"></param>
        /// <param name="game"></param>
        /// <returns></returns>
        public static Ship Clone(Ship oldShip, RoguelancerGame game) {
            Ship ship;
            ship = new Ship(game);
            ship.PlayerShipControl = oldShip.PlayerShipControl;
            ship.Model = oldShip.Model;
            return ship;
        }
        /// <summary>
        /// Initialize
        /// </summary>
        /// <param name="game"></param>
        public void Initialize(RoguelancerGame game) {
            Model.Initialize(game);
            if (PlayerShipControl.UseInput) {
                PlayerShipControl = new PlayerShipControl();
                PlayerShipControl.Initialize(game);
            }
        }
        /// <summary>
        /// Load Content
        /// </summary>
        /// <param name="game"></param>
        public void LoadContent(RoguelancerGame game) {
            Model.LoadContent(game);
            if (PlayerShipControl.UseInput) {
                PlayerShipControl.LoadContent(game);
            }
        }
        /// <summary>
        /// Update
        /// </summary>
        /// <param name="game"></param>
        public void Update(RoguelancerGame game) {
            if (game.GameState.CurrentGameState == GameStates.Playing) {
                if (PlayerShipControl.UseInput) {
                    PlayerShipControl.UpdateModel(Model, game);
                    if (!game.Input.InputItems.Toggles.ToggleCamera) {
                        Model.Update(game);
                    }
                } else {
                    Model.Update(game);
                }
            }
        }
        /// <summary>
        /// Draw
        /// </summary>
        /// <param name="game"></param>
        public void Draw(RoguelancerGame game) {
            if (!Docked) {
                Model.Draw(game);
                if (PlayerShipControl.UseInput) {
                    PlayerShipControl.Draw(game);
                }
            }
        }
        #endregion
    }
}