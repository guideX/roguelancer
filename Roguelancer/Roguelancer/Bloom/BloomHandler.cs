// Roguelancer 0.1 Pre Alpha by Leon Aiossa
// http://www.team-nexgen.org
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
            try {
                _bloom = new BloomComponent(game);
            } catch {
                throw;
            }
        }
        /// <summary>
        /// Initialize
        /// </summary>
        /// <param name="game"></param>
        public void Initialize(RoguelancerGame game) {
            try {
                game.Components.Add(_bloom);
            } catch {
                throw;
            }
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
            try {
                _bloom.Settings = BloomSettings.PresetSettings[_bloomSettings];
                _bloom.Visible = visible;
            } catch {
                throw;
            }
        }
        /// <summary>
        /// Draw
        /// </summary>
        public void Draw() {
            try {
                _bloom.BeginDraw();
            } catch {
                throw;
            }
        }
    }
}