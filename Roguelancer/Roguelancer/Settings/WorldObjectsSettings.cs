using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Roguelancer.Functionality;
using Roguelancer.Models;
namespace Roguelancer.Settings {
    /// <summary>
    /// Model World Objects
    /// </summary>
    public class WorldObjectsSettings {
        /// <summary>
        /// Model
        /// </summary>
        public WorldObjectModel Model { get; set; }
        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="description"></param>
        /// <param name="descriptionLong"></param>
        /// <param name="startupPosition"></param>
        /// <param name="startupModelRotation"></param>
        /// <param name="settingsModelObject"></param>
        /// <param name="starSystemId"></param>
        /// <param name="initialModelUp"></param>
        /// <param name="initialModelRight"></param>
        /// <param name="initialVelocity"></param>
        /// <param name="initialCurrentThrust"></param>
        /// <param name="initialDirection"></param>
        /// <param name="cargoSpace"></param>
        /// <param name="id"></param>
        /// <param name="dockable"></param>
        /// <param name="destinationIndex"></param>
        /// <param name="jumpHoleTarget"></param>
        public WorldObjectsSettings(
                string description,
                string descriptionLong,
                Vector3 startupPosition,
                Vector3 startupModelRotation,
                SettingsObjectModel settingsModelObject,
                int starSystemId,
                Vector3 initialModelUp,
                Vector3 initialModelRight,
                Vector3 initialVelocity,
                float initialCurrentThrust,
                Vector3 initialDirection,
                int cargoSpace,
                int id, 
                bool dockable,
                int? destinationIndex,
                int? jumpHoleTarget
            ) {
            Model = new WorldObjectModel() {
                Description = description,
                DescriptionLong = descriptionLong,
                StartupPosition = startupPosition,
                StartupRotation = startupModelRotation,
                SettingsModelObject = settingsModelObject,
                StarSystemId = starSystemId,
                InitialModelUp = initialModelUp,
                InitialModelRight = initialModelRight,
                InitialVelocity = initialVelocity,
                InitialCurrentThrust = initialCurrentThrust,
                InitialDirection = initialDirection,
                CargoSpace = cargoSpace,
                ID = id,
                //Guid = guid,
                Dockable = dockable,
                DestinationIndex = destinationIndex,
                JumpHoleTarget = jumpHoleTarget
            };
        }
        /// <summary>
        /// Read
        /// </summary>
        /// <param name="ID"></param>
        /// <param name="modelSettings"></param>
        /// <param name="iniFile"></param>
        /// <param name="section"></param>
        /// <returns></returns>
        public static WorldObjectsSettings Read(int ID, List<SettingsObjectModel> modelSettings, string iniFile, string section) {
            int? jumpHoleTarget = NativeMethods.ReadINIInt(iniFile, section, "jump_hole_target", 0);
            int? destinationIndex = NativeMethods.ReadINIInt(iniFile, section, "destination_index", 0);
            var description = NativeMethods.ReadINI(iniFile, section, "description", "");
            var descriptionLong = NativeMethods.ReadINI(iniFile, section, "description_long", "");
            var startupPosition = NativeMethods.ReadINIVector3(iniFile, section, "startup_position_x", "startup_position_y", "startup_position_z");
            var startupModelRotation = NativeMethods.ReadINIVector3(iniFile, section, "startup_model_rotation_x", "startup_model_rotation_y", "startup_model_rotation_z");
            var modelWorldObjectSettings = modelSettings.Where(s => s.Enabled && s.ModelId == NativeMethods.ReadINIInt(iniFile, section, "model_index", 0)).FirstOrDefault();
            if (modelWorldObjectSettings != null) {
                SettingsObjectModel settingsModelObject = modelWorldObjectSettings.Clone();
                var starSystemID = NativeMethods.ReadINIInt(iniFile, section, "system_index", 0);
                var initialModelUp = NativeMethods.ReadINIVector3(iniFile, section, "initial_model_up_x", "initial_model_up_y", "initial_model_up_z");
                var initialModelRight = NativeMethods.ReadINIVector3(iniFile, section, "initial_model_right_x", "initial_model_right_y", "initial_model_right_z");
                var initialVelocity = NativeMethods.ReadINIVector3(iniFile, section, "initial_velocity_x", "initial_velocity_y", "initial_velocity_z");
                var initialCurrentThrust = float.Parse(NativeMethods.ReadINI(iniFile, section, "initial_current_thrust", "0"));
                var initialDirection = NativeMethods.ReadINIVector3(iniFile, section, "initial_direction_x", "initial_direction_y", "initial_direction_z");
                var cargoSpace = NativeMethods.ReadINIInt(iniFile, section, "cargo_space", 0);
                var dockable = NativeMethods.ReadINIBool(iniFile, section, "dockable", false);
                return new WorldObjectsSettings(
                    description,
                    descriptionLong,
                    startupPosition,
                    startupModelRotation,
                    settingsModelObject,
                    starSystemID,
                    initialModelUp,
                    initialModelRight,
                    initialVelocity,
                    initialCurrentThrust,
                    initialDirection,
                    cargoSpace,
                    ID,
                    dockable,
                    destinationIndex,
                    jumpHoleTarget
                );
            } else {
                throw new System.Exception("Settings Not Found.");
            }
        }
    }
}