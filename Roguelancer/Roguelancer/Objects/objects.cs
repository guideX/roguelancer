using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Roguelancer.Functionality;
using Roguelancer.Particle;
namespace Roguelancer.Objects {
    public class GameObjects {
        public ShipCollection ships;
        private StarSystem starSystem;
        public GameObjects(RoguelancerGame _Game) {
            starSystem = new StarSystem();
            ships = new ShipCollection(_Game);
        }
        public void Initialize(RoguelancerGame _Game) {
            starSystem.Initialize(_Game);
            ships.Initialize(_Game);
        }
        public void LoadContent(RoguelancerGame _Game) {
            starSystem.LoadContent(_Game);
            ships.LoadContent(_Game);
        }
        public void Update(RoguelancerGame _Game) {
            starSystem.Update(_Game);
            ships.Update(_Game);
        }
        public void Draw(RoguelancerGame _Game) {
            starSystem.Draw(_Game);
            ships.Draw(_Game);
        }
    }
}