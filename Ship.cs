using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Roguelancer
{
    /// <summary>
    /// Player ship with Freelancer-style flight mechanics
    /// </summary>
    public class Ship
    {
        public const string CruiseControlBinding = "Shift+W";

        // Position and orientation
        public Vector3 Position { get; set; }
        public Matrix Orientation { get; private set; }
        public Vector3 Velocity { get; set; }
        
        // Movement properties
        public float Speed { get; private set; }
        public float MaxSpeed { get; set; } = 250f;
        public float MaxReverseSpeed { get; set; } = 150f;
        private float _configuredCruiseSpeed = 600f;
        /// <summary>
        /// Compatibility-facing cruise speed reference. The runtime policy is
        /// owned by CruiseDrive and guarantees at least its canonical 3.5x
        /// normal-speed multiplier.
        /// </summary>
        public float CruiseSpeed
        {
            get => CruiseDrive.GetEffectiveSpeed(MaxSpeed, _configuredCruiseSpeed);
            set => _configuredCruiseSpeed = float.IsNaN(value) || float.IsInfinity(value) || value < 0f ? 0f : value;
        }
        public float AfterburnerSpeed { get; set; } = 500f;
        /// <summary>Developer-only travel convenience; normal gameplay keeps this at one.</summary>
        public float ValidationTravelMultiplier { get; set; } = 1f;
        public float Acceleration { get; set; } = 150f;
        public float TurnSpeed { get; set; } = 1.5f;
        public float BankAmount { get; set; } = 1.2f;
        public float StrafeSpeed { get; set; } = 250f;
        
        // Energy system
        public ShipEnergy Energy { get; private set; }

        // Weapon and thruster energy are separate combat resources. Energy is
        // retained as a legacy general ship-system pool for compatibility but
        // afterburning no longer consumes it.
        public WeaponEnergy WeaponEnergy { get; private set; }
        public ThrusterEnergy ThrusterEnergy { get; private set; }
        
        // Shield system
        public ShieldSystem Shields { get; private set; }
        
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
        private bool _autoLevelToggle = false;
        private float _autoLevelSpeed = 1.5f;

        // Special states
        public bool IsAfterburnerActive { get; private set; }
        public CruiseDrive CruiseDrive { get; } = new CruiseDrive();
        public bool IsCruiseActive => CruiseDrive.IsActive;
        public bool EnginesKilled { get; set; }
        public bool AfterburnerJustActivated { get; private set; }
        public bool IsFreeFlightMode => _isFreeFlightMode;
        public bool IsCruiseCharging => CruiseDrive.IsCharging;
        public float CruiseChargeProgress => CruiseDrive.ChargeProgress;
        public string CruiseHudText => CruiseDrive.GetHudText();

        // Keyboard state tracking
        private KeyboardState _previousKeyboardState;
        private int _previousScrollWheelValue;
        
        // Ship model
        public Model Model { get; set; }
        public string DisplayName { get; set; } = "Scimitar";
        public string ModelPath { get; set; } = "SHIPS/scimitar/Scimitar2";
        public Matrix ModelRotationCorrection { get; set; } = Matrix.Identity;

        public IReadOnlyList<string> LastHardpointReconfigurationWarnings { get; private set; } = Array.Empty<string>();

        // Hull integrity
        public HullIntegrity Hull { get; private set; }
        public float CollisionRadius { get; set; } = 10f;
        
        // Cargo hold
        public CargoHold CargoHold { get; private set; }

        // Carried combat supplies remain on the authoritative loadout, but
        // are intentionally not mountable equipment or commodity cargo.
        public CombatConsumableInventory CombatConsumables => Loadout?.CombatConsumables;

        // Equipment/loadout backbone
        public ShipLoadout Loadout { get; private set; }
        
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
        public bool MissileLaunchRequested { get; private set; }
        public bool MineLaunchRequested { get; private set; }
        public bool CountermeasureLaunchRequested { get; private set; }
        
        // Docking system
        private Station _nearestStation = null;
        public Station NearestStation => _nearestStation;
        public bool CanDock => _nearestStation != null && Vector3.Distance(Position, _nearestStation.Position) <= _nearestStation.DockingRange;
        private bool _dockAssistActive = false;
        private Station _dockAssistTarget = null;
        public bool IsDockAssistActive => _dockAssistActive;
        public Station CurrentDockAssistTarget => _dockAssistTarget;
        
        // Autopilot / GOTO
        private bool _gotoActive = false;
        private SpaceObject _gotoTarget = null;
        public bool IsGotoActive => _gotoActive;
        public SpaceObject CurrentGotoTarget => _gotoTarget;

        // Full route autopilot
        private GotoAutopilot _gotoAutopilot;
        public GotoAutopilot GotoAutopilot => _gotoAutopilot;

        // Target speed set by autopilot (overrides player throttle while active)
        private float _autopilotTargetSpeed = -1f;
        
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
            _previousScrollWheelValue = Mouse.GetState().ScrollWheelValue;
            
            Hull = new HullIntegrity(100f);
            Hull.OnDestroyed += () =>
            {
                IsAfterburnerActive = false;
                CruiseDrive.Reset();
                Console.WriteLine("💀 PLAYER SHIP DESTROYED!");
                _notificationManager?.ShowMessage("SHIP DESTROYED", 5f);
                // Trigger player ship explosion
                _explosionParticles?.TriggerExplosion(Position, Velocity, intensity: 1.5f);
            };

            // Initialize cargo hold with default capacity
            CargoHold = new CargoHold(50);
            Loadout = ShipLoadout.CreateStarterLoadout();

            InitializeEnergy();
            InitializeShields();
            RefreshShieldFromLoadout();
            RefreshWeaponEnergyFromLoadout();
            RefreshThrusterFromLoadout();
        }

        public void SetNotificationManager(NotificationManager manager)
        {
            _notificationManager = manager;
        }

        /// <summary>
        /// Attach the full-route autopilot. Call once after construction.
        /// </summary>
        public void SetGotoAutopilot(GotoAutopilot autopilot)
        {
            _gotoAutopilot = autopilot;
        }

        /// <summary>
        /// Called by GotoAutopilot to set the desired speed while autopilot is active.
        /// Pass a negative value to release the override.
        /// </summary>
        public void SetAutopilotTargetSpeed(float speed)
        {
            _autopilotTargetSpeed = speed < 0f
                ? speed
                : speed * MathHelper.Clamp(ValidationTravelMultiplier, 1f, 20f);
        }

        /// <summary>
        /// Control methods for GotoAutopilot to manipulate ship state.
        /// </summary>
        public void SetEnginesKilled(bool killed) => EnginesKilled = killed;
        public float GetEffectiveCruiseSpeed() =>
            CruiseDrive.GetEffectiveSpeed(MaxSpeed, _configuredCruiseSpeed, ValidationTravelMultiplier);

        public bool TryActivateCruise(bool docked = false, bool validMovement = true)
        {
            bool activated = CruiseDrive.TryActivate(
                Hull?.IsDestroyed != true,
                docked,
                validMovement && !EnginesKilled && !_newtonianMode);
            if (activated)
            {
                IsAfterburnerActive = false;
            }

            return activated;
        }

        public bool ToggleCruise(bool docked = false, bool validMovement = true)
        {
            bool changed = CruiseDrive.ToggleActivation(
                Hull?.IsDestroyed != true,
                docked,
                validMovement && !EnginesKilled && !_newtonianMode);
            if (changed && CruiseDrive.IsChargingOrActive)
            {
                IsAfterburnerActive = false;
            }

            return changed;
        }

        public bool CancelCruise(CruiseCancellationReason reason = CruiseCancellationReason.Manual) =>
            CruiseDrive.Cancel(reason);

        /// <summary>
        /// Authoritative combat boundary for player damage. A positive
        /// hostile result disrupts cruise before shield absorption so a
        /// shield-only hit still breaks travel mode.
        /// </summary>
        public bool ApplyCombatDamage(float damage, bool hostile = true)
        {
            if (Hull == null || Hull.IsDestroyed || float.IsNaN(damage) || float.IsInfinity(damage) || damage <= 0f)
                return false;

            CruiseDrive.DisruptByDamage(damage, hostile);
            float hullDamage = Shields?.AbsorbDamage(damage) ?? damage;
            if (hullDamage > 0f)
            {
                Hull.TakeDamage(hullDamage);
            }

            return true;
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
        /// Rebinds weapon energy to the one canonical mounted powerplant.
        /// Mounting/restoring a plant starts its transient charge full; an
        /// unmounted ship has zero usable weapon energy.
        /// </summary>
        public void RefreshWeaponEnergyFromLoadout(bool restoreFull = true)
        {
            PowerplantEquipmentDefinition powerplant = Loadout?.GetMountedPowerplant();
            WeaponEnergy = new WeaponEnergy();
            WeaponEnergy.Configure(powerplant, restoreFull);
        }

        /// <summary>
        /// Rebinds the dedicated thruster-energy runtime from the one mounted
        /// canonical thruster. A newly mounted thruster starts full; an
        /// unmounted or invalid thruster has no usable afterburn energy.
        /// </summary>
        public void RefreshThrusterFromLoadout(bool restoreFull = true)
        {
            ThrusterEquipmentDefinition thruster = Loadout?.GetMountedThruster();
            ThrusterEnergy = new ThrusterEnergy();
            ThrusterEnergy.Configure(thruster, restoreFull);
            if (!ThrusterEnergy.HasMountedThruster)
            {
                IsAfterburnerActive = false;
            }
        }

        /// <summary>
        /// Initialize the ship's shield system
        /// </summary>
        public void InitializeShields(float maxShields = 50f, float regenRate = 15f, float regenDelay = 3f)
        {
            Shields = new ShieldSystem(maxShields, regenRate, regenDelay);
        }

        /// <summary>
        /// Rebinds runtime shield state from the one mounted canonical shield
        /// in the authoritative loadout. A newly mounted shield starts full;
        /// an unmounted ship has no active shield.
        /// </summary>
        public void RefreshShieldFromLoadout(bool restoreFull = true)
        {
            ShieldEquipmentDefinition shield = Loadout?.GetMountedShield();
            ShieldSystem previous = Shields;
            Shields = shield == null
                ? new ShieldSystem()
                : new ShieldSystem(shield);
            if (!restoreFull && shield != null && previous != null &&
                string.Equals(previous.MountedShieldId, shield.Id, StringComparison.OrdinalIgnoreCase))
            {
                float preservedCharge = MathHelper.Clamp(previous.CurrentShields, 0f, Shields.MaxShields);
                Shields.AbsorbDamage(Math.Max(0f, Shields.MaxShields - preservedCharge));
            }
        }

        public bool TryUseNanobot(out string message) =>
            CombatConsumableService.TryUseNanobot(this, out message);

        public bool TryUseShieldBattery(out string message) =>
            CombatConsumableService.TryUseShieldBattery(this, out message);
        
        /// <summary>
        /// Set new hull integrity (used when purchasing a new ship)
        /// Note: This will reset hull event handlers - they need to be re-registered after calling this
        /// </summary>
        public void SetHull(float maxHull)
        {
            Hull = new HullIntegrity(maxHull);
        }

        /// <summary>
        /// Refresh the coarse flight collision radius from the currently loaded
        /// model. The spaceflight collision system remains intentionally bounded;
        /// station presentation uses the more detailed model-derived envelopes.
        /// </summary>
        public void RefreshCollisionRadiusFromModel()
        {
            if (Model == null) return;

            float radius = 0f;
            foreach (ModelMesh mesh in Model.Meshes)
            {
                radius = MathF.Max(radius, mesh.BoundingSphere.Radius * 0.1f);
            }

            if (radius > 0f && !float.IsNaN(radius) && !float.IsInfinity(radius))
            {
                CollisionRadius = MathHelper.Clamp(radius, 3f, 100f);
            }
        }
        
        /// <summary>
        /// Update ship's energy system (regeneration)
        /// </summary>
        public void UpdateEnergy(GameTime gameTime)
        {
            Energy?.Update(gameTime);
        }

        public void UpdateWeaponEnergy(GameTime gameTime)
        {
            WeaponEnergy?.Update(gameTime, Hull?.IsDestroyed != true);
        }

        // Stub action methods
        private void FireActiveWeapons() { Console.WriteLine("Fire weapons (stub)"); }
        private void LaunchMissile() { MissileLaunchRequested = true; }
        private void LaunchTorpedo() { Console.WriteLine("Launch torpedo (stub)"); }
        private void LaunchMine() { MineLaunchRequested = true; }
        private void LaunchCountermeasures()
        {
            if (!HasMountedCountermeasureDropper())
            {
                Console.WriteLine("[COUNTERMEASURE] No countermeasure dropper mounted.");
                _notificationManager?.ShowMessage("No countermeasure dropper mounted.", 2f);
                return;
            }

            CountermeasureLaunchRequested = true;
        }
        private void TargetClosestEnemy() { Console.WriteLine("Target closest enemy (stub)"); }
        private void PreviousEnemyTarget() { Console.WriteLine("Previous enemy target (stub)"); }
        private void NextEnemyTarget() { Console.WriteLine("Next enemy target (stub)"); }
        private void NextTarget() { Console.WriteLine("Next target (stub)"); }
        private void PreviousTarget() { Console.WriteLine("Previous target (stub)"); }
        private void ClearTarget() { _currentTarget = null; Console.WriteLine("Clear target (stub)"); }
        
        /// <summary>
        /// Attempt to dock at the nearest station
        /// </summary>
        public bool TryDock()
        {
            if (!CanDock)
            {
                if (_nearestStation != null)
                {
                    float distance = Vector3.Distance(Position, _nearestStation.Position);
                    _notificationManager?.ShowMessage($"Too far from {_nearestStation.Name} ({distance:F0}m)", 2f);
                }
                else
                {
                    _notificationManager?.ShowMessage("No station in range", 2f);
                }
                return false;
            }

            CancelCruise(CruiseCancellationReason.Docking);
            _isDocking = true;
            Console.WriteLine($"[DOCK] Initiating docking at {_nearestStation.Name}");
            return true;
        }

        /// <summary>
        /// Update the nearest station for docking checks
        /// </summary>
        public void UpdateNearestStation(List<Station> stations)
        {
            if (stations == null || stations.Count == 0)
            {
                _nearestStation = null;
                return;
            }

            Station nearest = null;
            float nearestDistance = float.MaxValue;

            foreach (var station in stations)
            {
                float distance = Vector3.Distance(Position, station.Position);
                if (distance < nearestDistance)
                {
                    nearestDistance = distance;
                    nearest = station;
                }
            }

            _nearestStation = nearest;
        }

        public void Update(GameTime gameTime, KeyboardState keyboardState, bool isRearView = false)
        {
            float deltaTime = (float)gameTime.ElapsedGameTime.TotalSeconds;
            MouseState mouseState = Mouse.GetState();
            
            // Update energy system
            UpdateEnergy(gameTime);
            UpdateWeaponEnergy(gameTime);

            // Update shield system
            Shields?.Update(gameTime);

            // Emit damage smoke based on hull integrity
            DamageStage damageStage = DamageStage.None;
            if (Hull.HullPercentage <= 0.90f && Hull.HullPercentage > 0.75f)
            {
                damageStage = DamageStage.Light;
            }
            else if (Hull.HullPercentage <= 0.75f && Hull.HullPercentage > 0.50f)
            {
                damageStage = DamageStage.Heavy;
            }
            else if (Hull.HullPercentage <= 0.50f)
            {
                damageStage = DamageStage.Critical;
            }

            if (damageStage != DamageStage.None)
            {
                _damageSmokeParticles?.Emit(Position - Forward * 15, Velocity, damageStage);
            }
            
            bool spacebarPressed = keyboardState.IsKeyDown(Keys.Space) && _previousKeyboardState.IsKeyUp(Keys.Space);
            bool leftMouseHeld = mouseState.LeftButton == ButtonState.Pressed;
            bool leftMouseClicked = leftMouseHeld && _prevLeftMouseState == ButtonState.Released;
            
            bool zPressed = keyboardState.IsKeyDown(Keys.Z) && _previousKeyboardState.IsKeyUp(Keys.Z);
            bool bPressed = keyboardState.IsKeyDown(Keys.B) && _previousKeyboardState.IsKeyUp(Keys.B);
            bool xPressed = keyboardState.IsKeyDown(Keys.X) && _previousKeyboardState.IsKeyUp(Keys.X);
            bool rPressed = keyboardState.IsKeyDown(Keys.R) && _previousKeyboardState.IsKeyUp(Keys.R);
            bool ctrlRPressed = keyboardState.IsKeyDown(Keys.LeftControl) && rPressed;
            bool shiftRPressed = keyboardState.IsKeyDown(Keys.LeftShift) && rPressed;
            bool tPressed = keyboardState.IsKeyDown(Keys.T) && _previousKeyboardState.IsKeyUp(Keys.T);
            bool shiftTPressed = keyboardState.IsKeyDown(Keys.LeftShift) && tPressed;
            bool ctrlTPressed = keyboardState.IsKeyDown(Keys.LeftControl) && tPressed;
            bool nanobotPressed = keyboardState.IsKeyDown(Keys.Y) && _previousKeyboardState.IsKeyUp(Keys.Y);
            bool shieldBatteryPressed = keyboardState.IsKeyDown(Keys.K) && _previousKeyboardState.IsKeyUp(Keys.K);

            if (nanobotPressed)
            {
                if (TryUseNanobot(out string nanobotMessage))
                {
                    _notificationManager?.ShowMessage(nanobotMessage, 2f);
                }
                else if (!string.IsNullOrWhiteSpace(nanobotMessage))
                {
                    _notificationManager?.ShowMessage(nanobotMessage, 2f);
                }
            }

            if (shieldBatteryPressed)
            {
                if (TryUseShieldBattery(out string batteryMessage))
                {
                    _notificationManager?.ShowMessage(batteryMessage, 2f);
                }
                else if (!string.IsNullOrWhiteSpace(batteryMessage))
                {
                    _notificationManager?.ShowMessage(batteryMessage, 2f);
                }
            }
            
            if (keyboardState.IsKeyDown(Keys.Escape) && _previousKeyboardState.IsKeyUp(Keys.Escape))
            {
                if (_isFreeFlightMode)
                {
                    _isFreeFlightMode = false;
                    _notificationManager?.ShowMessage("Mouse Mode");
                }

                if (CruiseDrive.IsChargingOrActive)
                {
                    CancelCruise(CruiseCancellationReason.Manual);
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
            bool leftShiftHeld = keyboardState.IsKeyDown(Keys.LeftShift);
            bool rightShiftHeld = keyboardState.IsKeyDown(Keys.RightShift);
            bool cruiseKeyHeld = (leftShiftHeld || rightShiftHeld) && keyboardState.IsKeyDown(Keys.W);
            bool cruiseKeyPressed = cruiseKeyHeld &&
                (_previousKeyboardState.IsKeyUp(Keys.W) ||
                 (leftShiftHeld && _previousKeyboardState.IsKeyUp(Keys.LeftShift)) ||
                 (rightShiftHeld && _previousKeyboardState.IsKeyUp(Keys.RightShift)));

            // 1. Handle Afterburner state (TAB key) - Hold to activate
            bool tabHeld = keyboardState.IsKeyDown(Keys.Tab);
            bool tabJustPressed = tabHeld && _previousKeyboardState.IsKeyUp(Keys.Tab);
            bool tabJustReleased = !tabHeld && _previousKeyboardState.IsKeyDown(Keys.Tab);
            
            if (tabJustPressed && !IsAfterburnerActive)
            {
                if (CruiseDrive.IsChargingOrActive)
                {
                    CancelCruise(CruiseCancellationReason.AfterburnerActivation);
                }

                // Afterburn is available only from the mounted thruster pool.
                if (!EnginesKilled && ThrusterEnergy?.CanAfterburn(Hull?.IsDestroyed != true) == true)
                {
                    IsAfterburnerActive = true;
                    _notificationManager?.ShowMessage("Afterburner Engaged");
                }
                else
                {
                    _notificationManager?.ShowMessage("Afterburner Unavailable - No Thruster Energy");
                }
            }
            else if (tabJustReleased && IsAfterburnerActive)
            {
                // Deactivate afterburner when TAB is released
                IsAfterburnerActive = false;
                _notificationManager?.ShowMessage("Afterburner Disengaged");
            }
            
            if (cruiseKeyPressed)
            {
                if (CruiseDrive.IsChargingOrActive)
                {
                    CancelCruise(CruiseCancellationReason.Manual);
                    _notificationManager?.ShowMessage("Cruise Deactivated");
                }
                else if (TryActivateCruise())
                {
                    IsAfterburnerActive = false;
                    _notificationManager?.ShowMessage("Cruise Charging");
                }
                else
                {
                    _notificationManager?.ShowMessage(
                        CruiseDrive.IsCoolingDown ? "Cruise Unavailable - Cooling Down" : "Cruise Unavailable");
                }
            }

            // Advance the authoritative thruster pool only after all held
            // state transitions above have been resolved. Ordinary movement
            // never reaches this path as an energy consumer.
            if (EnginesKilled)
            {
                IsAfterburnerActive = false;
                CruiseDrive.Cancel(CruiseCancellationReason.IncompatibleFlight);
            }
            bool thrusterAfterburning = ThrusterEnergy?.Advance(
                gameTime,
                IsAfterburnerActive,
                Hull?.IsDestroyed != true) == true;
            if (IsAfterburnerActive && !thrusterAfterburning)
            {
                IsAfterburnerActive = false;
                _notificationManager?.ShowMessage("Afterburner Cut Out - Thruster Energy Depleted");
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
                    CancelCruise(CruiseCancellationReason.IncompatibleFlight);
                    _wasEnginesKilled = true;
                }
            }
            
            if (bPressed) ToggleNewtonianMode();

            if (keyboardState.IsKeyDown(Keys.L) && _previousKeyboardState.IsKeyUp(Keys.L))
            {
                _autoLevelToggle = !_autoLevelToggle;
                _notificationManager?.ShowMessage(_autoLevelToggle ? "Auto Level Enabled" : "Auto Level Disabled");
                Console.WriteLine($"Auto Level: {(_autoLevelToggle ? "ENABLED" : "DISABLED")}");
            }
            
            if (xPressed && !EnginesKilled)
            {
                _throttle = -1.0f;
                _notificationManager?.ShowMessage("Reverse Thrusters");
                Console.WriteLine("Reverse thrust engaged");
            }
            
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

                // Mouse wheel throttle control
                int scrollWheelDelta = mouseState.ScrollWheelValue - _previousScrollWheelValue;
                if (scrollWheelDelta != 0)
                {
                    float throttleStep = 0.1f * (scrollWheelDelta / 120); // Normalize scroll
                    _throttle = MathHelper.Clamp(_throttle + throttleStep, -1f, 1f);
                    
                    // Exit cruise mode when scrolling down (slowing down)
                    if (scrollWheelDelta < 0 && CruiseDrive.IsChargingOrActive)
                    {
                        CancelCruise(CruiseCancellationReason.Manual);
                        _notificationManager?.ShowMessage("Cruise Mode Deactivated");
                        Console.WriteLine("Mouse wheel: Cruise mode deactivated");
                    }
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
                        _targetSpeed = IsAfterburnerActive
                            ? AfterburnerSpeed * MathHelper.Clamp((ThrusterEnergy?.AfterburnSpeedMultiplier ?? 2f) / 2f, 0.5f, 1.5f)
                            : MaxSpeed * _throttle;
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
            
            if (!_isFreeFlightMode && !leftMouseHeld && _prevLeftMouseState == ButtonState.Pressed && !_gotoActive)
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

            if (isRearView)
            {
                pitchInput = -pitchInput;
            }
            
            bool isShiftHeld = keyboardState.IsKeyDown(Keys.LeftShift) || keyboardState.IsKeyDown(Keys.RightShift);
            if (isShiftHeld)
            {
                if (keyboardState.IsKeyDown(Keys.A)) rollInput = 1f;
                if (keyboardState.IsKeyDown(Keys.D)) rollInput = -1f;
            }

            Vector3 strafeVelocity = Vector3.Zero;
            if (!isShiftHeld)
            {
                if (keyboardState.IsKeyDown(Keys.A)) strafeVelocity -= Right * StrafeSpeed;
                if (keyboardState.IsKeyDown(Keys.D)) strafeVelocity += Right * StrafeSpeed;
            }
            if (isShiftHeld)
            {
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
            
            if ((_shouldAutoLevel || _autoLevelToggle) && !_gotoActive)
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

            CruiseDrive.Advance(
                deltaTime,
                Hull?.IsDestroyed != true,
                _isDocking || _gotoAutopilot?.IsDocked == true,
                !EnginesKilled && !_newtonianMode);

            float commandedSpeed = IsCruiseActive ? GetEffectiveCruiseSpeed() : _targetSpeed;
            if (EnginesKilled)
            {
                commandedSpeed = 0f;
            }

            float speedRate = IsCruiseActive
                ? Math.Max(1f, Acceleration) * CruiseDrive.AccelerationMultiplier
                : Math.Max(1f, Acceleration) * 5f;
            Speed = ApproachSpeed(Speed, commandedSpeed, speedRate, deltaTime);

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
            
            _prevLeftMouseState = mouseState.LeftButton; 
            _previousKeyboardState = keyboardState;
            _previousScrollWheelValue = mouseState.ScrollWheelValue;
        }

        private static float ApproachSpeed(float current, float target, float acceleration, float deltaTime)
        {
            if (float.IsNaN(current) || float.IsInfinity(current)) current = 0f;
            if (float.IsNaN(target) || float.IsInfinity(target)) target = 0f;
            if (float.IsNaN(acceleration) || float.IsInfinity(acceleration) || acceleration <= 0f) acceleration = 1f;
            if (float.IsNaN(deltaTime) || float.IsInfinity(deltaTime) || deltaTime <= 0f) return current;

            float step = acceleration * Math.Min(deltaTime, 60f);
            return current + MathHelper.Clamp(target - current, -step, step);
        }

        public void Draw(Matrix view, Matrix projection, Vector3 lightDirection)
        {
            if (Model == null) return;

            Matrix world = CreateModelWorldMatrix(Position, Orientation, ModelRotationCorrection, _pitchTiltAngle, _bankTiltAngle);
            
            foreach (ModelMesh mesh in Model.Meshes)
            {
                foreach (BasicEffect effect in mesh.Effects)
                {
                    ConfigureModelEffect(
                        effect,
                        world,
                        view,
                        projection,
                        lightDirection,
                        new Vector3(0.9f, 0.9f, 1.0f),
                        new Vector3(0.5f, 0.5f, 0.6f),
                        new Vector3(0.2f, 0.2f, 0.25f));
                }
                mesh.Draw();
            }

            MountedEquipmentRenderer.Draw(
                this,
                Position,
                Orientation,
                view,
                projection,
                lightDirection,
                _pitchTiltAngle,
                _bankTiltAngle);
        }

        /// <summary>
        /// Builds the same model-space correction and scale used by normal flight rendering.
        /// Station presentation passes use this with an identity orientation and zero tilt.
        /// </summary>
        public static Matrix CreateModelWorldMatrix(
            Vector3 position,
            Matrix orientation,
            Matrix modelRotationCorrection,
            float pitchTiltAngle = 0.0f,
            float bankTiltAngle = 0.0f)
        {
            Matrix modelScale = Matrix.CreateScale(0.1f);
            Matrix modelCorrection = Matrix.CreateRotationX(-MathHelper.PiOver2) * Matrix.CreateRotationY(MathHelper.Pi);
            Matrix pitchTilt = Matrix.CreateFromAxisAngle(orientation.Right, pitchTiltAngle);
            Matrix bankTilt = Matrix.CreateFromAxisAngle(orientation.Forward, bankTiltAngle);
            return modelScale * modelCorrection * modelRotationCorrection * orientation * pitchTilt * bankTilt * Matrix.CreateTranslation(position);
        }

        /// <summary>
        /// Returns the ship pose without the imported model scale/correction.
        /// Mounted-equipment metadata is authored in this displayed local
        /// space, so the ship model correction is applied exactly once by the
        /// ship pass and never duplicated for equipment attachments.
        /// </summary>
        public static Matrix CreateShipPoseWorldMatrix(
            Vector3 position,
            Matrix orientation,
            float pitchTiltAngle = 0f,
            float bankTiltAngle = 0f)
        {
            Matrix pitchTilt = Matrix.CreateFromAxisAngle(orientation.Right, pitchTiltAngle);
            Matrix bankTilt = Matrix.CreateFromAxisAngle(orientation.Forward, bankTiltAngle);
            return orientation * pitchTilt * bankTilt * Matrix.CreateTranslation(position);
        }

        /// <summary>
        /// Applies the shared BasicEffect material/lighting setup used by ship model passes.
        /// The model's imported texture and diffuse-color bindings are intentionally preserved.
        /// </summary>
        public static void ConfigureModelEffect(
            BasicEffect effect,
            Matrix world,
            Matrix view,
            Matrix projection,
            Vector3 lightDirection,
            Vector3 diffuseColor,
            Vector3 specularColor,
            Vector3 ambientLightColor)
        {
            effect.World = world;
            effect.View = view;
            effect.Projection = projection;
            effect.EnableDefaultLighting();
            effect.PreferPerPixelLighting = true;
            effect.SpecularPower = 16f;
            effect.Alpha = 1.0f;
            effect.DirectionalLight0.Direction = lightDirection;
            effect.DirectionalLight0.DiffuseColor = diffuseColor;
            effect.DirectionalLight0.SpecularColor = specularColor;
            effect.AmbientLightColor = ambientLightColor;
        }

        public float GetThrottle() => _throttle;
        
        public string GetFlightStatus()
        {
            if (EnginesKilled) return "ENGINES KILLED";
            if (CruiseDrive.IsActive) return "CRUISE ACTIVE";
            if (CruiseDrive.IsCharging) return "CRUISE CHARGING";
            if (CruiseDrive.IsCoolingDown) return "CRUISE COOLDOWN";
            if (IsAfterburnerActive) return "AFTERBURNER";
            if (_throttle < 0) return "REVERSE";
            return "NORMAL";
        }

        public bool ActivateGoto(SpaceObject target)
        {
            return ActivateGoto(target, false);
        }

        public bool ActivateGoto(SpaceObject target, bool preferDirectStationApproach)
        {
            if (target == null) return false;
            CancelCruise(CruiseCancellationReason.IncompatibleFlight);
            _gotoTarget = target;
            _gotoActive = true;
            _dockAssistActive = preferDirectStationApproach && target is Station;
            _dockAssistTarget = _dockAssistActive ? target as Station : null;
            EnginesKilled = false;
            IsAfterburnerActive = false;
            _autopilotTargetSpeed = -1f;

            // Delegate to full autopilot if available
            if (_gotoAutopilot != null)
            {
                if (!_gotoAutopilot.Activate(target, _dockAssistActive))
                {
                    _gotoActive = false;
                    _gotoTarget = null;
                    _dockAssistActive = false;
                    _dockAssistTarget = null;
                    _autopilotTargetSpeed = -1f;
                    EnginesKilled = false;
                    return false;
                }
            }
            else
            {
                // Fallback: simple legacy goto
                string modeLabel = _dockAssistActive ? "DOCK ASSIST" : "GOTO";
                _notificationManager?.ShowMessage($"{modeLabel}: {target.Name}");
                float distance = Vector3.Distance(Position, target.Position);
                if (distance > 5000f)
                {
                    TryActivateCruise(validMovement: true);
                }
                else
                {
                    CancelCruise(CruiseCancellationReason.TargetInvalid);
                }
            }

            return true;
        }

        public bool ActivateDockAssist(Station target)
        {
            return ActivateGoto(target, true);
        }
        
        public void CancelGoto(bool showNotification = true)
        {
            if (_gotoActive || _gotoAutopilot?.IsDocked == true)
            {
                if (showNotification)
                {
                    _notificationManager?.ShowMessage("GOTO Cancelled");
                    Console.WriteLine("GOTO cancelled");
                }
            }
            _gotoActive = false;
            _gotoTarget = null;
            _dockAssistActive = false;
            _dockAssistTarget = null;
            _autopilotTargetSpeed = -1f;
            _gotoAutopilot?.Cancel();
        }

        /// <summary>
        /// Restore normal flight after leaving a station interior. This is the
        /// only path that moves the authoritative ship out of the docked port;
        /// station display coordinates never reach Ship.Position.
        /// </summary>
        public void RestoreFlightState(Vector3 position, Vector3 forward)
        {
            CancelGoto(showNotification: false);
            Position = position;
            Velocity = Vector3.Zero;
            _newtonianVelocity = Vector3.Zero;
            SetFacing(forward);
            Reset();
            EnginesKilled = false;
            _previousKeyboardState = Keyboard.GetState();
            _prevLeftMouseState = ButtonState.Released;
            _previousScrollWheelValue = Mouse.GetState().ScrollWheelValue;
        }
        
        private void UpdateGoto(float deltaTime)
        {
            if (!_gotoActive) return;

            // If full autopilot is running, let it handle everything
            if (_gotoAutopilot != null && _gotoAutopilot.IsActive)
            {
                _gotoAutopilot.Update(deltaTime);

                // Sync _gotoActive flag in case autopilot finished
                if (!_gotoAutopilot.IsActive)
                {
                    _gotoActive = false;
                    _gotoTarget = null;
                    _dockAssistActive = false;
                    _dockAssistTarget = null;
                    _autopilotTargetSpeed = -1f;
                    EnginesKilled = !_gotoAutopilot.WasDockingDenied;
                    CancelCruise(CruiseCancellationReason.Docking);
                }

                // Apply autopilot target speed override
                if (_autopilotTargetSpeed >= 0f)
                    _targetSpeed = _autopilotTargetSpeed;

                return;
            }

            // Legacy fallback goto (no autopilot assigned)
            if (_gotoTarget == null) return;

            Vector3 toTarget = _gotoTarget.Position - Position;
            float distance = toTarget.Length();

            if (distance <= 300f + _gotoTarget.Radius)
            {
                CancelGoto();
                EnginesKilled = true;
                CancelCruise(CruiseCancellationReason.IncompatibleFlight);
                return;
            }

            if (CruiseDrive.IsChargingOrActive && distance < 1500f)
            {
                CancelCruise(CruiseCancellationReason.TargetInvalid);
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
                if (IsCruiseActive) _targetSpeed = CruiseSpeed;
                else if (IsCruiseCharging) _targetSpeed = MaxSpeed;
                else if (distance > 1500f) _targetSpeed = MaxSpeed;
                else if (distance > 800f) _targetSpeed = MaxSpeed * MathHelper.Lerp(0.8f, 1.0f, (distance - 800f) / 700f);
                else if (distance > 300f) _targetSpeed = MaxSpeed * MathHelper.Lerp(0.1f, 0.8f, (distance - 300f) / 500f);
                else _targetSpeed = MaxSpeed * 0.1f;
            }
            else
            {
                if (IsCruiseActive && Speed > CruiseSpeed * 0.5f)
                {
                    CancelCruise(CruiseCancellationReason.TargetInvalid);
                }
                else if (IsCruiseCharging && alignment < 0.3f)
                {
                    CancelCruise(CruiseCancellationReason.TargetInvalid);
                }
                _targetSpeed = MaxSpeed * 0.5f;
            }
        }

        public void ToggleNewtonianMode()
        {
            _newtonianMode = !_newtonianMode;
            if (_newtonianMode)
            {
                CancelCruise(CruiseCancellationReason.IncompatibleFlight);
            }
            if (!_newtonianMode) _newtonianVelocity = Forward * Speed;
            else _newtonianVelocity = Velocity;
            _notificationManager?.ShowMessage(_newtonianMode ? "Newtonian Flight" : "Standard Flight");
            Console.WriteLine($"Newtonian Mode: {(_newtonianMode ? "ENABLED" : "DISABLED")}");
        }

        /// <summary>
        /// Instantly orient the ship to face the given direction.
        /// Used when entering a tradelane so the model faces the travel direction.
        /// </summary>
        public void SetFacing(Vector3 direction)
        {
            if (direction.LengthSquared() < 0.0001f) return;
            direction.Normalize();

            // Build a look-at rotation: Forward in MonoGame is -Z
            Vector3 up = Vector3.Up;
            // Handle near-vertical directions
            if (Math.Abs(Vector3.Dot(direction, up)) > 0.999f)
                up = Vector3.Forward;

            Matrix lookAt = Matrix.CreateWorld(Vector3.Zero, direction, up);
            _rotation = Quaternion.CreateFromRotationMatrix(lookAt);
            _rotation.Normalize();
            Orientation = Matrix.CreateFromQuaternion(_rotation);
        }

        /// <summary>
        /// Apply an incremental rotation around the given axis by the given angle (radians).
        /// Used by tradelane auto-orient to smoothly steer the ship.
        /// </summary>
        public void ApplyRotation(Vector3 axis, float angleRadians)
        {
            if (axis.LengthSquared() < 0.0001f || Math.Abs(angleRadians) < 0.00001f) return;
            Quaternion delta = Quaternion.CreateFromAxisAngle(axis, angleRadians);
            _rotation *= delta;
            _rotation.Normalize();
            Orientation = Matrix.CreateFromQuaternion(_rotation);
        }

        public void Reset()
        {
            IsAfterburnerActive = false;
            CruiseDrive.Reset();
            _throttle = 0f;
            _targetSpeed = 0f;
            MissileLaunchRequested = false;
            MineLaunchRequested = false;
            CountermeasureLaunchRequested = false;
            _previousKeyboardState = Keyboard.GetState();
            Shields?.FullRestore();
            WeaponEnergy?.FullRestore();
            ThrusterEnergy?.FullRestore();
        }

        /// <summary>
        /// Restore a saved motion state without re-running user input.
        /// </summary>
        public void ApplySavedState(Vector3 position, Vector3 velocity, Vector3? forward = null)
        {
            Reset();

            Position = position;
            Velocity = velocity;
            Speed = velocity.Length();
            _newtonianVelocity = velocity;
            _targetSpeed = Speed;

            if (forward.HasValue && forward.Value.LengthSquared() > 0.0001f)
            {
                SetFacing(forward.Value);
            }
            else
            {
                Orientation = Matrix.CreateFromQuaternion(_rotation);
            }
        }

        public void SetLoadout(ShipLoadout loadout)
        {
            Loadout = loadout ?? ShipLoadout.CreateStarterLoadout();
            RefreshShieldFromLoadout();
            RefreshWeaponEnergyFromLoadout();
            RefreshThrusterFromLoadout();
        }

        /// <summary>
        /// Applies ship-definition hardpoint metadata to the existing loadout.
        /// This is the only transition point that changes physical mount
        /// topology; owned equipment remains on the loadout throughout.
        /// </summary>
        public void ApplyHardpointLayout(IEnumerable<ShipHardpointDefinition> definitions)
        {
            List<ShipHardpointDefinition> metadata = definitions?
                .Where(definition => definition != null)
                .ToList();
            IEnumerable<ShipHardpoint> target = metadata == null || metadata.Count == 0
                ? null
                : metadata.Select(definition => definition.ToRuntimeHardpoint());

            if (Loadout == null)
            {
                Loadout = target == null ? ShipLoadout.CreateStarterLoadout(false) : new ShipLoadout(target);
                RefreshShieldFromLoadout();
                RefreshWeaponEnergyFromLoadout();
                RefreshThrusterFromLoadout();
                LastHardpointReconfigurationWarnings = Array.Empty<string>();
                return;
            }

            Loadout = Loadout.ReconfigureHardpoints(target, out List<string> warnings);
            RefreshShieldFromLoadout();
            RefreshWeaponEnergyFromLoadout();
            RefreshThrusterFromLoadout();
            LastHardpointReconfigurationWarnings = warnings;
            foreach (string warning in warnings)
            {
                Console.WriteLine($"[LOADOUT] {DisplayName}: {warning}");
            }
        }

        public string GetHardpointDiagnostics()
        {
            string source = Loadout?.UsesGenericFallbackLayout == true ? "GenericFallback" : "ShipDefinition/Custom";
            string assignments = Loadout == null
                ? "unavailable"
                : string.Join(" | ", Loadout.Hardpoints.Select(hardpoint =>
                    $"{hardpoint.Id} -> {(hardpoint.IsEmpty ? "empty" : hardpoint.MountedEquipmentId)}"));
            return $"Ship: {DisplayName} | Hardpoints ({source}): {assignments}";
        }

        public IEnumerable<WeaponEquipmentDefinition> GetMountedGuns()
        {
            return Loadout?.GetMountedGuns() ?? Array.Empty<WeaponEquipmentDefinition>();
        }

        public bool HasMountedGun()
        {
            return Loadout?.HasMountedGun() == true;
        }

        public WeaponEquipmentDefinition GetPrimaryMountedGun()
        {
            return Loadout?.GetPrimaryMountedGun();
        }

        public PowerplantEquipmentDefinition GetMountedPowerplant()
        {
            return Loadout?.GetMountedPowerplant();
        }

        public ThrusterEquipmentDefinition GetMountedThruster()
        {
            return Loadout?.GetMountedThruster();
        }

        public IEnumerable<EquipmentDefinition> GetMountedMissileLaunchers()
        {
            return Loadout?.GetMountedMissileLaunchers() ?? Array.Empty<EquipmentDefinition>();
        }

        public bool HasMountedMissileLauncher()
        {
            return Loadout?.HasMountedMissileLauncher() == true;
        }

        public EquipmentDefinition GetPrimaryMountedMissileLauncher()
        {
            return Loadout?.GetPrimaryMountedMissileLauncher();
        }

        public IEnumerable<EquipmentDefinition> GetMountedMineDroppers()
        {
            return Loadout?.GetMountedMineDroppers() ?? Array.Empty<EquipmentDefinition>();
        }

        public bool HasMountedMineDropper()
        {
            return Loadout?.HasMountedMineDropper() == true;
        }

        public EquipmentDefinition GetPrimaryMountedMineDropper()
        {
            return Loadout?.GetPrimaryMountedMineDropper();
        }

        public bool ConsumeMissileLaunchRequest()
        {
            bool requested = MissileLaunchRequested;
            MissileLaunchRequested = false;
            return requested;
        }

        public bool ConsumeMineLaunchRequest()
        {
            bool requested = MineLaunchRequested;
            MineLaunchRequested = false;
            return requested;
        }

        public bool ConsumeCountermeasureLaunchRequest()
        {
            bool requested = CountermeasureLaunchRequested;
            CountermeasureLaunchRequested = false;
            return requested;
        }

        public IEnumerable<EquipmentDefinition> GetMountedCountermeasureDroppers()
        {
            return Loadout?.GetMountedCountermeasureDroppers() ?? Array.Empty<EquipmentDefinition>();
        }

        public bool HasMountedCountermeasureDropper()
        {
            return Loadout?.HasMountedCountermeasureDropper() == true;
        }

        public EquipmentDefinition GetPrimaryMountedCountermeasureDropper()
        {
            return Loadout?.GetPrimaryMountedCountermeasureDropper();
        }
    }
}
