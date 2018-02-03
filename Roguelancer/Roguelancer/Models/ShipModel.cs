using Roguelancer.Functionality;
using Roguelancer.Interfaces;
using Roguelancer.Objects;
namespace Roguelancer.Models {
    /// <summary>
    /// Ship Model
    /// </summary>
    public class ShipModel {
        /// <summary>
        /// Docked
        /// </summary>
        public bool Docked { get; set; }
        /// <summary>
        /// Docked To
        /// </summary>
        public GameModel DockedTo { get; set; }
        /// <summary>
        /// Going To Object
        /// </summary>
        public StationObject GoingToObject { get; set; }
        /// <summary>
        /// Going To
        /// </summary>
        public bool GoingTo { get; set; }
        /// <summary>
        /// Money
        /// </summary>
        public decimal Money { get; set; }
        /// <summary>
        /// Cargo Hold
        /// </summary>
        public CargoHoldModel CargoHold { get; set; }
        /// <summary>
        /// Hard Points
        /// </summary>
        //public List<HardPoint> HardPoints { get; set; }
        /// <summary>
        /// Player Ship Control
        /// </summary>
        public IPlayerShipControl PlayerShipControl;
        /// <summary>
        /// Ship Model
        /// </summary>
        public ShipModel(RoguelancerGame game) {
            Money = 2000.00m;
            CargoHold = new CargoHoldModel();
            PlayerShipControl = new PlayerShipControl(game);
            //HardPoints = new List<HardPoint>();
        }
    }
}