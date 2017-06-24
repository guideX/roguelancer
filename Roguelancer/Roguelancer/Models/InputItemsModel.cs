// Roguelancer 0.1 Pre Alpha by Leon Aiossa
// http://team-nexgen.com
using Roguelancer.Functionality;
namespace Roguelancer.Models {
    /// <summary>
    /// Input Items
    /// </summary>
    public class InputItemsModel {
        /// <summary>
        /// Mouse
        /// </summary>
        public MouseInputModel Mouse;
        /// <summary>
        /// keys
        /// </summary>
        public KeyInputModel Keys;
        /// <summary>
        /// Keys
        /// </summary>
        public KeyInputModel OldKeys;
        /// <summary>
        /// Old Keys Counter
        /// </summary>
        public int OldKeysCounter;
        /// <summary>
        /// Toggles
        /// </summary>
        public TogglesModel Toggles;
    }
}