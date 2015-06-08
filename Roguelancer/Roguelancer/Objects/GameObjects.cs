using Roguelancer.Particle;
using Roguelancer.Functionality;
using Roguelancer.Settings;
namespace Roguelancer.Objects {
    public class GameObjects {
        /// <summary>
        /// Ships
        /// </summary>
        public ShipCollection ships;
        /// <summary>
        /// Bullets
        /// </summary>
        public Bullets Bullets;
        /// <summary>
        /// Stations
        /// </summary>
        private StarSystem _starSystem { get; set; }
        /// <summary>
        /// Entry Point
        /// </summary>
        /// <param name="game"></param>
        public GameObjects(RoguelancerGame game) {
            _starSystem = new StarSystem(game);
            ships = new ShipCollection(game);
            Bullets = new Bullets(game);
        }
        /// <summary>
        /// Initialize
        /// </summary>
        /// <param name="game"></param>
        public void Initialize(RoguelancerGame game) {
            _starSystem.Initialize(game);
            ships.Initialize(game);
            Bullets.Initialize(game);
        }
        /// <summary>
        /// Load Content
        /// </summary>
        /// <param name="game"></param>
        public void LoadContent(RoguelancerGame game) {
            _starSystem.LoadContent(game);
            ships.LoadContent(game);
            Bullets.LoadContent(game);
        }
        /// <summary>
        /// Update
        /// </summary>
        /// <param name="game"></param>
        public void Update(RoguelancerGame game) {
            _starSystem.Update(game);
            ships.Update(game);
            Bullets.Update(game);
        }
        /// <summary>
        /// Draw
        /// </summary>
        /// <param name="game"></param>
        public void Draw(RoguelancerGame game) {
            _starSystem.Draw(game);
            ships.Draw(game);
            Bullets.Draw(game);
        }
        /// <summary>
        /// Reset
        /// </summary>
        /// <param name="game"></param>
        public void Reset(RoguelancerGame game) {
            _starSystem.Reset(game);
            ships.Reset(game);
        }
    }
}