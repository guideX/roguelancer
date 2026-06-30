using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Roguelancer
{
    internal enum NavTargetKind
    {
        Unknown,
        Ship,
        Station,
        CargoPod
    }

    internal sealed class NavTargetHudData
    {
        public object Target { get; set; }
        public SpaceObject SpaceTarget { get; set; }
        public CargoPod CargoTarget { get; set; }
        public Mission MissionContext { get; set; }
        public NavTargetKind Kind { get; set; } = NavTargetKind.Unknown;
        public string Name { get; set; } = string.Empty;
        public string TypeLabel { get; set; } = string.Empty;
        public string MissionLabel { get; set; } = string.Empty;
        public string FactionId { get; set; } = string.Empty;
        public string FactionLabel { get; set; } = string.Empty;
        public string StandingLabel { get; set; } = string.Empty;
        public string DistanceLabel { get; set; } = string.Empty;
        public string StatusLabel { get; set; } = string.Empty;
        public string IntegrityLabel { get; set; } = string.Empty;
        public Color AccentColor { get; set; } = Color.White;
        public bool CanGoto { get; set; }
        public bool IsResolved { get; set; }
    }

    internal static class NavTargeting
    {
        public static bool TryResolveMissionObjective(
            Mission mission,
            IReadOnlyList<SpaceObject> spaceObjects,
            IReadOnlyList<NpcShip> npcShips,
            out SpaceObject resolvedTarget,
            out string statusText)
        {
            resolvedTarget = null;
            statusText = string.Empty;

            if (mission == null)
            {
                statusText = "mission was null";
                return false;
            }

            if (mission.TargetSpaceObject != null)
            {
                if (mission.Type == MissionType.Bounty)
                {
                    if (mission.TargetSpaceObject is NpcShip boundNpc && !boundNpc.IsDestroyed)
                    {
                        resolvedTarget = boundNpc;
                        statusText = "bounty target resolved";
                        return true;
                    }
                }
                else if (mission.Type == MissionType.Delivery)
                {
                    if (mission.TargetSpaceObject is Station boundStation)
                    {
                        resolvedTarget = boundStation;
                        statusText = "delivery destination resolved";
                        return true;
                    }
                }
                else if (mission.Type == MissionType.Escort)
                {
                    if (mission.TargetSpaceObject is NpcShip escortNpc && !escortNpc.IsDestroyed)
                    {
                        resolvedTarget = escortNpc;
                        statusText = "escort target resolved";
                        return true;
                    }

                    if (mission.TargetSpaceObject is Station escortDestination)
                    {
                        resolvedTarget = escortDestination;
                        statusText = "escort destination resolved";
                        return true;
                    }
                }
                else
                {
                    resolvedTarget = mission.TargetSpaceObject;
                    statusText = "mission objective resolved";
                    return true;
                }
            }

            IReadOnlyList<SpaceObject> safeSpaceObjects = spaceObjects ?? Array.Empty<SpaceObject>();
            IReadOnlyList<NpcShip> safeNpcShips = npcShips ?? Array.Empty<NpcShip>();

            if (mission.Type == MissionType.Bounty)
            {
                resolvedTarget = ResolveNpcTarget(mission.Target, safeNpcShips, safeSpaceObjects);
                if (resolvedTarget != null)
                {
                    statusText = "bounty target resolved";
                    return true;
                }

                statusText = mission.GetHudFallbackLine();
                return false;
            }

            if (mission.Type == MissionType.Delivery)
            {
                resolvedTarget = ResolveStationTarget(mission.Destination, safeSpaceObjects);
                if (resolvedTarget != null)
                {
                    statusText = "delivery destination resolved";
                    return true;
                }

                statusText = mission.GetHudFallbackLine();
                return false;
            }

            if (mission.Type == MissionType.Escort)
            {
                resolvedTarget = ResolveNpcTarget(mission.Target, safeNpcShips, safeSpaceObjects);
                if (resolvedTarget != null)
                {
                    statusText = "escort target resolved";
                    return true;
                }

                resolvedTarget = ResolveStationTarget(mission.Destination, safeSpaceObjects);
                if (resolvedTarget != null)
                {
                    statusText = "escort destination resolved";
                    return true;
                }

                statusText = mission.GetHudFallbackLine();
                return false;
            }

            statusText = "mission objective unavailable";
            return false;
        }

        public static bool TryStartGotoToMissionObjective(
            Ship playerShip,
            Mission mission,
            IReadOnlyList<SpaceObject> spaceObjects,
            IReadOnlyList<NpcShip> npcShips,
            out string failureReason)
        {
            failureReason = string.Empty;

            if (playerShip == null)
            {
                failureReason = "player ship unavailable";
                return false;
            }

            if (!TryResolveMissionObjective(mission, spaceObjects, npcShips, out SpaceObject resolvedTarget, out string statusText) ||
                resolvedTarget == null)
            {
                failureReason = string.IsNullOrWhiteSpace(statusText)
                    ? "mission objective could not be resolved"
                    : statusText;
                return false;
            }

            playerShip.ActivateGoto(resolvedTarget);
            return true;
        }

        public static bool TryBuildHudData(
            object target,
            Vector3 playerPosition,
            ReputationManager reputationManager,
            FactionManager factionManager,
            Mission missionContext,
            out NavTargetHudData hudData,
            out string failureReason)
        {
            hudData = null;
            failureReason = string.Empty;

            if (target == null)
            {
                failureReason = "target was null";
                return false;
            }

            factionManager ??= new FactionManager();

            if (target is CargoPod cargoPod)
            {
                hudData = BuildCargoPodHudData(cargoPod, playerPosition);
                if (missionContext != null)
                {
                    hudData.MissionContext = missionContext;
                    hudData.MissionLabel = $"Mission: {missionContext.GetTypeLabel()}";
                }

                return true;
            }

            if (target is SpaceObject spaceTarget)
            {
                hudData = BuildSpaceObjectHudData(spaceTarget, playerPosition, reputationManager, factionManager, missionContext);
                return true;
            }

            failureReason = "unsupported nav target type";
            return false;
        }

        public static Mission FindMissionForTarget(MissionManager missionManager, object target)
        {
            if (missionManager == null || target == null)
            {
                return null;
            }

            if (target is SpaceObject spaceTarget)
            {
                return missionManager.ActiveMissions.FirstOrDefault(m =>
                    m != null &&
                    m.Status == MissionStatus.Active &&
                    ReferenceEquals(m.TargetSpaceObject, spaceTarget));
            }

            return null;
        }

        private static NavTargetHudData BuildCargoPodHudData(CargoPod cargoPod, Vector3 playerPosition)
        {
            Commodity commodity = cargoPod.GetCommodity();
            float distance = Vector3.Distance(playerPosition, cargoPod.Position);

            return new NavTargetHudData
            {
                Target = cargoPod,
                CargoTarget = cargoPod,
                Kind = NavTargetKind.CargoPod,
                Name = commodity != null ? $"{commodity.Name} x{cargoPod.Quantity}" : "Cargo Pod",
                TypeLabel = "Cargo Pod",
                MissionLabel = string.Empty,
                FactionId = FactionManager.NeutralCivilians,
                FactionLabel = "Neutral Civilians",
                StandingLabel = "Neutral (0.00)",
                DistanceLabel = FormatDistance(distance),
                StatusLabel = commodity != null
                    ? $"Tractor cargo: {commodity.Name} x{cargoPod.Quantity}"
                    : "Unknown cargo",
                IntegrityLabel = "No hull data",
                AccentColor = commodity?.DisplayColor ?? Color.LightSkyBlue,
                CanGoto = false,
                IsResolved = true
            };
        }

        private static NavTargetHudData BuildSpaceObjectHudData(
            SpaceObject spaceTarget,
            Vector3 playerPosition,
            ReputationManager reputationManager,
            FactionManager factionManager,
            Mission missionContext)
        {
            string factionId = ResolveFactionId(spaceTarget, factionManager);
            Faction faction = factionManager.GetFaction(factionId);
            string standing = reputationManager?.GetStandingSummary(factionId) ?? "Neutral (0.00)";
            float distance = Vector3.Distance(playerPosition, spaceTarget.Position);

            NavTargetKind kind = NavTargetKind.Unknown;
            string typeLabel = "Object";
            string statusLabel = "Targetable object";
            string integrityLabel = "No hull data";
            Color accentColor = faction.Color;
            bool canGoto = true;

            if (spaceTarget is NpcShip npcTarget)
            {
                kind = NavTargetKind.Ship;
                typeLabel = "Ship";
                statusLabel = npcTarget.IsDestroyed ? "Destroyed" : npcTarget.IsTrafficEngaged ? "Engaged" : "Active";
                integrityLabel = $"Hull {npcTarget.Hull.HullPercentage:P0} | Shields {npcTarget.Shields.ShieldPercentage:P0}";
                accentColor = reputationManager != null && reputationManager.IsHostile(factionId)
                    ? Color.IndianRed
                    : reputationManager != null && reputationManager.IsFriendly(factionId)
                        ? Color.LightGreen
                        : faction.Color;
            }
            else if (spaceTarget is Station stationTarget)
            {
                kind = NavTargetKind.Station;
                typeLabel = "Station";
                statusLabel = "Dockable";
                integrityLabel = $"Dock range {stationTarget.DockingRange:N0}m";
                accentColor = Color.LimeGreen;
            }
            else
            {
                statusLabel = "World object";
            }

            string missionLabel = string.Empty;
            if (missionContext != null)
            {
                missionLabel = $"Mission: {missionContext.GetTypeLabel()}";
                if (missionContext.Type == MissionType.Bounty)
                {
                    statusLabel = $"{statusLabel} | Objective target";
                }
                else if (missionContext.Type == MissionType.Delivery)
                {
                    statusLabel = $"{statusLabel} | Mission destination";
                }
            }

            return new NavTargetHudData
            {
                Target = spaceTarget,
                SpaceTarget = spaceTarget,
                Kind = kind,
                Name = spaceTarget.Name ?? "Unknown Target",
                TypeLabel = typeLabel,
                MissionLabel = missionLabel,
                FactionId = factionId,
                FactionLabel = faction.DisplayName,
                StandingLabel = standing,
                DistanceLabel = FormatDistance(distance),
                StatusLabel = statusLabel,
                IntegrityLabel = integrityLabel,
                AccentColor = accentColor,
                CanGoto = canGoto,
                IsResolved = true
            };
        }

        private static string ResolveFactionId(SpaceObject spaceTarget, FactionManager factionManager)
        {
            if (spaceTarget is NpcShip npcTarget)
            {
                return FactionManager.NormalizeFactionId(npcTarget.FactionId);
            }

            if (spaceTarget is Station stationTarget)
            {
                return FactionManager.NormalizeFactionId(stationTarget.FactionId);
            }

            return FactionManager.NeutralCivilians;
        }

        private static SpaceObject ResolveNpcTarget(string targetName, IReadOnlyList<NpcShip> npcShips, IReadOnlyList<SpaceObject> spaceObjects)
        {
            if (string.IsNullOrWhiteSpace(targetName))
            {
                return null;
            }

            NpcShip npc = npcShips.FirstOrDefault(candidate =>
                candidate != null &&
                !candidate.IsDestroyed &&
                candidate.Name != null &&
                candidate.Name.IndexOf(targetName, StringComparison.OrdinalIgnoreCase) >= 0);
            if (npc != null)
            {
                return npc;
            }

            return spaceObjects.OfType<NpcShip>().FirstOrDefault(candidate =>
                candidate != null &&
                !candidate.IsDestroyed &&
                candidate.Name != null &&
                candidate.Name.IndexOf(targetName, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        private static SpaceObject ResolveStationTarget(string destination, IReadOnlyList<SpaceObject> spaceObjects)
        {
            if (string.IsNullOrWhiteSpace(destination))
            {
                return null;
            }

            return spaceObjects.OfType<Station>().FirstOrDefault(candidate =>
                candidate != null &&
                candidate.Name != null &&
                (candidate.Name.Equals(destination, StringComparison.OrdinalIgnoreCase) ||
                 candidate.Name.IndexOf(destination, StringComparison.OrdinalIgnoreCase) >= 0 ||
                 destination.IndexOf(candidate.Name, StringComparison.OrdinalIgnoreCase) >= 0));
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
