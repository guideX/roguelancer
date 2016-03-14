using Roguelancer.Enum;
using Roguelancer.Objects;
using System.Collections.Generic;
namespace Roguelancer.Interfaces {
    /// <summary>
    /// Game Menu
    /// </summary>
    public interface IGameMenu : IGame {
        /// <summary>
        /// Current Menu
        /// </summary>
        CurrentMenu CurrentMenu { get; set; }
        /// <summary>
        /// Menu Buttons
        /// </summary>
        List<MenuButton> MenuButtons { get; set; }
    }
}