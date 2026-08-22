#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Roguelancer
{
    public enum ReputationBand
    {
        Hostile,
        Unfriendly,
        Neutral,
        Friendly,
        Allied
    }

    public enum ReputationChangeReason
    {
        MissionCompleted,
        MissionFailed,
        FactionShipDestroyed,
        FactionShipAttacked,
        PoliceScan,
        PirateAmbushDefense,
        ManualDebug,
        Other
    }

    /// <summary>
    /// Immutable result for one direct or secondary standing mutation.
    /// Relationship bands are derived from the bounded numeric values.
    /// </summary>
    public sealed class ReputationChangeResult
    {
        public string FactionId { get; init; } = FactionManager.NeutralCivilians;
        public string FactionDisplayName { get; init; } = "Neutral Civilians";
        public float OldValue { get; init; }
        public float NewValue { get; init; }
        public float Delta { get; init; }
        public ReputationBand OldBand { get; init; }
        public ReputationBand NewBand { get; init; }
        public ReputationChangeReason Reason { get; init; }
        public bool IsSecondaryEffect { get; init; }
        public string SourceFactionId { get; init; } = string.Empty;

        public bool BandChanged => OldBand != NewBand;
        public bool Increased => Delta > 0f;
        public bool Decreased => Delta < 0f;
    }

    /// <summary>
    /// Authoritative mutable player standing. FactionManager remains the
    /// metadata/identity authority; this class owns only player state.
    /// </summary>
    public sealed class ReputationManager
    {
        public const float MinimumStanding = -1f;
        public const float MaximumStanding = 1f;
        public const float HostileThreshold = -0.60f;
        public const float UnfriendlyThreshold = -0.20f;
        public const float FriendlyThreshold = 0.20f;
        public const float AlliedThreshold = 0.60f;
        public const float Precision = 0.0001f;

        // Starting values intentionally keep Fort Bush and the ordinary game
        // loop accessible while establishing the protagonist's rogue identity.
        private static readonly IReadOnlyDictionary<string, float> StartingProfile
            = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase)
            {
                [FactionManager.LibertyRogues] = 0.35f,
                [FactionManager.LibertyPolice] = -0.25f,
                [FactionManager.LibertyNavy] = -0.22f,
                [FactionManager.LibertyCorporations] = 0.00f,
                [FactionManager.BountyHunters] = -0.05f,
                [FactionManager.Junkers] = 0.05f,
                [FactionManager.NeutralCivilians] = 0.00f
            };

        private readonly FactionManager _factionManager;
        private readonly Dictionary<string, float> _standing = new(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<NpcShip> _processedPlayerKills = new();

        public ReputationManager(FactionManager factionManager)
        {
            _factionManager = factionManager ?? new FactionManager();
            ResetToNewGame();
        }

        public FactionManager FactionManager => _factionManager;

        /// <summary>Raised only when a bounded value actually changes.</summary>
        public event Action<ReputationChangeResult> OnReputationChanged = delegate { };

        public IReadOnlyDictionary<string, float> GetStartingProfileSnapshot() =>
            new Dictionary<string, float>(StartingProfile, StringComparer.OrdinalIgnoreCase);

        public float GetStanding(string? factionId)
        {
            string normalized = FactionManager.NormalizeFactionId(factionId);
            return _standing.TryGetValue(normalized, out float value)
                ? NormalizeStoredStanding(value)
                : 0f;
        }

        public ReputationBand GetBand(string? factionId) => GetBandForStanding(GetStanding(factionId));

        public string GetStandingLabel(string? factionId) => GetBand(factionId).ToString();

        public string GetStandingSummary(string? factionId)
        {
            float standing = GetStanding(factionId);
            return $"{GetStandingLabel(factionId)} ({standing:+0.00;-0.00;0.00})";
        }

        public bool IsHostile(string? factionId) => GetBand(factionId) == ReputationBand.Hostile;
        public bool IsFriendly(string? factionId) => GetBand(factionId) is ReputationBand.Friendly or ReputationBand.Allied;
        public bool IsAllied(string? factionId) => GetBand(factionId) == ReputationBand.Allied;
        public bool MeetsRequirement(string? factionId, float minimumStanding) =>
            GetStanding(factionId) + Precision >= NormalizeRequirement(minimumStanding);
        public bool MeetsReputationRequirement(string? factionId, float minimumStanding) =>
            MeetsRequirement(factionId, minimumStanding);
        public bool CanDockWithFaction(string? factionId) => !IsHostile(factionId);

        /// <summary>
        /// Sets a standing directly for tests, migration, and controlled setup.
        /// Gameplay mutations should use AdjustReputation/AddReputation.
        /// </summary>
        public ReputationChangeResult? SetReputation(string? factionId, float value, string? reason = null)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
                return null;

            string normalized = FactionManager.NormalizeFactionId(factionId);
            float previous = GetStanding(normalized);
            float updated = QuantizeStanding(value);
            return CommitChange(normalized, previous, updated, ParseReason(reason), false, normalized);
        }

        public ReputationChangeResult? AdjustReputation(
            string? factionId,
            float delta,
            ReputationChangeReason reason = ReputationChangeReason.ManualDebug)
        {
            return AdjustReputationInternal(factionId, delta, reason, applySecondary: true, isSecondary: false, sourceFactionId: null);
        }

        // Compatibility entry point used by existing gameplay systems.
        public ReputationChangeResult? AddReputation(string? factionId, float delta, string? reason = null) =>
            AdjustReputation(factionId, delta, ParseReason(reason));

        public ReputationChangeResult? AddReputation(string? factionId, float delta, ReputationChangeReason reason) =>
            AdjustReputation(factionId, delta, reason);

        /// <summary>
        /// Applies a single authoritative player-kill penalty. Attribution is
        /// accepted only from the NPC's actual player-damage marker, and the
        /// instance is deduplicated so duplicate destruction callbacks cannot
        /// award repeated penalties.
        /// </summary>
        public ReputationChangeResult? ApplyPlayerShipDestroyed(NpcShip destroyedShip)
        {
            if (destroyedShip == null || !destroyedShip.WasDamagedByPlayer || !_processedPlayerKills.Add(destroyedShip))
                return null;

            return AdjustReputation(
                destroyedShip.FactionId,
                -0.10f,
                ReputationChangeReason.FactionShipDestroyed);
        }

        public void ResetToNewGame()
        {
            _standing.Clear();
            _processedPlayerKills.Clear();

            foreach (Faction faction in _factionManager.Factions.Values)
            {
                string id = FactionManager.NormalizeFactionId(faction.Id);
                _standing[id] = GetStartingValue(id, faction);
            }

            foreach (KeyValuePair<string, float> entry in StartingProfile)
                _standing[FactionManager.NormalizeFactionId(entry.Key)] = QuantizeStanding(entry.Value);
        }

        /// <summary>
        /// Loads saved numeric values over a fresh profile. Missing/invalid
        /// entries keep the new-game default; unknown valid IDs are preserved.
        /// </summary>
        public void LoadStandings(IReadOnlyDictionary<string, float>? standings)
        {
            ResetToNewGame();
            if (standings == null)
                return;

            foreach (KeyValuePair<string, float> entry in standings)
            {
                if (string.IsNullOrWhiteSpace(entry.Key) || float.IsNaN(entry.Value) || float.IsInfinity(entry.Value))
                    continue;

                _standing[FactionManager.NormalizeFactionId(entry.Key)] = QuantizeStanding(entry.Value);
            }

            Console.WriteLine($"[REPUTATION] Restored standings for {_standing.Count} factions");
        }

        public IReadOnlyDictionary<string, float> GetStandingsSnapshot() =>
            new Dictionary<string, float>(_standing, StringComparer.OrdinalIgnoreCase);

        public IReadOnlyList<ReputationStandingEntry> GetOrderedStandings()
        {
            return _standing.Keys
                .Select(id => new ReputationStandingEntry(
                    id,
                    _factionManager.GetFaction(id).DisplayName,
                    GetStanding(id),
                    GetBand(id)))
                .OrderBy(entry => entry.DisplayName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(entry => entry.FactionId, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        public static ReputationBand GetBandForStanding(float standing)
        {
            float normalized = NormalizeStoredStanding(standing);
            if (normalized <= HostileThreshold) return ReputationBand.Hostile;
            if (normalized < UnfriendlyThreshold) return ReputationBand.Unfriendly;
            if (normalized <= FriendlyThreshold) return ReputationBand.Neutral;
            if (normalized < AlliedThreshold) return ReputationBand.Friendly;
            return ReputationBand.Allied;
        }

        public static string FormatStanding(float standing) =>
            NormalizeStoredStanding(standing).ToString("+0.00;-0.00;0.00", CultureInfo.InvariantCulture);

        private ReputationChangeResult? AdjustReputationInternal(
            string? factionId,
            float delta,
            ReputationChangeReason reason,
            bool applySecondary,
            bool isSecondary,
            string? sourceFactionId)
        {
            if (float.IsNaN(delta) || float.IsInfinity(delta) || Math.Abs(delta) < Precision)
                return null;

            string normalized = FactionManager.NormalizeFactionId(factionId);
            float previous = GetStanding(normalized);
            float updated = QuantizeStanding(previous + delta);
            ReputationChangeResult? result = CommitChange(
                normalized,
                previous,
                updated,
                reason,
                isSecondary,
                sourceFactionId ?? normalized);

            if (applySecondary)
            {
                foreach (KeyValuePair<string, float> ripple in FactionRelationshipMatrix.GetRippleTargets(normalized))
                {
                    if (string.IsNullOrWhiteSpace(ripple.Key) ||
                        ripple.Key.Equals(normalized, StringComparison.OrdinalIgnoreCase))
                        continue;

                    float rippleDelta = delta * ripple.Value;
                    if (Math.Abs(rippleDelta) < Precision)
                        continue;

                    AdjustReputationInternal(
                        ripple.Key,
                        rippleDelta,
                        reason,
                        applySecondary: false,
                        isSecondary: true,
                        sourceFactionId: normalized);
                }
            }

            return result;
        }

        private ReputationChangeResult? CommitChange(
            string factionId,
            float previous,
            float updated,
            ReputationChangeReason reason,
            bool isSecondary,
            string sourceFactionId)
        {
            float normalizedPrevious = QuantizeStanding(previous);
            float normalizedUpdated = QuantizeStanding(updated);
            if (Math.Abs(normalizedUpdated - normalizedPrevious) < Precision)
                return null;

            _standing[factionId] = normalizedUpdated;
            ReputationChangeResult result = new()
            {
                FactionId = factionId,
                FactionDisplayName = _factionManager.GetFaction(factionId).DisplayName,
                OldValue = normalizedPrevious,
                NewValue = normalizedUpdated,
                Delta = QuantizeStanding(normalizedUpdated - normalizedPrevious),
                OldBand = GetBandForStanding(normalizedPrevious),
                NewBand = GetBandForStanding(normalizedUpdated),
                Reason = reason,
                IsSecondaryEffect = isSecondary,
                SourceFactionId = sourceFactionId ?? string.Empty
            };

            string secondary = isSecondary ? " secondary" : string.Empty;
            Console.WriteLine($"[REPUTATION] {factionId} {FormatStanding(normalizedPrevious)} -> {FormatStanding(normalizedUpdated)} ({FormatStanding(result.Delta)}, {reason}{secondary})");
            OnReputationChanged?.Invoke(result);
            return result;
        }

        private static float GetStartingValue(string factionId, Faction faction)
        {
            if (StartingProfile.TryGetValue(factionId, out float configured))
                return QuantizeStanding(configured);

            return faction?.IsCriminal == true ? -0.05f : 0f;
        }

        private static float NormalizeRequirement(float minimumStanding)
        {
            if (float.IsNaN(minimumStanding) || float.IsInfinity(minimumStanding))
                return MaximumStanding;

            return QuantizeStanding(minimumStanding);
        }

        private static float QuantizeStanding(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
                return 0f;

            return Math.Clamp(MathF.Round(value, 4, MidpointRounding.AwayFromZero), MinimumStanding, MaximumStanding);
        }

        private static float NormalizeStoredStanding(float value) => QuantizeStanding(value);

        private static ReputationChangeReason ParseReason(string? reason)
        {
            if (string.IsNullOrWhiteSpace(reason)) return ReputationChangeReason.ManualDebug;
            if (Enum.TryParse(reason.Replace(" ", string.Empty), ignoreCase: true, out ReputationChangeReason parsed))
                return parsed;
            if (reason.Contains("mission", StringComparison.OrdinalIgnoreCase) && reason.Contains("reward", StringComparison.OrdinalIgnoreCase))
                return ReputationChangeReason.MissionCompleted;
            if (reason.Contains("mission", StringComparison.OrdinalIgnoreCase) && reason.Contains("fail", StringComparison.OrdinalIgnoreCase))
                return ReputationChangeReason.MissionFailed;
            if (reason.Contains("police", StringComparison.OrdinalIgnoreCase) || reason.Contains("scan", StringComparison.OrdinalIgnoreCase))
                return ReputationChangeReason.PoliceScan;
            if (reason.Contains("pirate", StringComparison.OrdinalIgnoreCase))
                return ReputationChangeReason.PirateAmbushDefense;
            return ReputationChangeReason.Other;
        }
    }

    public readonly record struct ReputationStandingEntry(
        string FactionId,
        string DisplayName,
        float Value,
        ReputationBand Band);
}
