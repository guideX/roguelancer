// Roguelancer 0.1 Pre Alpha by Leon Aiossa
// http://www.team-nexgen.org
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Content;
using System;
using Roguelancer.Functionality;
using Roguelancer.Interfaces;
using System.Collections.Generic;
using System.Linq;
using Roguelancer.Settings;
using Roguelancer.Models;
namespace Roguelancer.Objects {
    public class ShipCollection : IGame {
        public List<Ship> ships { get; set; }
        public ShipCollection(RoguelancerGame _Game) {
            Reset(_Game);
        }
        public void Reset(RoguelancerGame _Game) {
            ships = new List<Ship>();
            Ship playerShip = new Ship(_Game);
            Ship tempShip;
            playerShip.model.worldObject = _Game.settings.starSystemSettings[0].ships.Where(s => s.settingsModelObject.modelType == ModelType.Ship && s.settingsModelObject.isPlayer == true).FirstOrDefault();
            playerShip.playerShipControl.useInput = true;
            ships.Add(playerShip);
            foreach(ModelWorldObjects modelWorldObject in _Game.settings.starSystemSettings[0].ships.Where(s => s.settingsModelObject.isPlayer == false).ToList()) {
                tempShip = new Ship(_Game);
                tempShip.model = new GameModel(_Game);
                tempShip.model.worldObject = ModelWorldObjects.Clone(modelWorldObject);
                tempShip.playerShipControl.useInput = false;
                tempShip.model.modelMode = Enum.ModelModeEnum.ship;
                ships.Add(Ship.Clone(tempShip, _Game));
            }
        }
        public Ship GetPlayerShip(RoguelancerGame _Game) {
            foreach(Ship ship in ships) {
                if(ship.playerShipControl.useInput == true) {
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
        public PlayerShipControl playerShipControl;
        public Ship(RoguelancerGame _Game) {
            model = new GameModel(_Game);
            playerShipControl = new PlayerShipControl();
            model.particleSystemEnabled = true;
        }
        public static Ship Clone(Ship oldShip, RoguelancerGame _Game) {
            Ship ship;
            ship = new Ship(_Game);
            ship.playerShipControl = oldShip.playerShipControl;
            ship.model = oldShip.model;
            return ship;
        }
        public void Initialize(RoguelancerGame _Game) {
            model.Initialize(_Game);
            model.modelMode = Enum.ModelModeEnum.ship;
            if(playerShipControl.useInput) {
                playerShipControl = new PlayerShipControl();
                playerShipControl.Initialize(_Game);
            }
        }
        public void LoadContent(RoguelancerGame _Game) {
            model.LoadContent(_Game);
            if(playerShipControl.useInput) {
                playerShipControl.LoadContent(_Game);
            }
        }
        public void Update(RoguelancerGame _Game) {
            if(_Game.gameState.currentGameState == GameState.GameStates.playing) {
                if(playerShipControl.useInput) {
                    playerShipControl.UpdateModel(model, _Game);
                    if(_Game.input.lInputItems.toggles.toggleCamera == false) {
                        model.Update(_Game);
                    }
                } else {
                    model.Update(_Game);
                }
            }
        }
        public void Draw(RoguelancerGame _Game) {
            model.Draw(_Game);
            if(playerShipControl.useInput) {
                playerShipControl.Draw(_Game);
            }
        }
    }
}