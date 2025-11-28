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
        public float Scale { get; set; } = 100f;
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

        public Sun(GraphicsDevice graphicsDevice, Vector3 position, float scale = 100f) {
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
                // Create fallback textures if loading fails
                _surfaceTexture = new Texture2D(_graphicsDevice, 1, 1);
                _surfaceTexture.SetData(new[] { Color.Yellow });
                _coronaTexture = new Texture2D(_graphicsDevice, 1, 1);
                _coronaTexture.SetData(new[] { Color.Orange });
            }
        }

        public void Update(GameTime gameTime) {
            float deltaTime = (float)gameTime.ElapsedGameTime.TotalSeconds;
            _surfaceRotation += RotationSpeed * deltaTime;
            _coronaRotation -= (RotationSpeed * 0.5f) * deltaTime; // Rotate in opposite direction
        }

        public void Draw(Matrix view, Matrix projection) {
            if (_vertexBuffer == null || _surfaceTexture == null || _coronaTexture == null) return;

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

            foreach (var pass in _effect.CurrentTechnique.Passes) {
                pass.Apply();
                _graphicsDevice.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0, 2);
            }

            // --- Draw Surface (foreground layer) ---
            Matrix surfaceTransform = Matrix.CreateRotationZ(_surfaceRotation) * billboardWorld;
            _effect.World = surfaceTransform;
            _effect.Texture = _surfaceTexture;
            _effect.Alpha = 1.0f;

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
