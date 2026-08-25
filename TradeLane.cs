using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Roguelancer.Configuration;
using System;
using System.Collections.Generic;

namespace Roguelancer
{
    /// <summary>
    /// One logical, shared trade-lane corridor. The existing paired ring
    /// presentation is retained: forward and reverse traffic use stable
    /// directional rails through the same route and can pass one another.
    /// Transit state is per traveler rather than a lane-global flag.
    /// </summary>
    public class TradeLane
    {
        private sealed class TransitState
        {
            public object Traveler;
            public bool IsPlayer;
            public TradeLaneDirection Direction;
            public List<TradelaneRing> Rings;
            public int CurrentRingIndex;
            public int NextRingIndex;
            public float SegmentProgress;
            public float SegmentDuration;
            public Vector3 SegmentStart;
            public Vector3 SegmentEnd;
        }

        private readonly GraphicsDevice _graphicsDevice;
        private readonly TradelaneConfig _config;
        private readonly Dictionary<NpcShip, TransitState> _npcTransit = new();
        private readonly Dictionary<int, TradeLaneDisruptionInfo> _disruptions = new();
        private readonly List<TradeLaneTransitEvent> _transitEvents = new();

        private TransitState _playerTransit;
        private Vector3 _trafficOffsetAxis;
        private double _simulationTimeSeconds;

        public List<TradelaneRing> Rings { get; } = new();
        public List<TradelaneRing> ForwardRings { get; } = new();
        public List<TradelaneRing> ReverseRings { get; } = new();

        public TradelaneConfig Config => _config;
        public string LaneId => _config.StableId;
        public Vector3 LaneDirection { get; private set; }
        public Vector3 TrafficOffsetAxis => _trafficOffsetAxis;
        public double SimulationTimeSeconds => _simulationTimeSeconds;

        public bool IsActive => _playerTransit != null;

        /// <summary>Permanent ring destruction remains a lane-wide hard failure.</summary>
        public bool IsBroken
        {
            get
            {
                for (int i = 0; i < Rings.Count; i++)
                {
                    if (Rings[i]?.IsDestroyed == true)
                        return true;
                }

                return false;
            }
        }

        public int CurrentRingIndex => _playerTransit?.CurrentRingIndex ?? -1;
        public int NextRingIndex => _playerTransit?.NextRingIndex ?? -1;
        public int TravelDirection => _playerTransit == null ? 0 : (int)_playerTransit.Direction;
        public TradeLaneDirection? ActiveDirection => _playerTransit?.Direction;
        public int ActiveTravelerCount => (_playerTransit == null ? 0 : 1) + _npcTransit.Count;
        public float TransitProgress => _playerTransit == null ? 0f : SanitizeProgress(_playerTransit.SegmentProgress);
        public TradelaneRing NearbyEntryRing { get; private set; }

        public const float MinimumFollowingDistance = 180f;
        public const float DefaultRecoveryDurationSeconds = 45f;
        public const float DefaultDisruptionDamageThreshold = 100f;

        public TradeLane(GraphicsDevice graphicsDevice, TradelaneConfig config)
        {
            _graphicsDevice = graphicsDevice;
            _config = config ?? new TradelaneConfig();

            Vector3 delta = _config.EndPosition - _config.StartPosition;
            float distance = delta.Length();
            LaneDirection = IsFiniteVector(delta) && distance > 0.01f
                ? delta / distance
                : Vector3.Forward;

            float ringSpacing = TradeLaneStateSanitizer.Positive(_config.RingSpacing, 500f);
            int ringCount = distance > 0.01f
                ? Math.Max(2, Math.Min(256, (int)(distance / ringSpacing) + 1))
                : 2;

            Vector3 upDir = Math.Abs(Vector3.Dot(LaneDirection, Vector3.Up)) > 0.99f
                ? Vector3.Forward
                : Vector3.Up;
            float halfVerticalOffset = MathHelper.Clamp(
                TradeLaneStateSanitizer.NonNegative(_config.RingVerticalOffset) * 0.5f,
                0f,
                500f);

            Vector3 lateral = Vector3.Cross(LaneDirection, Vector3.Up);
            if (!IsFiniteVector(lateral) || lateral.LengthSquared() < 0.0001f)
                lateral = Vector3.Cross(LaneDirection, Vector3.Forward);
            _trafficOffsetAxis = lateral.LengthSquared() > 0.0001f
                ? Vector3.Normalize(lateral)
                : Vector3.Right;

            Color ringColor = _config.RingColor;
            for (int i = 0; i < ringCount; i++)
            {
                float t = ringCount <= 1 ? 0f : (float)i / (ringCount - 1);
                Vector3 basePosition = Vector3.Lerp(_config.StartPosition, _config.EndPosition, t);
                if (!IsFiniteVector(basePosition))
                    basePosition = Vector3.Zero;

                TradelaneRing.RingType type = i == 0
                    ? TradelaneRing.RingType.Start
                    : i == ringCount - 1
                        ? TradelaneRing.RingType.End
                        : TradelaneRing.RingType.Intermediate;

                var forwardRing = new TradelaneRing(
                    graphicsDevice,
                    $"{_config.Name} Ring {i + 1} (Fwd)",
                    basePosition + upDir * halfVerticalOffset,
                    LaneDirection,
                    type,
                    i,
                    SanitizeRingScale(_config.RingScale),
                    ringColor,
                    TradelaneRing.RingDirection.Forward);
                var reverseRing = new TradelaneRing(
                    graphicsDevice,
                    $"{_config.Name} Ring {i + 1} (Rev)",
                    basePosition - upDir * halfVerticalOffset,
                    LaneDirection,
                    type,
                    i,
                    SanitizeRingScale(_config.RingScale),
                    ringColor,
                    TradelaneRing.RingDirection.Reverse);

                ForwardRings.Add(forwardRing);
                ReverseRings.Add(reverseRing);
                Rings.Add(forwardRing);
                Rings.Add(reverseRing);
            }
        }

        public void SetModel(Model model)
        {
            for (int i = 0; i < Rings.Count; i++)
                Rings[i].Model = model;
        }

        public List<SpaceObject> GetRingsAsSpaceObjects()
        {
            var objects = new List<SpaceObject>();
            TradelaneRing forwardEntry = GetEntryRing(TradeLaneDirection.Forward);
            TradelaneRing reverseEntry = GetEntryRing(TradeLaneDirection.Reverse);
            if (forwardEntry != null) objects.Add(forwardEntry);
            if (reverseEntry != null) objects.Add(reverseEntry);
            return objects;
        }

        public TradelaneRing GetEntryRing(TradeLaneDirection direction)
        {
            List<TradelaneRing> route = GetRoute(direction);
            if (route == null || route.Count == 0)
                return null;
            return direction == TradeLaneDirection.Forward ? route[0] : route[route.Count - 1];
        }

        public TradelaneRing GetExitRing(TradeLaneDirection direction)
        {
            List<TradelaneRing> route = GetRoute(direction);
            if (route == null || route.Count == 0)
                return null;
            return direction == TradeLaneDirection.Forward ? route[route.Count - 1] : route[0];
        }

        public IReadOnlyList<TradelaneRing> GetRouteRings(TradeLaneDirection direction) => GetRoute(direction);

        public Vector3 GetDirectionalOffset(TradeLaneDirection direction)
        {
            float offset = TradeLaneStateSanitizer.NonNegative(_config.TrafficLateralOffset);
            offset = MathHelper.Clamp(offset, 0f, 250f);
            return _trafficOffsetAxis * (direction == TradeLaneDirection.Forward ? offset : -offset);
        }

        public Vector3 GetRingTravelPosition(TradelaneRing ring, TradeLaneDirection direction)
        {
            if (ring == null || !IsFiniteVector(ring.Position))
                return Vector3.Zero;
            return ring.Position + GetDirectionalOffset(direction);
        }

        public TradelaneRing GetNearbyEntryRing(Vector3 playerPosition)
        {
            if (!IsFiniteVector(playerPosition))
                return null;

            float dockingRange = TradeLaneStateSanitizer.Positive(_config.DockingRange, 600f);
            TradelaneRing forward = GetEntryRing(TradeLaneDirection.Forward);
            if (IsEntryAvailable(TradeLaneDirection.Forward, forward) &&
                Vector3.Distance(playerPosition, forward.Position) <= dockingRange)
            {
                return forward;
            }

            TradelaneRing reverse = GetEntryRing(TradeLaneDirection.Reverse);
            if (IsEntryAvailable(TradeLaneDirection.Reverse, reverse) &&
                Vector3.Distance(playerPosition, reverse.Position) <= dockingRange)
            {
                return reverse;
            }

            return null;
        }

        public bool IsEntryAvailable(TradeLaneDirection direction, TradelaneRing entryRing = null)
        {
            entryRing ??= GetEntryRing(direction);
            if (entryRing == null || !OwnsEntryRing(entryRing, direction))
                return false;
            return CanUseRoute(direction);
        }

        public bool CanUseRoute(TradeLaneDirection direction)
        {
            List<TradelaneRing> route = GetRoute(direction);
            if (route == null || route.Count < 2 || IsBroken ||
                !IsFiniteVector(_config.StartPosition) || !IsFiniteVector(_config.EndPosition) ||
                !IsFiniteVector(LaneDirection))
                return false;

            for (int i = 0; i < route.Count; i++)
            {
                if (!IsRingOperational(route[i].Index))
                    return false;
            }

            return true;
        }

        public bool StartTravel(TradelaneRing entryRing, Ship playerShip = null)
        {
            return StartTraveler(playerShip, isPlayer: true, entryRing);
        }

        public bool StartTravelForNpc(NpcShip npc, TradelaneRing entryRing)
        {
            if (npc == null || npc.IsDestroyed)
                return false;
            return StartTraveler(npc, isPlayer: false, entryRing);
        }

        private bool StartTraveler(object traveler, bool isPlayer, TradelaneRing entryRing)
        {
            if (entryRing == null || !IsFiniteVector(entryRing.Position))
                return false;
            if (isPlayer ? _playerTransit != null : _npcTransit.ContainsKey((NpcShip)traveler))
                return false;

            TradeLaneDirection direction;
            if (entryRing == GetEntryRing(TradeLaneDirection.Forward) && entryRing.Direction == TradelaneRing.RingDirection.Forward)
                direction = TradeLaneDirection.Forward;
            else if (entryRing == GetEntryRing(TradeLaneDirection.Reverse) && entryRing.Direction == TradelaneRing.RingDirection.Reverse)
                direction = TradeLaneDirection.Reverse;
            else
                return false;

            if (!CanUseRoute(direction))
                return false;

            List<TradelaneRing> route = GetRoute(direction);
            var state = new TransitState
            {
                Traveler = traveler,
                IsPlayer = isPlayer,
                Direction = direction,
                Rings = route,
                CurrentRingIndex = direction == TradeLaneDirection.Forward ? 0 : route.Count - 1,
                SegmentProgress = 0f
            };

            if (!SetNextSegment(state))
                return false;

            if (isPlayer)
                _playerTransit = state;
            else
                _npcTransit[(NpcShip)traveler] = state;

            route[state.CurrentRingIndex].TriggerPulse();
            return true;
        }

        private bool SetNextSegment(TransitState state)
        {
            if (state?.Rings == null || state.Rings.Count < 2 ||
                state.CurrentRingIndex < 0 || state.CurrentRingIndex >= state.Rings.Count)
                return false;

            int next = state.CurrentRingIndex + (int)state.Direction;
            if (next < 0 || next >= state.Rings.Count)
                return false;
            if (!IsRingOperational(state.Rings[next].Index))
                return false;

            state.NextRingIndex = next;
            state.SegmentProgress = 0f;
            state.SegmentStart = GetRingTravelPosition(state.Rings[state.CurrentRingIndex], state.Direction);
            state.SegmentEnd = GetRingTravelPosition(state.Rings[next], state.Direction);
            float distance = Vector3.Distance(state.SegmentStart, state.SegmentEnd);
            float speed = GetTransitSpeed();
            state.SegmentDuration = distance <= 0.01f ? 0f : distance / speed;
            return true;
        }

        public void Update(GameTime gameTime, Vector3 playerPosition)
        {
            Update(gameTime);
            NearbyEntryRing = GetNearbyEntryRing(playerPosition);
        }

        public void Update(GameTime gameTime)
        {
            float deltaTime = TradeLaneStateSanitizer.Elapsed(
                gameTime == null ? 0f : (float)gameTime.ElapsedGameTime.TotalSeconds);
            _simulationTimeSeconds += deltaTime;

            if (gameTime != null)
            {
                for (int i = 0; i < Rings.Count; i++)
                    Rings[i]?.Update(gameTime);
            }

            UpdateDisruptions(deltaTime);

            if (_playerTransit != null)
                UpdateTraveler(_playerTransit, deltaTime);

            if (_npcTransit.Count > 0)
            {
                var travelers = new List<TransitState>(_npcTransit.Values);
                for (int i = 0; i < travelers.Count; i++)
                {
                    if (travelers[i] != null && travelers[i].Traveler is NpcShip npc && _npcTransit.ContainsKey(npc))
                        UpdateTraveler(travelers[i], deltaTime);
                }
            }
        }

        private void UpdateTraveler(TransitState state, float deltaTime)
        {
            if (state == null)
                return;

            if (!IsValidTraveler(state))
            {
                RemoveTraveler(state, TradeLaneTransitExitReason.InvalidState);
                return;
            }

            if (!IsRingOperational(state.Rings[state.CurrentRingIndex].Index) ||
                !IsRingOperational(state.Rings[state.NextRingIndex].Index))
            {
                RemoveTraveler(state, TradeLaneTransitExitReason.Disrupted);
                return;
            }

            float remaining = deltaTime;
            int guard = 0;
            while (state != null && IsCurrentState(state) && remaining >= 0f && guard++ < 512)
            {
                if (!IsFiniteVector(state.SegmentStart) || !IsFiniteVector(state.SegmentEnd) ||
                    !TradeLaneStateSanitizer.IsFinite(state.SegmentDuration))
                {
                    RemoveTraveler(state, TradeLaneTransitExitReason.InvalidState);
                    return;
                }

                if (state.SegmentDuration <= 0.0001f)
                {
                    ArriveAtNextRing(state);
                    continue;
                }

                float available = state.SegmentDuration * (1f - SanitizeProgress(state.SegmentProgress));
                if (remaining < available)
                {
                    state.SegmentProgress += remaining / state.SegmentDuration;
                    remaining = 0f;
                    break;
                }

                remaining -= Math.Max(0f, available);
                state.SegmentProgress = 1f;
                ArriveAtNextRing(state);
            }

            if (guard >= 512 && IsCurrentState(state))
                RemoveTraveler(state, TradeLaneTransitExitReason.InvalidState);
        }

        private void ArriveAtNextRing(TransitState state)
        {
            if (!IsCurrentState(state))
                return;

            state.CurrentRingIndex = state.NextRingIndex;
            state.Rings[state.CurrentRingIndex].TriggerPulse();

            bool isExit = state.Direction == TradeLaneDirection.Forward
                ? state.CurrentRingIndex == state.Rings.Count - 1
                : state.CurrentRingIndex == 0;
            if (isExit)
            {
                RemoveTraveler(state, TradeLaneTransitExitReason.Completed);
                return;
            }

            if (!SetNextSegment(state))
            {
                RemoveTraveler(state, IsRingOperational(state.Rings[state.CurrentRingIndex].Index)
                    ? TradeLaneTransitExitReason.InvalidState
                    : TradeLaneTransitExitReason.Disrupted);
            }
        }

        private bool IsValidTraveler(TransitState state)
        {
            if (state.Rings == null || state.Rings.Count < 2 ||
                state.CurrentRingIndex < 0 || state.CurrentRingIndex >= state.Rings.Count ||
                state.NextRingIndex < 0 || state.NextRingIndex >= state.Rings.Count ||
                Math.Abs(state.NextRingIndex - state.CurrentRingIndex) != 1)
                return false;

            if (state.IsPlayer && state.Traveler is Ship player && player.Hull?.IsDestroyed == true)
                return false;

            if (!state.IsPlayer && (state.Traveler is not NpcShip npc || npc.IsDestroyed))
                return false;

            return state.IsPlayer || state.Traveler is NpcShip;
        }

        private bool IsCurrentState(TransitState state)
        {
            if (state == null)
                return false;
            return state.IsPlayer
                ? ReferenceEquals(_playerTransit, state)
                : state.Traveler is NpcShip npc && _npcTransit.TryGetValue(npc, out TransitState current) && ReferenceEquals(current, state);
        }

        private void RemoveTraveler(TransitState state, TradeLaneTransitExitReason reason)
        {
            if (state == null || !IsCurrentState(state))
                return;

            Vector3 position = GetStatePosition(state);
            Vector3 direction = GetTravelForward(state.Direction);
            Vector3 velocity = direction * (reason == TradeLaneTransitExitReason.Completed ? GetTransitSpeed() : Math.Max(250f, GetTransitSpeed() * 0.2f));
            if (!IsFiniteVector(position)) position = Vector3.Zero;
            if (!IsFiniteVector(velocity)) velocity = Vector3.Zero;

            _transitEvents.Add(new TradeLaneTransitEvent
            {
                Traveler = state.Traveler,
                LaneId = LaneId,
                Direction = state.Direction,
                RingIndex = state.CurrentRingIndex,
                Position = position,
                Velocity = velocity,
                Reason = reason
            });

            if (state.IsPlayer)
                _playerTransit = null;
            else if (state.Traveler is NpcShip npc)
                _npcTransit.Remove(npc);
        }

        public bool EjectTraveler(object traveler, TradeLaneTransitExitReason reason)
        {
            if (traveler == null)
                return false;
            if (traveler is NpcShip npc && _npcTransit.TryGetValue(npc, out TransitState npcState))
            {
                RemoveTraveler(npcState, reason);
                return true;
            }
            if (_playerTransit != null && ReferenceEquals(_playerTransit.Traveler, traveler))
            {
                RemoveTraveler(_playerTransit, reason);
                return true;
            }
            return false;
        }

        public bool IsTravelerInTransit(object traveler)
        {
            if (traveler is NpcShip npc)
                return _npcTransit.ContainsKey(npc);
            return traveler != null && _playerTransit != null && ReferenceEquals(_playerTransit.Traveler, traveler);
        }

        public bool TryGetTransitSnapshot(object traveler, out TradeLaneTransitSnapshot snapshot)
        {
            snapshot = null;
            TransitState state = null;
            if (traveler is NpcShip npc)
                _npcTransit.TryGetValue(npc, out state);
            else if (_playerTransit != null && ReferenceEquals(_playerTransit.Traveler, traveler))
                state = _playerTransit;

            if (state == null)
                return false;

            snapshot = BuildSnapshot(state);
            return snapshot != null;
        }

        public IReadOnlyList<TradeLaneTransitSnapshot> GetActiveTransitSnapshots()
        {
            var result = new List<TradeLaneTransitSnapshot>();
            if (_playerTransit != null) result.Add(BuildSnapshot(_playerTransit));
            foreach (TransitState state in _npcTransit.Values)
                result.Add(BuildSnapshot(state));
            return result;
        }

        private TradeLaneTransitSnapshot BuildSnapshot(TransitState state)
        {
            if (state == null) return null;
            Vector3 position = GetStatePosition(state);
            Vector3 velocity = GetTravelForward(state.Direction) * GetTransitSpeed();
            return new TradeLaneTransitSnapshot
            {
                Traveler = state.Traveler,
                LaneId = LaneId,
                Direction = state.Direction,
                CurrentRingIndex = state.CurrentRingIndex,
                NextRingIndex = state.NextRingIndex,
                SegmentProgress = SanitizeProgress(state.SegmentProgress),
                Position = position,
                Velocity = velocity,
                IsPlayer = state.IsPlayer
            };
        }

        public List<TradeLaneTransitEvent> DrainTransitEvents()
        {
            var events = new List<TradeLaneTransitEvent>(_transitEvents);
            _transitEvents.Clear();
            return events;
        }

        public Vector3 GetCurrentTransitPosition()
        {
            return _playerTransit == null ? Vector3.Zero : GetStatePosition(_playerTransit);
        }

        public Vector3 GetCurrentTransitPosition(object traveler)
        {
            if (traveler is NpcShip npc && _npcTransit.TryGetValue(npc, out TransitState state))
                return GetStatePosition(state);
            return _playerTransit != null && ReferenceEquals(_playerTransit.Traveler, traveler)
                ? GetStatePosition(_playerTransit)
                : Vector3.Zero;
        }

        public Vector3 GetTravelForward()
        {
            return _playerTransit == null ? LaneDirection : GetTravelForward(_playerTransit.Direction);
        }

        public Vector3 GetTravelForward(TradeLaneDirection direction)
        {
            return direction == TradeLaneDirection.Forward ? LaneDirection : -LaneDirection;
        }

        public float GetTransitSpeed()
        {
            return MathHelper.Clamp(TradeLaneStateSanitizer.Positive(_config.TravelSpeed, 1500f), 50f, 10000f);
        }

        private Vector3 GetStatePosition(TransitState state)
        {
            if (state == null || state.Rings == null || state.CurrentRingIndex < 0 || state.CurrentRingIndex >= state.Rings.Count)
                return Vector3.Zero;
            Vector3 position = Vector3.Lerp(state.SegmentStart, state.SegmentEnd, SanitizeProgress(state.SegmentProgress));
            return TradeLaneStateSanitizer.IsFinite(position) ? position : Vector3.Zero;
        }

        public bool TryDisruptSegment(
            int ringIndex,
            TradeLaneDisruptionSource source = TradeLaneDisruptionSource.Unknown,
            string sourceName = null,
            float recoveryDurationSeconds = 0f)
        {
            if (ringIndex < 0 || ringIndex >= ForwardRings.Count || ringIndex >= ReverseRings.Count)
                return false;

            TradeLaneDisruptionInfo info = GetOrCreateDisruption(ringIndex);
            if (info.IsActive)
                return false;

            float recovery = recoveryDurationSeconds > 0f
                ? recoveryDurationSeconds
                : TradeLaneStateSanitizer.Positive(_config.DisruptionRecoverySeconds, DefaultRecoveryDurationSeconds);
            info.State = TradeLaneDisruptionState.Disrupted;
            info.Source = Enum.IsDefined(typeof(TradeLaneDisruptionSource), source) ? source : TradeLaneDisruptionSource.Unknown;
            info.SourceName = sourceName ?? string.Empty;
            info.DamageAccumulated = 0f;
            info.RecoveryDurationSeconds = MathHelper.Clamp(recovery, 1f, 300f);
            info.RemainingRecoverySeconds = info.RecoveryDurationSeconds;
            info.OccurredAtSeconds = _simulationTimeSeconds;
            return true;
        }

        public bool ApplyRingDamage(
            int ringIndex,
            float damage,
            bool hostile,
            TradeLaneDisruptionSource source = TradeLaneDisruptionSource.Unknown,
            string sourceName = null)
        {
            if (!hostile || ringIndex < 0 || ringIndex >= ForwardRings.Count ||
                !TradeLaneStateSanitizer.IsFinite(damage) || damage <= 0f)
                return false;

            TradeLaneDisruptionInfo info = GetOrCreateDisruption(ringIndex);
            if (info.IsActive)
                return false;

            float threshold = TradeLaneStateSanitizer.Positive(_config.DisruptionDamageThreshold, DefaultDisruptionDamageThreshold);
            info.DamageAccumulated = Math.Min(threshold, info.DamageAccumulated + damage);
            if (info.DamageAccumulated < threshold)
                return false;

            return TryDisruptSegment(ringIndex, source, sourceName);
        }

        private TradeLaneDisruptionInfo GetOrCreateDisruption(int ringIndex)
        {
            if (!_disruptions.TryGetValue(ringIndex, out TradeLaneDisruptionInfo info))
            {
                info = new TradeLaneDisruptionInfo
                {
                    LaneId = LaneId,
                    LaneName = _config.Name ?? string.Empty,
                    RingIndex = ringIndex,
                    SegmentId = $"{LaneId}:ring:{ringIndex}",
                    State = TradeLaneDisruptionState.Operational
                };
                _disruptions[ringIndex] = info;
            }

            return info;
        }

        private void UpdateDisruptions(float deltaTime)
        {
            if (deltaTime <= 0f || _disruptions.Count == 0)
                return;

            foreach (TradeLaneDisruptionInfo info in _disruptions.Values)
            {
                if (!info.IsActive)
                    continue;

                info.RemainingRecoverySeconds = Math.Max(0f, info.RemainingRecoverySeconds - deltaTime);
                float transition = Math.Min(5f, info.RecoveryDurationSeconds * 0.15f);
                if (info.RemainingRecoverySeconds <= 0f)
                {
                    info.State = TradeLaneDisruptionState.Operational;
                    info.DamageAccumulated = 0f;
                    info.Source = TradeLaneDisruptionSource.Unknown;
                    info.SourceName = string.Empty;
                    info.RecoveryDurationSeconds = 0f;
                }
                else if (info.RemainingRecoverySeconds <= transition)
                {
                    info.State = TradeLaneDisruptionState.Recovering;
                }
            }
        }

        public TradeLaneDisruptionState GetDisruptionState(int ringIndex)
        {
            return _disruptions.TryGetValue(ringIndex, out TradeLaneDisruptionInfo info)
                ? info.State
                : TradeLaneDisruptionState.Operational;
        }

        public bool TryGetDisruptionInfo(int ringIndex, out TradeLaneDisruptionInfo info)
        {
            info = null;
            if (!_disruptions.TryGetValue(ringIndex, out TradeLaneDisruptionInfo current))
                return false;
            info = current;
            return current.IsActive;
        }

        public IReadOnlyList<TradeLaneDisruptionInfo> GetActiveDisruptions()
        {
            var result = new List<TradeLaneDisruptionInfo>();
            foreach (TradeLaneDisruptionInfo info in _disruptions.Values)
            {
                if (info.IsActive)
                    result.Add(info);
            }
            return result;
        }

        public void ResetTransientState()
        {
            _playerTransit = null;
            _npcTransit.Clear();
            _transitEvents.Clear();
            _simulationTimeSeconds = 0d;
            _disruptions.Clear();
            NearbyEntryRing = null;
        }

        public void Repair()
        {
            for (int i = 0; i < Rings.Count; i++)
                Rings[i].IsDestroyed = false;
            ResetTransientState();
        }

        public void Draw(Matrix view, Matrix projection, Vector3 lightDirection)
        {
            for (int i = 0; i < Rings.Count; i++)
                Rings[i]?.Draw(view, projection, lightDirection);
        }

        public void DrawEnergyEffects(Matrix view, Matrix projection)
        {
            for (int i = 0; i < Rings.Count; i++)
                Rings[i]?.DrawEnergyEffect(view, projection);
        }

        private List<TradelaneRing> GetRoute(TradeLaneDirection direction) =>
            direction == TradeLaneDirection.Forward ? ForwardRings : ReverseRings;

        private bool OwnsEntryRing(TradelaneRing ring, TradeLaneDirection direction) =>
            ReferenceEquals(ring, GetEntryRing(direction));

        private bool IsRingOperational(int ringIndex)
        {
            if (ringIndex < 0 || ringIndex >= ForwardRings.Count || ringIndex >= ReverseRings.Count)
                return false;
            if (ForwardRings[ringIndex].IsDestroyed || ReverseRings[ringIndex].IsDestroyed)
                return false;
            return !_disruptions.TryGetValue(ringIndex, out TradeLaneDisruptionInfo info) || !info.IsActive;
        }

        private static float SanitizeProgress(float progress)
        {
            return float.IsNaN(progress) || float.IsInfinity(progress) ? 0f : MathHelper.Clamp(progress, 0f, 1f);
        }

        private static float SanitizeRingScale(float scale)
        {
            return float.IsNaN(scale) || float.IsInfinity(scale) || scale <= 0f
                ? 5f
                : MathHelper.Clamp(scale, 0.1f, 100f);
        }

        private static bool IsFiniteVector(Vector3 value) => TradeLaneStateSanitizer.IsFinite(value);
    }
}
