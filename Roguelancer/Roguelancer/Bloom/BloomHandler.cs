// Roguelancer 0.1 Pre Alpha by Leon Aiossa
// http://www.team-nexgen.com
namespace Roguelancer.Bloom {
    /// <summary>
    /// Bloom Handler
    /// </summary>
    public class BloomHandler {
        /// <summary>
        /// Bloom Settings
        /// </summary>
        private int _bloomSettings = 0;
        /// <summary>
        /// Bloom Component
        /// </summary>
        private BloomComponent _bloom;
        /// <summary>
        /// Bloom Handler
        /// </summary>
        /// <param name="game"></param>
        public BloomHandler(RoguelancerGame game) {
            _bloom = new BloomComponent(game);
        }
        /// <summary>
        /// Initialize
        /// </summary>
        /// <param name="game"></param>
        public void Initialize(RoguelancerGame game) {
            game.Components.Add(_bloom);
        }
        /// <summary>
        /// Load Content
        /// </summary>
        public void LoadContent() { }
        /// <summary>
        /// Update
        /// </summary>
        /// <param name="_BloomVisible"></param>
        public void Update(bool visible) {
            _bloom.Settings = BloomSettings.PresetSettings[_bloomSettings];
            _bloom.Visible = visible;
        }
        /// <summary>
        /// Draw
        /// </summary>
        public void Draw() {
            _bloom.BeginDraw();
        }
    }
}