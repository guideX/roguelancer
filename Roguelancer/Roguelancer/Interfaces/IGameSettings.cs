// Roguelancer 0.1 Pre Alpha by Leon Aiossa
// http://team-nexgen.com
using Microsoft.Xna.Framework;
using Roguelancer.Models;
using Roguelancer.Models.Settings;
using Roguelancer.Settings;
using System.Collections.Generic;
namespace Roguelancer.Interfaces {
    /// <summary>
    /// Game Settings
    /// </summary>
    public interface IGameSettings {
        GameSettingsModel Model { get; set; }
    }
}