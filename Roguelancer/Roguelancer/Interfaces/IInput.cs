// Roguelancer 0.1 Pre Alpha by Leon Aiossa
// http://www.team-nexgen.com
using Roguelancer.Models;
namespace Roguelancer.Interfaces {
    public interface IInput : IGame {
        #region "public variables"
        /// <summary>
        /// Input Items
        /// </summary>
        InputItemsModel InputItems { get; set; }
        #endregion
    }
}