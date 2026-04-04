using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;

namespace Roguelancer {
    /// <summary>
    /// Full-screen wormhole tunnel effect displayed during jump hole transit.
    /// Creates a swirling tunnel of light with speed lines, energy spirals,
    /// and color-shifting effects like flying through a wormhole at high speed.
    /// </summary>
    public class JumpHoleTransitEffect {
        private GraphicsDevice _graphicsDevice;
        private SpriteBatch _spriteBatch;
        private Texture2D _pixel;
        private SpriteFont _font;
        private Random _random;

        // Transit state
        public bool IsActive { get; private set; }
        public float Progress { get; private set; } // 0 to 1
        public float Duration { get; private set; }
        private float _elapsed;
        private string _targetSystemName;

        // Tunnel particles
        private struct TunnelParticle {
            public float Angle;       // Position around tunnel circumference
            public float Depth;       // 0 = far end, 1 = camera
            public float Speed;       // How fast it approaches
            public Color Color;
            public float Size;
            public float Phase;
        }

        // Speed lines (streaking past)
        private struct SpeedLine {
            public Vector2 Start;
            public float Length;
            public float Speed;
            public Color Color;
            public float Angle;
        }

        // Energy arcs
        private struct EnergyArc {
            public float StartAngle;
            public float EndAngle;
            public float Depth;
            public Color Color;
            public float Life;
            public float MaxLife;
        }

        private TunnelParticle[] _tunnelParticles;
        private SpeedLine[] _speedLines;
        private List<EnergyArc> _energyArcs;

        private const int TunnelParticleCount = 200;
        private const int SpeedLineCount = 80;
        private const int MaxEnergyArcs = 30;

        private float _colorShiftPhase;
        private float _shakeAmount;
        private Vector2 _shakeOffset;

        // Tunnel visual parameters
        private Color _primaryColor = new Color(100, 160, 255);
        private Color _secondaryColor = new Color(180, 100, 255);
        private Color _flashColor = new Color(255, 255, 255);

        public JumpHoleTransitEffect(GraphicsDevice graphicsDevice, SpriteFont font) {
            _graphicsDevice = graphicsDevice;
            _spriteBatch = new SpriteBatch(graphicsDevice);
            _font = font;
            _random = new Random();

            _pixel = new Texture2D(graphicsDevice, 1, 1);
            _pixel.SetData(new[] { Color.White });

            _tunnelParticles = new TunnelParticle[TunnelParticleCount];
            _speedLines = new SpeedLine[SpeedLineCount];
            _energyArcs = new List<EnergyArc>();

            InitializeParticles();
        }

        private void InitializeParticles() {
            // Initialize tunnel ring particles
            for (int i = 0; i < TunnelParticleCount; i++) {
                _tunnelParticles[i] = CreateTunnelParticle();
            }

            // Initialize speed lines
            for (int i = 0; i < SpeedLineCount; i++) {
                _speedLines[i] = CreateSpeedLine();
            }
        }

        private TunnelParticle CreateTunnelParticle() {
            return new TunnelParticle {
                Angle = (float)_random.NextDouble() * MathHelper.TwoPi,
                Depth = (float)_random.NextDouble(),
                Speed = 0.3f + (float)_random.NextDouble() * 0.7f,
                Color = Color.Lerp(_primaryColor, _secondaryColor, (float)_random.NextDouble()),
                Size = 2f + (float)_random.NextDouble() * 4f,
                Phase = (float)_random.NextDouble() * MathHelper.TwoPi
            };
        }

        private SpeedLine CreateSpeedLine() {
            int w = _graphicsDevice.Viewport.Width;
            int h = _graphicsDevice.Viewport.Height;
            Vector2 center = new Vector2(w / 2f, h / 2f);

            float angle = (float)_random.NextDouble() * MathHelper.TwoPi;
            float dist = 50f + (float)_random.NextDouble() * Math.Max(w, h) * 0.6f;

            return new SpeedLine {
                Start = center + new Vector2((float)Math.Cos(angle), (float)Math.Sin(angle)) * dist,
                Length = 20f + (float)_random.NextDouble() * 80f,
                Speed = 300f + (float)_random.NextDouble() * 600f,
                Color = Color.Lerp(_primaryColor, Color.White, (float)_random.NextDouble() * 0.6f),
                Angle = angle
            };
        }

        /// <summary>
        /// Start the jump hole transit effect
        /// </summary>
        public void Start(float duration, string targetSystemName, Color primaryColor, Color secondaryColor) {
            IsActive = true;
            Duration = duration;
            _elapsed = 0f;
            Progress = 0f;
            _targetSystemName = targetSystemName;
            _primaryColor = primaryColor;
            _secondaryColor = secondaryColor;
            _colorShiftPhase = 0f;
            _shakeAmount = 0f;

            // Reinitialize particles with new colors
            InitializeParticles();

            Console.WriteLine($"[JUMP] Transit effect started - Duration: {duration}s, Target: {targetSystemName}");
        }

        /// <summary>
        /// Update the transit effect
        /// </summary>
        public void Update(float deltaTime) {
            if (!IsActive) return;

            _elapsed += deltaTime;
            Progress = MathHelper.Clamp(_elapsed / Duration, 0f, 1f);
            _colorShiftPhase += deltaTime * 3.0f;

            // Intensity ramps up then down
            float intensity = Progress < 0.1f ? Progress / 0.1f :
                             Progress > 0.9f ? (1.0f - Progress) / 0.1f :
                             1.0f;

            // Screen shake increases in the middle
            _shakeAmount = 5f * intensity * (0.5f + 0.5f * (float)Math.Sin(_colorShiftPhase * 8.0f));
            _shakeOffset = new Vector2(
                (float)_random.NextDouble() * _shakeAmount * 2 - _shakeAmount,
                (float)_random.NextDouble() * _shakeAmount * 2 - _shakeAmount
            );

            // Update tunnel particles (approach camera)
            for (int i = 0; i < _tunnelParticles.Length; i++) {
                _tunnelParticles[i].Depth += _tunnelParticles[i].Speed * deltaTime * (1.0f + intensity);
                _tunnelParticles[i].Angle += deltaTime * 0.5f; // Slow rotation

                if (_tunnelParticles[i].Depth > 1.0f) {
                    _tunnelParticles[i] = CreateTunnelParticle();
                    _tunnelParticles[i].Depth = 0f;
                }
            }

            // Update speed lines (streak outward from center)
            int w = _graphicsDevice.Viewport.Width;
            int h = _graphicsDevice.Viewport.Height;
            Vector2 center = new Vector2(w / 2f, h / 2f);

            for (int i = 0; i < _speedLines.Length; i++) {
                Vector2 dir = _speedLines[i].Start - center;
                if (dir.LengthSquared() > 0.01f) dir.Normalize();
                _speedLines[i].Start += dir * _speedLines[i].Speed * deltaTime * intensity;
                _speedLines[i].Length += _speedLines[i].Speed * deltaTime * 0.3f;

                // Reset if off screen
                float dist = Vector2.Distance(_speedLines[i].Start, center);
                if (dist > Math.Max(w, h)) {
                    _speedLines[i] = CreateSpeedLine();
                }
            }

            // Spawn energy arcs
            if (_energyArcs.Count < MaxEnergyArcs && _random.NextDouble() < 0.15 * intensity) {
                float arcAngle = (float)_random.NextDouble() * MathHelper.TwoPi;
                _energyArcs.Add(new EnergyArc {
                    StartAngle = arcAngle,
                    EndAngle = arcAngle + 0.3f + (float)_random.NextDouble() * 1.0f,
                    Depth = 0.3f + (float)_random.NextDouble() * 0.5f,
                    Color = Color.Lerp(_primaryColor, Color.White, 0.5f + (float)_random.NextDouble() * 0.5f),
                    Life = 0f,
                    MaxLife = 0.3f + (float)_random.NextDouble() * 0.5f
                });
            }

            // Update energy arcs
            for (int i = _energyArcs.Count - 1; i >= 0; i--) {
                var arc = _energyArcs[i];
                arc.Life += deltaTime;
                if (arc.Life >= arc.MaxLife) {
                    _energyArcs.RemoveAt(i);
                } else {
                    _energyArcs[i] = arc;
                }
            }

            // Check if transit is complete
            if (_elapsed >= Duration) {
                IsActive = false;
                Console.WriteLine("[JUMP] Transit effect completed");
            }
        }

        /// <summary>
        /// Draw the full-screen wormhole tunnel effect
        /// </summary>
        public void Draw() {
            if (!IsActive) return;

            int w = _graphicsDevice.Viewport.Width;
            int h = _graphicsDevice.Viewport.Height;
            Vector2 center = new Vector2(w / 2f, h / 2f) + _shakeOffset;

            float intensity = Progress < 0.1f ? Progress / 0.1f :
                             Progress > 0.9f ? (1.0f - Progress) / 0.1f :
                             1.0f;

            _spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.Additive);

            // Background - deep space blue/purple
            float bgAlpha = 0.9f * intensity;
            Color bgColor = Color.Lerp(new Color(5, 5, 20), new Color(20, 10, 40),
                0.5f + 0.5f * (float)Math.Sin(_colorShiftPhase * 0.5f));
            _spriteBatch.Draw(_pixel, new Rectangle(0, 0, w, h), bgColor * bgAlpha);

            // Draw tunnel rings (concentric circles receding into distance)
            DrawTunnelRings(center, w, h, intensity);

            // Draw speed lines
            DrawSpeedLines(center, intensity);

            // Draw tunnel particles
            DrawTunnelParticles(center, w, h, intensity);

            // Draw energy arcs
            DrawEnergyArcs(center, w, h, intensity);

            // Draw central bright core (vanishing point)
            DrawVanishingPoint(center, intensity);

            // Flash effect at start and end
            if (Progress < 0.05f) {
                float flashAlpha = 1.0f - (Progress / 0.05f);
                _spriteBatch.Draw(_pixel, new Rectangle(0, 0, w, h), Color.White * flashAlpha);
            }
            if (Progress > 0.95f) {
                float flashAlpha = (Progress - 0.95f) / 0.05f;
                _spriteBatch.Draw(_pixel, new Rectangle(0, 0, w, h), Color.White * flashAlpha);
            }

            // Draw system name appearing near end
            if (_font != null && Progress > 0.7f) {
                float textAlpha = (Progress - 0.7f) / 0.3f;
                string text = $"Entering {_targetSystemName}";
                Vector2 textSize = _font.MeasureString(text);
                Vector2 textPos = new Vector2(w / 2f - textSize.X / 2, h / 2f + 150);
                _spriteBatch.DrawString(_font, text, textPos, Color.White * textAlpha);
            }

            _spriteBatch.End();
        }

        private void DrawTunnelRings(Vector2 center, int w, int h, float intensity) {
            int ringCount = 12;
            float maxRadius = Math.Max(w, h) * 0.8f;

            for (int r = 0; r < ringCount; r++) {
                float depth = (float)r / ringCount;
                float animatedDepth = (depth + _colorShiftPhase * 0.1f) % 1.0f;

                // Rings get smaller as they go deeper (perspective)
                float radius = maxRadius * (1.0f - animatedDepth * 0.9f);
                float alpha = 0.15f * intensity * (1.0f - animatedDepth);

                // Color shifts along the tunnel
                Color ringColor = Color.Lerp(_primaryColor, _secondaryColor,
                    0.5f + 0.5f * (float)Math.Sin(animatedDepth * 4 + _colorShiftPhase));
                ringColor *= alpha;

                // Draw ring as segmented circle
                int segments = 48;
                float thickness = 2f + (1.0f - animatedDepth) * 3f;

                for (int s = 0; s < segments; s++) {
                    float angle1 = (float)(s * Math.PI * 2.0 / segments);
                    float angle2 = (float)((s + 1) * Math.PI * 2.0 / segments);

                    Vector2 p1 = center + new Vector2((float)Math.Cos(angle1), (float)Math.Sin(angle1)) * radius;
                    Vector2 p2 = center + new Vector2((float)Math.Cos(angle2), (float)Math.Sin(angle2)) * radius;

                    DrawLine2D(p1, p2, (int)thickness, ringColor);
                }
            }
        }

        private void DrawSpeedLines(Vector2 center, float intensity) {
            for (int i = 0; i < _speedLines.Length; i++) {
                var sl = _speedLines[i];
                Vector2 dir = sl.Start - center;
                if (dir.LengthSquared() > 0.01f) dir.Normalize();

                Vector2 end = sl.Start + dir * sl.Length;
                float distFromCenter = Vector2.Distance(sl.Start, center);
                float alpha = 0.3f * intensity * MathHelper.Clamp(distFromCenter / 200f, 0f, 1f);

                Color lineColor = sl.Color * alpha;

                DrawLine2D(sl.Start, end, 2, lineColor);
            }
        }

        private void DrawTunnelParticles(Vector2 center, int w, int h, float intensity) {
            float maxRadius = Math.Max(w, h) * 0.6f;

            for (int i = 0; i < _tunnelParticles.Length; i++) {
                var tp = _tunnelParticles[i];

                // Convert tunnel coordinate to screen position
                float perspectiveScale = 1.0f - tp.Depth * 0.9f;
                float screenRadius = maxRadius * perspectiveScale;
                Vector2 pos = center + new Vector2(
                    (float)Math.Cos(tp.Angle) * screenRadius,
                    (float)Math.Sin(tp.Angle) * screenRadius
                );

                // Size increases as particle approaches
                float size = tp.Size * (1.0f + tp.Depth * 3.0f);
                float alpha = 0.4f * intensity * tp.Depth;

                // Brightness pulse
                float pulse = 0.7f + 0.3f * (float)Math.Sin(tp.Phase + _colorShiftPhase * 2.0f);
                Color col = tp.Color * (alpha * pulse);

                // Draw as a small bright rectangle
                int halfSize = (int)(size / 2);
                _spriteBatch.Draw(_pixel, new Rectangle(
                    (int)pos.X - halfSize, (int)pos.Y - halfSize,
                    (int)size, (int)size
                ), col);
            }
        }

        private void DrawEnergyArcs(Vector2 center, int w, int h, float intensity) {
            float maxRadius = Math.Max(w, h) * 0.4f;

            foreach (var arc in _energyArcs) {
                float lifeRatio = arc.Life / arc.MaxLife;
                float alpha = lifeRatio < 0.3f ? lifeRatio / 0.3f : (1.0f - lifeRatio) / 0.7f;
                alpha *= intensity;

                float radius = maxRadius * arc.Depth;
                Color arcColor = arc.Color * alpha;

                int segments = 12;
                float angleSpan = arc.EndAngle - arc.StartAngle;

                for (int s = 0; s < segments; s++) {
                    float a1 = arc.StartAngle + angleSpan * s / segments;
                    float a2 = arc.StartAngle + angleSpan * (s + 1) / segments;

                    Vector2 p1 = center + new Vector2((float)Math.Cos(a1), (float)Math.Sin(a1)) * radius;
                    Vector2 p2 = center + new Vector2((float)Math.Cos(a2), (float)Math.Sin(a2)) * radius;

                    DrawLine2D(p1, p2, 3, arcColor);
                }
            }
        }

        private void DrawVanishingPoint(Vector2 center, float intensity) {
            // Bright core at center of tunnel
            float coreSize = 30f + 20f * (float)Math.Sin(_colorShiftPhase * 2.0f);
            float coreAlpha = 0.3f * intensity;

            // Multiple concentric glows
            for (int i = 5; i >= 0; i--) {
                float size = coreSize * (1.0f + i * 0.8f);
                float alpha = coreAlpha * (1.0f - i * 0.15f);
                Color coreColor = Color.Lerp(Color.White, _primaryColor, i * 0.15f) * alpha;

                int halfSize = (int)(size / 2);
                _spriteBatch.Draw(_pixel, new Rectangle(
                    (int)center.X - halfSize, (int)center.Y - halfSize,
                    (int)size, (int)size
                ), coreColor);
            }
        }

        private void DrawLine2D(Vector2 start, Vector2 end, int thickness, Color color) {
            Vector2 edge = end - start;
            float angle = (float)Math.Atan2(edge.Y, edge.X);
            float length = edge.Length();

            _spriteBatch.Draw(_pixel,
                new Rectangle((int)start.X, (int)start.Y, (int)length, thickness),
                null, color, angle, new Vector2(0, 0.5f), SpriteEffects.None, 0);
        }
    }
}
