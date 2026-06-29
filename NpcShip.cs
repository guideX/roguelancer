using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;

namespace Roguelancer
{
    /// <summary>
    /// Compact ambient traffic behaviors used by the encounter manager.
    /// </summary>
    public enum TrafficZoneBehaviorType
    {
        LawfulPatrol,
        TraderRoute,
        PirateAmbush,
        StationTraffic
    }

    /// <summary>
    /// NPC ship with simple patrol behavior
    /// </summary>
    public class NpcShip : SpaceObject
    {
        public Matrix Orientation => Matrix.CreateFromQuaternion(_rotation);
        public Vector3 Velocity { get; set; }
        public float Speed { get; private set; }
        public Matrix ModelRotationCorrection { get; set; } = Matrix.Identity;
        public string ModelPath { get; set; }
        public string FactionId { get; set; }
        public TrafficZoneBehaviorType TrafficBehavior { get; private set; } = TrafficZoneBehaviorType.LawfulPatrol;
        public string TrafficZoneId { get; private set; } = string.Empty;
        public float TrafficLifetimeSeconds { get; set; } = 0f;
        public float TrafficAgeSeconds { get; private set; } = 0f;
        public float TrafficCruiseSpeed { get; private set; } = 160f;
        public float TrafficActivationRange { get; private set; } = 6500f;
        public float TrafficLoiterRadius { get; private set; } = 900f;
        public Vector3? TrafficRouteStart { get; private set; }
        public Vector3? TrafficRouteEnd { get; private set; }

        // Hull integrity
        public HullIntegrity Hull { get; private set; }
        public bool IsDestroyed => Hull.IsDestroyed;

        // Shield system
        public ShieldSystem Shields { get; private set; }
        
        // Event to signal when the ship is destroyed
        public event Action<NpcShip> OnDestroyed;

        private float _patrolRadius;
        private Vector3 _patrolCenter;
        private float _patrolAngle;
        private float _patrolSpeed;
        private float _bobPhase;
        private float _bobSpeed;
        private float _trafficRouteHoldTimer;
        private bool _trafficRouteTowardEnd = true;
        private Quaternion _rotation = Quaternion.Identity; // Use Quaternion instead of Matrix
        
        public Vector3 Forward => Vector3.Transform(Vector3.Forward, _rotation);
        public Vector3 Up => Vector3.Transform(Vector3.Up, _rotation);
        public Vector3 Right => Vector3.Transform(Vector3.Right, _rotation);
        
        public NpcShip(string name, Vector3 startPosition, Vector3 patrolCenter, float patrolRadius, float patrolSpeed, string factionId = null)
            : base(name, startPosition, 10f)
        {
            _patrolCenter = patrolCenter;
            _patrolRadius = patrolRadius;
            _patrolSpeed = patrolSpeed;
            FactionId = FactionManager.NormalizeFactionId(factionId);
            _bobPhase = (float)(new Random().NextDouble() * MathHelper.TwoPi);
            _bobSpeed = 0.3f + (float)(new Random().NextDouble() * 0.4f);
            
            // Initialize hull integrity
            Hull = new HullIntegrity(75f); // NPCs start with 75 hull points
            Hull.OnDestroyed += () =>
            {
                Console.WriteLine($"NPC SHIP '{Name}' DESTROYED!");
                OnDestroyed?.Invoke(this);
            };
            
            Console.WriteLine($"[NPC] {name} created with Hull: {Hull.CurrentHull}/{Hull.MaxHull}, IsDestroyed: {Hull.IsDestroyed}");
            
            // Initialize shield system for NPC ships
            Shields = new ShieldSystem(40f, 10f, 4f); // NPCs get 40 shields, slower regen
            
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

        public void ConfigureTrafficBehavior(
            TrafficZoneBehaviorType behaviorType,
            string trafficZoneId,
            Vector3 anchorPoint,
            float loiterRadius,
            float cruiseSpeed,
            float activationRange = 6500f,
            Vector3? routeStart = null,
            Vector3? routeEnd = null)
        {
            TrafficBehavior = behaviorType;
            TrafficZoneId = trafficZoneId ?? string.Empty;
            _patrolCenter = anchorPoint;
            TrafficLoiterRadius = Math.Max(100f, loiterRadius);
            TrafficCruiseSpeed = Math.Max(20f, cruiseSpeed);
            TrafficActivationRange = Math.Max(100f, activationRange);
            TrafficRouteStart = routeStart;
            TrafficRouteEnd = routeEnd;
            _trafficRouteHoldTimer = 0f;

            if (routeStart.HasValue && routeEnd.HasValue)
            {
                _trafficRouteTowardEnd = Vector3.DistanceSquared(Position, routeStart.Value) <= Vector3.DistanceSquared(Position, routeEnd.Value);
                if (Vector3.DistanceSquared(Position, routeStart.Value) < 25f)
                {
                    _trafficRouteTowardEnd = true;
                }
                else if (Vector3.DistanceSquared(Position, routeEnd.Value) < 25f)
                {
                    _trafficRouteTowardEnd = false;
                }
            }
        }
        
        public void Update(GameTime gameTime, DamageSmokeParticles damageSmoke, Ship playerShip = null, ReputationManager reputationManager = null)
        {
            if (IsDestroyed) return; // Don't update if destroyed

            float deltaTime = (float)gameTime.ElapsedGameTime.TotalSeconds;

            if (TrafficLifetimeSeconds > 0f || !string.IsNullOrWhiteSpace(TrafficZoneId))
            {
                TrafficAgeSeconds += deltaTime;
            }

            // Update shield regeneration
            Shields?.Update(gameTime);

            // Emit damage smoke if hull is low
            DamageStage damageStage = DamageStage.None;
            if (Hull.HullPercentage <= 0.75f && Hull.HullPercentage > 0.50f)
            {
                damageStage = DamageStage.Light;
            }
            else if (Hull.HullPercentage <= 0.50f && Hull.HullPercentage > 0.25f)
            {
                damageStage = DamageStage.Heavy;
            }
            else if (Hull.HullPercentage <= 0.25f && Hull.HullPercentage > 0)
            {
                damageStage = DamageStage.Critical;
            }

            if (damageStage != DamageStage.None)
            {
                damageSmoke?.Emit(Position - Forward * 15, Velocity, damageStage);
            }
            
            switch (TrafficBehavior)
            {
                case TrafficZoneBehaviorType.TraderRoute:
                    UpdateTraderRouteBehavior(deltaTime);
                    break;
                case TrafficZoneBehaviorType.PirateAmbush:
                    UpdatePirateAmbushBehavior(deltaTime, playerShip, reputationManager);
                    break;
                case TrafficZoneBehaviorType.StationTraffic:
                    UpdateCircularPatrolBehavior(deltaTime, TrafficLoiterRadius * 0.45f, TrafficCruiseSpeed * 0.75f, 0.9f);
                    break;
                default:
                    UpdateCircularPatrolBehavior(deltaTime, _patrolRadius, Math.Max(TrafficCruiseSpeed, 200f), _patrolSpeed);
                    break;
            }
        }
        
        public void Draw(Matrix view, Matrix projection, Vector3 lightDirection)
        {
            if (Model == null || IsDestroyed) return;
            
            // Match player ship rendering exactly: scale, correction, then orientation
            Matrix modelScale = Matrix.CreateScale(0.1f);
            Matrix modelCorrection = Matrix.CreateRotationX(-MathHelper.PiOver2) * Matrix.CreateRotationY(MathHelper.Pi);
            Matrix world = modelScale * modelCorrection * ModelRotationCorrection * Orientation * Matrix.CreateTranslation(Position);
            
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
                    effect.Alpha = 1.0f;
                    
                    effect.DirectionalLight0.Direction = lightDirection;
                    effect.DirectionalLight0.DiffuseColor = new Vector3(0.9f, 0.9f, 1.0f);
                    effect.DirectionalLight0.SpecularColor = new Vector3(0.5f, 0.5f, 0.6f);
                    effect.AmbientLightColor = new Vector3(0.2f, 0.2f, 0.25f);
                }
                
                mesh.Draw();
            }
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

        private void UpdateCircularPatrolBehavior(float deltaTime, float radius, float cruiseSpeed, float patrolSpeed)
        {
            _patrolAngle += patrolSpeed * deltaTime;
            _bobPhase += _bobSpeed * deltaTime;

            float bobHeight = (float)Math.Sin(_bobPhase) * (radius * 0.2f);
            Vector3 targetPosition = _patrolCenter + new Vector3(
                (float)Math.Sin(_patrolAngle) * radius,
                bobHeight,
                (float)Math.Cos(_patrolAngle) * radius
            );

            MoveTowardTarget(targetPosition, cruiseSpeed, deltaTime, 1.5f);
        }

        private void UpdateTraderRouteBehavior(float deltaTime)
        {
            if (!TrafficRouteStart.HasValue || !TrafficRouteEnd.HasValue)
            {
                UpdateCircularPatrolBehavior(deltaTime, _patrolRadius, Math.Max(TrafficCruiseSpeed, 160f), _patrolSpeed);
                return;
            }

            Vector3 target = _trafficRouteTowardEnd ? TrafficRouteEnd.Value : TrafficRouteStart.Value;
            float distance = Vector3.Distance(Position, target);
            if (distance <= 180f)
            {
                _trafficRouteHoldTimer += deltaTime;
                Speed = MathHelper.Lerp(Speed, 40f, deltaTime * 2f);
                if (_trafficRouteHoldTimer >= 1.0f)
                {
                    _trafficRouteTowardEnd = !_trafficRouteTowardEnd;
                    _trafficRouteHoldTimer = 0f;
                }
            }
            else
            {
                _trafficRouteHoldTimer = 0f;
                MoveTowardTarget(target, TrafficCruiseSpeed, deltaTime, 1.2f);
                return;
            }

            Velocity = Forward * Speed;
            Position += Velocity * deltaTime;
        }

        private void UpdatePirateAmbushBehavior(float deltaTime, Ship playerShip, ReputationManager reputationManager)
        {
            bool isHostile = reputationManager == null || reputationManager.IsHostile(FactionId);
            if (playerShip != null && isHostile)
            {
                float distanceToPlayer = Vector3.Distance(Position, playerShip.Position);
                if (distanceToPlayer <= TrafficActivationRange)
                {
                    MoveTowardTarget(playerShip.Position, TrafficCruiseSpeed * 1.15f, deltaTime, 1.7f);
                    return;
                }
            }

            UpdateCircularPatrolBehavior(deltaTime, TrafficLoiterRadius, Math.Max(80f, TrafficCruiseSpeed * 0.55f), 0.45f);
        }

        private void MoveTowardTarget(Vector3 targetPosition, float cruiseSpeed, float deltaTime, float maxTurnRate)
        {
            Vector3 toTarget = targetPosition - Position;
            float distanceToTarget = toTarget.Length();

            if (distanceToTarget > 1f)
            {
                Vector3 desiredDirection = Vector3.Normalize(toTarget);

                Vector3 currentForward = Forward;
                Vector3 rotationAxis = Vector3.Cross(currentForward, desiredDirection);
                float rotationAxisLength = rotationAxis.Length();

                if (rotationAxisLength > 0.0001f)
                {
                    rotationAxis /= rotationAxisLength;
                    float angle = (float)Math.Acos(MathHelper.Clamp(Vector3.Dot(currentForward, desiredDirection), -1f, 1f));
                    float turnAngle = Math.Min(angle, maxTurnRate * deltaTime);

                    Quaternion rotationDelta = Quaternion.CreateFromAxisAngle(rotationAxis, turnAngle);
                    _rotation = rotationDelta * _rotation;
                    _rotation.Normalize();
                }

                Speed = MathHelper.Lerp(Speed, Math.Min(distanceToTarget * 20f, cruiseSpeed), deltaTime * 2f);
            }
            else
            {
                Speed = MathHelper.Lerp(Speed, 0f, deltaTime * 2f);
            }

            Velocity = Forward * Speed;
            Position += Velocity * deltaTime;
        }
    }
}
