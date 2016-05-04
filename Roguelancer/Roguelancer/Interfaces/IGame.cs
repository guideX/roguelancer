// Roguelancer 0.1 Pre Alpha by Leon Aiossa
// http://www.team-nexgen.com
namespace Roguelancer.Interfaces {
    /// <summary>
    /// IGame
    /// </summary>
    public interface IGame {
        /// <summary>
        /// Initialize
        /// </summary>
        /// <param name="game"></param>
        void Initialize(RoguelancerGame game);
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
        /// Dispose
        /// </summary>
        void Dispose();
        /// <summary>
        /// Reset
        /// </summary>
        /// <param name="game"></param>
        //void Reset(RoguelancerGame game);
    }
}