// Roguelancer 0.1 Pre Alpha by Leon Aiossa
// http://www.team-nexgen.org
using Roguelancer.Models;
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
        /// <summary>
        /// Dispose
        /// </summary>
        /// <param name="game"></param>
        void Dispose(RoguelancerGame game);
    }
}