// Roguelancer 0.1 Pre Alpha by Leon Aiossa
// http://www.team-nexgen.org
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Content;
using System;
using Roguelancer.Functionality;
using Roguelancer.Interfaces;
namespace Roguelancer.Objects {
    public class clsShip : IGame {
        public clsModel model;
        public clsPlayerShipControl playerShipControl;
        public string debugText { get; set; }
        public clsShip() {
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