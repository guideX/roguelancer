﻿// Roguelancer Planet
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
    public class PlanetCollection : IGame {
        public List<Planet> planets { get; set; }
        public string debugText { get; set; }
        public PlanetCollection() {
            planets = new List<Planet>();
        }
        public void Initialize(clsGame _Game) {
            foreach(SettingsModelObject settings in _Game.settings.planets) {
                Planet p = new Planet();
                p.model.settings = settings;
                planets.Add(p);
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
            debugText = "";
            for(int i = 0; i <= planets.Count - 1; i++) {
                planets[i].Update(_Game);
                if(!string.IsNullOrEmpty(debugText)) {
                    debugText = debugText + ", " + planets[i].model.position.Z.ToString() + " - " + planets[i].model.position.Y.ToString() + " - " + planets[i].model.position.Z.ToString();
                } else {
                    debugText = planets[i].model.position.Z.ToString() + " - " + planets[i].model.position.Y.ToString() + " - " + planets[i].model.position.Z.ToString();
                }
            }
        }
        public void Draw(clsGame _Game) {
            for(int i = 0; i <= planets.Count - 1; i++) {
                planets[i].Draw(_Game);
            }
        }
    }
    public class Planet : IGame {
        public clsModel model;
        public Planet() {
            model = new clsModel();
        }
        public void Initialize(clsGame _Game) {
            model.drawMode = clsModel.DrawMode.planet;
            model.Initialize(_Game);
        }
        public void LoadContent(clsGame _Game) {
            model.drawMode = clsModel.DrawMode.planet;
            model.LoadContent(_Game);
        }
        public void Update(clsGame _Game) {
            model.Update(_Game);
        }
        public void Draw(clsGame _Game) {
            model.Draw(_Game);
        }
    }
}