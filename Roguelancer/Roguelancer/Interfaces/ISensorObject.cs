

using Roguelancer.Models;
using System.Collections.Generic;
namespace Roguelancer.Interfaces {
    /// <summary>
    /// Sensor Object
    /// </summary>
    public interface ISensorObject : IGame {
        /// <summary>
        /// Game Model
        /// </summary>
        GameModel Model { get; set; }
    }
    /// <summary>
    /// Sensor Object
    /// </summary>
    public interface ITradeLaneSensorObject : IGame {
        /// <summary>
        /// Game Model
        /// </summary>
        List<TradeLaneModel> Models { get; set; }
    }
}