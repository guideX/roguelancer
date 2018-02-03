using Microsoft.Xna.Framework;
using Roguelancer.Settings;

namespace Roguelancer.Models {
    /// <summary>
    /// World Object Model
    /// </summary>
    public class WorldObjectModel {
        /// <summary>
        /// ID
        /// </summary>
        public int ID { get; set; }
        /// <summary>
        /// Description
        /// </summary>
        public string Description { get; set; }
        /// <summary>
        /// Description Long
        /// </summary>
        public string DescriptionLong { get; set; }
        /// <summary>
        /// Startup Position
        /// </summary>
        public Vector3 StartupPosition { get; set; }
        /// <summary>
        /// Scaling
        /// </summary>
        public float Scaling { get; set; }
        /// <summary>
        /// Startup Model Rotation
        /// </summary>
        public Vector3 StartupRotation { get; set; }
        /// <summary>
        /// Settings Model Object
        /// </summary>
        public SettingsObjectModel SettingsModelObject { get; set; }
        /// <summary>
        /// Star System ID
        /// </summary>
        public int StarSystemId { get; set; }
        /// <summary>
        /// Default Startup Position
        /// </summary>
        public Vector3 DefaultStartupPosition { get; set; }
        /// <summary>
        /// Default Model Rotation
        /// </summary>
        public Vector3 DefaultModelRotation { get; set; }
        /// <summary>
        /// Initial Model Up
        /// </summary>
        public Vector3 InitialModelUp { get; set; }
        /// <summary>
        /// Initial Model Right
        /// </summary>
        public Vector3 InitialModelRight { get; set; }
        /// <summary>
        /// Initial Velocity
        /// </summary>
        public Vector3 InitialVelocity { get; set; }
        /// <summary>
        /// Initial Current Thrust
        /// </summary>
        public float InitialCurrentThrust { get; set; }
        /// <summary>
        /// Initial Direction
        /// </summary>
        public Vector3 InitialDirection { get; set; }
        /// <summary>
        /// Cargo Space
        /// </summary>
        public int CargoSpace { get; set; }
    }
}
