using System;
using System.Collections.Generic;

namespace Roguelancer
{
    /// <summary>
    /// Defines how reputation changes ripple across allied and opposed factions.
    /// </summary>
    public static class FactionRelationshipMatrix
    {
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

        public static IReadOnlyDictionary<string, float> GetRippleTargets(string? factionId)
        {
            string normalized = FactionManager.NormalizeFactionId(factionId);
            return _matrix.TryGetValue(normalized, out var relationships) ? relationships : EmptyRelationships;
        }
    }
}
