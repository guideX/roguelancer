// Roguelancer 0.1 Pre Alpha by Leon Aiossa
// http://www.team-nexgen.com
using Microsoft.Xna.Framework;
using Roguelancer.Models;
namespace Roguelancer.Interfaces {
    /// <summary>
    /// Game Camera
    /// </summary>
    public interface IGameCamera : IGame {
        /// <summary>
        /// Projection
        /// </summary>
        //Matrix Projection { get; set; }
        /// <summary>
        /// View
        /// </summary>
        //Matrix View { get; set; }
        /// <summary>
        /// Game Camera Model
        /// </summary>
        GameCameraModel Model { get; set; }
        /// <summary>
        /// Shake
        /// </summary>
        /// <param name="magnitude"></param>
        /// <param name="duration"></param>
        /// <param name="shakeUseDuration"></param>
        void Shake(float magnitude, float duration, bool shakeUseDuration);
        /// <summary>
        /// Stop Shaking
        /// </summary>
        void StopShaking();
    }
}