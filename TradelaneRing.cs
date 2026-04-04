using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Linq;

namespace Roguelancer {
    /// <summary>
    /// Represents a single tradelane ring in space.
    /// Renders the TRACK_RING 3D model and an energy pulse effect when active.
    /// </summary>
    public class TradelaneRing : SpaceObject {
        public enum RingType { Start, Intermediate, End }

        private GraphicsDevice _graphicsDevice;
        private BasicEffect _pulseEffect;

        /// <summary>
        /// The 3D model for this ring
        /// </summary>
        public new Model Model { get; set; }

        /// <summary>
        /// Direction the lane faces (unit vector from start to end)
        /// </summary>
        public Vector3 LaneDirection { get; set; }

        /// <summary>
        /// Type of ring (Start, Intermediate, End)
        /// </summary>
        public RingType Type { get; set; }

        /// <summary>
        /// Index of this ring in the lane sequence
        /// </summary>
        public int Index { get; set; }

        /// <summary>
        /// Scale applied when drawing the model
        /// </summary>
        public float Scale { get; set; } = 5.0f;

        /// <summary>
        /// Energy color of the ring
        /// </summary>
        public Color EnergyColor { get; set; } = new Color(100, 200, 255);

        /// <summary>
        /// Whether this ring is currently pulsing (ship passing through)
        /// </summary>
        public bool IsPulsing { get; private set; }

        /// <summary>
        /// Whether this ring has been destroyed (breaks the lane)
        /// </summary>
        public bool IsDestroyed { get; set; }

        private float _pulseTimer;
        private const float PulseDuration = 0.6f;
        private float _ambientGlowPhase;

        // Pulse ring vertices for energy effect
        private VertexPositionColor[] _pulseVertices;
        private const int PulseSegments = 32;
        private float _pulseRadius = 60f;

        public TradelaneRing(GraphicsDevice graphicsDevice, string name, Vector3 position, Vector3 laneDirection, RingType type, int index, float scale, Color energyColor)
            : base(name, position, 100f) {
            _graphicsDevice = graphicsDevice;
            LaneDirection = laneDirection;
            Type = type;
            Index = index;
            Scale = scale;
            EnergyColor = energyColor;
            _ambientGlowPhase = index * 0.5f;

            _pulseEffect = new BasicEffect(graphicsDevice) {
                VertexColorEnabled = true,
                LightingEnabled = false,
                FogEnabled = false
            };

            _pulseVertices = new VertexPositionColor[(PulseSegments + 1) * 2];
        }

        /// <summary>
        /// Trigger a pulse effect (ship passing through)
        /// </summary>
        public void TriggerPulse() {
            IsPulsing = true;
            _pulseTimer = PulseDuration;
        }

        /// <summary>
        /// Update the ring state
        /// </summary>
        public void Update(GameTime gameTime) {
            if (IsDestroyed) return;

            float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
            _ambientGlowPhase += dt * 2f;

            if (IsPulsing) {
                _pulseTimer -= dt;
                if (_pulseTimer <= 0f) {
                    IsPulsing = false;
                    _pulseTimer = 0f;
                }
            }
        }

        /// <summary>
        /// Draw the 3D ring model
        /// </summary>
        public void Draw(Matrix view, Matrix projection, Vector3 lightDirection) {
            if (IsDestroyed || Model == null) return;

            // Orient the ring so that its "forward" axis aligns with the lane direction.
            // The TRACK_RING model's default forward is -Z, so we build a rotation
            // from that basis to LaneDirection.
            Matrix orientation = CreateOrientationMatrix(LaneDirection);

            var world = Matrix.CreateScale(Scale) *
                        orientation *
                        Matrix.CreateTranslation(Position);

            foreach (var mesh in Model.Meshes) {
                bool hasTextureCoordinates = mesh.MeshParts.Any(part =>
                    part.VertexBuffer.VertexDeclaration.GetVertexElements().Any(element =>
                        element.VertexElementUsage == VertexElementUsage.TextureCoordinate && element.UsageIndex == 0
                    )
                );

                foreach (BasicEffect effect in mesh.Effects) {
                    effect.World = world;
                    effect.View = view;
                    effect.Projection = projection;

                    if (hasTextureCoordinates && effect.Texture != null) {
                        effect.TextureEnabled = true;
                    } else {
                        effect.TextureEnabled = false;
                        effect.DiffuseColor = new Vector3(0.6f, 0.6f, 0.7f);
                    }

                    effect.EnableDefaultLighting();
                    effect.PreferPerPixelLighting = true;
                    effect.DirectionalLight0.Enabled = true;
                    effect.DirectionalLight0.Direction = lightDirection;
                    effect.DirectionalLight0.DiffuseColor = Vector3.One;
                    effect.DirectionalLight0.SpecularColor = new Vector3(0.3f, 0.3f, 0.3f);
                    effect.SpecularPower = 16f;
                    effect.AmbientLightColor = new Vector3(0.15f, 0.15f, 0.2f);

                    // Tint with energy color when pulsing
                    if (IsPulsing) {
                        float pulseIntensity = _pulseTimer / PulseDuration;
                        effect.EmissiveColor = EnergyColor.ToVector3() * pulseIntensity * 0.5f;
                    } else {
                        // Subtle ambient glow
                        float glow = (float)(Math.Sin(_ambientGlowPhase) * 0.05f + 0.05f);
                        effect.EmissiveColor = EnergyColor.ToVector3() * glow;
                    }
                }
                mesh.Draw();
            }
        }

        /// <summary>
        /// Draw the energy pulse ring effect (additive blending)
        /// </summary>
        public void DrawEnergyEffect(Matrix view, Matrix projection) {
            if (IsDestroyed) return;

            // Ambient glow ring
            float ambientAlpha = (float)(Math.Sin(_ambientGlowPhase) * 0.15f + 0.25f);
            float radius = _pulseRadius * Scale * 0.15f;

            if (IsPulsing) {
                float pulseProgress = 1f - (_pulseTimer / PulseDuration);
                radius *= (1f + pulseProgress * 0.5f);
                ambientAlpha = MathHelper.Lerp(1f, 0.2f, pulseProgress);
            }

            // Build a ring of line segments around the ring center, oriented along the lane
            Matrix orientation = CreateOrientationMatrix(LaneDirection);
            Vector3 up = Vector3.Transform(Vector3.Up, orientation);
            Vector3 right = Vector3.Transform(Vector3.Right, orientation);

            Color color = EnergyColor * ambientAlpha;

            for (int i = 0; i <= PulseSegments; i++) {
                float angle = (float)(i * Math.PI * 2.0 / PulseSegments);
                float nextAngle = (float)((i + 1) * Math.PI * 2.0 / PulseSegments);

                Vector3 p1 = Position + (up * (float)Math.Cos(angle) + right * (float)Math.Sin(angle)) * radius;
                Vector3 p2 = Position + (up * (float)Math.Cos(nextAngle) + right * (float)Math.Sin(nextAngle)) * radius;

                int idx = i * 2;
                if (idx + 1 < _pulseVertices.Length) {
                    _pulseVertices[idx] = new VertexPositionColor(p1, color);
                    _pulseVertices[idx + 1] = new VertexPositionColor(p2, color);
                }
            }

            _pulseEffect.View = view;
            _pulseEffect.Projection = projection;
            _pulseEffect.World = Matrix.Identity;

            var oldBlend = _graphicsDevice.BlendState;
            _graphicsDevice.BlendState = BlendState.Additive;

            foreach (var pass in _pulseEffect.CurrentTechnique.Passes) {
                pass.Apply();
                _graphicsDevice.DrawUserPrimitives(PrimitiveType.LineList, _pulseVertices, 0, PulseSegments);
            }

            _graphicsDevice.BlendState = oldBlend;
        }

        /// <summary>
        /// Build a rotation matrix that orients -Z to point along the given direction
        /// </summary>
        private Matrix CreateOrientationMatrix(Vector3 forward) {
            forward = Vector3.Normalize(forward);
            Vector3 tentativeUp = Math.Abs(Vector3.Dot(forward, Vector3.Up)) > 0.99f ? Vector3.Forward : Vector3.Up;
            Vector3 right = Vector3.Normalize(Vector3.Cross(tentativeUp, forward));
            Vector3 up = Vector3.Cross(forward, right);
            return new Matrix(
                right.X, right.Y, right.Z, 0,
                up.X, up.Y, up.Z, 0,
                forward.X, forward.Y, forward.Z, 0,
                0, 0, 0, 1
            );
        }
    }
}
