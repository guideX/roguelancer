using Roguelancer.Models;
namespace Roguelancer.Objects.Base {
    /// <summary>
    /// Game Object
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public abstract class GameObject<T> {
        /// <summary>
        /// Game Model
        /// </summary>
        public GameModel Model { get; set; }
    }
}