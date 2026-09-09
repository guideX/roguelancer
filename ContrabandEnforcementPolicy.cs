#nullable enable

using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Roguelancer;

/// <summary>
/// Read-only law-enforcement policy for NPC-to-NPC contraband detection.
/// Faction hostility remains a separate combat rule; this policy only answers
/// whether a lawful scanner has a bounded, manifest-backed contraband target.
/// </summary>
public static class ContrabandEnforcementPolicy
{
    public const float DefaultDetectionRange = 6_500f;
    public const float SeizureRange = 260f;
    public const int MaximumCandidatesPerSource = 64;

    public static bool IsLawfulEnforcementFaction(string? factionId) =>
        string.Equals(
            FactionManager.NormalizeFactionId(factionId),
            PoliceEnforcementService.InitialPolicingFactionId,
            StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Commodity legality is canonical and deliberately ignores faction,
    /// stolen provenance, route type, and ship presentation.
    /// </summary>
    public static bool HasActualContraband(NpcCargoManifestSnapshot? manifest) =>
        manifest?.Stacks?.Any(stack =>
            stack?.Commodity?.IsContraband == true && stack.Quantity > 0) == true;

    public static bool IsValidTarget(
        NpcShip? enforcementNpc,
        NpcShip? target,
        NpcCargoManifestSnapshot? manifest,
        float maxDistance)
    {
        if (enforcementNpc == null || target == null || enforcementNpc == target ||
            enforcementNpc.IsDestroyed || target.IsDestroyed ||
            !IsLawfulEnforcementFaction(enforcementNpc.FactionId) ||
            string.Equals(
                FactionManager.NormalizeFactionId(enforcementNpc.FactionId),
                FactionManager.NormalizeFactionId(target.FactionId),
                StringComparison.OrdinalIgnoreCase) ||
            target.IsTradeLaneTransit || !HasActualContraband(manifest))
        {
            return false;
        }

        float range = Math.Max(100f, float.IsNaN(maxDistance) || float.IsInfinity(maxDistance)
            ? DefaultDetectionRange
            : maxDistance);
        return Vector3.DistanceSquared(enforcementNpc.Position, target.Position) <= range * range;
    }

    /// <summary>
    /// Selects a real local manifest-bearing target. The caller supplies the
    /// already spatially-local candidates; no system-wide cargo knowledge is
    /// introduced here.
    /// </summary>
    public static NpcShip? SelectNearestTarget(
        NpcShip? enforcementNpc,
        IReadOnlyList<NpcShip>? candidates,
        float maxDistance,
        Func<NpcShip, NpcCargoManifestSnapshot?>? manifestResolver)
    {
        if (enforcementNpc == null || candidates == null || candidates.Count == 0 ||
            !IsLawfulEnforcementFaction(enforcementNpc.FactionId) || manifestResolver == null)
        {
            return null;
        }

        float nearestDistanceSquared = float.PositiveInfinity;
        NpcShip? nearest = null;
        string nearestName = string.Empty;
        string nearestIdentity = string.Empty;
        int inspected = 0;
        float range = Math.Max(100f, maxDistance);

        for (int index = 0; index < candidates.Count && inspected < MaximumCandidatesPerSource; index++)
        {
            NpcShip? candidate = candidates[index];
            if (candidate == null)
                continue;

            inspected++;
            NpcCargoManifestSnapshot? manifest = manifestResolver(candidate);
            if (!IsValidTarget(enforcementNpc, candidate, manifest, range))
                continue;

            float distanceSquared = Vector3.DistanceSquared(enforcementNpc.Position, candidate.Position);
            string candidateName = candidate.Name ?? string.Empty;
            string candidateIdentity = NpcIdentity.GetStableIdentity(candidate);
            bool isCloser = distanceSquared < nearestDistanceSquared;
            bool isNameTie = Math.Abs(distanceSquared - nearestDistanceSquared) <= 0.0001f &&
                (nearest == null || string.CompareOrdinal(candidateName, nearestName) < 0);
            bool isIdentityTie = Math.Abs(distanceSquared - nearestDistanceSquared) <= 0.0001f &&
                string.Equals(candidateName, nearestName, StringComparison.Ordinal) &&
                string.CompareOrdinal(candidateIdentity, nearestIdentity) < 0;

            if (isCloser || isNameTie || isIdentityTie)
            {
                nearestDistanceSquared = distanceSquared;
                nearest = candidate;
                nearestName = candidateName;
                nearestIdentity = candidateIdentity;
            }
        }

        return nearest;
    }
}

/// <summary>
/// Bounded transient provenance for one physical lawful seizure. It is a
/// diagnostic record, not a police inventory or a market transaction.
/// </summary>
public sealed record LawfulContrabandSeizureRecord(
    int PodRuntimeIdentity,
    string CommodityId,
    int Quantity,
    string EnforcerIdentity,
    string EnforcerFactionId);
