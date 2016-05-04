// Roguelancer 0.1 Pre Alpha by Leon Aiossa
// http://www.team-nexgen.com
using Roguelancer.Models;
namespace Roguelancer.Interfaces {
    /// <summary>
    /// Game Camera
    /// </summary>
    public interface IGameCamera {
        /// <summary>
        /// Game Camera Model
        /// </summary>
        GameCameraModel Model { get; set; }
        /// <summary>
        /// Initialize
        /// </summary>
        /// <param name="game"></param>
        void Initialize(RoguelancerGame game);
        /// <summary>
        /// Update
        /// </summary>
        /// <param name="game"></param>
        void Update(RoguelancerGame game);
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