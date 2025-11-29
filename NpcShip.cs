using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;

namespace Roguelancer
{
    /// <summary>
    /// NPC ship with simple patrol behavior
    /// </summary>
    public class NpcShip : SpaceObject
    {
        public Matrix Orientation => Matrix.CreateFromQuaternion(_rotation);
        public Vector3 Velocity { get; set; }
        public float Speed { get; private set; }
        
        // Hull integrity
        public HullIntegrity Hull { get; private set; }
        public bool IsDestroyed => Hull.IsDestroyed;
        
        // Event to signal when the ship is destroyed
        public event Action<NpcShip> OnDestroyed;

        private float _patrolRadius;
        private Vector3 _patrolCenter;
        private float _patrolAngle;
        private float _patrolSpeed;
        private float _bobPhase;
        private float _bobSpeed;
        private Quaternion _rotation = Quaternion.Identity; // Use Quaternion instead of Matrix
        
        public Vector3 Forward => Vector3.Transform(Vector3.Forward, _rotation);
        public Vector3 Up => Vector3.Transform(Vector3.Up, _rotation);
        public Vector3 Right => Vector3.Transform(Vector3.Right, _rotation);
        
        public NpcShip(string name, Vector3 startPosition, Vector3 patrolCenter, float patrolRadius, float patrolSpeed)
            : base(name, startPosition, 10f)
        {
            _patrolCenter = patrolCenter;
            _patrolRadius = patrolRadius;
            _patrolSpeed = patrolSpeed;
            _bobPhase = (float)(new Random().NextDouble() * MathHelper.TwoPi);
            _bobSpeed = 0.3f + (float)(new Random().NextDouble() * 0.4f);
            
            // Initialize hull integrity
            Hull = new HullIntegrity(75f); // NPCs start with 75 hull points
            Hull.OnDestroyed += () =>
            {
                Console.WriteLine($"?? NPC SHIP '{Name}' DESTROYED!");
                OnDestroyed?.Invoke(this);
            };
            
            Console.WriteLine($"[NPC] {name} created with Hull: {Hull.CurrentHull}/{Hull.MaxHull}, IsDestroyed: {Hull.IsDestroyed}");
            
            // Initial orientation facing toward patrol center
            Vector3 toCenter = patrolCenter - startPosition;
            
            // Handle case where patrol center equals start position (static ships)
            if (toCenter.LengthSquared() < 0.0001f)
            {
                // Default to facing forward for static ships
                _rotation = Quaternion.Identity;
            }
            else
            {
                toCenter = Vector3.Normalize(toCenter);
                _rotation = CreateRotationFromDirection(toCenter);
            }
            
            // Calculate initial patrol angle
            Vector3 offset = startPosition - patrolCenter;
            _patrolAngle = (float)Math.Atan2(offset.X, offset.Z);
        }
        
        public void Update(GameTime gameTime)
        {
            if (IsDestroyed) return; // Don't update if destroyed

            float deltaTime = (float)gameTime.ElapsedGameTime.TotalSeconds;
            
            // Update patrol angle
            _patrolAngle += _patrolSpeed * deltaTime;
            
            // Update bob phase
            _bobPhase += _bobSpeed * deltaTime;
            
            // Calculate target position on patrol circle with vertical bobbing
            float bobHeight = (float)Math.Sin(_bobPhase) * (_patrolRadius * 0.2f);
            Vector3 targetPosition = _patrolCenter + new Vector3(
                (float)Math.Sin(_patrolAngle) * _patrolRadius,
                bobHeight,
                (float)Math.Cos(_patrolAngle) * _patrolRadius
            );
            
            // Calculate desired direction
            Vector3 toTarget = targetPosition - Position;
            float distanceToTarget = toTarget.Length();
            
            if (distanceToTarget > 1f)
            {
                Vector3 desiredDirection = Vector3.Normalize(toTarget);
                
                // Smoothly rotate toward desired direction using Quaternion
                Vector3 currentForward = Forward;
                Vector3 rotationAxis = Vector3.Cross(currentForward, desiredDirection);
                float rotationAxisLength = rotationAxis.Length();
                
                if (rotationAxisLength > 0.0001f)
                {
                    rotationAxis /= rotationAxisLength;
                    float angle = (float)Math.Acos(MathHelper.Clamp(Vector3.Dot(currentForward, desiredDirection), -1f, 1f));
                    float maxTurnRate = 1.5f * deltaTime;
                    float turnAngle = Math.Min(angle, maxTurnRate);
                    
                    // Apply rotation as Quaternion
                    Quaternion rotationDelta = Quaternion.CreateFromAxisAngle(rotationAxis, turnAngle);
                    _rotation = rotationDelta * _rotation;
                    _rotation.Normalize();
                }
                
                // Move toward target - MASSIVELY INCREASED SPEED (100x)
                Speed = MathHelper.Lerp(Speed, Math.Min(distanceToTarget * 20f, 20000f), deltaTime * 2f);
            }
            else
            {
                Speed = MathHelper.Lerp(Speed, 10000f, deltaTime * 2f); // Increased from 100f to 10000f
            }
            
            Velocity = Forward * Speed;
            Position += Velocity * deltaTime;
        }
        
        public void Draw(Matrix view, Matrix projection, Vector3 lightDirection)
        {
            if (Model == null || IsDestroyed) return;
            
            // Apply same model correction as player ship
            Matrix modelCorrection = Matrix.CreateRotationX(-MathHelper.PiOver2) * Matrix.CreateRotationY(MathHelper.Pi);
            Matrix world = modelCorrection * Orientation * Matrix.CreateTranslation(Position);
            
            // Get graphics device from first mesh's effect to set render states
            var graphicsDevice = Model.Meshes[0].Effects[0].GraphicsDevice;
            
            // Save current render states
            var oldBlendState = graphicsDevice.BlendState;
            var oldDepthStencilState = graphicsDevice.DepthStencilState;
            var oldRasterizerState = graphicsDevice.RasterizerState;
            
            // Force opaque rendering with proper depth testing
            graphicsDevice.BlendState = BlendState.Opaque;
            graphicsDevice.DepthStencilState = DepthStencilState.Default;
            graphicsDevice.RasterizerState = RasterizerState.CullCounterClockwise;
            
            foreach (ModelMesh mesh in Model.Meshes)
            {
                foreach (BasicEffect effect in mesh.Effects)
                {
                    effect.World = world;
                    effect.View = view;
                    effect.Projection = projection;
                    
                    effect.EnableDefaultLighting();
                    effect.PreferPerPixelLighting = true;
                    effect.SpecularPower = 16f;
                    
                    // Force full alpha - no transparency
                    effect.Alpha = 1.0f;
                    
                    effect.DirectionalLight0.Direction = lightDirection;
                    effect.DirectionalLight0.DiffuseColor = new Vector3(0.9f, 0.9f, 1.0f);
                    effect.DirectionalLight0.SpecularColor = new Vector3(0.5f, 0.5f, 0.6f);
                    effect.AmbientLightColor = new Vector3(0.2f, 0.2f, 0.25f);
                }
                
                mesh.Draw();
            }
            
            // Restore previous render states
            graphicsDevice.BlendState = oldBlendState;
            graphicsDevice.DepthStencilState = oldDepthStencilState;
            graphicsDevice.RasterizerState = oldRasterizerState;
        }
        
        private Quaternion CreateRotationFromDirection(Vector3 direction)
        {
            Vector3 forward = Vector3.Normalize(direction);
            Vector3 right = Vector3.Normalize(Vector3.Cross(Vector3.Up, forward));
            if (right.LengthSquared() < 0.01f) right = Vector3.Right;
            Vector3 up = Vector3.Cross(forward, right);
            
            Matrix rotationMatrix = new Matrix(
                right.X, right.Y, right.Z, 0f,
                up.X, up.Y, up.Z, 0f,
                forward.X, forward.Y, forward.Z, 0f,
                0f, 0f, 0f, 1f
            );
            
            return Quaternion.CreateFromRotationMatrix(rotationMatrix);
        }
    }
}
