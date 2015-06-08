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
using Roguelancer.Settings;
using Roguelancer.Models;
namespace Roguelancer.Objects {
    public class PlanetCollection : IGame {
        public List<Planet> planets { get; set; }
        public PlanetCollection() {
            planets = new List<Planet>();
        }
        public void Initialize(RoguelancerGame _Game) {
            Planet tempPlanet;
            foreach(ModelWorldObjects modelWorldObject in _Game.settings.starSystemSettings[0].planets) {
                tempPlanet = new Planet(_Game);
                tempPlanet.model.worldObject = modelWorldObject;
                planets.Add(tempPlanet);
            }

            for(int i = 0; i <= planets.Count - 1; i++) {
                planets[i].Initialize(_Game);
            }
        }
        public void LoadContent(RoguelancerGame _Game) {
            for(int i = 0; i <= planets.Count - 1; i++) {
                planets[i].LoadContent(_Game);
            }
        }
        public void Update(RoguelancerGame _Game) {
            for(int i = 0; i <= planets.Count - 1; i++) {
                planets[i].Update(_Game);
            }
        }
        public void Draw(RoguelancerGame _Game) {
            for(int i = 0; i <= planets.Count - 1; i++) {
                planets[i].Draw(_Game);
            }
        }
    }
    public class Planet : IGame, IDockable {
        public GameModel model;
        public Planet(RoguelancerGame _Game) {
            model = new GameModel(_Game);
        }
        public void Initialize(RoguelancerGame _Game) {
            model.modelMode = Enum.ModelModeEnum.planet;
            
            model.Initialize(_Game);
        }
        public void LoadContent(RoguelancerGame _Game) {
            model.LoadContent(_Game);
        }
        public void Update(RoguelancerGame _Game) {
            model.UpdatePosition();
            model.Update(_Game);
        }
        public void Draw(RoguelancerGame _Game) {
            model.Draw(_Game);
        }
        public void Dock(RoguelancerGame game, Ship ship) {
            
        }
        public void UnDock(RoguelancerGame game, Ship ship) {

        }
    }
}