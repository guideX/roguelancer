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
        public float Scale { get; set; } = 2000f;
        public Color EmissiveColor { get; set; } = new Color(1.0f, 0.8f, 0.4f);
        public float EmissiveIntensity { get; set; } = 1.5f;

        // Rendering resources
        private GraphicsDevice _graphicsDevice;
        private Effect _sunEffect;
        private VertexBuffer _vertexBuffer;
        private IndexBuffer _indexBuffer;
        private Texture2D _noiseTexture;

        public Sun(GraphicsDevice graphicsDevice, Vector3 position, float scale = 2000f) {
            _graphicsDevice = graphicsDevice;
            Position = position;
            Scale = scale;

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
                _sunEffect = content.Load<Effect>("SunEffect");
                Console.WriteLine("SunEffect.fx loaded successfully!");
            } catch (Exception ex) {
                Console.WriteLine($"Error loading sun effect: {ex.Message}");
            }
            
            _noiseTexture = CreateNoiseTexture(256);
        }

        private Texture2D CreateNoiseTexture(int size)
        {
            Texture2D texture = new Texture2D(_graphicsDevice, size, size, false, SurfaceFormat.Color);
            Color[] data = new Color[size * size];
            Random random = new Random();

            for (int i = 0; i < data.Length; i++)
            {
                byte value = (byte)random.Next(256);
                data[i] = new Color(value, value, value, value);
            }

            texture.SetData(data);
            return texture;
        }

        public void Update(GameTime gameTime) {
            // Time is passed to the shader, no CPU-side rotation needed
        }

        public void Draw(Matrix view, Matrix projection) {
            if (_vertexBuffer == null) {
                Console.WriteLine("[WARNING] SUN DRAW SKIPPED: Missing vertex buffer!");
                return;
            }
            
            if (_sunEffect == null) {
                Console.WriteLine("[WARNING] SUN DRAW SKIPPED: Shader not loaded! Attempting fallback rendering...");
                // Don't skip - we should still try to render something
                return;
            }

            // Save current render states
            var oldBlendState = _graphicsDevice.BlendState;
            var oldDepthStencilState = _graphicsDevice.DepthStencilState;
            var oldRasterizerState = _graphicsDevice.RasterizerState;
            var oldSamplerState = _graphicsDevice.SamplerStates[0];

            try {
                _graphicsDevice.SetVertexBuffer(_vertexBuffer);
                _graphicsDevice.Indices = _indexBuffer;
                _graphicsDevice.BlendState = BlendState.Additive;
                _graphicsDevice.DepthStencilState = DepthStencilState.DepthRead;

                Matrix invView = Matrix.Invert(view);
                Vector3 cameraPosition = invView.Translation;

                Matrix billboardWorld = Matrix.CreateScale(Scale) * Matrix.CreateBillboard(Position, cameraPosition, Vector3.Up, invView.Forward);

                _sunEffect.Parameters["World"].SetValue(billboardWorld);
                _sunEffect.Parameters["View"].SetValue(view);
                _sunEffect.Parameters["Projection"].SetValue(projection);
                _sunEffect.Parameters["Time"]?.SetValue((float)DateTime.Now.TimeOfDay.TotalSeconds);
                _sunEffect.Parameters["CameraPosition"]?.SetValue(cameraPosition);
                _sunEffect.Parameters["NoiseTexture"]?.SetValue(_noiseTexture);
                _sunEffect.Parameters["SunColor"]?.SetValue(EmissiveColor.ToVector3());
                
                foreach (var pass in _sunEffect.CurrentTechnique.Passes) {
                    pass.Apply();
                    _graphicsDevice.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0, 2);
                }
            }
            catch (Exception ex) {
                Console.WriteLine($"[ERROR] Sun rendering failed: {ex.Message}");
            }
            finally {
                // Always restore render states
                _graphicsDevice.BlendState = oldBlendState;
                _graphicsDevice.DepthStencilState = oldDepthStencilState;
                _graphicsDevice.RasterizerState = oldRasterizerState;
                _graphicsDevice.SamplerStates[0] = oldSamplerState;
            }
        }

        public Vector3 GetLightDirection(Vector3 fromPosition) {
            return Vector3.Normalize(Position - fromPosition);
        }

        public float GetDistance(Vector3 fromPosition) {
            return Vector3.Distance(Position, fromPosition);
        }
    }
}
