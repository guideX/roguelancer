// Roguelancer 0.1 Pre Alpha by Leon Aiossa
// http://www.team-nexgen.org
using System.Runtime.InteropServices;
using System.Text;
using System.Globalization;
using Microsoft.Xna.Framework;
namespace Roguelancer.Functionality {
    /// <summary>
    /// Ini File
    /// </summary>
    public static class IniFile {
        #region "private calls"
        /// <summary>
        /// Write Private Profile String
        /// </summary>
        /// <param name="section"></param>
        /// <param name="key"></param>
        /// <param name="val"></param>
        /// <param name="filePath"></param>
        /// <returns></returns>
        [DllImport("kernel32")]
        private static extern long WritePrivateProfileString(string section, string key, string val, string filePath);
        /// <summary>
        /// Get Private Profile String
        /// </summary>
        /// <param name="section"></param>
        /// <param name="key"></param>
        /// <param name="def"></param>
        /// <param name="retVal"></param>
        /// <param name="size"></param>
        /// <param name="filePath"></param>
        /// <returns></returns>
        [DllImport("kernel32")]
        private static extern int GetPrivateProfileString(string section, string key, string def, StringBuilder retVal, int size, string filePath);
        #endregion
        #region "public functions"
        /// <summary>
        /// Write Ini
        /// </summary>
        /// <param name="file"></param>
        /// <param name="section"></param>
        /// <param name="key"></param>
        /// <param name="value"></param>
        public static void WriteINI(string file, string section, string key, string value) {
            try {
                WritePrivateProfileString(section, key, value, file);
            } catch {
                throw;
            }
        }
        /// <summary>
        /// Read INI
        /// </summary>
        /// <param name="file"></param>
        /// <param name="section"></param>
        /// <param name="key"></param>
        /// <param name="def"></param>
        /// <returns></returns>
        public static string ReadINI(string file, string section, string key, string def = "") {
            try {
                var temp = new StringBuilder(255);
                var i = GetPrivateProfileString(section, key, "", temp, 255, file);
                var msg = temp.ToString();
                if (!string.IsNullOrEmpty(msg.Trim())) {
                    return msg;
                } else {
                    return def;
                }
            } catch {
                throw;
            }
        }
        /// <summary>
        /// Read Ini Float
        /// </summary>
        /// <param name="file"></param>
        /// <param name="section"></param>
        /// <param name="key"></param>
        /// <param name="def"></param>
        /// <returns></returns>
        public static float ReadINIFloat(string file, string section, string key, float def = 0.0f) {
            try {
                return float.Parse(ReadINI(file, section, key, def.ToString()), CultureInfo.InvariantCulture.NumberFormat);
            } catch {
                throw;
            }
        }
        /// <summary>
        /// Read Ini Vector3
        /// </summary>
        /// <param name="file"></param>
        /// <param name="section"></param>
        /// <param name="key1"></param>
        /// <param name="key2"></param>
        /// <param name="key3"></param>
        /// <param name="def"></param>
        /// <returns></returns>
        public static Vector3 ReadINIVector3(string file, string section, string key1, string key2, string key3, float def = 0.0f) {
            try {
                return new Vector3(ReadINIFloat(file, section, key1, def), ReadINIFloat(file, section, key2, def), ReadINIFloat(file, section, key3, def));
            } catch {
                throw;
            }
        }
        /// <summary>
        /// Read Ini Double
        /// </summary>
        /// <param name="file"></param>
        /// <param name="section"></param>
        /// <param name="key"></param>
        /// <param name="def"></param>
        /// <returns></returns>
        public static double ReadIniDouble(string file, string section, string key, double def = 0.0) {
            try {
                var msg = ReadINI(file, section, key, def.ToString());
                double result;
                double.TryParse(msg, out result);
                return result;
            } catch {
                throw;
            }
        }
        /// <summary>
        /// Read From Ini Float
        /// </summary>
        /// <param name="file"></param>
        /// <param name="section"></param>
        /// <param name="key"></param>
        /// <param name="def"></param>
        /// <returns></returns>
        public static float ReadIniFloat(string file, string section, string key, float def = 0.0f) {
            try {
                var msg = ReadINI(file, section, key, def.ToString());
                float result;
                float.TryParse(msg, out result);
                return result;
            } catch {
                throw;
            }
        }
        #endregion
    }
}