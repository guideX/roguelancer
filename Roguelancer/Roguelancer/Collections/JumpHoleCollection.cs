using Roguelancer.Collections.Base;
using Roguelancer.Interfaces;
using Roguelancer.Objects;
namespace Roguelancer.Collections {
    /// <summary>
    /// Jump Hole Collection
    /// </summary>
    public class JumpHoleCollection : CollectionObject<JumpHoleObject>, IGame {
        #region "public methods"
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
                Objects.Add(s);
            }
            Objects.ForEach(o => o.Initialize(game));
        }
        #endregion
    }
}