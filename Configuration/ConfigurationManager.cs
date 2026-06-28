using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Roguelancer;

namespace Roguelancer.Configuration {
    /// <summary>
    /// Manages loading and accessing game configuration from JSON files
    /// </summary>
    public class ConfigurationManager {
        private const string ConfigurationPath = "Configuration";
        private const string ModelsPath = "models";
        private const string ShipsPath = "ships";
        private const string SystemsPath = "systems";
        private const string StationsPath = "stations";
        private const string JumpHolesPath = "jumpholes";

        public List<ModelConfig> Models { get; private set; } = new();
        public List<ShipConfig> Ships { get; private set; } = new();
        public List<SystemConfig> Systems { get; private set; } = new();
        public List<StationConfig> Stations { get; private set; } = new();
        public List<JumpHoleConfig> JumpHoles { get; private set; } = new();

        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions {
            WriteIndented = true,
            PropertyNameCaseInsensitive = true,
            AllowTrailingCommas = true,
            ReadCommentHandling = JsonCommentHandling.Skip
        };

        /// <summary>
        /// Load all configuration files
        /// </summary>
        public void LoadAll() {
            Console.WriteLine("[CONFIG] Loading configuration files...");
            Console.WriteLine($"[CONFIG] Current Directory: {Directory.GetCurrentDirectory()}");
            Console.WriteLine($"[CONFIG] Looking for config path: {Path.GetFullPath(ConfigurationPath)}");

            LoadModels();
            LoadSystems();
            LoadShips();
            LoadStations();
            LoadJumpHoles();

            Console.WriteLine($"[CONFIG] Loaded {Models.Count} models, {Systems.Count} systems, {Ships.Count} ships, {Stations.Count} stations, {JumpHoles.Count} jump holes");
        }

        /// <summary>
        /// Load all model configurations
        /// </summary>
        private void LoadModels() {
            string modelsDir = Path.Combine(ConfigurationPath, ModelsPath);
            if (!Directory.Exists(modelsDir)) {
                Console.WriteLine($"[CONFIG] Models directory not found: {modelsDir}");
                return;
            }

            foreach (string file in Directory.GetFiles(modelsDir, "*.json")) {
                try {
                    string json = File.ReadAllText(file);
                    var model = JsonSerializer.Deserialize<ModelConfig>(json, JsonOptions);
                    if (model != null && model.Enabled) {
                        Models.Add(model);
                        Console.WriteLine($"[CONFIG] Loaded model: {model.Name} from {Path.GetFileName(file)}");
                    }
                } catch (Exception ex) {
                    Console.WriteLine($"[CONFIG] Error loading model {file}: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Load all system configurations
        /// </summary>
        private void LoadSystems() {
            string systemsDir = Path.Combine(ConfigurationPath, SystemsPath);
            if (!Directory.Exists(systemsDir)) {
                Console.WriteLine($"[CONFIG] Systems directory not found: {systemsDir}");
                return;
            }

            foreach (string file in Directory.GetFiles(systemsDir, "*.json")) {
                try {
                    string json = File.ReadAllText(file);
                    var system = JsonSerializer.Deserialize<SystemConfig>(json, JsonOptions);
                    if (system != null) {
                        NormalizeSystemConfig(system);
                        Systems.Add(system);
                        Console.WriteLine($"[CONFIG] Loaded system: {system.Description} from {Path.GetFileName(file)}");
                    }
                } catch (Exception ex) {
                    Console.WriteLine($"[CONFIG] Error loading system {file}: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Load all ship configurations
        /// </summary>
        private void LoadShips() {
            string shipsDir = Path.Combine(ConfigurationPath, ShipsPath);
            if (!Directory.Exists(shipsDir)) {
                Console.WriteLine($"[CONFIG] Ships directory not found: {shipsDir}");
                return;
            }

            foreach (string file in Directory.GetFiles(shipsDir, "*.json")) {
                try {
                    string json = File.ReadAllText(file);
                    var ship = JsonSerializer.Deserialize<ShipConfig>(json, JsonOptions);
                    if (ship != null) {
                        NormalizeShipConfig(ship);
                        Ships.Add(ship);
                        Console.WriteLine($"[CONFIG] Loaded ship: {ship.Description} from {Path.GetFileName(file)}");
                    }
                } catch (Exception ex) {
                    Console.WriteLine($"[CONFIG] Error loading ship {file}: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Load all station configurations
        /// </summary>
        private void LoadStations() {
            string stationsDir = Path.Combine(ConfigurationPath, StationsPath);
            if (!Directory.Exists(stationsDir)) {
                Console.WriteLine($"[CONFIG] Stations directory not found: {stationsDir}");
                return;
            }

            foreach (string file in Directory.GetFiles(stationsDir, "*.json")) {
                try {
                    string json = File.ReadAllText(file);
                    var station = JsonSerializer.Deserialize<StationConfig>(json, JsonOptions);
                    if (station != null) {
                        NormalizeStationConfig(station);
                        Stations.Add(station);
                        Console.WriteLine($"[CONFIG] Loaded station: {station.Description} from {Path.GetFileName(file)}");
                    }
                } catch (Exception ex) {
                    Console.WriteLine($"[CONFIG] Error loading station {file}: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Load all jump hole configurations
        /// </summary>
        private void LoadJumpHoles() {
            string jumpHolesDir = Path.Combine(ConfigurationPath, JumpHolesPath);
            if (!Directory.Exists(jumpHolesDir)) {
                Console.WriteLine($"[CONFIG] Jump holes directory not found: {jumpHolesDir}");
                return;
            }

            foreach (string file in Directory.GetFiles(jumpHolesDir, "*.json")) {
                try {
                    string json = File.ReadAllText(file);
                    var jumpHole = JsonSerializer.Deserialize<JumpHoleConfig>(json, JsonOptions);
                    if (jumpHole != null) {
                        JumpHoles.Add(jumpHole);
                        Console.WriteLine($"[CONFIG] Loaded jump hole: {jumpHole.Name} from {Path.GetFileName(file)}");
                    }
                } catch (Exception ex) {
                    Console.WriteLine($"[CONFIG] Error loading jump hole {file}: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Get a model by index (1-based)
        /// </summary>
        public ModelConfig GetModel(int index) {
            if (index < 1 || index > Models.Count) {
                Console.WriteLine($"[CONFIG] Invalid model index: {index}");
                return null;
            }
            return Models[index - 1];
        }

        /// <summary>
        /// Get a system by index (1-based)
        /// </summary>
        public SystemConfig GetSystem(int index) {
            if (index < 1 || index > Systems.Count) {
                Console.WriteLine($"[CONFIG] Invalid system index: {index}");
                return null;
            }
            return Systems[index - 1];
        }

        /// <summary>
        /// Get all ships for a specific system
        /// </summary>
        public List<ShipConfig> GetShipsForSystem(int systemIndex) {
            return Ships.FindAll(s => s.SystemIndex == systemIndex);
        }

        /// <summary>
        /// Get all stations for a specific system
        /// </summary>
        public List<StationConfig> GetStationsForSystem(int systemIndex) {
            return Stations.FindAll(s => s.SystemIndex == systemIndex);
        }

        /// <summary>
        /// Get all loaded ship configurations.
        /// </summary>
        /// <returns>A list of all ship configurations.</returns>
        public List<ShipConfig> GetAllShipConfigs()
        {
            return Ships;
        }

        /// <summary>
        /// Get all jump holes for a specific system
        /// </summary>
        public List<JumpHoleConfig> GetJumpHolesForSystem(int systemIndex) {
            return JumpHoles.FindAll(j => j.SystemIndex == systemIndex);
        }

        private static void NormalizeShipConfig(ShipConfig ship)
        {
            ship.FactionId = FactionManager.NormalizeFactionId(ship.FactionId);
        }

        private static void NormalizeStationConfig(StationConfig station)
        {
            station.FactionId = FactionManager.NormalizeFactionId(station.FactionId);
        }

        private static void NormalizeSystemConfig(SystemConfig system)
        {
            if (system.NpcPatrols == null)
            {
                system.NpcPatrols = new List<NpcPatrolConfig>();
                return;
            }

            foreach (var patrol in system.NpcPatrols)
            {
                if (patrol == null)
                {
                    continue;
                }

                patrol.FactionId = FactionManager.NormalizeFactionId(patrol.FactionId);
            }
        }
    }
}
