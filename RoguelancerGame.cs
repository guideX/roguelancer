using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System;
using System.Collections.Generic;
using Roguelancer.Configuration;

namespace Roguelancer {
    /// <summary>
    /// Roguelancer Game Class
    /// </summary>
    public class RoguelancerGame : Game {
        /// <summary>
        /// Graphics
        /// </summary>
        private GraphicsDeviceManager _graphics;
        /// <summary>
        /// Sprite Batch
        /// </summary>
        private SpriteBatch _spriteBatch;
        /// <summary>
        /// Player Ship
        /// </summary>
        private Ship _playerShip;
        /// <summary>
        /// Camera
        /// </summary>
        private Camera _camera;
        /// <summary>
        /// Starfield
        /// </summary>
        private Starfield _starfield;
        /// <summary>
        /// Engine Glow
        /// </summary>
        private EngineGlow _engineGlow;
        /// <summary>
        /// Engine Trail
        /// </summary>
        private EngineTrail _engineTrail;
        /// <summary>
        /// Cruise Sparks
        /// </summary>
        private CruiseSparks _cruiseSparks;
        /// <summary>
        /// Charge Particles
        /// </summary>
        private ChargeParticles _chargeParticles;
        /// <summary>
        /// Weapon System
        /// </summary>
        private WeaponSystem _weaponSystem;
        /// <summary>
        /// Sun
        /// </summary>
        private Sun _sun;
        /// <summary>
        /// Font
        /// </summary>
        private SpriteFont _font;
        /// <summary>
        /// Notification Manager
        /// </summary>
        private NotificationManager _notificationManager; // NEW: On-screen messages
        /// <summary>
        /// Configuration Manager
        /// </summary>
        private ConfigurationManager _config;
        /// <summary>
        /// Lighting Direction
        /// </summary>
        private Vector3 _lightDirection = Vector3.Normalize(new Vector3(1, -1, 1));
        /// <summary>
        /// Space Objects
        /// </summary>
        private List<SpaceObject> _spaceObjects = new();
        /// <summary>
        /// Selected Space Object Index
        /// </summary>
        private int _selectedSpaceObjectIndex = -1;
        /// <summary>
        /// Npc Ships
        /// </summary>
        private List<NpcShip> _npcShips = new();
        /// <summary>
        /// Wrecks
        /// </summary>
        private List<Wreck> _wrecks = new();
        /// <summary>
        /// Wreck Model
        /// </summary>
        private Model _wreckModel;
        /// <summary>
        /// Markers
        /// </summary>
        private List<Marker3D> _markers = new();
        /// <summary>
        /// Motion Trail
        /// </summary>
        private MotionTrail _motionTrail;
        /// <summary>
        /// Pixel
        /// </summary>
        private Texture2D _pixel;
        /// <summary>
        /// Is Window Focused
        /// </summary>
        private bool _isWindowFocused = true;
        private float _inputCooldown = 0f; // Cooldown timer for input processing
        private const float InputCooldownTime = 0.1f; // 100ms cooldown

        /// <summary>
        /// Roguelancer Game
        /// </summary>
        public RoguelancerGame() {
            _graphics = new GraphicsDeviceManager(this);
            Content.RootDirectory = "Content";
            IsMouseVisible = false;
            _graphics.PreferredBackBufferWidth = 1920;
            _graphics.PreferredBackBufferHeight = 1080;
            _graphics.IsFullScreen = false;
            this.InactiveSleepTime = TimeSpan.Zero;
            this.IsFixedTimeStep = true;
            _graphics.SynchronizeWithVerticalRetrace = true;
            this.TargetElapsedTime = TimeSpan.FromMilliseconds(16.666);
            
            // Initialize configuration manager
            _config = new ConfigurationManager();
            
            AllocConsole();
            Console.WriteLine("========================================");
            Console.WriteLine("    ROGUELANCER DEBUG CONSOLE");
            Console.WriteLine("========================================");
            Console.WriteLine("Game initialized. Console ready for debug output.");
            Console.WriteLine();
            _graphics.ApplyChanges();
        }
        /// <summary>
        /// Alloc Console
        /// </summary>
        /// <returns></returns>
        [System.Runtime.InteropServices.DllImport("kernel32.dll", SetLastError = true)]
        [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
        static extern bool AllocConsole();
        /// <summary>
        /// Initialize
        /// </summary>
        protected override void Initialize() {
            float aspectRatio = GraphicsDevice.Viewport.AspectRatio;
            _camera = new Camera(aspectRatio);
            _playerShip = new Ship(Vector3.Zero);
            Window.ClientSizeChanged += OnWindowFocusChanged;
            this.Activated += OnWindowActivated;
            this.Deactivated += OnWindowDeactivated;
            
            // Load configuration files
            _config.LoadAll();
            
            // Get current system (default to system 1)
            SystemConfig currentSystem = _config.GetSystem(1);
            if (currentSystem != null) {
                Console.WriteLine($"[SYSTEM] Loaded: {currentSystem.Description}");
                
                // Create sun from system config
                _sun = new Sun(GraphicsDevice, 
                    new Vector3(currentSystem.SunPositionX, currentSystem.SunPositionY, currentSystem.SunPositionZ), 
                    scale: currentSystem.SunScale) {
                    EmissiveColor = new Color(255, 220, 180), // Warm yellow-orange
                    EmissiveIntensity = currentSystem.SunIntensity
                };
                
                Console.WriteLine($"[SUN] INITIALIZED at position: {_sun.Position}, scale: {currentSystem.SunScale}, intensity: {currentSystem.SunIntensity}");
            } else {
                // Fallback to hardcoded sun if no system config
                _sun = new Sun(GraphicsDevice, new Vector3(5000, 2000, 3000), scale: 2000f) {
                    EmissiveColor = new Color(255, 220, 180),
                    EmissiveIntensity = 2.0f
                };
                Console.WriteLine("[SUN] Using default configuration (no system config found)");
            }

            // Populate space objects from station configs
            _spaceObjects.Add(new SpaceObject("SUN", _sun.Position, 500f));
            
            List<StationConfig> stations = _config.GetStationsForSystem(1);
            foreach (StationConfig stationConfig in stations) {
                _spaceObjects.Add(new SpaceObject(
                    stationConfig.Description,
                    stationConfig.StartupPosition,
                    stationConfig.Radius
                ));
                Console.WriteLine($"[STATION] Added: {stationConfig.Description} at {stationConfig.StartupPosition}");
            }

            // Spawn NPC ships from ship configs
            List<ShipConfig> ships = _config.GetShipsForSystem(1);
            foreach (ShipConfig shipConfig in ships) {
                // Get model path for this ship
                ModelConfig model = _config.GetModel(shipConfig.ModelIndex);
                if (model == null) {
                    Console.WriteLine($"[SHIP] Warning: Invalid model index {shipConfig.ModelIndex} for ship {shipConfig.Description}");
                    continue;
                }

                // Create NPC ship with patrol pattern if configured
                if (shipConfig.PatrolCenter.HasValue && shipConfig.PatrolRadius.HasValue && shipConfig.PatrolSpeed.HasValue) {
                    NpcShip npc = new NpcShip(
                        shipConfig.Description,
                        shipConfig.StartupPosition,
                        shipConfig.PatrolCenter.Value,
                        shipConfig.PatrolRadius.Value,
                        shipConfig.PatrolSpeed.Value
                    );
                    npc.Velocity = shipConfig.InitialVelocity;
                    _npcShips.Add(npc);
                    npc.OnDestroyed += HandleNpcDestroyed; // Subscribe to the event
                    Console.WriteLine($"[SHIP] Added patrol ship: {shipConfig.Description} (Model: {model.Name})");
                } else {
                    // Static ship (no patrol)
                    NpcShip npc = new NpcShip(
                        shipConfig.Description,
                        shipConfig.StartupPosition,
                        shipConfig.StartupPosition, // patrol center = start position
                        0f, // no patrol radius
                        0f  // no patrol speed
                    );
                    npc.Velocity = shipConfig.InitialVelocity;
                    _npcShips.Add(npc);
                    npc.OnDestroyed += HandleNpcDestroyed; // Subscribe to the event
                    Console.WriteLine($"[SHIP] Added static ship: {shipConfig.Description} (Model: {model.Name})");
                }
            }

            // Create reference grid markers - MUCH SPARSER GRID
            int gridSize = 20000; // Expanded range but fewer markers
            int gridSpacing = 2000; // MUCH LARGER spacing (was 250, now 2000) - 8x less dense
            Color markerColor = new Color(0, 255, 255, 180); // Brighter cyan, more visible

            for (int x = -gridSize; x <= gridSize; x += gridSpacing) {
                for (int z = -gridSize; z <= gridSize; z += gridSpacing) {
                    // Add markers at multiple heights for 3D depth perception
                    _markers.Add(new Marker3D(GraphicsDevice, new Vector3(x, 0, z), markerColor, 400f)); // Ground level - larger size

                    // Add some vertical markers too (every 2nd marker instead of every 4th)
                    if ((x / gridSpacing) % 2 == 0 && (z / gridSpacing) % 2 == 0) {
                        _markers.Add(new Marker3D(GraphicsDevice, new Vector3(x, 1000, z), Color.Yellow * 0.6f, 350f)); // Higher up
                        _markers.Add(new Marker3D(GraphicsDevice, new Vector3(x, -1000, z), Color.Red * 0.5f, 350f)); // Lower down
                    }
                }
            }

            Console.WriteLine($"Created {_markers.Count} reference markers in sparse grid (spacing: {gridSpacing} units)");

            base.Initialize();
        }

        protected override void LoadContent() {
            _spriteBatch = new SpriteBatch(GraphicsDevice);

            // Load ship model
            try {
                _playerShip.Model = Content.Load<Model>("SHIPS/scimitar/Scimitar2");
                Console.WriteLine("Scimitar model loaded successfully!");

                // Load wreck model
                try
                {
                    _wreckModel = Content.Load<Model>("MODELS/wreck");
                    Console.WriteLine("Wreck model loaded successfully!");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error loading wreck model: {ex.Message}");
                    // Fallback to a simple model or null if it fails
                    _wreckModel = null;
                }

                // Load models for NPC ships based on their configuration
                for (int i = 0; i < _npcShips.Count; i++) {
                    // Get ship config to find model index
                    List<ShipConfig> ships = _config.GetShipsForSystem(1);
                    if (i < ships.Count) {
                        ModelConfig model = _config.GetModel(ships[i].ModelIndex);
                        if (model != null && model.Enabled) {
                            try {
                                _npcShips[i].Model = Content.Load<Model>(model.Path);
                                Console.WriteLine($"[SHIP] Loaded model '{model.Name}' for {_npcShips[i].Name}");
                            } catch (Exception ex) {
                                Console.WriteLine($"[SHIP] Error loading model for {_npcShips[i].Name}: {ex.Message}");
                                // Fallback to player model
                                _npcShips[i].Model = _playerShip.Model;
                            }
                        } else {
                            Console.WriteLine($"[SHIP] Model disabled or not found for {_npcShips[i].Name}, using fallback");
                            _npcShips[i].Model = _playerShip.Model;
                        }
                    }
                }
                Console.WriteLine($"Loaded models for {_npcShips.Count} NPC ships!");
            } catch (Exception ex) {
                Console.WriteLine($"Error loading model: {ex.Message}");
                Console.WriteLine("Make sure the FBX file is added to the Content Pipeline.");
            }

            // Load sun content
            _sun.LoadContent(Content);

            // Create starfield
            _starfield = new Starfield(GraphicsDevice, 10000);

            // Create engine glow effect
            _engineGlow = new EngineGlow(GraphicsDevice);

            // Initialize engine trail
            _engineTrail = new EngineTrail(GraphicsDevice);

            // Initialize cruise sparks (firefly effect during charge)
            _cruiseSparks = new CruiseSparks(GraphicsDevice);

            // Initialize charge particles (energy buildup while charging beam weapon)
            _chargeParticles = new ChargeParticles(GraphicsDevice);

            // Initialize weapon system (blasters)
            _weaponSystem = new WeaponSystem(GraphicsDevice);

            // Initialize motion trail
            _motionTrail = new MotionTrail(GraphicsDevice);

            // Initialize notification manager
            _notificationManager = new NotificationManager(_font, GraphicsDevice.Viewport);
            _playerShip.SetNotificationManager(_notificationManager);

            _pixel = new Texture2D(GraphicsDevice, 1, 1);
            _pixel.SetData(new[] { Color.White });

            // Load font for HUD text
            try {
                _font = Content.Load<SpriteFont>("Fonts/HudFont");
                Console.WriteLine("HUD Font loaded successfully!");
            } catch (Exception ex) {
                _font = null;
                Console.WriteLine($"Font not loaded: {ex.Message}");
            }
        }

        protected override void Update(GameTime gameTime) {
            KeyboardState keyboardState = Keyboard.GetState();
            MouseState mouseState = Mouse.GetState();
            float deltaTime = (float)gameTime.ElapsedGameTime.TotalSeconds;

            // Handle input cooldown after window activation
            if (_inputCooldown > 0)
            {
                _inputCooldown -= deltaTime;
                _prevKeys = keyboardState;
                _prevMouseState = mouseState;
                return; // Skip the rest of the update logic
            }

            // Initialize last position on first frame
            if (_firstFrame) {
                _lastPlayerPosition = _playerShip.Position;
                _firstFrame = false;
            }

            // Debug output every second with distance traveled
            _debugTimer += deltaTime;
            if (_debugTimer >= 1f) {
                _debugTimer = 0f;
                float distanceTraveled = Vector3.Distance(_playerShip.Position, _lastPlayerPosition);
                Console.WriteLine($"=========== MOVEMENT DEBUG ===========");
                Console.WriteLine($"Ship Position: X={_playerShip.Position.X:F1} Y={_playerShip.Position.Y:F1} Z={_playerShip.Position.Z:F1}");
                Console.WriteLine($"Ship Velocity: X={_playerShip.Velocity.X:F1} Y={_playerShip.Velocity.Y:F1} Z={_playerShip.Velocity.Z:F1}");
                Console.WriteLine($"Speed: {_playerShip.Speed:F1} | Throttle: {_playerShip.GetThrottle() * 100:F0}%");
                Console.WriteLine($"Distance Traveled/sec: {distanceTraveled:F1} units");
                Console.WriteLine($"DeltaTime: {deltaTime:F4} | FPS: {(1.0f / deltaTime):F1}"); // NEW: Show framerate
                Console.WriteLine($"Camera Position: X={_camera.Position.X:F1} Y={_camera.Position.Y:F1} Z={_camera.Position.Z:F1}");

                // GOTO Debug Info
                if (_playerShip.IsGotoActive && _playerShip.CurrentGotoTarget != null) {
                    float gotoDistance = Vector3.Distance(_playerShip.Position, _playerShip.CurrentGotoTarget.Position);
                    Console.WriteLine($">>> GOTO ACTIVE: {_playerShip.CurrentGotoTarget.Name}");
                    Console.WriteLine($">>> Distance to Target: {gotoDistance:F1} units ({gotoDistance / 1000f:F2} km)");
                    Console.WriteLine($">>> Cruise: {(_playerShip.IsCruiseActive ? "ACTIVE" : "INACTIVE")}");

                }

                // Show NPC positions too
                if (_npcShips.Count > 0) {
                    Console.WriteLine($"NPC1 Position: X={_npcShips[0].Position.X:F1} Y={_npcShips[0].Position.Y:F1} Z={_npcShips[0].Position.Z:F1}");
                    Console.WriteLine($"NPC1 Speed: {_npcShips[0].Speed:F1}");
                }
                Console.WriteLine($"======================================");

                _lastPlayerPosition = _playerShip.Position;
            }

            // NOTE: ESC now handled in Ship.cs - cancels cruise/GOTO instead of exiting
            // To exit the game, close the window or press Alt+F4

            // Toggle turret view with H
            if (keyboardState.IsKeyDown(Keys.H) && _prevKeys.IsKeyUp(Keys.H)) {
                _camera.ToggleTurretView();
                _notificationManager.ShowMessage(_camera.GetCurrentViewName());
            }

            // Set rear view with V (hold to show)

            if (!_camera.IsRearViewActive && keyboardState.IsKeyDown(Keys.V)) {
                _camera.SetRearView(true);
                Console.WriteLine("_camera.SetRearView(true);");
            } else if (_camera.IsRearViewActive && keyboardState.IsKeyUp(Keys.V)) {
                _camera.SetRearView(false);
                Console.WriteLine("_camera.SetRearView(false);");
            }
            //_camera.SetRearView(keyboardState.IsKeyDown(Keys.V) && !keyboardState.IsKeyDown(Keys.LeftControl));
            //if (keyboardState.IsKeyDown(Keys.V) && _prevKeys.IsKeyUp(Keys.V) && !keyboardState.IsKeyDown(Keys.LeftControl))
            //{
            //_notificationManager.ShowMessage(_camera.GetCurrentViewName());
            //}
            //else if (keyboardState.IsKeyUp(Keys.V) && _prevKeys.IsKeyDown(Keys.V) && !keyboardState.IsKeyDown(Keys.LeftControl))
            //{
            //_notificationManager.ShowMessage(_camera.GetCurrentViewName());
            //}

            // Cycle views with Ctrl+V
            //if (keyboardState.IsKeyDown(Keys.V) && _prevKeys.IsKeyUp(Keys.V) && keyboardState.IsKeyDown(Keys.LeftControl))
            //{
            //_camera.CycleView();
            //_notificationManager.ShowMessage(_camera.GetCurrentViewName());
            //}

            // Turret view controls (mouse or arrow keys)
            if (_camera.IsTurretViewActive) {
                float turretSensitivity = 2f * deltaTime;
                float yawDelta = 0f;
                float pitchDelta = 0f;

                // Mouse input (if not in mouse flight mode)
                if (!_playerShip.IsFreeFlightMode && mouseState.LeftButton == ButtonState.Pressed) {
                    Vector2 screenCenter = new Vector2(GraphicsDevice.Viewport.Width / 2f, GraphicsDevice.Viewport.Height / 2f);
                    Vector2 mousePos = new Vector2(mouseState.X, mouseState.Y);
                    Vector2 mouseDelta = mousePos - screenCenter;
                    yawDelta = mouseDelta.X * 0.001f;
                    pitchDelta = -mouseDelta.Y * 0.001f;
                }
                // Arrow key input
                if (keyboardState.IsKeyDown(Keys.Left)) yawDelta -= turretSensitivity;
                if (keyboardState.IsKeyDown(Keys.Right)) yawDelta += turretSensitivity;
                if (keyboardState.IsKeyDown(Keys.Up)) pitchDelta += turretSensitivity;
                if (keyboardState.IsKeyDown(Keys.Down)) pitchDelta -= turretSensitivity;

                _camera.UpdateTurretView(yawDelta, pitchDelta);
            }

            // Update player ship
            _playerShip.Update(gameTime, keyboardState);

            // FIX: Update NPC ships
            foreach (var npc in _npcShips) {
                npc.Update(gameTime);
            }

            // Update wrecks
            foreach (var wreck in _wrecks)
            {
                wreck.Update(gameTime);
            }

            // FIX: Update motion trail
            _motionTrail.Update(deltaTime, _playerShip.Position, _playerShip.Speed);

            // Update sun
            _sun.Update(gameTime);

            // Update lighting direction based on sun position
            _lightDirection = _sun.GetLightDirection(_playerShip.Position);

            // Update starfield lifecycle
            _starfield.Update(deltaTime);

            // Cycle starfield mode with B key
            if (keyboardState.IsKeyDown(Keys.B) && _prevKeys.IsKeyUp(Keys.B))
            {
                _starfield.CycleMode();
                _notificationManager.ShowMessage($"Starfield: {_starfield.GetModeName()}");
                Console.WriteLine($"[STARFIELD] Mode changed to: {_starfield.GetModeName()}");
            }

            // Update mouse visibility based on flight mode
            IsMouseVisible = !_playerShip.IsFreeFlightMode;

            // Trigger camera shake when afterburner is activated - REMOVED to eliminate wiggle
            // if (_playerShip.AfterburnerJustActivated) {
            //     _camera.AddShake(0.2f, 4f);
            // }

            // Continuous mild shake while afterburner is active - REMOVED to eliminate wiggle
            // if (_playerShip.IsAfterburnerActive) {
            //     _camera.AddShake(0.03f, 8f);
            // }

            // Update camera shake
            _camera.UpdateShake(deltaTime);

            // Update camera to follow ship with more responsive smoothing
            _camera.Follow(_playerShip.Position, _playerShip.Forward, _playerShip.Up, 0.3f); // Increased from 0.1f to reduce flopping

            // Update engine trail with current throttle
            _engineTrail.Update(gameTime, _playerShip, _playerShip.GetThrottle());

            // Update cruise sparks (firefly particles during charge)
            _cruiseSparks.Update(gameTime, _playerShip);

            // Update charge particles (beam weapon charging effect)
            _chargeParticles.Update((float)gameTime.ElapsedGameTime.TotalSeconds);
            
            // Emit charge particles if beam weapon is charging
            if (_weaponSystem.IsCharging())
            {
                float chargeProgress = _weaponSystem.GetChargeProgress();
                _chargeParticles.EmitChargeParticles(_playerShip.Position, _playerShip.Orientation, chargeProgress);
            }

            // Update weapon system
            _weaponSystem.Update(gameTime);

            // Check projectile collisions with NPC ships
            foreach (var npc in _npcShips)
            {
                if (!npc.IsDestroyed)
                {
                    _weaponSystem.CheckCollisions(npc.Position, npc.CollisionRadius, npc.Hull);
                }
            }

            // TODO: Check NPC weapon fire against player (when NPCs can fire)
            // _npcWeaponSystem.CheckCollisions(_playerShip.Position, _playerShip.CollisionRadius, _playerShip.Hull);

            // Update notification manager
            _notificationManager.Update(gameTime);

            // WEAPON SWITCHING with number keys
            if (keyboardState.IsKeyDown(Keys.D1) && _prevKeys.IsKeyUp(Keys.D1))
            {
                _weaponSystem.CurrentWeapon = WeaponType.BlueDonut;
                _notificationManager.ShowMessage("Weapon: Blue Donut");
                Console.WriteLine("Switched to Blue Donut");
            }
            else if (keyboardState.IsKeyDown(Keys.D2) && _prevKeys.IsKeyUp(Keys.D2))
            {
                _weaponSystem.CurrentWeapon = WeaponType.Fireball;
                _notificationManager.ShowMessage("Weapon: Fireball");
                Console.WriteLine("Switched to Fireball");
            }
            else if (keyboardState.IsKeyDown(Keys.D3) && _prevKeys.IsKeyUp(Keys.D3))
            {
                _weaponSystem.CurrentWeapon = WeaponType.QuickBlaster;
                _notificationManager.ShowMessage("Weapon: Quick Blaster");
                Console.WriteLine("Switched to Quick Blaster");
            }
            else if (keyboardState.IsKeyDown(Keys.D4) && _prevKeys.IsKeyUp(Keys.D4))
            {
                _weaponSystem.CurrentWeapon = WeaponType.ChargeBeam;
                _notificationManager.ShowMessage("Weapon: Charge Beam");
                Console.WriteLine("Switched to Charge Beam");
            }

            // RIGHT MOUSE BUTTON: Fire weapons or charge beam!
            if (mouseState.RightButton == ButtonState.Pressed)
            {
                // Calculate ship transform
                Matrix modelCorrection = Matrix.CreateRotationX(-MathHelper.PiOver2) * Matrix.CreateRotationY(MathHelper.Pi);
                Matrix shipTransform = modelCorrection * _playerShip.Orientation * Matrix.CreateTranslation(_playerShip.Position);

                // Get mouse cursor position in 3D world space
                Vector3 nearPoint = GraphicsDevice.Viewport.Unproject(
                    new Vector3(mouseState.X, mouseState.Y, 0),
                    _camera.Projection,
                    _camera.View,
                    Matrix.Identity
                );

                Vector3 farPoint = GraphicsDevice.Viewport.Unproject(
                    new Vector3(mouseState.X, mouseState.Y, 1),
                    _camera.Projection,
                    _camera.View,
                    Matrix.Identity
                );

                // Direction from near to far point (where mouse is aiming)
                Vector3 mouseAimDirection = Vector3.Normalize(farPoint - nearPoint);

                // Gun position at center front of ship
                Vector3 centerGunOffset = new Vector3(0f, 0f, 20f);
                Vector3 gunPosition = Vector3.Transform(centerGunOffset, shipTransform);

                // Continuously fire while holding - weapon system handles refire rate internally
                _weaponSystem.StartFiring(gunPosition, mouseAimDirection, _playerShip.Velocity);
            }
            else if (_prevMouseState.RightButton == ButtonState.Pressed)
            {
                // Mouse button released - stop firing
                _weaponSystem.StopFiring();
            }


            HandleTargetingInput(keyboardState);

            base.Update(gameTime);
        }

        private void HandleNpcDestroyed(NpcShip destroyedShip)
        {
            // Create a wreck where the NPC ship was destroyed
            if (_wreckModel != null)
            {
                var wreck = new Wreck(destroyedShip.Position, destroyedShip.Orientation, _wreckModel);
                _wrecks.Add(wreck);
                Console.WriteLine($"Wreck created at {wreck.Position}");
            }
        }

        private void HandleTargetingInput(KeyboardState kb) {
            MouseState mouseState = Mouse.GetState();

            // ✨ LEFT CLICK: Target object under mouse cursor (only in Mouse Mode)
            if (!_playerShip.IsFreeFlightMode && mouseState.LeftButton == ButtonState.Pressed && _prevMouseState.LeftButton == ButtonState.Released) {
                // Raycast from mouse position into 3D world
                int clickedObjectIndex = GetObjectUnderMouse(mouseState.X, mouseState.Y);
                if (clickedObjectIndex >= 0) {
                    _selectedSpaceObjectIndex = clickedObjectIndex;
                    SpaceObject target = _spaceObjects[clickedObjectIndex];
                    float distance = Vector3.Distance(_playerShip.Position, target.Position);
                    _notificationManager.ShowMessage($"Target: {target.Name}");
                    Console.WriteLine($"Mouse targeting: {target.Name} at {distance / 1000f:F2}km");
                }
            }

            // Cycle selection with T (next) / Shift+T (previous)
            if (Pressed(kb, Keys.T) && !kb.IsKeyDown(Keys.LeftShift) && !kb.IsKeyDown(Keys.RightShift)) {
                if (_spaceObjects.Count > 0) {
                    _selectedSpaceObjectIndex = (_selectedSpaceObjectIndex + 1) % _spaceObjects.Count;
                }
            } else if (Pressed(kb, Keys.T) && (kb.IsKeyDown(Keys.LeftShift) || kb.IsKeyDown(Keys.RightShift))) {
                if (_spaceObjects.Count > 0) {
                    _selectedSpaceObjectIndex = (_selectedSpaceObjectIndex - 1 + _spaceObjects.Count) % _spaceObjects.Count;
                }
            }
            // Clear target Ctrl+T
            if (Pressed(kb, Keys.T) && (kb.IsKeyDown(Keys.LeftControl) || kb.IsKeyDown(Keys.RightControl))) {
                _selectedSpaceObjectIndex = -1;
                _notificationManager.ShowMessage("Target Cleared");
            }
            // Activate GOTO with G (stub mapping) or Enter if object selected
            if (_selectedSpaceObjectIndex >= 0 && Pressed(kb, Keys.G)) {
                _playerShip.ActivateGoto(_spaceObjects[_selectedSpaceObjectIndex]);
            }
            // Cancel GOTO with Ctrl+G
            if ((kb.IsKeyDown(Keys.LeftControl) || kb.IsKeyDown(Keys.RightControl)) && Pressed(kb, Keys.G)) {
                _playerShip.CancelGoto();
            }
        }

        // ✨ NEW: Raycast from mouse to find clicked object
        private int GetObjectUnderMouse(int mouseX, int mouseY) {
            // Create ray from mouse cursor into 3D world
            Vector3 nearPoint = GraphicsDevice.Viewport.Unproject(
                new Vector3(mouseX, mouseY, 0),
                _camera.Projection,
                _camera.View,
                Matrix.Identity
            );

            Vector3 farPoint = GraphicsDevice.Viewport.Unproject(
                new Vector3(mouseX, mouseY, 1),
                _camera.Projection,
                _camera.View,
                Matrix.Identity
            );

            Vector3 rayDirection = Vector3.Normalize(farPoint - nearPoint);
            Vector3 rayOrigin = nearPoint;

            // Find closest object intersecting with ray
            int closestObjectIndex = -1;
            float closestDistance = float.MaxValue;

            for (int i = 0; i < _spaceObjects.Count; i++) {
                SpaceObject obj = _spaceObjects[i];

                // Simple sphere intersection test
                Vector3 toObject = obj.Position - rayOrigin;
                float projectionLength = Vector3.Dot(toObject, rayDirection);

                // Object is behind camera
                if (projectionLength < 0) continue;

                Vector3 closestPoint = rayOrigin + rayDirection * projectionLength;
                float distanceToCenter = Vector3.Distance(closestPoint, obj.Position);

                // Click radius = object radius + tolerance for easier clicking
                float clickRadius = obj.Radius + 200f;

                if (distanceToCenter <= clickRadius) {
                    float distanceFromCamera = Vector3.Distance(rayOrigin, obj.Position);
                    if (distanceFromCamera < closestDistance) {
                        closestDistance = distanceFromCamera;
                        closestObjectIndex = i;
                    }
                }
            }

            return closestObjectIndex;
        }

        private bool Pressed(KeyboardState kb, Keys key) {
            return kb.IsKeyDown(key) && !_playerShip.IsFreeFlightMode && !_prevKeys.IsKeyDown(key);
        }

        // Window focus event handlers
        private void OnWindowActivated(object sender, EventArgs e) {
            _isWindowFocused = true;
            // Reset input states to prevent stale inputs from causing issues
            _prevKeys = Keyboard.GetState();
            _prevMouseState = Mouse.GetState();
            _inputCooldown = InputCooldownTime; // Start input cooldown
            Console.WriteLine("[WINDOW] ACTIVATED - Input enabled, cooldown started.");
        }

        private void OnWindowDeactivated(object sender, EventArgs e) {
            _isWindowFocused = false;
            _playerShip.Reset(); // Reset ship state
            Console.WriteLine("[WINDOW] DEACTIVATED - Input disabled, ship state reset.");
        }

        private void OnWindowFocusChanged(object sender, EventArgs e) {
            _isWindowFocused = IsActive;
        }

        private KeyboardState _prevKeys;
        private MouseState _prevMouseState; // ✨ NEW: Track previous mouse state
        private float _debugTimer = 0f;
        private Vector3 _lastPlayerPosition;
        private bool _firstFrame = true;

        protected override void EndDraw() {
            _prevKeys = Keyboard.GetState();
            _prevMouseState = Mouse.GetState(); // ✨ NEW: Track mouse state
            base.EndDraw();
        }

        protected override void Draw(GameTime gameTime) {
            // FIX: Clear BOTH color AND depth buffers to prevent artifacts
            GraphicsDevice.Clear(ClearOptions.Target | ClearOptions.DepthBuffer, Color.Black, 1.0f, 0);

            // === 3D RENDERING PHASE ===
            // Set up 3D rendering states
            GraphicsDevice.DepthStencilState = DepthStencilState.Default;
            GraphicsDevice.BlendState = BlendState.Opaque;
            GraphicsDevice.SamplerStates[0] = SamplerState.LinearWrap;
            // Re-enable proper culling for performance
            GraphicsDevice.RasterizerState = RasterizerState.CullCounterClockwise;

            // Draw starfield (drawn first, furthest back)
            _starfield.Draw(_camera.View, _camera.Projection, _camera.Position, _playerShip.Velocity);

            // Reset render states after starfield (it changes them)
            GraphicsDevice.DepthStencilState = DepthStencilState.Default;
            GraphicsDevice.BlendState = BlendState.Opaque;
            GraphicsDevice.RasterizerState = RasterizerState.CullCounterClockwise;

            // Draw sun (before ship so it's properly depth-tested)
            _sun.Draw(_camera.View, _camera.Projection);
            
            // Debug: Check if sun is in view frustum (do this only occasionally to avoid spam)
            if (gameTime.TotalGameTime.TotalSeconds % 2 < 0.016f) { // Every ~2 seconds
                float distanceToSun = Vector3.Distance(_camera.Position, _sun.Position);
                Vector3 directionToSun = Vector3.Normalize(_sun.Position - _camera.Position);
                Vector3 cameraForward = Vector3.Normalize(_camera.Position - _playerShip.Position);
                float dotProduct = Vector3.Dot(cameraForward, directionToSun);
                Console.WriteLine($"[SUN] DEBUG: Distance={distanceToSun:F1}, Dot={dotProduct:F2} (>0=in front), CamPos=({_camera.Position.X:F1},{_camera.Position.Y:F1},{_camera.Position.Z:F1})");
            }

            // Draw ship
            _playerShip.Draw(_camera.View, _camera.Projection, _lightDirection);

            // Draw NPC ships
            foreach (var npc in _npcShips) {
                npc.Draw(_camera.View, _camera.Projection, _lightDirection);
            }

            // Draw wrecks
            foreach (var wreck in _wrecks)
            {
                wreck.Draw(_camera.View, _camera.Projection, _lightDirection);
            }

            // Draw reference markers (helps visualize movement) - FIXED in world space
            // Temporarily disable depth test so markers always visible
            var oldDepth = GraphicsDevice.DepthStencilState;
            GraphicsDevice.DepthStencilState = DepthStencilState.DepthRead;

            foreach (var marker in _markers) {
                marker.Draw(_camera.View, _camera.Projection);
            }

            // Draw motion trail showing where we've been (bright yellow line)
            _motionTrail.Draw(_camera.View, _camera.Projection);

            GraphicsDevice.DepthStencilState = oldDepth;

            // Draw engine glows with dramatic intensity during cruise charge
            float glowIntensity = Math.Max(1.0f, _playerShip.GetThrottle()); // Minimum idle glow

            if (_playerShip.IsCruiseCharging) {
                // CRUISE CHARGE GLOW - Ramps from 0.5 to 3.0 over 5 seconds
                float chargeProgress = _playerShip.CruiseChargeProgress;
                glowIntensity = MathHelper.Lerp(0.5f, 2.0f, chargeProgress); // Reduced max buildup from 3.0 to 2.0

                // Pulsing effect during charge
                float pulse = (float)Math.Sin(gameTime.TotalGameTime.TotalSeconds * 8.0) * 0.2f + 0.8f;
                glowIntensity *= pulse;
            } else if (_playerShip.IsAfterburnerActive) {
                glowIntensity = 1.2f; // Reduced from 1.8f for less intensity
            } else if (_playerShip.IsCruiseActive) {
                glowIntensity = 1.0f; // Reduced from 1.5f for less intensity
            }

            _engineGlow.DrawEngineGlows(_playerShip, _camera, glowIntensity);

            // Draw cruise sparks (firefly particles) - drawn after glow but before trail
            _cruiseSparks.Draw(_camera);

            // Draw charge particles (beam weapon charging effect)
            _chargeParticles.Draw(_camera);

            // Draw weapon projectiles (blaster bolts)
            _weaponSystem.Draw(_camera);

            // Draw engine trail after glow for layering
            _engineTrail.Draw(_camera);

            // === 2D UI RENDERING PHASE ===
            // Reset all render states before SpriteBatch to prevent artifacts
            GraphicsDevice.DepthStencilState = DepthStencilState.None;
            GraphicsDevice.BlendState = BlendState.AlphaBlend;
            GraphicsDevice.RasterizerState = RasterizerState.CullCounterClockwise;
            GraphicsDevice.SamplerStates[0] = SamplerState.LinearClamp;

            // Draw HUD
            DrawHUD();

            base.Draw(gameTime);
        }

        private void DrawHUD() {
            _spriteBatch.Begin();

            DrawStatusPanel();
            DrawCurrentTargetDisplay(); // NEW: Show current target prominently
            DrawSpaceObjectBrackets();
            DrawGotoStatus();
            DrawSunDistanceIndicator(); // NEW: Show distance to sun
            DrawCrosshair(); // NEW: Mouse crosshair
            DrawCoordinates();

            // Draw notifications on top of everything else
            _notificationManager.Draw(_spriteBatch);

            _spriteBatch.End();
        }

        private void DrawCrosshair() {
            MouseState mouseState = Mouse.GetState();
            Vector2 mousePos = new Vector2(mouseState.X, mouseState.Y);

            // Crosshair size and style
            int size = 20;
            int thickness = 2;
            int gap = 8; // Gap in the center
            Color color = Color.Lime; // Bright green crosshair

            // Draw cross-style reticle
            // Horizontal line (left)
            _spriteBatch.Draw(_pixel, new Rectangle((int)mousePos.X - size, (int)mousePos.Y - thickness / 2, size - gap, thickness), color);
            // Horizontal line (right)
            _spriteBatch.Draw(_pixel, new Rectangle((int)mousePos.X + gap, (int)mousePos.Y - thickness / 2, size - gap, thickness), color);
            // Vertical line (top)
            _spriteBatch.Draw(_pixel, new Rectangle((int)mousePos.X - thickness / 2, (int)mousePos.Y - size, thickness, size - gap), color);
            // Vertical line (bottom)
            _spriteBatch.Draw(_pixel, new Rectangle((int)mousePos.X - thickness / 2, (int)mousePos.Y + gap, thickness, size - gap), color);

            // Center dot
            int dotSize = 3;
            _spriteBatch.Draw(_pixel, new Rectangle((int)mousePos.X - dotSize / 2, (int)mousePos.Y - dotSize / 2, dotSize, dotSize), color);

            // Outer circle brackets for extra style
            int circleRadius = 30;
            int bracketLength = 8;
            int bracketThickness = 2;

            // Top bracket
            _spriteBatch.Draw(_pixel, new Rectangle((int)mousePos.X - bracketThickness / 2, (int)mousePos.Y - circleRadius, bracketThickness, bracketLength), color * 0.6f);
            // Bottom bracket
            _spriteBatch.Draw(_pixel, new Rectangle((int)mousePos.X - bracketThickness / 2, (int)mousePos.Y + circleRadius - bracketLength, bracketThickness, bracketLength), color * 0.6f);
            // Left bracket
            _spriteBatch.Draw(_pixel, new Rectangle((int)mousePos.X - circleRadius, (int)mousePos.Y - bracketThickness / 2, bracketLength, bracketThickness), color * 0.6f);
            // Right bracket
            _spriteBatch.Draw(_pixel, new Rectangle((int)mousePos.X + circleRadius - bracketLength, (int)mousePos.Y - bracketThickness / 2, bracketLength, bracketThickness), color * 0.6f);
        }

        private void DrawCoordinates() {
            if (_font == null) return;

            string posText = $"POS: X: {_playerShip.Position.X:F1}   Y: {_playerShip.Position.Y:F1}   Z: {_playerShip.Position.Z:F1}";
            string velText = $"VEL: X: {_playerShip.Velocity.X:F1}   Y: {_playerShip.Velocity.Y:F1}   Z: {_playerShip.Velocity.Z:F1}";
            
            Vector2 posTextSize = _font.MeasureString(posText);
            Vector2 velTextSize = _font.MeasureString(velText);

            float maxWidth = Math.Max(posTextSize.X, velTextSize.X);
            float totalHeight = posTextSize.Y + velTextSize.Y;

            Vector2 position = new Vector2(
                (GraphicsDevice.Viewport.Width - maxWidth) / 2,
                GraphicsDevice.Viewport.Height - totalHeight - 15 // a bit more padding
            );

            Rectangle background = new Rectangle(
                (int)position.X - 10,
                (int)position.Y - 5,
                (int)maxWidth + 20,
                (int)totalHeight + 10
            );
            _spriteBatch.Draw(_pixel, background, Color.Black * 0.7f);

            _spriteBatch.DrawString(_font, posText, position, Color.White);
            _spriteBatch.DrawString(_font, velText, position + new Vector2(0, posTextSize.Y), Color.White);
        }

        private void DrawCurrentTargetDisplay() {
            // Only draw if we have a target selected
            if (_selectedSpaceObjectIndex < 0 || _selectedSpaceObjectIndex >= _spaceObjects.Count)
                return;

            SpaceObject target = _spaceObjects[_selectedSpaceObjectIndex];
            float distance = Vector3.Distance(_playerShip.Position, target.Position);

            // Draw target info at top center of screen
            int screenCenterX = GraphicsDevice.Viewport.Width / 2;
            int topY = 20;

            // Background panel
            Rectangle panel = new Rectangle(screenCenterX - 200, topY, 400, 80);
            _spriteBatch.Draw(_pixel, panel, Color.Black * 0.8f);
            _spriteBatch.Draw(_pixel, new Rectangle(panel.X, panel.Y, panel.Width, 3), Color.Orange);
            _spriteBatch.Draw(_pixel, new Rectangle(panel.X, panel.Y, 3, panel.Height), Color.Orange);
            _spriteBatch.Draw(_pixel, new Rectangle(panel.X + panel.Width - 3, panel.Y, 3, panel.Height), Color.Orange);
            _spriteBatch.Draw(_pixel, new Rectangle(panel.X, panel.Y + panel.Height - 3, panel.Width, 3), Color.Orange);

            if (_font != null) {
                // Target label
                _spriteBatch.DrawString(_font, "TARGET", new Vector2(panel.X + 10, panel.Y + 10), Color.Orange);

                // Target name (large)
                Vector2 nameSize = _font.MeasureString(target.Name);
                _spriteBatch.DrawString(_font, target.Name, new Vector2(screenCenterX - nameSize.X / 2, panel.Y + 30), Color.White);

                // Distance
                string distText = $"{distance / 1000f:F2} km";
                Vector2 distSize = _font.MeasureString(distText);
                _spriteBatch.DrawString(_font, distText, new Vector2(screenCenterX - distSize.X / 2, panel.Y + 55), Color.Cyan);
            } else {
                // Fallback: Just draw colored bars to indicate target is selected
                _spriteBatch.Draw(_pixel, new Rectangle(panel.X + 10, panel.Y + 30, (int)(380 * MathHelper.Clamp(distance / 10000f, 0f, 1f)), 30), Color.Orange * 0.8f);
            }

            // Draw targeting reticle on the actual target or off-screen indicator
            Vector3 screenPos3D = GraphicsDevice.Viewport.Project(target.Position, _camera.Projection, _camera.View, Matrix.Identity);
            
            // Check if target is on screen
            bool isOnScreen = screenPos3D.Z >= 0 && screenPos3D.Z <= 1 &&
                             screenPos3D.X >= 0 && screenPos3D.X <= GraphicsDevice.Viewport.Width &&
                             screenPos3D.Y >= 0 && screenPos3D.Y <= GraphicsDevice.Viewport.Height;
            
            if (isOnScreen)
            {
                // Target is visible - draw reticle on it
                Vector2 targetScreenPos = new Vector2(screenPos3D.X, screenPos3D.Y);
                DrawTargetingReticle(targetScreenPos, distance);
            }
            else
            {
                // Target is off-screen - draw directional arrow at screen edge
                DrawOffScreenTargetIndicator(target.Position, distance);
            }
        }

        /// <summary>
        /// Draw an arrow at the edge of the screen pointing toward an off-screen target
        /// </summary>
        private void DrawOffScreenTargetIndicator(Vector3 targetWorldPos, float distance)
        {
            // Calculate direction from camera to target in screen space
            Vector3 targetDir = targetWorldPos - _camera.Position;
            targetDir.Normalize();
            
            // Transform direction to camera space
            Matrix viewMatrix = _camera.View;
            Vector3 targetCameraSpace = Vector3.TransformNormal(targetDir, viewMatrix);
            
            // Project to 2D screen direction (ignore depth)
            Vector2 screenDir = new Vector2(targetCameraSpace.X, -targetCameraSpace.Y);
            
            // Handle case where target is behind camera
            if (targetCameraSpace.Z > 0)
            {
                screenDir = -screenDir;
            }
            
            if (screenDir.LengthSquared() > 0.0001f)
            {
                screenDir.Normalize();
            }
            else
            {
                // Fallback if we can't determine direction
                screenDir = Vector2.UnitX;
            }
            
            // Calculate arrow position at screen edge
            int screenWidth = GraphicsDevice.Viewport.Width;
            int screenHeight = GraphicsDevice.Viewport.Height;
            int edgeMargin = 50; // Distance from screen edge
            
            Vector2 screenCenter = new Vector2(screenWidth / 2f, screenHeight / 2f);
            Vector2 arrowPos = screenCenter;
            
            // Find intersection with screen rectangle
            float halfWidth = screenWidth / 2f - edgeMargin;
            float halfHeight = screenHeight / 2f - edgeMargin;
            
            // Calculate which edge the arrow should be on
            float tx = (screenDir.X != 0) ? halfWidth / Math.Abs(screenDir.X) : float.MaxValue;
            float ty = (screenDir.Y != 0) ? halfHeight / Math.Abs(screenDir.Y) : float.MaxValue;
            float t = Math.Min(tx, ty);
            
            arrowPos = screenCenter + screenDir * t;
            
            // Calculate arrow rotation angle
            float arrowAngle = (float)Math.Atan2(screenDir.Y, screenDir.X);
            
            // Draw the arrow indicator
            Color arrowColor = Color.Orange;
            float pulse = (float)Math.Sin(DateTime.Now.TimeOfDay.TotalSeconds * 4.0) * 0.3f + 0.7f;
            arrowColor = arrowColor * pulse;
            
            DrawDirectionalArrow(arrowPos, arrowAngle, 30f, arrowColor);
            
            // Draw distance text near arrow
            if (_font != null)
            {
                string distText = $"{distance / 1000f:F1}km";
                Vector2 textSize = _font.MeasureString(distText);
                Vector2 textOffset = new Vector2(-textSize.X / 2, -35); // Above arrow
                
                // Adjust text position to stay on screen
                Vector2 textPos = arrowPos + textOffset;
                textPos.X = MathHelper.Clamp(textPos.X, 5, screenWidth - textSize.X - 5);
                textPos.Y = MathHelper.Clamp(textPos.Y, 5, screenHeight - textSize.Y - 5);
                
                _spriteBatch.DrawString(_font, distText, textPos, arrowColor * 0.8f);
            }
        }

        /// <summary>
        /// Draw a directional arrow pointing in a specific direction
        /// </summary>
        private void DrawDirectionalArrow(Vector2 position, float angle, float size, Color color)
        {
            // Arrow is drawn pointing right (0 radians), then rotated
            Vector2 direction = new Vector2((float)Math.Cos(angle), (float)Math.Sin(angle));
            Vector2 perpendicular = new Vector2(-direction.Y, direction.X);
            
            // Arrow vertices (triangle pointing right + tail)
            Vector2 tip = position + direction * size;
            Vector2 back = position - direction * (size * 0.4f);
            Vector2 topWing = position + perpendicular * (size * 0.5f);
            Vector2 bottomWing = position - perpendicular * (size * 0.5f);
            
            // Draw arrow body (triangle)
            int thickness = 4;
            
            // Arrow tip to top wing
            DrawLine(tip, topWing, thickness, color);
            // Arrow tip to bottom wing
            DrawLine(tip, bottomWing, thickness, color);
            // Top wing to back
            DrawLine(topWing, back, thickness, color);
            // Bottom wing to back
            DrawLine(bottomWing, back, thickness, color);
            
            // Draw arrow outline for visibility
            DrawLine(tip, topWing, thickness + 2, Color.Black * 0.5f);
            DrawLine(tip, bottomWing, thickness + 2, Color.Black * 0.5f);
            DrawLine(topWing, back, thickness + 2, Color.Black * 0.5f);
            DrawLine(bottomWing, back, thickness + 2, Color.Black * 0.5f);
            
            // Fill the arrow
            DrawTriangle(tip, topWing, back, color * 0.7f);
            DrawTriangle(tip, bottomWing, back, color * 0.7f);
        }

        /// <summary>
        /// Draw a line between two points
        /// </summary>
        private void DrawLine(Vector2 start, Vector2 end, int thickness, Color color)
        {
            Vector2 edge = end - start;
            float angle = (float)Math.Atan2(edge.Y, edge.X);
            float length = edge.Length();
            
            _spriteBatch.Draw(_pixel,
                new Rectangle((int)start.X, (int)start.Y, (int)length, thickness),
                null,
                color,
                angle,
                new Vector2(0, 0.5f),
                SpriteEffects.None,
                0);
        }

        /// <summary>
        /// Draw a filled triangle (simple approximation using rectangles)
        /// </summary>
        private void DrawTriangle(Vector2 p1, Vector2 p2, Vector2 p3, Color color)
        {
            // Simple triangle fill - draw lines from center to edges
            Vector2 center = (p1 + p2 + p3) / 3f;
            
            // Draw multiple lines to create a filled effect
            int steps = 8;
            for (int i = 0; i <= steps; i++)
            {
                float t = i / (float)steps;
                Vector2 edge1 = Vector2.Lerp(p1, p2, t);
                Vector2 edge2 = Vector2.Lerp(p1, p3, t);
                DrawLine(edge1, edge2, 2, color);
            }
        }

        private void DrawTargetingReticle(Vector2 center, float distance) {
            // Dynamic size based on distance
            float scale = MathHelper.Clamp(8000f / distance, 0.5f, 3.0f);
            int size = (int)(40 * scale);
            int thickness = 3;
            Color color = Color.Orange;

            // Animated pulsing effect
            float pulse = (float)Math.Sin(DateTime.Now.TimeOfDay.TotalSeconds * 3.0) * 0.3f + 0.7f;
            color = color * pulse;

            // Draw cross-hair reticle
            // Horizontal line
            _spriteBatch.Draw(_pixel, new Rectangle((int)center.X - size, (int)center.Y - thickness / 2, size - 10, thickness), color);
            _spriteBatch.Draw(_pixel, new Rectangle((int)center.X + 10, (int)center.Y - thickness / 2, size - 10, thickness), color);

            // Vertical line
            _spriteBatch.Draw(_pixel, new Rectangle((int)center.X - thickness / 2, (int)center.Y - size, thickness, size - 10), color);
            _spriteBatch.Draw(_pixel, new Rectangle((int)center.X - thickness / 2, (int)center.Y + 10, thickness, size - 10), color);

            // Corner brackets (larger and more prominent)
            int bracketLen = (int)(30 * scale);
            int bracketThick = 4;

            // Top-left
            _spriteBatch.Draw(_pixel, new Rectangle((int)center.X - size, (int)center.Y - size, bracketLen, bracketThick), color);
            _spriteBatch.Draw(_pixel, new Rectangle((int)center.X - size, (int)center.Y - size, bracketThick, bracketLen), color);

            // Top-right
            _spriteBatch.Draw(_pixel, new Rectangle((int)center.X + size - bracketLen, (int)center.Y - size, bracketLen, bracketThick), color);
            _spriteBatch.Draw(_pixel, new Rectangle((int)center.X + size - bracketThick, (int)center.Y - size, bracketThick, bracketLen), color);

            // Bottom-left
            _spriteBatch.Draw(_pixel, new Rectangle((int)center.X - size, (int)center.Y + size - bracketThick, bracketLen, bracketThick), color);
            _spriteBatch.Draw(_pixel, new Rectangle((int)center.X - size, (int)center.Y + size - bracketLen, bracketThick, bracketLen), color);

            // Bottom-right
            _spriteBatch.Draw(_pixel, new Rectangle((int)center.X + size - bracketLen, (int)center.Y + size - bracketThick, bracketLen, bracketThick), color);
            _spriteBatch.Draw(_pixel, new Rectangle((int)center.X + size - bracketThick, (int)center.Y + size - bracketLen, bracketThick, bracketLen), color);

            // Center dot
            int dotSize = (int)(6 * scale);
            _spriteBatch.Draw(_pixel, new Rectangle((int)center.X - dotSize / 2, (int)center.Y - dotSize / 2, dotSize, dotSize), color);
        }

        private void DrawStatusPanel() {
            // Draw status box background - made taller for hull bar
            Rectangle panel = new Rectangle(10, 10, 280, 270);
            _spriteBatch.Draw(_pixel, panel, Color.Black * 0.7f);
            _spriteBatch.Draw(_pixel, new Rectangle(10, 10, 280, 2), Color.Cyan);
            _spriteBatch.Draw(_pixel, new Rectangle(10, 10, 2, 270), Color.Cyan);
            _spriteBatch.Draw(_pixel, new Rectangle(288, 10, 2, 270), Color.Cyan);
            _spriteBatch.Draw(_pixel, new Rectangle(10, 278, 280, 2), Color.Cyan);

            if (_font != null) {
                _spriteBatch.DrawString(_font, "STATUS", new Vector2(20, 20), Color.Cyan);
                
                // Hull integrity display
                float hullPercent = _playerShip.Hull.HullPercentage;
                Color hullColor = _playerShip.Hull.GetHullColor();
                _spriteBatch.DrawString(_font, $"Hull: {hullPercent * 100:F0}%", new Vector2(20, 45), hullColor);
                
                // Hull bar
                Rectangle hullBarBg = new Rectangle(20, 65, 250, 15);
                Rectangle hullBarFill = new Rectangle(20, 65, (int)(250 * hullPercent), 15);
                _spriteBatch.Draw(_pixel, hullBarBg, Color.DarkGray * 0.5f);
                _spriteBatch.Draw(_pixel, hullBarFill, hullColor);
                
                // Other status info
                _spriteBatch.DrawString(_font, $"Speed: {Math.Abs(_playerShip.Speed):F1}", new Vector2(20, 90), Color.White);
                _spriteBatch.DrawString(_font, $"Throttle: {_playerShip.GetThrottle() * 100:F0}%", new Vector2(20, 110), Color.White);
                _spriteBatch.DrawString(_font, $"Mode: {_playerShip.GetFlightStatus()}", new Vector2(20, 130), Color.Yellow);
                
                // Weapon display
                _spriteBatch.DrawString(_font, $"Weapon: {_weaponSystem.CurrentWeapon}", new Vector2(20, 150), Color.Orange);
                
                // Charge beam indicator
                if (_weaponSystem.IsCharging())
                {
                    float chargeProgress = _weaponSystem.GetChargeProgress();
                    _spriteBatch.DrawString(_font, $"CHARGING: {chargeProgress * 100:F0}%", new Vector2(20, 170), Color.Yellow);
                    
                    // Charge bar
                    Rectangle chargeBarBg = new Rectangle(20, 190, 250, 15);
                    Rectangle chargeBarFill = new Rectangle(20, 190, (int)(250 * chargeProgress), 15);
                    _spriteBatch.Draw(_pixel, chargeBarBg, Color.DarkGray * 0.5f);
                    _spriteBatch.Draw(_pixel, chargeBarFill, Color.Lerp(Color.Yellow, Color.Red, chargeProgress));
                }
                
                if (_playerShip.IsNewtonianMode)
                    _spriteBatch.DrawString(_font, "NEWTONIAN", new Vector2(20, 210), Color.Lime);
                if (_camera.IsTurretViewActive)
                    _spriteBatch.DrawString(_font, "TURRET VIEW", new Vector2(20, 230), Color.Orange);
                if (_playerShip.IsGotoActive && _playerShip.CurrentGotoTarget != null)
                    _spriteBatch.DrawString(_font, $"GOTO: {_playerShip.CurrentGotoTarget.Name}", new Vector2(20, 250), Color.Orange);
            } else {
                // Fallback visual indicators when no font available
                // Hull bar (horizontal)
                float hullPercent = _playerShip.Hull.HullPercentage;
                Color hullColor = _playerShip.Hull.GetHullColor();
                int hullBarWidth = (int)(250 * hullPercent);
                _spriteBatch.Draw(_pixel, new Rectangle(20, 30, 250, 20), Color.DarkGray * 0.5f);
                _spriteBatch.Draw(_pixel, new Rectangle(20, 30, hullBarWidth, 20), hullColor);

                // Speed bar (horizontal)
                int speedBarWidth = (int)(250 * MathHelper.Clamp(Math.Abs(_playerShip.Speed) / _playerShip.MaxSpeed, 0f, 1f));
                _spriteBatch.Draw(_pixel, new Rectangle(20, 60, 250, 20), Color.DarkGray * 0.5f);
                _spriteBatch.Draw(_pixel, new Rectangle(20, 60, speedBarWidth, 20), Color.Cyan);

                // Throttle bar (horizontal)
                float throttle = _playerShip.GetThrottle();
                int throttleBarWidth = (int)(250 * Math.Abs(throttle));
                Color throttleColor = throttle >= 0 ? Color.Green : Color.Orange;
                _spriteBatch.Draw(_pixel, new Rectangle(20, 90, 250, 20), Color.DarkGray * 0.5f);
                _spriteBatch.Draw(_pixel, new Rectangle(20, 90, throttleBarWidth, 20), throttleColor);

                // Weapon/charge indicator
                if (_weaponSystem.IsCharging())
                {
                    float chargeProgress = _weaponSystem.GetChargeProgress();
                    Rectangle chargeBar = new Rectangle(20, 120, (int)(250 * chargeProgress), 20);
                    _spriteBatch.Draw(_pixel, chargeBar, Color.Lerp(Color.Yellow, Color.Red, chargeProgress));
                }

                // Status indicators
                int yPos = 150;
                if (_playerShip.IsAfterburnerActive) {
                    _spriteBatch.Draw(_pixel, new Rectangle(20, yPos, 250, 20), Color.Red * 0.7f);
                    yPos += 25;
                }
                if (_playerShip.IsCruiseActive) {
                    _spriteBatch.Draw(_pixel, new Rectangle(20, yPos, 250, 20), Color.Yellow * 0.7f);
                    yPos += 25;
                }
                if (_playerShip.IsNewtonianMode) {
                    _spriteBatch.Draw(_pixel, new Rectangle(20, yPos, 250, 20), Color.Lime * 0.7f);
                    yPos += 25;
                }
                if (_camera.IsTurretViewActive) {
                    _spriteBatch.Draw(_pixel, new Rectangle(20, yPos, 250, 20), Color.Orange * 0.7f);
                    yPos += 25;
                }
                if (_playerShip.EnginesKilled) {
                    _spriteBatch.Draw(_pixel, new Rectangle(20, yPos, 250, 20), Color.Gray * 0.7f);
                }
            }
        }
        
        private void DrawSpaceObjectBrackets() {
            if (_spaceObjects.Count == 0) return;
            for (int i = 0; i < _spaceObjects.Count; i++) {
                SpaceObject obj = _spaceObjects[i];
                Vector3 screenPos3D = GraphicsDevice.Viewport.Project(obj.Position, _camera.Projection, _camera.View, Matrix.Identity);
                if (screenPos3D.Z < 0 || screenPos3D.Z > 1) continue; // behind camera
                Vector2 screenPos = new Vector2(screenPos3D.X, screenPos3D.Y);
                float distance = Vector3.Distance(_playerShip.Position, obj.Position);
                float scale = MathHelper.Clamp(8000f / distance, 0.4f, 2.5f);
                DrawTargetBracket(screenPos, scale, i == _selectedSpaceObjectIndex);
                if (_font != null) {
                    _spriteBatch.DrawString(_font, obj.Name, screenPos + new Vector2(12, -20), i == _selectedSpaceObjectIndex ? Color.Orange : Color.LightBlue);
                    _spriteBatch.DrawString(_font, $"{distance / 1000f:F2}k", screenPos + new Vector2(12, 0), Color.LightGray);
                }
            }
            
            // Draw NPC ship hull bars
            foreach (var npc in _npcShips)
            {
                if (npc.IsDestroyed) continue; // Don't draw hull bar for destroyed ships
                
                // Project ship position to screen
                Vector3 screenPos3D = GraphicsDevice.Viewport.Project(
                    npc.Position + new Vector3(0, 15, 0), // Offset above ship
                    _camera.Projection, 
                    _camera.View, 
                    Matrix.Identity
                );
                
                if (screenPos3D.Z < 0 || screenPos3D.Z > 1) continue; // Behind camera
                
                Vector2 screenPos = new Vector2(screenPos3D.X, screenPos3D.Y);
                float distance = Vector3.Distance(_playerShip.Position, npc.Position);
                
                // Only show hull bar for nearby ships (within 2000 units)
                if (distance > 2000f) continue;
                
                // Hull bar dimensions
                int barWidth = 60;
                int barHeight = 6;
                float hullPercent = npc.Hull.HullPercentage;
                Color hullColor = npc.Hull.GetHullColor();
                
                // Center the bar on screen position
                Rectangle barBg = new Rectangle(
                    (int)(screenPos.X - barWidth / 2), 
                    (int)screenPos.Y, 
                    barWidth, 
                    barHeight
                );
                Rectangle barFill = new Rectangle(
                    (int)(screenPos.X - barWidth / 2), 
                    (int)screenPos.Y, 
                    (int)(barWidth * hullPercent), 
                    barHeight
                );
                
                // Draw background (dark)
                _spriteBatch.Draw(_pixel, barBg, Color.Black * 0.7f);
                // Draw fill (health color)
                _spriteBatch.Draw(_pixel, barFill, hullColor);
                // Draw border
                _spriteBatch.Draw(_pixel, new Rectangle(barBg.X, barBg.Y, barBg.Width, 1), Color.White * 0.5f);
                _spriteBatch.Draw(_pixel, new Rectangle(barBg.X, barBg.Bottom - 1, barBg.Width, 1), Color.White * 0.5f);
                _spriteBatch.Draw(_pixel, new Rectangle(barBg.X, barBg.Y, 1, barBg.Height), Color.White * 0.5f);
                _spriteBatch.Draw(_pixel, new Rectangle(barBg.Right - 1, barBg.Y, 1, barBg.Height), Color.White * 0.5f);
                
                // Optional: Draw ship name below bar if close enough
                if (_font != null && distance < 1000f)
                {
                    Vector2 nameSize = _font.MeasureString(npc.Name);
                    _spriteBatch.DrawString(_font, npc.Name, 
                        new Vector2(screenPos.X - nameSize.X / 2, screenPos.Y + barHeight + 2), 
                        Color.White * 0.8f);
                }
            }
        }

        private void DrawTargetBracket(Vector2 center, float scale, bool selected) {
            int lineLen = (int)(20 * scale);
            int thickness = 2;
            Color col = selected ? Color.Orange : Color.LightBlue;
            // Corners
            // Top-left
            _spriteBatch.Draw(_pixel, new Rectangle((int)(center.X - lineLen), (int)(center.Y - lineLen), lineLen, thickness), col);
            _spriteBatch.Draw(_pixel, new Rectangle((int)(center.X - lineLen), (int)(center.Y - lineLen), thickness, lineLen), col);
            // Top-right
            _spriteBatch.Draw(_pixel, new Rectangle((int)(center.X), (int)(center.Y - lineLen), lineLen, thickness), col);
            _spriteBatch.Draw(_pixel, new Rectangle((int)(center.X + lineLen - thickness), (int)(center.Y - lineLen), thickness, lineLen), col);
            // Bottom-left
            _spriteBatch.Draw(_pixel, new Rectangle((int)(center.X - lineLen), (int)(center.Y), lineLen, thickness), col);
            _spriteBatch.Draw(_pixel, new Rectangle((int)(center.X - lineLen), (int)(center.Y + lineLen - thickness), thickness, lineLen), col);
            // Bottom-right
            _spriteBatch.Draw(_pixel, new Rectangle((int)(center.X), (int)(center.Y), lineLen, thickness), col);
            _spriteBatch.Draw(_pixel, new Rectangle((int)(center.X + lineLen - thickness), (int)(center.Y), thickness, lineLen), col);
        }

        private void DrawGotoStatus() {
            if (_playerShip.IsGotoActive && _playerShip.CurrentGotoTarget != null && _font != null) {
                Vector2 pos = new Vector2(GraphicsDevice.Viewport.Width / 2 - 120, 30);
                _spriteBatch.Draw(_pixel, new Rectangle((int)pos.X - 10, (int)pos.Y - 10, 250, 50), Color.Black * 0.4f);
                float dist = Vector3.Distance(_playerShip.Position, _playerShip.CurrentGotoTarget.Position) - _playerShip.CurrentGotoTarget.Radius;
                _spriteBatch.DrawString(_font, $"GOTO: {_playerShip.CurrentGotoTarget.Name}", pos, Color.Orange);
                _spriteBatch.DrawString(_font, $"DIST: {Math.Max(dist, 0) / 1000f:F2} k", pos + new Vector2(0, 22), Color.White);
            }
        }

        private void DrawSunDistanceIndicator() {
            // Calculate distance to sun
            float sunDistance = _sun.GetDistance(_playerShip.Position);
            float sunDistanceKm = sunDistance / 1000f;

            // Draw sun distance panel (top-right) - MADE BIGGER
            Rectangle panel = new Rectangle(GraphicsDevice.Viewport.Width - 320, 10, 310, 180);
            _spriteBatch.Draw(_pixel, panel, Color.Black * 0.8f);
            _spriteBatch.Draw(_pixel, new Rectangle(panel.X, panel.Y, panel.Width, 3), Color.Orange);
            _spriteBatch.Draw(_pixel, new Rectangle(panel.X, panel.Y, 3, panel.Height), Color.Orange);
            _spriteBatch.Draw(_pixel, new Rectangle(panel.X + panel.Width - 3, panel.Y, 3, panel.Height), Color.Orange);
            _spriteBatch.Draw(_pixel, new Rectangle(panel.X, panel.Y + panel.Height - 3, panel.Width, 3), Color.Orange);

            // Draw title bar with "SUN" label using bars
            _spriteBatch.Draw(_pixel, new Rectangle(panel.X + 5, panel.Y + 5, panel.Width - 10, 40), Color.Orange * 0.4f);

            // Distance bar visualization (8km = full bar, 0km = empty) - MADE TALLER
            float maxDistance = 8000f; // Max distance to show (8km)
            float distanceRatio = MathHelper.Clamp(1f - (sunDistance / maxDistance), 0f, 1f);
            int barWidth = (int)((panel.Width - 20) * distanceRatio);

            // Color changes as you get closer: Blue -> Yellow -> Orange -> Red
            Color distanceColor;
            if (distanceRatio < 0.25f) distanceColor = Color.Cyan; // Far
            else if (distanceRatio < 0.5f) distanceColor = Color.Yellow; // Medium
            else if (distanceRatio < 0.75f) distanceColor = Color.Orange; // Close
            else distanceColor = Color.Red; // Very close!

            // Background bar - MUCH TALLER
            _spriteBatch.Draw(_pixel, new Rectangle(panel.X + 10, panel.Y + 50, panel.Width - 20, 60), Color.DarkGray * 0.5f);
            // Distance bar (fills as you get closer)
            _spriteBatch.Draw(_pixel, new Rectangle(panel.X + 10, panel.Y + 50, barWidth, 60), distanceColor * 0.9f);

            // Numeric display using stacked bars (like 7-segment display)
            DrawNumericDistance(panel.X + 15, panel.Y + 115, sunDistanceKm);
        }

        private void DrawNumericDistance(int x, int y, float distanceKm) {
            // Simple bar-based numeric display
            // Shows distance in kilometers with 2 digits
            int distance = (int)distanceKm;
            int thousands = (distance / 1000) % 10;
            int hundreds = (distance / 100) % 10;
            int tens = (distance / 10) % 10;
            int ones = distance % 10;

            // Draw simplified digit bars
            int digitWidth = 50;
            int spacing = 10;

            if (thousands > 0) {
                DrawSimpleDigit(x, y, thousands);
                x += digitWidth + spacing;
            }
            if (thousands > 0 || hundreds > 0) {
                DrawSimpleDigit(x, y, hundreds);
                x += digitWidth + spacing;
            }
            DrawSimpleDigit(x, y, tens);
            x += digitWidth + spacing;
            DrawSimpleDigit(x, y, ones);
        }

        private void DrawSimpleDigit(int x, int y, int digit) {
            // Draw a simple bar representation of the digit
            int barHeight = 5;

            Color digitColor = Color.Cyan;

            // Just draw a vertical bar with height representing the digit (0-9)
            // Each unit adds one segment
            int totalHeight = digit * barHeight;
            _spriteBatch.Draw(_pixel, new Rectangle(x, y + (9 - digit) * barHeight, 40, totalHeight), digitColor);

            // Draw outline
            _spriteBatch.Draw(_pixel, new Rectangle(x, y, 40, 2), digitColor * 0.5f);
            _spriteBatch.Draw(_pixel, new Rectangle(x, y + 45, 40, 2), digitColor * 0.5f);
            _spriteBatch.Draw(_pixel, new Rectangle(x, y, 2, 45), digitColor * 0.5f);
            _spriteBatch.Draw(_pixel, new Rectangle(x + 38, y, 2, 45), digitColor * 0.5f);
        }
    }
}