

using Roguelancer.Bloom;
namespace Roguelancer.Models {
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