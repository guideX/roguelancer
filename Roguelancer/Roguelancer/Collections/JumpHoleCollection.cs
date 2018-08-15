using Roguelancer.Collections.Base;
using Roguelancer.Interfaces;
using Roguelancer.Models;
using Roguelancer.Models.Collection;
using Roguelancer.Objects;
namespace Roguelancer.Collections {
    /// <summary>
    /// Jump Hole Collection
    /// </summary>
    public class JumpHoleCollection : CollectionObject<JumpHoleObject>, IGame {
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
        public override void Initialize(RoguelancerGame game) {
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
        #endregion
    }
}