using System.Linq;
using Roguelancer.Collections.Base;
using Roguelancer.Enum;
using Roguelancer.Interfaces;
using Roguelancer.Objects;
//using Roguelancer.Collections.Base;
//using Roguelancer.Interfaces;
//using Roguelancer.Objects;
namespace Roguelancer.Collections {
    /// <summary>
    /// Station Collection
    /// </summary>
    public class JumpHoleCollection : CollectionObject<JumpHoleObject>, IGame {
        #region "public methods"
        /// <summary>
        /// Jump Hole Collection
        /// </summary>
        public JumpHoleCollection(RoguelancerGame game) {
            Reset(game);
        }
        /// <summary>
        /// Initialize
        /// </summary>
        /// <param name="game"></param>
        public override void Initialize(RoguelancerGame game) {
            var n = 0;
            var existingGuids = Objects.Select(o => o.Model.WorldObject.Model.SystemGuid).ToList();
            foreach (var obj in game.Settings.Model.StarSystemSettings[game.CurrentStarSystemId].Model.JumpHoles) {
                if (!existingGuids.Contains(obj.Model.SystemGuid)) {
                    n++;
                    var j = new JumpHoleObject(game);
                    j.DockableObjectType.ReferenceID = n;
                    j.DockableObjectType.ObjectType = ModelTypeEnum.JumpHole;
                    j.Model.WorldObject = obj;
                    Objects.Add(j);
                    j.Initialize(game);
                }
            }
        }
        #endregion
    }
    /*
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
    */
}