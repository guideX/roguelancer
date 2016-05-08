// Roguelancer 0.1 Pre Alpha by Leon Aiossa
// http://team-nexgen.com
namespace Roguelancer.Settings {
    /// <summary>
    /// Jump Hole Settings
    /// </summary>
    public class JumpHoleSettings {
        /// <summary>
        /// Jump Hole Ini File
        /// </summary>
        public string JumpHoleIniFile { get; set; }
        /// <summary>
        /// Jump Hole Settings
        /// </summary>
        /// <param name="jumpHoleIniFile"></param>
        public JumpHoleSettings(string jumpHoleIniFile) {
            JumpHoleIniFile = jumpHoleIniFile;
        }
    }
}