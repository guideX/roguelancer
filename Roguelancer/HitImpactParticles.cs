using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;

namespace Roguelancer
{
    /// <summary>
    /// Particle system for weapon hit impact effects
    /// Creates small burst of particles when a weapon hits a target
    /// </summary>
    public class HitImpactParticles
    {
        private class Particle
        {
            public Vector3 Position;
            public Vector3 Velocity;
            public float Life;
            public float MaxLife;
            public float Size;
            public Color Color;
            public float Rotation;
            public float RotationSpeed;
        }

        private readonly List<Particle> _particles = new List<Particle>(200);
        private readonly GraphicsDevice _graphicsDevice;
        private readonly BasicEffect _effect;
        private readonly Texture2D _texture;
        private VertexPositionColorTexture[] _vertexBuffer = new VertexPositionColorTexture[0];
        private short[] _indexBuffer = new short[0];
        private Random _random = new Random();

        public HitImpactParticles(GraphicsDevice graphicsDevice)
        {
            _graphicsDevice = graphicsDevice;
            _texture = CreateParticleTexture(graphicsDevice, 16);

            _effect = new BasicEffect(graphicsDevice)
            {
                TextureEnabled = true,
                VertexColorEnabled = true,
                Texture = _texture,
                World = Matrix.Identity,
                LightingEnabled = false
            };
        }

        private Texture2D CreateParticleTexture(GraphicsDevice device, int size)
        {
            Texture2D tex = new Texture2D(device, size, size);
            Color[] data = new Color[size * size];
            Vector2 center = new Vector2(size / 2f, size / 2f);
            float maxDist = size / 2f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float d = Vector2.Distance(new Vector2(x, y), center) / maxDist;
                    float a = 1f - d;
                    a = (float)Math.Pow(a, 2); // Sharp falloff
                    data[y * size + x] = new Color(1f, 1f, 1f, MathHelper.Clamp(a, 0f, 1f));
                }
            }
            tex.SetData(data);
            return tex;
        }

        /// <summary>
        /// Trigger a hit impact effect at the specified position
        /// </summary>
        /// <param name="position">World position of impact</param>
        /// <param name="impactDirection">Direction of the hit (for particle spread)</param>
        /// <param name="color">Color of the impact (based on weapon type)</param>
        public void TriggerImpact(Vector3 position, Vector3 impactDirection, Color color)
        {
            int particleCount = 20; // Small burst of particles

            for (int i = 0; i < particleCount; i++)
            {
                Particle p = new Particle();
                p.Position = position;

                // Spread particles in a cone away from impact direction
                Vector3 randomDir = new Vector3(
                    (float)(_random.NextDouble() * 2 - 1),
                    (float)(_random.NextDouble() * 2 - 1),
                    (float)(_random.NextDouble() * 2 - 1)
                );
                randomDir.Normalize();

                // Bias toward impact direction
                Vector3 spreadDir = Vector3.Normalize(impactDirection + randomDir * 0.5f);
                float speed = (float)(_random.NextDouble() * 80 + 40);
                p.Velocity = spreadDir * speed;

                p.Life = 0f;
                p.MaxLife = (float)(_random.NextDouble() * 0.3 + 0.2); // Quick burst
                p.Size = (float)(_random.NextDouble() * 2 + 1);
                p.Color = color;
                p.Rotation = (float)(_random.NextDouble() * MathHelper.TwoPi);
                p.RotationSpeed = (float)(_random.NextDouble() * 4 - 2);

                _particles.Add(p);
            }
        }

        public void Update(float deltaTime)
        {
            for (int i = _particles.Count - 1; i >= 0; i--)
            {
                Particle p = _particles[i];
                p.Life += deltaTime;

                if (p.Life >= p.MaxLife)
                {
                    _particles.RemoveAt(i);
                    continue;
                }

                // Update position
                p.Position += p.Velocity * deltaTime;

                // Apply drag
                p.Velocity *= 0.9f;

                // Update rotation
                p.Rotation += p.RotationSpeed * deltaTime;

                // Fade out
                float lifeRatio = p.Life / p.MaxLife;
                float alpha = 1f - lifeRatio;
                p.Color = new Color(
                    (byte)(p.Color.R * alpha),
                    (byte)(p.Color.G * alpha),
                    (byte)(p.Color.B * alpha),
                    (byte)(255 * alpha)
                );
            }
        }

        public void Draw(Camera camera)
        {
            if (_particles.Count == 0) return;

            // Rebuild vertex/index buffers if needed
            int quadCount = _particles.Count;
            if (_vertexBuffer.Length < quadCount * 4)
            {
                _vertexBuffer = new VertexPositionColorTexture[quadCount * 4];
                _indexBuffer = new short[quadCount * 6];

                for (int i = 0; i < quadCount; i++)
                {
                    _indexBuffer[i * 6 + 0] = (short)(i * 4 + 0);
                    _indexBuffer[i * 6 + 1] = (short)(i * 4 + 1);
                    _indexBuffer[i * 6 + 2] = (short)(i * 4 + 2);
                    _indexBuffer[i * 6 + 3] = (short)(i * 4 + 0);
                    _indexBuffer[i * 6 + 4] = (short)(i * 4 + 2);
                    _indexBuffer[i * 6 + 5] = (short)(i * 4 + 3);
                }
            }

            // Build billboard quads for each particle
            Vector3 cameraPos = camera.Position;
            for (int i = 0; i < _particles.Count; i++)
            {
                Particle p = _particles[i];
                Vector3 toCamera = Vector3.Normalize(cameraPos - p.Position);
                Vector3 right = Vector3.Normalize(Vector3.Cross(Vector3.Up, toCamera));
                Vector3 up = Vector3.Cross(toCamera, right);

                // Apply rotation
                Matrix rotation = Matrix.CreateFromAxisAngle(toCamera, p.Rotation);
                right = Vector3.TransformNormal(right, rotation);
                up = Vector3.TransformNormal(up, rotation);

                Vector3 rightScaled = right * p.Size;
                Vector3 upScaled = up * p.Size;

                _vertexBuffer[i * 4 + 0] = new VertexPositionColorTexture(p.Position - rightScaled - upScaled, p.Color, new Vector2(0, 0));
                _vertexBuffer[i * 4 + 1] = new VertexPositionColorTexture(p.Position + rightScaled - upScaled, p.Color, new Vector2(1, 0));
                _vertexBuffer[i * 4 + 2] = new VertexPositionColorTexture(p.Position + rightScaled + upScaled, p.Color, new Vector2(1, 1));
                _vertexBuffer[i * 4 + 3] = new VertexPositionColorTexture(p.Position - rightScaled + upScaled, p.Color, new Vector2(0, 1));
            }

            // Save current render states
            var oldBlend = _graphicsDevice.BlendState;
            var oldDepth = _graphicsDevice.DepthStencilState;
            var oldRasterizer = _graphicsDevice.RasterizerState;

            // Set up for particle rendering
            _graphicsDevice.BlendState = BlendState.Additive;
            _graphicsDevice.DepthStencilState = DepthStencilState.DepthRead;
            _graphicsDevice.RasterizerState = RasterizerState.CullNone;

            _effect.View = camera.View;
            _effect.Projection = camera.Projection;

            foreach (EffectPass pass in _effect.CurrentTechnique.Passes)
            {
                pass.Apply();
                _graphicsDevice.DrawUserIndexedPrimitives(
                    PrimitiveType.TriangleList,
                    _vertexBuffer,
                    0,
                    _particles.Count * 4,
                    _indexBuffer,
                    0,
                    _particles.Count * 2
                );
            }

            // Restore render states
            _graphicsDevice.BlendState = oldBlend;
            _graphicsDevice.DepthStencilState = oldDepth;
            _graphicsDevice.RasterizerState = oldRasterizer;
        }

        public void Clear()
        {
            _particles.Clear();
        }
    }
}
