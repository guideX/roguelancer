#nullable enable

using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;

namespace Roguelancer;

/// <summary>
/// Shared local-combat policy for NPC-to-NPC faction targeting. This is kept
/// separate from player disposition so reputation and temporary hostility can
/// never authorize faction combat by accident.
/// </summary>
public static class NpcFactionCombatTargeting
{
    public static bool IsHostileFactionPair(string? sourceFactionId, string? targetFactionId)
    {
        if (string.IsNullOrWhiteSpace(sourceFactionId) || string.IsNullOrWhiteSpace(targetFactionId))
            return false;

        string source = sourceFactionId.Trim();
        string target = targetFactionId.Trim();
        if (source.Length == 0 || target.Length == 0 ||
            source.Equals(target, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return FactionRelationshipMatrix.GetRelationship(source, target) == FactionRelationshipKind.Hostile;
    }

    public static bool IsValidHostileTarget(NpcShip? source, NpcShip? target, float? maxDistance = null)
    {
        if (source == null || target == null || source == target ||
            source.IsDestroyed || target.IsDestroyed ||
            !IsHostileFactionPair(source.FactionId, target.FactionId))
        {
            return false;
        }

        if (!maxDistance.HasValue)
            return true;

        float distance = Math.Max(0f, maxDistance.Value);
        return Vector3.DistanceSquared(source.Position, target.Position) <= distance * distance;
    }

    /// <summary>
    /// Ambient piracy is an explicit, shipment-owned exception to the broad
    /// faction matrix: a Rogue raider may attack the ordinary trader it was
    /// assigned even though neutral civilians are not a global Rogue combat
    /// relationship. The trader still must be live and out of transit.
    /// </summary>
    public static bool IsValidAmbientPirateTarget(NpcShip? source, NpcShip? target, float? maxDistance = null)
    {
        if (source == null || target == null || source == target ||
            !source.IsAmbientPirateRaider ||
            !string.Equals(FactionManager.NormalizeFactionId(source.FactionId), FactionManager.LibertyRogues, StringComparison.OrdinalIgnoreCase) ||
            source.IsDestroyed || target.IsDestroyed ||
            target.TrafficBehavior != TrafficZoneBehaviorType.TraderRoute || target.IsTradeLaneTransit)
            return false;

        if (!maxDistance.HasValue)
            return true;

        float distance = Math.Max(0f, maxDistance.Value);
        return Vector3.DistanceSquared(source.Position, target.Position) <= distance * distance;
    }

    /// <summary>
    /// Validates the separate lawful-contraband combat origin. The manifest
    /// callback is owned by the traffic/economy integration; faction hostility
    /// is intentionally not consulted here.
    /// </summary>
    public static bool IsValidContrabandEnforcementTarget(
        NpcShip? source,
        NpcShip? target,
        float? maxDistance,
        Func<NpcShip, bool>? hasContraband)
    {
        if (source == null || target == null || hasContraband == null ||
            !ContrabandEnforcementPolicy.IsLawfulEnforcementFaction(source.FactionId) ||
            !hasContraband(target))
        {
            return false;
        }

        if (source == target || source.IsDestroyed || target.IsDestroyed ||
            target.IsTradeLaneTransit ||
            string.Equals(
                FactionManager.NormalizeFactionId(source.FactionId),
                FactionManager.NormalizeFactionId(target.FactionId),
                StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!maxDistance.HasValue)
            return true;

        float distance = Math.Max(100f, maxDistance.Value);
        return Vector3.DistanceSquared(source.Position, target.Position) <= distance * distance;
    }

    /// <summary>
    /// Validates a target supplied by an active mission objective. The mission
    /// owns the exception to the broad faction matrix; all runtime combat,
    /// damage, disengagement, and weapon behavior remains unchanged.
    /// </summary>
    public static bool IsValidMissionTarget(NpcShip? source, NpcShip? target, float? maxDistance = null)
    {
        if (source == null || target == null || source == target ||
            source.IsDestroyed || target.IsDestroyed)
        {
            return false;
        }

        if (!maxDistance.HasValue)
            return true;

        float distance = Math.Max(0f, maxDistance.Value);
        return Vector3.DistanceSquared(source.Position, target.Position) <= distance * distance;
    }

    /// <summary>
    /// Selects the nearest eligible local contact. Distance is primary; name
    /// and then source-order are deterministic tie breakers, so updates do not
    /// randomly retarget a ship when contacts are equally close.
    /// </summary>
    public static NpcShip? SelectNearestHostileTarget(
        NpcShip? source,
        IReadOnlyList<NpcShip>? candidates,
        float maxDistance)
    {
        if (source == null || candidates == null || candidates.Count == 0 ||
            source.IsDestroyed || maxDistance <= 0f)
        {
            return null;
        }

        float maxDistanceSquared = maxDistance * maxDistance;
        float nearestDistanceSquared = maxDistanceSquared;
        NpcShip? nearest = null;
        string nearestName = string.Empty;
        int nearestIndex = int.MaxValue;

        for (int i = 0; i < candidates.Count; i++)
        {
            NpcShip? candidate = candidates[i];
            if (!IsValidHostileTarget(source, candidate, maxDistance))
                continue;

            float distanceSquared = Vector3.DistanceSquared(source.Position, candidate!.Position);
            string candidateName = candidate.Name ?? string.Empty;
            bool isCloser = distanceSquared < nearestDistanceSquared;
            bool isNameTie = Math.Abs(distanceSquared - nearestDistanceSquared) <= 0.0001f &&
                (nearest == null || string.CompareOrdinal(candidateName, nearestName) < 0);
            bool isOrderTie = Math.Abs(distanceSquared - nearestDistanceSquared) <= 0.0001f &&
                string.Equals(candidateName, nearestName, StringComparison.Ordinal) && i < nearestIndex;

            if (isCloser || isNameTie || isOrderTie)
            {
                nearestDistanceSquared = distanceSquared;
                nearest = candidate;
                nearestName = candidateName;
                nearestIndex = i;
            }
        }

        return nearest;
    }
}
