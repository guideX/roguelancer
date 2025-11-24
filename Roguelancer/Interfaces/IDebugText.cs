using Roguelancer.Models;
namespace Roguelancer.Interfaces {
    /// <summary>
    /// Debug Text
    /// </summary>
    public interface IDebugText {
        /// <summary>
        /// Load Content
        /// </summary>
        /// <param name="game"></param>
        void LoadContent(RoguelancerGame game);
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
        /// <summary>
        /// Model
        /// </summary>
        DebugTextModel Model { get; set; }
    }
}