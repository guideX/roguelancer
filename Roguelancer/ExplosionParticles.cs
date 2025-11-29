using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;

namespace Roguelancer
{
    /// <summary>
    /// Particle system for ship explosion effects
    /// Creates expanding debris, fire, and smoke particles when ships are destroyed
    /// </summary>
    public class ExplosionParticles
    {
        private class Particle
        {
            public Vector3 Position;
            public Vector3 Velocity;
            public float Life;
            public float MaxLife;
            public float Size;
            public float InitialSize;
            public Color Color;
            public float RotationSpeed;
            public float Rotation;
        }

        private readonly List<Particle> _particles = new List<Particle>(500);
        private readonly GraphicsDevice _graphicsDevice;
        private readonly BasicEffect _effect;
        private readonly Texture2D _texture;
        private VertexPositionColorTexture[] _vertexBuffer = new VertexPositionColorTexture[0];
        private short[] _indexBuffer = new short[0];
        private Random _random = new Random();

        public ExplosionParticles(GraphicsDevice graphicsDevice)
        {
            _graphicsDevice = graphicsDevice;
            _texture = CreateParticleTexture(graphicsDevice, 32);

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
                    // Softer falloff for explosion particles
                    float a = 1f - d;
                    a = (float)Math.Pow(a, 1.5); // Gentle falloff
                    data[y * size + x] = new Color(1f, 1f, 1f, MathHelper.Clamp(a, 0f, 1f));
                }
            }
            tex.SetData(data);
            return tex;
        }

        /// <summary>
        /// Trigger an explosion at the specified position
        /// </summary>
        /// <param name="position">World position of explosion</param>
        /// <param name="velocity">Velocity of destroyed ship (for momentum)</param>
        /// <param name="intensity">Scale of explosion (1.0 = normal ship, larger for bigger ships)</param>
        public void TriggerExplosion(Vector3 position, Vector3 velocity, float intensity = 1.0f)
        {
            int particleCount = (int)(150 * intensity); // More particles for bigger explosions
            
            Console.WriteLine($"?? EXPLOSION at {position}, {particleCount} particles, intensity {intensity}");

            // Create initial flash particles (bright, fast expansion)
            for (int i = 0; i < particleCount / 3; i++)
            {
                Particle p = new Particle();
                p.Position = position;
                
                // Random direction for debris
                Vector3 randomDir = new Vector3(
                    (float)(_random.NextDouble() * 2 - 1),
                    (float)(_random.NextDouble() * 2 - 1),
                    (float)(_random.NextDouble() * 2 - 1)
                );
                randomDir.Normalize();
                
                // Fast initial velocity with ship momentum
                float speed = (float)(_random.NextDouble() * 300 + 100) * intensity;
                p.Velocity = velocity + randomDir * speed;
                
                p.Life = 0f;
                p.MaxLife = (float)(_random.NextDouble() * 0.5 + 0.3); // Short-lived flash
                p.InitialSize = (float)(_random.NextDouble() * 3 + 2) * intensity;
                p.Size = p.InitialSize;
                p.Color = new Color(255, 255, 200); // Bright white-yellow
                p.RotationSpeed = (float)(_random.NextDouble() * 4 - 2);
                p.Rotation = (float)(_random.NextDouble() * MathHelper.TwoPi);
                
                _particles.Add(p);
            }

            // Create fire particles (orange/red, medium duration)
            for (int i = 0; i < particleCount / 3; i++)
            {
                Particle p = new Particle();
                p.Position = position + new Vector3(
                    (float)(_random.NextDouble() * 4 - 2),
                    (float)(_random.NextDouble() * 4 - 2),
                    (float)(_random.NextDouble() * 4 - 2)
                );
                
                Vector3 randomDir = new Vector3(
                    (float)(_random.NextDouble() * 2 - 1),
                    (float)(_random.NextDouble() * 2 - 1),
                    (float)(_random.NextDouble() * 2 - 1)
                );
                randomDir.Normalize();
                
                float speed = (float)(_random.NextDouble() * 200 + 50) * intensity;
                p.Velocity = velocity * 0.5f + randomDir * speed;
                
                p.Life = 0f;
                p.MaxLife = (float)(_random.NextDouble() * 1.0 + 0.8);
                p.InitialSize = (float)(_random.NextDouble() * 4 + 3) * intensity;
                p.Size = p.InitialSize;
                
                // Random fire colors
                if (_random.NextDouble() > 0.5)
                    p.Color = new Color(255, 140, 0); // Orange
                else
                    p.Color = new Color(255, 50, 0); // Red-orange
                
                p.RotationSpeed = (float)(_random.NextDouble() * 3 - 1.5);
                p.Rotation = (float)(_random.NextDouble() * MathHelper.TwoPi);
                
                _particles.Add(p);
            }

            // Create smoke/debris particles (dark, long-lasting)
            for (int i = 0; i < particleCount / 3; i++)
            {
                Particle p = new Particle();
                p.Position = position + new Vector3(
                    (float)(_random.NextDouble() * 6 - 3),
                    (float)(_random.NextDouble() * 6 - 3),
                    (float)(_random.NextDouble() * 6 - 3)
                );
                
                Vector3 randomDir = new Vector3(
                    (float)(_random.NextDouble() * 2 - 1),
                    (float)(_random.NextDouble() * 2 - 1),
                    (float)(_random.NextDouble() * 2 - 1)
                );
                randomDir.Normalize();
                
                float speed = (float)(_random.NextDouble() * 100 + 30) * intensity;
                p.Velocity = velocity * 0.3f + randomDir * speed;
                
                p.Life = 0f;
                p.MaxLife = (float)(_random.NextDouble() * 2.0 + 1.5); // Lingers longest
                p.InitialSize = (float)(_random.NextDouble() * 5 + 4) * intensity;
                p.Size = p.InitialSize;
                
                // Dark smoke/debris colors
                int gray = _random.Next(40, 80);
                p.Color = new Color(gray, gray, gray);
                
                p.RotationSpeed = (float)(_random.NextDouble() * 2 - 1);
                p.Rotation = (float)(_random.NextDouble() * MathHelper.TwoPi);
                
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
                
                // Apply drag/deceleration
                p.Velocity *= 0.96f;
                
                // Update rotation
                p.Rotation += p.RotationSpeed * deltaTime;

                // Fade out over lifetime
                float lifeRatio = p.Life / p.MaxLife;
                float alpha = 1f - lifeRatio;
                
                // Expand smoke particles over time
                p.Size = p.InitialSize * (1f + lifeRatio * 0.5f);
                
                // Fade color
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
            _graphicsDevice.BlendState = BlendState.Additive; // Additive blending for fire/explosion
            _graphicsDevice.DepthStencilState = DepthStencilState.DepthRead; // Read depth but don't write
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
