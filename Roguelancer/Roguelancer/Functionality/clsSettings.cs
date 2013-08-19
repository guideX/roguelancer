using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Xna.Framework;
namespace Roguelancer.Functionality {
    public class Settings {
        public Vector3 shipModelScaling = new Vector3(2.0f, 2.0f, 1.0f);
        public string shipTexture = "SHIPS\\PI_TRANSPORT\\PI_TRANSPORT";
        public string appPath = System.IO.Directory.GetCurrentDirectory();
        public string groundTexture = "";
        public string font = "LucidaFont";
        public Vector2 resolution = new Vector2(1024, 768);
        public SettingsModelObject earth = new SettingsModelObject(
            "Earth", 
            new Vector3(1.0f, 0f, -1.0f), 
            new Vector3(0.0f, -1.9f, 0.0f), 
            new Vector3(.5f, .5f, .5f)
        );
        public List<SettingsModelObject> planets { get; set; }
        public Settings() {
            planets = new List<SettingsModelObject>();
            planets.Add(earth);
        }
    }
    public class SettingsModelObject {
        public string modelPath { get; set; }
        public Vector3 startupPosition { get; set; }
        public Vector3 modelRotation { get; set; }
        public Vector3 modelScaling { get; set; }
        public SettingsModelObject(string _modelPath, 
                Vector3 _startupPosition, 
                Vector3 _modelRotation, 
                Vector3 _modelScaling) {
            modelPath = _modelPath;
            startupPosition = _startupPosition;
            modelRotation = _modelRotation;
            modelScaling = _modelScaling;
        }
    }
}