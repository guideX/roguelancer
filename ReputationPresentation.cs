using System;
using System.Collections.Generic;

namespace Roguelancer
{
    /// <summary>
    /// Pure, read-only presentation data shared by station and mission UI.
    /// It deliberately contains no mutation or formatting authority for saves.
    /// </summary>
    public static class ReputationPresentation
    {
        public static string BuildStationFactionLine(Station station, ReputationManager reputationManager)
        {
            string factionId = FactionManager.NormalizeFactionId(station?.FactionId);
            string factionName = reputationManager?.FactionManager.GetFaction(factionId).DisplayName
                ?? FactionManager.GetFactionDisplayName(factionId);
            return $"FACTION: {factionName}";
        }

        public static string BuildStationStandingLine(Station station, ReputationManager reputationManager)
        {
            string factionId = FactionManager.NormalizeFactionId(station?.FactionId);
            return $"STANDING: {FormatBand(reputationManager?.GetBand(factionId) ?? ReputationBand.Neutral)}";
        }

        public static string BuildMissionEmployerLine(Mission mission, ReputationManager reputationManager)
        {
            string factionId = FactionManager.NormalizeFactionId(mission?.FactionId);
            string factionName = reputationManager?.FactionManager.GetFaction(factionId).DisplayName
                ?? FactionManager.GetFactionDisplayName(factionId);
            return $"EMPLOYER: {factionName}";
        }

        public static string BuildMissionStandingLine(Mission mission, ReputationManager reputationManager)
        {
            string factionId = FactionManager.NormalizeFactionId(mission?.FactionId);
            return $"YOUR STANDING: {FormatBand(reputationManager?.GetBand(factionId) ?? ReputationBand.Neutral)}";
        }

        public static IReadOnlyList<ReputationOverviewLine> BuildOverview(ReputationManager reputationManager)
        {
            if (reputationManager == null)
                return Array.Empty<ReputationOverviewLine>();

            List<ReputationOverviewLine> lines = new();
            foreach (ReputationStandingEntry entry in reputationManager.GetOrderedStandings())
            {
                lines.Add(new ReputationOverviewLine(
                    entry.FactionId,
                    entry.DisplayName,
                    FormatBand(entry.Band),
                    entry.Value));
            }

            return lines;
        }

        public static string FormatBand(ReputationBand band) => band.ToString().ToUpperInvariant();
    }

    public readonly record struct ReputationOverviewLine(
        string FactionId,
        string DisplayName,
        string BandLabel,
        float Value);
}
