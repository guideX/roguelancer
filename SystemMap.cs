using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;

namespace Roguelancer {
    /// <summary>
    /// Full-screen 2D system map overlay showing bases, jump holes, tradelanes, planets, and the sun.
    /// Toggled on/off with M. Does NOT display individual ships.
    /// </summary>
    public class SystemMap {
        private SpriteFont _font;
        private Texture2D _pixel;
        private GraphicsDevice _graphicsDevice;

        /// <summary>
        /// Whether the map overlay is currently visible
        /// </summary>
        public bool IsVisible { get; private set; }

        // Cached references for drawing
        private List<SpaceObject> _spaceObjects;
        private List<JumpHole> _jumpHoles;
        private List<TradeLane> _tradeLanes;
        private Vector3 _playerPosition;
        private string _systemName;

        // Map layout
        private const int MapMargin = 60;
        private const float WorldExtent = 25000f; // units from center to edge

        // Animation
        private float _pulseTimer;

        public SystemMap(GraphicsDevice graphicsDevice, SpriteFont font) {
            _graphicsDevice = graphicsDevice;
            _font = font;

            _pixel = new Texture2D(graphicsDevice, 1, 1);
            _pixel.SetData(new[] { Color.White });
        }

        /// <summary>
        /// Toggle the map on or off
        /// </summary>
        public void Toggle() {
            IsVisible = !IsVisible;
        }

        /// <summary>
        /// Show the map
        /// </summary>
        public void Show() {
            IsVisible = true;
        }

        /// <summary>
        /// Hide the map
        /// </summary>
        public void Hide() {
            IsVisible = false;
        }

        /// <summary>
        /// Update references used for drawing. Call every frame (cheap).
        /// </summary>
        public void UpdateData(
            List<SpaceObject> spaceObjects,
            List<JumpHole> jumpHoles,
            List<TradeLane> tradeLanes,
            Vector3 playerPosition,
            string systemName) {
            _spaceObjects = spaceObjects;
            _jumpHoles = jumpHoles;
            _tradeLanes = tradeLanes;
            _playerPosition = playerPosition;
            _systemName = systemName;
        }

        public void Update(GameTime gameTime) {
            if (!IsVisible) return;
            _pulseTimer += (float)gameTime.ElapsedGameTime.TotalSeconds;
        }

        /// <summary>
        /// Draw the full-screen map overlay. Must be called inside a SpriteBatch.Begin/End block.
        /// </summary>
        public void Draw(SpriteBatch spriteBatch) {
            if (!IsVisible || _spaceObjects == null) return;

            int screenW = _graphicsDevice.Viewport.Width;
            int screenH = _graphicsDevice.Viewport.Height;

            // Semi-transparent background
            spriteBatch.Draw(_pixel, new Rectangle(0, 0, screenW, screenH), Color.Black * 0.85f);

            // Map area
            int mapX = MapMargin;
            int mapY = MapMargin + 40; // room for title
            int mapW = screenW - MapMargin * 2;
            int mapH = screenH - MapMargin * 2 - 40;

            // Map border
            Color borderColor = new Color(60, 120, 180);
            DrawRect(spriteBatch, mapX, mapY, mapW, mapH, borderColor);

            // Grid inside map
            DrawGrid(spriteBatch, mapX, mapY, mapW, mapH);

            // Title
            if (_font != null) {
                string title = $"SYSTEM MAP - {_systemName ?? "Unknown"}";
                Vector2 titleSize = _font.MeasureString(title);
                spriteBatch.DrawString(_font, title,
                    new Vector2((screenW - titleSize.X) / 2, MapMargin + 8),
                    Color.Gold);

                // Instructions
                string instructions = "Click to target | Press M or ESC to close";

                Vector2 instrSize = _font.MeasureString(instructions);
                spriteBatch.DrawString(_font, instructions,
                    new Vector2((screenW - instrSize.X) / 2, screenH - MapMargin + 8),
                    Color.Gray);
            }

            // Draw tradelane routes (lines connecting rings)
            DrawTradeLanes(spriteBatch, mapX, mapY, mapW, mapH);

            // Draw all non-ship space objects
            foreach (var obj in _spaceObjects) {
                // Skip NPC ships (they are SpaceObjects but also NpcShip)
                if (obj is NpcShip) continue;

                Vector2 mapPos = WorldToMap(obj.Position, mapX, mapY, mapW, mapH);

                // Determine icon style by type
                GetObjectStyle(obj, out Color objColor, out string label, out int iconSize);

                // Clamp to map bounds
                mapPos.X = MathHelper.Clamp(mapPos.X, mapX + 4, mapX + mapW - 4);
                mapPos.Y = MathHelper.Clamp(mapPos.Y, mapY + 4, mapY + mapH - 4);

                // Draw icon
                DrawIcon(spriteBatch, mapPos, objColor, iconSize, obj);

                // Draw label
                if (_font != null && !string.IsNullOrEmpty(label)) {
                    Vector2 labelSize = _font.MeasureString(label);
                    Vector2 labelPos = new Vector2(mapPos.X - labelSize.X / 2, mapPos.Y + iconSize + 2);

                    // Keep label on screen
                    labelPos.X = MathHelper.Clamp(labelPos.X, mapX + 2, mapX + mapW - labelSize.X - 2);
                    labelPos.Y = MathHelper.Clamp(labelPos.Y, mapY + 2, mapY + mapH - labelSize.Y - 2);

                    // Shadow
                    spriteBatch.DrawString(_font, label, labelPos + Vector2.One, Color.Black * 0.6f);
                    spriteBatch.DrawString(_font, label, labelPos, objColor * 0.9f);
                }
            }

            // Draw player position as a blinking dot
            DrawPlayerIcon(spriteBatch, mapX, mapY, mapW, mapH);
        }

        /// <summary>
        /// Convert world XZ position to 2D map pixel position (top-down view, Y is ignored)
        /// </summary>
        private Vector2 WorldToMap(Vector3 worldPos, int mapX, int mapY, int mapW, int mapH) {
            float nx = (worldPos.X + WorldExtent) / (WorldExtent * 2f);
            float nz = (worldPos.Z + WorldExtent) / (WorldExtent * 2f);

            return new Vector2(
                mapX + nx * mapW,
                mapY + nz * mapH
            );
        }

        /// <summary>
        /// Convert a 2D screen pixel position back to a world XZ position (Y = 0).
        /// </summary>
        private Vector3 MapToWorld(Vector2 screenPos, int mapX, int mapY, int mapW, int mapH) {
            float nx = (screenPos.X - mapX) / mapW;
            float nz = (screenPos.Y - mapY) / mapH;
            return new Vector3(
                nx * (WorldExtent * 2f) - WorldExtent,
                0f,
                nz * (WorldExtent * 2f) - WorldExtent
            );
        }

        /// <summary>
        /// Try to select a space object at the given screen click position.
        /// Returns the matched SpaceObject or null if nothing was close enough.
        /// </summary>
        public SpaceObject HandleClick(Vector2 clickPos, List<SpaceObject> spaceObjects) {
            if (!IsVisible || spaceObjects == null) return null;

            int screenW = _graphicsDevice.Viewport.Width;
            int screenH = _graphicsDevice.Viewport.Height;
            int mapX = MapMargin;
            int mapY = MapMargin + 40;
            int mapW = screenW - MapMargin * 2;
            int mapH = screenH - MapMargin * 2 - 40;

            // Only handle clicks within the map area
            if (clickPos.X < mapX || clickPos.X > mapX + mapW ||
                clickPos.Y < mapY || clickPos.Y > mapY + mapH)
                return null;

            const float HitRadiusPx = 18f; // pixel radius for hit detection
            SpaceObject closest = null;
            float closestDist = float.MaxValue;

            foreach (var obj in spaceObjects) {
                if (obj is NpcShip) continue; // NPC ships are not shown on the map

                Vector2 mapPos = WorldToMap(obj.Position, mapX, mapY, mapW, mapH);
                mapPos.X = MathHelper.Clamp(mapPos.X, mapX + 4, mapX + mapW - 4);
                mapPos.Y = MathHelper.Clamp(mapPos.Y, mapY + 4, mapY + mapH - 4);

                float dist = Vector2.Distance(clickPos, mapPos);
                if (dist <= HitRadiusPx && dist < closestDist) {
                    closestDist = dist;
                    closest = obj;
                }
            }

            return closest;
        }

        /// <summary>
        /// Determine the color, label, and icon size based on what kind of object this is
        /// </summary>
        private void GetObjectStyle(SpaceObject obj, out Color color, out string label, out int iconSize) {
            string nameLower = obj.Name?.ToLowerInvariant() ?? "";

            if (obj is JumpHole) {
                color = Color.MediumPurple;
                label = obj.Name;
                iconSize = 8;
            } else if (obj is Station) {
                color = Color.LimeGreen;
                label = obj.Name;
                iconSize = 8;
            } else if (obj is Planet) {
                color = Color.CornflowerBlue;
                label = obj.Name;
                iconSize = 10;
            } else if (nameLower.Contains("sun")) {
                color = Color.Yellow;
                label = "Sun";
                iconSize = 14;
            } else if (nameLower.Contains("tradelane") || nameLower.Contains("trade lane")) {
                color = Color.Cyan;
                label = obj.Name;
                iconSize = 5;
            } else if (nameLower.Contains("station") || nameLower.Contains("base") || nameLower.Contains("platform")) {
                color = Color.LimeGreen;
                label = obj.Name;
                iconSize = 8;
            } else if (nameLower.Contains("jump") || nameLower.Contains("gate")) {
                color = Color.MediumPurple;
                label = obj.Name;
                iconSize = 8;
            } else {
                // Generic object (asteroid, etc.)
                color = Color.LightGray;
                label = obj.Name;
                iconSize = 5;
            }
        }

        /// <summary>
        /// Draw a styled icon for a space object
        /// </summary>
        private void DrawIcon(SpriteBatch spriteBatch, Vector2 pos, Color color, int size, SpaceObject obj) {
            if (obj is JumpHole) {
                // Diamond shape for jump holes
                DrawDiamond(spriteBatch, pos, size, color);
            } else if (obj.Name?.ToLowerInvariant().Contains("sun") == true) {
                // Sun: filled circle (large)
                DrawFilledCircle(spriteBatch, pos, size, Color.Yellow);
                // Glow
                float glow = (float)Math.Sin(_pulseTimer * 2.0) * 0.2f + 0.8f;
                DrawFilledCircle(spriteBatch, pos, size + 4, Color.Yellow * 0.3f * glow);
            } else if (obj is Planet) {
                // Circle for planets
                DrawFilledCircle(spriteBatch, pos, size, color);
            } else if (obj is Station) {
                // Square for stations
                DrawFilledSquare(spriteBatch, pos, size, color);
            } else {
                // Small square default
                DrawFilledSquare(spriteBatch, pos, size, color);
            }
        }

        /// <summary>
        /// Draw the player icon as a pulsing triangle
        /// </summary>
        private void DrawPlayerIcon(SpriteBatch spriteBatch, int mapX, int mapY, int mapW, int mapH) {
            Vector2 playerMapPos = WorldToMap(_playerPosition, mapX, mapY, mapW, mapH);

            playerMapPos.X = MathHelper.Clamp(playerMapPos.X, mapX + 4, mapX + mapW - 4);
            playerMapPos.Y = MathHelper.Clamp(playerMapPos.Y, mapY + 4, mapY + mapH - 4);

            // Blink effect
            float blink = (float)Math.Sin(_pulseTimer * 4.0) * 0.5f + 0.5f;
            Color playerColor = Color.Lerp(Color.White, Color.Lime, blink);

            // Draw small crosshair for player
            int size = 8;
            spriteBatch.Draw(_pixel, new Rectangle((int)playerMapPos.X - size, (int)playerMapPos.Y, size * 2 + 1, 2), playerColor);
            spriteBatch.Draw(_pixel, new Rectangle((int)playerMapPos.X, (int)playerMapPos.Y - size, 2, size * 2 + 1), playerColor);

            // Outer ring
            DrawCircleOutline(spriteBatch, playerMapPos, 10, playerColor * 0.6f);

            // Label
            if (_font != null) {
                string label = "YOU";
                Vector2 labelSize = _font.MeasureString(label);
                Vector2 labelPos = new Vector2(playerMapPos.X - labelSize.X / 2, playerMapPos.Y + 14);
                labelPos.X = MathHelper.Clamp(labelPos.X, mapX + 2, mapX + mapW - labelSize.X - 2);
                spriteBatch.DrawString(_font, label, labelPos + Vector2.One, Color.Black * 0.6f);
                spriteBatch.DrawString(_font, label, labelPos, playerColor);
            }
        }

        /// <summary>
        /// Draw all tradelane routes as dashed lines with ring dots along each path.
        /// </summary>
        private void DrawTradeLanes(SpriteBatch spriteBatch, int mapX, int mapY, int mapW, int mapH) {
            if (_tradeLanes == null || _tradeLanes.Count == 0) return;

            Color laneLineColor = new Color(0, 200, 220) * 0.5f; // dim cyan line
            Color ringDotColor = new Color(0, 220, 255);          // bright cyan dots
            Color endpointColor = Color.White;                     // white for start/end

            foreach (var lane in _tradeLanes) {
                if (lane.Rings.Count < 2) continue;

                // Draw connecting line from first ring to last ring
                Vector2 startMap = WorldToMap(lane.Rings[0].Position, mapX, mapY, mapW, mapH);
                Vector2 endMap = WorldToMap(lane.Rings[lane.Rings.Count - 1].Position, mapX, mapY, mapW, mapH);

                startMap.X = MathHelper.Clamp(startMap.X, mapX + 2, mapX + mapW - 2);
                startMap.Y = MathHelper.Clamp(startMap.Y, mapY + 2, mapY + mapH - 2);
                endMap.X = MathHelper.Clamp(endMap.X, mapX + 2, mapX + mapW - 2);
                endMap.Y = MathHelper.Clamp(endMap.Y, mapY + 2, mapY + mapH - 2);

                // Draw the lane line
                DrawMapLine(spriteBatch, startMap, endMap, laneLineColor);

                // Draw a small dot for each ring along the path
                for (int i = 0; i < lane.Rings.Count; i++) {
                    var ring = lane.Rings[i];
                    Vector2 ringMap = WorldToMap(ring.Position, mapX, mapY, mapW, mapH);
                    ringMap.X = MathHelper.Clamp(ringMap.X, mapX + 2, mapX + mapW - 2);
                    ringMap.Y = MathHelper.Clamp(ringMap.Y, mapY + 2, mapY + mapH - 2);

                    bool isEndpoint = (ring.Type == TradelaneRing.RingType.Start || ring.Type == TradelaneRing.RingType.End);
                    int dotSize = isEndpoint ? 5 : 3;
                    Color dotColor = isEndpoint ? endpointColor : ringDotColor;

                    spriteBatch.Draw(_pixel, new Rectangle(
                        (int)ringMap.X - dotSize / 2, (int)ringMap.Y - dotSize / 2,
                        dotSize, dotSize), dotColor);
                }

                // Label the lane name at the midpoint
                if (_font != null) {
                    Vector2 midMap = (startMap + endMap) / 2f;
                    // Offset label slightly so it doesn't sit right on the line
                    Vector2 lineDir = endMap - startMap;
                    Vector2 perp = new Vector2(-lineDir.Y, lineDir.X);
                    if (perp.LengthSquared() > 0) perp.Normalize();
                    Vector2 labelPos = midMap + perp * 12f;

                    string label = lane.Config.Name;
                    Vector2 labelSize = _font.MeasureString(label);
                    labelPos -= labelSize / 2f;
                    labelPos.X = MathHelper.Clamp(labelPos.X, mapX + 2, mapX + mapW - labelSize.X - 2);
                    labelPos.Y = MathHelper.Clamp(labelPos.Y, mapY + 2, mapY + mapH - labelSize.Y - 2);

                    spriteBatch.DrawString(_font, label, labelPos + Vector2.One, Color.Black * 0.6f);
                    spriteBatch.DrawString(_font, label, labelPos, new Color(0, 200, 220) * 0.8f);
                }
            }
        }

        // ---- Drawing primitives ----

        private void DrawRect(SpriteBatch sb, int x, int y, int w, int h, Color color) {
            int t = 2;
            sb.Draw(_pixel, new Rectangle(x, y, w, t), color);
            sb.Draw(_pixel, new Rectangle(x, y + h - t, w, t), color);
            sb.Draw(_pixel, new Rectangle(x, y, t, h), color);
            sb.Draw(_pixel, new Rectangle(x + w - t, y, t, h), color);
        }

        private void DrawGrid(SpriteBatch sb, int mapX, int mapY, int mapW, int mapH) {
            Color gridColor = new Color(30, 50, 70);
            int divisions = 8;
            for (int i = 1; i < divisions; i++) {
                int gx = mapX + (mapW * i / divisions);
                int gy = mapY + (mapH * i / divisions);
                sb.Draw(_pixel, new Rectangle(gx, mapY, 1, mapH), gridColor);
                sb.Draw(_pixel, new Rectangle(mapX, gy, mapW, 1), gridColor);
            }
        }

        private void DrawFilledSquare(SpriteBatch sb, Vector2 center, int halfSize, Color color) {
            sb.Draw(_pixel, new Rectangle(
                (int)center.X - halfSize, (int)center.Y - halfSize,
                halfSize * 2, halfSize * 2), color);
        }

        private void DrawFilledCircle(SpriteBatch sb, Vector2 center, int radius, Color color) {
            // Approximate circle with filled rectangles
            for (int y = -radius; y <= radius; y++) {
                int halfWidth = (int)Math.Sqrt(radius * radius - y * y);
                sb.Draw(_pixel, new Rectangle(
                    (int)center.X - halfWidth, (int)center.Y + y,
                    halfWidth * 2, 1), color);
            }
        }

        private void DrawCircleOutline(SpriteBatch sb, Vector2 center, int radius, Color color) {
            int segments = 24;
            for (int i = 0; i < segments; i++) {
                float angle1 = MathHelper.TwoPi * i / segments;
                float angle2 = MathHelper.TwoPi * (i + 1) / segments;
                Vector2 p1 = center + new Vector2((float)Math.Cos(angle1), (float)Math.Sin(angle1)) * radius;
                Vector2 p2 = center + new Vector2((float)Math.Cos(angle2), (float)Math.Sin(angle2)) * radius;

                // Draw line segment
                Vector2 edge = p2 - p1;
                float angle = (float)Math.Atan2(edge.Y, edge.X);
                float length = edge.Length();
                sb.Draw(_pixel,
                    new Rectangle((int)p1.X, (int)p1.Y, (int)length, 1),
                    null, color, angle, Vector2.Zero, SpriteEffects.None, 0);
            }
        }

        private void DrawDiamond(SpriteBatch sb, Vector2 center, int halfSize, Color color) {
            // Draw diamond as 4 lines
            Vector2 top = center + new Vector2(0, -halfSize);
            Vector2 right = center + new Vector2(halfSize, 0);
            Vector2 bottom = center + new Vector2(0, halfSize);
            Vector2 left = center + new Vector2(-halfSize, 0);

            DrawMapLine(sb, top, right, color);
            DrawMapLine(sb, right, bottom, color);
            DrawMapLine(sb, bottom, left, color);
            DrawMapLine(sb, left, top, color);

            // Fill center
            for (int y = -halfSize; y <= halfSize; y++) {
                int hw = halfSize - Math.Abs(y);
                if (hw > 0) {
                    sb.Draw(_pixel, new Rectangle(
                        (int)center.X - hw, (int)center.Y + y,
                        hw * 2, 1), color * 0.5f);
                }
            }
        }

        private void DrawMapLine(SpriteBatch sb, Vector2 start, Vector2 end, Color color) {
            Vector2 edge = end - start;
            float angle = (float)Math.Atan2(edge.Y, edge.X);
            float length = edge.Length();
            sb.Draw(_pixel,
                new Rectangle((int)start.X, (int)start.Y, (int)length, 2),
                null, color, angle, new Vector2(0, 0.5f), SpriteEffects.None, 0);
        }
    }
}
