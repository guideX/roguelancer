// Roguelancer 0.1 Pre Alpha by Leon Aiossa
// http://www.team-nexgen.org
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
                Stations = new StationCollection();
                Planets = new PlanetCollection();
                _stars = new Starfields(game.Settings.StarSystemSettings[0].starSettings);
                TradeLanes = new TradeLaneCollection();
                Ships = new ShipCollection(game);
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
                Stations.Initialize(game);
                Planets.Initialize(game);
                _stars.Initialize(game);
                TradeLanes.Initialize(game);
                Ships.Initialize(game);
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
                Stations.LoadContent(game);
                Planets.LoadContent(game);
                _stars.LoadContent(game);
                TradeLanes.LoadContent(game);
                Ships.LoadContent(game);
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
                Stations.Update(game);
                Planets.Update(game);
                _stars.Update(game);
                TradeLanes.Update(game);
                Ships.Update(game);
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
                Stations.Draw(game);
                Planets.Draw(game);
                _stars.Draw(game);
                TradeLanes.Draw(game);
                Ships.Draw(game);
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
                TradeLanes.Reset(game);
                Ships.Reset(game);
                Stations = new StationCollection();
                Planets = new PlanetCollection();
                _stars = new Starfields(new StarSettings(false, 0, 0, 0, 0, 0, 0, 0));
            } catch {
                throw;
            }
        }
        #endregion
    }
}