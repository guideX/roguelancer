using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Xna.Framework;
namespace Roguelancer.Functionality {
    public class CameraSettings {
        public Vector3 desiredPositionOffset = new Vector3(0, 400.0f, 1830.0f);
        public float stiffness = 1800.0f; // How loosely the ship can be positioned
        public float damping = 600.0f; // Weird Back and fourth thingy
        public float mass = 50.0f; // Too little and ship is all over the place, too much and it sways back and fourth
        public float fieldOfView = MathHelper.ToRadians(45.0f); // Builds a perspective projection matrix based on a field of view
        public float nearPlaneDistance = 1.0f; // How close the camera can get
        public float clippingDistance = 1000000.0f; // Distance for the clipping plane
        public Vector3 lookAtOffset = new Vector3(0, 2.8f, 0);
        public float lookAtDivideBy = 2.0f;
        public float newCameraX = 0.5f;
        public float newCameraY = 0.5f;
        public int thrustViewAmount = 10000;
        public float aspectRatio = 4.0f / 3.0f; // Screen Dimensions
    }
    public class Settings {
        public CameraSettings cameraSettings;
        public string appPath = System.IO.Directory.GetCurrentDirectory();
        public string groundTexture = "";
        public string font = "LucidaFont";
        public Vector2 resolution = new Vector2(1280, 1024);
        public SettingsModelObject testStation = new SettingsModelObject(
            "BASES\\BADLANDS\\BADLANDS",
            new Vector3(1.0f, 0f, 20.0f),
            new Vector3(0.0f, -1.9f, 0.0f),
            new Vector3(2.0f, 2.0f, 1.0f)
        );
        public SettingsModelObject playerShip = new SettingsModelObject(
            "SHIPS\\PI_TRANSPORT\\PI_TRANSPORT",
            new Vector3(1.0f, 0f, 10.0f),
            new Vector3(0.0f, -1.9f, 0.0f),
            new Vector3(.5f, .5f, .5f),
            true
        );
        public SettingsModelObject enemyShip = new SettingsModelObject(
            "Ship",
            new Vector3(1.0f, 0f, 20.0f),
            new Vector3(0.0f, -1.9f, 0.0f),
            new Vector3(2.0f, 2.0f, 1.0f)
        );
        public SettingsModelObject earth = new SettingsModelObject(
            "Earth",
            new Vector3(1.0f, 0f, 20.0f),
            new Vector3(0.0f, -1.9f, 0.0f),
            new Vector3(5000.0f, 5000.0f, 5000.0f)
        );
        public List<SettingsModelObject> ships { get; set; }
        public List<SettingsModelObject> planets { get; set; }
        public List<SettingsModelObject> stations { get; set; }
        public Settings() {
            cameraSettings = new CameraSettings();
            ships = new List<SettingsModelObject>();
            planets = new List<SettingsModelObject>();
            stations = new List<SettingsModelObject>();
            stations.Add(testStation);
            planets.Add(earth);
            ships.Add(playerShip);
            ships.Add(enemyShip);
        }
    }
    public class SettingsModelObject {
        private string _modelPath { get; set; }
        private Vector3 _startupPosition { get; set; }
        private Vector3 _modelRotation { get; set; }
        private Vector3 _modelScaling { get; set; }
        private bool _isPlayerShip { get; set; }
        public SettingsModelObject(
                string modelPath, 
                Vector3 startupPosition, 
                Vector3 modelRotation, 
                Vector3 modelScaling, 
                bool isPlayerShip = false
            ) {
            _modelPath = modelPath;
            _startupPosition = startupPosition;
            _modelRotation = modelRotation;
            _modelScaling = modelScaling;
            _isPlayerShip = isPlayerShip;
        }
        public Vector3 startupPosition { get { return _startupPosition; } }
        public Vector3 modelRotation { get { return _modelRotation; } }
        public Vector3 modelScaling { get { return _modelScaling; } }
        public string modelPath { get { return _modelPath; } }
        public bool isPlayerShip { get { return _isPlayerShip; } }
    }
}