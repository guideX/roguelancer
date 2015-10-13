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
        /// <param name="game"></param>
        public ShipCollection(RoguelancerGame game) {
            Reset(game);
        }
        /// <summary>
        /// Reset
        /// </summary>
        /// <param name="game"></param>
        public void Reset(RoguelancerGame game) {
            ships = new List<Ship>();
            var playerShip = new Ship(game);
            Ship tempShip;
            playerShip.model.WorldObject = game.Settings.starSystemSettings[0].ships.Where(s => s.settingsModelObject.modelType == ModelType.Ship && s.settingsModelObject.isPlayer == true).FirstOrDefault();
            playerShip.PlayerShipControl.UseInput = true;
            ships.Add(playerShip);
            foreach(ModelWorldObjects modelWorldObject in game.Settings.starSystemSettings[0].ships.Where(s => s.settingsModelObject.isPlayer == false).ToList()) {
                tempShip = new Ship(game);
                tempShip.model = new GameModel(game);
                tempShip.model.WorldObject = ModelWorldObjects.Clone(modelWorldObject);
                tempShip.PlayerShipControl.UseInput = false;
                tempShip.model.ModelMode = Enum.ModelModeEnum.Ship;
                ships.Add(Ship.Clone(tempShip, game));
            }
        }
        public Ship GetPlayerShip(RoguelancerGame game) {
            foreach(Ship ship in ships) {
                if(ship.PlayerShipControl.UseInput == true) {
                    return ship;
                }
            }
            return new Ship(game);
        }
        public void Initialize(RoguelancerGame game) {
            foreach(Ship _ship in ships) {
                _ship.Initialize(game);
            }
        }
        public void LoadContent(RoguelancerGame game) {
            foreach(Ship _ship in ships) {
                _ship.LoadContent(game);
            }
        }
        public void Update(RoguelancerGame game) {
            foreach(Ship _ship in ships) {
                _ship.Update(game);
            }
        }
        public void Draw(RoguelancerGame game) {
            foreach(Ship _ship in ships) {
                _ship.Draw(game);
            }
        }
    }
    public class Ship : IGame {
        public GameModel model;
        public IPlayerShipControl PlayerShipControl;
        public Ship(RoguelancerGame game) {
            model = new GameModel(game);
            PlayerShipControl = new PlayerShipControl();
            model.ParticleSystemEnabled = true;
        }
        public static Ship Clone(Ship oldShip, RoguelancerGame game) {
            Ship ship;
            ship = new Ship(game);
            ship.PlayerShipControl = oldShip.PlayerShipControl;
            ship.model = oldShip.model;
            return ship;
        }
        public void Initialize(RoguelancerGame game) {
            model.Initialize(game);
            model.ModelMode = Enum.ModelModeEnum.Ship;
            if(PlayerShipControl.UseInput) {
                PlayerShipControl = new PlayerShipControl();
                PlayerShipControl.Initialize(game);
            }
        }
        public void LoadContent(RoguelancerGame game) {
            model.LoadContent(game);
            if(PlayerShipControl.UseInput) {
                PlayerShipControl.LoadContent(game);
            }
        }
        public void Update(RoguelancerGame game) {
            if(game.GameState.CurrentGameState == GameStates.Playing) {
                if(PlayerShipControl.UseInput) {
                    PlayerShipControl.UpdateModel(model, game);
                    if(game.Input.InputItems.Toggles.ToggleCamera == false) {
                        model.Update(game);
                    }
                } else {
                    model.Update(game);
                }
            }
        }
        public void Draw(RoguelancerGame game) {
            model.Draw(game);
            if(PlayerShipControl.UseInput) {
                PlayerShipControl.Draw(game);
            }
        }
    }
}