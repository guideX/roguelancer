// Roguelancer 0.1 Pre Alpha by Leon Aiossa
// http://team-nexgen.com
namespace Roguelancer.Models {
    /// <summary>
    /// Bloom Settings Model
    /// </summary>
    public class BloomSettingsModel {
        /// <summary>
        /// Name
        /// </summary>
        public string Name { get; set; }
        /// <summary>
        /// Bloom Threashold
        /// </summary>
        public float BloomThreshold { get; set; }
        /// <summary>
        /// Blur Amount
        /// </summary>
        public float BlurAmount { get; set; }
        /// <summary>
        /// Bloom Intensity
        /// </summary>
        public float BloomIntensity { get; set; }
        /// <summary>
        /// Base Intentisty
        /// </summary>
        public float BaseIntensity { get; set; }
        /// <summary>
        /// Bloom Saturation
        /// </summary>
        public float BloomSaturation { get; set; }
        /// <summary>
        /// Base Saturation
        /// </summary>
        public float BaseSaturation { get; set; }
        /// <summary>
        /// Bloom Settings Model
        /// </summary>
        /// <param name="name"></param>
        /// <param name="bloomThreshold"></param>
        /// <param name="blurAmount"></param>
        /// <param name="bloomIntensity"></param>
        /// <param name="baseIntensity"></param>
        /// <param name="bloomSaturation"></param>
        /// <param name="baseSaturation"></param>
        public BloomSettingsModel(string name, float bloomThreshold, float blurAmount, float bloomIntensity, float baseIntensity, float bloomSaturation, float baseSaturation) {
            Name = name;
            BloomThreshold = bloomThreshold;
            BlurAmount = blurAmount;
            BloomIntensity = bloomIntensity;
            BaseIntensity = baseIntensity;
            BloomSaturation = bloomSaturation;
            BaseSaturation = baseSaturation;
        }
        /// <summary>
        /// Preset Settings
        /// </summary>
        public static BloomSettingsModel[] PresetSettings = {
        //                Name           Thresh  Blur Bloom  Base  BloomSat BaseSat
        new BloomSettingsModel("Default",     0.25f,  4,   1.25f, 1,    1,       1),
        new BloomSettingsModel("Soft",        0,      3,   1,     1,    1,       1),
        new BloomSettingsModel("Desaturated", 0.5f,   8,   2,     1,    0,       1),
        new BloomSettingsModel("Saturated",   0.25f,  4,   2,     1,    2,       0),
        new BloomSettingsModel("Blurry",      0,      2,   1,     0.1f, 1,       1),
        new BloomSettingsModel("Subtle",      0.5f,   2,   1,     1,    1,       1),
    };
    }
}