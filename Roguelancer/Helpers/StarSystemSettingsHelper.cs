using Roguelancer.Functionality;
using Roguelancer.Models;
using System.Collections.Generic;
namespace Roguelancer.Helpers {
    /// <summary>
    /// Star System Settings Helper
    /// </summary>
    public static class StarSystemSettingsHelper {
        /// <summary>
        /// Root Dir
        /// </summary>
        private static string _rootDir = System.IO.Directory.GetCurrentDirectory() + @"\..\..\..\";
        /// <summary>
        /// Get Star System
        /// </summary>
        /// <param name="starSystemID"></param>
        /// <returns></returns>
        public static StarSettingsModel GetStarSystem(int starSystemID) {
            return new StarSettingsModel(
                NativeMethods.ReadINIBool(_rootDir + @"configuration\systems\systems.ini", starSystemID.ToString(), "starsEnabled", false),
                NativeMethods.ReadINIInt(_rootDir + @"configuration\systems\systems.ini", starSystemID.ToString(), "amountOfStarsPerSheet", 0),
                NativeMethods.ReadINILong(_rootDir + @"configuration\systems\systems.ini", starSystemID.ToString(), "maxPositionX", 0),
                NativeMethods.ReadINILong(_rootDir + @"configuration\systems\systems.ini", starSystemID.ToString(), "maxPositionY", 0),
                NativeMethods.ReadINIInt(_rootDir + @"configuration\systems\systems.ini", starSystemID.ToString(), "maxSize", 0),
                NativeMethods.ReadINILong(_rootDir + @"configuration\systems\systems.ini", starSystemID.ToString(), "maxPositionIncrementY", 0),
                NativeMethods.ReadINILong(_rootDir + @"configuration\systems\systems.ini", starSystemID.ToString(), "maxPositionStartingY", 0),
                NativeMethods.ReadINILong(_rootDir + @"configuration\systems\systems.ini", starSystemID.ToString(), "numberOfStarSheets", 0),
                NativeMethods.ReadINI(_rootDir + @"configuration\systems\systems.ini", starSystemID.ToString(), "path", "")
            );
        }
        /// <summary>
        /// Get Planets
        /// </summary>
        /// <param name="starSystemID"></param>
        /// <returns></returns>
        public static List<WorldObjectModel> GetPlanets(int starSystemID) {
            var results = new List<WorldObjectModel>();
            var path = NativeMethods.ReadINI(_rootDir + @"configuration\systems\systems.ini", starSystemID.ToString(), "path", "");
            //Model.Planets.Add(WorldObjectsSettings.Read(i, modelSettings, systemIniStartPath + path + @"\planets.ini", i.ToString().Trim()));
            //var n = NativeMethods.ReadINIInt(_rootDir + path + @"\planets.ini", "settings", "count", 0);
            for (var i = 1; i < NativeMethods.ReadINIInt(_rootDir + path + @"\planets.ini", "settings", "count", 0) + 1; ++i) {

            }
            return results;
        }
    }
}