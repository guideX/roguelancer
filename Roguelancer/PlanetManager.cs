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
    /// Planet Manager
    /// </summary>
    public class PlanetManager
    {
        #region "private members"

        private readonly ContentManager _content;
        private readonly GraphicsDevice _graphicsDevice;
        private readonly List<Planet> _planets = new List<Planet>();
        private readonly Dictionary<string, Model> _loadedModels = new Dictionary<string, Model>();
        private float _totalTime = 0f;

        #endregion

        #region "public properties"

        #endregion

        #region "constructor"

        /// <summary>
        /// Planet Manager
        /// </summary>
        /// <param name="content"></param>
        /// <param name="graphicsDevice"></param>
        public PlanetManager(ContentManager content, GraphicsDevice graphicsDevice)
        {
            _content = content;
            _graphicsDevice = graphicsDevice;
        }

        #endregion

        #region "methods"

        /// <summary>
        /// Loads a planetary system from a JSON configuration file.
        /// </summary>
        /// <param name="systemFileName">The name of the system file (e.g., "sol-system.json").</param>
        public void LoadSystem(string systemFileName)
        {
            _planets.Clear();
            _loadedModels.Clear();

            var configPath = Path.Combine("Configuration", "Systems", systemFileName);
            var jsonString = File.ReadAllText(configPath);
            
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
            };
            var systemConfig = JsonSerializer.Deserialize<SystemConfiguration>(jsonString, options);

            foreach (var planetConfig in systemConfig.Planets)
            {
                var surfaceModel = LoadModel(planetConfig.SurfaceModelAsset);
                var cloudsModel = LoadModel(planetConfig.CloudsModelAsset);
                var atmosphereModel = LoadModel(planetConfig.AtmosphereModelAsset);

                var surfaceTexture = LoadTexture(planetConfig.SurfaceTextureAsset);
                var cloudsTexture = LoadTexture(planetConfig.CloudsTextureAsset);
                var atmosphereTexture = LoadTexture(planetConfig.AtmosphereTextureAsset);

                var planet = new Planet(
                    planetConfig.Name,
                    Vector3.Zero, // Initial position, will be updated by orbit
                    planetConfig.Radius,
                    surfaceModel,
                    cloudsModel,
                    atmosphereModel)
                {
                    SurfaceTexture = surfaceTexture,
                    CloudTexture = cloudsTexture,
                    AtmosphereTexture = atmosphereTexture,
                    OrbitRadius = planetConfig.OrbitRadius,
                    OrbitSpeed = planetConfig.OrbitSpeed
                };
                _planets.Add(planet);
            }
        }

        private Model LoadModel(string assetName)
        {
            if (string.IsNullOrEmpty(assetName)) return null;
            if (_loadedModels.TryGetValue(assetName, out var model))
            {
                return model;
            }

            try
            {
                var newModel = _content.Load<Model>(assetName);
                _loadedModels[assetName] = newModel;
                return newModel;
            }
            catch (ContentLoadException ex)
            {
                Console.WriteLine($"Failed to load model '{assetName}': {ex.Message}");
                return null;
            }
        }

        private Texture2D LoadTexture(string assetName)
        {
            if (string.IsNullOrEmpty(assetName)) return null;
            try
            {
                return _content.Load<Texture2D>(assetName);
            }
            catch (ContentLoadException ex)
            {
                Console.WriteLine($"Failed to load texture '{assetName}': {ex.Message}")
;
                return null;
            }
        }

        /// <summary>
        /// Get Planets
        /// </summary>
        /// <returns></returns>
        public List<Planet> GetPlanets()
        {
            return _planets;
        }

        /// <summary>
        /// Update
        /// </summary>
        /// <param name="gameTime"></param>
        public void Update(GameTime gameTime)
        {
            _totalTime += (float)gameTime.ElapsedGameTime.TotalSeconds;

            foreach (var planet in _planets)
            {
                // Update planet's orbital position
                var orbitX = (float)Math.Cos(_totalTime * planet.OrbitSpeed) * planet.OrbitRadius;
                var orbitZ = (float)Math.Sin(_totalTime * planet.OrbitSpeed) * planet.OrbitRadius;
                planet.Position = new Vector3(orbitX, planet.Position.Y, orbitZ);

                planet.Update(gameTime);
            }
        }

        /// <summary>
        /// Draw
        /// </summary>
        /// <param name="camera"></param>
        /// <param name="lightDirection"></param>
        public void Draw(Camera camera, Vector3 lightDirection)
        {
            foreach (var planet in _planets)
            {
                planet.Draw(camera.View, camera.Projection, lightDirection, camera.Position);
            }
        }

        #endregion
    }
}
