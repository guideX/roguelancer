using System.Collections.Generic;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework;
using Roguelancer.Interfaces;
using Roguelancer.Enum;
using Roguelancer.Models;
using Roguelancer.Objects;
namespace Roguelancer.Interfaces {
    /// <summary>
    /// Game Menu
    /// </summary>
    public interface IGameMenu : IGame {
        #region "public properties"
        /// <summary>
        /// Current Menu
        /// </summary>
        CurrentMenu CurrentMenu { get; set; }
        /// <summary>
        /// Menu Buttons
        /// </summary>
        List<MenuButton> MenuButtons { get; set; }
        /// <summary>
        /// Last Menu
        /// </summary>
        CurrentMenu LastMenu { get; set; }
        /// <summary>
        /// Background Texture
        /// </summary>
        Texture2D BackgroundTexture { get; set; }
        /// <summary>
        /// Screen Height
        /// </summary>
        int ScreenHeight { get; set; }
        /// <summary>
        /// Screen Width
        /// </summary>
        int ScreenWidth { get; set; }
        /// <summary>
        /// Screen Rectangle
        /// </summary>
        Rectangle ScreenRectangle { get; set; }
        #endregion
    }
}