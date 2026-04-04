using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Roguelancer.Configuration;
using System;
using System.Collections.Generic;

namespace Roguelancer {
    /// <summary>
    /// A jump hole portal that connects two star systems.
    /// Renders as a swirling ring of energy lights with a glowing core.
    /// </summary>
    public class JumpHole : SpaceObject {
        private GraphicsDevice _graphicsDevice;
        private BasicEffect _effect;
        private JumpHoleConfig _config;

        // Ring particles (orbiting energy orbs)
        private struct RingParticle {
            public float Angle;
            public float Speed;
            public float RadiusOffset;
            public float Size;
            public Color Color;
            public float Phase;
        }

        // Spiral energy tendrils
        private struct EnergyTendril {
            public float BaseAngle;
            public float Length;
            public float Speed;
            public Color Color;
            public float Phase;
        }

        // Core pulse particles (floating towards center)
        private struct CoreParticle {
            public Vector3 Offset;
            public float Life;
            public float MaxLife;
            public float Size;
            public Color Color;
            public float Speed;
        }

        private RingParticle[] _ringParticles;
        private EnergyTendril[] _tendrils;
        private List<CoreParticle> _coreParticles;
        private Random _random;
        private float _time;
        private float _pulsePhase;

        // Rendering buffers
        private VertexPositionColor[] _vertices;
        private int _vertexCount;
        private const int MaxVertices = 16384;

        // Visual settings
        private const int RingParticleCount = 48;
        private const int TendrilCount = 8;
        private const int MaxCoreParticles = 60;
        private const float BaseRingRadius = 200f;

        /// <summary>
        /// The configuration for this jump hole
        /// </summary>
        public JumpHoleConfig Config => _config;

        /// <summary>
        /// Whether the player is close enough to enter this jump hole
        /// </summary>
        public bool IsPlayerInRange { get; private set; }

        public JumpHole(GraphicsDevice graphicsDevice, JumpHoleConfig config)
            : base(config.Name, config.Position, config.Radius) {
            _graphicsDevice = graphicsDevice;
            _config = config;
            _random = new Random(config.Name.GetHashCode());
            _time = 0f;
            _pulsePhase = 0f;

            _vertices = new VertexPositionColor[MaxVertices];
            _vertexCount = 0;

            _effect = new BasicEffect(graphicsDevice) {
                VertexColorEnabled = true,
                LightingEnabled = false,
                FogEnabled = false
            };

            InitializeRingParticles();
            InitializeTendrils();
            _coreParticles = new List<CoreParticle>();
        }

        private void InitializeRingParticles() {
            _ringParticles = new RingParticle[RingParticleCount];
            for (int i = 0; i < RingParticleCount; i++) {
                float angle = (float)(i * Math.PI * 2.0 / RingParticleCount);
                _ringParticles[i] = new RingParticle {
                    Angle = angle,
                    Speed = 0.8f + (float)_random.NextDouble() * 0.6f,
                    RadiusOffset = -20f + (float)_random.NextDouble() * 40f,
                    Size = 4f + (float)_random.NextDouble() * 6f,
                    Color = Color.Lerp(_config.RingColor, Color.White, (float)_random.NextDouble() * 0.4f),
                    Phase = (float)_random.NextDouble() * MathHelper.TwoPi
                };
            }
        }

        private void InitializeTendrils() {
            _tendrils = new EnergyTendril[TendrilCount];
            for (int i = 0; i < TendrilCount; i++) {
                _tendrils[i] = new EnergyTendril {
                    BaseAngle = (float)(i * Math.PI * 2.0 / TendrilCount),
                    Length = 120f + (float)_random.NextDouble() * 80f,
                    Speed = 0.3f + (float)_random.NextDouble() * 0.4f,
                    Color = Color.Lerp(_config.CoreColor, _config.RingColor, (float)_random.NextDouble()),
                    Phase = (float)_random.NextDouble() * MathHelper.TwoPi
                };
            }
        }

        /// <summary>
        /// Update jump hole animation and check player proximity
        /// </summary>
        public void Update(GameTime gameTime, Vector3 playerPosition) {
            float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
            _time += dt;
            _pulsePhase += dt * 2.0f;

            // Update ring particles
            for (int i = 0; i < _ringParticles.Length; i++) {
                _ringParticles[i].Angle += _ringParticles[i].Speed * dt;
                if (_ringParticles[i].Angle > MathHelper.TwoPi)
                    _ringParticles[i].Angle -= MathHelper.TwoPi;
            }

            // Spawn core particles
            if (_coreParticles.Count < MaxCoreParticles && _random.NextDouble() < 0.3) {
                float angle = (float)_random.NextDouble() * MathHelper.TwoPi;
                float dist = BaseRingRadius * 0.3f + (float)_random.NextDouble() * BaseRingRadius * 0.7f;
                _coreParticles.Add(new CoreParticle {
                    Offset = new Vector3(
                        (float)Math.Cos(angle) * dist,
                        (float)Math.Sin(angle) * dist,
                        -10f + (float)_random.NextDouble() * 20f
                    ),
                    Life = 0f,
                    MaxLife = 1.5f + (float)_random.NextDouble() * 2.0f,
                    Size = 2f + (float)_random.NextDouble() * 4f,
                    Color = Color.Lerp(_config.CoreColor, Color.White, (float)_random.NextDouble() * 0.6f),
                    Speed = 30f + (float)_random.NextDouble() * 50f
                });
            }

            // Update core particles (drift towards center)
            for (int i = _coreParticles.Count - 1; i >= 0; i--) {
                var p = _coreParticles[i];
                p.Life += dt;

                // Move towards center
                Vector3 toCenter = -p.Offset;
                if (toCenter.LengthSquared() > 0.01f) {
                    toCenter.Normalize();
                    p.Offset += toCenter * p.Speed * dt;
                }

                if (p.Life >= p.MaxLife) {
                    _coreParticles.RemoveAt(i);
                } else {
                    _coreParticles[i] = p;
                }
            }

            // Check player proximity
            float distToPlayer = Vector3.Distance(Position, playerPosition);
            IsPlayerInRange = distToPlayer <= _config.ActivationRange;
        }

        /// <summary>
        /// Draw the jump hole portal effect
        /// </summary>
        public void Draw(Matrix view, Matrix projection, Vector3 cameraPosition) {
            _vertexCount = 0;

            // Calculate billboard matrix (face camera)
            Vector3 forward = cameraPosition - Position;
            if (forward.LengthSquared() < 0.001f) forward = Vector3.Forward;
            forward.Normalize();
            Vector3 up = Vector3.Up;
            Vector3 right = Vector3.Cross(up, forward);
            if (right.LengthSquared() < 0.001f) {
                up = Vector3.Right;
                right = Vector3.Cross(up, forward);
            }
            right.Normalize();
            up = Vector3.Cross(forward, right);
            up.Normalize();

            // Draw the outer energy ring
            DrawEnergyRing(right, up, forward);

            // Draw swirling tendrils
            DrawTendrils(right, up, forward);

            // Draw core glow
            DrawCoreGlow(right, up);

            // Draw core particles
            DrawCoreParticles(right, up);

            // Draw ring particles (orbiting energy orbs)
            DrawRingParticles(right, up);

            // Render all accumulated vertices
            if (_vertexCount >= 3) {
                _effect.View = view;
                _effect.Projection = projection;
                _effect.World = Matrix.Identity;

                // Save state
                var oldBlend = _graphicsDevice.BlendState;
                var oldDepth = _graphicsDevice.DepthStencilState;

                _graphicsDevice.BlendState = BlendState.Additive;
                _graphicsDevice.DepthStencilState = DepthStencilState.DepthRead;

                foreach (var pass in _effect.CurrentTechnique.Passes) {
                    pass.Apply();
                    _graphicsDevice.DrawUserPrimitives(
                        PrimitiveType.TriangleList,
                        _vertices,
                        0,
                        _vertexCount / 3
                    );
                }

                // Restore state
                _graphicsDevice.BlendState = oldBlend;
                _graphicsDevice.DepthStencilState = oldDepth;
            }
        }

        private void DrawEnergyRing(Vector3 right, Vector3 up, Vector3 forward) {
            // Draw a pulsing ring made of line segments
            int segments = 64;
            float pulse = 0.9f + 0.1f * (float)Math.Sin(_pulsePhase * 1.5f);
            float ringRadius = BaseRingRadius * pulse;

            for (int i = 0; i < segments; i++) {
                float angle1 = (float)(i * Math.PI * 2.0 / segments);
                float angle2 = (float)((i + 1) * Math.PI * 2.0 / segments);

                // Wobble the ring slightly
                float wobble1 = 5f * (float)Math.Sin(angle1 * 3 + _time * 2.0f);
                float wobble2 = 5f * (float)Math.Sin(angle2 * 3 + _time * 2.0f);

                float r1 = ringRadius + wobble1;
                float r2 = ringRadius + wobble2;

                Vector3 p1 = Position + right * (float)Math.Cos(angle1) * r1 + up * (float)Math.Sin(angle1) * r1;
                Vector3 p2 = Position + right * (float)Math.Cos(angle2) * r2 + up * (float)Math.Sin(angle2) * r2;

                // Inner ring (slightly smaller)
                float innerR1 = r1 * 0.92f;
                float innerR2 = r2 * 0.92f;
                Vector3 p3 = Position + right * (float)Math.Cos(angle1) * innerR1 + up * (float)Math.Sin(angle1) * innerR1;
                Vector3 p4 = Position + right * (float)Math.Cos(angle2) * innerR2 + up * (float)Math.Sin(angle2) * innerR2;

                // Color varies around ring
                float colorShift = (float)Math.Sin(angle1 * 2 + _time * 3.0f) * 0.3f + 0.7f;
                Color ringColor = Color.Lerp(_config.RingColor, Color.White, colorShift * 0.3f) * (0.6f * colorShift);

                AddQuad(p1, p2, p4, p3, ringColor);
            }
        }

        private void DrawTendrils(Vector3 right, Vector3 up, Vector3 forward) {
            for (int t = 0; t < _tendrils.Length; t++) {
                var tendril = _tendrils[t];
                float baseAngle = tendril.BaseAngle + _time * tendril.Speed;
                int steps = 16;

                for (int i = 0; i < steps - 1; i++) {
                    float frac1 = (float)i / steps;
                    float frac2 = (float)(i + 1) / steps;

                    // Spiral inward
                    float r1 = BaseRingRadius * (1.0f - frac1 * 0.7f);
                    float r2 = BaseRingRadius * (1.0f - frac2 * 0.7f);

                    float a1 = baseAngle + frac1 * MathHelper.TwoPi * 1.5f;
                    float a2 = baseAngle + frac2 * MathHelper.TwoPi * 1.5f;

                    Vector3 p1 = Position + right * (float)Math.Cos(a1) * r1 + up * (float)Math.Sin(a1) * r1;
                    Vector3 p2 = Position + right * (float)Math.Cos(a2) * r2 + up * (float)Math.Sin(a2) * r2;

                    // Width tapers towards center
                    float width1 = 6f * (1.0f - frac1 * 0.8f);
                    float width2 = 6f * (1.0f - frac2 * 0.8f);

                    Vector3 perp1 = Vector3.Cross(p2 - p1, forward);
                    if (perp1.LengthSquared() > 0.001f) perp1.Normalize();

                    float alpha = 0.5f * (1.0f - frac1);
                    Color col = tendril.Color * alpha;

                    AddQuad(
                        p1 + perp1 * width1,
                        p2 + perp1 * width2,
                        p2 - perp1 * width2,
                        p1 - perp1 * width1,
                        col
                    );
                }
            }
        }

        private void DrawCoreGlow(Vector3 right, Vector3 up) {
            // Central glowing disc
            float pulse = 0.7f + 0.3f * (float)Math.Sin(_pulsePhase * 2.0f);
            float coreRadius = BaseRingRadius * 0.6f * pulse;
            Color coreColor = _config.CoreColor * (0.15f * pulse);

            // Draw as series of concentric rings fading outward
            int rings = 8;
            for (int r = 0; r < rings; r++) {
                float frac = (float)r / rings;
                float radius = coreRadius * (1.0f - frac);
                float alpha = 0.12f * (1.0f - frac * 0.8f) * pulse;
                Color ringCol = Color.Lerp(_config.CoreColor, Color.White, frac * 0.5f) * alpha;

                int segments = 24;
                for (int i = 0; i < segments; i++) {
                    float a1 = (float)(i * Math.PI * 2.0 / segments);
                    float a2 = (float)((i + 1) * Math.PI * 2.0 / segments);

                    Vector3 p1 = Position + right * (float)Math.Cos(a1) * radius + up * (float)Math.Sin(a1) * radius;
                    Vector3 p2 = Position + right * (float)Math.Cos(a2) * radius + up * (float)Math.Sin(a2) * radius;

                    AddTriangle(Position, p1, p2, ringCol);
                }
            }
        }

        private void DrawCoreParticles(Vector3 right, Vector3 up) {
            foreach (var p in _coreParticles) {
                float lifeRatio = p.Life / p.MaxLife;
                float alpha = lifeRatio < 0.2f ? lifeRatio / 0.2f : (1.0f - lifeRatio) / 0.8f;
                alpha = MathHelper.Clamp(alpha, 0f, 1f);

                Vector3 worldPos = Position + right * p.Offset.X + up * p.Offset.Y;
                float size = p.Size * alpha;
                Color col = p.Color * (alpha * 0.6f);

                // Draw as small quad
                AddQuad(
                    worldPos + right * (-size) + up * size,
                    worldPos + right * size + up * size,
                    worldPos + right * size + up * (-size),
                    worldPos + right * (-size) + up * (-size),
                    col
                );
            }
        }

        private void DrawRingParticles(Vector3 right, Vector3 up) {
            for (int i = 0; i < _ringParticles.Length; i++) {
                var rp = _ringParticles[i];
                float radius = BaseRingRadius + rp.RadiusOffset;

                // Pulsing brightness
                float brightness = 0.6f + 0.4f * (float)Math.Sin(rp.Phase + _time * 3.0f);

                Vector3 pos = Position
                    + right * (float)Math.Cos(rp.Angle) * radius
                    + up * (float)Math.Sin(rp.Angle) * radius;

                float size = rp.Size * brightness;
                Color col = rp.Color * (brightness * 0.8f);

                // Bright orb quad
                AddQuad(
                    pos + right * (-size) + up * size,
                    pos + right * size + up * size,
                    pos + right * size + up * (-size),
                    pos + right * (-size) + up * (-size),
                    col
                );

                // Glow halo (larger, dimmer)
                float haloSize = size * 2.5f;
                Color haloCol = col * 0.2f;
                AddQuad(
                    pos + right * (-haloSize) + up * haloSize,
                    pos + right * haloSize + up * haloSize,
                    pos + right * haloSize + up * (-haloSize),
                    pos + right * (-haloSize) + up * (-haloSize),
                    haloCol
                );
            }
        }

        private void AddTriangle(Vector3 p1, Vector3 p2, Vector3 p3, Color color) {
            if (_vertexCount + 3 > MaxVertices) return;
            _vertices[_vertexCount++] = new VertexPositionColor(p1, color);
            _vertices[_vertexCount++] = new VertexPositionColor(p2, color);
            _vertices[_vertexCount++] = new VertexPositionColor(p3, color);
        }

        private void AddQuad(Vector3 p1, Vector3 p2, Vector3 p3, Vector3 p4, Color color) {
            if (_vertexCount + 6 > MaxVertices) return;
            // Triangle 1
            _vertices[_vertexCount++] = new VertexPositionColor(p1, color);
            _vertices[_vertexCount++] = new VertexPositionColor(p2, color);
            _vertices[_vertexCount++] = new VertexPositionColor(p3, color);
            // Triangle 2
            _vertices[_vertexCount++] = new VertexPositionColor(p1, color);
            _vertices[_vertexCount++] = new VertexPositionColor(p3, color);
            _vertices[_vertexCount++] = new VertexPositionColor(p4, color);
        }
    }
}
