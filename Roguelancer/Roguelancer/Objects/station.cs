using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Roguelancer.Functionality;
using Roguelancer.Interfaces;
using Roguelancer.Settings;
namespace Roguelancer.Objects {
    public class StationCollection : IGame {
        public List<Station> stations { get; set; }
        public StationCollection() {
            stations = new List<Station>();
        }
        public void Initialize(RoguelancerGame _Game) {
            Station tempStation;
            foreach(ModelWorldObjects modelWorldObject in _Game.settings.starSystemSettings[0].stations) {
                tempStation = new Station(_Game);
                tempStation.model.worldObject = modelWorldObject;
                stations.Add(tempStation);
            }
            for(int i = 0; i <= stations.Count - 1; i++) {
                stations[i].Initialize(_Game);
            }
        }
        public void LoadContent(RoguelancerGame _Game) {
            for(int i = 0; i <= stations.Count - 1; i++) {
                stations[i].LoadContent(_Game);
            }
        }
        public void Update(RoguelancerGame _Game) {
            for(int i = 0; i <= stations.Count - 1; i++) {
                stations[i].Update(_Game);
            }
        }
        public void Draw(RoguelancerGame _Game) {
            for(int i = 0; i <= stations.Count - 1; i++) {
                stations[i].Draw(_Game);
            }
        }
    }
    public class Station : IGame {
        public GameModel model;
        public Station(RoguelancerGame _Game) {
            model = new GameModel(_Game);
        }
        public void Initialize(RoguelancerGame _Game) {
            model.modelMode = GameModel.ModelMode.station;
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
    }
}
