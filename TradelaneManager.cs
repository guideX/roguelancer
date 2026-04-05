using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Roguelancer.Configuration;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace Roguelancer {
    /// <summary>
    /// Manages all tradelanes in the current system.
    /// Handles loading configs, creating rings, transit state, and rendering.
    /// </summary>
    public class TradelaneManager {
        private GraphicsDevice _graphicsDevice;
        private SpriteFont _font;
        private Texture2D _pixel;

        private List<TradeLane> _tradeLanes = new();
        private List<TradelaneConfig> _allConfigs = new();

        // The currently active tradelane (player is traveling in)
        private TradeLane _activeLane;

        // Input tracking
        private KeyboardState _prevKeys;

        // Prompt visibility
        private TradelaneRing _nearbyRing;
        private TradeLane _nearbyLane;

        // Auto-orient approach state
        private bool _isAutoOrienting;
        private TradelaneRing _autoOrientTarget;
        private TradeLane _autoOrientLane;

        /// <summary>
        /// Whether the player is currently traveling in a tradelane
        /// </summary>
        public bool IsInTransit => _activeLane?.IsActive == true;

        /// <summary>
        /// Current system index
        /// </summary>
        public int CurrentSystemIndex { get; private set; } = 1;

        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions {
            PropertyNameCaseInsensitive = true,
            AllowTrailingCommas = true,
            ReadCommentHandling = JsonCommentHandling.Skip
        };

        public TradelaneManager(GraphicsDevice graphicsDevice, SpriteFont font) {
            _graphicsDevice = graphicsDevice;
            _font = font;

            _pixel = new Texture2D(graphicsDevice, 1, 1);
            _pixel.SetData(new[] { Color.White });
        }

        /// <summary>
        /// Load all tradelane configurations from the Configuration/tradelanes directory
        /// </summary>
        public void LoadAllConfigs() {
            _allConfigs.Clear();
            string tradelanesDir = Path.Combine("Configuration", "tradelanes");

            if (!Directory.Exists(tradelanesDir)) {
                Console.WriteLine($"[TRADELANES] Directory not found: {tradelanesDir}");
                Directory.CreateDirectory(tradelanesDir);
                Console.WriteLine($"[TRADELANES] Created directory: {tradelanesDir}");
                return;
            }

            foreach (string file in Directory.GetFiles(tradelanesDir, "*.json")) {
                try {
                    string json = File.ReadAllText(file);
                    var config = JsonSerializer.Deserialize<TradelaneConfig>(json, JsonOptions);
                    if (config != null) {
                        _allConfigs.Add(config);
                        Console.WriteLine($"[TRADELANES] Loaded: {config.Name} (System {config.SystemIndex})");
                    }
                } catch (Exception ex) {
                    Console.WriteLine($"[TRADELANES] Error loading {file}: {ex.Message}");
                }
            }

            Console.WriteLine($"[TRADELANES] Total configs loaded: {_allConfigs.Count}");
        }

        /// <summary>
        /// Load tradelanes for the specified system index
        /// </summary>
        public void LoadTradelanesForSystem(int systemIndex) {
            _tradeLanes.Clear();
            _activeLane = null;
            CurrentSystemIndex = systemIndex;

            foreach (var config in _allConfigs) {
                if (config.SystemIndex == systemIndex) {
                    var lane = new TradeLane(_graphicsDevice, config);
                    _tradeLanes.Add(lane);
                    Console.WriteLine($"[TRADELANES] Created tradelane: {config.Name} with {lane.Rings.Count} rings");
                }
            }

            Console.WriteLine($"[TRADELANES] Active tradelanes in system {systemIndex}: {_tradeLanes.Count}");
        }

        /// <summary>
        /// Load the 3D model and assign it to all rings
        /// </summary>
        public void LoadContent(ContentManager content) {
            foreach (var lane in _tradeLanes) {
                string modelPath = lane.Config.ModelPath;
                if (string.IsNullOrEmpty(modelPath)) {
                    modelPath = "BASES/TRACK_RING/TRACK_RING";
                }

                try {
                    var model = content.Load<Model>(modelPath);
                    lane.SetModel(model);
                    Console.WriteLine($"[TRADELANES] Model loaded for: {lane.Config.Name}");
                } catch (Exception ex) {
                    Console.WriteLine($"[TRADELANES] Error loading model '{modelPath}': {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Get all active TradeLane objects for the current system
        /// </summary>
        public List<TradeLane> GetTradeLanes() {
            return _tradeLanes;
        }

        /// <summary>
        /// Get all tradelane entry/exit rings as targetable SpaceObjects
        /// </summary>
        public List<SpaceObject> GetTradelaneSpaceObjects() {
            var objects = new List<SpaceObject>();
            foreach (var lane in _tradeLanes) {
                objects.AddRange(lane.GetRingsAsSpaceObjects());
            }
            return objects;
        }

        /// <summary>
        /// Update all tradelanes, handle transit input, and manage ship position
        /// </summary>
        /// <param name="gameTime">Game time</param>
        /// <param name="playerShip">Player ship (position/velocity modified during transit)</param>
        /// <param name="keyboardState">Current keyboard state</param>
        public void Update(GameTime gameTime, Ship playerShip, KeyboardState keyboardState) {
            float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
            Vector3 playerPos = playerShip.Position;

            // Update all tradelanes
            _nearbyRing = null;
            _nearbyLane = null;

            foreach (var lane in _tradeLanes) {
                lane.Update(gameTime, playerPos);

                if (!IsInTransit && lane.NearbyEntryRing != null) {
                    _nearbyRing = lane.NearbyEntryRing;
                    _nearbyLane = lane;
                }
            }

            // Auto-orient approach: when within AutoOrientRange, smoothly rotate and guide the ship
            _isAutoOrienting = false;
            _autoOrientTarget = null;
            _autoOrientLane = null;

            if (!IsInTransit && _nearbyRing != null && _nearbyLane != null) {
                float distToRing = Vector3.Distance(playerPos, _nearbyRing.Position);
                float autoOrientRange = _nearbyLane.Config.AutoOrientRange;
                float activationRange = _nearbyLane.Config.ActivationRange;

                if (distToRing <= autoOrientRange) {
                    _isAutoOrienting = true;
                    _autoOrientTarget = _nearbyRing;
                    _autoOrientLane = _nearbyLane;

                    // Determine the direction the ship should face to fly through the ring
                    Vector3 travelDir = _nearbyRing.Direction == TradelaneRing.RingDirection.Forward
                        ? _nearbyLane.LaneDirection
                        : -_nearbyLane.LaneDirection;

                    // Smoothly orient the ship toward the ring opening
                    float orientStrength = 1f - MathHelper.Clamp((distToRing - activationRange) / (autoOrientRange - activationRange), 0f, 1f);
                    float orientSpeed = 2.5f * orientStrength;

                    Vector3 currentForward = playerShip.Forward;
                    float alignment = Vector3.Dot(currentForward, travelDir);
                    if (alignment < 0.999f) {
                        Vector3 rotAxis = Vector3.Cross(currentForward, travelDir);
                        if (rotAxis.LengthSquared() > 0.0001f) {
                            rotAxis.Normalize();
                            float angle = (float)Math.Acos(MathHelper.Clamp(alignment, -1f, 1f));
                            float step = Math.Min(angle, orientSpeed * dt);
                            playerShip.ApplyRotation(rotAxis, step);
                        }
                    }

                    // Gently pull the ship toward the ring center (lateral correction)
                    Vector3 toRingCenter = _nearbyRing.Position - playerPos;
                    // Remove the component along the travel direction to get the lateral offset
                    Vector3 lateralOffset = toRingCenter - travelDir * Vector3.Dot(toRingCenter, travelDir);
                    if (lateralOffset.LengthSquared() > 1f) {
                        float pullStrength = 80f * orientStrength;
                        playerShip.Position += Vector3.Normalize(lateralOffset) * Math.Min(lateralOffset.Length(), pullStrength * dt);
                    }
                }
            }

            // Check for tradelane activation (F5 key when near an entry ring)
            if (_nearbyRing != null && _nearbyLane != null &&
                keyboardState.IsKeyDown(Keys.F5) && _prevKeys.IsKeyUp(Keys.F5)) {
                float distForActivation = Vector3.Distance(playerPos, _nearbyRing.Position);
                if (distForActivation <= _nearbyLane.Config.DockingRange) {
                    if (_nearbyLane.StartTravel(_nearbyRing)) {
                        _activeLane = _nearbyLane;
                        // Orient the ship to face the tradelane travel direction
                        Vector3 dir = _nearbyRing.Direction == TradelaneRing.RingDirection.Forward
                            ? _nearbyLane.LaneDirection
                            : -_nearbyLane.LaneDirection;
                        playerShip.SetFacing(dir);
                        Console.WriteLine($"[TRADELANES] Player entered tradelane: {_nearbyLane.Config.Name}");
                    }
                }
            }

            // During transit, lock the player ship to the lane
            if (IsInTransit && _activeLane != null) {
                Vector3 transitPos = _activeLane.GetCurrentTransitPosition();
                playerShip.Position = transitPos;
                playerShip.Velocity = _activeLane.GetTravelForward() * _activeLane.Config.TravelSpeed;

                // Allow player to exit early with ESC
                if (keyboardState.IsKeyDown(Keys.Escape) && _prevKeys.IsKeyUp(Keys.Escape)) {
                    ExitTradelane(playerShip);
                }

                // Check if travel completed
                if (!_activeLane.IsActive) {
                    // Lane travel completed naturally
                    _activeLane = null;
                    playerShip.Velocity = Vector3.Zero;
                    Console.WriteLine("[TRADELANES] Player exited tradelane (arrived)");
                }

                // Check if lane was broken
                if (_activeLane != null && _activeLane.IsBroken) {
                    ExitTradelane(playerShip);
                    Console.WriteLine("[TRADELANES] Player ejected from tradelane (lane broken!)");
                }
            }

            _prevKeys = keyboardState;
        }

        /// <summary>
        /// Exit the tradelane immediately (ESC or lane break)
        /// </summary>
        private void ExitTradelane(Ship playerShip) {
            if (_activeLane != null) {
                // Give the player some residual velocity in the travel direction
                playerShip.Velocity = _activeLane.GetTravelForward() * 250f;
                _activeLane = null;
            }
        }

        /// <summary>
        /// Draw all tradelane ring models (3D opaque pass)
        /// </summary>
        public void Draw3D(Matrix view, Matrix projection, Vector3 lightDirection) {
            foreach (var lane in _tradeLanes) {
                lane.Draw(view, projection, lightDirection);
            }
        }

        /// <summary>
        /// Draw energy effects on rings (additive pass)
        /// </summary>
        public void DrawEnergyEffects(Matrix view, Matrix projection) {
            foreach (var lane in _tradeLanes) {
                lane.DrawEnergyEffects(view, projection);
            }
        }

        /// <summary>
        /// Draw HUD elements (proximity prompt, transit status)
        /// </summary>
        public void DrawHUD(SpriteBatch spriteBatch) {
            if (spriteBatch == null) return;

            // Draw transit status
            if (IsInTransit && _activeLane != null && _font != null) {
                int w = _graphicsDevice.Viewport.Width;
                int h = _graphicsDevice.Viewport.Height;

                string statusText = $"TRADELANE: {_activeLane.Config.Name}";
                Vector2 statusSize = _font.MeasureString(statusText);

                // Background bar at top
                Rectangle statusBg = new Rectangle(0, 0, w, 40);
                spriteBatch.Draw(_pixel, statusBg, new Color(0, 50, 100) * 0.7f);
                spriteBatch.Draw(_pixel, new Rectangle(0, 38, w, 2), _activeLane.Config.RingColor);

                // Status text
                spriteBatch.DrawString(_font, statusText,
                    new Vector2((w - statusSize.X) / 2, 10), Color.White);

                // Ring progress indicator
                int progressBarWidth = 300;
                int progressBarX = (w - progressBarWidth) / 2;
                int progressBarY = 45;
                float progress = (float)_activeLane.CurrentRingIndex / Math.Max(1, _activeLane.Rings.Count - 1);
                if (_activeLane.TravelDirection < 0) progress = 1f - progress;

                spriteBatch.Draw(_pixel, new Rectangle(progressBarX, progressBarY, progressBarWidth, 6), Color.DarkGray * 0.5f);
                spriteBatch.Draw(_pixel, new Rectangle(progressBarX, progressBarY, (int)(progressBarWidth * progress), 6), _activeLane.Config.RingColor);

                // ESC to exit hint
                string escText = "Press ESC to disengage";
                Vector2 escSize = _font.MeasureString(escText);
                spriteBatch.DrawString(_font, escText,
                    new Vector2((w - escSize.X) / 2, progressBarY + 12), Color.Gray * 0.7f);

                return;
            }

            // Draw proximity prompt when near a tradelane entry ring
            if (_nearbyRing != null && _nearbyLane != null && _font != null) {
                int w = _graphicsDevice.Viewport.Width;
                int h = _graphicsDevice.Viewport.Height;

                string direction = _nearbyRing.Direction == TradelaneRing.RingDirection.Forward ? "forward" : "reverse";
                string ringLabel = _nearbyRing.Direction == TradelaneRing.RingDirection.Forward ? "TOP" : "BOTTOM";

                // Show different prompts based on whether we're auto-orienting or close enough to dock
                string promptText;
                Color promptColor;
                if (_isAutoOrienting) {
                    promptText = $"Approaching {_nearbyLane.Config.Name} [{ringLabel}] ({direction})\nPress F5 to dock";
                    promptColor = Color.Cyan;
                } else {
                    promptText = $"Press F5 to enter {_nearbyLane.Config.Name} [{ringLabel}] ({direction})";
                    promptColor = Color.White;
                }

                Vector2 textSize = _font.MeasureString(promptText);

                int boxW = (int)textSize.X + 40;
                int boxH = (int)textSize.Y + 20;
                int boxX = (w - boxW) / 2;
                int boxY = h / 2 + 100;

                // Background
                spriteBatch.Draw(_pixel, new Rectangle(boxX, boxY, boxW, boxH), Color.Black * 0.7f);
                // Border
                Color borderColor = _isAutoOrienting ? Color.Cyan * 0.8f : _nearbyLane.Config.RingColor;
                spriteBatch.Draw(_pixel, new Rectangle(boxX, boxY, boxW, 2), borderColor);
                spriteBatch.Draw(_pixel, new Rectangle(boxX, boxY + boxH - 2, boxW, 2), borderColor);
                spriteBatch.Draw(_pixel, new Rectangle(boxX, boxY, 2, boxH), borderColor);
                spriteBatch.Draw(_pixel, new Rectangle(boxX + boxW - 2, boxY, 2, boxH), borderColor);

                // Text
                spriteBatch.DrawString(_font, promptText,
                    new Vector2(boxX + 20, boxY + 10), promptColor);
            }
        }
    }
}
