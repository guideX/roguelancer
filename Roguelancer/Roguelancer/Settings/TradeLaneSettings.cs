// Roguelancer 0.1 Pre Alpha by Leon Aiossa
// http://www.team-nexgen.org
namespace Roguelancer.Settings {
    public class TradeLaneSettings {
        public string tradelaneIniFile { get; set; }
        public TradeLaneSettings(string _tradelaneIniFile) {
            tradelaneIniFile = _tradelaneIniFile;
        }
    }
}