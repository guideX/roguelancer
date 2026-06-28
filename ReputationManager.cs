using System;
using System.Collections.Generic;

namespace Roguelancer
{
    /// <summary>
    /// Tracks player reputation with factions and applies relationship ripples.
    /// </summary>
    public class ReputationManager
    {
        private readonly FactionManager _factionManager;
        private readonly Dictionary<string, float> _standing = new(StringComparer.OrdinalIgnoreCase);

        public ReputationManager(FactionManager factionManager)
        {
            _factionManager = factionManager ?? new FactionManager();
            SeedDefaultStandings();
        }

        public float GetStanding(string? factionId)
        {
            string normalized = FactionManager.NormalizeFactionId(factionId);
            if (_standing.TryGetValue(normalized, out float value))
            {
                return ClampStanding(value);
            }

            return 0f;
        }

        public void SetReputation(string? factionId, float value, string? reason = null)
        {
            string normalized = FactionManager.NormalizeFactionId(factionId);
            float clamped = ClampStanding(value);
            float previous = GetStanding(normalized);
            _standing[normalized] = clamped;
            LogStandingChange(normalized, previous, clamped, reason, "SET");
        }

        public void AddReputation(string? factionId, float delta, string? reason = null)
        {
            string normalized = FactionManager.NormalizeFactionId(factionId);
            ApplyDelta(normalized, delta, reason, isRipple: false);

            foreach (var ripple in FactionRelationshipMatrix.GetRippleTargets(normalized))
            {
                if (string.IsNullOrWhiteSpace(ripple.Key) || ripple.Key.Equals(normalized, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                float rippleDelta = delta * ripple.Value;
                if (Math.Abs(rippleDelta) < 0.0001f)
                {
                    continue;
                }

                ApplyDelta(ripple.Key, rippleDelta, reason, isRipple: true);
            }
        }

        public bool IsHostile(string? factionId) => GetStanding(factionId) <= -0.35f;
        public bool IsFriendly(string? factionId) => GetStanding(factionId) >= 0.35f;

        public string GetStandingLabel(string? factionId)
        {
            float standing = GetStanding(factionId);
            if (standing <= -0.35f) return "Hostile";
            if (standing >= 0.35f) return "Friendly";
            return "Neutral";
        }

        public string GetStandingSummary(string? factionId)
        {
            float standing = GetStanding(factionId);
            return $"{GetStandingLabel(factionId)} ({standing:+0.00;-0.00;0.00})";
        }

        public IReadOnlyDictionary<string, float> GetStandingsSnapshot()
        {
            return new Dictionary<string, float>(_standing, StringComparer.OrdinalIgnoreCase);
        }

        public void LoadStandings(IReadOnlyDictionary<string, float> standings)
        {
            _standing.Clear();
            SeedDefaultStandings();

            if (standings == null)
            {
                return;
            }

            foreach (var kvp in standings)
            {
                if (string.IsNullOrWhiteSpace(kvp.Key))
                {
                    continue;
                }

                _standing[FactionManager.NormalizeFactionId(kvp.Key)] = NormalizeStanding(kvp.Value);
            }

            Console.WriteLine($"[REPUTATION] Restored standings for {_standing.Count} factions");
        }

        private void SeedDefaultStandings()
        {
            foreach (var faction in _factionManager.Factions.Values)
            {
                float standing = faction.Id.Equals(FactionManager.LibertyRogues, StringComparison.OrdinalIgnoreCase)
                    || faction.IsCriminal
                    ? -0.50f
                    : faction.IsLawful
                        ? 0.45f
                        : 0.0f;

                _standing[FactionManager.NormalizeFactionId(faction.Id)] = standing;
            }

            // Keep civilians safely neutral by default.
            _standing[FactionManager.NeutralCivilians] = 0.0f;
        }

        private void ApplyDelta(string factionId, float delta, string? reason, bool isRipple)
        {
            float previous = GetStanding(factionId);
            float updated = ClampStanding(previous + delta);
            _standing[factionId] = updated;

            string rippleSuffix = isRipple ? " ripple" : string.Empty;
            LogStandingChange(factionId, previous, updated, reason, $"ADD{rippleSuffix}");
        }

        private static float ClampStanding(float value)
        {
            return Math.Clamp(value, -1f, 1f);
        }

        private static float NormalizeStanding(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
            {
                return 0f;
            }

            return ClampStanding(value);
        }

        private void LogStandingChange(string factionId, float previous, float current, string? reason, string action)
        {
            Faction faction = _factionManager.GetFaction(factionId);
            string reasonText = string.IsNullOrWhiteSpace(reason) ? string.Empty : $" | {reason}";
            Console.WriteLine($"[REPUTATION] {action} {faction.DisplayName} ({factionId}): {previous:+0.00;-0.00;0.00} -> {current:+0.00;-0.00;0.00}{reasonText}");
        }
    }
}
