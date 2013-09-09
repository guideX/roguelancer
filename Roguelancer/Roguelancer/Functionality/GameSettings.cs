using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Xna.Framework;
using System.Globalization;
namespace Roguelancer.Functionality {
    public enum ModelType { // THE TYPE OF MODEL
        Ship = 1,
        Planet = 2,
        Station = 3
    }
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
    public class StarSettings {
        public bool starsEnabled { get; set; }
        public int amountOfStarsPerSheet { get; set; }
        public int maxPositionX { get; set; }
        public int maxPositionY { get; set; }
        public int maxSize { get; set; }
        public int maxPositionIncrementY { get; set; }
        public int maxPositionStartingY { get; set; }
        public int numberOfStarSheets { get; set; }
        public StarSettings
            (
                bool _starsEnabled,
                int _amountOfStarsPerSheet,
                int _maxPositionX,
                int _maxPositionY,
                int _maxSize,
                int _maxPositionIncrementY,
                int _maxPositionStartingY,
                int _numberOfStarSheets
            ) {
            starsEnabled = _starsEnabled;
            amountOfStarsPerSheet = _amountOfStarsPerSheet;
            maxPositionX = _maxPositionX;
            maxPositionY = _maxPositionY;
            maxSize = _maxSize;
            maxPositionIncrementY = _maxPositionIncrementY;
            maxPositionStartingY = _maxPositionStartingY;
            numberOfStarSheets = _numberOfStarSheets;
        }
    }
    public class StarSystemSettings { // SETTINGS FOR EACH STAR SYSTEM
        private string _path { get; set; }
        private int _starSystemId { get; set; }
        public List<ModelWorldObjects> ships { get; set; }
        public List<ModelWorldObjects> stations { get; set; }
        public List<ModelWorldObjects> planets { get; set; }
        public StarSettings starSettings { get; set; }
        public StarSystemSettings(
                int starSystemId,
                string path,
                string systemIniStartPath,
                List<SettingsModelObject> modelSettings,
                ModelWorldObjects player,
                StarSettings _starSettings
            ) {
            ships = new List<ModelWorldObjects>();
            planets = new List<ModelWorldObjects>();
            stations = new List<ModelWorldObjects>();
            player.settingsModelObject.isPlayer = true;
            ships.Add(player);
            for(int i = 1; i < Convert.ToInt32(IniFile.ReadINI(systemIniStartPath + path + @"\ships.ini", "settings", "count", "0")) + 1; ++i) {
                ships.Add(ModelWorldObjects.Read(modelSettings, systemIniStartPath + path + @"\ships.ini", i.ToString().Trim()));
            }
            for(int i = 1; i < Convert.ToInt32(IniFile.ReadINI(systemIniStartPath + path + @"\stations.ini", "settings", "count", "0")) + 1; ++i) {
                stations.Add(ModelWorldObjects.Read(modelSettings, systemIniStartPath + path + @"\stations.ini", i.ToString().Trim()));
            }
            for(int i = 1; i < Convert.ToInt32(IniFile.ReadINI(systemIniStartPath + path + @"\planets.ini", "settings", "count", "0")) + 1; ++i) {
                planets.Add(ModelWorldObjects.Read(modelSettings, systemIniStartPath + path + @"\planets.ini", i.ToString().Trim()));
            }
            starSettings = _starSettings;
        }
    }
    public class SettingsModelObject {
        private string _modelPath { get; set; }
        private Vector3 _modelScaling { get; set; }
        private bool _enabled { get; set; }
        private int _modelId { get; set; }
        private ModelType _modelType { get; set; }
        private bool _isPlayer { get; set; }
        public SettingsModelObject(
                string modelPath,
                Vector3 modelScaling,
                ModelType modelType,
                bool enabled,
                int modelId
            ) {
            _modelPath = modelPath;
            _modelScaling = modelScaling;
            _modelType = modelType;
            _enabled = enabled;
            _modelId = modelId;
        }
        public static SettingsModelObject Clone(SettingsModelObject oldObject) {
            return new SettingsModelObject(
                oldObject.modelPath,
                oldObject.modelScaling,
                oldObject.modelType,
                oldObject.enabled,
                oldObject.modelId
            );
        }
        public string modelPath { get { return _modelPath; } }
        public ModelType modelType { get { return _modelType; } }
        public Vector3 modelScaling { get { return _modelScaling; } }
        public bool enabled { get { return _enabled; } }
        public int modelId { get { return _modelId; } }
        public bool isPlayer { get { return _isPlayer; } set { _isPlayer = value; } }
    }
    public class CameraSettings {
        public Vector3 desiredPositionOffset = new Vector3(0, 400.0f, 1830.0f);
        public float stiffness = 1800.0f; // How loosely the ship can be positioned
        public float damping = 600.0f; // Weird Back and fourth thingy
        public float mass = 50.0f; // Too little and ship is all over the place, too much and it sways back and fourth
        public float fieldOfView = MathHelper.ToRadians(45.0f); // Builds a perspective projection matrix based on a field of view
        public float nearPlaneDistance = 1.0f; // How close the camera can get
        public float clippingDistance = 1000000.0f; // Distance for the clipping plane
        public Vector3 lookAtOffset = new Vector3(0, 3.8f, 0);
        public float lookAtDivideBy = 2.0f;
        public float newCameraX = 0.5f;
        public float newCameraY = 0.5f;
        public int thrustViewAmount = 10000;
        public float aspectRatio = 4.0f / 3.0f; // Screen Dimensions
        public CameraSettings(string cameraSettingsIniFile) {
            desiredPositionOffset = IniFile.ReadINIVector3(cameraSettingsIniFile, "settings", "desired_position_offset_x", "desired_position_offset_y", "desired_position_offset_z");
            stiffness = IniFile.ReadIniFloat(cameraSettingsIniFile, "settings", "stiffness");
            damping = IniFile.ReadIniFloat(cameraSettingsIniFile, "settings", "damping");
            mass = IniFile.ReadIniFloat(cameraSettingsIniFile, "settings", "mass");
            fieldOfView = MathHelper.ToRadians(IniFile.ReadIniFloat(cameraSettingsIniFile, "settings", "field_of_view"));
            nearPlaneDistance = IniFile.ReadIniFloat(cameraSettingsIniFile, "settings", "near_plane_distance");
            clippingDistance = IniFile.ReadIniFloat(cameraSettingsIniFile, "settings", "clipping_distance");
            lookAtOffset = IniFile.ReadINIVector3(cameraSettingsIniFile, "settings", "look_at_offset_x", "look_at_offset_y", "look_at_offset_z");
            lookAtDivideBy = IniFile.ReadIniFloat(cameraSettingsIniFile, "settings", "look_at_divide_by");
            newCameraX = IniFile.ReadIniFloat(cameraSettingsIniFile, "settings", "new_camera_x");
            newCameraY = IniFile.ReadIniFloat(cameraSettingsIniFile, "settings", "new_camera_y");
            int.TryParse(IniFile.ReadINI(cameraSettingsIniFile, "settings", "thrust_view_amount"), out thrustViewAmount);
            aspectRatio = IniFile.ReadIniFloat(cameraSettingsIniFile, "settings", "aspect_ratio_1") / IniFile.ReadIniFloat(cameraSettingsIniFile, "settings", "aspect_ratio_2");
        }
    }
    public class GameSettings {
        public string menuBackgroundTexture;
        public string menuText;
        public CameraSettings cameraSettings;
        public string groundTexture = "";
        public string font = "LucidaFont";
        public Vector2 resolution = new Vector2(1280, 1024);
        public List<SettingsModelObject> modelSettings { get; set; }
        public List<StarSystemSettings> starSystemSettings { get; set; }
        private string appPath;
        private string directoryCorrection;
        private string gameSettingsIniFile;
        private string modelSettingsIniFile;
        private string systemsSettingsIniFile;
        private string playerIniFile;
        private string systemIniStartPath;
        private string cameraSettingsIniFile;
        public GameSettings() {
            LoadSettings();
            cameraSettings = new CameraSettings(cameraSettingsIniFile);
            LoadObjects();
            LoadStarSystem();
        }
        private void LoadSettings() {
            appPath = System.IO.Directory.GetCurrentDirectory();
            directoryCorrection = @"\..\..\..\";
            gameSettingsIniFile = appPath + directoryCorrection + @"settings\settings\settings.ini";
            modelSettingsIniFile = appPath + directoryCorrection + @"settings\models.ini";
            systemsSettingsIniFile = appPath + directoryCorrection + @"settings\systems\systems.ini";
            playerIniFile = appPath + directoryCorrection + @"settings\player\settings.ini";
            cameraSettingsIniFile = appPath + directoryCorrection + @"settings\camera.ini";
            systemIniStartPath = appPath + directoryCorrection + @"settings\systems\";
            menuBackgroundTexture = IniFile.ReadINI(gameSettingsIniFile, "Settings", "menu_background");
            menuText = "Roguelancer" + Environment.NewLine + Environment.NewLine + "10 = Play Game" + Environment.NewLine + "F9 = Return to menu" + Environment.NewLine + "ESC = Quit";
        }
        private void LoadObjects() {
            modelSettings = new List<SettingsModelObject>();
            int count = Convert.ToInt32(IniFile.ReadINI(modelSettingsIniFile, "settings", "count", "0"));
            for(int i = 1; i < count + 1; ++i) {
                modelSettings.Add(new SettingsModelObject(
                    IniFile.ReadINI(modelSettingsIniFile, i.ToString().Trim(), "path"),
                    IniFile.ReadINIVector3(modelSettingsIniFile, i.ToString(), "model_scaling_x", "model_scaling_y", "model_scaling_z"),
                    (ModelType)Convert.ToInt32(IniFile.ReadINI(modelSettingsIniFile, i.ToString().Trim(), "type", "0")),
                    Convert.ToBoolean(IniFile.ReadINI(modelSettingsIniFile, i.ToString().Trim(), "enabled", "false")),
                    i
                ));
            }
        }
        private ModelWorldObjects LoadPlayer() {
            return ModelWorldObjects.Read(modelSettings, playerIniFile, "settings");
        }
        public void LoadStarSystem() {
            starSystemSettings = new List<StarSystemSettings>();
            int systemCount = Convert.ToInt32(IniFile.ReadINI(systemsSettingsIniFile, "settings", "count", "0"));
            for(int i = 1; i < systemCount + 1; ++i) {
                starSystemSettings.Add(new StarSystemSettings(
                    i,
                    IniFile.ReadINI(systemsSettingsIniFile, i.ToString().Trim(), "path", ""),
                    systemIniStartPath,
                    modelSettings,
                    LoadPlayer(),
                    new StarSettings(
                        Convert.ToBoolean(IniFile.ReadINI(systemsSettingsIniFile, i.ToString(), "starsEnabled", "")),
                        Convert.ToInt32(IniFile.ReadINI(systemsSettingsIniFile, i.ToString(), "amountOfStarsPerSheet", "0")),
                        Convert.ToInt32(IniFile.ReadINI(systemsSettingsIniFile, i.ToString(), "maxPositionX", "0")),
                        Convert.ToInt32(IniFile.ReadINI(systemsSettingsIniFile, i.ToString(), "maxPositionY", "0")),
                        Convert.ToInt32(IniFile.ReadINI(systemsSettingsIniFile, i.ToString(), "maxSize", "0")),
                        Convert.ToInt32(IniFile.ReadINI(systemsSettingsIniFile, i.ToString(), "maxPositionIncrementY", "0")),
                        Convert.ToInt32(IniFile.ReadINI(systemsSettingsIniFile, i.ToString(), "maxPositionStartingY", "0")),
                        Convert.ToInt32(IniFile.ReadINI(systemsSettingsIniFile, i.ToString(), "numberOfStarSheets", "0"))
                    )
                ));
            }
        }
        private void Initialize(RoguelancerGame _Game) { }
        private void LoadContent(RoguelancerGame _Game) { }
        public void Update(RoguelancerGame _Game) { }
        private void Draw(RoguelancerGame _Game) { }
    }
}