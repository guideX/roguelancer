using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;

namespace Roguelancer {
    /// <summary>
    /// Represents a sun/star in the game world, rendered as a procedural billboard.
    /// </summary>
    public class Sun {
        public Vector3 Position { get; set; }
        public float Scale { get; set; } = 2000f; // Increased from 100f for proper visibility
        public Color EmissiveColor { get; set; } = Color.White;
        public float EmissiveIntensity { get; set; } = 1.5f;
        public float RotationSpeed { get; set; } = 0.1f;

        // Texture and rendering resources
        private GraphicsDevice _graphicsDevice;
        private BasicEffect _effect;
        private VertexBuffer _vertexBuffer;
        private IndexBuffer _indexBuffer;
        private Texture2D _surfaceTexture;
        private Texture2D _coronaTexture;
        private float _surfaceRotation = 0f;
        private float _coronaRotation = 0f;

        public Sun(GraphicsDevice graphicsDevice, Vector3 position, float scale = 2000f) {
            _graphicsDevice = graphicsDevice;
            Position = position;
            Scale = scale;

            _effect = new BasicEffect(_graphicsDevice);
            _effect.TextureEnabled = true;
            _effect.VertexColorEnabled = false; // Using texture color

            BuildQuad();
        }

        private void BuildQuad() {
            var vertices = new VertexPositionTexture[4];
            vertices[0] = new VertexPositionTexture(new Vector3(-1, 1, 0), new Vector2(0, 0));
            vertices[1] = new VertexPositionTexture(new Vector3(1, 1, 0), new Vector2(1, 0));
            vertices[2] = new VertexPositionTexture(new Vector3(-1, -1, 0), new Vector2(0, 1));
            vertices[3] = new VertexPositionTexture(new Vector3(1, -1, 0), new Vector2(1, 1));

            _vertexBuffer = new VertexBuffer(_graphicsDevice, typeof(VertexPositionTexture), 4, BufferUsage.WriteOnly);
            _vertexBuffer.SetData(vertices);

            var indices = new ushort[] { 0, 1, 2, 2, 1, 3 };
            _indexBuffer = new IndexBuffer(_graphicsDevice, typeof(ushort), 6, BufferUsage.WriteOnly);
            _indexBuffer.SetData(indices);
        }

        public void LoadContent(Microsoft.Xna.Framework.Content.ContentManager content) {
            try {
                // Using placeholder textures. Assumes you have these in your Content project.
                _surfaceTexture = content.Load<Texture2D>("SUN/surface");
                _coronaTexture = content.Load<Texture2D>("SUN/corona");
                Console.WriteLine("Procedural sun textures loaded successfully!");
            } catch (Exception ex) {
                Console.WriteLine($"Error loading sun textures: {ex.Message}");
                Console.WriteLine("Creating procedural fallback textures for sun...");
                // Create procedural fallback textures if loading fails
                _surfaceTexture = CreateSunSurfaceTexture(256);
                _coronaTexture = CreateCoronaTexture(256);
                Console.WriteLine("Fallback sun textures created successfully!");
            }
        }

        /// <summary>
        /// Create a procedural sun surface texture with radial gradient
        /// </summary>
        private Texture2D CreateSunSurfaceTexture(int size) {
            Texture2D texture = new Texture2D(_graphicsDevice, size, size);
            Color[] data = new Color[size * size];
            
            Vector2 center = new Vector2(size / 2f, size / 2f);
            float maxRadius = size / 2f;

            for (int y = 0; y < size; y++) {
                for (int x = 0; x < size; x++) {
                    Vector2 pos = new Vector2(x, y);
                    float distance = Vector2.Distance(pos, center);
                    float normalizedDistance = distance / maxRadius;

                    // Create radial gradient: bright center, fading to edges
                    if (normalizedDistance > 1.0f) {
                        data[y * size + x] = Color.Transparent;
                    } else {
                        // Smooth falloff using power curve
                        float intensity = 1.0f - (float)Math.Pow(normalizedDistance, 0.5);
                        
                        // Warm yellow-orange color
                        byte r = (byte)(255 * intensity);
                        byte g = (byte)(220 * intensity);
                        byte b = (byte)(100 * intensity);
                        byte a = (byte)(255 * intensity);
                        
                        data[y * size + x] = new Color(r, g, b, a);
                    }
                }
            }

            texture.SetData(data);
            return texture;
        }

        /// <summary>
        /// Create a procedural corona texture (outer glow)
        /// </summary>
        private Texture2D CreateCoronaTexture(int size) {
            Texture2D texture = new Texture2D(_graphicsDevice, size, size);
            Color[] data = new Color[size * size];
            
            Vector2 center = new Vector2(size / 2f, size / 2f);
            float maxRadius = size / 2f;

            for (int y = 0; y < size; y++) {
                for (int x = 0; x < size; x++) {
                    Vector2 pos = new Vector2(x, y);
                    float distance = Vector2.Distance(pos, center);
                    float normalizedDistance = distance / maxRadius;

                    // Create softer, larger glow
                    if (normalizedDistance > 1.0f) {
                        data[y * size + x] = Color.Transparent;
                    } else {
                        // Very soft falloff for corona glow
                        float intensity = 1.0f - (float)Math.Pow(normalizedDistance, 1.5);
                        
                        // Orange-red corona color
                        byte r = (byte)(255 * intensity);
                        byte g = (byte)(150 * intensity);
                        byte b = (byte)(50 * intensity);
                        byte a = (byte)(180 * intensity); // Semi-transparent
                        
                        data[y * size + x] = new Color(r, g, b, a);
                    }
                }
            }

            texture.SetData(data);
            return texture;
        }

        public void Update(GameTime gameTime) {
            float deltaTime = (float)gameTime.ElapsedGameTime.TotalSeconds;
            _surfaceRotation += RotationSpeed * deltaTime;
            _coronaRotation -= (RotationSpeed * 0.5f) * deltaTime; // Rotate in opposite direction
        }

        public void Draw(Matrix view, Matrix projection) {
            if (_vertexBuffer == null || _surfaceTexture == null || _coronaTexture == null) {
                Console.WriteLine("[WARNING] SUN DRAW SKIPPED: Missing resources!");
                return;
            }

            // Set up render states for additive blending
            _graphicsDevice.SetVertexBuffer(_vertexBuffer);
            _graphicsDevice.Indices = _indexBuffer;
            _graphicsDevice.BlendState = BlendState.Additive;
            _graphicsDevice.DepthStencilState = DepthStencilState.DepthRead; // Read depth but don't write

            // Extract camera position from view matrix
            Matrix invView = Matrix.Invert(view);
            Vector3 cameraPosition = invView.Translation;
            Vector3 cameraForward = invView.Forward;

            // Calculate billboard matrix to always face the camera
            Matrix billboardWorld = Matrix.CreateScale(Scale) * Matrix.CreateBillboard(Position, cameraPosition, Vector3.Up, cameraForward);

            // --- Draw Corona (background layer) ---
            Matrix coronaTransform = Matrix.CreateScale(1.5f) * Matrix.CreateRotationZ(_coronaRotation) * billboardWorld;
            _effect.World = coronaTransform;
            _effect.View = view;
            _effect.Projection = projection;
            _effect.Texture = _coronaTexture;
            _effect.Alpha = 0.6f; // Make corona slightly transparent
            _effect.DiffuseColor = EmissiveColor.ToVector3() * EmissiveIntensity;

            foreach (var pass in _effect.CurrentTechnique.Passes) {
                pass.Apply();
                _graphicsDevice.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0, 2);
            }

            // --- Draw Surface (foreground layer) ---
            Matrix surfaceTransform = Matrix.CreateRotationZ(_surfaceRotation) * billboardWorld;
            _effect.World = surfaceTransform;
            _effect.Texture = _surfaceTexture;
            _effect.Alpha = 1.0f;
            _effect.DiffuseColor = EmissiveColor.ToVector3() * EmissiveIntensity;

            foreach (var pass in _effect.CurrentTechnique.Passes) {
                pass.Apply();
                _graphicsDevice.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0, 2);
            }

            // Reset graphics device states
            _graphicsDevice.BlendState = BlendState.Opaque;
            _graphicsDevice.DepthStencilState = DepthStencilState.Default;
        }

        /// <summary>
        /// Get the direction from a point to the sun (for lighting calculations)
        /// </summary>
        public Vector3 GetLightDirection(Vector3 fromPosition) {
            return Vector3.Normalize(Position - fromPosition);
        }

        /// <summary>
        /// Get the distance from a point to the sun
        /// </summary>
        public float GetDistance(Vector3 fromPosition) {
            return Vector3.Distance(Position, fromPosition);
        }
    }
}
