// Roguelancer 0.1 Pre Alpha by Leon Aiossa
// http://www.team-nexgen.org
public class clsBloomSettings {
    public readonly string Name;
    public readonly float BloomThreshold;
    public readonly float BlurAmount;
    public readonly float BloomIntensity;
    public readonly float BaseIntensity;
    public readonly float BloomSaturation;
    public readonly float BaseSaturation;
    public clsBloomSettings(string name, float bloomThreshold, float blurAmount, float bloomIntensity, float baseIntensity, float bloomSaturation, float baseSaturation) {
        Name = name;
        BloomThreshold = bloomThreshold;
        BlurAmount = blurAmount;
        BloomIntensity = bloomIntensity;
        BaseIntensity = baseIntensity;
        BloomSaturation = bloomSaturation;
        BaseSaturation = baseSaturation;
    }
    public static clsBloomSettings[] PresetSettings = {
        //                Name           Thresh  Blur Bloom  Base  BloomSat BaseSat
        new clsBloomSettings("Default",     0.25f,  4,   1.25f, 1,    1,       1),
        new clsBloomSettings("Soft",        0,      3,   1,     1,    1,       1),
        new clsBloomSettings("Desaturated", 0.5f,   8,   2,     1,    0,       1),
        new clsBloomSettings("Saturated",   0.25f,  4,   2,     1,    2,       0),
        new clsBloomSettings("Blurry",      0,      2,   1,     0.1f, 1,       1),
        new clsBloomSettings("Subtle",      0.5f,   2,   1,     1,    1,       1),
    };
}
