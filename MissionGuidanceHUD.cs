using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;

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

            // Draw mission type tag
            Vector2 tagSize = _font.MeasureString(typeTag);
            Vector2 tagPos = new Vector2(screenPos.X - tagSize.X / 2, screenPos.Y - 50);

            // Background
            spriteBatch.Draw(_pixel,
                new Rectangle((int)tagPos.X - 4, (int)tagPos.Y - 2, (int)tagSize.X + 8, (int)tagSize.Y + 4),
                Color.Black * 0.6f);
            spriteBatch.DrawString(_font, typeTag, tagPos, color);

            // Draw distance below
            Vector2 distSize = _font.MeasureString(distText);
            Vector2 distPos = new Vector2(screenPos.X - distSize.X / 2, screenPos.Y - 28);
            spriteBatch.DrawString(_font, distText, distPos, Color.White * 0.9f);

            // Draw small diamond marker at screen position
            int markerSize = 6;
            float pulse = 0.7f + 0.3f * (float)Math.Sin(_animationTime * 4.0);
            Color markerColor = color * pulse;

            // Diamond shape using rectangles rotated 45 degrees
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

            int edgeMargin = 60;
            Vector2 screenCenter = new Vector2(viewport.Width / 2f, viewport.Height / 2f);
            float halfWidth = viewport.Width / 2f - edgeMargin;
            float halfHeight = viewport.Height / 2f - edgeMargin;

            float tx = (screenDir.X != 0) ? halfWidth / Math.Abs(screenDir.X) : float.MaxValue;
            float ty = (screenDir.Y != 0) ? halfHeight / Math.Abs(screenDir.Y) : float.MaxValue;
            float t = Math.Min(tx, ty);

            Vector2 arrowPos = screenCenter + screenDir * t;

            float pulse = 0.6f + 0.4f * (float)Math.Sin(_animationTime * 3.0);
            Color arrowColor = color * pulse;

            // Draw arrow triangle
            float arrowAngle = (float)Math.Atan2(screenDir.Y, screenDir.X);
            Vector2 arrowDir = new Vector2((float)Math.Cos(arrowAngle), (float)Math.Sin(arrowAngle));
            Vector2 arrowPerp = new Vector2(-arrowDir.Y, arrowDir.X);

            Vector2 tip = arrowPos + arrowDir * 20f;
            Vector2 left = arrowPos - arrowDir * 10f + arrowPerp * 12f;
            Vector2 right = arrowPos - arrowDir * 10f - arrowPerp * 12f;

            // Simple filled arrow using lines
            DrawLine(spriteBatch, tip, left, 3, arrowColor);
            DrawLine(spriteBatch, tip, right, 3, arrowColor);
            DrawLine(spriteBatch, left, right, 3, arrowColor);

            // Distance text near arrow
            string distText = FormatDistance(data.DistanceToTarget);
            Vector2 distSize = _font.MeasureString(distText);
            Vector2 textPos = arrowPos - new Vector2(distSize.X / 2, 25);
            textPos.X = MathHelper.Clamp(textPos.X, 5, viewport.Width - distSize.X - 5);
            textPos.Y = MathHelper.Clamp(textPos.Y, 5, viewport.Height - distSize.Y - 5);

            spriteBatch.Draw(_pixel,
                new Rectangle((int)textPos.X - 3, (int)textPos.Y - 1, (int)distSize.X + 6, (int)distSize.Y + 2),
                Color.Black * 0.5f);
            spriteBatch.DrawString(_font, distText, textPos, arrowColor * 0.9f);

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
            float pulse = 0.5f + 0.5f * (float)Math.Sin(_animationTime * 5.0);
            Color alertColor = color * pulse;

            string alertText = data.Mission.Type switch
            {
                MissionType.Delivery => "APPROACHING DESTINATION",
                MissionType.Bounty => "TARGET NEARBY",
                MissionType.Escort => "NEAR ESCORT WAYPOINT",
                _ => "NEAR OBJECTIVE"
            };

            string distText = FormatDistance(data.DistanceToTarget);
            alertText += $" - {distText}";

            Vector2 textSize = _font.MeasureString(alertText);
            Vector2 pos = new Vector2(
                (viewport.Width - textSize.X) / 2,
                viewport.Height * 0.15f);

            // Background
            spriteBatch.Draw(_pixel,
                new Rectangle((int)pos.X - 10, (int)pos.Y - 5, (int)textSize.X + 20, (int)textSize.Y + 10),
                Color.Black * 0.6f * pulse);
            spriteBatch.Draw(_pixel,
                new Rectangle((int)pos.X - 10, (int)pos.Y - 5, (int)textSize.X + 20, 2),
                alertColor);
            spriteBatch.Draw(_pixel,
                new Rectangle((int)pos.X - 10, (int)pos.Y + (int)textSize.Y + 3, (int)textSize.X + 20, 2),
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
    }
}
