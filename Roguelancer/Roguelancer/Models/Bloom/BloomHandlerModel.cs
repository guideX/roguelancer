// Roguelancer 0.1 Pre Alpha by Leon Aiossa
// http://team-nexgen.com
using Roguelancer.Bloom;
namespace Roguelancer.Models.Bloom {
    /// <summary>
    /// Bloom Handler Model
    /// </summary>
    public class BloomHandlerModel {
        /// <summary>
        /// Bloom Settings
        /// </summary>
        public int BloomSettings { get; set; }
        /// <summary>
        /// Bloom Component
        /// </summary>
        public BloomComponent Bloom { get; set; }
    }
}