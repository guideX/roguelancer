using System.Linq;
using System.Collections.Generic;
using Roguelancer.Interfaces;
using Roguelancer.Models;
using Roguelancer.Enum;
using Roguelancer.Helpers;
using Roguelancer.Objects;
using Roguelancer.Collections.Base;
namespace Roguelancer.Collections {
    /// <summary>
    /// Ship Collection
    /// </summary>
    public class ShipCollection : CollectionObject<ShipObject>, IGame {
        #region "public methods"
        /// <summary>
        /// Ship Collection
        /// </summary>
        public ShipCollection(RoguelancerGame game) {
            Reset(game);
        }
        /// <summary>
        /// Reset
        /// </summary>
        /// <param name="game"></param>
        public override void Reset(RoguelancerGame game) {
            Objects = new List<ShipObject>();
            var playerShip = new ShipObject(game);
            ShipObject tempShip;
            playerShip.Model.WorldObject = game.Settings.Model.StarSystemSettings[game.CurrentStarSystemId].Model.Ships.Where(s => s.Model.SettingsModelObject.ModelType == ModelTypeEnum.Ship && s.Model.SettingsModelObject.IsPlayer).FirstOrDefault();
            playerShip.ShipModel.PlayerShipControl.Model.UseInput = true;
            playerShip.Model.WorldObject.Model.StarSystemId = game.CurrentStarSystemId;
            Objects.Add(playerShip);
            foreach (var modelWorldObject in game.Settings.Model.StarSystemSettings[game.CurrentStarSystemId].Model.Ships.Where(s => !s.Model.SettingsModelObject.IsPlayer).ToList()) {
                tempShip = new ShipObject(game) {
                    Model = new GameModel(game, null, null)
                };
                if (modelWorldObject != null) {
                    tempShip.Model.WorldObject = modelWorldObject.Clone();
                    tempShip.ShipModel.PlayerShipControl.Model.UseInput = false;
                    Objects.Add(ShipHelper.Clone(tempShip, game));
                }
            }
        }
        #endregion
    }
}