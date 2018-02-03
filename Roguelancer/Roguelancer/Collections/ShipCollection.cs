using System.Linq;
using Roguelancer.Interfaces;
using Roguelancer.Models;
using Roguelancer.Enum;
using Roguelancer.Helpers;
using Roguelancer.Objects;

namespace Roguelancer.Collections {
    /// <summary>
    /// Ship Collection
    /// </summary>
    public class ShipCollection : IShipCollection {
        #region "public properties"
        /// <summary>
        /// Model
        /// </summary>
        public ShipCollectionModel Model { get; set; }
        #endregion
        #region "public methods"
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
            var playerShip = new ShipObject(game);
            ShipObject tempShip;
            playerShip.Model.WorldObject = game.Settings.Model.StarSystemSettings[game.CurrentStarSystemId].Model.Ships.Where(s => s.Model.SettingsModelObject.ModelType == ModelType.Ship && s.Model.SettingsModelObject.IsPlayer).FirstOrDefault();
            playerShip.ShipModel.PlayerShipControl.Model.UseInput = true;
            Model.Ships.Add(playerShip);
            foreach (var modelWorldObject in game.Settings.Model.StarSystemSettings[game.CurrentStarSystemId].Model.Ships.Where(s => !s.Model.SettingsModelObject.IsPlayer).ToList()) {
                tempShip = new ShipObject(game);
                tempShip.Model = new GameModel(game, null, null);
                tempShip.Model.WorldObject = modelWorldObject.Clone();
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
}