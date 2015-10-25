using Roguelancer.Models;
namespace Roguelancer.Interfaces {
    public interface ISensorObject : IGame {
        /// <summary>
        /// Description
        /// </summary>
        string Description { get; set; }
        /// <summary>
        /// Model
        /// </summary>
        GameModel Model { get; set; }
    }
}