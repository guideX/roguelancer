using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Roguelancer.Functionality;
using Roguelancer.Interfaces;
namespace Roguelancer.Objects {
    public class StationCollection : IGame {
        public List<Station> stations { get; set; }
        public StationCollection() {
            stations = new List<Station>();
        }
        public void Initialize(clsGame _Game) {
            foreach(SettingsModelObject settings in _Game.settings.planets) {
                Station s = new Station();
                s.model.settings = settings;
                stations.Add(s);
            }
            for(int i = 0; i <= stations.Count - 1; i++) {
                stations[i].Initialize(_Game);
            }
        }
        public void LoadContent(clsGame _Game) {
            for(int i = 0; i <= stations.Count - 1; i++) {
                stations[i].LoadContent(_Game);
            }
        }
        public void Update(clsGame _Game) {
            for(int i = 0; i <= stations.Count - 1; i++) {
                stations[i].Update(_Game);
            }
        }
        public void Draw(clsGame _Game) {
            for(int i = 0; i <= stations.Count - 1; i++) {
                stations[i].Draw(_Game);
            }
        }
    }
    public class Station : IGame {
        public clsModel model;
        public Station() {
            model = new clsModel();
        }
        public void Initialize(clsGame _Game) {
            model.modelMode = clsModel.ModelMode.station;
            model.Initialize(_Game);
        }
        public void LoadContent(clsGame _Game) {
            model.LoadContent(_Game);
        }
        public void Update(clsGame _Game) {
            model.UpdatePosition();
            model.Update(_Game);
        }
        public void Draw(clsGame _Game) {
            model.Draw(_Game);
        }
    }
}
