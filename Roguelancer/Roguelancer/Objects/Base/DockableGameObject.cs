using Roguelancer.Enum;
using Roguelancer.Interfaces;
using Roguelancer.Models;
namespace Roguelancer.Objects.Base {
    /// <summary>
    /// Dockable Game Object
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public abstract class DockableGameObject<T> : DockableObject, IGame where T : IGame {
        /// <summary>
        /// Sensor Object
        /// </summary>
        private IDockableSensorObject _sensorObject;
        /// <summary>
        /// Game Model
        /// </summary>
        public virtual GameModel Model { get; set; }
        /// <summary>
        /// Model
        /// </summary>
        public StationModel StationModel { get; set; }
        /// <summary>
        /// Dockable Game Object
        /// </summary>
        /// <param name="sensorObject"></param>
        public void SetSensorObject(IDockableSensorObject sensorObject) {
            _sensorObject = sensorObject;
        }
        /// <summary>
        /// Initialize
        /// </summary>
        /// <param name="game"></param>
        public void Initialize(RoguelancerGame game) {
            Model.Initialize(game);
            Initialize(game, Model, StationModel.StationID); // Initialize Dockable Object
        }
        /// <summary>
        /// Load Content
        /// </summary>
        /// <param name="game"></param>
        public void LoadContent(RoguelancerGame game) {
            Model.LoadContent(game);
            LoadContent(game, Model, StationModel.StationID); // Dockable Object Load Content
        }
        /// <summary>
        /// Update
        /// </summary>
        /// <param name="game"></param>
        public void Update(RoguelancerGame game) {
            if (game.GameState.Model.CurrentGameState == GameStatesEnum.Playing) {
                Model.UpdatePosition(); // Update Position
                Model.Update(game); // Update
            }
            Update(game, Model, StationModel.StationID); // Update Dockable Object Station Stuff
        }
        /// <summary>
        /// Draw
        /// </summary>
        /// <param name="game"></param>
        public void Draw(RoguelancerGame game) {
            if (game.GameState.Model.CurrentGameState == GameStatesEnum.Playing) {
                Model.Draw(game);
            }
            Draw(game, Model, StationModel.StationID); // Draw Dockable Object Station Stuff
        }
        /// <summary>
        /// Reset
        /// </summary>
        /// <param name="game"></param>
        public void Reset(RoguelancerGame game) {
            Model = new GameModel(game, null, _sensorObject);
            StationModel = new StationModel();
        }
        /// <summary>
        /// Dispose
        /// </summary>
        /// <param name="game"></param>
        public void Dispose(RoguelancerGame game) {
            Model = null;
        }
    }
}