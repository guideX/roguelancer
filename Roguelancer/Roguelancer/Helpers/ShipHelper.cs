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
        public static Ship GetPlayerShip(RoguelancerGame game) {
            return game.Objects.Model.Ships.Model.Ships.Where(s => s.ShipModel.PlayerShipControl.Model.UseInput).LastOrDefault();
        }
        /// <summary>
        /// Clone
        /// </summary>
        /// <param name="oldShip"></param>
        /// <param name="game"></param>
        /// <returns></returns>
        public static Ship Clone(Ship oldShip, RoguelancerGame game) {
            Ship ship;
            ship = new Ship(game);
            ship.ShipModel.PlayerShipControl = oldShip.ShipModel.PlayerShipControl;
            ship.Model = oldShip.Model;
            ship.ShipModel.CargoHold = oldShip.ShipModel.CargoHold;
            ship.Docked = oldShip.Docked;
            return ship;
        }
    }
}