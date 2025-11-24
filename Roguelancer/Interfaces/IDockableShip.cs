using Roguelancer.Models;
namespace Roguelancer.Interfaces {
    /// <summary>
    /// Dockable Ship
    /// </summary>
    public interface IDockableShip {
        /// <summary>
        /// Ship Model
        /// </summary>
        ShipModel ShipModel { get; set; }
    }
}