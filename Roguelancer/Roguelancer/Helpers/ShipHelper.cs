using Roguelancer.Models;
using Roguelancer.Objects;
using System.Linq;
namespace Roguelancer.Helpers {
    /// <summary>
    /// Ship Helper
    /// </summary>
    public static class ShipHelper {
        /// <summary>
        /// Get Player Ship
        /// </summary>
        /// <returns></returns>
        public static ShipObject GetPlayerShip(GameObjectsModel gameObjects) {
            return gameObjects.Ships.Objects.Where(s => s.ShipModel.PlayerShipControl.Model.UseInput).LastOrDefault();
        }
        /// <summary>
        /// Clone
        /// </summary>
        /// <param name="oldShip"></param>
        /// <param name="game"></param>
        /// <returns></returns>
        public static ShipObject Clone(ShipObject oldShip, RoguelancerGame game) {
            ShipObject ship;
            ship = new ShipObject(game);
            ship.ShipModel.PlayerShipControl = oldShip.ShipModel.PlayerShipControl;
            ship.Model = oldShip.Model;
            ship.ShipModel.CargoHold = oldShip.ShipModel.CargoHold;
            ship.ShipModel.Docked = oldShip.ShipModel.Docked;
            return ship;
        }
    }
}