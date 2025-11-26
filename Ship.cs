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
        public float CruiseSpeed { get; set; } = 500f;
        public float AfterburnerSpeed { get; set; } = 350f; 
        public float Acceleration { get; set; } = 50f; 
        public float TurnSpeed { get; set; } = 1.5f;
        public float BankAmount { get; set; } = 1.2f;
        public float StrafeSpeed { get; set; } = 250f;
        
        // Ship state
        private float _currentBankAngle = 0f;
        private float _currentPitch = 0f;
        private float _throttle = 0f;
        private float _targetSpeed = 0f;
        private bool _wasEnginesKilled = false;
        private Quaternion _rotation = Quaternion.Identity;

        // Mouse flight mode
        private bool _isFreeFlightMode = false; // True: ship follows mouse. False: mouse is a cursor.
        private Vector2 _lastMousePosition;
        private bool _mouseFlightInitialized = false;
        private ButtonState _prevLeftMouseState = ButtonState.Released;

        // Special states
        public bool IsAfterburnerActive { get; private set; }
        public bool IsCruiseActive { get; private set; }
        public bool EnginesKilled { get; private set; }
        public bool AfterburnerJustActivated { get; private set; }
        public bool IsFreeFlightMode => _isFreeFlightMode;
        
        // Cruise charge system
        private bool _cruiseCharging = false;
        private float _cruiseChargeTimer = 0f;
        private const float CruiseChargeTime = 5f;
        private const float CruiseLungeTime = 0.8f;
        private const float CruiseChargePhase = 3.5f;
        private const float CruiseBurstTime = 0.7f;
        public bool IsCruiseCharging => _cruiseCharging;
        public float CruiseChargeProgress => _cruiseCharging ? (_cruiseChargeTimer / CruiseChargeTime) : 0f;

        // Keyboard state tracking
        private KeyboardState _previousKeyboardState;
        
        // Ship model
        public Model Model { get; set; }
        
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
        
        public Ship(Vector3 startPosition)
        {
            Position = startPosition;
            Orientation = Matrix.CreateFromQuaternion(_rotation);
            Velocity = Vector3.Zero;
            _previousKeyboardState = Keyboard.GetState();
        }

        public void SetNotificationManager(NotificationManager manager)
        {
            _notificationManager = manager;
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

                if (IsCruiseActive || _cruiseCharging)
                {
                    IsCruiseActive = false;
                    _cruiseCharging = false;
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
                    Console.WriteLine("✈️ FREE FLIGHT MODE - Ship follows mouse");
                }
                else
                {
                    _notificationManager?.ShowMessage("Mouse Mode");
                    Console.WriteLine("⌨️ MOUSE MODE - Mouse is a cursor");
                }
            }
            
            bool fireWeapons = (mouseState.RightButton == ButtonState.Pressed && _prevLeftMouseState == ButtonState.Released) ||
                              (keyboardState.IsKeyDown(Keys.LeftControl) && !keyboardState.IsKeyDown(Keys.T));
            
            if (fireWeapons) FireActiveWeapons();
            
            bool wasAfterburnerActive = IsAfterburnerActive;
            IsAfterburnerActive = keyboardState.IsKeyDown(Keys.Tab);
            
            if (IsAfterburnerActive)
            {
                IsCruiseActive = false;
                _cruiseCharging = false;
                _cruiseChargeTimer = 0f;
            }
            
            if (IsAfterburnerActive && !wasAfterburnerActive)
            {
                _notificationManager?.ShowMessage("Afterburner Engaged");
            }
            
            AfterburnerJustActivated = !wasAfterburnerActive && IsAfterburnerActive;
            
            bool cruiseCombo = keyboardState.IsKeyDown(Keys.W) && (keyboardState.IsKeyDown(Keys.LeftShift) || keyboardState.IsKeyDown(Keys.RightShift));
            if (cruiseCombo && (_previousKeyboardState.IsKeyUp(Keys.W) || _previousKeyboardState.IsKeyUp(Keys.LeftShift)))
            {
                if (!IsCruiseActive && !_cruiseCharging)
                {
                    _cruiseCharging = true;
                    _cruiseChargeTimer = 0f;
                    IsAfterburnerActive = false;
                    _notificationManager?.ShowMessage("Cruise Charging");
                    Console.WriteLine("Cruise engines charging... (5 second buildup)");
                }
                else if (IsCruiseActive || _cruiseCharging)
                {
                    IsCruiseActive = false;
                    _cruiseCharging = false;
                    _cruiseChargeTimer = 0f;
                    _notificationManager?.ShowMessage("Cruise Deactivated");
                    Console.WriteLine("Cruise engine toggle: OFF");
                }
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
                    _wasEnginesKilled = true;
                }
            }
            
            if (bPressed) ToggleNewtonianMode();
            
            if (xPressed && !EnginesKilled)
            {
                _throttle = -1.0f; // Full reverse
                _targetSpeed = MaxReverseSpeed * _throttle;
                _notificationManager?.ShowMessage("Reverse Thrusters");
                Console.WriteLine("Reverse thrust engaged (stub)");
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

                // W increases throttle, S decreases
                if (keyboardState.IsKeyDown(Keys.W) && !cruiseCombo && !IsCruiseActive)
                {
                    _throttle = MathHelper.Clamp(_throttle + deltaTime * 0.5f, -1f, 1f);
                }
                else if (keyboardState.IsKeyDown(Keys.S) && !cruiseCombo && !IsCruiseActive)
                {
                    _throttle = MathHelper.Clamp(_throttle - deltaTime * 0.5f, -1f, 1f);
                }
                else if (!keyboardState.IsKeyDown(Keys.W) && !keyboardState.IsKeyDown(Keys.S) && !IsCruiseActive && !_gotoActive)
                {
                    // No throttle input, gradually return to zero if not in cruise/goto
                    // _throttle = MathHelper.Lerp(_throttle, 0f, deltaTime * 1.5f);
                }

                if (!_gotoActive)
                {
                     _targetSpeed = IsAfterburnerActive ? AfterburnerSpeed : (_throttle >= 0 ? MaxSpeed * _throttle : MaxReverseSpeed * -_throttle);
                }
            }
            
            float pitchInput = 0f, yawInput = 0f, rollInput = 0f;
            var viewport = _notificationManager.GetViewport(); // Assuming a getter exists

            // Steering logic
            bool temporarySteering = !_isFreeFlightMode && leftMouseHeld;
            if (_isFreeFlightMode || temporarySteering)
            {
                // Free Flight: ship follows mouse cursor
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
                    pitchInput = -mouseDeltaFromCenter.Y * mouseSensitivity / (viewport.Height / 2f);
                }

                yawInput = MathHelper.Clamp(yawInput, -1f, 1f);
                pitchInput = MathHelper.Clamp(pitchInput, -1f, 1f);
            }
            
            // Strafe and Roll inputs are always available
            if (keyboardState.IsKeyDown(Keys.A)) rollInput = 1f; // A for roll left
            if (keyboardState.IsKeyDown(Keys.D)) rollInput = -1f; // D for roll right
            
            Vector3 strafeVelocity = Vector3.Zero;
            // Strafe with Shift + A/D/W/S
            if (keyboardState.IsKeyDown(Keys.LeftShift) || keyboardState.IsKeyDown(Keys.RightShift))
            {
                if (keyboardState.IsKeyDown(Keys.A)) strafeVelocity -= Right * StrafeSpeed;
                if (keyboardState.IsKeyDown(Keys.D)) strafeVelocity += Right * StrafeSpeed;
                if (keyboardState.IsKeyDown(Keys.W) && !cruiseCombo) strafeVelocity += Up * StrafeSpeed;
                if (keyboardState.IsKeyDown(Keys.S)) strafeVelocity -= Up * StrafeSpeed;
            }
            
            float turnRate = TurnSpeed * deltaTime;
            if (IsAfterburnerActive) turnRate *= 0.6f;
            
            Quaternion rotationDelta = Quaternion.Identity;
            if (Math.Abs(pitchInput) > 0.01f) rotationDelta *= Quaternion.CreateFromAxisAngle(Right, pitchInput * turnRate);
            if (Math.Abs(yawInput) > 0.01f) rotationDelta *= Quaternion.CreateFromAxisAngle(Up, yawInput * turnRate);
            if (Math.Abs(rollInput) > 0.01f) rotationDelta *= Quaternion.CreateFromAxisAngle(Forward, rollInput * turnRate * 1.5f);
            
            _rotation *= rotationDelta;
            _rotation.Normalize();
            Orientation = Matrix.CreateFromQuaternion(_rotation);
            
            UpdateGoto(deltaTime);
            
            if (_cruiseCharging)
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
            
            if (_cruiseCharging)
            {
                _cruiseChargeTimer += deltaTime;
                if (_cruiseChargeTimer >= CruiseChargePhase && !IsCruiseActive)
                {
                    IsCruiseActive = true;
                    _targetSpeed = CruiseSpeed;
                }
                if (_cruiseChargeTimer >= CruiseChargeTime)
                {
                    _cruiseCharging = false;
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
            Matrix world = modelScale * modelCorrection * Orientation * pitchTilt * bankTilt * Matrix.CreateTranslation(Position);
            
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
                _cruiseCharging = true;
                _cruiseChargeTimer = 0f;
                IsAfterburnerActive = false;
            }
            else
            {
                IsCruiseActive = false;
                _cruiseCharging = false;
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
                _cruiseCharging = false;
                _cruiseChargeTimer = 0f;
                return;
            }
            
            if ((IsCruiseActive || _cruiseCharging) && distance < 1500f)
            {
                IsCruiseActive = false;
                _cruiseCharging = false;
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
                if (IsCruiseActive || _cruiseCharging) _targetSpeed = CruiseSpeed;
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
                    _cruiseCharging = false;
                    _cruiseChargeTimer = 0f;
                }
                else if (_cruiseCharging && alignment < 0.3f)
                {
                    _cruiseCharging = false;
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
    }
}
