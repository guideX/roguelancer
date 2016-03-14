// Roguelancer 0.1 Pre Alpha by Leon Aiossa
// http://www.team-nexgen.com
using Roguelancer.Models.Bloom;
namespace Roguelancer.Bloom {
    /// <summary>
    /// Bloom Handler
    /// </summary>
    public class BloomHandler {
        /// <summary>
        /// Bloom Handler Model
        /// </summary>
        public BloomHandlerModel Model { get; set; }
        /// <summary>
        /// Bloom Handler
        /// </summary>
        /// <param name="game"></param>
        public BloomHandler(RoguelancerGame game) {
            Model = new BloomHandlerModel();
            Model.Bloom = new BloomComponent(game);
        }
        /// <summary>
        /// Initialize
        /// </summary>
        /// <param name="game"></param>
        public void Initialize(RoguelancerGame game) {
            game.Components.Add(Model.Bloom);
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
            Model.Bloom.Model.Settings = BloomSettingsModel.PresetSettings[Model.BloomSettings];
            Model.Bloom.Visible = visible;
        }
        /// <summary>
        /// Draw
        /// </summary>
        public void Draw() {
            Model.Bloom.BeginDraw();
        }
    }
}