using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Roguelancer.Particle;
using Roguelancer.Functionality;
namespace Roguelancer.Objects {
    public class StarSystem {
        private StationCollection stations;
        private PlanetCollection planets;
        private Starfields stars;
        public StarSystem(RoguelancerGame game) {
            stations = new StationCollection();
            planets = new PlanetCollection();
            stars = new Starfields(game.settings.starSystemSettings[0].starSettings);
        }
        public void Initialize(RoguelancerGame game) {
            stations.Initialize(game);
            planets.Initialize(game);
            stars.Initialize(game);
        }
        public void LoadContent(RoguelancerGame game) {
            stations.LoadContent(game);
            planets.LoadContent(game);
            stars.LoadContent(game);
        }
        public void Update(RoguelancerGame game) {
            stations.Update(game);
            planets.Update(game);
            stars.Update(game);
        }
        public void Draw(RoguelancerGame game) {
            stations.Draw(game);
            planets.Draw(game);
            stars.Draw(game);
        }
        public void Reset(RoguelancerGame game) {
            stations = new StationCollection();
            planets = new PlanetCollection();
            stars = new Starfields(new StarSettings(false, 0, 0, 0, 0, 0, 0, 0));
        }
    }
}