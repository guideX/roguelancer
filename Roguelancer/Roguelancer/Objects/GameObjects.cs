using Roguelancer.Collections;
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
            Model = new GameObjectsModel() {
                Stations = new StationCollection(game),
                Planets = new PlanetCollection(game),
                Stars = new Starfields(game.Settings.Model.StarSystemSettings[game.CurrentStarSystemId].Model.StarSettings),
                Ships = new ShipCollection(game),
                Bullets = new BulletCollection(game),
                JumpHoles = new JumpHoleCollection(game),
                DockingRings = new DockingRingCollection(game)
            };
        }
        /// <summary>
        /// Initialize
        /// </summary>
        /// <param name="game"></param>
        public void Initialize(RoguelancerGame game) {
            if (Model.Stations != null) Model.Stations.Initialize(game);
            if (Model.Planets != null) Model.Planets.Initialize(game);
            if (Model.Stars != null) Model.Stars.Initialize(game);
            if (Model.Ships != null) Model.Ships.Initialize(game);
            if (Model.Bullets != null) Model.Bullets.Initialize(game);
            if (Model.DockingRings != null) Model.DockingRings.Initialize(game);
            if (Model.JumpHoles != null) Model.JumpHoles.Initialize(game);
        }
        /// <summary>
        /// Load Content
        /// </summary>
        /// <param name="game"></param>
        public void LoadContent(RoguelancerGame game) {
            if (Model.Stations != null) Model.Stations.LoadContent(game);
            if (Model.Planets != null) Model.Planets.LoadContent(game);
            if (Model.Stars != null) Model.Stars.LoadContent(game);
            if (Model.Ships != null) Model.Ships.LoadContent(game);
            if (Model.Bullets != null) Model.Bullets.LoadContent(game);
            if (Model.DockingRings != null) Model.DockingRings.LoadContent(game);
            if (Model.JumpHoles != null) Model.JumpHoles.LoadContent(game);
        }
        /// <summary>
        /// Update
        /// </summary>
        /// <param name="game"></param>
        public void Update(RoguelancerGame game) {
            if (game.GameState.Model.CurrentGameState == GameStatesEnum.Playing) {
                if (Model.Stations != null) Model.Stations.Update(game);
                if (Model.Planets != null) Model.Planets.Update(game);
                if (Model.Stars != null) Model.Stars.Update(game);
                if (Model.Ships != null) Model.Ships.Update(game);
                if (Model.Bullets != null) Model.Bullets.Update(game);
                if (Model.DockingRings != null) Model.DockingRings.Update(game);
                if (Model.JumpHoles != null) Model.JumpHoles.Update(game);
            } else {
                if (Model.Stations != null) Model.Stations.Update(game);
                if (Model.Planets != null) Model.Planets.Update(game);
            }
        }
        /// <summary>
        /// Draw
        /// </summary>
        /// <param name="game"></param>
        public void Draw(RoguelancerGame game) {
            if (game.GameState.Model.CurrentGameState == GameStatesEnum.Playing) {
                if (Model.Stations != null) Model.Stations.Draw(game);
                if (Model.Planets != null) Model.Planets.Draw(game);
                if (Model.Stars != null) Model.Stars.Draw(game);
                if (Model.Ships != null) Model.Ships.Draw(game);
                if (Model.Bullets != null) Model.Bullets.Draw(game);
                if (Model.DockingRings != null) Model.DockingRings.Draw(game);
                if (Model.JumpHoles != null) Model.JumpHoles.Draw(game);
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
            if (Model.Ships != null) Model.Ships.Reset(game);
            Model.Stations = new StationCollection(game);
            Model.Planets = new PlanetCollection(game);
            Model.Stars = new Starfields(new StarSettingsModel(false, 0, 0, 0, 0, 0, 0, 0));
            Model.DockingRings = new DockingRingCollection(game);
            Model.JumpHoles = new JumpHoleCollection(game);
        }
        /// <summary>
        /// Dispose
        /// </summary>
        public void Dispose(RoguelancerGame game) {
            if (Model.Ships != null) Model.Ships.Dispose(game);
            if (Model.Stations != null) Model.Stations.Dispose(game);
            if (Model.Planets != null) Model.Planets.Dispose(game);
            if (Model.Stars != null) Model.Stars.Dispose(game);
            if (Model.DockingRings != null) Model.DockingRings.Dispose(game);
        }
        #endregion
    }
}