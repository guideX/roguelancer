// Roguelancer 0.1 Pre Alpha by Leon Aiossa
// http://team-nexgen.com
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Roguelancer.Enum;
using Roguelancer.Objects;
using System.Collections.Generic;
namespace Roguelancer.Models {
    /// <summary>
    /// Game Menu Model
    /// </summary>
    public class GameMenuModel {
        /// <summary>
        /// Current Menu
        /// </summary>
        public CurrentMenu CurrentMenu { get; set; }
        /// <summary>
        /// Menu Buttons
        /// </summary>
        public List<MenuButton> MenuButtons { get; set; }
        /// <summary>
        /// Last Menu
        /// </summary>
        public CurrentMenu LastMenu { get; set; }
        /// <summary>
        /// Background Texture
        /// </summary>
        public Texture2D BackgroundTexture { get; set; }
        /// <summary>
        /// Screen Height
        /// </summary>
        public int ScreenHeight { get; set; }
        /// <summary>
        /// Screen Width
        /// </summary>
        public int ScreenWidth { get; set; }
        /// <summary>
        /// Screen Rectangle
        /// </summary>
        public Rectangle ScreenRectangle { get; set; }
        /// <summary>
        /// Entry Point
        /// </summary>
        public GameMenuModel() {
            MenuButtons = new List<MenuButton>(); // Create new List of MenuButton's
        }
    }
}