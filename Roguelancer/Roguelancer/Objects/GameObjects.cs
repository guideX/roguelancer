using Roguelancer.Functionality;
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
        public IShipCollection ships { get; set; }
        /// <summary>
        /// Bullets
        /// </summary>
        public Bullets Bullets { get; set; }
        #endregion
        #region "private variables"
        /// <summary>
        /// Trade Lanes
        /// </summary>
        private TradeLaneCollection _tradeLanes { get; set; }
        /// <summary>
        /// Stations
        /// </summary>
        private StationCollection _stations;
        /// <summary>
        /// Planets
        /// </summary>
        private PlanetCollection _planets;
        /// <summary>
        /// Stars
        /// </summary>
        private Starfields _stars;
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
            try {
                _stations = new StationCollection();
                _planets = new PlanetCollection();
                _stars = new Starfields(game.Settings.StarSystemSettings[0].starSettings);
                _tradeLanes = new TradeLaneCollection();
                ships = new ShipCollection(game);
                Bullets = new Bullets(game);
            } catch {
                throw;
            }
        }
        /// <summary>
        /// Initialize
        /// </summary>
        /// <param name="game"></param>
        public void Initialize(RoguelancerGame game) {
            try {
                _stations.Initialize(game);
                _planets.Initialize(game);
                _stars.Initialize(game);
                _tradeLanes.Initialize(game);
                ships.Initialize(game);
                Bullets.Initialize(game);
            } catch {
                throw;
            }
        }
        /// <summary>
        /// Load Content
        /// </summary>
        /// <param name="game"></param>
        public void LoadContent(RoguelancerGame game) {
            try {
                _stations.LoadContent(game);
                _planets.LoadContent(game);
                _stars.LoadContent(game);
                _tradeLanes.LoadContent(game);
                ships.LoadContent(game);
                Bullets.LoadContent(game);
            } catch {
                throw;
            }
        }
        /// <summary>
        /// Update
        /// </summary>
        /// <param name="game"></param>
        public void Update(RoguelancerGame game) {
            try {
                _stations.Update(game);
                _planets.Update(game);
                _stars.Update(game);
                _tradeLanes.Update(game);
                ships.Update(game);
                Bullets.Update(game);
            } catch {
                throw;
            }
        }
        /// <summary>
        /// Draw
        /// </summary>
        /// <param name="game"></param>
        public void Draw(RoguelancerGame game) {
            try {
                _stations.Draw(game);
                _planets.Draw(game);
                _stars.Draw(game);
                _tradeLanes.Draw(game);
                ships.Draw(game);
                Bullets.Draw(game);
            } catch {
                throw;
            }
        }
        /// <summary>
        /// Reset
        /// </summary>
        /// <param name="game"></param>
        public void Reset(RoguelancerGame game) {
            try {
                _tradeLanes.Reset(game);
                ships.Reset(game);
                _stations = new StationCollection();
                _planets = new PlanetCollection();
                _stars = new Starfields(new StarSettings(false, 0, 0, 0, 0, 0, 0, 0));
            } catch {
                throw;
            }
        }
        #endregion
    }
}