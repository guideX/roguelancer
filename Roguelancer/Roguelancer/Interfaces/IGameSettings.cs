// Roguelancer 0.1 Pre Alpha by Leon Aiossa
// http://team-nexgen.com
using Roguelancer.Models.Settings;
namespace Roguelancer.Interfaces {
    /// <summary>
    /// Game Settings
    /// </summary>
    public interface IGameSettings {
        GameSettingsModel Model { get; set; }
    }
}