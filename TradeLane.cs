using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Roguelancer.Configuration;
using System;
using System.Collections.Generic;

namespace Roguelancer {
    /// <summary>
    /// A tradelane consisting of a sequence of rings between two endpoints.
    /// Players dock at one end and are accelerated through the lane at high speed.
    /// If a ring is destroyed, the lane breaks and the ship is ejected.
    /// 
    /// The lane supports bidirectional travel:
    ///   - Approach the Start ring to travel toward the End.
    ///   - Approach the End ring to travel toward the Start.
    /// </summary>
    public class TradeLane {
        private GraphicsDevice _graphicsDevice;
        private TradelaneConfig _config;

        /// <summary>
        /// All rings in order from start to end
        /// </summary>
        public List<TradelaneRing> Rings { get; } = new();

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
        /// Current ring index the ship is at (or heading toward)
        /// </summary>
        public int CurrentRingIndex { get; private set; }

        /// <summary>
        /// Direction of travel: +1 = start to end, -1 = end to start
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

        public TradeLane(GraphicsDevice graphicsDevice, TradelaneConfig config) {
            _graphicsDevice = graphicsDevice;
            _config = config;

            LaneDirection = Vector3.Normalize(config.EndPosition - config.StartPosition);
            float totalDistance = Vector3.Distance(config.StartPosition, config.EndPosition);
            int ringCount = Math.Max(2, (int)(totalDistance / config.RingSpacing) + 1);

            for (int i = 0; i < ringCount; i++) {
                float t = (float)i / (ringCount - 1);
                Vector3 ringPosition = Vector3.Lerp(config.StartPosition, config.EndPosition, t);

                TradelaneRing.RingType type;
                if (i == 0) type = TradelaneRing.RingType.Start;
                else if (i == ringCount - 1) type = TradelaneRing.RingType.End;
                else type = TradelaneRing.RingType.Intermediate;

                string ringName = $"{config.Name} Ring {i + 1}";
                var ring = new TradelaneRing(
                    graphicsDevice, ringName, ringPosition,
                    LaneDirection, type, i,
                    config.RingScale, config.RingColor
                );
                Rings.Add(ring);
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
        /// Get all rings as SpaceObjects (for targeting)
        /// </summary>
        public List<SpaceObject> GetRingsAsSpaceObjects() {
            var objects = new List<SpaceObject>();
            // Add the start and end ring as targetable objects so players can GOTO them
            if (Rings.Count > 0) {
                objects.Add(Rings[0]);
            }
            if (Rings.Count > 1) {
                objects.Add(Rings[Rings.Count - 1]);
            }
            return objects;
        }

        /// <summary>
        /// Check if the player is near an entry ring (start or end) and return that ring
        /// </summary>
        public TradelaneRing GetNearbyEntryRing(Vector3 playerPosition) {
            if (Rings.Count == 0) return null;

            // Check start ring
            float distToStart = Vector3.Distance(playerPosition, Rings[0].Position);
            if (distToStart <= _config.ActivationRange && !Rings[0].IsDestroyed) {
                return Rings[0];
            }

            // Check end ring
            float distToEnd = Vector3.Distance(playerPosition, Rings[Rings.Count - 1].Position);
            if (distToEnd <= _config.ActivationRange && !Rings[Rings.Count - 1].IsDestroyed) {
                return Rings[Rings.Count - 1];
            }

            return null;
        }

        /// <summary>
        /// Initiate travel from the given entry ring
        /// </summary>
        public bool StartTravel(TradelaneRing entryRing) {
            if (IsActive || IsBroken) return false;

            if (entryRing == Rings[0]) {
                // Travel from start to end
                TravelDirection = 1;
                CurrentRingIndex = 0;
            } else if (entryRing == Rings[Rings.Count - 1]) {
                // Travel from end to start
                TravelDirection = -1;
                CurrentRingIndex = Rings.Count - 1;
            } else {
                return false;
            }

            IsActive = true;
            _lastPulsedRingIndex = CurrentRingIndex;
            Rings[CurrentRingIndex].TriggerPulse();
            AdvanceToNextRing();

            Console.WriteLine($"[TRADELANE] Travel started: {_config.Name} direction={TravelDirection}");
            return true;
        }

        /// <summary>
        /// Set up interpolation to the next ring
        /// </summary>
        private void AdvanceToNextRing() {
            int nextIndex = CurrentRingIndex + TravelDirection;

            // Check if we've reached the end
            if (nextIndex < 0 || nextIndex >= Rings.Count) {
                CompleteTravel();
                return;
            }

            // Check if the next ring is destroyed (lane break)
            if (Rings[nextIndex].IsDestroyed) {
                BreakLane();
                return;
            }

            _transitStartPos = Rings[CurrentRingIndex].Position;
            _transitEndPos = Rings[nextIndex].Position;
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

            // Update all rings
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
            if (IsActive) {
                _transitProgress += dt / _transitDuration;

                if (_transitProgress >= 1f) {
                    // Arrived at current ring - pulse it and advance
                    Rings[CurrentRingIndex].TriggerPulse();
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
            Console.WriteLine($"[TRADELANE] Travel complete: {_config.Name}");
        }

        /// <summary>
        /// Break the lane (ring destroyed mid-transit)
        /// </summary>
        private void BreakLane() {
            IsActive = false;
            IsBroken = true;
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
