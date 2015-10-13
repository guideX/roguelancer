// Roguelancer 0.1 Pre Alpha by Leon Aiossa
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
    public class ShipCollection : IGame {
        /// <summary>
        /// Ships
        /// </summary>
        public List<Ship> ships { get; set; }
        /// <summary>
        /// Entry Point
        /// </summary>
        /// <param name="_Game"></param>
        public ShipCollection(RoguelancerGame _Game) {
            Reset(_Game);
        }
        /// <summary>
        /// Reset
        /// </summary>
        /// <param name="_Game"></param>
        public void Reset(RoguelancerGame _Game) {
            ships = new List<Ship>();
            var playerShip = new Ship(_Game);
            Ship tempShip;
            playerShip.model.WorldObject = _Game.Settings.starSystemSettings[0].ships.Where(s => s.settingsModelObject.modelType == ModelType.Ship && s.settingsModelObject.isPlayer == true).FirstOrDefault();
            playerShip.PlayerShipControl.UseInput = true;
            ships.Add(playerShip);
            foreach(ModelWorldObjects modelWorldObject in _Game.Settings.starSystemSettings[0].ships.Where(s => s.settingsModelObject.isPlayer == false).ToList()) {
                tempShip = new Ship(_Game);
                tempShip.model = new GameModel(_Game);
                tempShip.model.WorldObject = ModelWorldObjects.Clone(modelWorldObject);
                tempShip.PlayerShipControl.UseInput = false;
                tempShip.model.ModelMode = Enum.ModelModeEnum.Ship;
                ships.Add(Ship.Clone(tempShip, _Game));
            }
        }
        public Ship GetPlayerShip(RoguelancerGame _Game) {
            foreach(Ship ship in ships) {
                if(ship.PlayerShipControl.UseInput == true) {
                    return ship;
                }
            }
            return new Ship(_Game);
        }
        public void Initialize(RoguelancerGame _Game) {
            foreach(Ship _ship in ships) {
                _ship.Initialize(_Game);
            }
        }
        public void LoadContent(RoguelancerGame _Game) {
            foreach(Ship _ship in ships) {
                _ship.LoadContent(_Game);
            }
        }
        public void Update(RoguelancerGame _Game) {
            foreach(Ship _ship in ships) {
                _ship.Update(_Game);
            }
        }
        public void Draw(RoguelancerGame _Game) {
            foreach(Ship _ship in ships) {
                _ship.Draw(_Game);
            }
        }
    }
    public class Ship : IGame {
        public GameModel model;
        public IPlayerShipControl PlayerShipControl;
        public Ship(RoguelancerGame _Game) {
            model = new GameModel(_Game);
            PlayerShipControl = new PlayerShipControl();
            model.ParticleSystemEnabled = true;
        }
        public static Ship Clone(Ship oldShip, RoguelancerGame _Game) {
            Ship ship;
            ship = new Ship(_Game);
            ship.PlayerShipControl = oldShip.PlayerShipControl;
            ship.model = oldShip.model;
            return ship;
        }
        public void Initialize(RoguelancerGame _Game) {
            model.Initialize(_Game);
            model.ModelMode = Enum.ModelModeEnum.Ship;
            if(PlayerShipControl.UseInput) {
                PlayerShipControl = new PlayerShipControl();
                PlayerShipControl.Initialize(_Game);
            }
        }
        public void LoadContent(RoguelancerGame _Game) {
            model.LoadContent(_Game);
            if(PlayerShipControl.UseInput) {
                PlayerShipControl.LoadContent(_Game);
            }
        }
        public void Update(RoguelancerGame _Game) {
            if(_Game.GameState.CurrentGameState == GameStates.Playing) {
                if(PlayerShipControl.UseInput) {
                    PlayerShipControl.UpdateModel(model, _Game);
                    if(_Game.Input.InputItems.Toggles.ToggleCamera == false) {
                        model.Update(_Game);
                    }
                } else {
                    model.Update(_Game);
                }
            }
        }
        public void Draw(RoguelancerGame _Game) {
            model.Draw(_Game);
            if(PlayerShipControl.UseInput) {
                PlayerShipControl.Draw(_Game);
            }
        }
    }
}