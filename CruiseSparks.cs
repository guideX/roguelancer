using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;

namespace Roguelancer
{
    /// <summary>
    /// Renders firefly/spark particles during cruise charge sequence.
    /// These float away from the engines with a glowing, flickering effect.
    /// </summary>
    public class CruiseSparks
    {
        private class Spark
        {
            public Vector3 Position;
            public Vector3 Velocity;
            public float Life;
            public float MaxLife;
            public float Size;
            public float Brightness; // Flicker intensity
            public float FlickerSpeed; // How fast it flickers
        }
        
        private readonly List<Spark> _sparks = new List<Spark>(512);
        private readonly GraphicsDevice _graphicsDevice;
        private readonly BasicEffect _effect;
        private readonly Texture2D _texture;
        private readonly Random _random = new Random();
        
        // Emission control - MASSIVELY INCREASED for visibility
        public float EmissionRate = 300f; // Was 60, now 300 - 5x more sparks!
        private float _emissionAccumulator;
        
        // Batching buffers
        private VertexPositionColorTexture[] _vertexBuffer = new VertexPositionColorTexture[0];
        private short[] _indexBuffer = new short[0];
        
        public CruiseSparks(GraphicsDevice graphicsDevice)
        {
            _graphicsDevice = graphicsDevice;
            _effect = new BasicEffect(graphicsDevice)
            {
                TextureEnabled = true,
                VertexColorEnabled = true,
                LightingEnabled = false
            };
            _texture = CreateSparkTexture(graphicsDevice, 16);
            _effect.Texture = _texture;
        }
        
        private Texture2D CreateSparkTexture(GraphicsDevice device, int size)
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
                    // Sharp bright center with soft falloff (firefly look)
                    float a = 1f - d;
                    a = (float)Math.Pow(a, 3); // Cubic falloff for bright center
                    data[y * size + x] = new Color(1f, 1f, 1f, MathHelper.Clamp(a, 0f, 1f));
                }
            }
            tex.SetData(data);
            return tex;
        }
        
        public void Update(GameTime gameTime, Ship ship)
        {
            float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
            
            // Update existing sparks
            for (int i = _sparks.Count - 1; i >= 0; i--)
            {
                Spark s = _sparks[i];
                
                // Age and move
                s.Life += dt;
                if (s.Life >= s.MaxLife)
                {
                    _sparks.RemoveAt(i);
                    continue;
                }
                
                // Float away from ship with slight deceleration
                s.Velocity *= 0.98f; // Light drag
                s.Position += s.Velocity * dt;
                
                // Update flicker
                s.Brightness = 0.5f + 0.5f * (float)Math.Sin(gameTime.TotalGameTime.TotalSeconds * s.FlickerSpeed);
            }
            
            // Only emit during cruise charging
            if (!ship.IsCruiseCharging || ship.Model == null)
            {
                return;
            }
            
            // Emit rate increases with charge progress
            float chargeIntensity = MathHelper.Lerp(0.3f, 1.5f, ship.CruiseChargeProgress);
            float rate = EmissionRate * chargeIntensity;
            
            _emissionAccumulator += rate * dt;
            int emitCount = (int)_emissionAccumulator;
            _emissionAccumulator -= emitCount;
            
            if (emitCount <= 0) return;
            
            // Debug output (only occasionally to avoid spam)
            if (_sparks.Count % 50 == 0 && emitCount > 0)
            {
                Console.WriteLine($"? Cruise Sparks: {_sparks.Count} active, emitting {emitCount} new sparks (charge: {ship.CruiseChargeProgress:P0})");
            }
            
            // Emit from engine positions
            Matrix modelCorrection = Matrix.CreateRotationX(-MathHelper.PiOver2) * Matrix.CreateRotationY(MathHelper.Pi);
            Matrix shipTransform = modelCorrection * ship.Orientation * Matrix.CreateTranslation(ship.Position);
            
            Vector3[] engineOffsets = new[]
            {
                new Vector3(-2.5f, 0f, 6.5f),  // Left engine
                new Vector3(2.5f, 0f, 6.5f),   // Right engine
                new Vector3(0f, 0f, 7.0f)      // Center engine
            };
            
            for (int e = 0; e < engineOffsets.Length; e++)
            {
                Vector3 enginePos = Vector3.Transform(engineOffsets[e], shipTransform);
                int perEngine = emitCount / engineOffsets.Length;
                
                for (int i = 0; i < perEngine; i++)
                {
                    Spark spark = new Spark();
                    
                    // Start slightly offset from engine center (random spread) - MUCH LARGER OFFSET
                    Vector3 randomOffset = new Vector3(
                        (float)(_random.NextDouble() * 2 - 1),
                        (float)(_random.NextDouble() * 2 - 1),
                        (float)(_random.NextDouble() * 2 - 1)
                    ) * 8f; // Was 1.5f, now 8f for large ship scale
                    
                    spark.Position = enginePos + randomOffset;
                    
                    // Velocity: mostly backward from ship, with random drift
                    Vector3 baseDir = -ship.Forward;
                    Vector3 randomDrift = new Vector3(
                        (float)(_random.NextDouble() * 2 - 1),
                        (float)(_random.NextDouble() * 2 - 1),
                        (float)(_random.NextDouble() * 2 - 1)
                    ) * 1.5f; // Increased drift for more spread
                    
                    // Slower than exhaust - floaty firefly feel, but faster for large scale
                    spark.Velocity = (baseDir + randomDrift) * (40f + (float)_random.NextDouble() * 30f); // Was 15-25, now 40-70
                    
                    spark.Life = 0f;
                    spark.MaxLife = 2.0f + (float)_random.NextDouble() * 1.5f; // Was 1.5-2.5, now 2.0-3.5 seconds (longer life)
                    spark.Size = 15f + (float)_random.NextDouble() * 20f; // Was 2-5, now 15-35 units (MUCH LARGER!)
                    spark.Brightness = (float)_random.NextDouble(); // Random starting brightness
                    spark.FlickerSpeed = 5f + (float)_random.NextDouble() * 10f; // Random flicker rate
                    
                    _sparks.Add(spark);
                }
            }
        }
        
        public void Draw(Camera camera)
        {
            int count = _sparks.Count;
            if (count == 0) return;
            
            // Ensure buffers sized
            int requiredVertices = count * 4;
            int requiredIndices = count * 6;
            if (_vertexBuffer.Length < requiredVertices) _vertexBuffer = new VertexPositionColorTexture[requiredVertices];
            if (_indexBuffer.Length < requiredIndices) _indexBuffer = new short[requiredIndices];
            
            Vector3 camPos = camera.Position;
            
            // Build billboards for each spark
            for (int i = 0; i < count; i++)
            {
                Spark s = _sparks[i];
                
                // Age-based fade
                float t = s.Life / s.MaxLife;
                float alpha = (1f - t) * s.Brightness; // Fade out over time + flicker
                alpha = MathHelper.Clamp(alpha * 1.5f, 0f, 1f); // BOOSTED 50% brighter!
                
                // Size grows slightly as it fades
                float size = s.Size * (1f + t * 0.6f); // Increased growth from 0.4f to 0.6f
                
                // Color: Bright cyan-white (electric/energy look) - MUCH BRIGHTER!
                // Shift from bright white to cyan-blue as it ages
                Vector3 colorStart = new Vector3(1.5f, 1.5f, 1.5f); // Was (1,1,1) - NOW SUPER BRIGHT!
                Vector3 colorEnd = new Vector3(0.6f, 1.2f, 1.5f);   // Was (0.3,0.7,1) - NOW MUCH BRIGHTER CYAN!
                Vector3 colorVec = Vector3.Lerp(colorStart, colorEnd, t * 0.7f);
                Color color = new Color(colorVec) * alpha;
                
                // Billboard facing camera
                Vector3 forward = camPos - s.Position;
                if (forward.LengthSquared() < 0.00001f) forward = Vector3.Forward;
                forward.Normalize();
                
                Vector3 right = Vector3.Cross(Vector3.Up, forward);
                if (right.LengthSquared() < 0.00001f) right = Vector3.Right;
                else right.Normalize();
                
                Vector3 up = Vector3.Cross(forward, right);
                
                // Build quad
                int vBase = i * 4;
                int iBase = i * 6;
                
                _vertexBuffer[vBase + 0] = new VertexPositionColorTexture(s.Position + (-right - up) * size, color, new Vector2(0, 1));
                _vertexBuffer[vBase + 1] = new VertexPositionColorTexture(s.Position + (right - up) * size, color, new Vector2(1, 1));
                _vertexBuffer[vBase + 2] = new VertexPositionColorTexture(s.Position + (-right + up) * size, color, new Vector2(0, 0));
                _vertexBuffer[vBase + 3] = new VertexPositionColorTexture(s.Position + (right + up) * size, color, new Vector2(1, 0));
                
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
            
            // Additive blending for bright firefly effect
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
    }
}
