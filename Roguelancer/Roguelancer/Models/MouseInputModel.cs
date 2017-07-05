

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
namespace Roguelancer.Models {
    /// <summary>
    /// Mouse Input Model
    /// </summary>
    public class MouseInputModel {
        /// <summary>
        /// Scroll Wheel
        /// </summary>
        public float ScrollWheel { get; set; }
        /// <summary>
        /// Left Button
        /// </summary>
        public bool LeftButton { get; set; }
        /// <summary>
        /// Right Button
        /// </summary>
        public bool RightButton { get; set; }
        /// <summary>
        /// Vector
        /// </summary>
        public Vector2 Vector { get; set; }
        /// <summary>
        /// State
        /// </summary>
        public MouseState State { get; set; }
    }
}