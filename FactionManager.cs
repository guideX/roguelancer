using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Globalization;

namespace Roguelancer
{
    /// <summary>
    /// Stores faction metadata and provides fallback behavior for unknown faction ids.
    /// </summary>
    public class FactionManager
    {
        public const string LibertyPolice = "liberty_police";
        public const string LibertyNavy = "liberty_navy";
        public const string LibertyRogues = "liberty_rogues";
        public const string LibertyCorporations = "liberty_corporations";
        public const string BountyHunters = "bounty_hunters";
        public const string Junkers = "junkers";
        public const string NeutralCivilians = "neutral_civilians";

        private readonly Dictionary<string, Faction> _factions = new(StringComparer.OrdinalIgnoreCase);

        public FactionManager()
        {
            RegisterDefaultFactions();
        }

        public IReadOnlyDictionary<string, Faction> Factions => _factions;

        public static string NormalizeFactionId(string? factionId)
        {
            return string.IsNullOrWhiteSpace(factionId) ? NeutralCivilians : factionId.Trim();
        }

        public static string GetFactionDisplayName(string? factionId)
        {
            string normalized = NormalizeFactionId(factionId);
            return PrettyDisplayName(normalized);
        }

        public static string CoalesceFactionId(params string?[] factionIds)
        {
            foreach (string? factionId in factionIds)
            {
                if (!string.IsNullOrWhiteSpace(factionId))
                {
                    return NormalizeFactionId(factionId);
                }
            }

            return NeutralCivilians;
        }

        public void RegisterFaction(Faction faction)
        {
            if (faction == null || string.IsNullOrWhiteSpace(faction.Id))
            {
                return;
            }

            _factions[faction.Id.Trim()] = faction;
        }

        public Faction GetFaction(string? factionId)
        {
            string normalized = NormalizeFactionId(factionId);
            if (_factions.TryGetValue(normalized, out var faction))
            {
                return faction;
            }

            return CreatePlaceholderFaction(normalized);
        }

        public bool TryGetFaction(string? factionId, out Faction faction)
        {
            string normalized = NormalizeFactionId(factionId);
            if (_factions.TryGetValue(normalized, out faction))
            {
                return true;
            }

            faction = CreatePlaceholderFaction(normalized);
            return false;
        }

        private void RegisterDefaultFactions()
        {
            RegisterFaction(new Faction
            {
                Id = LibertyPolice,
                DisplayName = "Liberty Police",
                Description = "System law enforcement and docking authority.",
                Color = Color.SteelBlue,
                IsLawful = true
            });

            RegisterFaction(new Faction
            {
                Id = LibertyNavy,
                DisplayName = "Liberty Navy",
                Description = "Military patrol and system defense forces.",
                Color = Color.Navy,
                IsLawful = true
            });

            RegisterFaction(new Faction
            {
                Id = LibertyRogues,
                DisplayName = "Liberty Rogues",
                Description = "Outlaws, smugglers, and opportunistic raiders.",
                Color = Color.IndianRed,
                IsCriminal = true
            });

            RegisterFaction(new Faction
            {
                Id = LibertyCorporations,
                DisplayName = "Liberty Corporations",
                Description = "Commercial interests and private trade groups.",
                Color = Color.Goldenrod,
                IsLawful = true
            });

            RegisterFaction(new Faction
            {
                Id = BountyHunters,
                DisplayName = "Bounty Hunters",
                Description = "Independent enforcement and contract hunters.",
                Color = Color.DarkOrange,
                IsLawful = true
            });

            RegisterFaction(new Faction
            {
                Id = Junkers,
                DisplayName = "Junkers",
                Description = "Scrappers, salvagers, and opportunistic scavengers.",
                Color = Color.SaddleBrown
            });

            RegisterFaction(new Faction
            {
                Id = NeutralCivilians,
                DisplayName = "Neutral Civilians",
                Description = "Unaffiliated freighters, traders, and travelers.",
                Color = Color.LightGray
            });
        }

        private static Faction CreatePlaceholderFaction(string factionId)
        {
            return new Faction
            {
                Id = factionId,
                DisplayName = PrettyDisplayName(factionId),
                Description = "Custom or unknown faction.",
                Color = Color.Gray
            };
        }

        private static string PrettyDisplayName(string factionId)
        {
            string cleaned = factionId.Replace('_', ' ');
            return CultureInfo.InvariantCulture.TextInfo.ToTitleCase(cleaned);
        }
    }
}
