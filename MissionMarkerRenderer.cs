using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;

namespace Roguelancer
{
    /// <summary>
    /// Renders 3D mission markers (beacons), waypoint path arrows, and proximity highlight halos
    /// </summary>
    public class MissionMarkerRenderer
    {
        private GraphicsDevice _graphicsDevice;
        private BasicEffect _effect;
        private float _animationTime;

        // Beacon geometry
        private const int DiamondSegments = 8;

        public MissionMarkerRenderer(GraphicsDevice graphicsDevice)
        {
            _graphicsDevice = graphicsDevice;
            _effect = new BasicEffect(graphicsDevice)
            {
                VertexColorEnabled = true,
                LightingEnabled = false
            };
        }

        /// <summary>
        /// Update animation timer
        /// </summary>
        public void Update(float deltaTime)
        {
            _animationTime += deltaTime;
        }

        /// <summary>
        /// Draw all 3D mission markers, path arrows, and proximity halos
        /// </summary>
        public void Draw3D(Matrix view, Matrix projection, Vector3 playerPosition, MissionWaypointSystem waypointSystem)
        {
            if (waypointSystem == null) return;

            _effect.View = view;
            _effect.Projection = projection;

            var oldBlend = _graphicsDevice.BlendState;
            var oldDepth = _graphicsDevice.DepthStencilState;

            _graphicsDevice.BlendState = BlendState.Additive;
            _graphicsDevice.DepthStencilState = DepthStencilState.DepthRead;

            foreach (var kvp in waypointSystem.GuidanceData)
            {
                var data = kvp.Value;
                if (data.Mission.Status != MissionStatus.Active) continue;
                if (data.ResolvedTarget == null) continue;

                Color missionColor = GetMissionColor(data.Mission.Type);

                // Draw waypoint path arrows
                DrawWaypointPath(playerPosition, data, missionColor);

                // Draw beacon at final target
                DrawBeacon(data.ResolvedTarget.Value, missionColor, data.DistanceToTarget);

                // Draw proximity halo when near
                if (data.IsNearTarget)
                {
                    DrawProximityHalo(data.ResolvedTarget.Value, missionColor, data.DistanceToTarget);
                }
            }

            _graphicsDevice.BlendState = oldBlend;
            _graphicsDevice.DepthStencilState = oldDepth;
        }

        /// <summary>
        /// Draw a pulsing 3D diamond beacon at the mission target, visible from far away
        /// </summary>
        private void DrawBeacon(Vector3 position, Color color, float distanceToPlayer)
        {
            // Scale beacon size based on distance (bigger when far away for visibility)
            float baseSize = 60f;
            float distanceScale = MathHelper.Clamp(distanceToPlayer / 4000f, 0.6f, 5f);
            float pulse = 0.7f + 0.3f * (float)Math.Sin(_animationTime * 4.0);
            float size = baseSize * distanceScale * pulse;

            // Rotate the beacon
            float rotation = _animationTime * 2.0f;

            // Build diamond shape (two pyramids joined at base)
            var vertices = new List<VertexPositionColor>();
            Color beaconColor = color * (0.8f + 0.2f * pulse);

            Vector3 top = position + Vector3.Up * size * 2.5f;
            Vector3 bottom = position - Vector3.Up * size * 2.5f;

            for (int i = 0; i < DiamondSegments; i++)
            {
                float angle1 = rotation + (float)i / DiamondSegments * MathHelper.TwoPi;
                float angle2 = rotation + (float)(i + 1) / DiamondSegments * MathHelper.TwoPi;

                Vector3 p1 = position + new Vector3(
                    (float)Math.Cos(angle1) * size,
                    0,
                    (float)Math.Sin(angle1) * size);

                Vector3 p2 = position + new Vector3(
                    (float)Math.Cos(angle2) * size,
                    0,
                    (float)Math.Sin(angle2) * size);

                // Top pyramid
                vertices.Add(new VertexPositionColor(top, beaconColor));
                vertices.Add(new VertexPositionColor(p1, beaconColor * 0.6f));
                vertices.Add(new VertexPositionColor(p2, beaconColor * 0.6f));

                // Bottom pyramid
                vertices.Add(new VertexPositionColor(bottom, beaconColor));
                vertices.Add(new VertexPositionColor(p2, beaconColor * 0.6f));
                vertices.Add(new VertexPositionColor(p1, beaconColor * 0.6f));
            }

            if (vertices.Count >= 3)
            {
                _effect.World = Matrix.Identity;
                foreach (var pass in _effect.CurrentTechnique.Passes)
                {
                    pass.Apply();
                    _graphicsDevice.DrawUserPrimitives(
                        PrimitiveType.TriangleList,
                        vertices.ToArray(), 0, vertices.Count / 3);
                }
            }

            // Draw vertical beam line extending up and down
            float beamHeight = size * 8f;
            Color beamColor = color * (0.4f + 0.2f * pulse);
            var beamVertices = new VertexPositionColor[]
            {
                new(position + Vector3.Up * beamHeight, beamColor * 0.15f),
                new(position, beamColor),
                new(position, beamColor),
                new(position - Vector3.Up * beamHeight, beamColor * 0.15f)
            };

            _effect.World = Matrix.Identity;
            foreach (var pass in _effect.CurrentTechnique.Passes)
            {
                pass.Apply();
                _graphicsDevice.DrawUserPrimitives(PrimitiveType.LineList, beamVertices, 0, 2);
            }

            // Draw flashing green outer rings that pulse outward
            DrawFlashingRings(position, size, distanceToPlayer);
        }

        /// <summary>
        /// Draw flashing green rings around the beacon to make mission targets unmissable
        /// </summary>
        private void DrawFlashingRings(Vector3 position, float beaconSize, float distanceToPlayer)
        {
            int ringCount = 3;
            int segments = 32;
            float flashRate = 5.0f;
            float ringScale = MathHelper.Clamp(distanceToPlayer / 3000f, 1f, 4f);

            for (int r = 0; r < ringCount; r++)
            {
                float phase = _animationTime * flashRate + r * MathHelper.TwoPi / ringCount;
                float expand = (float)((Math.Sin(phase) + 1.0) * 0.5); // 0..1 pulsing
                float ringRadius = beaconSize * (1.5f + expand * 2.0f) * ringScale;
                float alpha = 0.4f + 0.6f * (1f - expand); // Brighter when small
                float flash = (float)Math.Abs(Math.Sin(_animationTime * 6.0 + r * 1.2));
                Color ringColor = Color.Lime * alpha * (0.5f + 0.5f * flash);

                var ringVerts = new VertexPositionColor[segments * 2];
                for (int i = 0; i < segments; i++)
                {
                    float a1 = (float)i / segments * MathHelper.TwoPi;
                    float a2 = (float)(i + 1) / segments * MathHelper.TwoPi;

                    ringVerts[i * 2] = new VertexPositionColor(
                        position + new Vector3((float)Math.Cos(a1) * ringRadius, 0, (float)Math.Sin(a1) * ringRadius),
                        ringColor);
                    ringVerts[i * 2 + 1] = new VertexPositionColor(
                        position + new Vector3((float)Math.Cos(a2) * ringRadius, 0, (float)Math.Sin(a2) * ringRadius),
                        ringColor);
                }

                _effect.World = Matrix.Identity;
                foreach (var pass in _effect.CurrentTechnique.Passes)
                {
                    pass.Apply();
                    _graphicsDevice.DrawUserPrimitives(PrimitiveType.LineList, ringVerts, 0, segments);
                }

                // Draw a second ring tilted vertically for 3D visibility
                for (int i = 0; i < segments; i++)
                {
                    float a1 = (float)i / segments * MathHelper.TwoPi;
                    float a2 = (float)(i + 1) / segments * MathHelper.TwoPi;

                    ringVerts[i * 2] = new VertexPositionColor(
                        position + new Vector3((float)Math.Cos(a1) * ringRadius, (float)Math.Sin(a1) * ringRadius, 0),
                        ringColor * 0.7f);
                    ringVerts[i * 2 + 1] = new VertexPositionColor(
                        position + new Vector3((float)Math.Cos(a2) * ringRadius, (float)Math.Sin(a2) * ringRadius, 0),
                        ringColor * 0.7f);
                }

                foreach (var pass in _effect.CurrentTechnique.Passes)
                {
                    pass.Apply();
                    _graphicsDevice.DrawUserPrimitives(PrimitiveType.LineList, ringVerts, 0, segments);
                }
            }
        }

        /// <summary>
        /// Draw green arrow segments along the waypoint path
        /// </summary>
        private void DrawWaypointPath(Vector3 playerPos, MissionGuidanceData data, Color missionColor)
        {
            if (data.Waypoints.Count == 0) return;

            Color pathColor = Color.Lime * 0.9f;
            float arrowSpacing = 250f;
            float arrowSize = 30f;

            // Build path segments: player -> waypoint1 -> waypoint2 -> ... -> target
            Vector3 prevPoint = playerPos;
            foreach (var waypoint in data.Waypoints)
            {
                Vector3 nextPoint = waypoint.Position;
                float segmentLength = Vector3.Distance(prevPoint, nextPoint);

                if (segmentLength < 50f)
                {
                    prevPoint = nextPoint;
                    continue;
                }

                Vector3 direction = Vector3.Normalize(nextPoint - prevPoint);

                // Color tradelane segments differently - all bright for visibility
                float segFlash = 0.7f + 0.3f * (float)Math.Sin(_animationTime * 5.0);
                Color segColor = waypoint.Type switch
                {
                    MissionWaypointType.TradelaneEntry => Color.Cyan * 0.8f * segFlash,
                    MissionWaypointType.TradelaneExit => Color.Cyan * 0.7f * segFlash,
                    MissionWaypointType.Destination => Color.Lime * 0.9f * segFlash,
                    _ => pathColor * segFlash
                };

                // Draw arrows along this segment - more arrows, closer together
                int arrowCount = Math.Max(2, (int)(segmentLength / arrowSpacing));
                arrowCount = Math.Min(arrowCount, 40); // Allow more arrows for long paths

                for (int i = 1; i <= arrowCount; i++)
                {
                    float t = (float)i / (arrowCount + 1);
                    // Add animation offset so arrows appear to move along path
                    float animOffset = (_animationTime * 0.3f) % 1.0f;
                    float adjustedT = (t + animOffset / (arrowCount + 1)) % 1.0f;
                    if (adjustedT <= 0.01f || adjustedT >= 0.99f) continue;

                    Vector3 arrowPos = Vector3.Lerp(prevPoint, nextPoint, adjustedT);
                    DrawArrow3D(arrowPos, direction, arrowSize, segColor);
                }

                prevPoint = nextPoint;
            }
        }

        /// <summary>
        /// Draw a single 3D arrow (triangle) pointing in a direction
        /// </summary>
        private void DrawArrow3D(Vector3 position, Vector3 direction, float size, Color color)
        {
            // Build a flat triangle arrow pointing in direction
            Vector3 right = Vector3.Cross(direction, Vector3.Up);
            if (right.LengthSquared() < 0.001f)
                right = Vector3.Cross(direction, Vector3.Right);
            right = Vector3.Normalize(right) * size * 0.5f;

            Vector3 tip = position + direction * size;
            Vector3 leftWing = position - right;
            Vector3 rightWing = position + right;

            var vertices = new VertexPositionColor[]
            {
                new(tip, color),
                new(leftWing, color * 0.3f),
                new(rightWing, color * 0.3f)
            };

            _effect.World = Matrix.Identity;
            foreach (var pass in _effect.CurrentTechnique.Passes)
            {
                pass.Apply();
                _graphicsDevice.DrawUserPrimitives(PrimitiveType.TriangleList, vertices, 0, 1);
            }
        }

        /// <summary>
        /// Draw a pulsing ring halo around objects when the player is nearby
        /// </summary>
        private void DrawProximityHalo(Vector3 position, Color color, float distance)
        {
            // Halo gets brighter and tighter as player approaches - flash green!
            float intensity = MathHelper.Clamp(1f - distance / 2000f, 0.2f, 1f);
            float flash = (float)Math.Abs(Math.Sin(_animationTime * 6.0));
            float haloSize = 120f + 40f * (float)Math.Sin(_animationTime * 3.0);
            Color haloColor = Color.Lime * intensity * (0.6f + 0.4f * flash);

            int segments = 24;
            var vertices = new VertexPositionColor[segments * 2];

            for (int i = 0; i < segments; i++)
            {
                float angle1 = (float)i / segments * MathHelper.TwoPi;
                float angle2 = (float)(i + 1) / segments * MathHelper.TwoPi;

                vertices[i * 2] = new VertexPositionColor(
                    position + new Vector3((float)Math.Cos(angle1) * haloSize, 0, (float)Math.Sin(angle1) * haloSize),
                    haloColor);
                vertices[i * 2 + 1] = new VertexPositionColor(
                    position + new Vector3((float)Math.Cos(angle2) * haloSize, 0, (float)Math.Sin(angle2) * haloSize),
                    haloColor);
            }

            _effect.World = Matrix.Identity;
            foreach (var pass in _effect.CurrentTechnique.Passes)
            {
                pass.Apply();
                _graphicsDevice.DrawUserPrimitives(PrimitiveType.LineList, vertices, 0, segments);
            }

            // Draw a second ring rotated 90 degrees (vertical ring)
            for (int i = 0; i < segments; i++)
            {
                float angle1 = (float)i / segments * MathHelper.TwoPi;
                float angle2 = (float)(i + 1) / segments * MathHelper.TwoPi;

                vertices[i * 2] = new VertexPositionColor(
                    position + new Vector3((float)Math.Cos(angle1) * haloSize, (float)Math.Sin(angle1) * haloSize, 0),
                    haloColor * 0.7f);
                vertices[i * 2 + 1] = new VertexPositionColor(
                    position + new Vector3((float)Math.Cos(angle2) * haloSize, (float)Math.Sin(angle2) * haloSize, 0),
                    haloColor * 0.7f);
            }

            foreach (var pass in _effect.CurrentTechnique.Passes)
            {
                pass.Apply();
                _graphicsDevice.DrawUserPrimitives(PrimitiveType.LineList, vertices, 0, segments);
            }
        }

        /// <summary>
        /// Get color associated with mission type
        /// </summary>
        private Color GetMissionColor(MissionType type)
        {
            return type switch
            {
                MissionType.Delivery => new Color(80, 200, 255),  // Cyan-blue
                MissionType.Bounty => new Color(255, 80, 80),     // Red
                MissionType.Escort => new Color(255, 200, 50),    // Yellow-gold
                _ => Color.White
            };
        }
    }
}
