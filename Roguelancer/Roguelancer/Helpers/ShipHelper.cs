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
    }
}