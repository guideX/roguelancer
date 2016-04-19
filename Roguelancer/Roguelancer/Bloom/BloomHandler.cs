// Roguelancer 0.1 Pre Alpha by Leon Aiossa
// http://www.team-nexgen.com
using Roguelancer.Interfaces;
using Roguelancer.Models.Bloom;
namespace Roguelancer.Bloom {
    /// <summary>
    /// Bloom Handler
    /// </summary>
    public class BloomHandler : IGame {
        /// <summary>
        /// Bloom Handler Model
        /// </summary>
        public BloomHandlerModel Model { get; set; }
        /// <summary>
        /// Bloom Handler
        /// </summary>
        /// <param name="game"></param>
        public BloomHandler(RoguelancerGame game) {
            if (game.Settings.BloomEnabled) {
                Model = new BloomHandlerModel();
                Model.Bloom = new BloomComponent(game);
            }
        }
        /// <summary>
        /// Initialize
        /// </summary>
        /// <param name="game"></param>
        public void Initialize(RoguelancerGame game) {
            if (game.Settings.BloomEnabled) {
                game.Components.Add(Model.Bloom);
            }
        }
        /// <summary>
        /// Load Content
        /// </summary>
        public void LoadContent(RoguelancerGame game) {
        }
        /// <summary>
        /// Update
        /// </summary>
        /// <param name="_BloomVisible"></param>
        public void Update(RoguelancerGame game) {
            if (game.Settings.BloomEnabled) {
                Model.Bloom.Model.Settings = BloomSettingsModel.PresetSettings[Model.BloomSettings];
                Model.Bloom.Visible = true;
            }
        }
        /// <summary>
        /// Draw
        /// </summary>
        public void Draw(RoguelancerGame game) {
            if (game.Settings.BloomEnabled) {
                Model.Bloom.BeginDraw();
            }
        }
    }
}