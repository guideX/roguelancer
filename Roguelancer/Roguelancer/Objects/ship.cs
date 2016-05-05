// Roguelancer 0.1 Pre Alpha by Leon Aiossa
// http://www.team-nexgen.com
using System.Linq;
using Roguelancer.Functionality;
using Roguelancer.Interfaces;
using Roguelancer.Settings;
using Roguelancer.Models;
using Roguelancer.Enum;
using Roguelancer.Helpers;
namespace Roguelancer.Objects {
    /// <summary>
    /// Ship Collection
    /// </summary>
    public class ShipCollection : IShipCollection {
        #region "public variables"
        /// <summary>
        /// Model
        /// </summary>
        public ShipCollectionModel Model { get; set; }
        #endregion
        #region "public functions"
        /// <summary>
        /// Entry Point
        /// </summary>
        /// <param name="game"></param>
        public ShipCollection(RoguelancerGame game) {
            Model = new ShipCollectionModel();
            Reset(game);
        }
        /// <summary>
        /// Initialize
        /// </summary>
        /// <param name="game"></param>
        public void Initialize(RoguelancerGame game) {
            foreach (var _ship in Model.Ships) {
                _ship.Initialize(game);
            }
        }
        /// <summary>
        /// Load Content
        /// </summary>
        /// <param name="game"></param>
        public void LoadContent(RoguelancerGame game) {
            foreach (var _ship in Model.Ships) {
                _ship.LoadContent(game);
            }
        }
        /// <summary>
        /// Update
        /// </summary>
        /// <param name="game"></param>
        public void Update(RoguelancerGame game) {
            foreach (var _ship in Model.Ships) {
                _ship.Update(game);
            }
        }
        /// <summary>
        /// Draw
        /// </summary>
        /// <param name="game"></param>
        public void Draw(RoguelancerGame game) {
            foreach (var _ship in Model.Ships) {
                _ship.Draw(game);
            }
        }
        /// <summary>
        /// Reset
        /// </summary>
        /// <param name="game"></param>
        public void Reset(RoguelancerGame game) {
            Model = new ShipCollectionModel();
            var playerShip = new Ship(game);
            Ship tempShip;
            playerShip.Model.WorldObject = game.Settings.StarSystemSettings[game.StarSystemId].Ships.Where(s => s.SettingsModelObject.modelType == ModelType.Ship && s.SettingsModelObject.isPlayer).FirstOrDefault();
            playerShip.ShipModel.PlayerShipControl.Model.UseInput = true;
            Model.Ships.Add(playerShip);
            foreach (var modelWorldObject in game.Settings.StarSystemSettings[game.StarSystemId].Ships.Where(s => !s.SettingsModelObject.isPlayer).ToList()) {
                tempShip = new Ship(game);
                tempShip.Model = new GameModel(game, null);
                tempShip.Model.WorldObject = ModelWorldObjects.Clone(modelWorldObject);
                tempShip.ShipModel.PlayerShipControl.Model.UseInput = false;
                Model.Ships.Add(ShipHelper.Clone(tempShip, game));
            }
        }
        /// <summary>
        /// Dispose
        /// </summary>
        public void Dispose(RoguelancerGame game) {
            Model = null;
        }
        #endregion
    }
    /// <summary>
    /// Ship
    /// </summary>
    public class Ship : IGame, ISensorObject, IDockableShip {
        #region "public variables"
        /// <summary>
        /// Docked
        /// </summary>
        public bool Docked { get; set; }
        /// <summary>
        /// Ship Model
        /// </summary>
        public ShipModel ShipModel { get; set; }
        /// <summary>
        /// Game Model
        /// </summary>
        public GameModel Model { get; set; }
        #endregion
        #region "public functions"
        /// <summary>
        /// Ship
        /// </summary>
        /// <param name="game"></param>
        public Ship(RoguelancerGame game) {
            Reset(game);
        }
        /// <summary>
        /// Initialize
        /// </summary>
        /// <param name="game"></param>
        public void Initialize(RoguelancerGame game) {
            Model.Initialize(game);
            if (ShipModel.PlayerShipControl.Model.UseInput) {
                ShipModel.PlayerShipControl = new PlayerShipControl(game);
            }
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
            if (game.GameState.Model.CurrentGameState == GameStates.Playing) {
                if (ShipModel.PlayerShipControl.Model.UseInput) {
                    ShipModel.PlayerShipControl.UpdateModel(Model, game);
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
            }
        }
        /// <summary>
        /// Dispose
        /// </summary>
        public void Dispose(RoguelancerGame game) {
            Docked = false;
            Model.Dispose(game);
            Model = null;
            ShipModel = null;
        }
        /// <summary>
        /// Reset
        /// </summary>
        /// <param name="game"></param>
        public void Reset(RoguelancerGame game) {
            ShipModel = new ShipModel();
            ShipModel.Money = 2000.00m;
            ShipModel.CargoHold = new CargoHoldModel();
            Model = new GameModel(game, null);
            ShipModel.PlayerShipControl = new PlayerShipControl(game);
            //HardPoints = new List<HardPoint>();
        }
        #endregion
    }
}