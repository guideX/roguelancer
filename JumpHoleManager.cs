using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Roguelancer.Configuration;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace Roguelancer {
    /// <summary>
    /// Manages all jump holes in the current system, handles transit between systems.
    /// </summary>
    public class JumpHoleManager {
        private GraphicsDevice _graphicsDevice;
        private SpriteFont _font;
        private Texture2D _pixel;

        // Current system's jump holes
        private List<JumpHole> _jumpHoles = new();

        // All loaded jump hole configs (across all systems)
        private List<JumpHoleConfig> _allConfigs = new();

        // Transit effect
        private JumpHoleTransitEffect _transitEffect;

        // Transit state
        public bool IsInTransit { get; private set; }
        public int CurrentSystemIndex { get; private set; } = 1;
        private JumpHoleConfig _transitTarget;

        // Callback for system change
        public event Action<int, string> OnSystemChange;

        // Input tracking
        private KeyboardState _prevKeys;

        // Prompt visibility
        private JumpHole _nearbyJumpHole;

        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions {
            PropertyNameCaseInsensitive = true,
            AllowTrailingCommas = true,
            ReadCommentHandling = JsonCommentHandling.Skip
        };

        public JumpHoleManager(GraphicsDevice graphicsDevice, SpriteFont font) {
            _graphicsDevice = graphicsDevice;
            _font = font;

            _pixel = new Texture2D(graphicsDevice, 1, 1);
            _pixel.SetData(new[] { Color.White });

            _transitEffect = new JumpHoleTransitEffect(graphicsDevice, font);
        }

        /// <summary>
        /// Load all jump hole configurations from the Configuration/jumpholes directory
        /// </summary>
        public void LoadAllConfigs() {
            _allConfigs.Clear();
            string jumpHolesDir = Path.Combine("Configuration", "jumpholes");

            if (!Directory.Exists(jumpHolesDir)) {
                Console.WriteLine($"[JUMPHOLES] Directory not found: {jumpHolesDir}");
                return;
            }

            foreach (string file in Directory.GetFiles(jumpHolesDir, "*.json")) {
                try {
                    string json = File.ReadAllText(file);
                    var config = JsonSerializer.Deserialize<JumpHoleConfig>(json, JsonOptions);
                    if (config != null) {
                        _allConfigs.Add(config);
                        Console.WriteLine($"[JUMPHOLES] Loaded: {config.Name} (System {config.SystemIndex} -> System {config.TargetSystemIndex})");
                    }
                } catch (Exception ex) {
                    Console.WriteLine($"[JUMPHOLES] Error loading {file}: {ex.Message}");
                }
            }

            Console.WriteLine($"[JUMPHOLES] Total configs loaded: {_allConfigs.Count}");
        }

        /// <summary>
        /// Load jump holes for the specified system index
        /// </summary>
        public void LoadJumpHolesForSystem(int systemIndex) {
            _jumpHoles.Clear();
            CurrentSystemIndex = systemIndex;

            foreach (var config in _allConfigs) {
                if (config.SystemIndex == systemIndex) {
                    var jumpHole = new JumpHole(_graphicsDevice, config);
                    _jumpHoles.Add(jumpHole);
                    Console.WriteLine($"[JUMPHOLES] Created jump hole: {config.Name} at {config.Position}");
                }
            }

            Console.WriteLine($"[JUMPHOLES] Active jump holes in system {systemIndex}: {_jumpHoles.Count}");
        }

        /// <summary>
        /// Get all jump holes as SpaceObjects for targeting
        /// </summary>
        public List<SpaceObject> GetJumpHolesAsSpaceObjects() {
            var objects = new List<SpaceObject>();
            foreach (var jh in _jumpHoles) {
                objects.Add(jh);
            }
            return objects;
        }

        /// <summary>
        /// Get the list of active jump holes
        /// </summary>
        public List<JumpHole> GetJumpHoles() {
            return _jumpHoles;
        }

        /// <summary>
        /// Update jump holes and handle transit
        /// </summary>
        public void Update(GameTime gameTime, Vector3 playerPosition, KeyboardState keyboardState) {
            float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;

            // Update transit effect
            if (IsInTransit) {
                _transitEffect.Update(dt);

                if (!_transitEffect.IsActive) {
                    // Transit complete - trigger system change
                    CompleteTransit();
                }

                _prevKeys = keyboardState;
                return; // Don't update jump holes during transit
            }

            // Update all jump holes
            _nearbyJumpHole = null;
            foreach (var jh in _jumpHoles) {
                jh.Update(gameTime, playerPosition);

                if (jh.IsPlayerInRange) {
                    _nearbyJumpHole = jh;
                }
            }

            // Check for jump activation (F4 key when near a jump hole)
            if (_nearbyJumpHole != null &&
                keyboardState.IsKeyDown(Keys.F4) && _prevKeys.IsKeyUp(Keys.F4)) {
                InitiateTransit(_nearbyJumpHole.Config);
            }

            _prevKeys = keyboardState;
        }

        /// <summary>
        /// Start the jump hole transit sequence
        /// </summary>
        private void InitiateTransit(JumpHoleConfig config) {
            IsInTransit = true;
            _transitTarget = config;

            // Find target system name
            string targetName = $"System {config.TargetSystemIndex}";

            // Start the visual effect
            _transitEffect.Start(
                config.TransitDuration,
                targetName,
                config.RingColor,
                config.CoreColor
            );

            Console.WriteLine($"[JUMPHOLES] Transit initiated: {config.Name} -> System {config.TargetSystemIndex}");
        }

        /// <summary>
        /// Complete the transit and switch systems
        /// </summary>
        private void CompleteTransit() {
            IsInTransit = false;
            int targetSystem = _transitTarget.TargetSystemIndex;
            string targetJumpHole = _transitTarget.TargetJumpHoleName;

            Console.WriteLine($"[JUMPHOLES] Transit complete! Switching to system {targetSystem}");

            // Find the destination jump hole position (where player will appear)
            Vector3 arrivalPosition = Vector3.Zero;
            foreach (var config in _allConfigs) {
                if (config.SystemIndex == targetSystem && config.Name == targetJumpHole) {
                    // Arrive slightly in front of the destination jump hole
                    arrivalPosition = config.Position + new Vector3(0, 0, config.Radius * 2f);
                    break;
                }
            }

            // Load jump holes for the new system
            LoadJumpHolesForSystem(targetSystem);

            // Notify game to switch systems
            OnSystemChange?.Invoke(targetSystem, targetJumpHole);

            _transitTarget = null;
        }

        /// <summary>
        /// Get the arrival position when entering a system through a specific jump hole
        /// </summary>
        public Vector3 GetArrivalPosition(int systemIndex, string jumpHoleName) {
            foreach (var config in _allConfigs) {
                if (config.SystemIndex == systemIndex && config.Name == jumpHoleName) {
                    // Position the player just outside the jump hole
                    return config.Position + new Vector3(0, 0, config.Radius * 2f);
                }
            }
            return new Vector3(3000, 1500, 2000); // Fallback
        }

        /// <summary>
        /// Draw all jump holes (3D rendering phase)
        /// </summary>
        public void Draw3D(Matrix view, Matrix projection, Vector3 cameraPosition) {
            if (IsInTransit) return; // Don't draw jump holes during transit

            foreach (var jh in _jumpHoles) {
                jh.Draw(view, projection, cameraPosition);
            }
        }

        /// <summary>
        /// Draw proximity prompt when near a jump hole
        /// </summary>
        public void DrawHUD(SpriteBatch spriteBatch) {
            // Draw transit effect (uses its own sprite batch)
            if (IsInTransit) {
                _transitEffect.Draw();
                return;
            }

            // Draw proximity prompt when near a jump hole
            if (_nearbyJumpHole != null && _font != null) {
                int w = _graphicsDevice.Viewport.Width;
                int h = _graphicsDevice.Viewport.Height;

                string promptText = $"Press F4 to enter {_nearbyJumpHole.Name}";
                Vector2 textSize = _font.MeasureString(promptText);
                Vector2 pos = new Vector2(w / 2f - textSize.X / 2, h / 2f + 100);

                // Pulsing effect
                float pulse = 0.6f + 0.4f * (float)Math.Sin(DateTime.Now.TimeOfDay.TotalSeconds * 3.0);

                // Background
                Rectangle bg = new Rectangle(
                    (int)pos.X - 15, (int)pos.Y - 8,
                    (int)textSize.X + 30, (int)textSize.Y + 16
                );
                spriteBatch.Draw(_pixel, bg, Color.Black * 0.7f);

                // Border
                Color borderColor = Color.Lerp(_nearbyJumpHole.Config.RingColor, Color.White, 0.3f) * pulse;
                spriteBatch.Draw(_pixel, new Rectangle(bg.X, bg.Y, bg.Width, 2), borderColor);
                spriteBatch.Draw(_pixel, new Rectangle(bg.X, bg.Bottom - 2, bg.Width, 2), borderColor);
                spriteBatch.Draw(_pixel, new Rectangle(bg.X, bg.Y, 2, bg.Height), borderColor);
                spriteBatch.Draw(_pixel, new Rectangle(bg.Right - 2, bg.Y, 2, bg.Height), borderColor);

                // Text
                spriteBatch.DrawString(_font, promptText, pos, Color.White * pulse);
            }
        }

        // ?????????????????????????????????????????????????????????????????
        //  Autopilot hooks (called by GotoAutopilot)
        // ?????????????????????????????????????????????????????????????????

        /// <summary>
        /// Attempts to initiate a jump hole transit from code (GotoAutopilot).
        /// Returns true if transit was successfully started.
        /// </summary>
        public bool TryInitiateTransitFor(JumpHole jumpHole, Ship playerShip) {
            if (IsInTransit) return false;
            if (jumpHole == null) return false;

            float dist = Vector3.Distance(playerShip.Position, jumpHole.Position);
            if (dist > jumpHole.Config.Radius * 2.5f) return false; // too far

            InitiateTransit(jumpHole.Config);
            return true;
        }
    }
}
