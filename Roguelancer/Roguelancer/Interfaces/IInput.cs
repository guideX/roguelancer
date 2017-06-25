// Roguelancer 0.1 Pre Alpha by Leon Aiossa
// http://team-nexgen.com
using Microsoft.Xna.Framework.Input;
using Roguelancer.Models;
namespace Roguelancer.Interfaces {
    /// <summary>
    /// Input
    /// </summary>
    public interface IInput {
        /// <summary>
        /// Last Keyboard State
        /// </summary>
        KeyboardState LastKeyboardState { get; set; }
        /// <summary>
        /// Current Keyboard State
        /// </summary>
        KeyboardState CurrentKeyboardState { get; set; }
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