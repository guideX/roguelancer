using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using Roguelancer.Configuration;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace Roguelancer
{
    /// <summary>
    /// Station Manager
    /// </summary>
    public class StationManager
    {
        private readonly ContentManager _content;
        private readonly GraphicsDevice _graphicsDevice;
        private readonly List<Station> _stations = new List<Station>();
        private readonly Dictionary<string, Model> _loadedModels = new Dictionary<string, Model>();
        private Model _fallbackModel = null;

        /// <summary>
        /// Station Manager
        /// </summary>
        /// <param name="content"></param>
        /// <param name="graphicsDevice"></param>
        public StationManager(ContentManager content, GraphicsDevice graphicsDevice)
        {
            _content = content;
            _graphicsDevice = graphicsDevice;
        }

        /// <summary>
        /// Load Stations for a specific system index (1-based). Pass 0 to load all stations.
        /// </summary>
        public void LoadStations(int systemIndex = 0)
        {
            _stations.Clear();
            _loadedModels.Clear();

            var configPath = Path.Combine("Configuration", "stations");
            
            Console.WriteLine($"[STATION MANAGER] Loading stations from: {configPath} (system {systemIndex})");
            
            if (!Directory.Exists(configPath))
            {
                Console.WriteLine($"[STATION MANAGER] ERROR: Configuration directory not found: {configPath}");
                return;
            }

            var stationFiles = Directory.GetFiles(configPath, "station_*.json");
            Console.WriteLine($"[STATION MANAGER] Found {stationFiles.Length} station configuration files");

            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
            };

            foreach (var file in stationFiles)
            {
                Console.WriteLine($"[STATION MANAGER] Loading config: {Path.GetFileName(file)}");
                var jsonString = File.ReadAllText(file);
                var stationConfig = JsonSerializer.Deserialize<StationConfig>(jsonString, options);
                if (stationConfig != null)
                {
                    // Filter by system index if specified
                    if (systemIndex > 0 && stationConfig.SystemIndex != systemIndex)
                    {
                        continue;
                    }

                    Console.WriteLine($"[STATION MANAGER] Config loaded: {stationConfig.Description}");
                    Console.WriteLine($"[STATION MANAGER]   Model path: {stationConfig.ModelPath}");
                    Console.WriteLine($"[STATION MANAGER]   Position: {stationConfig.StartupPosition}");
                    Console.WriteLine($"[STATION MANAGER]   Radius: {stationConfig.Radius}");
                    
                    var model = LoadModelWithFallback(stationConfig.ModelPath, stationConfig.Description);
                    if (model != null)
                    {
                        _stations.Add(new Station(stationConfig, model));
                        Console.WriteLine($"[STATION MANAGER] ? Station created successfully!");
                    }
                    else
                    {
                        Console.WriteLine($"[STATION MANAGER] ? Failed to create station (no model)");
                    }
                }
                else
                {
                    Console.WriteLine($"[STATION MANAGER] ? Failed to deserialize config from: {file}");
                }
            }
            
            Console.WriteLine($"[STATION MANAGER] Loading complete. Total stations: {_stations.Count}");
        }

        private Model LoadModelWithFallback(string assetName, string stationDescription)
        {
            // Try to load the specified model first
            var model = LoadModel(assetName);
            if (model != null)
            {
                return model;
            }

            // If no model path or loading failed, use fallback
            Console.WriteLine($"[STATION MANAGER] Using fallback placeholder for: {stationDescription}");
            return GetFallbackModel();
        }

        private Model GetFallbackModel()
        {
            if (_fallbackModel != null)
            {
                return _fallbackModel;
            }

            // Try to load a simple model as fallback
            try
            {
                // Try loading the player ship model as fallback
                _fallbackModel = _content.Load<Model>("SHIPS/scimitar/Scimitar2");
                Console.WriteLine($"[STATION MANAGER] Using Scimitar model as station placeholder");
                return _fallbackModel;
            }
            catch
            {
                Console.WriteLine($"[STATION MANAGER] ? Could not load fallback model");
                return null;
            }
        }

        private Model LoadModel(string assetName)
        {
            if (string.IsNullOrEmpty(assetName))
            {
                return null;
            }
            
            if (_loadedModels.TryGetValue(assetName, out var model))
            {
                Console.WriteLine($"[STATION MANAGER] Using cached model: {assetName}");
                return model;
            }

            try
            {
                Console.WriteLine($"[STATION MANAGER] Attempting to load model: {assetName}");
                var newModel = _content.Load<Model>(assetName);
                _loadedModels[assetName] = newModel;
                Console.WriteLine($"[STATION MANAGER] ? Model loaded successfully: {assetName}");
                Console.WriteLine($"[STATION MANAGER]   Meshes: {newModel.Meshes.Count}, Bones: {newModel.Bones.Count}");
                return newModel;
            }
            catch (ContentLoadException ex)
            {
                Console.WriteLine($"[STATION MANAGER] ? Failed to load model '{assetName}': {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Get Stations
        /// </summary>
        /// <returns></returns>
        public List<Station> GetStations()
        {
            return _stations;
        }

        /// <summary>
        /// Update
        /// </summary>
        /// <param name="gameTime"></param>
        public void Update(GameTime gameTime)
        {
            // Update all stations (for rotation animation, etc.)
            foreach (var station in _stations)
            {
                station.Update(gameTime);
            }
        }

        /// <summary>
        /// Draw
        /// </summary>
        /// <param name="camera"></param>
        /// <param name="lightDirection"></param>
        public void Draw(Camera camera, Vector3 lightDirection)
        {
            foreach (var station in _stations)
            {
                station.Draw(camera.View, camera.Projection, lightDirection);
            }
        }
    }
}
