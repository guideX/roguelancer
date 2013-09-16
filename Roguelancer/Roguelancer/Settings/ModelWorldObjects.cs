using Microsoft.Xna.Framework;
using Roguelancer.Functionality;
using System;
using System.Collections.Generic;
using System.Linq;
namespace Roguelancer.Settings {
    public class ModelWorldObjects { // THE MODELS IN THE WORLD
        private Vector3 _startupPosition { get; set; }
        private Vector3 _startupModelRotation { get; set; }
        private SettingsModelObject _settingsModelObject { get; set; }
        private int _starSystemId { get; set; }
        private Vector3 _defaultStartupPosition { get; set; }
        private Vector3 _defaultModelRotation { get; set; }
        private Vector3 _initialModelUp { get; set; }
        private Vector3 _initialModelRight { get; set; }
        private Vector3 _initialVelocity { get; set; }
        private float _initialCurrentThrust { get; set; }
        public Vector3 _initialDirection { get; set; }
        public ModelWorldObjects(Vector3 startupPosition,
                Vector3 startupModelRotation,
                SettingsModelObject settingsModelObject,
                int starSystemId,
                Vector3 initialModelUp,
                Vector3 initialModelRight,
                Vector3 initialVelocity,
                float initialCurrentThrust,
                Vector3 initialDirection
            ) {
            _startupPosition = startupPosition;
            _startupModelRotation = startupModelRotation;
            _settingsModelObject = settingsModelObject;
            _starSystemId = starSystemId;
            _initialModelUp = initialModelUp;
            _initialModelRight = initialModelRight;
            _initialVelocity = initialVelocity;
            _initialCurrentThrust = initialCurrentThrust;
            _initialDirection = initialDirection;
        }
        public static ModelWorldObjects Clone(ModelWorldObjects oldObject) {
            return new ModelWorldObjects(
                    oldObject.startupPosition,
                    oldObject.startupModelRotation,
                    SettingsModelObject.Clone(oldObject.settingsModelObject),
                    oldObject.starSystemId,
                    oldObject.initialModelUp,
                    oldObject.initialModelRight,
                    oldObject.initialVelocity,
                    oldObject.initialCurrentThrust,
                    oldObject._initialDirection
                );
        }
        public static ModelWorldObjects Read(List<SettingsModelObject> modelSettings, string iniFile, string section) {
            return new ModelWorldObjects(
                IniFile.ReadINIVector3(iniFile, section, "startup_position_x", "startup_position_y", "startup_position_z"),
                IniFile.ReadINIVector3(iniFile, section, "startup_model_rotation_x", "startup_model_rotation_y", "startup_model_rotation_z"),
                SettingsModelObject.Clone(modelSettings.Where(s => s.enabled == true && s.modelId == Convert.ToInt32(IniFile.ReadINI(iniFile, section, "model_index", "0"))).FirstOrDefault()),
                Convert.ToInt32(IniFile.ReadINI(iniFile, section, "system_index", "0")),
                IniFile.ReadINIVector3(iniFile, section, "initial_model_up_x", "initial_model_up_y", "initial_model_up_z"),
                IniFile.ReadINIVector3(iniFile, section, "initial_model_right_x", "initial_model_right_y", "initial_model_right_z"),
                IniFile.ReadINIVector3(iniFile, section, "initial_velocity_x", "initial_velocity_y", "initial_velocity_z"),
                float.Parse(IniFile.ReadINI(iniFile, section, "initial_current_thrust", "0")),
                IniFile.ReadINIVector3(iniFile, section, "initial_direction_x", "initial_direction_y", "initial_direction_z")
                );
        }
        public SettingsModelObject settingsModelObject { get { return _settingsModelObject; } }
        public Vector3 startupPosition { get { return _startupPosition; } }
        public Vector3 startupModelRotation { get { return _startupModelRotation; } }
        public int starSystemId { get { return _starSystemId; } }
        public Vector3 initialModelUp { get { return _initialModelUp; } }
        public Vector3 initialModelRight { get { return _initialModelRight; } }
        public Vector3 initialVelocity { get { return _initialVelocity; } }
        public float initialCurrentThrust { get { return _initialCurrentThrust; } }
        public Vector3 initialDirection { get { return _initialDirection; } }
    }
}