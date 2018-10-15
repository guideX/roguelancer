using Roguelancer.Models;
namespace Roguelancer.Interfaces {
    /// <summary>
    /// Game Graphics
    /// </summary>
    public interface IGameGraphics {
        /// <summary>
        /// Load Content
        /// </summary>
        /// <param name="game"></param>
        void LoadContent(RoguelancerGame game);
        /// <summary>
        /// Draw
        /// </summary>
        /// <param name="game"></param>
        void Draw(RoguelancerGame game);
        /// <summary>
        /// Dispose
        /// </summary>
        /// <param name="game"></param>
        void Dispose(RoguelancerGame game);
        /// <summary>
        /// Model
        /// </summary>
        //GameGraphicsModel Model { get; set; }
    }
}