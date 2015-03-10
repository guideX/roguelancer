// Roguelancer 0.1 Pre Alpha by Leon Aiossa
// http://www.team-nexgen.org
using Roguelancer.Functionality;
namespace Roguelancer.Bloom {
    public class BloomHandler {
        private int lBloomSettings = 0;
        private BloomComponent _bloom;
        public BloomHandler(RoguelancerGame game) {
            _bloom = new BloomComponent(game);
        }
        public void Initialize(RoguelancerGame game) {
            game.Components.Add(_bloom);
        }
        public void LoadContent() {
            // TODO
        }
        public void Update(bool _BloomVisible) {
            _bloom.Settings = BloomSettings.PresetSettings[lBloomSettings];
            _bloom.Visible = _BloomVisible;
        }
        public void Draw() {
            _bloom.BeginDraw();
        }
    }
}