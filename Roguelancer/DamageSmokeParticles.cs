using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;

namespace Roguelancer
{
    /// <summary>
    /// Damage Stage
    /// </summary>
    public enum DamageStage
    {
        /// <summary>
        /// None
        /// </summary>
        None,
        /// <summary>
        /// Light
        /// </summary>
        Light,
        /// <summary>
        /// Heavy
        /// </summary>
        Heavy,
        /// <summary>
        /// Critical
        /// </summary>
        Critical
    }

    /// <summary>
    /// Particle system for damage smoke effects
    /// </summary>
    public class DamageSmokeParticles
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
            public float ExpansionFactor;
        }

        private readonly List<Particle> _particles = new List<Particle>(1000);
        private readonly GraphicsDevice _graphicsDevice;
        private readonly BasicEffect _effect;
        private readonly Texture2D _texture;
        private VertexPositionColorTexture[] _vertexBuffer = new VertexPositionColorTexture[0];
        private short[] _indexBuffer = new short[0];
        private Random _random = new Random();

        public DamageSmokeParticles(GraphicsDevice graphicsDevice)
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
                    float a = 1f - d * d; // Quadratic falloff for softer smoke
                    data[y * size + x] = new Color(1f, 1f, 1f, MathHelper.Clamp(a, 0f, 1f));
                }
            }
            tex.SetData(data);
            return tex;
        }

        public void Emit(Vector3 position, Vector3 shipVelocity, DamageStage stage)
        {
            if (stage == DamageStage.None) return;

            int particleCount = 0;
            float maxLifeMin = 0, maxLifeMax = 0;
            float initialSizeMin = 0, initialSizeMax = 0;
            int colorMin = 0, colorMax = 0;
            float expansionFactor = 0;

            switch (stage)
            {
                case DamageStage.Light:
                    particleCount = _random.Next(1, 3);
                    maxLifeMin = 1.0f; maxLifeMax = 2.0f;
                    initialSizeMin = 1f; initialSizeMax = 2f;
                    colorMin = 80; colorMax = 120;
                    expansionFactor = 1.0f;
                    break;
                case DamageStage.Heavy:
                    particleCount = _random.Next(3, 6);
                    maxLifeMin = 2.0f; maxLifeMax = 3.0f;
                    initialSizeMin = 2f; initialSizeMax = 3f;
                    colorMin = 40; colorMax = 80;
                    expansionFactor = 1.5f;
                    break;
                case DamageStage.Critical:
                    particleCount = _random.Next(6, 12);
                    maxLifeMin = 3.0f; maxLifeMax = 4.0f;
                    initialSizeMin = 4f; initialSizeMax = 5f;
                    colorMin = 20; colorMax = 60;
                    expansionFactor = 2.5f;
                    break;
            }

            for (int i = 0; i < particleCount; i++)
            {
                if (_particles.Count >= 1000) return;

                Particle p = new Particle();
                p.Position = position;

                Vector3 randomDir = new Vector3(
                    (float)(_random.NextDouble() * 2 - 1),
                    (float)(_random.NextDouble() * 2 - 1),
                    (float)(_random.NextDouble() * 2 - 1)
                );
                randomDir.Normalize();

                float speed = (float)(_random.NextDouble() * 10 + 5);
                p.Velocity = shipVelocity * 0.5f + randomDir * speed;

                p.Life = 0f;
                p.MaxLife = (float)(_random.NextDouble() * maxLifeMax + maxLifeMin);
                p.InitialSize = (float)(_random.NextDouble() * initialSizeMax + initialSizeMin);
                p.Size = p.InitialSize;
                p.ExpansionFactor = expansionFactor;

                int gray = _random.Next(colorMin, colorMax);
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

                p.Position += p.Velocity * deltaTime;
                p.Velocity *= 0.98f; // Drag
                p.Rotation += p.RotationSpeed * deltaTime;

                float lifeRatio = p.Life / p.MaxLife;
                float alpha = 1f - lifeRatio;
                p.Size = p.InitialSize * (1f + lifeRatio * p.ExpansionFactor); // Smoke expands based on stage

                p.Color = new Color(p.Color, alpha);
            }
        }

        public void Draw(Camera camera)
        {
            if (_particles.Count == 0) return;

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

            Vector3 cameraUp = camera.View.Up;
            Vector3 cameraRight = camera.View.Right;

            for (int i = 0; i < _particles.Count; i++)
            {
                Particle p = _particles[i];
                
                Vector3 right = cameraRight * p.Size;
                Vector3 up = cameraUp * p.Size;

                _vertexBuffer[i * 4 + 0] = new VertexPositionColorTexture(p.Position - right - up, p.Color, new Vector2(0, 0));
                _vertexBuffer[i * 4 + 1] = new VertexPositionColorTexture(p.Position + right - up, p.Color, new Vector2(1, 0));
                _vertexBuffer[i * 4 + 2] = new VertexPositionColorTexture(p.Position + right + up, p.Color, new Vector2(1, 1));
                _vertexBuffer[i * 4 + 3] = new VertexPositionColorTexture(p.Position - right + up, p.Color, new Vector2(0, 1));
            }

            var oldBlend = _graphicsDevice.BlendState;
            var oldDepth = _graphicsDevice.DepthStencilState;
            var oldRasterizer = _graphicsDevice.RasterizerState;

            _graphicsDevice.BlendState = BlendState.AlphaBlend;
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
