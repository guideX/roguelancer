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
        void Dispose(RoguelancerGame game);
    }
}