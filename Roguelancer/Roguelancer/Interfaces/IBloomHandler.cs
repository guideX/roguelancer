// Roguelancer 0.1 Pre Alpha by Leon Aiossa
// http://team-nexgen.com
using Roguelancer.Models.Bloom;
namespace Roguelancer.Interfaces {
    /// <summary>
    /// Bloom Component
    /// </summary>
    public interface IBloomHandler {
        /// <summary>
        /// Model
        /// </summary>
        BloomHandlerModel Model { get; set; }
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
        /// Draw
        /// </summary>
        /// <param name="game"></param>
        void Draw(RoguelancerGame game);
    }
}