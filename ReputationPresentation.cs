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
            ReputationBand band = reputationManager?.GetBand(factionId) ?? ReputationBand.Neutral;
            float value = reputationManager?.GetStanding(factionId) ?? 0f;
            string transient = reputationManager?.IsTemporarilyHostile(factionId) == true
                ? " | TEMPORARILY HOSTILE"
                : string.Empty;
            return $"STANDING: {FormatBand(band)} ({ReputationManager.FormatStanding(value)}){transient}";
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
            ReputationBand band = reputationManager?.GetBand(factionId) ?? ReputationBand.Neutral;
            float value = reputationManager?.GetStanding(factionId) ?? 0f;
            string transient = reputationManager?.IsTemporarilyHostile(factionId) == true
                ? " | TEMPORARILY HOSTILE"
                : string.Empty;
            return $"YOUR STANDING: {FormatBand(band)} ({ReputationManager.FormatStanding(value)}){transient}";
        }

        public static string BuildMissionRequirementLine(Mission mission)
        {
            if (mission == null || !mission.HasReputationRequirement)
                return "REQUIRED STANDING: NONE";

            if (mission.MinimumEmployerReputation.HasValue && mission.MaximumEmployerReputation.HasValue)
            {
                return $"REQUIRED STANDING: {FormatBand(ReputationManager.GetMinimumRequirementBand(mission.MinimumEmployerReputation.Value))} TO {FormatBand(ReputationManager.GetBandForStanding(mission.MaximumEmployerReputation.Value))}";
            }

            if (mission.MinimumEmployerReputation.HasValue)
            {
                return $"REQUIRED STANDING: {FormatBand(ReputationManager.GetMinimumRequirementBand(mission.MinimumEmployerReputation.Value))} ({ReputationManager.FormatStanding(mission.MinimumEmployerReputation.Value)} MIN)";
            }

            return $"REQUIRED STANDING: {FormatBand(ReputationManager.GetBandForStanding(mission.MaximumEmployerReputation!.Value))} OR LOWER ({ReputationManager.FormatStanding(mission.MaximumEmployerReputation.Value)} MAX)";
        }

        public static string BuildMissionRewardLine(Mission mission)
        {
            if (mission == null)
                return "REPUTATION REWARD: NONE";

            string employer = FactionManager.GetFactionDisplayName(mission.FactionId);
            return $"REPUTATION: {ReputationManager.FormatStanding(MissionManager.GetMissionReputationReward(mission))} {employer}";
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
                    entry.Value,
                    reputationManager.IsTemporarilyHostile(entry.FactionId)
                        ? "TEMPORARILY HOSTILE"
                        : string.Empty));
            }

            return lines;
        }

        public static string FormatBand(ReputationBand band) => band.ToString().ToUpperInvariant();
    }

    public readonly record struct ReputationOverviewLine(
        string FactionId,
        string DisplayName,
        string BandLabel,
        float Value,
        string TransientLabel);
}
