using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;

namespace Roguelancer
{
    /// <summary>
    /// Particle system for charge beam charging effect
    /// Emits energy particles from the ship while charging
    /// </summary>
    public class ChargeParticles
    {
        private class Particle
        {
            public Vector3 Position;
            public Vector3 Velocity;
            public float Life;
            public float MaxLife;
            public float Size;
            public Color Color;
        }

        private readonly List<Particle> _particles = new List<Particle>(200);
        private readonly GraphicsDevice _graphicsDevice;
        private readonly BasicEffect _effect;
        private readonly Texture2D _texture;
        private VertexPositionColorTexture[] _vertexBuffer = new VertexPositionColorTexture[0];
        private short[] _indexBuffer = new short[0];
        private Random _random = new Random();

        public ChargeParticles(GraphicsDevice graphicsDevice)
        {
            _graphicsDevice = graphicsDevice;
            _effect = new BasicEffect(graphicsDevice)
            {
                TextureEnabled = true,
                VertexColorEnabled = true,
                LightingEnabled = false
            };
            _texture = CreateParticleTexture(graphicsDevice, 16);
            _effect.Texture = _texture;
        }

        private Texture2D CreateParticleTexture(GraphicsDevice device, int size)
        {
            Texture2D tex = new Texture2D(device, size, size);
            Color[] data = new Color[size * size];
            Vector2 center = new Vector2(size / 2f);
            float maxDist = size / 2f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float d = Vector2.Distance(new Vector2(x, y), center) / maxDist;
                    
                    // Soft glowing particle
                    float a = 1f - d;
                    a = (float)Math.Pow(a, 2.5); // Medium falloff
                    
                    data[y * size + x] = new Color(1f, 1f, 1f, MathHelper.Clamp(a, 0f, 1f));
                }
            }
            tex.SetData(data);
            return tex;
        }

        public void EmitChargeParticles(Vector3 shipPosition, Matrix shipOrientation, float chargeProgress)
        {
            // More particles as charge increases
            int particlesToEmit = (int)(chargeProgress * 5f) + 1; // 1-6 particles per frame when charging

            for (int i = 0; i < particlesToEmit; i++)
            {
                // Emit from various points around the ship
                float angle = (float)_random.NextDouble() * MathHelper.TwoPi;
                float radius = 5f + (float)_random.NextDouble() * 3f;
                float height = ((float)_random.NextDouble() - 0.5f) * 4f;

                Vector3 localOffset = new Vector3(
                    (float)Math.Cos(angle) * radius,
                    height,
                    (float)Math.Sin(angle) * radius
                );

                Vector3 particlePos = Vector3.Transform(localOffset, shipOrientation) + shipPosition;

                // Particles move toward the front of the ship (where beam will fire)
                Vector3 shipForward = shipOrientation.Forward;
                Vector3 particleVelocity = shipForward * (50f + (float)_random.NextDouble() * 50f);
                
                // Add some random drift
                particleVelocity += new Vector3(
                    ((float)_random.NextDouble() - 0.5f) * 20f,
                    ((float)_random.NextDouble() - 0.5f) * 20f,
                    ((float)_random.NextDouble() - 0.5f) * 20f
                );

                // Color transitions from white to yellow as charge increases
                Color particleColor = Color.Lerp(Color.White, Color.Yellow, chargeProgress);

                Particle particle = new Particle
                {
                    Position = particlePos,
                    Velocity = particleVelocity,
                    Life = 0f,
                    MaxLife = 0.5f + (float)_random.NextDouble() * 0.5f, // 0.5-1.0 second life
                    Size = 5f + chargeProgress * 10f, // Grow with charge
                    Color = particleColor
                };

                _particles.Add(particle);
            }
        }

        public void Update(float deltaTime)
        {
            // Update and remove old particles
            for (int i = _particles.Count - 1; i >= 0; i--)
            {
                Particle p = _particles[i];
                p.Life += deltaTime;

                if (p.Life >= p.MaxLife)
                {
                    _particles.RemoveAt(i);
                    continue;
                }

                // Move particle
                p.Position += p.Velocity * deltaTime;

                // Slow down over time (drag effect)
                p.Velocity *= 0.95f;
            }
        }

        public void Draw(Camera camera)
        {
            int count = _particles.Count;
            if (count == 0) return;

            // Ensure buffers sized
            int requiredVertices = count * 4;
            int requiredIndices = count * 6;
            if (_vertexBuffer.Length < requiredVertices) _vertexBuffer = new VertexPositionColorTexture[requiredVertices];
            if (_indexBuffer.Length < requiredIndices) _indexBuffer = new short[requiredIndices];

            Vector3 camPos = camera.Position;

            // Build billboards
            for (int i = 0; i < count; i++)
            {
                Particle p = _particles[i];

                // Fade out over lifetime
                float t = p.Life / p.MaxLife;
                float alpha = 1f - t;
                Color color = p.Color * alpha;

                // Billboard facing camera
                Vector3 forward = camPos - p.Position;
                if (forward.LengthSquared() < 0.00001f) forward = Vector3.Forward;
                forward.Normalize();

                Vector3 right = Vector3.Cross(Vector3.Up, forward);
                if (right.LengthSquared() < 0.00001f) right = Vector3.Right;
                else right.Normalize();

                Vector3 up = Vector3.Cross(forward, right);

                float size = p.Size;

                // Build quad
                int vBase = i * 4;
                int iBase = i * 6;

                _vertexBuffer[vBase + 0] = new VertexPositionColorTexture(p.Position + (-right - up) * size, color, new Vector2(0, 1));
                _vertexBuffer[vBase + 1] = new VertexPositionColorTexture(p.Position + (right - up) * size, color, new Vector2(1, 1));
                _vertexBuffer[vBase + 2] = new VertexPositionColorTexture(p.Position + (-right + up) * size, color, new Vector2(0, 0));
                _vertexBuffer[vBase + 3] = new VertexPositionColorTexture(p.Position + (right + up) * size, color, new Vector2(1, 0));

                _indexBuffer[iBase + 0] = (short)(vBase + 0);
                _indexBuffer[iBase + 1] = (short)(vBase + 1);
                _indexBuffer[iBase + 2] = (short)(vBase + 2);
                _indexBuffer[iBase + 3] = (short)(vBase + 2);
                _indexBuffer[iBase + 4] = (short)(vBase + 1);
                _indexBuffer[iBase + 5] = (short)(vBase + 3);
            }

            // Save render states
            var oldBlend = _graphicsDevice.BlendState;
            var oldDepth = _graphicsDevice.DepthStencilState;
            var oldRaster = _graphicsDevice.RasterizerState;
            var oldSampler = _graphicsDevice.SamplerStates[0];

            // Additive blending for bright glow
            _graphicsDevice.BlendState = BlendState.Additive;
            _graphicsDevice.DepthStencilState = DepthStencilState.DepthRead;
            _graphicsDevice.RasterizerState = RasterizerState.CullNone;
            _graphicsDevice.SamplerStates[0] = SamplerState.LinearClamp;

            _effect.View = camera.View;
            _effect.Projection = camera.Projection;
            _effect.World = Matrix.Identity;

            foreach (EffectPass pass in _effect.CurrentTechnique.Passes)
            {
                pass.Apply();
                _graphicsDevice.DrawUserIndexedPrimitives(
                    PrimitiveType.TriangleList,
                    _vertexBuffer, 0, count * 4,
                    _indexBuffer, 0, count * 2
                );
            }

            // Restore render states
            _graphicsDevice.BlendState = oldBlend;
            _graphicsDevice.DepthStencilState = oldDepth;
            _graphicsDevice.RasterizerState = oldRaster;
            _graphicsDevice.SamplerStates[0] = oldSampler;
        }

        public void Clear()
        {
            _particles.Clear();
        }
    }
}
