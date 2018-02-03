namespace Roguelancer.Settings {
    /// <summary>
    /// Trade Lane Settings
    /// </summary>
    public class TradeLaneSettings {
        /// <summary>
        /// Tradelane Ini File
        /// </summary>
        public string tradelaneIniFile { get; set; }
        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="_tradelaneIniFile"></param>
        public TradeLaneSettings(string _tradelaneIniFile) {
            tradelaneIniFile = _tradelaneIniFile;
        }
    }
}