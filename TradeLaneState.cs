using Microsoft.Xna.Framework;
using System;

namespace Roguelancer
{
    /// <summary>
    /// Stable direction state for a traveler on one logical trade-lane route.
    /// </summary>
    public enum TradeLaneDirection
    {
        Forward = 1,
        Reverse = -1
    }

    public enum TradeLaneDisruptionState
    {
        Operational,
        Disrupted,
        Recovering
    }

    public enum TradeLaneDisruptionSource
    {
        Unknown,
        Player,
        Npc,
        Environment
    }

    public enum TradeLaneTransitExitReason
    {
        Completed,
        Manual,
        HostileDamage,
        Disrupted,
        Destroyed,
        Despawned,
        InvalidState
    }

    /// <summary>
    /// Authoritative, transient world-state information for one disrupted ring.
    /// This is intentionally not save data; it is the query seam for future
    /// disruption missions.
    /// </summary>
    public sealed class TradeLaneDisruptionInfo
    {
        public string LaneId { get; internal set; } = string.Empty;
        public string LaneName { get; internal set; } = string.Empty;
        public int RingIndex { get; internal set; } = -1;
        public string SegmentId { get; internal set; } = string.Empty;
        public TradeLaneDisruptionState State { get; internal set; } = TradeLaneDisruptionState.Operational;
        public TradeLaneDisruptionSource Source { get; internal set; } = TradeLaneDisruptionSource.Unknown;
        public string SourceName { get; internal set; } = string.Empty;
        public float DamageAccumulated { get; internal set; }
        public float RecoveryDurationSeconds { get; internal set; }
        public float RemainingRecoverySeconds { get; internal set; }
        public double OccurredAtSeconds { get; internal set; }

        public bool IsActive => State != TradeLaneDisruptionState.Operational;
        public bool IsRecovering => State == TradeLaneDisruptionState.Recovering;
    }

    /// <summary>
    /// Snapshot of a traveler state. The lane owns the mutable state and
    /// exposes this read-only copy for HUD, tests, and future mission logic.
    /// </summary>
    public sealed class TradeLaneTransitSnapshot
    {
        public object Traveler { get; internal set; }
        public string LaneId { get; internal set; } = string.Empty;
        public TradeLaneDirection Direction { get; internal set; }
        public int CurrentRingIndex { get; internal set; } = -1;
        public int NextRingIndex { get; internal set; } = -1;
        public float SegmentProgress { get; internal set; }
        public Vector3 Position { get; internal set; }
        public Vector3 Velocity { get; internal set; }
        public bool IsPlayer { get; internal set; }
    }

    /// <summary>
    /// One completed, aborted, or otherwise ejected lane traveler.
    /// </summary>
    public sealed class TradeLaneTransitEvent
    {
        public object Traveler { get; internal set; }
        public string LaneId { get; internal set; } = string.Empty;
        public TradeLaneDirection Direction { get; internal set; }
        public int RingIndex { get; internal set; } = -1;
        public Vector3 Position { get; internal set; }
        public Vector3 Velocity { get; internal set; }
        public TradeLaneTransitExitReason Reason { get; internal set; }
        public bool IsCompleted => Reason == TradeLaneTransitExitReason.Completed;
    }

    internal static class TradeLaneStateSanitizer
    {
        public const float MaximumAcceptedElapsedSeconds = 60f;

        public static float Elapsed(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value) || value <= 0f)
                return 0f;
            return Math.Min(value, MaximumAcceptedElapsedSeconds);
        }

        public static float Positive(float value, float fallback)
        {
            return float.IsNaN(value) || float.IsInfinity(value) || value <= 0f ? fallback : value;
        }

        public static float NonNegative(float value)
        {
            return float.IsNaN(value) || float.IsInfinity(value) || value < 0f ? 0f : value;
        }

        public static bool IsFinite(Vector3 value)
        {
            return IsFinite(value.X) && IsFinite(value.Y) && IsFinite(value.Z);
        }

        public static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
