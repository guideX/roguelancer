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
        public void Initialize(RoguelancerGame game) {
            Planet tempPlanet;
            foreach(ModelWorldObjects modelWorldObject in game.Settings.starSystemSettings[0].planets) {
                tempPlanet = new Planet(game);
                tempPlanet.model.WorldObject = modelWorldObject;
                planets.Add(tempPlanet);
            }

            for(int i = 0; i <= planets.Count - 1; i++) {
                planets[i].Initialize(game);
            }
        }
        public void LoadContent(RoguelancerGame game) {
            for(int i = 0; i <= planets.Count - 1; i++) {
                planets[i].LoadContent(game);
            }
        }
        public void Update(RoguelancerGame game) {
            for(int i = 0; i <= planets.Count - 1; i++) {
                planets[i].Update(game);
            }
        }
        public void Draw(RoguelancerGame game) {
            for(int i = 0; i <= planets.Count - 1; i++) {
                planets[i].Draw(game);
            }
        }
    }
    public class Planet : IGame, IDockable {
        public GameModel model;
        public Planet(RoguelancerGame game) {
            model = new GameModel(game);
        }
        public void Initialize(RoguelancerGame game) {
            model.ModelMode = Enum.ModelModeEnum.Planet;
            
            model.Initialize(game);
        }
        public void LoadContent(RoguelancerGame game) {
            model.LoadContent(game);
        }
        public void Update(RoguelancerGame game) {
            model.UpdatePosition();
            model.Update(game);
        }
        public void Draw(RoguelancerGame game) {
            model.Draw(game);
        }
        public void Dock(RoguelancerGame game, Ship ship) {
            
        }
        public void UnDock(RoguelancerGame game, Ship ship) {

        }
    }
}