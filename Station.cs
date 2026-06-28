using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Roguelancer.Configuration;
using System;
using System.Linq;

namespace Roguelancer
{
    /// <summary>
    /// Represents a station space object.
    /// </summary>
    public class Station : SpaceObject
    {
        /// <summary>
        /// Model
        /// </summary>
        public Model Model { get; }

        /// <summary>
        /// Station Configuration
        /// </summary>
        public StationConfig Config { get; }

        /// <summary>
        /// Faction ownership for docking and reputation checks.
        /// </summary>
        public string FactionId { get; }

        /// <summary>
        /// Docking range - distance at which a ship can dock
        /// </summary>
        public float DockingRange { get; set; }

        /// <summary>
        /// Current rotation angle for animated rotation
        /// </summary>
        private float _currentRotationY = 0f;

        private bool _hasLoggedDrawAttempt = false;

        /// <summary>
        /// Station
        /// </summary>
        /// <param name="config"></param>
        /// <param name="model"></param>
        public Station(StationConfig config, Model model)
            : base(config.Description, config.StartupPosition, config.Radius)
        {
            Config = config;
            Model = model;
            FactionId = FactionManager.NormalizeFactionId(config.FactionId);
            DockingRange = config.DockingRange;
        }

        /// <summary>
        /// Get the world-space docking point for this station
        /// </summary>
        public Vector3 GetDockingPoint()
        {
            // Transform the relative docking point by the station's rotation and position
            Matrix rotation = Matrix.CreateRotationX(Config.StartupModelRotationX) *
                            Matrix.CreateRotationY(_currentRotationY) *
                            Matrix.CreateRotationZ(Config.StartupModelRotationZ);
            
            Vector3 scaledDockingPoint = Config.DockingPoint * Config.Scale;
            Vector3 rotatedPoint = Vector3.Transform(scaledDockingPoint, rotation);
            return Position + rotatedPoint;
        }

        /// <summary>
        /// Get the approach point (distance from docking point)
        /// </summary>
        public Vector3 GetApproachPoint()
        {
            Vector3 dockingPoint = GetDockingPoint();
            Vector3 directionToStation = Vector3.Normalize(Position - dockingPoint);
            return dockingPoint + directionToStation * Config.DockingApproachDistance;
        }

        /// <summary>
        /// Update station (handle rotation animation)
        /// </summary>
        public void Update(GameTime gameTime)
        {
            if (Config.RotationSpeed > 0)
            {
                float deltaTime = (float)gameTime.ElapsedGameTime.TotalSeconds;
                _currentRotationY += Config.RotationSpeed * deltaTime;
                
                // Wrap around at 2?
                if (_currentRotationY > MathHelper.TwoPi)
                {
                    _currentRotationY -= MathHelper.TwoPi;
                }
            }
        }

        /// <summary>
        /// Draw
        /// </summary>
        /// <param name="view"></param>
        /// <param name="projection"></param>
        /// <param name="lightDirection"></param>
        public void Draw(Matrix view, Matrix projection, Vector3 lightDirection)
        {
            if (Model == null)
            {
                if (!_hasLoggedDrawAttempt)
                {
                    Console.WriteLine($"[STATION DRAW] {Name} has no model!");
                    _hasLoggedDrawAttempt = true;
                }
                return;
            }

            if (!_hasLoggedDrawAttempt)
            {
                Console.WriteLine($"[STATION DRAW] Drawing {Name} at {Position}");
                Console.WriteLine($"[STATION DRAW] Model has {Model.Meshes.Count} meshes");
                Console.WriteLine($"[STATION DRAW] Radius: {Radius}");
                _hasLoggedDrawAttempt = true;
            }

            // Use configured scale for the station - stations should be HUGE!
            float stationScale = Config.Scale; // Use scale from config (default 10x)
            
            // Apply animated rotation if rotation speed is set
            float rotationY = Config.RotationSpeed > 0 ? _currentRotationY : Config.StartupModelRotationY;
            
            var world = Matrix.CreateScale(stationScale) *
                        Matrix.CreateRotationX(Config.StartupModelRotationX) *
                        Matrix.CreateRotationY(rotationY) *
                        Matrix.CreateRotationZ(Config.StartupModelRotationZ) *
                        Matrix.CreateTranslation(Position);

            foreach (var mesh in Model.Meshes)
            {
                // Check if the mesh has texture coordinates
                bool hasTextureCoordinates = mesh.MeshParts.Any(part =>
                    part.VertexBuffer.VertexDeclaration.GetVertexElements().Any(element =>
                        element.VertexElementUsage == VertexElementUsage.TextureCoordinate && element.UsageIndex == 0
                    )
                );

                foreach (BasicEffect effect in mesh.Effects)
                {
                    effect.World = world;
                    effect.View = view;
                    effect.Projection = projection;
                    
                    // Enable textures if the mesh has texture coordinates
                    if (hasTextureCoordinates && effect.Texture != null)
                    {
                        effect.TextureEnabled = true;
                    }
                    else
                    {
                        effect.TextureEnabled = false;
                        effect.DiffuseColor = new Vector3(0.7f, 0.7f, 0.7f); // Fallback color if no texture
                    }
                    
                    effect.EnableDefaultLighting();
                    effect.PreferPerPixelLighting = true;
                    effect.DirectionalLight0.Enabled = true;
                    effect.DirectionalLight0.Direction = lightDirection;
                    effect.DirectionalLight0.DiffuseColor = Vector3.One;
                    effect.DirectionalLight0.SpecularColor = new Vector3(0.25f, 0.25f, 0.25f);
                    effect.SpecularPower = 16f;
                    effect.AmbientLightColor = new Vector3(0.2f, 0.2f, 0.2f); // Add some ambient light
                }
                mesh.Draw();
            }
        }
    }
}
