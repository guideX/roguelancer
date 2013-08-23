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
namespace Roguelancer.Objects {
    public class ShipCollection: IGame {
        public List<ship> ships { get; set; }
        public ShipCollection() {
            ships = new List<ship>();
            ship playerShip = new ship();
            playerShip.model.settings = new Settings().playerShip;
            playerShip.playerShipControl.useInput = true;
            ships.Add(playerShip);
        }
        public ship GetPlayerShip() {
            foreach(ship ship in ships) {
                if(ship.playerShipControl.useInput == true) {
                    return ship;
                }
            }
            return new ship();
        }
        public void Initialize(clsGame _Game) {
            foreach(ship _ship in ships) {
                _ship.Initialize(_Game);
            }
        }
        public void LoadContent(clsGame _Game) {
            foreach(ship _ship in ships) {
                _ship.LoadContent(_Game);
            }
        }
        public void Update(clsGame _Game) {
            foreach(ship _ship in ships) {
                _ship.Update(_Game);
            }
        }
        public void Draw(clsGame _Game) {
            foreach(ship _ship in ships) {
                _ship.Draw(_Game);
            }
        }
    }
    public class ship : IGame {
        public clsModel model;
        public clsPlayerShipControl playerShipControl;
        public string debugText { get; set; }
        public ship() {
            model = new clsModel();
            playerShipControl = new clsPlayerShipControl();
        }
        public void Initialize(clsGame _Game) {
            model.drawMode = clsModel.DrawMode.mainModel;
            model.Initialize(_Game);
            if(playerShipControl.useInput) {
                playerShipControl = new clsPlayerShipControl();
                playerShipControl.Initialize(_Game);
            }
        }
        public void LoadContent(clsGame _Game) {
            model.LoadContent(_Game);
            if(playerShipControl.useInput) {
                playerShipControl.model = model;
                playerShipControl.LoadContent(_Game);
            }
        }
        public void Update(clsGame _Game) {
            if(playerShipControl.useInput) {
                playerShipControl.Update(_Game);
            }
            if(playerShipControl.useInput) {
                if(_Game.input.lInputItems.lToggles.lToggleCamera == false) {
                    model.Update(_Game);
                }
            } else {
                model.Update(_Game);
            }
            debugText = model.position.X.ToString() + " - " + model.position.Y.ToString() + " - " + model.position.Z.ToString();
        }
        public void Draw(clsGame _Game) {
            model.Draw(_Game);
            if(playerShipControl.useInput) {
                playerShipControl.Draw(_Game);
            }
        }
    }
}