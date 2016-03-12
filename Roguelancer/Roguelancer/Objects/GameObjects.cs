// Roguelancer 0.1 Pre Alpha by Leon Aiossa
// http://www.team-nexgen.com
using Roguelancer.Interfaces;
using Roguelancer.Particle;
using Roguelancer.Settings;
namespace Roguelancer.Objects {
    /// <summary>
    /// Game Objects
    /// </summary>
    public class GameObjects : IGameObjects {
        #region "public variables"
        /// <summary>
        /// Ships
        /// </summary>
        public IShipCollection Ships { get; set; }
        /// <summary>
        /// Bullets
        /// </summary>
        public Bullets Bullets { get; set; }
        #endregion
        #region "private variables"
        /// <summary>
        /// Trade Lanes
        /// </summary>
        public TradeLaneCollection TradeLanes { get; set; }
        /// <summary>
        /// Stations
        /// </summary>
        public StationCollection Stations;
        /// <summary>
        /// Planets
        /// </summary>
        public PlanetCollection Planets;
        /// <summary>
        /// Stars
        /// </summary>
        public Starfields Stars;
        /// <summary>
        /// Stations
        /// </summary>
        #endregion
        #region "public functions"
        /// <summary>
        /// Entry Point
        /// </summary>
        /// <param name="game"></param>
        public GameObjects(RoguelancerGame game) {
            Stations = new StationCollection();
            Planets = new PlanetCollection();
            Stars = new Starfields(game.Settings.StarSystemSettings[game.StarSystemId].StarSettings);
            TradeLanes = new TradeLaneCollection();
            Ships = new ShipCollection(game);
            Bullets = new Bullets(game);
        }
        /// <summary>
        /// Initialize
        /// </summary>
        /// <param name="game"></param>
        public void Initialize(RoguelancerGame game) {
            Stations.Initialize(game);
            Planets.Initialize(game);
            Stars.Initialize(game);
            TradeLanes.Initialize(game);
            Ships.Initialize(game);
            Bullets.Initialize(game);
        }
        /// <summary>
        /// Load Content
        /// </summary>
        /// <param name="game"></param>
        public void LoadContent(RoguelancerGame game) {
            Stations.LoadContent(game);
            Planets.LoadContent(game);
            Stars.LoadContent(game);
            TradeLanes.LoadContent(game);
            Ships.LoadContent(game);
            Bullets.LoadContent(game);
        }
        /// <summary>
        /// Update
        /// </summary>
        /// <param name="game"></param>
        public void Update(RoguelancerGame game) {
            Stations.Update(game);
            Planets.Update(game);
            Stars.Update(game);
            TradeLanes.Update(game);
            Ships.Update(game);
            Bullets.Update(game);
        }
        /// <summary>
        /// Draw
        /// </summary>
        /// <param name="game"></param>
        public void Draw(RoguelancerGame game) {
            Stations.Draw(game);
            Planets.Draw(game);
            Stars.Draw(game);
            TradeLanes.Draw(game);
            Ships.Draw(game);
            Bullets.Draw(game);
        }
        /// <summary>
        /// Reset
        /// </summary>
        /// <param name="game"></param>
        public void Reset(RoguelancerGame game) {
            TradeLanes.Reset(game);
            Ships.Reset(game);
            Stations = new StationCollection();
            Planets = new PlanetCollection();
            Stars = new Starfields(new StarSettings(false, 0, 0, 0, 0, 0, 0, 0));
        }
        #endregion
    }
}