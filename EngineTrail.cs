using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.Collections.Generic;

namespace Roguelancer
{
    /// <summary>
    /// Renders a particle trail behind the ship's engines.
    /// </summary>
    public class EngineTrail
    {
        private class Particle
        {
            public Vector3 Position;
            public Vector3 Velocity;
            public float Life;          // Current life
            public float MaxLife;       // Total lifespan
            public float Size;          // Starting size
        }
        
        private readonly List<Particle> _particles = new List<Particle>(2048);
        private readonly GraphicsDevice _graphicsDevice;
        private readonly BasicEffect _effect;
        private readonly Texture2D _texture;
        
        // Emission control
        public float EmissionRate = 200f; // particles per second per engine at full throttle
        public float ParticleLife = 2.5f; // seconds
        public float BaseSize = 3f;
        
        private float _emissionAccumulator;
        
        // Batching buffers
        private VertexPositionColorTexture[] _vertexBuffer = new VertexPositionColorTexture[0];
        private short[] _indexBuffer = new short[0];
        
        public EngineTrail(GraphicsDevice graphicsDevice)
        {
            _graphicsDevice = graphicsDevice;
            _effect = new BasicEffect(graphicsDevice)
            {
                TextureEnabled = true,
                VertexColorEnabled = true,
                LightingEnabled = false
            };
            _texture = CreateSoftTexture(graphicsDevice, 32);
            _effect.Texture = _texture;
        }
        
        private Texture2D CreateSoftTexture(GraphicsDevice device, int size)
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
                    float a = 1f - d;
                    a *= a; // soften falloff
                    data[y * size + x] = new Color(1f, 1f, 1f, a);
                }
            }
            tex.SetData(data);
            return tex;
        }
        
        public void Update(GameTime gameTime, Ship ship, float throttle)
        {
            float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
            
            // Remove / age particles
            for (int i = _particles.Count - 1; i >= 0; i--)
            {
                _particles[i].Life += dt;
                if (_particles[i].Life >= _particles[i].MaxLife)
                {
                    _particles.RemoveAt(i);
                    continue;
                }
                _particles[i].Position += _particles[i].Velocity * dt;
            }
            
            if (ship.Model == null) return;
            
            float intensity = throttle;
            
            // ? CRUISE CHARGING - Intense particle emission
            if (ship.IsCruiseCharging)
            {
                intensity = MathHelper.Lerp(1.0f, 2.5f, ship.CruiseChargeProgress); // Ramp up!
            }
            else if (ship.IsAfterburnerActive)
            {
                intensity = 1.5f;
            }
            else if (ship.IsCruiseActive)
            {
                intensity = 1.8f; // More intense for active cruise
            }
            
            intensity = MathHelper.Clamp(intensity, 0.05f, 2.5f); // Allow up to 2.5x intensity
            
            float rate = EmissionRate * intensity;
            _emissionAccumulator += rate * dt;
            int emitCount = (int)_emissionAccumulator;
            _emissionAccumulator -= emitCount;
            if (emitCount <= 0) return;
            
            Matrix modelCorrection = Matrix.CreateRotationX(-MathHelper.PiOver2) * Matrix.CreateRotationY(MathHelper.Pi);
            Matrix shipTransform = modelCorrection * ship.Orientation * Matrix.CreateTranslation(ship.Position);
            Vector3[] offsets = new[]
            {
                new Vector3(-2.5f, 0f, 6.5f),
                new Vector3(2.5f, 0f, 6.5f),
                new Vector3(0f, 0f, 7.0f)
            };
            
            for (int e = 0; e < offsets.Length; e++)
            {
                Vector3 enginePos = Vector3.Transform(offsets[e], shipTransform);
                int perEngine = emitCount / offsets.Length;
                for (int i = 0; i < perEngine; i++)
                {
                    Particle p = new Particle();
                    p.Position = enginePos;
                    Vector3 baseDir = -ship.Forward;
                    Vector3 rand = new Vector3(
                        (float)(RandomSingleton.NextDouble() * 2 - 1) * 0.2f,
                        (float)(RandomSingleton.NextDouble() * 2 - 1) * 0.2f,
                        (float)(RandomSingleton.NextDouble() * 2 - 1) * 0.2f);
                    p.Velocity = (baseDir + rand) * (30f + intensity * 60f);
                    p.Life = 0f;
                    p.MaxLife = ParticleLife * (ship.IsAfterburnerActive ? 0.7f : 1f);
                    p.Size = BaseSize + intensity * 6f;
                    _particles.Add(p);
                }
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
            
            // Prepare billboard axes once per particle
            for (int i = 0; i < count; i++)
            {
                Particle p = _particles[i];
                float t = p.Life / p.MaxLife;
                float alpha = (1f - t); alpha *= (1f - t); // sharpen fade
                float size = p.Size * (1f + t * 0.3f);
                Color color = shipColorFromLife(t) * alpha;
                Vector3 forward = camPos - p.Position;
                if (forward.LengthSquared() < 0.00001f) forward = Vector3.Forward; // prevent NaN
                forward.Normalize();
                Vector3 right = Vector3.Cross(Vector3.Up, forward);
                if (right.LengthSquared() < 0.00001f) right = Vector3.Right; else right.Normalize();
                Vector3 up = Vector3.Cross(forward, right);
                
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
            
            // ? FIX: Save ALL render states including SamplerState
            var oldBlend = _graphicsDevice.BlendState;
            var oldDepth = _graphicsDevice.DepthStencilState;
            var oldRaster = _graphicsDevice.RasterizerState;
            var oldSampler = _graphicsDevice.SamplerStates[0];
            
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
                _graphicsDevice.DrawUserIndexedPrimitives(PrimitiveType.TriangleList, _vertexBuffer, 0, count * 4, _indexBuffer, 0, count * 2);
            }
            
            // ? FIX: Restore ALL render states
            _graphicsDevice.BlendState = oldBlend;
            _graphicsDevice.DepthStencilState = oldDepth;
            _graphicsDevice.RasterizerState = oldRaster;
            _graphicsDevice.SamplerStates[0] = oldSampler;
        }
        
        private Color shipColorFromLife(float t)
        {
            Vector3 start = new Vector3(0.6f, 0.8f, 1.0f);
            Vector3 end = new Vector3(1.0f, 0.5f, 0.1f);
            Vector3 col = Vector3.Lerp(start, end, t);
            return new Color(col);
        }
        
        private static class RandomSingleton
        {
            private static readonly System.Random _rnd = new System.Random();
            public static double NextDouble() => _rnd.NextDouble();
        }
    }
}
