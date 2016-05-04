// Roguelancer 0.1 Pre Alpha by Leon Aiossa
// http://www.team-nexgen.com
using Roguelancer.Models;
namespace Roguelancer.Interfaces {
    /// <summary>
    /// Input
    /// </summary>
    public interface IInput {
        /// <summary>
        /// Input Items
        /// </summary>
        InputItemsModel InputItems { get; set; }
        /// <summary>
        /// Update
        /// </summary>
        /// <param name="game"></param>
        void Update(RoguelancerGame game);
        /// <summary>
        /// Dispose
        /// </summary>
        void Dispose(RoguelancerGame game);
    }
}