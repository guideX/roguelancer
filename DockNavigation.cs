using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Roguelancer
{
    internal sealed class DockAssistHudData
    {
        public Station Station { get; set; }
        public string HeaderLabel { get; set; } = "DOCK ASSIST";
        public string StationLabel { get; set; } = string.Empty;
        public string DistanceLabel { get; set; } = string.Empty;
        public string DockRangeLabel { get; set; } = string.Empty;
        public string RangeDeltaLabel { get; set; } = string.Empty;
        public string GuidanceLabel { get; set; } = string.Empty;
        public bool InRange { get; set; }
        public float DistanceToStation { get; set; }
        public float DistanceToDockRange { get; set; }
    }

    internal static class DockNavigation
    {
        public static bool TryResolveNearestDockableStation(
            IReadOnlyList<Station> stations,
            Vector3 playerPosition,
            ReputationManager reputationManager,
            out Station nearestStation,
            out float distance,
            out string failureReason)
        {
            nearestStation = null;
            distance = 0f;
            failureReason = string.Empty;

            List<Station> dockableStations = GetDockableStationsSortedByDistance(stations, playerPosition, reputationManager);
            if (dockableStations.Count == 0)
            {
                failureReason = "no dockable station found";
                return false;
            }

            nearestStation = dockableStations[0];
            distance = Vector3.Distance(playerPosition, nearestStation.Position);
            return true;
        }

        public static List<Station> GetStationsSortedByDistance(IReadOnlyList<Station> stations, Vector3 playerPosition)
        {
            if (stations == null || stations.Count == 0)
            {
                return new List<Station>();
            }

            return stations
                .Where(station => station != null)
                .OrderBy(station => Vector3.Distance(playerPosition, station.Position))
                .ThenBy(station => station.Name ?? string.Empty, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        public static List<Station> GetDockableStationsSortedByDistance(
            IReadOnlyList<Station> stations,
            Vector3 playerPosition,
            ReputationManager reputationManager)
        {
            if (stations == null || stations.Count == 0)
            {
                return new List<Station>();
            }

            return stations
                .Where(station => station != null)
                .Where(station => IsDockableStation(station, reputationManager))
                .OrderBy(station => Vector3.Distance(playerPosition, station.Position))
                .ThenBy(station => station.Name ?? string.Empty, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        public static bool TryBuildDockAssistHudData(
            Station station,
            Vector3 playerPosition,
            out DockAssistHudData hudData,
            out string failureReason)
        {
            hudData = null;
            failureReason = string.Empty;

            if (station == null)
            {
                failureReason = "station was null";
                return false;
            }

            float distance = Vector3.Distance(playerPosition, station.Position);
            float dockRange = Math.Max(0f, station.DockingRange);
            float remaining = Math.Max(distance - dockRange, 0f);
            bool inRange = distance <= dockRange;

            hudData = new DockAssistHudData
            {
                Station = station,
                StationLabel = $"Approaching {station.Name}",
                DistanceLabel = $"Distance: {FormatDistance(distance)}",
                DockRangeLabel = $"Dock range: {FormatDistance(dockRange)}",
                RangeDeltaLabel = inRange
                    ? "Within dock range"
                    : $"To dock: {FormatDistance(remaining)} remaining",
                GuidanceLabel = inRange
                    ? "Press F3 to dock"
                    : $"Too far: approach to within {FormatDistance(dockRange)}",
                InRange = inRange,
                DistanceToStation = distance,
                DistanceToDockRange = remaining
            };

            return true;
        }

        public static bool IsDockableStation(Station station, ReputationManager reputationManager)
        {
            if (station == null)
            {
                return false;
            }

            string factionId = FactionManager.NormalizeFactionId(station.FactionId);
            if (reputationManager != null && reputationManager.IsHostile(factionId))
            {
                return false;
            }

            return true;
        }

        private static string FormatDistance(float distance)
        {
            if (distance >= 1000f)
            {
                return $"{distance / 1000f:F1} km";
            }

            return $"{distance:F0} m";
        }
    }
}
