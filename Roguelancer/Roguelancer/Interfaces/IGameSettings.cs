

using Roguelancer.Models;
namespace Roguelancer.Interfaces {
    /// <summary>
    /// Game Settings
    /// </summary>
    public interface IGameSettings {
        GameSettingsModel Model { get; set; }
    }
}