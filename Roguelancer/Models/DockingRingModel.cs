using Roguelancer.Interfaces;
using Roguelancer.Models;
using Roguelancer.Objects.Base;
using Roguelancer.Enum;
namespace Roguelancer.Objects {
    /// <summary>
    /// Planet
    /// </summary>
    public class DockingRingModel : DockableObject, IGame, IDockableSensorObject {
        #region "public properties"
        /// <summary>
        /// Model
        /// </summary>
        public StationModel StationModel { get; set; }
        /// <summary>
        /// Game Model
        /// </summary>
        public GameModel Model { get; set; }
        #endregion
        #region "public methods"
        /// <summary>
        /// Entry Point
        /// </summary>
        /// <param name="game"></param>
        public DockingRingModel(RoguelancerGame game) {
            Reset(game);
        }
        /// <summary>
        /// Initialize
        /// </summary>
        /// <param name="game"></param>
        public void Initialize(RoguelancerGame game) {
            //Model.Initialize(game);
            //Initialize(game, Model, StationModel.StationID); // Initialize Dockable Object
            Initialize(game, Model, StationModel.StationGuid); // Initialize Dockable Object
        }
        /// <summary>
        /// Load Content
        /// </summary>
        /// <param name="game"></param>
        public void LoadContent(RoguelancerGame game) {
            Model.LoadContent(game);
            //LoadContent(game, Model, StationModel.StationID); // Dockable Object Load Content
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
            //Update(game, Model, StationModel.StationID); // Update Dockable Object Station Stuff
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
            //Draw(game, Model, StationModel.StationID); // Draw Dockable Object Station Stuff
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
        /// <param name="game"></param>
        public void Reset(RoguelancerGame game) {
            Model = new GameModel(game, null, this, ModelTypeEnum.DockingRing, Model.Description);
            StationModel = new StationModel();
        }
        #endregion
    }
}