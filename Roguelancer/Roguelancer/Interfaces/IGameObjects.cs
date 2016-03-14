// Roguelancer 0.1 Pre Alpha by Leon Aiossa
// http://www.team-nexgen.com
using Roguelancer.Objects;
using Roguelancer.Particle;
namespace Roguelancer.Interfaces {
    /// <summary>
    /// Game Objects
    /// </summary>
    public interface IGameObjects : IGame {
        /// <summary>
        /// Ships
        /// </summary>
        IShipCollection Ships { get; set; }
        /// <summary>
        /// Reset
        /// </summary>
        /// <param name="game"></param>
        void Reset(RoguelancerGame game);
        /// <summary>
        /// Bullets
        /// </summary>
        Bullets Bullets { get; set; }
        /// <summary>
        /// Trade Lanes
        /// </summary>
        TradeLaneCollection TradeLanes { get; set; }
        /// <summary>
        /// Stations
        /// </summary>
        StationCollection Stations { get; set; }
        /// <summary>
        /// Planets
        /// </summary>
        PlanetCollection Planets { get; set; }
        /// <summary>
        /// Stars
        /// </summary>
        Starfields Stars { get; set; }
    }
}