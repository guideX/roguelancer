using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Roguelancer.Configuration;
using System;
using System.Collections.Generic;

namespace Roguelancer {
    /// <summary>
    /// A tradelane consisting of paired ring stacks between two endpoints.
    /// At each position, two rings are stacked vertically:
    ///   - Top ring: for forward travel (start -> end)
    ///   - Bottom ring: for reverse travel (end -> start)
    /// Players dock at one end and are accelerated through the lane at high speed.
    /// If a ring is destroyed, the lane breaks and the ship is ejected.
    /// </summary>
    public class TradeLane {
        private GraphicsDevice _graphicsDevice;
        private TradelaneConfig _config;

        /// <summary>
        /// All rings (both forward and reverse) for model assignment and rendering
        /// </summary>
        public List<TradelaneRing> Rings { get; } = new();

        /// <summary>
        /// Forward (top) rings in order from start to end, used for start->end travel
        /// </summary>
        public List<TradelaneRing> ForwardRings { get; } = new();

        /// <summary>
        /// Reverse (bottom) rings in order from start to end, used for end->start travel
        /// </summary>
        public List<TradelaneRing> ReverseRings { get; } = new();

        /// <summary>
        /// The configuration for this tradelane
        /// </summary>
        public TradelaneConfig Config => _config;

        /// <summary>
        /// Whether a ship is currently traveling in this lane
        /// </summary>
        public bool IsActive { get; private set; }

        /// <summary>
        /// Whether the lane is broken (a ring is destroyed)
        /// </summary>
        public bool IsBroken { get; private set; }

        /// <summary>
        /// Current ring index the ship is at (or heading toward) within the active ring list
        /// </summary>
        public int CurrentRingIndex { get; private set; }

        /// <summary>
        /// Direction of travel: +1 = start to end (forward rings), -1 = end to start (reverse rings)
        /// </summary>
        public int TravelDirection { get; private set; }

        /// <summary>
        /// Normalized lane axis vector (start -> end)
        /// </summary>
        public Vector3 LaneDirection { get; private set; }

        /// <summary>
        /// The ring the player is currently near (for prompt display)
        /// </summary>
        public TradelaneRing NearbyEntryRing { get; private set; }

        // Transit interpolation state
        private Vector3 _transitStartPos;
        private Vector3 _transitEndPos;
        private float _transitProgress;
        private float _transitDuration;

        // Pulse cascade
        private int _lastPulsedRingIndex = -1;

        // The active ring list during transit (forward or reverse)
        private List<TradelaneRing> _activeRings;

        public TradeLane(GraphicsDevice graphicsDevice, TradelaneConfig config) {
            _graphicsDevice = graphicsDevice;
            _config = config;

            LaneDirection = Vector3.Normalize(config.EndPosition - config.StartPosition);
            float totalDistance = Vector3.Distance(config.StartPosition, config.EndPosition);
            int ringCount = Math.Max(2, (int)(totalDistance / config.RingSpacing) + 1);

            // Compute the vertical offset direction (perpendicular to lane, along world up)
            Vector3 upDir = Vector3.Up;
            if (Math.Abs(Vector3.Dot(LaneDirection, Vector3.Up)) > 0.99f)
                upDir = Vector3.Forward;
            float halfOffset = config.RingVerticalOffset * 0.5f;

            for (int i = 0; i < ringCount; i++) {
                float t = (float)i / (ringCount - 1);
                Vector3 basePosition = Vector3.Lerp(config.StartPosition, config.EndPosition, t);

                TradelaneRing.RingType type;
                if (i == 0) type = TradelaneRing.RingType.Start;
                else if (i == ringCount - 1) type = TradelaneRing.RingType.End;
                else type = TradelaneRing.RingType.Intermediate;

                // Top ring (forward direction: start -> end)
                Vector3 topPosition = basePosition + upDir * halfOffset;
                string topName = $"{config.Name} Ring {i + 1} (Fwd)";
                var topRing = new TradelaneRing(
                    graphicsDevice, topName, topPosition,
                    LaneDirection, type, i,
                    config.RingScale, config.RingColor,
                    TradelaneRing.RingDirection.Forward
                );
                ForwardRings.Add(topRing);
                Rings.Add(topRing);

                // Bottom ring (reverse direction: end -> start)
                Vector3 bottomPosition = basePosition - upDir * halfOffset;
                string bottomName = $"{config.Name} Ring {i + 1} (Rev)";
                var bottomRing = new TradelaneRing(
                    graphicsDevice, bottomName, bottomPosition,
                    LaneDirection, type, i,
                    config.RingScale, config.RingColor,
                    TradelaneRing.RingDirection.Reverse
                );
                ReverseRings.Add(bottomRing);
                Rings.Add(bottomRing);
            }
        }

        /// <summary>
        /// Set the 3D model on all rings
        /// </summary>
        public void SetModel(Model model) {
            foreach (var ring in Rings) {
                ring.Model = model;
            }
        }

        /// <summary>
        /// Get entry/exit rings as SpaceObjects (for targeting).
        /// Returns the forward start ring and the reverse end ring.
        /// </summary>
        public List<SpaceObject> GetRingsAsSpaceObjects() {
            var objects = new List<SpaceObject>();
            // Forward entry: first forward ring (top, start position)
            if (ForwardRings.Count > 0) {
                objects.Add(ForwardRings[0]);
            }
            // Reverse entry: last reverse ring (bottom, end position)
            if (ReverseRings.Count > 0) {
                objects.Add(ReverseRings[ReverseRings.Count - 1]);
            }
            return objects;
        }

        /// <summary>
        /// Check if the player is near an entry ring and return that ring.
        /// Forward travel: approach the first forward (top) ring at the start.
        /// Reverse travel: approach the last reverse (bottom) ring at the end.
        /// Uses the extended DockingRange for detection.
        /// </summary>
        public TradelaneRing GetNearbyEntryRing(Vector3 playerPosition) {
            float dockRange = _config.DockingRange;

            // Check forward entry (first forward/top ring)
            if (ForwardRings.Count > 0 && !ForwardRings[0].IsDestroyed) {
                float dist = Vector3.Distance(playerPosition, ForwardRings[0].Position);
                if (dist <= dockRange) {
                    return ForwardRings[0];
                }
            }

            // Check reverse entry (last reverse/bottom ring)
            if (ReverseRings.Count > 0 && !ReverseRings[ReverseRings.Count - 1].IsDestroyed) {
                float dist = Vector3.Distance(playerPosition, ReverseRings[ReverseRings.Count - 1].Position);
                if (dist <= dockRange) {
                    return ReverseRings[ReverseRings.Count - 1];
                }
            }

            return null;
        }

        /// <summary>
        /// Initiate travel from the given entry ring.
        /// Forward (top) ring at start ? travel start to end through ForwardRings.
        /// Reverse (bottom) ring at end ? travel end to start through ReverseRings.
        /// </summary>
        public bool StartTravel(TradelaneRing entryRing) {
            if (IsActive || IsBroken) return false;

            if (entryRing.Direction == TradelaneRing.RingDirection.Forward && entryRing == ForwardRings[0]) {
                // Travel forward (start -> end) using top rings
                TravelDirection = 1;
                _activeRings = ForwardRings;
                CurrentRingIndex = 0;
            } else if (entryRing.Direction == TradelaneRing.RingDirection.Reverse && entryRing == ReverseRings[ReverseRings.Count - 1]) {
                // Travel reverse (end -> start) using bottom rings
                TravelDirection = -1;
                _activeRings = ReverseRings;
                CurrentRingIndex = ReverseRings.Count - 1;
            } else {
                return false;
            }

            IsActive = true;
            _lastPulsedRingIndex = CurrentRingIndex;
            _activeRings[CurrentRingIndex].TriggerPulse();
            AdvanceToNextRing();

            Console.WriteLine($"[TRADELANE] Travel started: {_config.Name} direction={TravelDirection}");
            return true;
        }

        /// <summary>
        /// Set up interpolation to the next ring in the active ring list
        /// </summary>
        private void AdvanceToNextRing() {
            if (_activeRings == null) return;
            int nextIndex = CurrentRingIndex + TravelDirection;

            // Check if we've reached the end
            if (nextIndex < 0 || nextIndex >= _activeRings.Count) {
                CompleteTravel();
                return;
            }

            // Check if the next ring is destroyed (lane break)
            if (_activeRings[nextIndex].IsDestroyed) {
                BreakLane();
                return;
            }

            _transitStartPos = _activeRings[CurrentRingIndex].Position;
            _transitEndPos = _activeRings[nextIndex].Position;
            _transitProgress = 0f;

            float ringDistance = Vector3.Distance(_transitStartPos, _transitEndPos);
            _transitDuration = ringDistance / _config.TravelSpeed;

            CurrentRingIndex = nextIndex;
        }

        /// <summary>
        /// Update the tradelane state
        /// </summary>
        public void Update(GameTime gameTime, Vector3 playerPosition) {
            float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;

            // Update all rings (both forward and reverse)
            foreach (var ring in Rings) {
                ring.Update(gameTime);
            }

            // Check for broken lane
            if (!IsBroken) {
                foreach (var ring in Rings) {
                    if (ring.IsDestroyed) {
                        IsBroken = true;
                        break;
                    }
                }
            }

            // Check nearby entry ring
            NearbyEntryRing = GetNearbyEntryRing(playerPosition);

            // Update transit
            if (IsActive && _activeRings != null) {
                _transitProgress += dt / _transitDuration;

                if (_transitProgress >= 1f) {
                    // Arrived at current ring - pulse it and advance
                    _activeRings[CurrentRingIndex].TriggerPulse();
                    _lastPulsedRingIndex = CurrentRingIndex;
                    AdvanceToNextRing();
                }
            }
        }

        /// <summary>
        /// Get the current position of the ship during transit (interpolated between rings)
        /// </summary>
        public Vector3 GetCurrentTransitPosition() {
            if (!IsActive) return Vector3.Zero;
            return Vector3.Lerp(_transitStartPos, _transitEndPos, MathHelper.Clamp(_transitProgress, 0f, 1f));
        }

        /// <summary>
        /// Get the direction of travel
        /// </summary>
        public Vector3 GetTravelForward() {
            return TravelDirection > 0 ? LaneDirection : -LaneDirection;
        }

        /// <summary>
        /// Complete travel (reached the end)
        /// </summary>
        private void CompleteTravel() {
            IsActive = false;
            _activeRings = null;
            Console.WriteLine($"[TRADELANE] Travel complete: {_config.Name}");
        }

        /// <summary>
        /// Break the lane (ring destroyed mid-transit)
        /// </summary>
        private void BreakLane() {
            IsActive = false;
            IsBroken = true;
            _activeRings = null;
            Console.WriteLine($"[TRADELANE] Lane BROKEN: {_config.Name} at ring {CurrentRingIndex}");
        }

        /// <summary>
        /// Repair all rings and reset lane state
        /// </summary>
        public void Repair() {
            foreach (var ring in Rings) {
                ring.IsDestroyed = false;
            }
            IsBroken = false;
        }

        /// <summary>
        /// Draw all rings (3D model pass)
        /// </summary>
        public void Draw(Matrix view, Matrix projection, Vector3 lightDirection) {
            foreach (var ring in Rings) {
                ring.Draw(view, projection, lightDirection);
            }
        }

        /// <summary>
        /// Draw all ring energy effects (additive pass)
        /// </summary>
        public void DrawEnergyEffects(Matrix view, Matrix projection) {
            foreach (var ring in Rings) {
                ring.DrawEnergyEffect(view, projection);
            }
        }
    }
}
