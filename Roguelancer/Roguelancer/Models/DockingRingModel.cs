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
        /// Dockable Object Type
        /// </summary>
        public DockableObjectTypeModel DockableObjectType { get; set; }
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
            Model.Initialize(game);
            Initialize(game, Model, DockableObjectType); // Initialize Dockable Object
        }
        /// <summary>
        /// Load Content
        /// </summary>
        /// <param name="game"></param>
        public void LoadContent(RoguelancerGame game) {
            Model.LoadContent(game);
            LoadContent(game, Model, DockableObjectType); // Dockable Object Load Content
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
            Update(game, Model, DockableObjectType); // Update Dockable Object Station Stuff
        }
        /// <summary>
        /// Draw
        /// </summary>
        /// <param name="game"></param>
        public void Draw(RoguelancerGame game) {
            if (game.GameState.Model.CurrentGameState == GameStatesEnum.Playing) {
                Model.Draw(game);
            }
            Draw(game, Model, DockableObjectType); // Draw Dockable Object Station Stuff
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
            Model = new GameModel(game, null, this);
            DockableObjectType = new DockableObjectTypeModel();
        }
        #endregion
    }
}