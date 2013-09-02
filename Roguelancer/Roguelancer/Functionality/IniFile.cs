// Roguelancer 0.1 Pre Alpha by Leon Aiossa
// http://www.team-nexgen.org
using System;
using System.Runtime.InteropServices;
using System.Text;
using System.Globalization;
using Microsoft.Xna.Framework;
namespace Roguelancer.Functionality {
    public static class IniFile {
        [DllImport("kernel32")]
        private static extern long WritePrivateProfileString(string section, string key,string val,string filePath);
        [DllImport("kernel32")]
        private static extern int GetPrivateProfileString(string section, string key, string def, StringBuilder retVal, int size,string filePath);
        public static void WriteINI(string file, string section, string key, string value) {
            WritePrivateProfileString(section, key, value, file);
        }
        public static string ReadINI(string file, string section, string key, string def = "") {
            StringBuilder temp = new StringBuilder(255);
            int i = GetPrivateProfileString(section, key, "", temp, 255, file);
            string msg = temp.ToString();
            if(!string.IsNullOrEmpty(msg.Trim())) {
                return msg;
            } else {
                return def;
            }
        }
        public static float ReadINIFloat(string file, string section, string key, float def = 0.0f) {
            return float.Parse(ReadINI(file, section, key, def.ToString()), CultureInfo.InvariantCulture.NumberFormat);
        }
        public static Vector3 ReadINIVector3(string file, string section, string key1, string key2, string key3, float def = 0.0f) {
            return new Vector3(ReadINIFloat(file, section, key1, def), ReadINIFloat(file, section, key2, def), ReadINIFloat(file, section, key3, def));
        }
    }
}