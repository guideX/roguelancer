using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System;
using System.Collections.Generic;

namespace Roguelancer
{
    public class Game1 : Game
    {
        private GraphicsDeviceManager _graphics;
        private SpriteBatch _spriteBatch;
        
        // Game components
        private Ship _playerShip;
        private Camera _camera;
        private Starfield _starfield;
        private EngineGlow _engineGlow;
        private EngineTrail _engineTrail; // NEW
        private Sun _sun;
        private SpriteFont _font;
        
        // Lighting
        private Vector3 _lightDirection = Vector3.Normalize(new Vector3(1, -1, 1));
        
        // Targeting / space objects
        private List<SpaceObject> _spaceObjects = new();
        private int _selectedSpaceObjectIndex = -1;
        
        // NPC ships
        private List<NpcShip> _npcShips = new();
        
        // Reference markers to show movement
        private List<Marker3D> _markers = new();
        
        // Motion trail showing where we've been
        private MotionTrail _motionTrail;
        
        // Simple 1x1 pixel for HUD
        private Texture2D _pixel;

        public Game1()
        {
            _graphics = new GraphicsDeviceManager(this);
            Content.RootDirectory = "Content";
            IsMouseVisible = false;
            _graphics.PreferredBackBufferWidth = 1920;
            _graphics.PreferredBackBufferHeight = 1080;
            _graphics.IsFullScreen = false;
            _graphics.ApplyChanges();
        }

        protected override void Initialize()
        {
            // Initialize camera
            float aspectRatio = GraphicsDevice.Viewport.AspectRatio;
            _camera = new Camera(aspectRatio);
            
            // Initialize player ship at origin
            _playerShip = new Ship(Vector3.Zero);
            
            // Initialize sun at a distant location
            _sun = new Sun(new Vector3(5000, 2000, 3000), scale: 150f)
            {
                EmissiveColor = new Color(255, 220, 180), // Warm yellow-orange
                EmissiveIntensity = 0.1f, // Reduced from 0.5f to 0.1f to prevent white blowout
                RotationSpeed = 0.05f
            };
            
            // Populate space objects (sample points)
            _spaceObjects.Add(new SpaceObject("Nav Buoy Alpha", new Vector3(3000, 200, -1500), 150f));
            _spaceObjects.Add(new SpaceObject("Mining Station", new Vector3(-4500, -800, 2200), 400f));
            _spaceObjects.Add(new SpaceObject("Jump Gate", new Vector3(8000, 1200, 5000), 600f));
            _spaceObjects.Add(new SpaceObject("Wreck", new Vector3(-2000, 500, -6000), 100f));
            
            // Spawn NPC ships in patrol patterns
            Random rand = new Random();
            for (int i = 0; i < 5; i++)
            {
                // Create patrol patterns at various distances
                Vector3 patrolCenter = new Vector3(
                    (float)(rand.NextDouble() * 6000 - 3000),
                    (float)(rand.NextDouble() * 2000 - 1000),
                    (float)(rand.NextDouble() * 6000 - 3000)
                );
                
                float patrolRadius = 500f + (float)(rand.NextDouble() * 1000f);
                float patrolSpeed = 0.1f + (float)(rand.NextDouble() * 0.2f);
                
                // Start position on the patrol circle
                float startAngle = (float)(rand.NextDouble() * MathHelper.TwoPi);
                Vector3 startPos = patrolCenter + new Vector3(
                    (float)Math.Sin(startAngle) * patrolRadius,
                    0f,
                    (float)Math.Cos(startAngle) * patrolRadius
                );
                
                NpcShip npc = new NpcShip($"Patrol Ship {i + 1}", startPos, patrolCenter, patrolRadius, patrolSpeed);
                _npcShips.Add(npc);
            }
            
            // Create reference grid markers
            int gridSize = 5000;
            int gridSpacing = 1000;
            Color markerColor = new Color(0, 255, 255, 128); // Cyan, semi-transparent
            
            for (int x = -gridSize; x <= gridSize; x += gridSpacing)
            {
                for (int z = -gridSize; z <= gridSize; z += gridSpacing)
                {
                    _markers.Add(new Marker3D(GraphicsDevice, new Vector3(x, 0, z), markerColor, 200f));
                }
            }
            
            Console.WriteLine($"Created {_markers.Count} reference markers in grid");
            
            base.Initialize();
        }

        protected override void LoadContent()
        {
            _spriteBatch = new SpriteBatch(GraphicsDevice);
            
            // Load ship model
            try
            {
                _playerShip.Model = Content.Load<Model>("SHIPS/scimitar/Scimitar2");
                Console.WriteLine("Scimitar model loaded successfully!");
                
                // Load DIFFERENT models for NPC ships for variety!
                string[] npcShipModels = new[]
                {
                    "SHIPS/PI_TRANSPORT/PI_TRANSPORT",  // Large transport ship
                    "SHIPS/scimitar/Scimitar2",         // Scimitar fighter
                    "SHIPS/PI_TRANSPORT/PI_TRANSPORT",  // Large transport ship
                    "SHIPS/scimitar/Scimitar2",         // Scimitar fighter
                    "SHIPS/PI_TRANSPORT/PI_TRANSPORT"   // Large transport ship
                };
                
                for (int i = 0; i < _npcShips.Count; i++)
                {
                    try
                    {
                        // Cycle through different ship models
                        string modelPath = npcShipModels[i % npcShipModels.Length];
                        _npcShips[i].Model = Content.Load<Model>(modelPath);
                        Console.WriteLine($"Loaded {modelPath} for NPC {i + 1}");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error loading NPC ship {i}: {ex.Message}");
                        // Fallback to scimitar
                        _npcShips[i].Model = _playerShip.Model;
                    }
                }
                Console.WriteLine($"Loaded models for {_npcShips.Count} NPC ships!");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading model: {ex.Message}");
                Console.WriteLine("Make sure the FBX file is added to the Content Pipeline.");
            }
            
            // Load sun model
            try
            {
                _sun.Model = Content.Load<Model>("SUN/unstablestar/UnstableStar");
                Console.WriteLine("Sun model loaded successfully!");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading sun model: {ex.Message}");
            }
            
            // Create starfield
            _starfield = new Starfield(GraphicsDevice, 3000);
            
            // Create engine glow effect
            _engineGlow = new EngineGlow(GraphicsDevice);
            
            // Initialize engine trail
            _engineTrail = new EngineTrail(GraphicsDevice);
            
            // Initialize motion trail
            _motionTrail = new MotionTrail(GraphicsDevice);
            
            _pixel = new Texture2D(GraphicsDevice, 1, 1);
            _pixel.SetData(new[] { Color.White });
            
            // Load font for HUD text
            try 
            { 
                _font = Content.Load<SpriteFont>("Fonts/HudFont"); 
                Console.WriteLine("HUD Font loaded successfully!");
            } 
            catch (Exception ex)
            { 
                _font = null; 
                Console.WriteLine($"Font not loaded: {ex.Message}");
            }
        }

        protected override void Update(GameTime gameTime)
        {
            KeyboardState keyboardState = Keyboard.GetState();
            MouseState mouseState = Mouse.GetState();
            float deltaTime = (float)gameTime.ElapsedGameTime.TotalSeconds;
            
            // Initialize last position on first frame
            if (_firstFrame)
            {
                _lastPlayerPosition = _playerShip.Position;
                _firstFrame = false;
            }
            
            // Debug output every second with distance traveled
            _debugTimer += deltaTime;
            if (_debugTimer >= 1f)
            {
                _debugTimer = 0f;
                float distanceTraveled = Vector3.Distance(_playerShip.Position, _lastPlayerPosition);
                Console.WriteLine($"=========== MOVEMENT DEBUG ===========");
                Console.WriteLine($"Ship Position: X={_playerShip.Position.X:F1} Y={_playerShip.Position.Y:F1} Z={_playerShip.Position.Z:F1}");
                Console.WriteLine($"Ship Velocity: X={_playerShip.Velocity.X:F1} Y={_playerShip.Velocity.Y:F1} Z={_playerShip.Velocity.Z:F1}");
                Console.WriteLine($"Speed: {_playerShip.Speed:F1} | Throttle: {_playerShip.GetThrottle()*100:F0}%");
                Console.WriteLine($"Distance Traveled/sec: {distanceTraveled:F1} units");
                Console.WriteLine($"Camera Position: X={_camera.Position.X:F1} Y={_camera.Position.Y:F1} Z={_camera.Position.Z:F1}");
                
                // GOTO Debug Info
                if (_playerShip.IsGotoActive && _playerShip.CurrentGotoTarget != null)
                {
                    float gotoDistance = Vector3.Distance(_playerShip.Position, _playerShip.CurrentGotoTarget.Position);
                    Console.WriteLine($">>> GOTO ACTIVE: {_playerShip.CurrentGotoTarget.Name}");
                    Console.WriteLine($">>> Distance to Target: {gotoDistance:F1} units ({gotoDistance/1000f:F2} km)");
                    Console.WriteLine($">>> Cruise: {(_playerShip.IsCruiseActive ? "ACTIVE" : "INACTIVE")}");
                }
                
                // Show NPC positions too
                if (_npcShips.Count > 0)
                {
                    Console.WriteLine($"NPC1 Position: X={_npcShips[0].Position.X:F1} Y={_npcShips[0].Position.Y:F1} Z={_npcShips[0].Position.Z:F1}");
                    Console.WriteLine($"NPC1 Speed: {_npcShips[0].Speed:F1}");
                }
                Console.WriteLine($"======================================");
                
                _lastPlayerPosition = _playerShip.Position;
            }
            
            // Exit on Escape
            if (keyboardState.IsKeyDown(Keys.Escape))
                Exit();
            
            // Toggle turret view with H
            if (keyboardState.IsKeyDown(Keys.H) && _prevKeys.IsKeyUp(Keys.H))
            {
                _camera.ToggleTurretView();
            }
            
            // Turret view controls (mouse or arrow keys)
            if (_camera.IsTurretViewActive)
            {
                float turretSensitivity = 2f * deltaTime;
                float yawDelta = 0f;
                float pitchDelta = 0f;
                
                // Mouse input (if not in mouse flight mode)
                if (!_playerShip.MouseFlightEnabled && mouseState.LeftButton == ButtonState.Pressed)
                {
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
            
            // Update sun
            _sun.Update(gameTime);
            
            // Update lighting direction based on sun position
            _lightDirection = _sun.GetLightDirection(_playerShip.Position);
            
            // Update mouse visibility based on flight mode
            IsMouseVisible = _playerShip.MouseFlightEnabled;
            
            // Trigger camera shake when afterburner is activated
            if (_playerShip.AfterburnerJustActivated)
            {
                // Strong initial shake that decays quickly
                _camera.AddShake(2.0f, 4f);
            }
            
            // Continuous mild shake while afterburner is active
            if (_playerShip.IsAfterburnerActive)
            {
                _camera.AddShake(0.3f, 8f);
            }
            
            // Update camera shake
            _camera.UpdateShake(deltaTime);
            
            // Update camera to follow ship
            _camera.Follow(_playerShip.Position, _playerShip.Forward, _playerShip.Up, 0.05f); // Reduced from 0.15f for more lag

            // Update engine trail with current throttle
            _engineTrail.Update(gameTime, _playerShip, _playerShip.GetThrottle());
            
            // Update motion trail to show where we've been
            _motionTrail.Update(deltaTime, _playerShip.Position, _playerShip.Speed);
            
            // Update NPC ships
            foreach (var npc in _npcShips)
            {
                npc.Update(gameTime);
            }
            
            HandleTargetingInput(keyboardState);
            
            base.Update(gameTime);
        }

        private void HandleTargetingInput(KeyboardState kb)
        {
            // Cycle selection with T (next) / Shift+T (previous)
            if (Pressed(kb, Keys.T) && !kb.IsKeyDown(Keys.LeftShift) && !kb.IsKeyDown(Keys.RightShift))
            {
                if (_spaceObjects.Count > 0)
                {
                    _selectedSpaceObjectIndex = (_selectedSpaceObjectIndex + 1) % _spaceObjects.Count;
                }
            }
            else if (Pressed(kb, Keys.T) && (kb.IsKeyDown(Keys.LeftShift) || kb.IsKeyDown(Keys.RightShift)))
            {
                if (_spaceObjects.Count > 0)
                {
                    _selectedSpaceObjectIndex = (_selectedSpaceObjectIndex - 1 + _spaceObjects.Count) % _spaceObjects.Count;
                }
            }
            // Clear target Ctrl+T
            if (Pressed(kb, Keys.T) && (kb.IsKeyDown(Keys.LeftControl) || kb.IsKeyDown(Keys.RightControl)))
            {
                _selectedSpaceObjectIndex = -1;
            }
            // Activate GOTO with G (stub mapping) or Enter if object selected
            if (_selectedSpaceObjectIndex >= 0 && Pressed(kb, Keys.G))
            {
                _playerShip.ActivateGoto(_spaceObjects[_selectedSpaceObjectIndex]);
            }
            // Cancel GOTO with Ctrl+G
            if ((kb.IsKeyDown(Keys.LeftControl) || kb.IsKeyDown(Keys.RightControl)) && Pressed(kb, Keys.G))
            {
                _playerShip.CancelGoto();
            }
        }
        
        private bool Pressed(KeyboardState kb, Keys key)
        {
            return kb.IsKeyDown(key) && !_playerShip.MouseFlightEnabled && !_prevKeys.IsKeyDown(key);
        }
        private KeyboardState _prevKeys;
        private float _debugTimer = 0f;
        private Vector3 _lastPlayerPosition;
        private bool _firstFrame = true;
        
        protected override void EndDraw()
        {
            _prevKeys = Keyboard.GetState();
            base.EndDraw();
        }

        protected override void Draw(GameTime gameTime)
        {
            // Clear to space black
            GraphicsDevice.Clear(Color.Black);
            
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
            
            // Draw ship
            _playerShip.Draw(_camera.View, _camera.Projection, _lightDirection);
            
            // Draw NPC ships
            foreach (var npc in _npcShips)
            {
                npc.Draw(_camera.View, _camera.Projection, _lightDirection);
            }
            
            // Draw reference markers (helps visualize movement) - FIXED in world space
            // Temporarily disable depth test so markers always visible
            var oldDepth = GraphicsDevice.DepthStencilState;
            GraphicsDevice.DepthStencilState = DepthStencilState.DepthRead;
            
            foreach (var marker in _markers)
            {
                marker.Draw(_camera.View, _camera.Projection);
            }
            
            // Draw motion trail showing where we've been (bright yellow line)
            _motionTrail.Draw(_camera.View, _camera.Projection);
            
            GraphicsDevice.DepthStencilState = oldDepth;
            
            // Draw engine glows (brighter during afterburner)
            float glowIntensity = Math.Max(0.2f, _playerShip.GetThrottle()); // Minimum idle glow
            if (_playerShip.IsAfterburnerActive)
                glowIntensity = 1.8f; // Extra bright for afterburner
            else if (_playerShip.IsCruiseActive)
                glowIntensity = 1.2f; // Bright for cruise

            _engineGlow.DrawEngineGlows(_playerShip, _camera, glowIntensity);
            
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
        
        private void DrawHUD()
        {
            _spriteBatch.Begin();
            
            DrawStatusPanel();
            DrawCurrentTargetDisplay(); // NEW: Show current target prominently
            DrawSpaceObjectBrackets();
            DrawGotoStatus();
            DrawSunDistanceIndicator(); // NEW: Show distance to sun
            
            _spriteBatch.End();
        }
        
        private void DrawCurrentTargetDisplay()
        {
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
            
            if (_font != null)
            {
                // Target label
                _spriteBatch.DrawString(_font, "TARGET", new Vector2(panel.X + 10, panel.Y + 10), Color.Orange);
                
                // Target name (large)
                Vector2 nameSize = _font.MeasureString(target.Name);
                _spriteBatch.DrawString(_font, target.Name, new Vector2(screenCenterX - nameSize.X / 2, panel.Y + 30), Color.White);
                
                // Distance
                string distText = $"{distance / 1000f:F2} km";
                Vector2 distSize = _font.MeasureString(distText);
                _spriteBatch.DrawString(_font, distText, new Vector2(screenCenterX - distSize.X / 2, panel.Y + 55), Color.Cyan);
            }
            else
            {
                // Fallback: Just draw colored bars to indicate target is selected
                _spriteBatch.Draw(_pixel, new Rectangle(panel.X + 10, panel.Y + 30, (int)(380 * MathHelper.Clamp(distance / 10000f, 0f, 1f)), 30), Color.Orange * 0.8f);
            }
            
            // Draw targeting reticle on the actual target
            Vector3 screenPos3D = GraphicsDevice.Viewport.Project(target.Position, _camera.Projection, _camera.View, Matrix.Identity);
            if (screenPos3D.Z >= 0 && screenPos3D.Z <= 1) // Only if in front of camera
            {
                Vector2 targetScreenPos = new Vector2(screenPos3D.X, screenPos3D.Y);
                DrawTargetingReticle(targetScreenPos, distance);
            }
        }
        
        private void DrawTargetingReticle(Vector2 center, float distance)
        {
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

        private void DrawStatusPanel()
        {
            // Draw status box background
            Rectangle panel = new Rectangle(10, 10, 280, 200);
            _spriteBatch.Draw(_pixel, panel, Color.Black * 0.7f);
            _spriteBatch.Draw(_pixel, new Rectangle(10, 10, 280, 2), Color.Cyan);
            _spriteBatch.Draw(_pixel, new Rectangle(10, 10, 2, 200), Color.Cyan);
            _spriteBatch.Draw(_pixel, new Rectangle(288, 10, 2, 200), Color.Cyan);
            _spriteBatch.Draw(_pixel, new Rectangle(10, 208, 280, 2), Color.Cyan);
            
            if (_font != null)
            {
                _spriteBatch.DrawString(_font, "STATUS", new Vector2(20, 20), Color.Cyan);
                _spriteBatch.DrawString(_font, $"Speed: {Math.Abs(_playerShip.Speed):F1}", new Vector2(20, 45), Color.White);
                _spriteBatch.DrawString(_font, $"Throttle: {_playerShip.GetThrottle()*100:F0}%", new Vector2(20, 65), Color.White);
                _spriteBatch.DrawString(_font, $"Mode: {_playerShip.GetFlightStatus()}", new Vector2(20, 85), Color.Yellow);
                if (_playerShip.IsNewtonianMode)
                    _spriteBatch.DrawString(_font, "NEWTONIAN", new Vector2(20, 105), Color.Lime);
                if (_camera.IsTurretViewActive)
                    _spriteBatch.DrawString(_font, "TURRET VIEW", new Vector2(20, 125), Color.Orange);
                if (_playerShip.IsGotoActive && _playerShip.CurrentGotoTarget != null)
                    _spriteBatch.DrawString(_font, $"GOTO: {_playerShip.CurrentGotoTarget.Name}", new Vector2(20, 145), Color.Orange);
            }
            else
            {
                // Fallback visual indicators when no font available
                // Speed bar (horizontal)
                int speedBarWidth = (int)(250 * MathHelper.Clamp(Math.Abs(_playerShip.Speed) / _playerShip.MaxSpeed, 0f, 1f));
                _spriteBatch.Draw(_pixel, new Rectangle(20, 30, 250, 20), Color.DarkGray * 0.5f);
                _spriteBatch.Draw(_pixel, new Rectangle(20, 30, speedBarWidth, 20), Color.Cyan);
                
                // Throttle bar (horizontal)
                float throttle = _playerShip.GetThrottle();
                int throttleBarWidth = (int)(250 * Math.Abs(throttle));
                Color throttleColor = throttle >= 0 ? Color.Green : Color.Orange;
                _spriteBatch.Draw(_pixel, new Rectangle(20, 60, 250, 20), Color.DarkGray * 0.5f);
                _spriteBatch.Draw(_pixel, new Rectangle(20, 60, throttleBarWidth, 20), throttleColor);
                
                // Status indicators
                int yPos = 90;
                if (_playerShip.IsAfterburnerActive)
                {
                    _spriteBatch.Draw(_pixel, new Rectangle(20, yPos, 250, 20), Color.Red * 0.7f);
                    yPos += 25;
                }
                if (_playerShip.IsCruiseActive)
                {
                    _spriteBatch.Draw(_pixel, new Rectangle(20, yPos, 250, 20), Color.Yellow * 0.7f);
                    yPos += 25;
                }
                if (_playerShip.IsNewtonianMode)
                {
                    _spriteBatch.Draw(_pixel, new Rectangle(20, yPos, 250, 20), Color.Lime * 0.7f);
                    yPos += 25;
                }
                if (_camera.IsTurretViewActive)
                {
                    _spriteBatch.Draw(_pixel, new Rectangle(20, yPos, 250, 20), Color.Orange * 0.7f);
                    yPos += 25;
                }
                if (_playerShip.EnginesKilled)
                {
                    _spriteBatch.Draw(_pixel, new Rectangle(20, yPos, 250, 20), Color.Gray * 0.7f);
                }
            }
        }

        private void DrawSpaceObjectBrackets()
        {
            if (_spaceObjects.Count == 0) return;
            for (int i = 0; i < _spaceObjects.Count; i++)
            {
                SpaceObject obj = _spaceObjects[i];
                Vector3 screenPos3D = GraphicsDevice.Viewport.Project(obj.Position, _camera.Projection, _camera.View, Matrix.Identity);
                if (screenPos3D.Z < 0 || screenPos3D.Z > 1) continue; // behind camera
                Vector2 screenPos = new Vector2(screenPos3D.X, screenPos3D.Y);
                float distance = Vector3.Distance(_playerShip.Position, obj.Position);
                float scale = MathHelper.Clamp(8000f / distance, 0.4f, 2.5f);
                DrawTargetBracket(screenPos, scale, i == _selectedSpaceObjectIndex);
                if (_font != null)
                {
                    _spriteBatch.DrawString(_font, obj.Name, screenPos + new Vector2(12, -20), i == _selectedSpaceObjectIndex ? Color.Orange : Color.LightBlue);
                    _spriteBatch.DrawString(_font, $"{distance/1000f:F2}k", screenPos + new Vector2(12, 0), Color.LightGray);
                }
            }
        }

        private void DrawTargetBracket(Vector2 center, float scale, bool selected)
        {
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

        private void DrawGotoStatus()
        {
            if (_playerShip.IsGotoActive && _playerShip.CurrentGotoTarget != null && _font != null)
            {
                Vector2 pos = new Vector2(GraphicsDevice.Viewport.Width / 2 - 120, 30);
                _spriteBatch.Draw(_pixel, new Rectangle((int)pos.X - 10, (int)pos.Y - 10, 250, 50), Color.Black * 0.4f);
                float dist = Vector3.Distance(_playerShip.Position, _playerShip.CurrentGotoTarget.Position) - _playerShip.CurrentGotoTarget.Radius;
                _spriteBatch.DrawString(_font, $"GOTO: {_playerShip.CurrentGotoTarget.Name}", pos, Color.Orange);
                _spriteBatch.DrawString(_font, $"DIST: {Math.Max(dist,0)/1000f:F2} k", pos + new Vector2(0, 22), Color.White);
            }
        }
        
        private void DrawSunDistanceIndicator()
        {
            // Calculate distance to sun
            float sunDistance = _sun.GetDistance(_playerShip.Position);
            float sunDistanceKm = sunDistance / 1000f;
            
            // Draw sun distance panel (top-right) - MADE BIGGER
            Rectangle panel = new Rectangle(GraphicsDevice.Viewport.Width - 320, 10, 310, 150);
            _spriteBatch.Draw(_pixel, panel, Color.Black * 0.8f);
            _spriteBatch.Draw(_pixel, new Rectangle(panel.X, panel.Y, panel.Width, 3), Color.Orange);
            _spriteBatch.Draw(_pixel, new Rectangle(panel.X, panel.Y, 3, panel.Height), Color.Orange);
            _spriteBatch.Draw(_pixel, new Rectangle(panel.X + panel.Width - 3, panel.Y, 3, panel.Height), Color.Orange);
            _spriteBatch.Draw(_pixel, new Rectangle(panel.X, panel.Y + panel.Height - 3, panel.Width, 3), Color.Orange);
            
            // Draw title bar with "SUN" label using bars
            _spriteBatch.Draw(_pixel, new Rectangle(panel.X + 5, panel.Y + 5, panel.Width - 10, 30), Color.Orange * 0.4f);
            
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
            _spriteBatch.Draw(_pixel, new Rectangle(panel.X + 10, panel.Y + 45, panel.Width - 20, 50), Color.DarkGray * 0.5f);
            // Distance bar (fills as you get closer)
            _spriteBatch.Draw(_pixel, new Rectangle(panel.X + 10, panel.Y + 45, barWidth, 50), distanceColor * 0.9f);
            
            // Numeric display using stacked bars (like 7-segment display)
            DrawNumericDistance(panel.X + 15, panel.Y + 105, sunDistanceKm);
        }
        
        private void DrawNumericDistance(int x, int y, float distanceKm)
        {
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
            
            if (thousands > 0)
            {
                DrawSimpleDigit(x, y, thousands);
                x += digitWidth + spacing;
            }
            if (thousands > 0 || hundreds > 0)
            {
                DrawSimpleDigit(x, y, hundreds);
                x += digitWidth + spacing;
            }
            DrawSimpleDigit(x, y, tens);
            x += digitWidth + spacing;
            DrawSimpleDigit(x, y, ones);
        }
        
        private void DrawSimpleDigit(int x, int y, int digit)
        {
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
