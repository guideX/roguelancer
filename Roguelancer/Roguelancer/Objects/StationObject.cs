using Roguelancer.Interfaces;
using Roguelancer.Objects.Base;
namespace Roguelancer.Objects {
    /// <summary>
    /// Station
    /// </summary>
    public class StationObject : DockableGameObject<StationObject>, IGame, IDockableSensorObject {
        #region "public properties"
        #endregion
        #region "public methods"
        /// <summary>
        /// Entry Point
        /// </summary>
        /// <param name="game"></param>
        public StationObject(RoguelancerGame game) {
            Reset(game);
            SetSensorObject(this);
        }
        #endregion
    }
}