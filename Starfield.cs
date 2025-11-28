using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;

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

        private Star[] _stars;
        private VertexPositionColor[] _vertices;
        private BasicEffect _effect;
        private GraphicsDevice _graphicsDevice;
        private Texture2D _starTexture;
        private Vector3 _lastCameraPosition;
        private Random _random;

        // Customization properties
        public StarfieldMode Mode { get; set; } = StarfieldMode.MotionStreak;
        public bool EnableLifecycle { get; set; } = true; // Stars fade in/out over time
        public float LifecycleSpeed { get; set; } = 0.05f; // How fast stars age
        public float TwinkleIntensity { get; set; } = 0.3f; // How much stars twinkle (0-1)
        public float ParallaxStrength { get; set; } = 0.7f; // Strength of parallax effect (0-1)
        public float StreakMultiplier { get; set; } = 0.3f; // Length of motion streaks
        
        private int _starCount;

        public Starfield(GraphicsDevice graphicsDevice, int starCount = 5000)
        {
            _graphicsDevice = graphicsDevice;
            _starCount = starCount;
            _stars = new Star[starCount];
            _vertices = new VertexPositionColor[starCount];
            _lastCameraPosition = Vector3.Zero;
            _random = new Random();
            
            // Generate initial stars
            for (int i = 0; i < starCount; i++)
            {
                _stars[i] = CreateNewStar(_random);
                // Randomize initial life so they don't all fade at once
                _stars[i].Life = (float)_random.NextDouble();
                _vertices[i] = new VertexPositionColor(_stars[i].Position, _stars[i].Color);
            }
            
            // Create a simple star texture for point sprites
            _starTexture = CreateStarTexture(graphicsDevice, 8);
            
            _effect = new BasicEffect(graphicsDevice)
            {
                VertexColorEnabled = true,
                LightingEnabled = false
            };
        }

        /// <summary>
        /// Create a new star with random properties
        /// </summary>
        private Star CreateNewStar(Random random)
        {
            // Random point on sphere
            float theta = (float)(random.NextDouble() * Math.PI * 2);
            float phi = (float)(Math.Acos(2 * random.NextDouble() - 1));
            
            // Varied depth for parallax effect
            float depth = (float)random.NextDouble();
            float radius = 500f + depth * 4000f;
            
            Vector3 position = new Vector3(
                radius * (float)(Math.Sin(phi) * Math.Cos(theta)),
                radius * (float)(Math.Sin(phi) * Math.Sin(theta)),
                radius * (float)Math.Cos(phi)
            );
            
            // More realistic star colors and brightness
            float starType = (float)random.NextDouble();
            float brightness = 0.3f + (float)(random.NextDouble() * 0.7f);
            Color color;
            
            if (starType < 0.1f) // Blue giants (rare)
            {
                color = new Color(0.7f * brightness, 0.8f * brightness, 1.0f * brightness);
            }
            else if (starType < 0.3f) // Blue-white stars
            {
                color = new Color(0.85f * brightness, 0.9f * brightness, 1.0f * brightness);
            }
            else if (starType < 0.6f) // White stars (most common)
            {
                color = new Color(brightness, brightness * 0.98f, brightness * 0.95f);
            }
            else if (starType < 0.85f) // Yellow-white stars
            {
                color = new Color(1.0f * brightness, 0.95f * brightness, 0.8f * brightness);
            }
            else // Red stars (cooler)
            {
                color = new Color(1.0f * brightness, 0.7f * brightness, 0.5f * brightness);
            }
            
            // Vary star sizes
            float size = 1f + (float)(random.NextDouble() * 3f);
            if (brightness > 0.8f) size *= 1.5f; // Brighter stars appear larger
            
            // Lifecycle properties
            float maxLife = 5f + (float)(random.NextDouble() * 10f); // 5-15 seconds lifespan
            
            return new Star
            {
                Position = position,
                Size = size,
                Color = color,
                Brightness = brightness,
                Depth = depth,
                Life = 0f, // Start faded out
                MaxLife = maxLife,
                TwinklePhase = (float)(random.NextDouble() * Math.PI * 2),
                TwinkleSpeed = 0.5f + (float)(random.NextDouble() * 2f)
            };
        }
        
        private Texture2D CreateStarTexture(GraphicsDevice device, int size)
        {
            Texture2D texture = new Texture2D(device, size, size);
            Color[] data = new Color[size * size];
            
            Vector2 center = new Vector2(size / 2f, size / 2f);
            
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    Vector2 pos = new Vector2(x, y);
                    float distance = Vector2.Distance(pos, center) / (size / 2f);
                    
                    // Create radial gradient with bright center
                    float alpha = Math.Max(0, 1f - distance);
                    alpha = (float)Math.Pow(alpha, 1.5f); // Sharper center
                    
                    data[y * size + x] = new Color(1f, 1f, 1f, alpha);
                }
            }
            
            texture.SetData(data);
            return texture;
        }

        /// <summary>
        /// Update star lifecycle (call this from your game's Update method)
        /// </summary>
        public void Update(float deltaTime)
        {
            if (!EnableLifecycle) return;

            for (int i = 0; i < _stars.Length; i++)
            {
                // Age the star
                _stars[i].Life += deltaTime * LifecycleSpeed;
                
                // Update twinkle phase
                _stars[i].TwinklePhase += deltaTime * _stars[i].TwinkleSpeed;
                
                // When star reaches end of life, respawn it
                if (_stars[i].Life >= _stars[i].MaxLife)
                {
                    _stars[i] = CreateNewStar(_random);
                }
                
                // Update vertex color based on lifecycle and mode
                Color finalColor = CalculateStarColor(_stars[i]);
                _vertices[i].Color = finalColor;
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
            _effect.World = Matrix.CreateTranslation(cameraPosition);
            
            // Draw based on current mode
            switch (Mode)
            {
                case StarfieldMode.Static:
                    DrawStaticStars();
                    break;

                case StarfieldMode.Parallax:
                    DrawParallaxStars(view, cameraPosition);
                    break;

                case StarfieldMode.MotionStreak:
                    // Draw stars with motion streaks that scale with speed
                    if (speed > 0.1f)
                    {
                        DrawStarsWithMotion(view, projection, shipVelocity, speed);
                    }
                    else
                    {
                        // Full stop - draw static stars
                        DrawStaticStars();
                    }
                    break;

                case StarfieldMode.Twinkling:
                    DrawTwinklingStars();
                    break;
            }
            
            // Restore render states
            _graphicsDevice.DepthStencilState = oldDepthStencilState;
            _graphicsDevice.BlendState = oldBlendState;
            _graphicsDevice.RasterizerState = oldRasterizerState;
        }
        
        private void DrawStaticStars()
        {
            // Draw stars as points with no depth testing (always in background)
            _graphicsDevice.DepthStencilState = DepthStencilState.None;
            _graphicsDevice.BlendState = BlendState.Additive;
            _graphicsDevice.RasterizerState = RasterizerState.CullNone;
            
            foreach (EffectPass pass in _effect.CurrentTechnique.Passes)
            {
                pass.Apply();
                _graphicsDevice.DrawUserPrimitives(
                    PrimitiveType.PointList,
                    _vertices,
                    0,
                    _stars.Length
                );
            }
        }

        private void DrawParallaxStars(Matrix view, Vector3 cameraPosition)
        {
            // Draw stars with depth-based parallax (closer stars appear to move more)
            _graphicsDevice.DepthStencilState = DepthStencilState.None;
            _graphicsDevice.BlendState = BlendState.Additive;
            _graphicsDevice.RasterizerState = RasterizerState.CullNone;

            // Update vertices with parallax offset based on camera movement
            Vector3 cameraMovement = cameraPosition - _lastCameraPosition;
            
            for (int i = 0; i < _stars.Length; i++)
            {
                float parallaxFactor = 1f - _stars[i].Depth * ParallaxStrength;
                Vector3 offset = cameraMovement * parallaxFactor * 0.1f;
                _vertices[i].Position = _stars[i].Position + offset;
            }
            
            foreach (EffectPass pass in _effect.CurrentTechnique.Passes)
            {
                pass.Apply();
                _graphicsDevice.DrawUserPrimitives(
                    PrimitiveType.PointList,
                    _vertices,
                    0,
                    _stars.Length
                );
            }
        }

        private void DrawTwinklingStars()
        {
            // Draw stars with pulsing/twinkling effect
            _graphicsDevice.DepthStencilState = DepthStencilState.None;
            _graphicsDevice.BlendState = BlendState.Additive;
            _graphicsDevice.RasterizerState = RasterizerState.CullNone;
            
            // Vertices already updated with twinkling in Update()
            foreach (EffectPass pass in _effect.CurrentTechnique.Passes)
            {
                pass.Apply();
                _graphicsDevice.DrawUserPrimitives(
                    PrimitiveType.PointList,
                    _vertices,
                    0,
                    _stars.Length
                );
            }
        }
        
        private void DrawStarsWithMotion(Matrix view, Matrix projection, Vector3 velocity, float speed)
        {
            // Set render states for motion streaks
            _graphicsDevice.DepthStencilState = DepthStencilState.None;
            _graphicsDevice.BlendState = BlendState.Additive;
            _graphicsDevice.RasterizerState = RasterizerState.CullNone;
            
            // Normalize velocity for direction
            if (velocity.LengthSquared() < 0.0001f) return; // Safety check
            
            Vector3 motionDir = Vector3.Normalize(velocity);
            
            // Smooth scaling of streak length based on speed
            float speedFactor = MathHelper.Clamp(speed / 50f, 0f, 1f);
            float baseStreakLength = speed * StreakMultiplier * speedFactor;
            float streakLength = Math.Min(baseStreakLength, 100f);
            
            // If streaks are too small, just draw static stars instead
            if (streakLength < 0.5f)
            {
                DrawStaticStars();
                return;
            }
            
            // Create line list for motion streaks
            VertexPositionColor[] lineVertices = new VertexPositionColor[_stars.Length * 2];
            
            for (int i = 0; i < _stars.Length; i++)
            {
                Vector3 starPos = _stars[i].Position;
                
                // Parallax effect - closer stars move faster
                float parallaxFactor = 1f - _stars[i].Depth * ParallaxStrength;
                Vector3 streak = motionDir * streakLength * parallaxFactor * _stars[i].Brightness;
                
                // Get lifecycle-adjusted color
                Color starColor = _vertices[i].Color;
                
                // Start point (star position) - full brightness
                lineVertices[i * 2] = new VertexPositionColor(starPos, starColor);
                
                // End point (stretched in motion direction) - fade out at the tail
                Color fadeColor = starColor;
                fadeColor.A = (byte)(fadeColor.A * (0.2f + speedFactor * 0.8f));
                lineVertices[i * 2 + 1] = new VertexPositionColor(starPos - streak, fadeColor);
            }
            
            foreach (EffectPass pass in _effect.CurrentTechnique.Passes)
            {
                pass.Apply();
                _graphicsDevice.DrawUserPrimitives(
                    PrimitiveType.LineList,
                    lineVertices,
                    0,
                    _stars.Length
                );
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
