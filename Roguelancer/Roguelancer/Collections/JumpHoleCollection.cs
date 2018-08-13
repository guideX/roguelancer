using Roguelancer.Interfaces;
using Roguelancer.Models;
using Roguelancer.Objects;
namespace Roguelancer.Collections {
    /// <summary>
    /// Jump Hole Collection
    /// </summary>
    public class JumpHoleCollection : IGame {
        #region "public properties"
        /// <summary>
        /// Model
        /// </summary>
        public JumpHoleCollectionModel Model { get; set; }
        #endregion
        #region "public methods"
        /// <summary>
        /// Jump Hole Collection
        /// </summary>
        public JumpHoleCollection() {
            Model = new JumpHoleCollectionModel();
        }
        /// <summary>
        /// Initialize
        /// </summary>
        /// <param name="game"></param>
        public void Initialize(RoguelancerGame game) {
            var n = 0;
            foreach (var obj in game.Settings.Model.StarSystemSettings[game.CurrentStarSystemId].Model.JumpHoles) {
                n++;
                var s = new JumpHoleObject(game);
                s.Model.WorldObject = obj;
                Model.JumpHoles.Add(s);
            }
            foreach (var jumphole in Model.JumpHoles) {
                jumphole.Initialize(game);
            }
        }
        /// <summary>
        /// Load Content
        /// </summary>
        /// <param name="game"></param>
        public void LoadContent(RoguelancerGame game) {
            foreach (var hole in Model.JumpHoles) {
                hole.LoadContent(game);
            }
        }
        /// <summary>
        /// Update
        /// </summary>
        /// <param name="game"></param>
        public void Update(RoguelancerGame game) {
            foreach (var hole in Model.JumpHoles) {
                hole.Update(game);
            }
        }
        /// <summary>
        /// Draw
        /// </summary>
        /// <param name="game"></param>
        public void Draw(RoguelancerGame game) {
            foreach (var hole in Model.JumpHoles) {
                hole.Draw(game);
            }
        }
        /// <summary>
        /// Dispose
        /// </summary>
        public void Dispose(RoguelancerGame game) {
            foreach (var hole in Model.JumpHoles) {
                hole.Dispose(game);
            }
        }
        /// <summary>
        /// Reset
        /// </summary>
        /// <param name="game"></param>
        public void Reset(RoguelancerGame game) {
        }
        #endregion
    }
}