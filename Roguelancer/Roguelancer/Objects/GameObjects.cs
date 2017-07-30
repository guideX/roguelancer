using Roguelancer.Enum;
using Roguelancer.Interfaces;
using Roguelancer.Models;
using Roguelancer.Particle;
using Roguelancer.Settings;
namespace Roguelancer.Objects {
    /// <summary>
    /// Game Objects
    /// </summary>
    public class GameObjects : IGameObjects {
        #region "public properties"
        /// <summary>
        /// Game Objects
        /// </summary>
        public GameObjectsModel Model { get; set; }
        #endregion
        #region "public methods"
        /// <summary>
        /// Entry Point
        /// </summary>
        /// <param name="game"></param>
        public GameObjects(RoguelancerGame game) {
            Model = new GameObjectsModel();
            Model.Stations = new StationCollection(game);
            Model.Planets = new PlanetCollection(game);
            Model.Stars = new Starfields(game.Settings.Model.StarSystemSettings[game.CurrentStarSystemId].StarSettings);
            Model.TradeLanes = new TradeLaneCollection(game);
            Model.Ships = new ShipCollection(game);
            Model.Bullets = new Bullets(game);
            //Model.JumpHoles = new JumpHoleCollection();
        }
        /// <summary>
        /// Initialize
        /// </summary>
        /// <param name="game"></param>
        public void Initialize(RoguelancerGame game) {
            Model.Stations.Initialize(game);
            Model.Planets.Initialize(game);
            Model.Stars.Initialize(game);
            Model.TradeLanes.Initialize(game);
            Model.Ships.Initialize(game);
            Model.Bullets.Initialize(game);
            //Model.JumpHoles.Initialize(game);
        }
        /// <summary>
        /// Load Content
        /// </summary>
        /// <param name="game"></param>
        public void LoadContent(RoguelancerGame game) {
            Model.Stations.LoadContent(game);
            Model.Planets.LoadContent(game);
            Model.Stars.LoadContent(game);
            Model.TradeLanes.LoadContent(game);
            Model.Ships.LoadContent(game);
            Model.Bullets.LoadContent(game);
            //Model.JumpHoles.Initialize(game);
        }
        /// <summary>
        /// Update
        /// </summary>
        /// <param name="game"></param>
        public void Update(RoguelancerGame game) {
            if (game.GameState.Model.CurrentGameState == GameStates.Playing) {
                Model.Stations.Update(game);
                Model.Planets.Update(game);
                Model.Stars.Update(game);
                Model.TradeLanes.Update(game);
                Model.Ships.Update(game);
                Model.Bullets.Update(game);
                //Model.JumpHoles.Initialize(game);
            } else {
                Model.Stations.Update(game);
                Model.Planets.Update(game);
            }
        }
        /// <summary>
        /// Draw
        /// </summary>
        /// <param name="game"></param>
        public void Draw(RoguelancerGame game) {
            if (game.GameState.Model.CurrentGameState == GameStates.Playing) {
                Model.Stations.Draw(game);
                Model.Planets.Draw(game);
                Model.Stars.Draw(game);
                Model.TradeLanes.Draw(game);
                Model.Ships.Draw(game);
                Model.Bullets.Draw(game);
                //Model.JumpHoles.Draw(game);
            } else {
                Model.Stations.Draw(game);
                Model.Planets.Draw(game);
            }
        }
        /// <summary>
        /// Reset
        /// </summary>
        /// <param name="game"></param>
        public void Reset(RoguelancerGame game) {
            Model.TradeLanes.Reset(game);
            Model.Ships.Reset(game);
            Model.Stations = new StationCollection(game);
            Model.Planets = new PlanetCollection(game);
            Model.Stars = new Starfields(new StarSettings(false, 0, 0, 0, 0, 0, 0, 0));
            //Model.JumpHoles = new JumpHoleCollection();
        }
        /// <summary>
        /// Dispose
        /// </summary>
        public void Dispose(RoguelancerGame game) {
            Model.TradeLanes.Dispose(game);
            Model.Ships.Dispose(game);
            Model.Stations.Dispose(game);
            Model.Planets.Dispose(game);
            Model.Stars.Dispose(game);
        }
        #endregion
    }
}