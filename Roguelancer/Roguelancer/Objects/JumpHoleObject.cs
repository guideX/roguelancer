using Roguelancer.Enum;
using Roguelancer.Interfaces;
using Roguelancer.Models;
using Roguelancer.Objects.Base;
namespace Roguelancer.Objects {
    /// <summary>
    /// Jump Hole
    /// </summary>
    public class JumpHoleObject : DockableObject, IDockableSensorObject {
        #region "public properties"
        /// <summary>
        /// Model
        /// </summary>
        public StationModel StationModel { get; set; }
        /// <summary>
        /// Model
        /// </summary>
        public GameModel Model { get; set; }
        /// <summary>
        /// Jump Hole Model
        /// </summary>
        public JumpHoleModel JumpHoleModel { get; set; }
        /// <summary>
        /// Entry Point
        /// </summary>
        /// <param name="game"></param>
        #endregion
        #region "public methods"
        public JumpHoleObject(RoguelancerGame game) {
            Reset(game);
        }
        /// <summary>
        /// Initialize
        /// </summary>
        /// <param name="game"></param>
        public void Initialize(RoguelancerGame game) {
            Initialize(game, Model, StationModel.StationGuid); // Initialize Dockable Object
        }
        /// <summary>
        /// Load Content
        /// </summary>
        /// <param name="game"></param>
        public void LoadContent(RoguelancerGame game) {
            Model.LoadContent(game);
            LoadContent(game, Model, StationModel.StationGuid); // Dockable Object Load Content
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
            Update(game, Model, StationModel.StationGuid); // Update Dockable Object Station Stuff
        }
        /// <summary>
        /// Draw
        /// </summary>
        /// <param name="game"></param>
        public void Draw(RoguelancerGame game) {
            if (game.GameState.Model.CurrentGameState == GameStatesEnum.Playing) {
                Model.Draw(game);
            }
            Draw(game, Model, StationModel.StationGuid); // Draw Dockable Object Station Stuff
        }
        /// <summary>
        /// Dispose
        /// </summary>
        public void Dispose(RoguelancerGame game) {
            Model = null;
        }
        /// <summary>
        /// Reset
        /// </summary>
        public void Reset(RoguelancerGame game) {
            Model = new GameModel(game, null, this, ModelTypeEnum.JumpHole, Model?.Description);
            StationModel = new StationModel();
        }
        #endregion
    }
}