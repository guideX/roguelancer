using System;
using System.IO;
using System.Text.Json;

namespace Roguelancer
{
    /// <summary>
    /// Game settings for online/offline mode
    /// </summary>
    public class GameSettings
    {
        public bool EnableOnlineMode { get; set; } = false;
        public string PlayerName { get; set; } = "Pilot";
        public string ServerUrl { get; set; } = "http://localhost:5000/gamehub";
        public bool AutoConnect { get; set; } = false;
        public int NetworkUpdateRate { get; set; } = 20; // Updates per second

        private static readonly string SettingsPath = "gamesettings.json";

        /// <summary>
        /// Load settings from file
        /// </summary>
        public static GameSettings Load()
        {
            try
            {
                if (File.Exists(SettingsPath))
                {
                    string json = File.ReadAllText(SettingsPath);
                    var settings = JsonSerializer.Deserialize<GameSettings>(json);
                    if (settings != null)
                    {
                        Console.WriteLine($"[SETTINGS] Loaded from {SettingsPath}");
                        Console.WriteLine($"[SETTINGS] Online Mode: {settings.EnableOnlineMode}");
                        Console.WriteLine($"[SETTINGS] Player Name: {settings.PlayerName}");
                        Console.WriteLine($"[SETTINGS] Server URL: {settings.ServerUrl}");
                        return settings;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SETTINGS] Failed to load settings: {ex.Message}");
            }

            Console.WriteLine("[SETTINGS] Using default settings (Offline Mode)");
            return new GameSettings();
        }

        /// <summary>
        /// Save settings to file
        /// </summary>
        public void Save()
        {
            try
            {
                var options = new JsonSerializerOptions { WriteIndented = true };
                string json = JsonSerializer.Serialize(this, options);
                File.WriteAllText(SettingsPath, json);
                Console.WriteLine($"[SETTINGS] Saved to {SettingsPath}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SETTINGS] Failed to save settings: {ex.Message}");
            }
        }

        /// <summary>
        /// Create a default settings file
        /// </summary>
        public static void CreateDefaultSettings()
        {
            var settings = new GameSettings
            {
                EnableOnlineMode = false,
                PlayerName = "Pilot",
                ServerUrl = "http://localhost:5000/gamehub",
                AutoConnect = false,
                NetworkUpdateRate = 20
            };
            settings.Save();
        }
    }
}
