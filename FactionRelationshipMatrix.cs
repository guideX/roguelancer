#nullable enable

using System;
using System.Collections.Generic;

namespace Roguelancer
{
    public enum FactionRelationshipKind
    {
        Neutral,
        Allied,
        Hostile
    }

    public readonly record struct FactionRelationshipDefinition(
        string SourceFactionId,
        string TargetFactionId,
        FactionRelationshipKind Kind);

    /// <summary>
    /// Stores the pre-existing broad reputation-ripple coefficients used by
    /// the original reputation foundation. Phase 31 combat consequences use
    /// the typed first-order relationship definitions below instead, so a
    /// mission or bribe cannot accidentally award an enemy-kill reward.
    /// </summary>
    public static class FactionRelationshipMatrix
    {
        private static readonly FactionRelationshipDefinition[] CombatRelationships =
        {
            new(FactionManager.LibertyPolice, FactionManager.LibertyRogues, FactionRelationshipKind.Hostile),
            new(FactionManager.LibertyRogues, FactionManager.LibertyPolice, FactionRelationshipKind.Hostile),
            // Corporation security details use the ordinary faction combat
            // pipeline against the same Rogue NPC threat that can attack a
            // protected merchant.
            new(FactionManager.LibertyCorporations, FactionManager.LibertyRogues, FactionRelationshipKind.Hostile),
            new(FactionManager.LibertyRogues, FactionManager.LibertyCorporations, FactionRelationshipKind.Hostile)
        };

        private static readonly IReadOnlyList<FactionRelationshipDefinition> EmptyCombatRelationships =
            Array.Empty<FactionRelationshipDefinition>();

        private static readonly IReadOnlyDictionary<string, IReadOnlyDictionary<string, float>> _matrix
            = new Dictionary<string, IReadOnlyDictionary<string, float>>(StringComparer.OrdinalIgnoreCase)
        {
            [FactionManager.LibertyPolice] = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase)
            {
                [FactionManager.LibertyNavy] = 0.30f,
                [FactionManager.LibertyCorporations] = 0.20f,
                [FactionManager.BountyHunters] = 0.15f,
                [FactionManager.LibertyRogues] = -0.60f,
                [FactionManager.Junkers] = -0.10f,
                [FactionManager.NeutralCivilians] = 0.05f
            },
            [FactionManager.LibertyNavy] = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase)
            {
                [FactionManager.LibertyPolice] = 0.30f,
                [FactionManager.LibertyCorporations] = 0.15f,
                [FactionManager.BountyHunters] = 0.10f,
                [FactionManager.LibertyRogues] = -0.55f,
                [FactionManager.Junkers] = -0.10f,
                [FactionManager.NeutralCivilians] = 0.05f
            },
            [FactionManager.LibertyRogues] = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase)
            {
                [FactionManager.LibertyPolice] = -0.60f,
                [FactionManager.LibertyNavy] = -0.55f,
                [FactionManager.LibertyCorporations] = -0.35f,
                [FactionManager.BountyHunters] = -0.50f,
                [FactionManager.Junkers] = 0.20f,
                [FactionManager.NeutralCivilians] = -0.05f
            },
            [FactionManager.LibertyCorporations] = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase)
            {
                [FactionManager.LibertyPolice] = 0.15f,
                [FactionManager.LibertyNavy] = 0.15f,
                [FactionManager.BountyHunters] = 0.10f,
                [FactionManager.LibertyRogues] = -0.35f,
                [FactionManager.Junkers] = -0.05f,
                [FactionManager.NeutralCivilians] = 0.05f
            },
            [FactionManager.BountyHunters] = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase)
            {
                [FactionManager.LibertyPolice] = 0.15f,
                [FactionManager.LibertyNavy] = 0.10f,
                [FactionManager.LibertyCorporations] = 0.10f,
                [FactionManager.LibertyRogues] = -0.60f,
                [FactionManager.Junkers] = -0.05f,
                [FactionManager.NeutralCivilians] = 0.05f
            },
            [FactionManager.Junkers] = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase)
            {
                [FactionManager.LibertyPolice] = -0.10f,
                [FactionManager.LibertyNavy] = -0.10f,
                [FactionManager.LibertyCorporations] = -0.05f,
                [FactionManager.BountyHunters] = 0.05f,
                [FactionManager.LibertyRogues] = 0.15f,
                [FactionManager.NeutralCivilians] = 0.10f
            },
            [FactionManager.NeutralCivilians] = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase)
            {
                [FactionManager.LibertyPolice] = 0.05f,
                [FactionManager.LibertyNavy] = 0.05f,
                [FactionManager.LibertyCorporations] = 0.05f,
                [FactionManager.BountyHunters] = 0.05f,
                [FactionManager.Junkers] = 0.10f,
                [FactionManager.LibertyRogues] = -0.10f
            }
        };

        private static readonly IReadOnlyDictionary<string, float> EmptyRelationships
            = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Returns the static faction-to-faction relationship used by the
        /// Phase 31 combat policy. The configured Police/Rogue and
        /// Corporation/Rogue pairs are stored in both directions; unknown and
        /// unconfigured pairs are Neutral.
        /// This is faction metadata, not player reputation and not persisted.
        /// </summary>
        public static FactionRelationshipKind GetRelationship(string? sourceFactionId, string? targetFactionId)
        {
            string source = FactionManager.NormalizeFactionId(sourceFactionId);
            string target = FactionManager.NormalizeFactionId(targetFactionId);

            foreach (FactionRelationshipDefinition relationship in CombatRelationships)
            {
                if (relationship.SourceFactionId.Equals(source, StringComparison.OrdinalIgnoreCase) &&
                    relationship.TargetFactionId.Equals(target, StringComparison.OrdinalIgnoreCase))
                {
                    return relationship.Kind;
                }
            }

            return FactionRelationshipKind.Neutral;
        }

        public static IReadOnlyList<FactionRelationshipDefinition> GetCombatRelationshipsFrom(string? sourceFactionId)
        {
            string source = FactionManager.NormalizeFactionId(sourceFactionId);
            List<FactionRelationshipDefinition> result = new();
            foreach (FactionRelationshipDefinition relationship in CombatRelationships)
            {
                if (relationship.SourceFactionId.Equals(source, StringComparison.OrdinalIgnoreCase))
                    result.Add(relationship);
            }

            return result.Count == 0 ? EmptyCombatRelationships : result;
        }

        public static IReadOnlyDictionary<string, float> GetRippleTargets(string? factionId)
        {
            string normalized = FactionManager.NormalizeFactionId(factionId);
            return _matrix.TryGetValue(normalized, out var relationships) ? relationships : EmptyRelationships;
        }
    }
}
