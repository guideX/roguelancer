// Roguelancer Planet
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Roguelancer.Interfaces;
using Roguelancer.Functionality;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
namespace Roguelancer.Objects {
    public class clsPlanetCollection : intGame {
        public List<clsPlanet> planets { get; set; }
        public clsPlanetCollection() {
            planets = new List<clsPlanet>();
        }
        public void Initialize(clsGame _Game) {
            foreach(clsSettingsModelObject planet in _Game.lSettings.planets) {
                planets.Add(new clsPlanet {
                    settings = planet
                });
            }
            for(int i = 0; i <= planets.Count - 1; i++) {
                planets[i].Initialize(_Game);
            }
        }
        public void LoadContent(clsGame _Game) {
            for(int i = 0; i <= planets.Count - 1; i++) {
                planets[i].LoadContent(_Game);
            }
        }
        public void Update(clsGame _Game) {
            for(int i = 0; i <= planets.Count - 1; i++) {
                planets[i].Update(_Game);
            }
        }
        public void Draw(clsGame _Game) {
            for(int i = 0; i <= planets.Count - 1; i++) {
                planets[i].Draw(_Game);
            }
        }
    }
    public class clsPlanet : intGame {
        public clsSettingsModelObject settings { get; set; }
        private clsModel model;
        public void Initialize(clsGame _Game) {
            model = new clsModel();
        }
        public void LoadContent(clsGame _Game) {
            model.drawMode = clsModel.DrawMode.planet;
            model.modelPath = settings.modelPath;
            if(settings != null) {
                model.modelPath = settings.modelPath;
                model.startPosition = settings.startupPosition;
                model.modelScaling = settings.modelScaling;
            }
            model.Initialize(_Game);
        }
        public void Update(clsGame _Game) {
            model.Update(_Game);
        }
        public void Draw(clsGame _Game) {
            model.Draw(_Game);
        }
    }
}