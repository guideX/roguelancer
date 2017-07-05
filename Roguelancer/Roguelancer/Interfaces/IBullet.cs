using Roguelancer.Models;
namespace Roguelancer.Interfaces {
    /// <summary>
    /// Bullet
    /// </summary>
    public interface IBullet : IGame {
        /// <summary>
        /// Bullet Model
        /// </summary>
        BulletModel BulletModel { get; set; }
        /// <summary>
        /// Dispose
        /// </summary>
        /// <param name="game"></param>
        //void Dispose(RoguelancerGame game);
    }
}