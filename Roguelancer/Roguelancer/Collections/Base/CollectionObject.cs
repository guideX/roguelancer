using Roguelancer.Interfaces;
using System.Collections.Generic;
namespace Roguelancer.Collections.Base {
    /// <summary>
    /// Collection Object
    /// </summary>
    public abstract class CollectionObject<T> : IGame where T : IGame {
        /// <summary>
        /// Objects
        /// </summary>
        public List<T> Objects { get; set; } = new List<T>();
        /// <summary>
        /// Initialize
        /// </summary>
        /// <param name="game"></param>
        public virtual void Initialize(RoguelancerGame game) {
            Objects.ForEach(o => o.Initialize(game));
        }
        /// <summary>
        /// Load Content
        /// </summary>
        /// <param name="game"></param>
        public virtual void LoadContent(RoguelancerGame game) {
            Objects.ForEach(o => o.LoadContent(game));
        }
        /// <summary>
        /// Update
        /// </summary>
        /// <param name="game"></param>
        public virtual void Update(RoguelancerGame game) {
            Objects.ForEach(o => o.Update(game));
        }
        /// <summary>
        /// Draw
        /// </summary>
        /// <param name="game"></param>
        public virtual void Draw(RoguelancerGame game) {
            Objects.ForEach(o => o.Draw(game));
        }
        /// <summary>
        /// Dispose
        /// </summary>
        /// <param name="game"></param>
        public virtual void Dispose(RoguelancerGame game) {
            Objects.ForEach(o => o.Dispose(game));
        }
        /// <summary>
        /// Reset
        /// </summary>
        /// <param name="game"></param>
        public virtual void Reset(RoguelancerGame game) {
            Objects.ForEach(o => o.Reset(game));
        }
    }
}