using Roguelancer.Models;
using Roguelancer.Objects;
namespace Roguelancer.Interfaces {
    /// <summary>
    /// Bullet
    /// </summary>
    public interface IBullet : IGame {
        /// <summary>
        /// Model
        /// </summary>
        GameModel Model { get; set; }
        /// <summary>
        /// Bullet Model
        /// </summary>
        BulletModel BulletModel { get; set; }
    }
}