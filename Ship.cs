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
        public float MaxSpeed { get; set; } = 16000f; // Was 8000, now 16000 (2x faster)
        public float MaxReverseSpeed { get; set; } = 8000f; // Was 4000, now 8000 (2x faster)
        public float CruiseSpeed { get; set; } = 60000f; // Was 30000, now 60000 (2x faster)
        public float AfterburnerSpeed { get; set; } = 30000f; // Was 15000, now 30000 (2x faster)
        public float Acceleration { get; set; } = 8000f; // Was 4000, now 8000 (2x faster acceleration)
        public float TurnSpeed { get; set; } = 1.5f; // Was 2.5, now 1.5 (slower turns - 40% reduction)
        public float BankAmount { get; set; } = 1.2f;
        public float StrafeSpeed { get; set; } = 6000f; // Was 3000, now 6000 (2x faster)
        
        // Ship state
        private float _currentBankAngle = 0f;
        private float _currentPitch = 0f;
        private float _throttle = 0f;
        private float _targetSpeed = 0f;
        private bool _wasEnginesKilled = false; // Track previous engine state

        // Mouse flight mode
        private bool _mouseFlightEnabled = false;
        private Vector2 _lastMousePosition;
        private bool _mouseFlightInitialized = false;
        private ButtonState _prevLeftMouseState = ButtonState.Released; // track previous left button
        private bool _mouseFlightToggle = false; // persistent space-bar toggle

        // Special states
        public bool IsAfterburnerActive { get; private set; }
        public bool IsCruiseActive { get; private set; }
        public bool EnginesKilled { get; private set; }
        public bool AfterburnerJustActivated { get; private set; }
        public bool MouseFlightEnabled => _mouseFlightEnabled;
        
        // Cruise charge system (5-second buildup)
        private bool _cruiseCharging = false;
        private float _cruiseChargeTimer = 0f;
        private const float CruiseChargeTime = 5f; // 5 seconds total
        private const float CruiseLungeTime = 0.8f; // Initial lunge duration
        private const float CruiseChargePhase = 3.5f; // Engine charge phase
        private const float CruiseBurstTime = 0.7f; // Final burst phase
        public bool IsCruiseCharging => _cruiseCharging;
        public float CruiseChargeProgress => _cruiseCharging ? (_cruiseChargeTimer / CruiseChargeTime) : 0f;

        // Keyboard state tracking
        private KeyboardState _previousKeyboardState;
        
        // Ship model
        public Model Model { get; set; }
        
        // Direction vectors
        public Vector3 Forward => Orientation.Forward;
        public Vector3 Up => Orientation.Up;
        public Vector3 Right => Orientation.Right;

        // Visual tilts
        private float _pitchTiltAngle = 0f; // Visual nose up/down tilt
        private const float PitchTiltAmount = 0.15f; // Max radians (~8.6 deg)
        private float _bankTiltAngle = 0f; // Visual left/right bank
        private const float BankTiltAmount = 0.25f; // Max radians (~14.3 deg)

        // Combat & targeting state stubs
        private bool _isDocking = false;
        private object _currentTarget = null; // placeholder
        private bool _freeFlightMode = true; // ESC sets true
        
        // Autopilot / GOTO
        private bool _gotoActive = false;
        private SpaceObject _gotoTarget = null;
        private float _gotoApproachDistance = 500f; // distance to stop
        public bool IsGotoActive => _gotoActive;
        public SpaceObject CurrentGotoTarget => _gotoTarget;
        
        // Newtonian flight mode
        private bool _newtonianMode = false;
        private Vector3 _newtonianVelocity = Vector3.Zero;
        public bool IsNewtonianMode => _newtonianMode;
        
        public Ship(Vector3 startPosition)
        {
            Position = startPosition;
            // Reset to identity - we'll handle model orientation in Draw
            Orientation = Matrix.Identity;
            Velocity = Vector3.Zero;
            _previousKeyboardState = Keyboard.GetState();
        }

        // Stub action methods
        private void FireActiveWeapons() { /* TODO: implement weapon firing */ Console.WriteLine("Fire weapons (stub)"); }
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
            
            // Toggle key detections
            bool tabPressed = keyboardState.IsKeyDown(Keys.Tab) && _previousKeyboardState.IsKeyUp(Keys.Tab);
            bool zPressed = keyboardState.IsKeyDown(Keys.Z) && _previousKeyboardState.IsKeyUp(Keys.Z); // Newtonian mode
            bool xPressed = keyboardState.IsKeyDown(Keys.X) && _previousKeyboardState.IsKeyUp(Keys.X); // reverse thrust
            bool spacePressed = keyboardState.IsKeyDown(Keys.Space) && _previousKeyboardState.IsKeyUp(Keys.Space);
            bool hPressed = keyboardState.IsKeyDown(Keys.H) && _previousKeyboardState.IsKeyUp(Keys.H); // turret view
            bool nPressed = keyboardState.IsKeyDown(Keys.N) && _previousKeyboardState.IsKeyUp(Keys.N); // engine kill
            bool f3Pressed = keyboardState.IsKeyDown(Keys.F3) && _previousKeyboardState.IsKeyUp(Keys.F3);
            bool rPressed = keyboardState.IsKeyDown(Keys.R) && _previousKeyboardState.IsKeyUp(Keys.R);
            bool ctrlRPressed = keyboardState.IsKeyDown(Keys.LeftControl) && keyboardState.IsKeyDown(Keys.R) && _previousKeyboardState.IsKeyUp(Keys.R);
            bool shiftRPressed = keyboardState.IsKeyDown(Keys.LeftShift) && keyboardState.IsKeyDown(Keys.R) && _previousKeyboardState.IsKeyUp(Keys.R);
            bool tPressed = keyboardState.IsKeyDown(Keys.T) && _previousKeyboardState.IsKeyUp(Keys.T);
            bool shiftTPressed = keyboardState.IsKeyDown(Keys.LeftShift) && keyboardState.IsKeyDown(Keys.T) && _previousKeyboardState.IsKeyUp(Keys.T);
            bool ctrlTPressed = keyboardState.IsKeyDown(Keys.LeftControl) && keyboardState.IsKeyDown(Keys.T) && _previousKeyboardState.IsKeyUp(Keys.T);
            
            // ESC: Cancel cruise/GOTO (Freelancer style) - no longer exits game
            if (keyboardState.IsKeyDown(Keys.Escape) && _previousKeyboardState.IsKeyUp(Keys.Escape))
            {
                // Cancel cruise mode
                if (IsCruiseActive || _cruiseCharging)
                {
                    IsCruiseActive = false;
                    _cruiseCharging = false;
                    _cruiseChargeTimer = 0f;
                    Console.WriteLine("ESC: Cruise mode cancelled");
                }
                
                // Cancel GOTO autopilot
                if (_gotoActive)
                {
                    CancelGoto();
                }
                
                // Cancel afterburner (if active)
                if (IsAfterburnerActive)
                {
                    // Afterburner is hold-to-use, so this just ensures it's off
                    IsAfterburnerActive = false;
                }
            }
            
            // Space toggles mouse flight persistent mode
            if (spacePressed)
            {
                _mouseFlightToggle = !_mouseFlightToggle;
                if (_mouseFlightToggle)
                {
                    _mouseFlightEnabled = true;
                    _mouseFlightInitialized = false;
                }
                else
                {
                    // When disabling toggle, fall back to hold logic state (release unless button currently held)
                    _mouseFlightEnabled = mouseState.LeftButton == ButtonState.Pressed;
                    if (_mouseFlightEnabled) _mouseFlightInitialized = false;
                }
            }
            
            // Hold left mouse enables temporary mouse flight if not toggled
            if (!_mouseFlightToggle)
            {
                if (mouseState.LeftButton == ButtonState.Pressed && _prevLeftMouseState == ButtonState.Released)
                {
                    _mouseFlightEnabled = true;
                    _mouseFlightInitialized = false;
                }
                else if (mouseState.LeftButton == ButtonState.Released && _prevLeftMouseState == ButtonState.Pressed)
                {
                    _mouseFlightEnabled = false;
                }
            }
            
            // Tab: Afterburner (HOLD to use - no longer a toggle)
            bool wasAfterburnerActive = IsAfterburnerActive;
            IsAfterburnerActive = keyboardState.IsKeyDown(Keys.Tab); // Active ONLY while TAB is held
            
            if (IsAfterburnerActive)
            {
                IsCruiseActive = false; // Cancel cruise when afterburner engaged
                _cruiseCharging = false; // Cancel cruise charging
                _cruiseChargeTimer = 0f;
            }
            
            // Set flag if we just turned it on
            AfterburnerJustActivated = !wasAfterburnerActive && IsAfterburnerActive;
            
            // Shift+W: Cruise toggle (Freelancer style) - NOW WITH DRAMATIC CHARGE SEQUENCE
            bool cruiseCombo = keyboardState.IsKeyDown(Keys.W) && (keyboardState.IsKeyDown(Keys.LeftShift) || keyboardState.IsKeyDown(Keys.RightShift));
            if (cruiseCombo && (_previousKeyboardState.IsKeyUp(Keys.W) || _previousKeyboardState.IsKeyUp(Keys.LeftShift)))
            {
                if (!IsCruiseActive && !_cruiseCharging)
                {
                    // START CRUISE CHARGE SEQUENCE
                    _cruiseCharging = true;
                    _cruiseChargeTimer = 0f;
                    IsAfterburnerActive = false; // Cancel afterburner
                    Console.WriteLine("Cruise engines charging... (5 second buildup)");
                }
                else if (IsCruiseActive || _cruiseCharging)
                {
                    // CANCEL CRUISE
                    IsCruiseActive = false;
                    _cruiseCharging = false;
                    _cruiseChargeTimer = 0f;
                    Console.WriteLine("Cruise engine toggle: OFF");
                }
            }
            
            // Z - Toggle Newtonian flight mode
            if (zPressed)
            {
                ToggleNewtonianMode();
            }
            
            // N - Engine kill (was Z)
            if (nPressed)
            {
                EnginesKilled = !EnginesKilled;
                Console.WriteLine("Engine kill: " + (EnginesKilled ? "ENGAGED" : "DISENGAGED"));
                if (EnginesKilled)
                {
                    _throttle = 0f; _targetSpeed = 0f; IsAfterburnerActive = false; IsCruiseActive = false; _wasEnginesKilled = true;
                }
            }
            
            // X: Reverse thrust (instant set reverse target)
            if (xPressed && !EnginesKilled)
            {
                _throttle = -0.5f;
                _targetSpeed = MaxReverseSpeed * _throttle;
                Console.WriteLine("Reverse thrust engaged (stub)");
            }
            
            // F3: Dock
            if (f3Pressed) Dock();
            
            // Weapons/combat inputs (stubs)
            // Left-click: Fire active weapons (conflicts with mouse flight hold – only fire if in toggle mode)
            if (mouseState.LeftButton == ButtonState.Pressed && _mouseFlightToggle && _prevLeftMouseState == ButtonState.Released)
            {
                FireActiveWeapons();
            }
            // Q missile, Shift+Q torpedo
            if (keyboardState.IsKeyDown(Keys.Q) && _previousKeyboardState.IsKeyUp(Keys.Q))
            {
                if (keyboardState.IsKeyDown(Keys.LeftShift) || keyboardState.IsKeyDown(Keys.RightShift)) LaunchTorpedo();
                else LaunchMissile();
            }
            // E mine
            if (keyboardState.IsKeyDown(Keys.E) && _previousKeyboardState.IsKeyUp(Keys.E)) LaunchMine();
            // C countermeasures
            if (keyboardState.IsKeyDown(Keys.C) && _previousKeyboardState.IsKeyUp(Keys.C)) LaunchCountermeasures();
            
            // Targeting: R / Shift+R / Ctrl+R
            if (rPressed && !shiftRPressed && !ctrlRPressed) TargetClosestEnemy();
            if (shiftRPressed) NextEnemyTarget();
            if (ctrlRPressed) PreviousEnemyTarget();
            
            // Target cycle: T / Shift+T / Ctrl+T
            if (tPressed && !shiftTPressed && !ctrlTPressed) NextTarget();
            if (shiftTPressed) PreviousTarget();
            if (ctrlTPressed) ClearTarget();
            
            // Existing thrust and movement logic (retain W/S throttle adjustments)
            if (!EnginesKilled)
            {
                if (keyboardState.IsKeyDown(Keys.W) && !cruiseCombo && !IsCruiseActive)
                {
                    _throttle = MathHelper.Clamp(_throttle + deltaTime * 2f, -0.5f, 1f);
                    _targetSpeed = IsAfterburnerActive ? AfterburnerSpeed : (_throttle >= 0 ? MaxSpeed * _throttle : MaxReverseSpeed * _throttle);
                }
                else if (keyboardState.IsKeyDown(Keys.S))
                {
                    // Decelerate / reverse
                    if (_throttle > 0f)
                    {
                        _throttle = MathHelper.Clamp(_throttle - deltaTime * 3f, 0f, 1f);
                        _targetSpeed = MaxSpeed * _throttle;
                    }
                    else
                    {
                        _throttle = MathHelper.Clamp(_throttle - deltaTime * 2f, -0.5f, 0f);
                        _targetSpeed = MaxReverseSpeed * _throttle;
                    }
                }
            }
            
            // Flight controls inputs
            float pitchInput = 0f;
            float yawInput = 0f;
            float rollInput = 0f;

            if (_mouseFlightEnabled)
            {
                // Mouse flight mode - Freelancer style
                Vector2 screenCenter = new Vector2(960, 540); // Half of 1920x1080
                Vector2 currentMousePos = new Vector2(mouseState.X, mouseState.Y);
                
                if (!_mouseFlightInitialized)
                {
                    _lastMousePosition = currentMousePos;
                    _mouseFlightInitialized = true;
                }
                
                // Calculate mouse offset from center
                Vector2 mouseOffset = currentMousePos - screenCenter;
                
                // Mouse sensitivity settings
                float mouseSensitivity = 0.003f;
                float deadzone = 20f; // Pixels of deadzone in center
                
                // Apply deadzone
                if (Math.Abs(mouseOffset.X) > deadzone)
                {
                    float adjustedX = mouseOffset.X - Math.Sign(mouseOffset.X) * deadzone;
                    yawInput = -adjustedX * mouseSensitivity; // Negative for correct direction
                }
                
                if (Math.Abs(mouseOffset.Y) > deadzone)
                {
                    float adjustedY = mouseOffset.Y - Math.Sign(mouseOffset.Y) * deadzone;
                    pitchInput = -adjustedY * mouseSensitivity; // Negative for correct direction
                }
                
                // Clamp input values
                yawInput = MathHelper.Clamp(yawInput, -1f, 1f);
                pitchInput = MathHelper.Clamp(pitchInput, -1f, 1f);
                
                // Roll controls still work with Q/E in mouse mode
                if (keyboardState.IsKeyDown(Keys.Q))
                    rollInput = -1f;
                if (keyboardState.IsKeyDown(Keys.E))
                    rollInput = 1f;
            }
            else
            {
                // Keyboard flight mode
                // Pitch controls (Up/Down arrows or I/K)
                if (keyboardState.IsKeyDown(Keys.Up) || keyboardState.IsKeyDown(Keys.I))
                    pitchInput = 1f;
                if (keyboardState.IsKeyDown(Keys.Down) || keyboardState.IsKeyDown(Keys.K))
                    pitchInput = -1f;
                
                // Yaw controls (Left/Right arrows or J/L or A/D)
                if (keyboardState.IsKeyDown(Keys.Left) || keyboardState.IsKeyDown(Keys.J) || 
                    (keyboardState.IsKeyDown(Keys.A) && !keyboardState.IsKeyDown(Keys.LeftShift)))
                    yawInput = 1f;
                if (keyboardState.IsKeyDown(Keys.Right) || keyboardState.IsKeyDown(Keys.L) || 
                    (keyboardState.IsKeyDown(Keys.D) && !keyboardState.IsKeyDown(Keys.LeftShift)))
                    yawInput = -1f;
                
                // Roll controls (Q/E)
                if (keyboardState.IsKeyDown(Keys.Q))
                    rollInput = -1f;
                if (keyboardState.IsKeyDown(Keys.E))
                    rollInput = 1f;
            }
            
            // Strafe (A/D always, vertical strafe with Shift+W/S retained?)
            Vector3 strafeVelocity = Vector3.Zero;
            if (keyboardState.IsKeyDown(Keys.A)) strafeVelocity -= Right * StrafeSpeed;
            if (keyboardState.IsKeyDown(Keys.D)) strafeVelocity += Right * StrafeSpeed;
            if (keyboardState.IsKeyDown(Keys.LeftShift) || keyboardState.IsKeyDown(Keys.RightShift))
            {
                if (keyboardState.IsKeyDown(Keys.W) && !cruiseCombo) strafeVelocity += Up * StrafeSpeed;
                if (keyboardState.IsKeyDown(Keys.S)) strafeVelocity -= Up * StrafeSpeed;
            }
            
            // Rotation application
            float turnRate = TurnSpeed * deltaTime; if (IsAfterburnerActive) turnRate *= 0.6f;
            if (Math.Abs(pitchInput) > 0.01f) { Orientation *= Matrix.CreateFromAxisAngle(Right, pitchInput * turnRate); _currentPitch = pitchInput; } else _currentPitch = MathHelper.Lerp(_currentPitch, 0f, deltaTime * 3f);
            if (Math.Abs(yawInput) > 0.01f) { Orientation *= Matrix.CreateFromAxisAngle(Up, yawInput * turnRate); _currentBankAngle = MathHelper.Lerp(_currentBankAngle, -yawInput * BankAmount, deltaTime * 5f); } else _currentBankAngle = MathHelper.Lerp(_currentBankAngle, 0f, deltaTime * 3f);
            if (Math.Abs(rollInput) > 0.01f) { Orientation *= Matrix.CreateFromAxisAngle(Forward, rollInput * turnRate * 1.5f); _currentBankAngle = 0f; } else if (Math.Abs(yawInput) > 0.01f) { Orientation *= Matrix.CreateFromAxisAngle(Forward, _currentBankAngle * deltaTime); }
            Orientation = OrthonormalizeOrientation(Orientation);
            
            // Autopilot GOTO handling (MOVED HERE - before speed interpolation!)
            UpdateGoto(deltaTime);
            
            // Speed interpolation (for normal mode)
            if (_cruiseCharging)
            {
                // ? CRUISE CHARGE SEQUENCE - 3 PHASES:
                float chargeProgress = _cruiseChargeTimer / CruiseChargeTime;
                
                if (_cruiseChargeTimer < CruiseLungeTime)
                {
                    // PHASE 1: INITIAL LUNGE (0-0.8s) - Ship lurches forward
                    float lungeFactor = _cruiseChargeTimer / CruiseLungeTime;
                    float lungeBoost = MaxSpeed * 3f; // 3x normal speed lunge
                    _targetSpeed = MathHelper.Lerp(Speed, lungeBoost, lungeFactor);
                    Speed = MathHelper.Lerp(Speed, _targetSpeed, deltaTime * 10f); // Fast ramp-up
                }
                else if (_cruiseChargeTimer < CruiseChargePhase)
                {
                    // PHASE 2: ENGINE CHARGE (0.8-4.3s) - Engines build power, speed holds
                    float chargePhaseProgress = (_cruiseChargeTimer - CruiseLungeTime) / (CruiseChargePhase - CruiseLungeTime);
                    // Hold at boosted speed while engines charge
                    _targetSpeed = MaxSpeed * (2f + chargePhaseProgress * 1.5f); // Build from 2x to 3.5x
                    Speed = MathHelper.Lerp(Speed, _targetSpeed, deltaTime * 3f); // Hold steady
                }
                else
                {
                    // PHASE 3: BURST TO CRUISE (4.3-5.0s) - Explosive acceleration to cruise speed
                    float burstProgress = (_cruiseChargeTimer - CruiseChargePhase) / CruiseBurstTime;
                    _targetSpeed = MathHelper.Lerp(MaxSpeed * 3.5f, CruiseSpeed, burstProgress);
                    Speed = MathHelper.Lerp(Speed, _targetSpeed, deltaTime * 15f); // RAPID burst!
                }
            }
            else if (IsCruiseActive) 
            {
                // Normal cruise - maintain cruise speed
                Speed = MathHelper.Lerp(Speed, CruiseSpeed, deltaTime * 5f);
            }
            else if (EnginesKilled) 
            {
                Speed = MathHelper.Lerp(Speed, 0f, deltaTime * 0.5f);
            }
            else 
            {
                Speed = MathHelper.Lerp(Speed, _targetSpeed, deltaTime * Acceleration / MaxSpeed);
            }

            // Velocity calculation depends on flight mode
            if (_newtonianMode)
            {
                // Newtonian mode: apply thrust as acceleration, maintain momentum
                Vector3 thrustAccel = Vector3.Zero;
                if (!EnginesKilled)
                {
                    // Forward/reverse thrust
                    float thrustMag = _throttle * Acceleration;
                    thrustAccel += Forward * thrustMag;
                }
                // Add strafe to thrust (already calculated above)
                thrustAccel += strafeVelocity / deltaTime; // convert strafe velocity to acceleration
                
                // Apply thrust acceleration
                _newtonianVelocity += thrustAccel * deltaTime;
                
                // Optional: velocity damping (simulating light friction/drag)
                float dampingFactor = 0.98f; // very light damping
                _newtonianVelocity *= dampingFactor;
                
                Velocity = _newtonianVelocity;
                Speed = Velocity.Length();
            }
            else
            {
                // Normal mode
                Velocity = Forward * Speed + strafeVelocity;
            }
            
            Position += Velocity * deltaTime;
            
            // Visual tilts
            float targetPitchTilt = pitchInput * PitchTiltAmount; _pitchTiltAngle = MathHelper.Lerp(_pitchTiltAngle, targetPitchTilt, deltaTime * 5f);
            float targetBankTilt = -yawInput * BankTiltAmount; if (Math.Abs(rollInput) > 0.01f) targetBankTilt *= 0.2f; _bankTiltAngle = MathHelper.Lerp(_bankTiltAngle, targetBankTilt, deltaTime * 4f);
            
            // Cruise charging logic (if applicable)
            if (_cruiseCharging)
            {
                // Update charge timer
                _cruiseChargeTimer += deltaTime;
                
                // Engage cruise active state after charge phase
                if (_cruiseChargeTimer >= CruiseChargePhase && !IsCruiseActive)
                {
                    IsCruiseActive = true;
                    _targetSpeed = CruiseSpeed; // Set to cruise speed
                    Console.WriteLine("Cruise engaged (auto) after charge");
                }
                
                // Handle burst phase
                if (_cruiseChargeTimer >= CruiseChargeTime)
                {
                    _cruiseCharging = false;
                    _cruiseChargeTimer = 0f;
                    Console.WriteLine("Cruise charge completed");
                }
            }

            // Store previous states
            _prevLeftMouseState = mouseState.LeftButton; _previousKeyboardState = keyboardState;
        }

        public void Draw(Matrix view, Matrix projection, Vector3 lightDirection)
        {
            if (Model == null) return;

            Matrix modelCorrection = Matrix.CreateRotationX(-MathHelper.PiOver2) * Matrix.CreateRotationY(MathHelper.Pi);
            
            // Visual tilt matrices (applied relative to ship orientation)
            Matrix pitchTilt = Matrix.CreateFromAxisAngle(Orientation.Right, _pitchTiltAngle);
            Matrix bankTilt = Matrix.CreateFromAxisAngle(Orientation.Forward, _bankTiltAngle);
            Matrix world = modelCorrection * Orientation * pitchTilt * bankTilt * Matrix.CreateTranslation(Position);
            
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
                    
                    // Lighting
                    effect.DirectionalLight0.Direction = lightDirection;
                    effect.DirectionalLight0.DiffuseColor = new Vector3(0.9f, 0.9f, 1.0f);
                    effect.DirectionalLight0.SpecularColor = new Vector3(0.5f, 0.5f, 0.6f);
                    
                    effect.AmbientLightColor = new Vector3(0.2f, 0.2f, 0.25f);
                }
                
                mesh.Draw();
            }
        }

        private Matrix OrthonormalizeOrientation(Matrix orientation)
        {
            // Stable Gram-Schmidt orthonormalization preserving combined pitch/yaw/roll
            Vector3 forward = orientation.Forward;
            Vector3 right = orientation.Right;
            Vector3 up = orientation.Up;

            forward = Vector3.Normalize(forward);

            right = right - forward * Vector3.Dot(right, forward);
            right = Vector3.Normalize(right);

            up = up - forward * Vector3.Dot(up, forward) - right * Vector3.Dot(up, right);
            up = Vector3.Normalize(up);

            if (forward.LengthSquared() < 0.9f || right.LengthSquared() < 0.9f || up.LengthSquared() < 0.9f)
            {
                forward = Vector3.Forward;
                right = Vector3.Right;
                up = Vector3.Up;
            }

            return new Matrix(
                right.X, right.Y, right.Z, 0f,
                up.X, up.Y, up.Z, 0f,
                forward.X, forward.Y, forward.Z, 0f,
                0f, 0f, 0f, 1f
            );
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
            
            // Calculate distance to target
            float distance = Vector3.Distance(Position, target.Position);
            
            Console.WriteLine($"?? ActivateGoto called: Target={target.Name}, Distance={distance:F0} units ({distance/1000f:F1}k)");
            
            // ? AUTO-ENGAGE CRUISE for distant targets (>5000 units)
            if (distance > 5000f)
            {
                // Start cruise charge sequence for long distance travel
                _cruiseCharging = true;
                _cruiseChargeTimer = 0f;
                IsAfterburnerActive = false;
                Console.WriteLine($"?? GOTO activated: {target.Name} - Distance: {distance / 1000f:F1}k - CRUISE CHARGING!");
                Console.WriteLine($"   ? _cruiseCharging={_cruiseCharging}, _cruiseChargeTimer={_cruiseChargeTimer}");
            }
            else
            {
                // Close target - use normal speed
                IsCruiseActive = false;
                _cruiseCharging = false;
                Console.WriteLine($"?? GOTO activated: {target.Name} - Distance: {distance:F0} units - NORMAL SPEED");
            }
        }
        
        public void CancelGoto()
        {
            if (_gotoActive) Console.WriteLine("GOTO cancelled");
            _gotoActive = false; 
            _gotoTarget = null;
            // Don't disable cruise - let player control it
        }
        
        private void UpdateGoto(float deltaTime)
        {
            if (!_gotoActive || _gotoTarget == null) return;
            
            Vector3 toTarget = _gotoTarget.Position - Position;
            float distance = toTarget.Length();
            
            // ? ARRIVAL CHECK (within 300 units + object radius)
            if (distance <= 300f + _gotoTarget.Radius)
            {
                Console.WriteLine($"? GOTO: Arrived at {_gotoTarget.Name}!");
                CancelGoto();
                EnginesKilled = true; // Full stop on arrival
                IsCruiseActive = false;
                _cruiseCharging = false;
                _cruiseChargeTimer = 0f;
                return;
            }
            
            // ?? CRUISE MANAGEMENT
            if (IsCruiseActive || _cruiseCharging)
            {
                // AUTO-DISENGAGE cruise at 1.5k for smooth approach
                if (distance < 1500f)
                {
                    IsCruiseActive = false;
                    _cruiseCharging = false;
                    _cruiseChargeTimer = 0f;
                    Console.WriteLine($"?? GOTO: Approaching - Cruise disengaged at {distance:F0} units");
                }
            }
            
            // ?? STEERING - Point toward target
            Vector3 desiredForward = Vector3.Normalize(toTarget);
            Vector3 currentForward = Forward;
            float alignment = Vector3.Dot(currentForward, desiredForward);
            
            // Rotate toward target
            float alignSpeed = TurnSpeed * 0.8f * deltaTime;
            Vector3 rotationAxis = Vector3.Cross(currentForward, desiredForward);
            float axisLen = rotationAxis.Length();
            
            if (axisLen > 0.0001f)
            {
                rotationAxis /= axisLen;
                float angle = (float)Math.Acos(MathHelper.Clamp(alignment, -1f, 1f));
                float step = Math.Min(angle, alignSpeed);
                Orientation *= Matrix.CreateFromAxisAngle(rotationAxis, step);
                Orientation = OrthonormalizeOrientation(Orientation);
            }
            
            // ? SPEED MANAGEMENT - Smart deceleration curve
            if (alignment > 0.9f) // Well aligned
            {
                if (IsCruiseActive || _cruiseCharging)
                {
                    // CRUISE MODE - Full speed ahead
                    _throttle = 1.0f;
                    _targetSpeed = CruiseSpeed;
                }
                else if (distance > 1500f)
                {
                    // FAR RANGE (1.5k - infinity) - Full normal speed
                    _throttle = 1.0f;
                    _targetSpeed = MaxSpeed;
                }
                else if (distance > 800f)
                {
                    // MID RANGE (800 - 1500) - Start slowing down (80% speed)
                    float slowFactor = MathHelper.Lerp(0.8f, 1.0f, (distance - 800f) / 700f);
                    _throttle = slowFactor;
                    _targetSpeed = MaxSpeed * slowFactor;
                }
                else if (distance > 300f)
                {
                    // CLOSE RANGE (300 - 800) - Smooth deceleration curve
                    float slowFactor = MathHelper.Lerp(0.1f, 0.8f, (distance - 300f) / 500f);
                    _throttle = slowFactor;
                    _targetSpeed = MaxSpeed * slowFactor;
                }
                else
                {
                    // FINAL APPROACH (<300) - Minimum speed for precision
                    _throttle = 0.1f;
                    _targetSpeed = MaxSpeed * 0.1f;
                }
            }
            else
            {
                // TURNING - Reduce speed while not aligned
                if (IsCruiseActive || _cruiseCharging)
                {
                    // Sharp turn needed - drop cruise
                    IsCruiseActive = false;
                    _cruiseCharging = false;
                    _cruiseChargeTimer = 0f;
                    Console.WriteLine("?? GOTO: Sharp turn - Cruise cancelled");
                }
                
                // Slow down for turning
                _throttle = 0.3f;
                _targetSpeed = MaxSpeed * 0.3f;
            }
        }

        public void ToggleNewtonianMode()
        {
            _newtonianMode = !_newtonianMode;
            if (!_newtonianMode)
            {
                // When exiting Newtonian mode, reset to normal velocity
                _newtonianVelocity = Forward * Speed;
            }
            else
            {
                // When entering Newtonian mode, preserve current velocity
                _newtonianVelocity = Velocity;
            }
            Console.WriteLine($"Newtonian Mode: {(_newtonianMode ? "ENABLED" : "DISABLED")}");
        }
    }
}
