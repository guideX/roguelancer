using Roguelancer.Enum;
namespace Roguelancer.Models {
    /// <summary>
    /// Keyboard Key Status Model
    /// </summary>
    public class KeyboardKeyStatusModel {
        /// <summary>
        /// Is Key Down
        /// </summary>
        public bool IsKeyDown { get; set; }
        /// <summary>
        /// Was key Pressed
        /// </summary>
        public bool WasKeyPressed { get; set; }
        /// <summary>
        /// Assignment
        /// </summary>
        public KeyboardAssignmentEnum Assignment { get; set; }
    }
}