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
namespace Roguelancer.Objects {
    public class ShipCollection : IGame {
        public List<Ship> ships { get; set; }
        public ShipCollection(RoguelancerGame _Game) {
            ships = new List<Ship>();
            Ship playerShip = new Ship();
            Ship tempShip;
            playerShip.model.worldObject = _Game.settings.starSystemSettings[0].ships.Where(s => s.settingsModelObject.modelType == ModelType.Ship && s.settingsModelObject.isPlayer == true).FirstOrDefault();
            playerShip.playerShipControl.useInput = true;
            ships.Add(playerShip);
            foreach(ModelWorldObjects modelWorldObject in _Game.settings.starSystemSettings[0].ships.Where(s => s.settingsModelObject.isPlayer == false).ToList()) {
                tempShip = new Ship();
                tempShip.model = new GameModel();
                tempShip.model.worldObject = ModelWorldObjects.Clone(modelWorldObject);
                tempShip.playerShipControl.useInput = false;
                tempShip.model.modelMode = GameModel.ModelMode.ship;
                ships.Add(Ship.Clone(tempShip));
            }
        }
        public Ship GetPlayerShip() {
            foreach(Ship ship in ships) {
                if(ship.playerShipControl.useInput == true) {
                    return ship;
                }
            }
            return new Ship();
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
        public List<HardPoint> hardPoints;
        public Ship() {
            model = new GameModel();
            playerShipControl = new PlayerShipControl();
            hardPoints = new List<HardPoint>();
        }
        public static Ship Clone(Ship oldShip) {
            Ship ship;
            ship = new Ship();
            ship.playerShipControl = oldShip.playerShipControl;
            ship.model = oldShip.model;
            return ship;
        }
        public void Initialize(RoguelancerGame _Game) {
            model.Initialize(_Game);
            model.modelMode = GameModel.ModelMode.ship;
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
            if(playerShipControl.useInput) {
                playerShipControl.UpdateModel(model, _Game);
                //playerShipControl.Update(_Game);
            }
            if(playerShipControl.useInput) {
                if(_Game.input.lInputItems.toggles.toggleCamera == false) {
                    model.Update(_Game);
                }
            } else {
                model.Update(_Game);
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