#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;

namespace Roguelancer
{
    public sealed class TemporaryHostilityChange
    {
        public string FactionId { get; init; } = FactionManager.NeutralCivilians;
        public string Reason { get; init; } = string.Empty;
        public bool IsActive { get; init; }
        public float RemainingSeconds { get; init; }
    }

    public readonly record struct TemporaryHostilitySnapshot(
        string FactionId,
        string Reason,
        float RemainingSeconds);

    /// <summary>
    /// Bounded, simulation-time-only aggression state. Persistent reputation
    /// remains in ReputationManager; this class only represents recent action.
    /// </summary>
    public sealed class TemporaryHostilityManager
    {
        public const float DefaultDurationSeconds = 60f;
        public const float MinimumDurationSeconds = 30f;
        public const float MaximumDurationSeconds = 120f;
        public const int MaximumActiveEntries = 32;

        private sealed class ActiveEntry
        {
            public string FactionId { get; init; } = FactionManager.NeutralCivilians;
            public string Reason { get; set; } = string.Empty;
            public float RemainingSeconds { get; set; }
        }

        private readonly Dictionary<string, ActiveEntry> _active = new(StringComparer.OrdinalIgnoreCase);

        public event Action<TemporaryHostilityChange> OnChanged = delegate { };

        public int ActiveCount => _active.Count;

        public IReadOnlyList<TemporaryHostilitySnapshot> GetActiveSnapshot()
        {
            return _active.Values
                .OrderBy(entry => entry.FactionId, StringComparer.OrdinalIgnoreCase)
                .Select(entry => new TemporaryHostilitySnapshot(
                    entry.FactionId,
                    entry.Reason,
                    Math.Max(0f, entry.RemainingSeconds)))
                .ToList();
        }

        public bool IsTemporarilyHostile(string? factionId)
        {
            string normalized = FactionManager.NormalizeFactionId(factionId);
            return _active.TryGetValue(normalized, out ActiveEntry? entry) && entry.RemainingSeconds > 0f;
        }

        public float GetRemainingSeconds(string? factionId)
        {
            string normalized = FactionManager.NormalizeFactionId(factionId);
            return _active.TryGetValue(normalized, out ActiveEntry? entry)
                ? Math.Max(0f, entry.RemainingSeconds)
                : 0f;
        }

        public bool RecordHostileAction(
            string? factionId,
            string reason = "recent hostile action",
            float durationSeconds = DefaultDurationSeconds)
        {
            string normalized = FactionManager.NormalizeFactionId(factionId);
            float boundedDuration = Math.Clamp(
                float.IsNaN(durationSeconds) || float.IsInfinity(durationSeconds)
                    ? DefaultDurationSeconds
                    : durationSeconds,
                MinimumDurationSeconds,
                MaximumDurationSeconds);

            if (!_active.TryGetValue(normalized, out ActiveEntry? entry))
            {
                if (_active.Count >= MaximumActiveEntries)
                {
                    ActiveEntry oldest = _active.Values
                        .OrderBy(candidate => candidate.RemainingSeconds)
                        .ThenBy(candidate => candidate.FactionId, StringComparer.OrdinalIgnoreCase)
                        .First();
                    _active.Remove(oldest.FactionId);
                }

                entry = new ActiveEntry
                {
                    FactionId = normalized,
                    Reason = string.IsNullOrWhiteSpace(reason) ? "recent hostile action" : reason.Trim(),
                    RemainingSeconds = boundedDuration
                };
                _active[normalized] = entry;
                Console.WriteLine($"[HOSTILITY] {normalized} active for {entry.RemainingSeconds:0.0}s");
                OnChanged?.Invoke(new TemporaryHostilityChange
                {
                    FactionId = normalized,
                    Reason = entry.Reason,
                    IsActive = true,
                    RemainingSeconds = entry.RemainingSeconds
                });
                return true;
            }

            entry.RemainingSeconds = boundedDuration;
            if (!string.IsNullOrWhiteSpace(reason))
                entry.Reason = reason.Trim();
            Console.WriteLine($"[HOSTILITY] {normalized} refreshed for {entry.RemainingSeconds:0.0}s");
            return false;
        }

        public void Update(float deltaSeconds)
        {
            float boundedDelta = Math.Max(0f, float.IsNaN(deltaSeconds) || float.IsInfinity(deltaSeconds) ? 0f : deltaSeconds);
            if (boundedDelta <= 0f || _active.Count == 0)
                return;

            List<string>? expiredFactionIds = null;
            foreach (ActiveEntry entry in _active.Values)
            {
                entry.RemainingSeconds -= boundedDelta;
                if (entry.RemainingSeconds > 0f)
                    continue;

                expiredFactionIds ??= new List<string>();
                expiredFactionIds.Add(entry.FactionId);
            }

            if (expiredFactionIds == null)
                return;

            foreach (string factionId in expiredFactionIds)
            {
                if (!_active.TryGetValue(factionId, out ActiveEntry? entry))
                    continue;

                _active.Remove(factionId);
                Console.WriteLine($"[HOSTILITY] {entry.FactionId} cleared");
                OnChanged?.Invoke(new TemporaryHostilityChange
                {
                    FactionId = entry.FactionId,
                    Reason = entry.Reason,
                    IsActive = false,
                    RemainingSeconds = 0f
                });
            }
        }

        public void RestoreSnapshot(IEnumerable<TemporaryHostilitySnapshot>? snapshots)
        {
            _active.Clear();
            if (snapshots == null)
                return;

            foreach (TemporaryHostilitySnapshot snapshot in snapshots
                .OrderBy(candidate => candidate.FactionId, StringComparer.OrdinalIgnoreCase)
                .Take(MaximumActiveEntries))
            {
                string normalized = FactionManager.NormalizeFactionId(snapshot.FactionId);
                if (snapshot.RemainingSeconds <= 0f ||
                    float.IsNaN(snapshot.RemainingSeconds) ||
                    float.IsInfinity(snapshot.RemainingSeconds))
                    continue;

                _active[normalized] = new ActiveEntry
                {
                    FactionId = normalized,
                    Reason = string.IsNullOrWhiteSpace(snapshot.Reason) ? "recent hostile action" : snapshot.Reason.Trim(),
                    RemainingSeconds = Math.Clamp(snapshot.RemainingSeconds, 0f, MaximumDurationSeconds)
                };
            }
        }

        public void Clear()
        {
            _active.Clear();
        }
    }
}
