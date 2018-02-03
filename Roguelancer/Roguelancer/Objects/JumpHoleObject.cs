using Roguelancer.Interfaces;
using Roguelancer.Models;
namespace Roguelancer.Objects {
    /// <summary>
    /// Jump Hole
    /// </summary>
    public class JumpHoleObject : IGame {
        #region "public properties"
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
            Model = new GameModel(game, null, null);
            JumpHoleModel = new JumpHoleModel(game);
        }
        /// <summary>
        /// Initialize
        /// </summary>
        /// <param name="game"></param>
        public void Initialize(RoguelancerGame game) {
            Model.Initialize(game);
        }
        /// <summary>
        /// Load Content
        /// </summary>
        /// <param name="game"></param>
        public void LoadContent(RoguelancerGame game) {
            Model.LoadContent(game);
        }
        /// <summary>
        /// Update
        /// </summary>
        /// <param name="game"></param>
        public void Update(RoguelancerGame game) {
            Model.Update(game);
        }
        /// <summary>
        /// Draw
        /// </summary>
        /// <param name="game"></param>
        public void Draw(RoguelancerGame game) {
            Model.Draw(game);
        }
        /// <summary>
        /// Dispose
        /// </summary>
        public void Dispose(RoguelancerGame game) {
            Model.Dispose(game);
        }
        /// <summary>
        /// Reset
        /// </summary>
        public void Reset(RoguelancerGame game) {
            Model = new GameModel(game, null, null);
        }
        #endregion
    }
}