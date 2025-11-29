using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System;

namespace Roguelancer
{
    /// <summary>
    /// Player ship with Freelancer-style flight mechanics
    /// </summary>
    public class Ship
    {
        // Position and orientation
        public Vector3 Position { get; set; }
        public Matrix Orientation { get; private set; }
        public Vector3 Velocity { get; set; }
        
        // Movement properties
        public float Speed { get; private set; }
        public float MaxSpeed { get; set; } = 250f;
        public float MaxReverseSpeed { get; set; } = 150f;
        public float CruiseSpeed { get; set; } = 600f;
        public float AfterburnerSpeed { get; set; } = 500f;
        public float Acceleration { get; set; } = 150f;
        public float TurnSpeed { get; set; } = 1.5f;
        public float BankAmount { get; set; } = 1.2f;
        public float StrafeSpeed { get; set; } = 250f;
        
        // Energy system
        public ShipEnergy Energy { get; private set; }
        
        // Ship state
        private float _currentBankAngle = 0f;
        private float _currentPitch = 0f;
        private float _throttle = 0f;
        private float _targetSpeed = 0f;
        private bool _wasEnginesKilled = false;
        private Quaternion _rotation = Quaternion.Identity;

        // Mouse flight mode
        private bool _isFreeFlightMode = false;
        private Vector2 _lastMousePosition;
        private bool _mouseFlightInitialized = false;
        private ButtonState _prevLeftMouseState = ButtonState.Released;
        private bool _shouldAutoLevel = false;
        private float _autoLevelSpeed = 1.5f;

        // Special states
        public bool IsAfterburnerActive { get; private set; }
        public bool IsCruiseActive { get; private set; }
        public bool EnginesKilled { get; private set; }
        public bool AfterburnerJustActivated { get; private set; }
        public bool IsFreeFlightMode => _isFreeFlightMode;
        
        // Cruise charge system
        public bool IsCruiseCharging { get; private set; }
        public float CruiseChargeProgress => _cruiseChargeTimer / CruiseChargeTime;
        private float _cruiseChargeTimer = 0f;
        private const float CruiseChargeTime = 3f;
        private const float CruiseLungeTime = 0.8f;
        private const float CruiseChargePhase = 1.5f;
        private const float CruiseBurstTime = 0.7f;

        // Keyboard state tracking
        private KeyboardState _previousKeyboardState;
        
        // Ship model
        public Model Model { get; set; }
        public Matrix ModelRotationCorrection { get; set; } = Matrix.Identity;

        // Hull integrity
        public HullIntegrity Hull { get; private set; }
        public float CollisionRadius { get; set; } = 10f;
        
        // Direction vectors
        public Vector3 Forward => Vector3.Transform(Vector3.Forward, _rotation);
        public Vector3 Up => Vector3.Transform(Vector3.Up, _rotation);
        public Vector3 Right => Vector3.Transform(Vector3.Right, _rotation);

        // Visual tilts
        private float _pitchTiltAngle = 0f;
        private const float PitchTiltAmount = 0.15f;
        private float _bankTiltAngle = 0f;
        private const float BankTiltAmount = 0.25f;

        // Combat & targeting
        private bool _isDocking = false;
        private object _currentTarget = null;
        
        // Autopilot / GOTO
        private bool _gotoActive = false;
        private SpaceObject _gotoTarget = null;
        public bool IsGotoActive => _gotoActive;
        public SpaceObject CurrentGotoTarget => _gotoTarget;
        
        // Newtonian flight mode
        private bool _newtonianMode = false;
        private Vector3 _newtonianVelocity = Vector3.Zero;
        public bool IsNewtonianMode => _newtonianMode;

        // Notification System
        private NotificationManager _notificationManager;
        
        // Explosion System
        private ExplosionParticles _explosionParticles;
        
        // Damage Smoke System
        private DamageSmokeParticles _damageSmokeParticles;

        public Ship(Vector3 startPosition)
        {
            Position = startPosition;
            Orientation = Matrix.CreateFromQuaternion(_rotation);
            Velocity = Vector3.Zero;
            _previousKeyboardState = Keyboard.GetState();
            
            Hull = new HullIntegrity(100f);
            Hull.OnDestroyed += () =>
            {
                Console.WriteLine("💀 PLAYER SHIP DESTROYED!");
                _notificationManager?.ShowMessage("SHIP DESTROYED", 5f);
                // Trigger player ship explosion
                _explosionParticles?.TriggerExplosion(Position, Velocity, intensity: 1.5f);
            };

            InitializeEnergy();
        }

        public void SetNotificationManager(NotificationManager manager)
        {
            _notificationManager = manager;
        }
        
        /// <summary>
        /// Set the explosion particles system for this ship
        /// </summary>
        public void SetExplosionSystem(ExplosionParticles explosionParticles)
        {
            _explosionParticles = explosionParticles;
        }

        /// <summary>
        /// Set the damage smoke particles system for this ship
        /// </summary>
        public void SetDamageSmokeSystem(DamageSmokeParticles damageSmokeParticles)
        {
            _damageSmokeParticles = damageSmokeParticles;
        }

        /// <summary>
        /// Initialize the ship's energy system
        /// </summary>
        public void InitializeEnergy(float maxEnergy = 200f, float regenRate = 50f, float regenDelay = 2f)
        {
            Energy = new ShipEnergy(maxEnergy, regenRate, regenDelay);
        }
        
        /// <summary>
        /// Update ship's energy system (regeneration)
        /// </summary>
        public void UpdateEnergy(GameTime gameTime)
        {
            Energy?.Update(gameTime);
        }

        // Stub action methods
        private void FireActiveWeapons() { Console.WriteLine("Fire weapons (stub)"); }
        private void LaunchMissile() { Console.WriteLine("Launch missile (stub)"); }
        private void LaunchTorpedo() { Console.WriteLine("Launch torpedo (stub)"); }
        private void LaunchMine() { Console.WriteLine("Launch mine (stub)"); }
        private void LaunchCountermeasures() { Console.WriteLine("Launch countermeasures (stub)"); }
        private void Dock() { _isDocking = true; Console.WriteLine("Dock command (stub)"); }
        private void TargetClosestEnemy() { Console.WriteLine("Target closest enemy (stub)"); }
        private void PreviousEnemyTarget() { Console.WriteLine("Previous enemy target (stub)"); }
        private void NextEnemyTarget() { Console.WriteLine("Next enemy target (stub)"); }
        private void NextTarget() { Console.WriteLine("Next target (stub)"); }
        private void PreviousTarget() { Console.WriteLine("Previous target (stub)"); }
        private void ClearTarget() { _currentTarget = null; Console.WriteLine("Clear target (stub)"); }

        public void Update(GameTime gameTime, KeyboardState keyboardState)
        {
            float deltaTime = (float)gameTime.ElapsedGameTime.TotalSeconds;
            MouseState mouseState = Mouse.GetState();
            
            // Update energy system
            UpdateEnergy(gameTime);

            // Emit damage smoke if hull is low
            if (Hull.HullPercentage > 0 && Hull.HullPercentage <= 0.25f) // From 25% hull
            {
                _damageSmokeParticles?.Emit(Position - Forward * 15, Velocity);
            }
            
            bool spacebarPressed = keyboardState.IsKeyDown(Keys.Space) && _previousKeyboardState.IsKeyUp(Keys.Space);
            bool leftMouseHeld = mouseState.LeftButton == ButtonState.Pressed;
            bool leftMouseClicked = leftMouseHeld && _prevLeftMouseState == ButtonState.Released;
            
            bool zPressed = keyboardState.IsKeyDown(Keys.Z) && _previousKeyboardState.IsKeyUp(Keys.Z);
            bool bPressed = keyboardState.IsKeyDown(Keys.B) && _previousKeyboardState.IsKeyUp(Keys.B);
            bool xPressed = keyboardState.IsKeyDown(Keys.X) && _previousKeyboardState.IsKeyUp(Keys.X);
            bool f3Pressed = keyboardState.IsKeyDown(Keys.F3) && _previousKeyboardState.IsKeyUp(Keys.F3);
            bool rPressed = keyboardState.IsKeyDown(Keys.R) && _previousKeyboardState.IsKeyUp(Keys.R);
            bool ctrlRPressed = keyboardState.IsKeyDown(Keys.LeftControl) && rPressed;
            bool shiftRPressed = keyboardState.IsKeyDown(Keys.LeftShift) && rPressed;
            bool tPressed = keyboardState.IsKeyDown(Keys.T) && _previousKeyboardState.IsKeyUp(Keys.T);
            bool shiftTPressed = keyboardState.IsKeyDown(Keys.LeftShift) && tPressed;
            bool ctrlTPressed = keyboardState.IsKeyDown(Keys.LeftControl) && tPressed;
            
            if (keyboardState.IsKeyDown(Keys.Escape) && _previousKeyboardState.IsKeyUp(Keys.Escape))
            {
                if (_isFreeFlightMode)
                {
                    _isFreeFlightMode = false;
                    _notificationManager?.ShowMessage("Mouse Mode");
                }

                if (IsCruiseActive || IsCruiseCharging)
                {
                    IsCruiseActive = false;
                    IsCruiseCharging = false;
                    _cruiseChargeTimer = 0f;
                    _notificationManager?.ShowMessage("Cruise Mode Deactivated");
                    Console.WriteLine("ESC: Cruise mode cancelled");
                }
                if (_gotoActive) CancelGoto();
                if (IsAfterburnerActive)
                {
                    IsAfterburnerActive = false;
                    _notificationManager?.ShowMessage("Afterburner Deactivated");
                }
            }
            
            if (spacebarPressed)
            {
                _isFreeFlightMode = !_isFreeFlightMode;
                if (_isFreeFlightMode)
                {
                    _notificationManager?.ShowMessage("Free Flight Mode");
                    Console.WriteLine("FREE FLIGHT MODE - Ship follows mouse");
                }
                else
                {
                    _notificationManager?.ShowMessage("Mouse Mode");
                    Console.WriteLine("MOUSE MODE - Mouse is a cursor");
                }
            }
            
            bool fireWeapons = (mouseState.RightButton == ButtonState.Pressed && _prevLeftMouseState == ButtonState.Released) ||
                              (keyboardState.IsKeyDown(Keys.LeftControl) && !keyboardState.IsKeyDown(Keys.T));
            
            if (fireWeapons) FireActiveWeapons();
            
            bool wasAfterburnerActive = IsAfterburnerActive;
            bool cruiseKeyPressed = keyboardState.IsKeyDown(Keys.LeftShift) && keyboardState.IsKeyDown(Keys.W);

            // 1. Handle Afterburner state (TAB key) - Hold to activate
            bool tabHeld = keyboardState.IsKeyDown(Keys.Tab);
            bool tabJustPressed = tabHeld && _previousKeyboardState.IsKeyUp(Keys.Tab);
            bool tabJustReleased = !tabHeld && _previousKeyboardState.IsKeyDown(Keys.Tab);
            
            if (tabJustPressed && !IsAfterburnerActive)
            {
                // Activate afterburner when TAB is first pressed
                IsAfterburnerActive = true;
                IsCruiseActive = false;
                IsCruiseCharging = false;
                _cruiseChargeTimer = 0f;
                _notificationManager?.ShowMessage("Afterburner Engaged");
            }
            else if (tabJustReleased && IsAfterburnerActive)
            {
                // Deactivate afterburner when TAB is released
                IsAfterburnerActive = false;
                _notificationManager?.ShowMessage("Afterburner Disengaged");
            }

            if (cruiseKeyPressed && (_previousKeyboardState.IsKeyUp(Keys.W) || _previousKeyboardState.IsKeyUp(Keys.LeftShift)))
            {
                if (!IsCruiseActive && !IsCruiseCharging)
                {
                    IsCruiseCharging = true;
                    _cruiseChargeTimer = 0f;
                    IsAfterburnerActive = false;
                    _notificationManager?.ShowMessage("Cruise Charging");
                }
                else
                {
                    IsCruiseActive = false;
                    IsCruiseCharging = false;
                    _cruiseChargeTimer = 0f;
                    _notificationManager?.ShowMessage("Cruise Deactivated");
                }
            }

            AfterburnerJustActivated = IsAfterburnerActive && !wasAfterburnerActive;
            if (AfterburnerJustActivated)
            {
                _notificationManager?.ShowMessage("Afterburner Engaged");
            }
            
            if (zPressed)
            {
                EnginesKilled = !EnginesKilled;
                _notificationManager?.ShowMessage(EnginesKilled ? "Engines Killed" : "Engines Online");
                Console.WriteLine("Engine kill (Z): " + (EnginesKilled ? "ENGAGED" : "DISENGAGED"));
                if (EnginesKilled)
                {
                    _throttle = 0f; 
                    _targetSpeed = 0f; 
                    IsAfterburnerActive = false; 
                    IsCruiseActive = false;
                    IsCruiseCharging = false;
                    _wasEnginesKilled = true;
                }
            }
            
            if (bPressed) ToggleNewtonianMode();
            
            if (xPressed && !EnginesKilled)
            {
                _throttle = -1.0f;
                _notificationManager?.ShowMessage("Reverse Thrusters");
                Console.WriteLine("Reverse thrust engaged");
            }
            
            if (f3Pressed) Dock();
            
            if (keyboardState.IsKeyDown(Keys.Q) && _previousKeyboardState.IsKeyUp(Keys.Q))
            {
                if (keyboardState.IsKeyDown(Keys.LeftShift) || keyboardState.IsKeyDown(Keys.RightShift)) LaunchTorpedo();
                else LaunchMissile();
            }
            if (keyboardState.IsKeyDown(Keys.E) && _previousKeyboardState.IsKeyUp(Keys.E)) LaunchMine();
            if (keyboardState.IsKeyDown(Keys.C) && _previousKeyboardState.IsKeyUp(Keys.C)) LaunchCountermeasures();
            
            if (rPressed && !shiftRPressed && !ctrlRPressed) TargetClosestEnemy();
            if (shiftRPressed) NextEnemyTarget();
            if (ctrlRPressed) PreviousEnemyTarget();
            
            if (tPressed && !shiftTPressed && !ctrlTPressed) NextTarget();
            if (shiftTPressed) PreviousTarget();
            if (ctrlTPressed) ClearTarget();
            
            if (!EnginesKilled)
            {
                if ((keyboardState.IsKeyDown(Keys.W) || keyboardState.IsKeyDown(Keys.S)) && _gotoActive)
                {
                    CancelGoto();
                    Console.WriteLine("Manual throttle input cancelled GOTO.");
                }

                if (keyboardState.IsKeyDown(Keys.W) && !cruiseKeyPressed && !IsCruiseActive)
                {
                    _throttle = MathHelper.Clamp(_throttle + deltaTime * 0.5f, -1f, 1f);
                }
                else if (keyboardState.IsKeyDown(Keys.S) && !IsCruiseActive)
                {
                    _throttle = MathHelper.Clamp(_throttle - deltaTime * 0.5f, 0f, 1f);
                    
                    if (IsAfterburnerActive)
                    {
                        IsAfterburnerActive = false;
                        _notificationManager?.ShowMessage("Afterburner Disengaged");
                    }
                }

                if (!_gotoActive)
                {
                    if (_throttle >= 0)
                    {
                        _targetSpeed = IsAfterburnerActive ? AfterburnerSpeed : MaxSpeed * _throttle;
                    }
                    else
                    {
                        _targetSpeed = MaxReverseSpeed * _throttle; // Use negative speed for reverse
                    }
                }
            }
            
            float pitchInput = 0f, yawInput = 0f, rollInput = 0f;
            var viewport = _notificationManager.GetViewport();

            bool temporarySteering = !_isFreeFlightMode && leftMouseHeld;
            
            if (!_isFreeFlightMode && !leftMouseHeld && _prevLeftMouseState == ButtonState.Pressed)
            {
                _shouldAutoLevel = true;
            }
            
            if (temporarySteering || _isFreeFlightMode)
            {
                _shouldAutoLevel = false;
            }
            
            if (_isFreeFlightMode || temporarySteering)
            {
                Vector2 screenCenter = new Vector2(viewport.Width / 2f, viewport.Height / 2f);
                Vector2 mousePosition = new Vector2(mouseState.X, mouseState.Y);
                Vector2 mouseDeltaFromCenter = mousePosition - screenCenter;

                float mouseSensitivity = 0.4f;
                float deadzone = 10f;

                if (Math.Abs(mouseDeltaFromCenter.X) > deadzone)
                {
                    yawInput = -mouseDeltaFromCenter.X * mouseSensitivity / (viewport.Width / 2f);
                }
                if (Math.Abs(mouseDeltaFromCenter.Y) > deadzone)
                {
                    pitchInput = mouseDeltaFromCenter.Y * mouseSensitivity / (viewport.Height / 2f);
                }

                yawInput = MathHelper.Clamp(yawInput, -1f, 1f);
                pitchInput = MathHelper.Clamp(pitchInput, -1f, 1f);
            }
            
            bool isStrafing = keyboardState.IsKeyDown(Keys.LeftShift) || keyboardState.IsKeyDown(Keys.RightShift);
            if (!isStrafing)
            {
                if (keyboardState.IsKeyDown(Keys.A)) rollInput = 1f;
                if (keyboardState.IsKeyDown(Keys.D)) rollInput = -1f;
            }

            Vector3 strafeVelocity = Vector3.Zero;
            if (isStrafing)
            {
                if (keyboardState.IsKeyDown(Keys.A)) strafeVelocity -= Right * StrafeSpeed;
                if (keyboardState.IsKeyDown(Keys.D)) strafeVelocity += Right * StrafeSpeed;
                if (keyboardState.IsKeyDown(Keys.W) && !cruiseKeyPressed) strafeVelocity += Up * StrafeSpeed;
                if (keyboardState.IsKeyDown(Keys.S)) strafeVelocity -= Up * StrafeSpeed;
            }
            
            float turnRate = TurnSpeed * deltaTime;
            if (IsAfterburnerActive) turnRate *= 0.6f;
            
            Quaternion pitchDelta = Quaternion.Identity;
            if (Math.Abs(pitchInput) > 0.01f) 
                pitchDelta = Quaternion.CreateFromAxisAngle(Vector3.Right, -pitchInput * turnRate);

            Quaternion yawDelta = Quaternion.Identity;
            if (Math.Abs(yawInput) > 0.01f) 
                yawDelta = Quaternion.CreateFromAxisAngle(Vector3.Up, yawInput * turnRate);

            Quaternion rollDelta = Quaternion.Identity;
            if (Math.Abs(rollInput) > 0.01f) 
                rollDelta = Quaternion.CreateFromAxisAngle(Vector3.Forward, rollInput * turnRate * 1.5f);

            _rotation = _rotation * pitchDelta * yawDelta * rollDelta;
            _rotation.Normalize();
            
            if (_shouldAutoLevel)
            {
                Vector3 currentForward = Vector3.Transform(Vector3.Forward, _rotation);
                Vector3 currentRight = Vector3.Transform(Vector3.Right, _rotation);
                
                Vector3 worldUp = Vector3.Up;
                Vector3 horizontalRight = currentRight - worldUp * Vector3.Dot(currentRight, worldUp);
                
                if (horizontalRight.LengthSquared() > 0.0001f)
                {
                    horizontalRight.Normalize();
                    
                    float rollAlignment = Vector3.Dot(currentRight, horizontalRight);
                    
                    if (rollAlignment < 0.995f)
                    {
                        Vector3 axis = Vector3.Cross(currentRight, horizontalRight);
                        float rollAngle = (float)Math.Acos(MathHelper.Clamp(rollAlignment, -1f, 1f));
                        
                        if (Vector3.Dot(axis, currentForward) < 0)
                            rollAngle = -rollAngle;
                        
                        float correctionAngle = rollAngle * deltaTime * _autoLevelSpeed;
                        Quaternion levelCorrection = Quaternion.CreateFromAxisAngle(currentForward, correctionAngle);
                        
                        _rotation = levelCorrection * _rotation;
                        _rotation.Normalize();
                    }
                    else
                    {
                        _shouldAutoLevel = false;
                    }
                }
                else
                {
                    _shouldAutoLevel = false;
                }
            }
            
            Orientation = Matrix.CreateFromQuaternion(_rotation);
            
            UpdateGoto(deltaTime);
            
            if (IsCruiseCharging)
            {
                if (_cruiseChargeTimer < CruiseLungeTime) Speed = MathHelper.Lerp(Speed, MaxSpeed * 3f, deltaTime * 20f);
                else if (_cruiseChargeTimer < CruiseChargePhase) Speed = MathHelper.Lerp(Speed, MaxSpeed * 3.5f, deltaTime * 6f);
                else Speed = MathHelper.Lerp(Speed, CruiseSpeed, deltaTime * 30f);
            }
            else if (IsCruiseActive) 
            {
                Speed = MathHelper.Lerp(Speed, CruiseSpeed, deltaTime * 10f);
            }
            else if (EnginesKilled) 
            {
                Speed = MathHelper.Lerp(Speed, 0f, deltaTime * 0.5f);
            }
            else 
            {
                Speed = MathHelper.Lerp(Speed, _targetSpeed, deltaTime * 5f);
            }

            if (_newtonianMode)
            {
                Vector3 thrustAccel = Vector3.Zero;
                if (!EnginesKilled)
                {
                    float thrustMag = _throttle * Acceleration * 2f;
                    thrustAccel += Forward * thrustMag;
                }
                thrustAccel += strafeVelocity / deltaTime;
                _newtonianVelocity += thrustAccel * deltaTime;
                _newtonianVelocity *= 0.98f;
                Velocity = _newtonianVelocity;
                Speed = Velocity.Length();
            }
            else
            {
                Velocity = Forward * Speed + strafeVelocity;
            }
            
            Position += Velocity * deltaTime;
            
            _pitchTiltAngle = MathHelper.Lerp(_pitchTiltAngle, pitchInput * PitchTiltAmount, deltaTime * 5f);
            _bankTiltAngle = MathHelper.Lerp(_bankTiltAngle, -yawInput * BankTiltAmount, deltaTime * 4f);
            
            if (IsCruiseCharging)
            {
                _cruiseChargeTimer += deltaTime;
                if (_cruiseChargeTimer >= CruiseChargePhase && !IsCruiseActive)
                {
                    IsCruiseActive = true;
                    _targetSpeed = CruiseSpeed;
                }
                if (_cruiseChargeTimer >= CruiseChargeTime)
                {
                    IsCruiseCharging = false;
                    _cruiseChargeTimer = 0f;
                }
            }

            _prevLeftMouseState = mouseState.LeftButton; 
            _previousKeyboardState = keyboardState;
        }

        public void Draw(Matrix view, Matrix projection, Vector3 lightDirection)
        {
            if (Model == null) return;

            Matrix modelScale = Matrix.CreateScale(0.1f);
            Matrix modelCorrection = Matrix.CreateRotationX(-MathHelper.PiOver2) * Matrix.CreateRotationY(MathHelper.Pi);
            
            Matrix pitchTilt = Matrix.CreateFromAxisAngle(Orientation.Right, _pitchTiltAngle);
            Matrix bankTilt = Matrix.CreateFromAxisAngle(Orientation.Forward, _bankTiltAngle);
            Matrix world = modelScale * modelCorrection * ModelRotationCorrection * Orientation * pitchTilt * bankTilt * Matrix.CreateTranslation(Position);
            
            var graphicsDevice = Model.Meshes[0].Effects[0].GraphicsDevice;
            var oldBlendState = graphicsDevice.BlendState;
            var oldDepthStencilState = graphicsDevice.DepthStencilState;
            var oldRasterizerState = graphicsDevice.RasterizerState;
            
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
                    effect.Alpha = 1.0f;
                    effect.DirectionalLight0.Direction = lightDirection;
                    effect.DirectionalLight0.DiffuseColor = new Vector3(0.9f, 0.9f, 1.0f);
                    effect.DirectionalLight0.SpecularColor = new Vector3(0.5f, 0.5f, 0.6f);
                    effect.AmbientLightColor = new Vector3(0.2f, 0.2f, 0.25f);
                }
                mesh.Draw();
            }
            
            graphicsDevice.BlendState = oldBlendState;
            graphicsDevice.DepthStencilState = oldDepthStencilState;
            graphicsDevice.RasterizerState = oldRasterizerState;
        }

        public float GetThrottle() => _throttle;
        
        public string GetFlightStatus()
        {
            if (EnginesKilled) return "ENGINES KILLED";
            if (IsCruiseActive) return "CRUISE";
            if (IsAfterburnerActive) return "AFTERBURNER";
            if (_throttle < 0) return "REVERSE";
            return "NORMAL";
        }

        public void ActivateGoto(SpaceObject target)
        {
            if (target == null) return;
            _gotoTarget = target; 
            _gotoActive = true; 
            EnginesKilled = false; 
            _notificationManager?.ShowMessage($"GOTO: {target.Name}");
            float distance = Vector3.Distance(Position, target.Position);
            
            if (distance > 5000f)
            {
                IsCruiseCharging = true;
                _cruiseChargeTimer = 0f;
                IsAfterburnerActive = false;
            }
            else
            {
                IsCruiseActive = false;
                IsCruiseCharging = false;
            }
        }
        
        public void CancelGoto()
        {
            if (_gotoActive)
            {
                _notificationManager?.ShowMessage("GOTO Cancelled");
                Console.WriteLine("GOTO cancelled");
            }
            _gotoActive = false; 
            _gotoTarget = null;
        }
        
        private void UpdateGoto(float deltaTime)
        {
            if (!_gotoActive || _gotoTarget == null) return;
            
            Vector3 toTarget = _gotoTarget.Position - Position;
            float distance = toTarget.Length();
            
            if (distance <= 300f + _gotoTarget.Radius)
            {
                CancelGoto();
                EnginesKilled = true;
                IsCruiseActive = false;
                IsCruiseCharging = false;
                _cruiseChargeTimer = 0f;
                return;
            }
            
            if ((IsCruiseActive || IsCruiseCharging) && distance < 1500f)
            {
                IsCruiseActive = false;
                IsCruiseCharging = false;
                _cruiseChargeTimer = 0f;
            }
            
            Vector3 desiredForward = Vector3.Normalize(toTarget);
            float alignment = Vector3.Dot(Forward, desiredForward);
            
            if (alignment < 0.99f)
            {
                float alignSpeed = TurnSpeed * 0.8f * deltaTime;
                Vector3 rotationAxis = Vector3.Cross(Forward, desiredForward);
                if (rotationAxis.LengthSquared() > 0.0001f)
                {
                    rotationAxis.Normalize();
                    float angle = (float)Math.Acos(MathHelper.Clamp(alignment, -1f, 1f));
                    float step = Math.Min(angle, alignSpeed);
                    Quaternion gotoRot = Quaternion.CreateFromAxisAngle(rotationAxis, step);
                    _rotation *= gotoRot;
                    _rotation.Normalize();
                }
            }
            
            if (alignment > 0.7f)
            {
                if (IsCruiseActive || IsCruiseCharging) _targetSpeed = CruiseSpeed;
                else if (distance > 1500f) _targetSpeed = MaxSpeed;
                else if (distance > 800f) _targetSpeed = MaxSpeed * MathHelper.Lerp(0.8f, 1.0f, (distance - 800f) / 700f);
                else if (distance > 300f) _targetSpeed = MaxSpeed * MathHelper.Lerp(0.1f, 0.8f, (distance - 300f) / 500f);
                else _targetSpeed = MaxSpeed * 0.1f;
            }
            else
            {
                if (IsCruiseActive && Speed > CruiseSpeed * 0.5f)
                {
                    IsCruiseActive = false;
                    IsCruiseCharging = false;
                    _cruiseChargeTimer = 0f;
                }
                else if (IsCruiseCharging && alignment < 0.3f)
                {
                    IsCruiseCharging = false;
                    _cruiseChargeTimer = 0f;
                }
                _targetSpeed = MaxSpeed * 0.5f;
            }
        }

        public void ToggleNewtonianMode()
        {
            _newtonianMode = !_newtonianMode;
            if (!_newtonianMode) _newtonianVelocity = Forward * Speed;
            else _newtonianVelocity = Velocity;
            _notificationManager?.ShowMessage(_newtonianMode ? "Newtonian Flight" : "Standard Flight");
            Console.WriteLine($"Newtonian Mode: {(_newtonianMode ? "ENABLED" : "DISABLED")}");
        }

        public void Reset()
        {
            IsAfterburnerActive = false;
            IsCruiseActive = false;
            IsCruiseCharging = false;
            _cruiseChargeTimer = 0f;
            _throttle = 0f;
            _targetSpeed = 0f;
        }
    }
}
