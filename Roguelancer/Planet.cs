using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Linq;

namespace Roguelancer
{
    /// <summary>
    /// Represents a planet space object.
    /// </summary>
    public class Planet : SpaceObject
    {
        public Model SurfaceModel { get; }
        public Model CloudsModel { get; }
        public Model AtmosphereModel { get; }

        public Texture2D SurfaceTexture { get; set; }
        public Texture2D CloudTexture { get; set; }
        public Texture2D AtmosphereTexture { get; set; }
        public float OrbitRadius { get; set; }
        public float OrbitSpeed { get; set; }

        private float _cloudRotation = 0f;
        private readonly float _cloudRotationSpeed;

        public Planet(
            string name,
            Vector3 position,
            float radius,
            Model surfaceModel,
            Model cloudsModel,
            Model atmosphereModel)
            : base(name, position, radius)
        {
            SurfaceModel = surfaceModel;
            CloudsModel = cloudsModel;
            AtmosphereModel = atmosphereModel;

            _cloudRotationSpeed = 0.005f; // Slower rotation for clouds
        }

        public void Update(GameTime gameTime)
        {
            float deltaTime = (float)gameTime.ElapsedGameTime.TotalSeconds;
            _cloudRotation += _cloudRotationSpeed * deltaTime;
        }

        public void Draw(Matrix view, Matrix projection, Vector3 lightDirection, Vector3 cameraPosition)
        {
            DrawSurface(view, projection, lightDirection);
            DrawClouds(view, projection, cameraPosition);
            DrawAtmosphere(view, projection, cameraPosition);
        }

        private void DrawSurface(Matrix view, Matrix projection, Vector3 lightDirection)
        {
            if (SurfaceModel == null) return;

            Matrix world = Matrix.CreateScale(Radius) * Matrix.CreateTranslation(Position);

            foreach (ModelMesh mesh in SurfaceModel.Meshes)
            {
                // FIX: Check if the mesh has texture coordinates before enabling texturing to prevent crashes.
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

                    if (hasTextureCoordinates && SurfaceTexture != null)
                    {
                        effect.TextureEnabled = true;
                        effect.Texture = SurfaceTexture;
                    }
                    else
                    {
                        effect.TextureEnabled = false;
                    }

                    effect.EnableDefaultLighting();
                    effect.PreferPerPixelLighting = true;
                    effect.DirectionalLight0.Enabled = true;
                    effect.DirectionalLight0.Direction = lightDirection;
                    effect.DirectionalLight0.DiffuseColor = Vector3.One;
                    effect.AmbientLightColor = new Vector3(0.1f, 0.1f, 0.1f);
                }
                mesh.Draw();
            }
        }

        private void DrawClouds(Matrix view, Matrix projection, Vector3 cameraPosition)
        {
            if (CloudsModel == null) return;

            float cloudLayerScale = Radius * 1.02f; // Clouds are slightly above the surface
            Matrix world = Matrix.CreateScale(cloudLayerScale) * Matrix.CreateRotationY(_cloudRotation) * Matrix.CreateTranslation(Position);

            var graphicsDevice = CloudsModel.Meshes[0].Effects[0].GraphicsDevice;
            var oldBlendState = graphicsDevice.BlendState;
            graphicsDevice.BlendState = BlendState.AlphaBlend;

            foreach (ModelMesh mesh in CloudsModel.Meshes)
            {
                // FIX: Check for texture coordinates on the cloud model.
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
                    
                    if (hasTextureCoordinates && CloudTexture != null)
                    {
                        effect.TextureEnabled = true;
                        effect.Texture = CloudTexture;
                    }
                    else
                    {
                        effect.TextureEnabled = false;
                    }

                    effect.Alpha = 0.7f;
                }
                mesh.Draw();
            }

            graphicsDevice.BlendState = oldBlendState;
        }

        private void DrawAtmosphere(Matrix view, Matrix projection, Vector3 cameraPosition)
        {
            if (AtmosphereModel == null) return;

            float atmosphereScale = Radius * 1.05f;
            Matrix world = Matrix.CreateScale(atmosphereScale) * Matrix.CreateTranslation(Position);

            var graphicsDevice = AtmosphereModel.Meshes[0].Effects[0].GraphicsDevice;
            var oldBlendState = graphicsDevice.BlendState;
            var oldDepthState = graphicsDevice.DepthStencilState;

            graphicsDevice.BlendState = BlendState.AlphaBlend;
            graphicsDevice.DepthStencilState = DepthStencilState.DepthRead;

            foreach (ModelMesh mesh in AtmosphereModel.Meshes)
            {
                // FIX: Check for texture coordinates on the atmosphere model.
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

                    if (hasTextureCoordinates && AtmosphereTexture != null)
                    {
                        effect.TextureEnabled = true;
                        effect.Texture = AtmosphereTexture;
                    }
                    else
                    {
                        effect.TextureEnabled = false;
                    }

                    // Rim lighting effect for atmosphere
                    // FIX: Correct rim lighting calculation. The original calculation was flawed.
                    // We need to find the normal of the point on the sphere closest to the camera
                    // and compare it with the direction to the camera.
                    Vector3 surfaceNormal = Vector3.Normalize(Position - cameraPosition); // A simplified normal pointing from camera to planet center
                    Vector3 toCamera = Vector3.Normalize(cameraPosition - Position);
                    float rim = 1.0f - Math.Max(0, Vector3.Dot(surfaceNormal, -toCamera)); // Use the angle between surface normal and camera direction
                    effect.Alpha = MathHelper.Clamp((float)Math.Pow(rim, 3.0) * 0.9f, 0.1f, 0.9f);
                }
                mesh.Draw();
            }

            graphicsDevice.BlendState = oldBlendState;
            graphicsDevice.DepthStencilState = oldDepthState;
        }
    }
}
