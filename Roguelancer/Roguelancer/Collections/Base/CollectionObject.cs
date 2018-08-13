using Roguelancer.Interfaces;
using System.Collections.Generic;
namespace Roguelancer.Collections.Base {
    /// <summary>
    /// Collection Object
    /// </summary>
    public abstract class CollectionObject<T> : IGame {
        /// <summary>
        /// Objects
        /// </summary>
        public List<T> Objects { get; set; } = new List<T>();
        /// <summary>
        /// Initialize
        /// </summary>
        /// <param name="game"></param>
        public virtual void Initialize(RoguelancerGame game) {
            var objs = new object[1]; objs[0] = game;
            foreach (var o in Objects) o.GetType().GetMethod("Initialize").Invoke(o, objs);
        }
        /// <summary>
        /// Load Content
        /// </summary>
        /// <param name="game"></param>
        public virtual void LoadContent(RoguelancerGame game) {
            var objs = new object[1]; objs[0] = game;
            foreach (var o in Objects) o.GetType().GetMethod("LoadContent").Invoke(o, objs);
        }
        /// <summary>
        /// Update
        /// </summary>
        /// <param name="game"></param>
        public virtual void Update(RoguelancerGame game) {
            var objs = new object[1]; objs[0] = game;
            foreach (var o in Objects) o.GetType().GetMethod("Update").Invoke(o, objs);
        }
        /// <summary>
        /// Draw
        /// </summary>
        /// <param name="game"></param>
        public virtual void Draw(RoguelancerGame game) {
            var objs = new object[1]; objs[0] = game;
            foreach (var o in Objects) o.GetType().GetMethod("Draw").Invoke(o, objs);
        }
        /// <summary>
        /// Dispose
        /// </summary>
        /// <param name="game"></param>
        public virtual void Dispose(RoguelancerGame game) {
            var objs = new object[1]; objs[0] = game;
            foreach (var o in Objects) o.GetType().GetMethod("Dispose").Invoke(o, objs);
        }
        /// <summary>
        /// Reset
        /// </summary>
        /// <param name="game"></param>
        public virtual void Reset(RoguelancerGame game) {
            var objs = new object[1]; objs[0] = game;
            foreach (var o in Objects) o.GetType().GetMethod("Reset").Invoke(o, objs);
            Objects = new List<T>();
        }
    }
}