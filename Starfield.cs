using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;

namespace Roguelancer
{
    /// <summary>
    /// Starfield rendering modes
    /// </summary>
    public enum StarfieldMode
    {
        Static,         // Static stars, no special effects
        Parallax,       // Stars with depth-based parallax
        MotionStreak,   // Motion blur streaks when moving (default)
        Twinkling       // Stars twinkle and pulse
    }

    /// <summary>
    /// Starfield background for space environment with lifecycle and modes
    /// </summary>
    public class Starfield
    {
        private struct Star
        {
            public Vector3 Position;
            public float Size;
            public Color Color;
            public float Brightness;
            public float Depth; // For parallax effect
            public float Life; // Current age (0-1)
            public float MaxLife; // How long before fading out
            public float TwinklePhase; // For twinkling effect
            public float TwinkleSpeed; // How fast it twinkles
        }

        private class StarLayer
        {
            public Star[] Stars;
            public VertexPositionColor[] Vertices;
            public float Depth; // Represents how far this layer is. 1.0 is closest, 0.0 is furthest.
            public float SpeedMultiplier;

            public StarLayer(int starCount, float depth, Random random)
            {
                Depth = depth;
                SpeedMultiplier = 1.0f - depth; // Farther layers move slower
                Stars = new Star[starCount];
                Vertices = new VertexPositionColor[starCount];

                for (int i = 0; i < starCount; i++)
                {
                    Stars[i] = CreateNewStar(random, depth);
                    Stars[i].Life = (float)random.NextDouble(); // Randomize initial life
                    Vertices[i] = new VertexPositionColor(Stars[i].Position, Stars[i].Color);
                }
            }

            public Star CreateNewStar(Random random, float depth)
            {
                float theta = (float)(random.NextDouble() * Math.PI * 2);
                float phi = (float)Math.Acos(2 * random.NextDouble() - 1);

                float radius = 500f + (1.0f - depth) * 8000f; // Farther layers have stars further away

                Vector3 position = new Vector3(
                    radius * (float)(Math.Sin(phi) * Math.Cos(theta)),
                    radius * (float)(Math.Sin(phi) * Math.Sin(theta)),
                    radius * (float)Math.Cos(phi)
                );

                float starType = (float)random.NextDouble();
                float brightness = 0.3f + (float)(random.NextDouble() * 0.7f);
                Color color;

                if (starType < 0.1f) color = new Color(0.7f * brightness, 0.8f * brightness, 1.0f * brightness);
                else if (starType < 0.3f) color = new Color(0.85f * brightness, 0.9f * brightness, 1.0f * brightness);
                else if (starType < 0.6f) color = new Color(brightness, brightness * 0.98f, brightness * 0.95f);
                else if (starType < 0.85f) color = new Color(1.0f * brightness, 0.95f * brightness, 0.8f * brightness);
                else color = new Color(1.0f * brightness, 0.7f * brightness, 0.5f * brightness);

                float size = 1f + (float)(random.NextDouble() * 3f) * (1.0f - depth); // Farther stars are smaller
                if (brightness > 0.8f) size *= 1.5f;

                float maxLife = 5f + (float)(random.NextDouble() * 10f);

                return new Star
                {
                    Position = position,
                    Size = size,
                    Color = color,
                    Brightness = brightness,
                    Depth = depth,
                    Life = 0f,
                    MaxLife = maxLife,
                    TwinklePhase = (float)(random.NextDouble() * Math.PI * 2),
                    TwinkleSpeed = 0.5f + (float)(random.NextDouble() * 2f)
                };
            }

            public void Update(float deltaTime, float lifecycleSpeed, Random random)
            {
                for (int i = 0; i < Stars.Length; i++)
                {
                    Stars[i].Life += deltaTime * lifecycleSpeed;
                    Stars[i].TwinklePhase += deltaTime * Stars[i].TwinkleSpeed;

                    if (Stars[i].Life >= Stars[i].MaxLife)
                    {
                        Stars[i] = CreateNewStar(random, Depth);
                    }
                }
            }
        }

        private List<StarLayer> _layers;
        private BasicEffect _effect;
        private GraphicsDevice _graphicsDevice;
        private Vector3 _lastCameraPosition;
        private Random _random;

        // Customization properties
        public StarfieldMode Mode { get; set; } = StarfieldMode.MotionStreak;
        public bool EnableLifecycle { get; set; } = true; // Stars fade in/out over time
        public float LifecycleSpeed { get; set; } = 0.05f; // How fast stars age
        public float TwinkleIntensity { get; set; } = 0.3f; // How much stars twinkle (0-1)
        public float ParallaxStrength { get; set; } = 1.0f; // Strength of parallax effect (0-1)
        public float StreakMultiplier { get; set; } = 0.3f; // Length of motion streaks
        
        public Starfield(GraphicsDevice graphicsDevice, int[] layerCounts)
        {
            _graphicsDevice = graphicsDevice;
            _random = new Random();
            _layers = new List<StarLayer>();
            _lastCameraPosition = Vector3.Zero;

            for (int i = 0; i < layerCounts.Length; i++)
            {
                // Depth goes from 0 (far) to almost 1 (near)
                float depth = i / (float)layerCounts.Length;
                _layers.Add(new StarLayer(layerCounts[i], depth, _random));
            }
            
            _effect = new BasicEffect(graphicsDevice)
            {
                VertexColorEnabled = true,
                LightingEnabled = false
            };
        }

        /// <summary>
        /// Update star lifecycle (call this from your game's Update method)
        /// </summary>
        public void Update(float deltaTime)
        {
            if (!EnableLifecycle) return;

            foreach (var layer in _layers)
            {
                layer.Update(deltaTime, LifecycleSpeed, _random);
                for (int i = 0; i < layer.Stars.Length; i++)
                {
                    Color finalColor = CalculateStarColor(layer.Stars[i]);
                    layer.Vertices[i].Color = finalColor;
                    layer.Vertices[i].Position = layer.Stars[i].Position;
                }
            }
        }

        /// <summary>
        /// Calculate star color based on lifecycle fade and current mode
        /// </summary>
        private Color CalculateStarColor(Star star)
        {
            Color baseColor = star.Color;
            float alpha = 1f;

            // Lifecycle fade in/out
            if (EnableLifecycle)
            {
                float fadeInDuration = 1f; // 1 second fade in
                float fadeOutDuration = 2f; // 2 seconds fade out
                float lifeNormalized = star.Life / star.MaxLife;

                if (star.Life < fadeInDuration)
                {
                    // Fade in
                    alpha = star.Life / fadeInDuration;
                }
                else if (lifeNormalized > 0.8f)
                {
                    // Fade out in last 20% of life
                    float fadeProgress = (lifeNormalized - 0.8f) / 0.2f;
                    alpha = 1f - fadeProgress;
                }
            }

            // Twinkling effect
            if (Mode == StarfieldMode.Twinkling && TwinkleIntensity > 0)
            {
                float twinkle = (float)Math.Sin(star.TwinklePhase) * 0.5f + 0.5f; // 0-1
                alpha *= 1f - (twinkle * TwinkleIntensity * 0.4f); // Reduce alpha by up to 40%
            }

            return baseColor * alpha;
        }

        public void Draw(Matrix view, Matrix projection, Vector3 cameraPosition, Vector3 shipVelocity)
        {
            // Use ship velocity instead of camera movement
            float speed = shipVelocity.Length();
            _lastCameraPosition = cameraPosition;
            
            // Save current render states
            var oldDepthStencilState = _graphicsDevice.DepthStencilState;
            var oldBlendState = _graphicsDevice.BlendState;
            var oldRasterizerState = _graphicsDevice.RasterizerState;
            
            // Update view and projection matrices
            _effect.View = view;
            _effect.Projection = projection;
            
            // Draw based on current mode
            switch (Mode)
            {
                case StarfieldMode.Static:
                    DrawStaticStars(cameraPosition);
                    break;

                case StarfieldMode.Parallax:
                    DrawParallaxStars(cameraPosition);
                    break;

                case StarfieldMode.MotionStreak:
                    // Draw stars with motion streaks that scale with speed
                    if (speed > 0.1f)
                    {
                        DrawStarsWithMotion(cameraPosition, shipVelocity, speed);
                    }
                    else
                    {
                        // Full stop - draw static stars
                        DrawStaticStars(cameraPosition);
                    }
                    break;

                case StarfieldMode.Twinkling:
                    DrawTwinklingStars(cameraPosition);
                    break;
            }
            
            // Restore render states
            _graphicsDevice.DepthStencilState = oldDepthStencilState;
            _graphicsDevice.BlendState = oldBlendState;
            _graphicsDevice.RasterizerState = oldRasterizerState;
        }
        
        private void DrawStaticStars(Vector3 cameraPosition)
        {
            // Draw stars as points with no depth testing (always in background)
            _graphicsDevice.DepthStencilState = DepthStencilState.None;
            _graphicsDevice.BlendState = BlendState.Additive;
            _graphicsDevice.RasterizerState = RasterizerState.CullNone;
            
            foreach (var layer in _layers)
            {
                _effect.World = Matrix.CreateTranslation(cameraPosition);
                foreach (EffectPass pass in _effect.CurrentTechnique.Passes)
                {
                    pass.Apply();
                    _graphicsDevice.DrawUserPrimitives(
                        PrimitiveType.PointList,
                        layer.Vertices,
                        0,
                        layer.Stars.Length
                    );
                }
            }
        }

        private void DrawParallaxStars(Vector3 cameraPosition)
        {
            // Draw stars with depth-based parallax (closer stars appear to move more)
            _graphicsDevice.DepthStencilState = DepthStencilState.None;
            _graphicsDevice.BlendState = BlendState.Additive;
            _graphicsDevice.RasterizerState = RasterizerState.CullNone;
            
            foreach (var layer in _layers)
            {
                // Apply parallax effect by moving the layer's world matrix
                Vector3 parallaxOffset = cameraPosition * layer.SpeedMultiplier * ParallaxStrength;
                _effect.World = Matrix.CreateTranslation(parallaxOffset);

                foreach (EffectPass pass in _effect.CurrentTechnique.Passes)
                {
                    pass.Apply();
                    _graphicsDevice.DrawUserPrimitives(
                        PrimitiveType.PointList,
                        layer.Vertices,
                        0,
                        layer.Stars.Length
                    );
                }
            }
        }

        private void DrawTwinklingStars(Vector3 cameraPosition)
        {
            // Draw stars with pulsing/twinkling effect
            _graphicsDevice.DepthStencilState = DepthStencilState.None;
            _graphicsDevice.BlendState = BlendState.Additive;
            _graphicsDevice.RasterizerState = RasterizerState.CullNone;
            
            foreach (var layer in _layers)
            {
                _effect.World = Matrix.CreateTranslation(cameraPosition);
                // Vertices already updated with twinkling in Update()
                foreach (EffectPass pass in _effect.CurrentTechnique.Passes)
                {
                    pass.Apply();
                    _graphicsDevice.DrawUserPrimitives(
                        PrimitiveType.PointList,
                        layer.Vertices,
                        0,
                        layer.Stars.Length
                    );
                }
            }
        }
        
        private void DrawStarsWithMotion(Vector3 cameraPosition, Vector3 velocity, float speed)
        {
            // Set render states for motion streaks
            _graphicsDevice.DepthStencilState = DepthStencilState.None;
            _graphicsDevice.BlendState = BlendState.Additive;
            _graphicsDevice.RasterizerState = RasterizerState.CullNone;
            
            // Normalize velocity for direction
            if (velocity.LengthSquared() < 0.0001f) return; // Safety check
            
            Vector3 motionDir = Vector3.Normalize(velocity);
            
            // Smooth scaling of streak length based on speed
            float speedFactor = MathHelper.Clamp(speed / 500f, 0f, 1f); // Increased divisor for less sensitive scaling
            float baseStreakLength = speed * StreakMultiplier * speedFactor;
            
            // If streaks are too small, just draw static stars instead
            if (baseStreakLength < 0.5f)
            {
                DrawStaticStars(cameraPosition);
                return;
            }

            foreach (var layer in _layers)
            {
                _effect.World = Matrix.CreateTranslation(cameraPosition);
                float streakLength = Math.Min(baseStreakLength * layer.SpeedMultiplier * 2.0f, 200f); // Layers move at different speeds
                if (streakLength < 0.5f) continue;

                // Create line list for motion streaks
                var lineVertices = new VertexPositionColor[layer.Stars.Length * 2];
                
                for (int i = 0; i < layer.Stars.Length; i++)
                {
                    Vector3 starPos = layer.Stars[i].Position;
                    
                    Vector3 streak = motionDir * streakLength * layer.Stars[i].Brightness;
                    
                    // Get lifecycle-adjusted color
                    Color starColor = layer.Vertices[i].Color;
                    
                    // Start point (star position) - full brightness
                    lineVertices[i * 2] = new VertexPositionColor(starPos, starColor);
                    
                    // End point (stretched in motion direction) - fade out at the tail
                    Color fadeColor = starColor;
                    fadeColor.A = (byte)(fadeColor.A * 0.2f);
                    lineVertices[i * 2 + 1] = new VertexPositionColor(starPos - streak, fadeColor);
                }
                
                foreach (EffectPass pass in _effect.CurrentTechnique.Passes)
                {
                    pass.Apply();
                    _graphicsDevice.DrawUserPrimitives(
                        PrimitiveType.LineList,
                        lineVertices,
                        0,
                        layer.Stars.Length
                    );
                }
            }
        }

        /// <summary>
        /// Cycle to the next rendering mode
        /// </summary>
        public void CycleMode()
        {
            Mode = Mode switch
            {
                StarfieldMode.Static => StarfieldMode.Parallax,
                StarfieldMode.Parallax => StarfieldMode.MotionStreak,
                StarfieldMode.MotionStreak => StarfieldMode.Twinkling,
                StarfieldMode.Twinkling => StarfieldMode.Static,
                _ => StarfieldMode.MotionStreak
            };
        }

        /// <summary>
        /// Get the name of the current mode
        /// </summary>
        public string GetModeName()
        {
            return Mode switch
            {
                StarfieldMode.Static => "Static Stars",
                StarfieldMode.Parallax => "Parallax Stars",
                StarfieldMode.MotionStreak => "Motion Streak",
                StarfieldMode.Twinkling => "Twinkling Stars",
                _ => "Unknown"
            };
        }
    }
}
