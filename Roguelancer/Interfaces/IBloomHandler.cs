using Roguelancer.Models;
namespace Roguelancer.Interfaces {
    /// <summary>
    /// Bloom Component
    /// </summary>
    public interface IBloomHandler : IGame {
        /// <summary>
        /// Model
        /// </summary>
        BloomHandlerModel Model { get; set; }
        /// <summary>
        /// Initialize
        /// </summary>
        /// <param name="game"></param>
        //void Initialize(RoguelancerGame game);
        /// <summary>
        /// Update
        /// </summary>
        /// <param name="game"></param>
        //void Update(RoguelancerGame game);
        /// <summary>
        /// Draw
        /// </summary>
        /// <param name="game"></param>
        //void Draw(RoguelancerGame game);
    }
}