using System.Collections.Generic;
using Roguelancer.Settings;
namespace Roguelancer.Models {
    /// <summary>
    /// Star System Settings Model
    /// </summary>
    public class StarSystemSettingsModel {
        /// <summary>
        /// Description
        /// </summary>
        public string Description { get; set; }
        /// <summary>
        /// Ships
        /// </summary>
        public List<WorldObjectsSettings> Ships { get; set; }
        /// <summary>
        /// Stations
        /// </summary>
        public List<WorldObjectsSettings> Stations { get; set; }
        /// <summary>
        /// Bullets
        /// </summary>
        public List<WorldObjectsSettings> Bullets { get; set; }
        /// <summary>
        /// Planets
        /// </summary>
        public List<WorldObjectsSettings> Planets { get; set; }
        /// <summary>
        /// Docking Rings
        /// </summary>
        public List<WorldObjectsSettings> DockingRings { get; set; }
        /// <summary>
        /// Trade Lanes
        /// </summary>
        public List<WorldObjectsSettings> TradeLanes { get; set; }
        /// <summary>
        /// Jump Holes
        /// </summary>
        public List<WorldObjectsSettings> JumpHoles { get; set; }
        /// <summary>
        /// Star Settings
        /// </summary>
        public StarSettingsModel StarSettings { get; set; }
    }
}