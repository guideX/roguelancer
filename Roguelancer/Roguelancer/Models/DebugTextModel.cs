using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
namespace Roguelancer.Models {
    /// <summary>
    /// Debug Text Model
    /// </summary>
    public class DebugTextModel {
        /// <summary>
        /// Timer Enabled
        /// </summary>
        public bool TimerEnabled { get; set; }
        /// <summary>
        /// Show Enabled
        /// </summary>
        public bool ShowEnabled { get; set; }
        /// <summary>
        /// Show Time
        /// </summary>
        public int ShowTime { get; set; }
        /// <summary>
        /// Current Show Time
        /// </summary>
        public int CurrentShowTime { get; set; }
        /// <summary>
        /// Text
        /// </summary>
        public string Text { get; set; }
        /// <summary>
        /// Font
        /// </summary>
        public SpriteFont Font { get; set; }
        /// <summary>
        /// Font Position
        /// </summary>
        public Vector2 FontPosition { get; set; }
        /// <summary>
        /// Debug Text Model
        /// </summary>
        public DebugTextModel() {
            ShowTime = 100;
        }
    }
}