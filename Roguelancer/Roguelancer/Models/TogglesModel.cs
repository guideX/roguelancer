// Roguelancer 0.1 Pre Alpha by Leon Aiossa
// http://team-nexgen.com
namespace Roguelancer.Models {
    /// <summary>
    /// Toggles Model
    /// </summary>
    public class TogglesModel {
        /// <summary>
        /// Free Mouse Mode
        /// </summary>
        public bool FreeMouseMode { get; set; }
        /// <summary>
        /// Mouse Mode
        /// </summary>
        public bool MouseMode { get; set; }
        /// <summary>
        /// Toggle Camera
        /// </summary>
        public bool ToggleCamera { get; set; }
        /// <summary>
        /// Revert Camera
        /// </summary>
        public bool RevertCamera { get; set; }
        /// <summary>
        /// Camera Snapshot
        /// </summary>
        public bool CameraSnapshot { get; set; }
        /// <summary>
        /// Cruise
        /// </summary>
        public bool Cruise { get; set; }
        /// <summary>
        /// Toggles Model
        /// </summary>
        public TogglesModel() {
            MouseMode = true;
        }
    }
}