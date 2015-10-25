// Roguelancer 0.1 Pre Alpha by Leon Aiossa
// http://www.team-nexgen.org
using Roguelancer.Functionality;
using Roguelancer.Objects;
namespace Roguelancer.Interfaces {
    /// <summary>
    /// IDockable
    /// </summary>
    public interface IDockable {
        /// <summary>
        /// Dock
        /// </summary>
        /// <param name="game"></param>
        /// <param name="ship"></param>
        void Dock(RoguelancerGame game, Ship ship);
        /// <summary>
        /// UnDoc
        /// </summary>
        /// <param name="game"></param>
        /// <param name="ship"></param>
        void UnDock(RoguelancerGame game, Ship ship);
    }
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
        /// Reset
        /// </summary>
        /// <param name="game"></param>
        //void Reset(RoguelancerGame game);
    }
}