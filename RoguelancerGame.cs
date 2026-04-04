using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
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
        /// Explosion Particles
        /// </summary>
        private ExplosionParticles _explosionParticles;
        /// <summary>
        /// Hit Impact Particles
        /// </summary>
        private HitImpactParticles _hitImpactParticles;
        /// <summary>
        /// Damage Smoke Particles
        /// </summary>
        private DamageSmokeParticles _damageSmokeParticles;
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
        private PlanetManager _planetManager;
        private StationManager _stationManager;

        // Network Manager for multiplayer
        private NetworkManager? _networkManager;
        private GameSettings _gameSettings;
        private float _networkUpdateTimer = 0f;
        private Dictionary<string, RemotePlayer> _remotePlayers = new();

        // Docking system
        private StationDockUI _stationDockUI;
        private PlayerCredits _playerCredits;
        private ShipDealer _shipDealer;
        private CommodityDealer _commodityDealer;

        // Input state tracking
        private KeyboardState _prevKeys;
        private MouseState _prevMouseState;

        // Debug tracking
        private float _debugTimer = 0f;
        private Vector3 _lastPlayerPosition;
        private bool _firstFrame = true;

        /// <summary>
        /// Alloc Console
        /// </summary>
        /// <returns></returns>
        [System.Runtime.InteropServices.DllImport("kernel32.dll", SetLastError = true)]
        [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
        static extern bool AllocConsole();

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

            // Load game settings
            _gameSettings = GameSettings.Load();

            AllocConsole();
            Console.WriteLine("========================================");
            Console.WriteLine("    ROGUELANCER DEBUG CONSOLE");
            Console.WriteLine("========================================");
            Console.WriteLine("Game initialized. Console ready for debug output.");
            Console.WriteLine($"Online Mode: {(_gameSettings.EnableOnlineMode ? "ENABLED" : "DISABLED")}");
            Console.WriteLine();
            _graphics.ApplyChanges();

            // Initialize network manager if online mode is enabled
            if (_gameSettings.EnableOnlineMode) {
                InitializeNetworkManager();
            }
        }

        /// <summary>
        /// Initialize the network manager for multiplayer
        /// </summary>
        private async void InitializeNetworkManager() {
            _networkManager = new NetworkManager();

            // Set up event handlers
            _networkManager.OnPlayerJoined += OnRemotePlayerJoined;
            _networkManager.OnPlayerLeft += OnRemotePlayerLeft;
            _networkManager.OnPlayerStateUpdated += OnRemotePlayerStateUpdated;
            _networkManager.OnProjectileFired += OnRemoteProjectileFired;
            _networkManager.OnPlayerHit += OnRemotePlayerHit;
            _networkManager.OnPlayerDestroyed += OnRemotePlayerDestroyed;
            _networkManager.OnChatMessageReceived += OnChatMessageReceived;
            _networkManager.OnWorldInitialized += OnWorldInitialized;

            if (_gameSettings.AutoConnect) {
                Console.WriteLine($"[NETWORK] Connecting to {_gameSettings.ServerUrl}...");
                bool connected = await _networkManager.ConnectAsync(_gameSettings.PlayerName, _gameSettings.ServerUrl);

                if (connected) {
                    _notificationManager?.ShowMessage($"Connected as {_gameSettings.PlayerName}", 3f);
                    Console.WriteLine("[NETWORK] Successfully connected to server!");
                } else {
                    _notificationManager?.ShowMessage("Failed to connect to server", 3f);
                    Console.WriteLine("[NETWORK] Failed to connect to server");
                }
            }
        }

        // Network event handlers
        private void OnWorldInitialized(List<RemotePlayer> players) {
            Console.WriteLine($"[NETWORK] World initialized with {players.Count} players");
            foreach (var player in players) {
                _remotePlayers[player.PlayerId] = player;
                // Load model for remote player
                if (_playerShip?.Model != null) {
                    player.Model = _playerShip.Model;
                }
            }
        }

        private void OnRemotePlayerJoined(RemotePlayer player) {
            _remotePlayers[player.PlayerId] = player;
            _notificationManager?.ShowMessage($"{player.PlayerName} joined", 2f);

            // Load model for remote player
            if (_playerShip?.Model != null) {
                player.Model = _playerShip.Model;
            }
        }

        private void OnRemotePlayerLeft(string playerId) {
            if (_remotePlayers.TryGetValue(playerId, out var player)) {
                _notificationManager?.ShowMessage($"{player.PlayerName} left", 2f);
                _remotePlayers.Remove(playerId);
            }
        }

        private void OnRemotePlayerStateUpdated(RemotePlayer player) {
            _remotePlayers[player.PlayerId] = player;
        }

        private void OnRemoteProjectileFired(NetworkProjectile projectile) {
            // Add remote projectile to weapon system
            Console.WriteLine($"[NETWORK] Remote projectile fired by {projectile.OwnerId}");
            // You can add this to your weapon system to render remote projectiles
        }

        private void OnRemotePlayerHit(string targetPlayerId, float damage) {
            if (targetPlayerId == _networkManager?.PlayerId) {
                // We got hit!
                _playerShip?.Hull.TakeDamage(damage);
                _notificationManager?.ShowMessage($"Hit! -{damage:F0} hull", 1f);
            }
        }

        private void OnRemotePlayerDestroyed(string playerId) {
            if (playerId == _networkManager?.PlayerId) {
                _notificationManager?.ShowMessage("YOU WERE DESTROYED!", 5f);
            } else if (_remotePlayers.TryGetValue(playerId, out var player)) {
                _notificationManager?.ShowMessage($"{player.PlayerName} destroyed!", 2f);
            }
        }

        private void OnChatMessageReceived(string playerName, string message) {
            _notificationManager?.ShowMessage($"{playerName}: {message}", 3f);
        }

        /// <summary>
        /// Initialize
        /// </summary>
        protected override void Initialize() {
            float aspectRatio = GraphicsDevice.Viewport.AspectRatio;
            _camera = new Camera(aspectRatio);
            _playerShip = new Ship(new Vector3(3000, 1500, 2000)); // Start closer to sun and station
            Window.ClientSizeChanged += OnWindowFocusChanged;
            this.Activated += OnWindowActivated;
            this.Deactivated += OnWindowDeactivated;

            _planetManager = new PlanetManager(Content, GraphicsDevice);

            // Initialize StationManager with GraphicsDevice
            _stationManager = new StationManager(Content, GraphicsDevice);

            // Load configuration files
            _config.LoadAll();

            // Get current system (default to system 1)
            SystemConfig currentSystem = _config.GetSystem(1);
            if (currentSystem != null) {
                Console.WriteLine($"[SYSTEM] Loaded: {currentSystem.Description}");

                // Load the planetary system from JSON
                _planetManager.LoadSystem("system_01_new_york.json");
                foreach (var planet in _planetManager.GetPlanets()) {
                    _spaceObjects.Add(planet);
                }

                // Load stations
                try {
                    _stationManager.LoadStations();
                    foreach (var station in _stationManager.GetStations()) {
                        _spaceObjects.Add(station);
                        Console.WriteLine($"[STATION] Loaded: {station.Name} at {station.Position}");
                    }
                } catch (Exception ex) {
                    Console.WriteLine($"[ERROR] Failed to load stations: {ex.Message}");
                }

                // Create sun from system config
                _sun = new Sun(GraphicsDevice,
                    new Vector3(currentSystem.SunPositionX, currentSystem.SunPositionY, currentSystem.SunPositionZ),
                    scale: currentSystem.SunScale * 15.0f) // Make it 15x larger = 30,000 unit diameter!
                {
                    EmissiveColor = new Color(255, 220, 180), // Warm yellow-orange
                    EmissiveIntensity = currentSystem.SunIntensity * 3.0f // Also much brighter
                };

                Console.WriteLine($"[SUN] INITIALIZED at position: {_sun.Position}, scale: {currentSystem.SunScale * 15.0f}, intensity: {currentSystem.SunIntensity * 3.0f}");
                Console.WriteLine($"[SUN] Distance from origin: {_sun.Position.Length():F2} units");
                Console.WriteLine($"[SUN] Player will start at: (3000, 1500, 2000) = ~2,828 units from sun");

                Console.WriteLine($"[PLANETS] Loaded {_planetManager.GetPlanets().Count} planets");
                foreach (var planet in _planetManager.GetPlanets()) {
                    Console.WriteLine($" - {planet.Name} at {planet.Position}");
                }

                Console.WriteLine($"[STATIONS] Loaded {_stationManager.GetStations().Count} stations");
                foreach (var station in _stationManager.GetStations()) {
                    Console.WriteLine($" - {station.Name} at {station.Position}");
                }
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
                var stationObject = new SpaceObject(
                    stationConfig.Description,
                    stationConfig.StartupPosition,
                    stationConfig.Radius
                );

                // Handle loading by model path OR model index
                if (!string.IsNullOrEmpty(stationConfig.ModelPath)) {
                    stationObject.ModelPath = stationConfig.ModelPath;
                } else if (stationConfig.ModelIndex > 0) {
                    ModelConfig modelConf = _config.GetModel(stationConfig.ModelIndex);
                    if (modelConf != null) {
                        stationObject.ModelPath = modelConf.Path;
                    }
                }

                _spaceObjects.Add(stationObject);
                Console.WriteLine($"[STATION] Added: {stationConfig.Description} at {stationConfig.StartupPosition}");
            }

            // Spawn NPC ships from system patrol configuration
            if (currentSystem != null && currentSystem.NpcPatrols != null) {
                SpawnNpcsFromConfig(currentSystem);
            }

            Console.WriteLine($"[NPC SPAWN] COMPLETE: Created {_npcShips.Count} NPC ships total");
            Console.WriteLine($"[NPC SPAWN] Space objects count: {_spaceObjects.Count}");

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

        /// <summary>
        /// Spawns NPC ships based on the patrol configurations for the current system.
        /// </summary>
        /// <param name="currentSystem">The configuration of the current system.</param>
        private void SpawnNpcsFromConfig(SystemConfig currentSystem) {
            var random = new Random();
            var allShipConfigs = _config.GetAllShipConfigs(); // Assuming this method exists to get all ships

            Console.WriteLine($"[NPC SPAWN] Starting NPC creation from system config. Patrol groups: {currentSystem.NpcPatrols.Count}");

            foreach (var patrolConfig in currentSystem.NpcPatrols) {
                // Find the ship configuration that matches the description in the patrol config
                var shipConfig = allShipConfigs.Find(sc => sc.Description == patrolConfig.ShipDescription);

                if (shipConfig == null) {
                    Console.WriteLine($"[NPC SPAWN] ✗ ERROR: Ship configuration '{patrolConfig.ShipDescription}' not found for patrol '{patrolConfig.Name}'. Skipping.");
                    continue;
                }

                Console.WriteLine($"[NPC SPAWN] Creating {patrolConfig.Count} ships for patrol '{patrolConfig.Name}' using '{shipConfig.Description}'.");

                for (int i = 0; i < patrolConfig.Count; i++) {
                    // Create a random offset within the spawn radius
                    float spawnAngle = (float)(random.NextDouble() * 2 * Math.PI);
                    float spawnDist = (float)(random.NextDouble() * patrolConfig.SpawnRadius);
                    var spawnOffset = new Vector3(
                        (float)Math.Cos(spawnAngle) * spawnDist,
                        (float)(random.NextDouble() * 2000 - 1000), // Random height offset
                        (float)Math.Sin(spawnAngle) * spawnDist
                    );

                    var patrolCenter = new Vector3(patrolConfig.PatrolCenterX, patrolConfig.PatrolCenterY, patrolConfig.PatrolCenterZ);
                    var spawnPosition = patrolCenter + spawnOffset;

                    // Create the NPC ship
                    NpcShip npc = new NpcShip(
                        $"{shipConfig.Description} {i + 1}",
                        spawnPosition,
                        patrolCenter,
                        patrolConfig.PatrolRadius,
                        patrolConfig.PatrolSpeed
                    );

                    // Assign model path for later loading
                    ModelConfig model = _config.GetModel(shipConfig.ModelIndex);
                    if (model != null) {
                        npc.ModelPath = model.Path;
                        // Apply model-specific rotation corrections from ship config
                        npc.ModelRotationCorrection = shipConfig.ModelCorrectionRotation;
                    } else {
                        Console.WriteLine($"[NPC SPAWN] ✗ WARNING: Invalid model index {shipConfig.ModelIndex} for ship '{shipConfig.Description}'.");
                    }

                    _npcShips.Add(npc);
                    _spaceObjects.Add(npc); // Add to targetable objects
                    npc.OnDestroyed += HandleNpcDestroyed; // Subscribe to the event

                    Console.WriteLine($"  ✓ Created ship: {npc.Name} at {npc.Position:F1}");
                }
            }
        }

        protected override void LoadContent() {
            _spriteBatch = new SpriteBatch(GraphicsDevice);

            // Load font for HUD text
            try {
                _font = Content.Load<SpriteFont>("Fonts/HudFont");
                Console.WriteLine("HUD Font loaded successfully!");
            } catch (Exception ex) {
                _font = null;
                Console.WriteLine($"Font not loaded: {ex.Message}");
            }

            // Initialize pixel texture
            _pixel = new Texture2D(GraphicsDevice, 1, 1);
            _pixel.SetData(new[] { Color.White });

            // Initialize player credits with enough to buy any ship
            _playerCredits = new PlayerCredits(50000); // Start with 50,000 credits

            // Initialize ship dealer
            _shipDealer = new ShipDealer();
            _shipDealer.LoadShipModels(Content);

            // Initialize commodity dealer
            _commodityDealer = new CommodityDealer();

            // Initialize station dock UI
            if (_font != null) {
                _stationDockUI = new StationDockUI(_font, _pixel, _shipDealer, _commodityDealer);
                _stationDockUI.OnUndock += HandleUndock;
                _stationDockUI.OnShipPurchased += HandleShipPurchased;
            }

            // Load ship model
            try {
                _playerShip.Model = Content.Load<Model>("SHIPS/scimitar/Scimitar2");
                Console.WriteLine("Scimitar model loaded successfully!");

                // Load wreck model
                try {
                    _wreckModel = Content.Load<Model>("MODELS/wreck");
                    Console.WriteLine("Wreck model loaded successfully!");
                } catch (Exception ex) {
                    Console.WriteLine($"Error loading wreck model: {ex.Message}");
                    // Fallback to a simple model or null if it fails
                    _wreckModel = null;
                }

                // Load models for all space objects that have a model path
                foreach (var spaceObject in _spaceObjects) {
                    if (!string.IsNullOrEmpty(spaceObject.ModelPath)) {
                        try {
                            spaceObject.Model = Content.Load<Model>(spaceObject.ModelPath);
                            Console.WriteLine($"[MODEL LOAD] ✓ SUCCESS - Loaded model '{spaceObject.ModelPath}' for {spaceObject.Name}");
                        } catch (Exception ex) {
                            Console.WriteLine($"[MODEL LOAD] ✗ ERROR - Failed to load model for {spaceObject.Name}: {ex.Message}");
                            Console.WriteLine($"[MODEL LOAD]   Attempted path: {spaceObject.ModelPath}");
                        }
                    }
                }

                // Load models for NPC ships based on their configuration
                Console.WriteLine($"[MODEL LOAD] Starting to load models for {_npcShips.Count} NPC ships");

                foreach (var npc in _npcShips) {
                    if (!string.IsNullOrEmpty(npc.ModelPath)) {
                        try {
                            npc.Model = Content.Load<Model>(npc.ModelPath);
                            Console.WriteLine($"[MODEL LOAD] ✓ SUCCESS - Loaded model '{npc.ModelPath}' for {npc.Name}");
                        } catch (Exception ex) {
                            Console.WriteLine($"[MODEL LOAD] ✗ ERROR - Failed to load model for {npc.Name}: {ex.Message}");
                            Console.WriteLine($"[MODEL LOAD]   Attempted path: {npc.ModelPath}");
                            npc.Model = _playerShip.Model; // Fallback to player model
                            Console.WriteLine($"[MODEL LOAD]   Using fallback (player model)");
                        }
                    } else {
                        Console.WriteLine($"[MODEL LOAD] ✗ SKIPPED - No model path defined for {npc.Name}, using fallback.");
                        npc.Model = _playerShip.Model;
                    }
                }
                Console.WriteLine($"[MODEL LOAD] COMPLETE - Loaded models for {_npcShips.Count} NPC ships!");
            } catch (Exception ex) {
                Console.WriteLine($"Error loading model: {ex.Message}");
                Console.WriteLine("Make sure the FBX file is added to the Content Pipeline.");
            }

            // Initialize StationManager with GraphicsDevice and load stations
            _stationManager = new StationManager(Content, GraphicsDevice);
            try {
                _stationManager.LoadStations();
            } catch (Exception ex) {
                Console.WriteLine($"Failed to load stations: {ex.Message}");
            }

            // Load sun content
            _sun.LoadContent(Content);

            // Create starfield with multiple layers for parallax effect
            _starfield = new Starfield(GraphicsDevice, new int[] { 7000, 2000, 1000 }); // Far, mid, near layers

            // Create engine glow effect
            _engineGlow = new EngineGlow(GraphicsDevice);

            // Initialize engine trail
            _engineTrail = new EngineTrail(GraphicsDevice);

            // Initialize cruise sparks (firefly effect during charge)
            _cruiseSparks = new CruiseSparks(GraphicsDevice);

            // Initialize charge particles (energy buildup while charging beam weapon)
            _chargeParticles = new ChargeParticles(GraphicsDevice);

            // Initialize explosion particles (ship destruction effects)
            _explosionParticles = new ExplosionParticles(GraphicsDevice);

            // Initialize damage smoke particles
            _damageSmokeParticles = new DamageSmokeParticles(GraphicsDevice);

            // Initialize hit impact particles (weapon hit effects)
            _hitImpactParticles = new HitImpactParticles(GraphicsDevice);

            // Initialize weapon system (blasters)
            _weaponSystem = new WeaponSystem(GraphicsDevice);
            _weaponSystem.SetEnergySystem(_playerShip.Energy);

            // Initialize motion trail
            _motionTrail = new MotionTrail(GraphicsDevice);

            // Initialize notification manager
            _notificationManager = new NotificationManager(_font, GraphicsDevice.Viewport);
            _playerShip.SetNotificationManager(_notificationManager);
            _playerShip.SetExplosionSystem(_explosionParticles);
            _playerShip.SetDamageSmokeSystem(_damageSmokeParticles);
        }

        protected override void Update(GameTime gameTime) {
            KeyboardState keyboardState = Keyboard.GetState();
            MouseState mouseState = Mouse.GetState();
            float deltaTime = (float)gameTime.ElapsedGameTime.TotalSeconds;

            // Handle input cooldown after window activation
            if (_inputCooldown > 0) {
                _inputCooldown -= deltaTime;
                _prevKeys = keyboardState;
                _prevMouseState = mouseState;
                return; // Skip the rest of the update logic
            }

            // If docked, handle station UI input
            if (_stationDockUI?.IsDocked == true) {
                HandleDockedInput(keyboardState);
                _prevKeys = keyboardState;
                return; // Don't update ship while docked
            }

            // Update nearest station for docking
            _playerShip.UpdateNearestStation(_stationManager.GetStations());

            // Check for docking request (F3)
            if (keyboardState.IsKeyDown(Keys.F3) && _prevKeys.IsKeyUp(Keys.F3)) {
                if (_playerShip.TryDock()) {
                    // Successfully initiated docking
                    _stationDockUI?.DockAtStation(_playerShip.NearestStation);
                    _notificationManager?.ShowMessage($"Docked at {_playerShip.NearestStation?.Name}", 3f);
                }
            }

            // Network connection toggle with F9
            if (keyboardState.IsKeyDown(Keys.F9) && _prevKeys.IsKeyUp(Keys.F9)) {
                if (_networkManager == null) {
                    _gameSettings.EnableOnlineMode = true;
                    InitializeNetworkManager();
                    _notificationManager?.ShowMessage("Online Mode Enabled", 2f);
                } else if (_networkManager.IsConnected) {
                    _ = _networkManager.DisconnectAsync();
                    _notificationManager?.ShowMessage("Disconnected", 2f);
                } else {
                    _ = _networkManager.ConnectAsync(_gameSettings.PlayerName, _gameSettings.ServerUrl);
                    _notificationManager?.ShowMessage("Connecting...", 2f);
                }
            }

            // Network update
            if (_networkManager?.IsConnected == true) {
                _networkUpdateTimer += deltaTime;
                float updateInterval = 1f / _gameSettings.NetworkUpdateRate;

                if (_networkUpdateTimer >= updateInterval) {
                    _ = _networkManager.SendPlayerStateAsync(_playerShip);
                    _networkUpdateTimer = 0f;
                }
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

                // NPC Debug Info
                Console.WriteLine($"--- NPC SHIPS: {_npcShips.Count} total ---");
                int aliveCount = 0;
                int destroyedCount = 0;
                foreach (var npc in _npcShips) {
                    if (npc.IsDestroyed)
                        destroyedCount++;
                    else
                        aliveCount++;
                }
                Console.WriteLine($"    Alive: {aliveCount}, Destroyed: {destroyedCount}");

                // Show first 3 NPC positions
                if (_npcShips.Count > 0) {
                    for (int i = 0; i < Math.Min(3, _npcShips.Count); i++) {
                        var npc = _npcShips[i];
                        float distToPlayer = Vector3.Distance(_playerShip.Position, npc.Position);
                        Console.WriteLine($"    NPC{i + 1} '{npc.Name}': Pos=({npc.Position.X:F0},{npc.Position.Y:F0},{npc.Position.Z:F0}) Speed={npc.Speed:F1} Dist={distToPlayer:F0} IsDestroyed={npc.IsDestroyed} HasModel={npc.Model != null}");
                    }
                }
                Console.WriteLine($"======================================");

                _lastPlayerPosition = _playerShip.Position;
            }

            // NOTE: ESC now handled in Ship.cs - cancels cruise/GOTO instead of exiting
            // To exit the game, close the window or press Alt+F4

            // Update PlanetManager
            _planetManager?.Update(gameTime);

            // Update StationManager (if needed)
            _stationManager?.Update(gameTime);

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
            _playerShip.Update(gameTime, keyboardState, _camera.IsRearViewActive);

            // FIX: Update NPC ships
            foreach (var npc in _npcShips) {
                npc.Update(gameTime, _damageSmokeParticles);
            }

            // Update wrecks
            foreach (var wreck in _wrecks) {
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
            if (keyboardState.IsKeyDown(Keys.B) && _prevKeys.IsKeyUp(Keys.B)) {
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
            if (_weaponSystem.IsCharging()) {
                float chargeProgress = _weaponSystem.GetChargeProgress();
                _chargeParticles.EmitChargeParticles(_playerShip.Position, _playerShip.Orientation, chargeProgress);
            }

            // Update explosion particles
            _explosionParticles.Update((float)gameTime.ElapsedGameTime.TotalSeconds);

            // Update damage smoke
            _damageSmokeParticles.Update((float)gameTime.ElapsedGameTime.TotalSeconds);

            // Update hit impact particles
            _hitImpactParticles.Update((float)gameTime.ElapsedGameTime.TotalSeconds);

            // Update weapon system
            _weaponSystem.Update(gameTime);

            // Set ship references for chain lightning
            List<object> allShips = new List<object>();
            foreach (var npc in _npcShips) {
                if (!npc.IsDestroyed) {
                    allShips.Add(npc);
                }
            }
            _weaponSystem.SetShipReferences(allShips);
            
            // Check projectile collisions with NPC ships
            foreach (var npc in _npcShips) {
                if (!npc.IsDestroyed) {
                    List<HitInfo> hits = _weaponSystem.CheckCollisions(npc.Position, npc.Radius, npc.Hull);

                    // Trigger impact effects for each hit
                    foreach (var hit in hits) {
                        _hitImpactParticles.TriggerImpact(hit.Position, hit.Direction, hit.WeaponColor);
                    }
                }
            }
            
            // Check lightning beam collisions (Wunderwafffle)
            _weaponSystem.CheckLightningCollisions(deltaTime);

            // TODO: Check NPC weapon fire against player (when NPCs can fire)
            // _npcWeaponSystem.CheckCollisions(_playerShip.Position, _playerShip.CollisionRadius, _playerShip.Hull);

            // Update notification manager
            _notificationManager.Update(gameTime);

            // WEAPON SWITCHING with number keys
            if (keyboardState.IsKeyDown(Keys.D1) && _prevKeys.IsKeyUp(Keys.D1)) {
                _weaponSystem.CurrentWeapon = WeaponType.BlueDonut;
                _notificationManager.ShowMessage("Weapon: Blue Donut");
                Console.WriteLine("Switched to Blue Donut");
            } else if (keyboardState.IsKeyDown(Keys.D2) && _prevKeys.IsKeyUp(Keys.D2)) {
                _weaponSystem.CurrentWeapon = WeaponType.Fireball;
                _notificationManager.ShowMessage("Weapon: Fireball");
                Console.WriteLine("Switched to Fireball");
            } else if (keyboardState.IsKeyDown(Keys.D3) && _prevKeys.IsKeyUp(Keys.D3)) {
                _weaponSystem.CurrentWeapon = WeaponType.QuickBlaster;
                _notificationManager.ShowMessage("Weapon: Quick Blaster");
                Console.WriteLine("Switched to Quick Blaster");
            } else if (keyboardState.IsKeyDown(Keys.D4) && _prevKeys.IsKeyUp(Keys.D4)) {
                _weaponSystem.CurrentWeapon = WeaponType.ChargeBeam;
                _notificationManager.ShowMessage("Weapon: Charge Beam");
                Console.WriteLine("Switched to Charge Beam");
            } else if (keyboardState.IsKeyDown(Keys.D5) && _prevKeys.IsKeyUp(Keys.D5)) {
                _weaponSystem.CurrentWeapon = WeaponType.LaserBolt;
                _notificationManager.ShowMessage("Weapon: Laser Bolt");
                Console.WriteLine("Switched to Laser Bolt");
            } else if (keyboardState.IsKeyDown(Keys.D6) && _prevKeys.IsKeyUp(Keys.D6)) {
                _weaponSystem.CurrentWeapon = WeaponType.Wunderwafffle;
                _notificationManager.ShowMessage("Weapon: Wunderwafffle");
                Console.WriteLine("Switched to Wunderwafffle");
            }

            // RIGHT MOUSE BUTTON: Fire weapons or charge beam!
            if (mouseState.RightButton == ButtonState.Pressed) {
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
            } else if (_prevMouseState.RightButton == ButtonState.Pressed) {
                // Mouse button released - stop firing
                _weaponSystem.StopFiring();
            }

            // LEFT MOUSE BUTTON: Click to select objects
            if (mouseState.LeftButton == ButtonState.Pressed && _prevMouseState.LeftButton == ButtonState.Released) {
                // Left mouse button just clicked - try to select an object
                if (!_playerShip.IsFreeFlightMode) {
                    HandleMouseClickSelection(mouseState);
                }
            }

            HandleTargetingInput(keyboardState);

            base.Update(gameTime);
        }

        /// <summary>
        /// Handle mouse click selection of objects in 3D space
        /// </summary>
        private void HandleMouseClickSelection(MouseState mouseState) {
            // Unproject mouse position into 3D world space
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

            // Create ray from near to far point
            Vector3 rayDirection = Vector3.Normalize(farPoint - nearPoint);
            Vector3 rayStart = nearPoint;

            // Test collision with all space objects
            float closestDistance = float.MaxValue;
            int closestObjectIndex = -1;

            for (int i = 0; i < _spaceObjects.Count; i++) {
                SpaceObject obj = _spaceObjects[i];
                
                // Calculate click radius (object size + generous buffer)
                float clickRadius = obj.Radius + 200f;

                // Vector from ray start to object
                Vector3 toObject = obj.Position - rayStart;

                // Project object position onto ray
                float projectionLength = Vector3.Dot(toObject, rayDirection);

                if (projectionLength < 0) continue; // Object behind camera

                // Closest point on ray to object
                Vector3 closestPointOnRay = rayStart + rayDirection * projectionLength;

                // Distance from object to closest point on ray
                float distanceToRay = Vector3.Distance(obj.Position, closestPointOnRay);

                if (distanceToRay <= clickRadius) {
                    // This object was hit! Check if it's closest
                    float distanceToObject = Vector3.Distance(_playerShip.Position, obj.Position);
                    if (distanceToObject < closestDistance) {
                        closestDistance = distanceToObject;
                        closestObjectIndex = i;
                    }
                }
            }

            // Update selection and show feedback
            if (closestObjectIndex >= 0) {
                _selectedSpaceObjectIndex = closestObjectIndex;
                SpaceObject selectedObject = _spaceObjects[closestObjectIndex];
                float distance = Vector3.Distance(_playerShip.Position, selectedObject.Position);
                
                _notificationManager?.ShowMessage($"Target: {selectedObject.Name}");
                Console.WriteLine($"[TARGETING] Left-click selected: {selectedObject.Name} at {distance / 1000f:F2}km");
            }
        }

        private void HandleNpcDestroyed(NpcShip destroyedShip) {
            // Trigger explosion effect
            _explosionParticles.TriggerExplosion(destroyedShip.Position, destroyedShip.Velocity, intensity: 1.0f);

            // Create a wreck where the NPC ship was destroyed
            if (_wreckModel != null) {
                var wreck = new Wreck(destroyedShip.Position, destroyedShip.Orientation, _wreckModel);
                _wrecks.Add(wreck);
                Console.WriteLine($"Wreck created at {wreck.Position}");
            }

            // Remove the destroyed ship from the list of targetable space objects
            _spaceObjects.Remove(destroyedShip);
        }

        /// <summary>
        /// Handle targeting input (T, Shift+T, Ctrl+T for cycling targets, G for GOTO)
        /// </summary>
        private void HandleTargetingInput(KeyboardState keyboardState) {
            bool tPressed = keyboardState.IsKeyDown(Keys.T) && _prevKeys.IsKeyUp(Keys.T);
            bool shiftT = tPressed && (keyboardState.IsKeyDown(Keys.LeftShift) || keyboardState.IsKeyDown(Keys.RightShift));
            bool ctrlT = tPressed && (keyboardState.IsKeyDown(Keys.LeftControl) || keyboardState.IsKeyDown(Keys.RightControl));
            bool gPressed = keyboardState.IsKeyDown(Keys.G) && _prevKeys.IsKeyUp(Keys.G);

            // T key: Cycle to next target
            if (tPressed && !shiftT && !ctrlT && _spaceObjects.Count > 0) {
                _selectedSpaceObjectIndex = (_selectedSpaceObjectIndex + 1) % _spaceObjects.Count;
                var selectedObject = _spaceObjects[_selectedSpaceObjectIndex];
                _notificationManager?.ShowMessage($"Target: {selectedObject.Name}");
                Console.WriteLine($"[TARGETING] Selected: {selectedObject.Name}");
            }

            // G key: GOTO selected target
            if (gPressed && _selectedSpaceObjectIndex >= 0 && _selectedSpaceObjectIndex < _spaceObjects.Count) {
                var target = _spaceObjects[_selectedSpaceObjectIndex];
                _playerShip.ActivateGoto(target);
            }
        }

        /// <summary>
        /// Handle input while docked at a station
        /// </summary>
        private void HandleDockedInput(KeyboardState keyboardState) {
            // Let ship dealer handle its own input first
            if (_stationDockUI?.CurrentArea == StationArea.ShipDealer) {
                bool purchased = _stationDockUI.HandleShipDealerInput(keyboardState, _prevKeys, _playerCredits, _playerShip);
                if (purchased) {
                    // Ship was purchased, notification already shown
                    return;
                }
            }

            // Let commodity dealer handle its own input
            if (_stationDockUI?.CurrentArea == StationArea.Dealer) {
                bool transactionMade = _stationDockUI.HandleCommodityDealerInput(keyboardState, _prevKeys, _playerCredits, _playerShip);
                if (transactionMade) {
                    // Transaction completed
                    return;
                }
            }

            // Navigate between areas
            if (keyboardState.IsKeyDown(Keys.D1) && _prevKeys.IsKeyUp(Keys.D1)) {
                _stationDockUI?.NavigateToArea(StationArea.Hangar);
            } else if (keyboardState.IsKeyDown(Keys.D2) && _prevKeys.IsKeyUp(Keys.D2)) {
                _stationDockUI?.NavigateToArea(StationArea.Bar);
            } else if (keyboardState.IsKeyDown(Keys.D3) && _prevKeys.IsKeyUp(Keys.D3)) {
                _stationDockUI?.NavigateToArea(StationArea.Dealer);
            } else if (keyboardState.IsKeyDown(Keys.D4) && _prevKeys.IsKeyUp(Keys.D4)) {
                _stationDockUI?.NavigateToArea(StationArea.ShipDealer);
            }

            // Undock with U key (only from hangar)
            if (keyboardState.IsKeyDown(Keys.U) && _prevKeys.IsKeyUp(Keys.U)) {
                if (_stationDockUI?.CurrentArea == StationArea.Hangar) {
                    _stationDockUI?.Undock();
                } else {
                    _notificationManager?.ShowMessage("Return to hangar to undock", 2f);
                }
            }
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
                _spriteBatch.DrawString(_font, "TARGET", new Vector2(20, 20), Color.Orange);

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

            if (isOnScreen) {
                // Target is visible - draw reticle on it
                Vector2 targetScreenPos = new Vector2(screenPos3D.X, screenPos3D.Y);
                DrawTargetingReticle(targetScreenPos, distance);
            } else {
                // Target is off-screen - draw directional arrow at screen edge
                DrawOffScreenTargetIndicator(target.Position, distance);
            }
        }

        /// <summary>
        /// Draw an arrow at the edge of the screen pointing toward an off-screen target
        /// </summary>
        private void DrawOffScreenTargetIndicator(Vector3 targetWorldPos, float distance) {
            // Calculate direction from camera to target in screen space
            Vector3 targetDir = targetWorldPos - _camera.Position;
            targetDir.Normalize();

            // Transform direction to camera space
            Matrix viewMatrix = _camera.View;
            Vector3 targetCameraSpace = Vector3.TransformNormal(targetDir, viewMatrix);

            // Project to 2D screen direction (ignore depth)
            Vector2 screenDir = new Vector2(targetCameraSpace.X, -targetCameraSpace.Y);

            // Handle case where target is behind camera
            if (targetCameraSpace.Z > 0) {
                screenDir = -screenDir;
            }

            if (screenDir.LengthSquared() > 0.0001f) {
                screenDir.Normalize();
            } else {
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
            if (_font != null) {
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
        private void DrawDirectionalArrow(Vector2 position, float angle, float size, Color color) {
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
        private void DrawLine(Vector2 start, Vector2 end, int thickness, Color color) {
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
        private void DrawTriangle(Vector2 p1, Vector2 p2, Vector2 p3, Color color) {
            // Simple triangle fill - draw lines from center to edges
            Vector2 center = (p1 + p2 + p3) / 3f;

            // Draw multiple lines to create a filled effect
            int steps = 8;
            for (int i = 0; i <= steps; i++) {
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
            Rectangle panel = new Rectangle(10, 10, 280, 310);
            _spriteBatch.Draw(_pixel, panel, Color.Black * 0.7f);
            _spriteBatch.Draw(_pixel, new Rectangle(10, 10, 280, 2), Color.Cyan);
            _spriteBatch.Draw(_pixel, new Rectangle(10, 10, 2, 310), Color.Cyan);
            _spriteBatch.Draw(_pixel, new Rectangle(288, 10, 2, 310), Color.Cyan);
            _spriteBatch.Draw(_pixel, new Rectangle(10, 318, 280, 2), Color.Cyan);

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

                // Energy display
                float energyPercent = _playerShip.Energy.EnergyPercentage;
                Color energyColor = _playerShip.Energy.GetEnergyColor();
                _spriteBatch.DrawString(_font, $"Energy: {energyPercent * 100:F0}%", new Vector2(20, 90), energyColor);

                // Energy bar
                Rectangle energyBarBg = new Rectangle(20, 110, 250, 15);
                Rectangle energyBarFill = new Rectangle(20, 110, (int)(250 * energyPercent), 15);
                _spriteBatch.Draw(_pixel, energyBarBg, Color.DarkGray * 0.5f);
                _spriteBatch.Draw(_pixel, energyBarFill, energyColor);

                // Other status info
                _spriteBatch.DrawString(_font, $"Speed: {Math.Abs(_playerShip.Speed):F1}", new Vector2(20, 135), Color.White);
                _spriteBatch.DrawString(_font, $"Throttle: {_playerShip.GetThrottle() * 100:F0}%", new Vector2(20, 155), Color.White);
                _spriteBatch.DrawString(_font, $"Mode: {_playerShip.GetFlightStatus()}", new Vector2(20, 175), Color.Yellow);

                // Weapon display
                _spriteBatch.DrawString(_font, $"Weapon: {_weaponSystem.CurrentWeapon}", new Vector2(20, 195), Color.Orange);

                // Charge beam indicator
                if (_weaponSystem.IsCharging()) {
                    float chargeProgress = _weaponSystem.GetChargeProgress();
                    _spriteBatch.DrawString(_font, $"CHARGING: {chargeProgress * 100:F0}%", new Vector2(20, 215), Color.Yellow);

                    // Charge bar
                    Rectangle chargeBarBg = new Rectangle(20, 235, 250, 15);
                    Rectangle chargeBarFill = new Rectangle(20, 235, (int)(250 * chargeProgress), 15);
                    _spriteBatch.Draw(_pixel, chargeBarBg, Color.DarkGray * 0.5f);
                    _spriteBatch.Draw(_pixel, chargeBarFill, Color.Lerp(Color.Yellow, Color.Red, chargeProgress));
                }

                if (_playerShip.IsNewtonianMode)
                    _spriteBatch.DrawString(_font, "NEWTONIAN", new Vector2(20, 255), Color.Lime);
                if (_camera.IsTurretViewActive)
                    _spriteBatch.DrawString(_font, "TURRET VIEW", new Vector2(20, 275), Color.Orange);
                if (_playerShip.IsGotoActive && _playerShip.CurrentGotoTarget != null)
                    _spriteBatch.DrawString(_font, $"GOTO: {_playerShip.CurrentGotoTarget.Name}", new Vector2(20, 295), Color.Orange);
            } else {
                // Fallback visual indicators when no font available
                // Hull bar (horizontal)
                float hullPercent = _playerShip.Hull.HullPercentage;
                Color hullColor = _playerShip.Hull.GetHullColor();
                int hullBarWidth = (int)(250 * hullPercent);
                _spriteBatch.Draw(_pixel, new Rectangle(20, 30, 250, 20), Color.DarkGray * 0.5f);
                _spriteBatch.Draw(_pixel, new Rectangle(20, 30, hullBarWidth, 20), hullColor);

                // Energy bar (horizontal)
                float energyPercent = _playerShip.Energy.EnergyPercentage;
                Color energyColor = _playerShip.Energy.GetEnergyColor();
                int energyBarWidth = (int)(250 * energyPercent);
                _spriteBatch.Draw(_pixel, new Rectangle(20, 60, 250, 20), Color.DarkGray * 0.5f);
                _spriteBatch.Draw(_pixel, new Rectangle(20, 60, energyBarWidth, 20), energyColor);

                // Speed bar (horizontal)
                int speedBarWidth = (int)(250 * MathHelper.Clamp(Math.Abs(_playerShip.Speed) / _playerShip.MaxSpeed, 0f, 1f));
                _spriteBatch.Draw(_pixel, new Rectangle(20, 90, 250, 20), Color.DarkGray * 0.5f);
                _spriteBatch.Draw(_pixel, new Rectangle(20, 90, speedBarWidth, 20), Color.Cyan);

                // Throttle bar (horizontal)
                float throttle = _playerShip.GetThrottle();
                int throttleBarWidth = (int)(250 * Math.Abs(throttle));
                Color throttleColor = throttle >= 0 ? Color.Green : Color.Orange;
                _spriteBatch.Draw(_pixel, new Rectangle(20, 120, 250, 20), Color.DarkGray * 0.5f);
                _spriteBatch.Draw(_pixel, new Rectangle(20, 120, throttleBarWidth, 20), throttleColor);

                // Weapon/charge indicator
                if (_weaponSystem.IsCharging()) {
                    float chargeProgress = _weaponSystem.GetChargeProgress();
                    Rectangle chargeBar = new Rectangle(20, 150, (int)(250 * chargeProgress), 20);
                    _spriteBatch.Draw(_pixel, chargeBar, Color.Lerp(Color.Yellow, Color.Red, chargeProgress));
                }

                // Status indicators
                int yPos = 180;
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

            // Draw NPC ship hull bars and targeting crosshairs
            foreach (var npc in _npcShips) {
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
                if (_font != null && distance < 1000f) {
                    Vector2 nameSize = _font.MeasureString(npc.Name);
                    _spriteBatch.DrawString(_font, npc.Name,
                        new Vector2(screenPos.X - nameSize.X / 2, screenPos.Y + barHeight + 2),
                        Color.White * 0.8f);
                }

                // ✨ NEW: Draw targeting lead crosshair
                DrawLeadingCrosshair(npc);
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
                Vector2 pos = new Vector2(GraphicsDevice.Viewport.Width / 2 -  120, 30);
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

        /// <summary>
        /// Draw a leading crosshair showing where to shoot to hit a moving target
        /// </summary>
        private void DrawLeadingCrosshair(NpcShip target) {
            // Calculate where the target will be when our bullet arrives
            float distance = Vector3.Distance(_playerShip.Position, target.Position);

            // Get weapon speed (use current weapon's speed)
            float projectileSpeed = 1200f; // Default speed
            var weaponStats = _weaponSystem.GetCurrentWeaponStats();
            if (weaponStats != null) {
                projectileSpeed = weaponStats.Speed;
            }

            // Don't draw lead indicator for beams or instant-hit weapons
            if (projectileSpeed <= 0f) {
                return;
            }

            // Time for bullet to reach target
            float timeToHit = distance / projectileSpeed;

            // Predict target position
            Vector3 predictedPosition = target.Position + target.Velocity * timeToHit;

            // Project predicted position to screen
            Vector3 leadScreenPos3D = GraphicsDevice.Viewport.Project(
                predictedPosition,
                _camera.Projection,
                _camera.View,
                Matrix.Identity
            );

            if (leadScreenPos3D.Z < 0 || leadScreenPos3D.Z > 1) return; // Behind camera

            Vector2 leadScreenPos = new Vector2(leadScreenPos3D.X, leadScreenPos3D.Y);

            // Check if on screen
            if (leadScreenPos.X < 0 || leadScreenPos.X > GraphicsDevice.Viewport.Width ||
                leadScreenPos.Y < 0 || leadScreenPos.Y > GraphicsDevice.Viewport.Height)
                return;

            // Draw leading crosshair
            Color leadColor = Color.Red * 0.8f; // Red crosshair for aiming
            int size = 15;
            int thickness = 2;
            int gap = 5;

            // Horizontal lines
            _spriteBatch.Draw(_pixel,
                new Rectangle((int)leadScreenPos.X - size, (int)leadScreenPos.Y - thickness / 2, size - gap, thickness),
                leadColor);
            _spriteBatch.Draw(_pixel,
                new Rectangle((int)leadScreenPos.X + gap, (int)leadScreenPos.Y - thickness / 2, size - gap, thickness),
                leadColor);

            // Vertical lines
            _spriteBatch.Draw(_pixel,
                new Rectangle((int)leadScreenPos.X - thickness / 2, (int)leadScreenPos.Y - size, thickness, size - 10),
                leadColor);
            _spriteBatch.Draw(_pixel,
                new Rectangle((int)leadScreenPos.X - thickness / 2, (int)leadScreenPos.Y + 10, thickness, size - 10),
                leadColor);

            // Center dot
            _spriteBatch.Draw(_pixel,
                new Rectangle((int)leadScreenPos.X - 2, (int)leadScreenPos.Y - 2, 4, 4),
                leadColor);

            // Optional: Draw a line from current position to lead position
            float lineDistance = Vector2.Distance(new Vector2(leadScreenPos3D.X, leadScreenPos3D.Y),
                new Vector2(GraphicsDevice.Viewport.Project(target.Position, _camera.Projection, _camera.View, Matrix.Identity).X,
                           GraphicsDevice.Viewport.Project(target.Position, _camera.Projection, _camera.View, Matrix.Identity).Y));

            if (lineDistance > 5f) // Only draw line if lead is far enough from actual position
            {
                Vector3 actualScreenPos3D = GraphicsDevice.Viewport.Project(target.Position, _camera.Projection, _camera.View, Matrix.Identity);
                Vector2 actualScreenPos = new Vector2(actualScreenPos3D.X, actualScreenPos3D.Y);
                DrawLine(actualScreenPos, leadScreenPos, 1, leadColor * 0.4f);
            }
        }

        /// <summary>
        /// Handle undocking from station
        /// </summary>
        private void HandleUndock() {
            Console.WriteLine("[DOCK] Player undocked");
            _notificationManager?.ShowMessage("Undocked - Systems online", 2f);
            _playerShip.Reset();
            IsMouseVisible = !_playerShip.IsFreeFlightMode;
        }

        /// <summary>
        /// Handle ship purchase event
        /// </summary>
        private void HandleShipPurchased(ShipDefinition newShip) {
            Console.WriteLine($"[SHIP PURCHASE] Switched to {newShip.Name}");
            _notificationManager?.ShowMessage($"Ship purchased: {newShip.Name}!", 3f);

            // Re-register hull event handlers (they get reset when SetHull is called)
            _playerShip.Hull.OnDestroyed += () => {
                Console.WriteLine("💀 PLAYER SHIP DESTROYED!");
                _notificationManager?.ShowMessage("SHIP DESTROYED", 5f);
                _explosionParticles?.TriggerExplosion(_playerShip.Position, _playerShip.Velocity, intensity: 1.5f);
            };

            // Re-connect notification manager and explosion systems
            _playerShip.SetNotificationManager(_notificationManager);
            _playerShip.SetExplosionSystem(_explosionParticles);
            _playerShip.SetDamageSmokeSystem(_damageSmokeParticles);

            Console.WriteLine($"[SHIP PURCHASE] New ship fully configured with hull: {_playerShip.Hull.MaxHull}, energy: {_playerShip.Energy.MaxEnergy}");
        }

        /// <summary>
        /// Handle window activation
        /// </summary>
        private void OnWindowActivated(object sender, EventArgs e) {
            _isWindowFocused = true;
            _prevKeys = Keyboard.GetState();
            _prevMouseState = Mouse.GetState();
            _inputCooldown = InputCooldownTime;
            Console.WriteLine("[WINDOW] ACTIVATED - Input enabled, cooldown started.");
        }

        /// <summary>
        /// Handle window deactivation
        /// </summary>
        private void OnWindowDeactivated(object sender, EventArgs e) {
            _isWindowFocused = false;
            _playerShip.Reset();
            Console.WriteLine("[WINDOW] DEACTIVATED - Input disabled, ship state reset.");
        }

        /// <summary>
        /// Handle window focus changes
        /// </summary>
        private void OnWindowFocusChanged(object sender, EventArgs e) {
            _isWindowFocused = IsActive;
        }

        protected override void EndDraw() {
            _prevKeys = Keyboard.GetState();
            _prevMouseState = Mouse.GetState();
            base.EndDraw();
        }

        protected override void Draw(GameTime gameTime) {
            GraphicsDevice.Clear(ClearOptions.Target | ClearOptions.DepthBuffer, Color.Black, 1.0f, 0);

            // === 3D RENDERING PHASE ===
            GraphicsDevice.DepthStencilState = DepthStencilState.Default;
            GraphicsDevice.BlendState = BlendState.Opaque;
            GraphicsDevice.SamplerStates[0] = SamplerState.LinearWrap;
            GraphicsDevice.RasterizerState = RasterizerState.CullCounterClockwise;

            // Draw starfield
            _starfield.Draw(_camera.View, _camera.Projection, _camera.Position, _playerShip.Velocity);

            // Reset render states
            GraphicsDevice.DepthStencilState = DepthStencilState.Default;
            GraphicsDevice.BlendState = BlendState.Opaque;
            GraphicsDevice.RasterizerState = RasterizerState.CullCounterClockwise;

            // Draw Sun
            if (_sun != null && _camera != null) {
                Vector3 lightDir = _sun.GetLightDirection(_camera.Position);
                _sun.Draw(_camera.View, _camera.Projection);

                if (gameTime.TotalGameTime.TotalSeconds % 5 < 0.016) {
                    float distFromCamera = Vector3.Distance(_sun.Position, _camera.Position);
                    Console.WriteLine($"[DEBUG SUN] Drawing sun at {_sun.Position}");
                    Console.WriteLine($"[DEBUG SUN] Distance from camera: {distFromCamera:F2} units");
                }
            }

            // Reset render states
            GraphicsDevice.DepthStencilState = DepthStencilState.Default;
            GraphicsDevice.BlendState = BlendState.Opaque;
            GraphicsDevice.RasterizerState = RasterizerState.CullCounterClockwise;

            // Draw Planets
            if (_planetManager != null && _camera != null && _sun != null) {
                Vector3 lightDir = _sun.GetLightDirection(Vector3.Zero);
                _planetManager.Draw(_camera, lightDir);
            }

            // Reset render states
            GraphicsDevice.DepthStencilState = DepthStencilState.Default;
            GraphicsDevice.BlendState = BlendState.Opaque;
            GraphicsDevice.RasterizerState = RasterizerState.CullCounterClockwise;

            // Draw Stations
            if (_stationManager != null && _camera != null && _sun != null) {
                Vector3 lightDir = _sun.GetLightDirection(Vector3.Zero);
                _stationManager.Draw(_camera, lightDir);
            }

            // Reset render states
            GraphicsDevice.DepthStencilState = DepthStencilState.Default;
            GraphicsDevice.BlendState = BlendState.Opaque;
            GraphicsDevice.RasterizerState = RasterizerState.CullCounterClockwise;

            // Draw ship
            _playerShip.Draw(_camera.View, _camera.Projection, _lightDirection);

            // Draw space objects
            foreach (var spaceObject in _spaceObjects) {
                if (spaceObject.Model != null) {
                    Matrix world = Matrix.CreateScale(1.0f) * Matrix.CreateTranslation(spaceObject.Position);
                    foreach (var mesh in spaceObject.Model.Meshes) {
                        foreach (BasicEffect effect in mesh.Effects) {
                            effect.World = world;
                            effect.View = _camera.View;
                            effect.Projection = _camera.Projection;
                            effect.EnableDefaultLighting();
                            effect.DirectionalLight0.Direction = _lightDirection;
                        }
                        mesh.Draw();
                    }
                }
            }

            // Reset render states
            GraphicsDevice.DepthStencilState = DepthStencilState.Default;
            GraphicsDevice.BlendState = BlendState.Opaque;
            GraphicsDevice.RasterizerState = RasterizerState.CullCounterClockwise;

            // Draw NPC ships
            int drawnNpcCount = 0;
            int skippedNpcCount = 0;
            foreach (var npc in _npcShips) {
                if (npc.IsDestroyed) {
                    skippedNpcCount++;
                    continue;
                }
                if (npc.Model == null) {
                    Console.WriteLine($"[DRAW] NPC {npc.Name} has no model!");
                    skippedNpcCount++;
                    continue;
                }

                npc.Draw(_camera.View, _camera.Projection, _lightDirection);
                drawnNpcCount++;
            }

            if (gameTime.TotalGameTime.TotalSeconds % 2 < 0.016f) {
                Console.WriteLine($"[DRAW] NPC Stats - Total: {_npcShips.Count}, Drawn: {drawnNpcCount}, Skipped: {skippedNpcCount}");
            }

            // Draw wrecks
            foreach (var wreck in _wrecks) {
                wreck.Draw(_camera.View, _camera.Projection, _lightDirection);
            }

            // Draw reference markers
            var oldDepth = GraphicsDevice.DepthStencilState;
            GraphicsDevice.DepthStencilState = DepthStencilState.DepthRead;

            foreach (var marker in _markers) {
                marker.Draw(_camera.View, _camera.Projection);
            }

            // Draw motion trail
            _motionTrail.Draw(_camera.View, _camera.Projection);

            GraphicsDevice.DepthStencilState = oldDepth;

            // Draw engine glows
            float glowIntensity = Math.Max(1.0f, _playerShip.GetThrottle());

            if (_playerShip.IsCruiseCharging) {
                float chargeProgress = _playerShip.CruiseChargeProgress;
                glowIntensity = MathHelper.Lerp(0.5f, 2.0f, chargeProgress);
                float pulse = (float)Math.Sin(gameTime.TotalGameTime.TotalSeconds * 8.0) * 0.2f + 0.8f;
                glowIntensity *= pulse;
            } else if (_playerShip.IsAfterburnerActive) {
                glowIntensity = 1.2f;
            } else if (_playerShip.IsCruiseActive) {
                glowIntensity = 1.0f;
            }

            _engineGlow.DrawEngineGlows(GraphicsDevice, _playerShip, _camera, glowIntensity);

            // Draw cruise sparks
            _cruiseSparks.Draw(_camera);

            // Draw charge particles
            _chargeParticles.Draw(_camera);

            // Draw damage smoke
            _damageSmokeParticles.Draw(_camera);

            // Draw explosion particles
            _explosionParticles.Draw(_camera);

            // Draw hit impact particles
            _hitImpactParticles.Draw(_camera);

            // Draw weapon projectiles
            _weaponSystem.Draw(_camera);

            // Draw engine trail
            _engineTrail.Draw(_camera);

            // === 2D UI RENDERING PHASE ===
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

            // If docked, draw station UI instead of regular HUD
            if (_stationDockUI?.IsDocked == true) {
                _stationDockUI.Draw(_spriteBatch, GraphicsDevice, _playerCredits, _playerShip);
            } else {
                // Regular HUD when not docked
                DrawStatusPanel();
                DrawCurrentTargetDisplay();
                DrawSpaceObjectBrackets();
                DrawGotoStatus();
                DrawSunDistanceIndicator();
                DrawCrosshair();
                DrawCoordinates();

                // Draw notifications
                _notificationManager.Draw(_spriteBatch);
            }

            _spriteBatch.End();
        }
    }
}