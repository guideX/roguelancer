using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Linq;

namespace Roguelancer
{
    /// <summary>
    /// 2D HUD overlay for mission guidance: distance display, off-screen arrows, proximity alerts
    /// </summary>
    public class MissionGuidanceHUD
    {
        private SpriteFont _font;
        private Texture2D _pixel;
        private float _animationTime;

        public MissionGuidanceHUD(SpriteFont font, Texture2D pixel)
        {
            _font = font;
            _pixel = pixel;
        }

        public void Update(float deltaTime)
        {
            _animationTime += deltaTime;
        }

        /// <summary>
        /// Draw 2D HUD elements for all active mission targets
        /// </summary>
        public void Draw(SpriteBatch spriteBatch, GraphicsDevice graphicsDevice, Camera camera,
            Vector3 playerPosition, MissionWaypointSystem waypointSystem)
        {
            if (waypointSystem == null || _font == null) return;

            Viewport viewport = graphicsDevice.Viewport;
            DrawActiveMissionPanel(spriteBatch, viewport, waypointSystem);

            foreach (var kvp in waypointSystem.GuidanceData)
            {
                var data = kvp.Value;
                if (data.Mission.Status != MissionStatus.Active) continue;
                if (data.ResolvedTarget == null) continue;

                Vector3 targetPos = data.ResolvedTarget.Value;
                Color missionColor = GetMissionColor(data.Mission.Type);

                // Project target to screen
                Vector3 screenPos3D = viewport.Project(targetPos, camera.Projection, camera.View, Matrix.Identity);
                bool isOnScreen = screenPos3D.Z >= 0 && screenPos3D.Z <= 1 &&
                    screenPos3D.X >= 0 && screenPos3D.X <= viewport.Width &&
                    screenPos3D.Y >= 0 && screenPos3D.Y <= viewport.Height;

                if (isOnScreen)
                {
                    // Draw on-screen mission marker label and distance
                    DrawOnScreenMarker(spriteBatch, new Vector2(screenPos3D.X, screenPos3D.Y),
                        data, missionColor, viewport);
                }
                else
                {
                    // Draw off-screen directional arrow
                    DrawOffScreenArrow(spriteBatch, viewport, camera, targetPos, data, missionColor);
                }

                // Draw proximity alert when near target
                if (data.IsNearTarget)
                {
                    DrawProximityAlert(spriteBatch, viewport, data, missionColor);
                }

                // Draw compass arrow near top-center of screen pointing toward mission objective
                DrawCompassArrow(spriteBatch, viewport, camera, targetPos, data);
            }

            // Draw next waypoint indicator for the first active mission with waypoints
            foreach (var kvp in waypointSystem.GuidanceData)
            {
                var data = kvp.Value;
                if (data.Mission.Status != MissionStatus.Active) continue;
                if (data.Waypoints.Count == 0) continue;

                var nextWaypoint = data.Waypoints[0];
                float distToWaypoint = Vector3.Distance(playerPosition, nextWaypoint.Position);

                if (distToWaypoint > 200f)
                {
                    DrawNextWaypointIndicator(spriteBatch, viewport, camera, nextWaypoint, distToWaypoint);
                }
                break; // Only show for first active mission
            }
        }

        private void DrawActiveMissionPanel(SpriteBatch spriteBatch, Viewport viewport, MissionWaypointSystem waypointSystem)
        {
            var data = waypointSystem.GuidanceData.Values
                .FirstOrDefault(entry => entry?.Mission != null && entry.Mission.Status == MissionStatus.Active);

            if (data?.Mission == null)
            {
                return;
            }

            string title = "ACTIVE MISSION";
            string objective = data.Mission.GetObjectiveText();
            string targetLine = data.Mission.Type switch
            {
                MissionType.Bounty => $"Target: {data.Mission.GetTargetLabel()}",
                MissionType.Delivery => $"Destination: {data.Mission.GetDestinationLabel()}",
                MissionType.Escort => $"Route: {data.Mission.GetTargetLabel()} -> {data.Mission.GetDestinationLabel()}",
                _ => data.Mission.GetObjectiveText()
            };
            string sourceLine = $"Client: {data.Mission.GetClientLabel()} | Faction: {FactionManager.GetFactionDisplayName(data.Mission.FactionId)}";
            string rewardLine = $"Reward: {data.Mission.Reward:N0} CR | Risk: {data.Mission.GetRiskLabel()}";
            string distanceLine = data.ResolvedTarget != null
                ? $"Distance: {FormatDistance(data.DistanceToTarget)}"
                : data.Mission.GetHudFallbackLine();

            Vector2 titleSize = _font.MeasureString(title);
            Vector2 objectiveSize = _font.MeasureString(objective);
            Vector2 targetSize = _font.MeasureString(targetLine);
            Vector2 sourceSize = _font.MeasureString(sourceLine);
            Vector2 rewardSize = _font.MeasureString(rewardLine);
            Vector2 distanceSize = _font.MeasureString(distanceLine);

            float panelWidth = Math.Max(Math.Max(Math.Max(titleSize.X, objectiveSize.X), Math.Max(targetSize.X, sourceSize.X)), Math.Max(rewardSize.X, distanceSize.X)) + 28f;
            float panelHeight = titleSize.Y + objectiveSize.Y + targetSize.Y + sourceSize.Y + rewardSize.Y + distanceSize.Y + 24f;

            Rectangle panel = new Rectangle(18, 18, (int)panelWidth, (int)panelHeight);
            spriteBatch.Draw(_pixel, panel, Color.Black * 0.65f);
            spriteBatch.Draw(_pixel, new Rectangle(panel.X, panel.Y, panel.Width, 2), Color.LimeGreen * 0.85f);
            spriteBatch.Draw(_pixel, new Rectangle(panel.X, panel.Y + panel.Height - 2, panel.Width, 2), Color.LimeGreen * 0.45f);

            Vector2 cursor = new Vector2(panel.X + 12, panel.Y + 8);
            spriteBatch.DrawString(_font, title, cursor, Color.LimeGreen);
            cursor.Y += titleSize.Y + 2f;
            spriteBatch.DrawString(_font, objective, cursor, Color.White);
            cursor.Y += objectiveSize.Y + 2f;
            spriteBatch.DrawString(_font, targetLine, cursor, Color.LightGreen);
            cursor.Y += targetSize.Y + 2f;
            spriteBatch.DrawString(_font, sourceLine, cursor, Color.Cyan);
            cursor.Y += sourceSize.Y + 2f;
            spriteBatch.DrawString(_font, rewardLine, cursor, Color.Yellow);
            cursor.Y += rewardSize.Y + 2f;
            spriteBatch.DrawString(_font, distanceLine, cursor, data.ResolvedTarget != null ? Color.Lime : Color.Orange);
        }

        private void DrawOnScreenMarker(SpriteBatch spriteBatch, Vector2 screenPos,
            MissionGuidanceData data, Color color, Viewport viewport)
        {
            string distText = FormatDistance(data.DistanceToTarget);
            string typeTag = data.Mission.Type switch
            {
                MissionType.Delivery => "[DELIVER]",
                MissionType.Bounty => "[BOUNTY]",
                MissionType.Escort => "[ESCORT]",
                _ => "[MISSION]"
            };

            string label = data.Mission.Type == MissionType.Bounty
                ? data.Mission.Target
                : data.Mission.Destination;

            // Flashing green pulse
            float flash = (float)Math.Abs(Math.Sin(_animationTime * 5.0));
            Color greenFlash = Color.Lime * (0.6f + 0.4f * flash);

            // Draw flashing green bracket around target
            int bracketSize = 28 + (int)(6 * flash);
            int bracketThick = 2;
            Color bracketColor = greenFlash;

            // Top-left corner
            spriteBatch.Draw(_pixel, new Rectangle((int)screenPos.X - bracketSize, (int)screenPos.Y - bracketSize, 12, bracketThick), bracketColor);
            spriteBatch.Draw(_pixel, new Rectangle((int)screenPos.X - bracketSize, (int)screenPos.Y - bracketSize, bracketThick, 12), bracketColor);
            // Top-right corner
            spriteBatch.Draw(_pixel, new Rectangle((int)screenPos.X + bracketSize - 12, (int)screenPos.Y - bracketSize, 12, bracketThick), bracketColor);
            spriteBatch.Draw(_pixel, new Rectangle((int)screenPos.X + bracketSize, (int)screenPos.Y - bracketSize, bracketThick, 12), bracketColor);
            // Bottom-left corner
            spriteBatch.Draw(_pixel, new Rectangle((int)screenPos.X - bracketSize, (int)screenPos.Y + bracketSize, 12, bracketThick), bracketColor);
            spriteBatch.Draw(_pixel, new Rectangle((int)screenPos.X - bracketSize, (int)screenPos.Y + bracketSize - 12, bracketThick, 12), bracketColor);
            // Bottom-right corner
            spriteBatch.Draw(_pixel, new Rectangle((int)screenPos.X + bracketSize - 12, (int)screenPos.Y + bracketSize, 12, bracketThick), bracketColor);
            spriteBatch.Draw(_pixel, new Rectangle((int)screenPos.X + bracketSize, (int)screenPos.Y + bracketSize - 12, bracketThick, 12), bracketColor);

            // Draw mission type tag with green background
            Vector2 tagSize = _font.MeasureString(typeTag);
            Vector2 tagPos = new Vector2(screenPos.X - tagSize.X / 2, screenPos.Y - 55);

            // Background with green border
            spriteBatch.Draw(_pixel,
                new Rectangle((int)tagPos.X - 6, (int)tagPos.Y - 3, (int)tagSize.X + 12, (int)tagSize.Y + 6),
                Color.Black * 0.7f);
            spriteBatch.Draw(_pixel,
                new Rectangle((int)tagPos.X - 6, (int)tagPos.Y - 3, (int)tagSize.X + 12, 2),
                greenFlash);
            spriteBatch.Draw(_pixel,
                new Rectangle((int)tagPos.X - 6, (int)tagPos.Y + (int)tagSize.Y + 1, (int)tagSize.X + 12, 2),
                greenFlash);
            spriteBatch.DrawString(_font, typeTag, tagPos, greenFlash);

            // Draw distance below in bright green
            Vector2 distSize = _font.MeasureString(distText);
            Vector2 distPos = new Vector2(screenPos.X - distSize.X / 2, screenPos.Y - 30);
            spriteBatch.DrawString(_font, distText, distPos, Color.Lime * 0.95f);

            // Draw larger crosshair marker at screen position
            int markerSize = 10;
            Color markerColor = greenFlash;

            // Crosshair lines
            spriteBatch.Draw(_pixel,
                new Rectangle((int)screenPos.X - markerSize, (int)screenPos.Y - 1, markerSize * 2, 2),
                markerColor);
            spriteBatch.Draw(_pixel,
                new Rectangle((int)screenPos.X - 1, (int)screenPos.Y - markerSize, 2, markerSize * 2),
                markerColor);
        }

        private void DrawOffScreenArrow(SpriteBatch spriteBatch, Viewport viewport, Camera camera,
            Vector3 targetWorldPos, MissionGuidanceData data, Color color)
        {
            // Calculate direction from camera to target in camera space
            Vector3 targetDir = targetWorldPos - camera.Position;
            targetDir.Normalize();

            Matrix viewMatrix = camera.View;
            Vector3 targetCameraSpace = Vector3.TransformNormal(targetDir, viewMatrix);

            Vector2 screenDir = new Vector2(targetCameraSpace.X, -targetCameraSpace.Y);
            if (targetCameraSpace.Z > 0) screenDir = -screenDir;

            if (screenDir.LengthSquared() > 0.0001f)
                screenDir.Normalize();
            else
                screenDir = Vector2.UnitX;

            int edgeMargin = 80;
            Vector2 screenCenter = new Vector2(viewport.Width / 2f, viewport.Height / 2f);
            float halfWidth = viewport.Width / 2f - edgeMargin;
            float halfHeight = viewport.Height / 2f - edgeMargin;

            float tx = (screenDir.X != 0) ? halfWidth / Math.Abs(screenDir.X) : float.MaxValue;
            float ty = (screenDir.Y != 0) ? halfHeight / Math.Abs(screenDir.Y) : float.MaxValue;
            float t = Math.Min(tx, ty);

            Vector2 arrowPos = screenCenter + screenDir * t;

            // Flashing green for high visibility
            float flash = (float)Math.Abs(Math.Sin(_animationTime * 5.0));
            Color arrowColor = Color.Lime * (0.6f + 0.4f * flash);

            // Draw larger arrow triangle
            float arrowAngle = (float)Math.Atan2(screenDir.Y, screenDir.X);
            Vector2 arrowDir = new Vector2((float)Math.Cos(arrowAngle), (float)Math.Sin(arrowAngle));
            Vector2 arrowPerp = new Vector2(-arrowDir.Y, arrowDir.X);

            Vector2 tip = arrowPos + arrowDir * 30f;
            Vector2 left = arrowPos - arrowDir * 15f + arrowPerp * 18f;
            Vector2 right = arrowPos - arrowDir * 15f - arrowPerp * 18f;

            // Filled arrow using thicker lines
            DrawLine(spriteBatch, tip, left, 4, arrowColor);
            DrawLine(spriteBatch, tip, right, 4, arrowColor);
            DrawLine(spriteBatch, left, right, 4, arrowColor);

            // Second smaller inner arrow for motion effect
            float innerOffset = 8f + 6f * flash;
            Vector2 tip2 = arrowPos + arrowDir * (30f - innerOffset);
            Vector2 left2 = arrowPos - arrowDir * (15f - innerOffset * 0.5f) + arrowPerp * 12f;
            Vector2 right2 = arrowPos - arrowDir * (15f - innerOffset * 0.5f) - arrowPerp * 12f;
            DrawLine(spriteBatch, tip2, left2, 2, arrowColor * 0.6f);
            DrawLine(spriteBatch, tip2, right2, 2, arrowColor * 0.6f);

            // Distance text near arrow - larger and green
            string distText = FormatDistance(data.DistanceToTarget);
            Vector2 distSize = _font.MeasureString(distText);
            Vector2 textPos = arrowPos - new Vector2(distSize.X / 2, 30);
            textPos.X = MathHelper.Clamp(textPos.X, 5, viewport.Width - distSize.X - 5);
            textPos.Y = MathHelper.Clamp(textPos.Y, 5, viewport.Height - distSize.Y - 5);

            spriteBatch.Draw(_pixel,
                new Rectangle((int)textPos.X - 4, (int)textPos.Y - 2, (int)distSize.X + 8, (int)distSize.Y + 4),
                Color.Black * 0.6f);
            spriteBatch.DrawString(_font, distText, textPos, Color.Lime * 0.95f);

            // Mission type indicator
            string typeChar = data.Mission.Type switch
            {
                MissionType.Delivery => "D",
                MissionType.Bounty => "B",
                MissionType.Escort => "E",
                _ => "M"
            };
            Vector2 typeSize = _font.MeasureString(typeChar);
            Vector2 typePos = arrowPos - new Vector2(typeSize.X / 2, typeSize.Y / 2);
            spriteBatch.DrawString(_font, typeChar, typePos, arrowColor);
        }

        private void DrawProximityAlert(SpriteBatch spriteBatch, Viewport viewport,
            MissionGuidanceData data, Color color)
        {
            float flash = (float)Math.Abs(Math.Sin(_animationTime * 6.0));
            Color alertColor = Color.Lime * (0.6f + 0.4f * flash);

            string alertText = data.Mission.Type switch
            {
                MissionType.Delivery => ">> APPROACHING DESTINATION <<",
                MissionType.Bounty => ">> TARGET NEARBY <<",
                MissionType.Escort => ">> NEAR ESCORT WAYPOINT <<",
                _ => ">> NEAR OBJECTIVE <<"
            };

            string distText = FormatDistance(data.DistanceToTarget);
            alertText += $" - {distText}";

            Vector2 textSize = _font.MeasureString(alertText);
            Vector2 pos = new Vector2(
                (viewport.Width - textSize.X) / 2,
                viewport.Height * 0.12f);

            // Flashing green background panel
            spriteBatch.Draw(_pixel,
                new Rectangle((int)pos.X - 14, (int)pos.Y - 6, (int)textSize.X + 28, (int)textSize.Y + 12),
                Color.Black * 0.7f);
            // Green border top
            spriteBatch.Draw(_pixel,
                new Rectangle((int)pos.X - 14, (int)pos.Y - 6, (int)textSize.X + 28, 3),
                alertColor);
            // Green border bottom
            spriteBatch.Draw(_pixel,
                new Rectangle((int)pos.X - 14, (int)pos.Y + (int)textSize.Y + 4, (int)textSize.X + 28, 3),
                alertColor);
            // Green border left
            spriteBatch.Draw(_pixel,
                new Rectangle((int)pos.X - 14, (int)pos.Y - 6, 3, (int)textSize.Y + 15),
                alertColor);
            // Green border right
            spriteBatch.Draw(_pixel,
                new Rectangle((int)pos.X + (int)textSize.X + 12, (int)pos.Y - 6, 3, (int)textSize.Y + 15),
                alertColor);

            spriteBatch.DrawString(_font, alertText, pos, alertColor);
        }

        private void DrawNextWaypointIndicator(SpriteBatch spriteBatch, Viewport viewport,
            Camera camera, MissionWaypoint waypoint, float distance)
        {
            Vector3 screenPos3D = viewport.Project(waypoint.Position, camera.Projection, camera.View, Matrix.Identity);

            if (screenPos3D.Z < 0 || screenPos3D.Z > 1) return;

            bool isOnScreen = screenPos3D.X >= 0 && screenPos3D.X <= viewport.Width &&
                              screenPos3D.Y >= 0 && screenPos3D.Y <= viewport.Height;

            if (!isOnScreen) return;

            Vector2 screenPos = new Vector2(screenPos3D.X, screenPos3D.Y);

            Color wpColor = waypoint.Type switch
            {
                MissionWaypointType.TradelaneEntry => Color.Cyan * 0.8f,
                MissionWaypointType.TradelaneExit => Color.Cyan * 0.6f,
                _ => Color.Lime * 0.7f
            };

            // Draw small waypoint label
            string wpLabel = waypoint.Label;
            if (wpLabel.Length > 25) wpLabel = wpLabel.Substring(0, 22) + "...";
            string wpDist = FormatDistance(distance);
            string text = $"{wpLabel} ({wpDist})";

            Vector2 textSize = _font.MeasureString(text);
            Vector2 textPos = new Vector2(screenPos.X - textSize.X / 2, screenPos.Y + 15);

            spriteBatch.Draw(_pixel,
                new Rectangle((int)textPos.X - 3, (int)textPos.Y - 1, (int)textSize.X + 6, (int)textSize.Y + 2),
                Color.Black * 0.4f);
            spriteBatch.DrawString(_font, text, textPos, wpColor);
        }

        private void DrawLine(SpriteBatch spriteBatch, Vector2 start, Vector2 end, int thickness, Color color)
        {
            Vector2 edge = end - start;
            float angle = (float)Math.Atan2(edge.Y, edge.X);
            float length = edge.Length();

            spriteBatch.Draw(_pixel,
                new Rectangle((int)start.X, (int)start.Y, (int)length, thickness),
                null, color, angle, new Vector2(0, 0.5f), SpriteEffects.None, 0);
        }

        private string FormatDistance(float distance)
        {
            if (distance >= 1000f)
                return $"{distance / 1000f:F1}km";
            return $"{distance:F0}m";
        }

        private Color GetMissionColor(MissionType type)
        {
            return type switch
            {
                MissionType.Delivery => new Color(80, 200, 255),
                MissionType.Bounty => new Color(255, 80, 80),
                MissionType.Escort => new Color(255, 200, 50),
                _ => Color.White
            };
        }

        /// <summary>
        /// Draw a persistent compass arrow near the top-center of the screen that always points
        /// toward the active mission objective, with distance and label
        /// </summary>
        private void DrawCompassArrow(SpriteBatch spriteBatch, Viewport viewport, Camera camera,
            Vector3 targetWorldPos, MissionGuidanceData data)
        {
            // Calculate direction from camera to target in camera space
            Vector3 targetDir = targetWorldPos - camera.Position;
            if (targetDir.LengthSquared() < 0.001f) return;
            targetDir.Normalize();

            Matrix viewMatrix = camera.View;
            Vector3 camSpace = Vector3.TransformNormal(targetDir, viewMatrix);

            // 2D direction on screen (X = right, Y = up in screen coords)
            Vector2 dir2D = new Vector2(camSpace.X, -camSpace.Y);
            if (camSpace.Z > 0) dir2D = -dir2D; // Target is behind camera
            if (dir2D.LengthSquared() < 0.0001f) dir2D = Vector2.UnitY;
            dir2D.Normalize();

            // Position: top-center area of screen
            float compassY = 55f;
            Vector2 compassCenter = new Vector2(viewport.Width / 2f, compassY);

            float flash = (float)Math.Abs(Math.Sin(_animationTime * 4.5));
            Color arrowColor = Color.Lime * (0.7f + 0.3f * flash);

            // Draw compass arrow pointing toward target
            float arrowLen = 22f;
            float arrowWidth = 14f;
            Vector2 tip = compassCenter + dir2D * arrowLen;
            Vector2 perp = new Vector2(-dir2D.Y, dir2D.X);
            Vector2 left = compassCenter - dir2D * 8f + perp * arrowWidth * 0.5f;
            Vector2 right = compassCenter - dir2D * 8f - perp * arrowWidth * 0.5f;

            DrawLine(spriteBatch, tip, left, 3, arrowColor);
            DrawLine(spriteBatch, tip, right, 3, arrowColor);
            DrawLine(spriteBatch, left, right, 2, arrowColor * 0.7f);

            // Draw label below compass
            string label = data.Mission.Type == MissionType.Bounty
                ? data.Mission.GetTargetLabel()
                : data.Mission.GetDestinationLabel();
            if (label.Length > 20) label = label.Substring(0, 17) + "...";
            string distStr = FormatDistance(data.DistanceToTarget);
            string compassText = $"{data.Mission.GetTypeLabel()} {label} - {distStr}";

            Vector2 textSize = _font.MeasureString(compassText);
            Vector2 textPos = new Vector2(viewport.Width / 2f - textSize.X / 2f, compassY + 28f);

            spriteBatch.Draw(_pixel,
                new Rectangle((int)textPos.X - 5, (int)textPos.Y - 2, (int)textSize.X + 10, (int)textSize.Y + 4),
                Color.Black * 0.6f);
            spriteBatch.DrawString(_font, compassText, textPos, arrowColor * 0.9f);
        }
    }
}
