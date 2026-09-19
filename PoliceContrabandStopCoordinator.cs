#nullable enable

using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;

namespace Roguelancer;

/// <summary>
/// Phase 71 lawful contraband stop coordinator. This is the one small
/// transient encounter owner that connects Phase 70 detection to the
/// existing police scan/demand/fugitive systems without building a new
/// crime framework.
/// Intended loop:
/// lawful detection (ContrabandEnforcement intercept, hold-fire) -&gt;
/// close to ScanRange -&gt; existing PoliceScanSystem scan/demand (single
/// owner) -&gt; comply via PoliceEnforcementService +
/// CargoHold.TryConfiscateCargo (resolve, no fugitive) OR
/// refuse/timeout/evade/attack -&gt; existing PoliceFugitiveManager pursuit
/// (convert to FugitivePursuit, normal weapons/combat).
/// Commodity.IsContraband remains authoritative. No shadow cargo. No new UI.
/// All state is transient; nothing here is save data.
/// </summary>
public sealed class PoliceContrabandStopCoordinator
{
    private readonly HashSet<NpcShip> _holdFireShips = new();
    private NpcShip? _activeOwner;

    public NpcShip? ActiveOwner => _activeOwner;
    public int HoldFireCount => _holdFireShips.Count;

    /// <summary>
    /// Runs after TrafficManager (detection) and PoliceScanSystem (scan)
    /// but before NpcWeaponSystem (fire) in the frame. Bounded to the
    /// existing enforcer list (at most 16) plus the one scan owner.
    /// </summary>
    public void Update(
        Ship? playerShip,
        IReadOnlyList<NpcShip>? npcs,
        TrafficManager? traffic,
        PoliceScanSystem? scan,
        PoliceFugitiveManager? fugitive,
        ReputationManager? reputation,
        Action<string>? log = null)
    {
        bool playerInvalid = playerShip == null ||
            playerShip.Hull?.IsDestroyed == true ||
            playerShip.IsTradeLaneTransit;

        if (playerInvalid)
        {
            // Player teardown/death/transit clears the transient stop without
            // creating a new fugitive. FugitiveManager owns its own death
            // reset; the scan demand must not linger as phantom state.
            ClearHoldFireExcept(null);
            _activeOwner = null;
            if (playerShip?.Hull?.IsDestroyed == true)
                scan?.Reset();
            return;
        }

        if (traffic == null || scan == null || npcs == null)
        {
            PruneOwner(npcs);
            return;
        }

        PruneOwner(npcs);
        PruneHoldFire(npcs);

        IReadOnlyList<NpcShip> enforcers = traffic.ActivePlayerContrabandEnforcers;
        NpcShip? scanOwner = (scan.State == PoliceScanState.Scanning ||
            scan.State == PoliceScanState.ContrabandDetected ||
            scan.State == PoliceScanState.Enforcement)
            ? scan.ActiveScanner
            : null;
        if (scanOwner != null && (scanOwner.IsDestroyed || !Contains(npcs, scanOwner)))
            scanOwner = null;

        // Deterministic single authority: retain the live scan owner when
        // one exists; otherwise the nearest live interceptor owns the
        // encounter for presentation. Other officers support but never open
        // a second demand (PoliceScanSystem is already single-owner).
        if (scanOwner != null)
        {
            _activeOwner = scanOwner;
        }
        else if (enforcers.Count > 0 && playerShip != null)
        {
            _activeOwner = SelectNearestEnforcer(playerShip, enforcers);
        }
        else
        {
            _activeOwner = null;
        }

        // Hold fire on every unconfirmed contraband interceptor. The weapon
        // gate still permits fire when another valid hostility reason holds
        // (faction disposition, temporary hostility/fugitive, retaliation).
        foreach (NpcShip enforcer in enforcers)
        {
            if (enforcer == null || enforcer.IsDestroyed ||
                enforcer.PlayerTargetReason != NpcPlayerTargetReason.ContrabandEnforcement)
                continue;

            if (!enforcer.IsLawfulStopHoldFire)
                enforcer.SetLawfulStopHoldFire(true);
            _holdFireShips.Add(enforcer);
        }

        // Compliant/clean resolution: the scan entered Cleared (paid and
        // confiscated, or clean hold). Cargo authority already removed only
        // the demanded illegal quantities; legal cargo survives. Clear the
        // interception targets immediately so no stale pursuit lingers until
        // the next traffic pass. No fugitive is created here.
        if (scan.State == PoliceScanState.Cleared)
        {
            foreach (NpcShip enforcer in enforcers)
            {
                if (enforcer != null && enforcer.PlayerTargetReason == NpcPlayerTargetReason.ContrabandEnforcement)
                    enforcer.ClearEncounterState();
            }

            ClearHoldFireExcept(null);
            if (_activeOwner != null && _activeOwner.PlayerTargetReason == NpcPlayerTargetReason.ContrabandEnforcement)
                _activeOwner = null;
            log?.Invoke("[CONTRABAND STOP] Compliance/clean scan cleared interception state.");
            return;
        }

        // Escalation: demand refused/timed out/fled (scan Enforcement) or an
        // independently active fugitive incident (refusal, timeout, escape
        // radius, trade-lane flight, explicit refuse input, scanner destroyed
        // by hostile action, or player attack on Police). Convert every
        // remaining contraband interceptor to genuine fugitive pursuit and
        // release hold-fire so normal pursuit/combat resumes through the
        // existing movement/weapon/damage pipeline. No parallel combat path.
        bool escalated = scan.State == PoliceScanState.Enforcement ||
            (fugitive?.IsActive == true);
        if (escalated && playerShip != null)
        {
            if (fugitive != null && !fugitive.IsActive)
                fugitive.BeginPursuit(playerShip, "contraband stop escalation", log);

            foreach (NpcShip enforcer in enforcers)
            {
                if (enforcer == null || enforcer.IsDestroyed)
                    continue;

                if (enforcer.PlayerTargetReason == NpcPlayerTargetReason.ContrabandEnforcement)
                {
                    enforcer.SetLawfulStopHoldFire(false);
                    enforcer.SetPlayerTarget(playerShip.Position, NpcPlayerTargetReason.FugitivePursuit);
                    log?.Invoke($"[CONTRABAND STOP] {enforcer.Name} escalated to fugitive pursuit.");
                }
            }

            ClearHoldFireExcept(null);
            return;
        }

        // Player attacked the stop owner (or any Police) without a scan
        // refusal yet: the fugitive manager observes monotonic player damage
        // in the game loop. If it is now active, the conversion above on the
        // next tick handles it. If temp hostility already exists without a
        // fugitive incident (legacy retaliation), hold-fire no longer blocks
        // fire because the weapon gate sees hostility. No action needed here.
    }

    public void NotifyNpcDestroyed(NpcShip? destroyedShip)
    {
        if (destroyedShip == null)
            return;

        _holdFireShips.Remove(destroyedShip);
        if (ReferenceEquals(_activeOwner, destroyedShip))
            _activeOwner = null;
    }

    public void NotifyNpcDespawned(NpcShip? despawnedShip)
    {
        NotifyNpcDestroyed(despawnedShip);
    }

    public void Reset()
    {
        ClearHoldFireExcept(null);
        _activeOwner = null;
    }

    private void PruneOwner(IReadOnlyList<NpcShip>? npcs)
    {
        if (_activeOwner == null)
            return;

        if (_activeOwner.IsDestroyed || (npcs != null && !Contains(npcs, _activeOwner)))
            _activeOwner = null;
    }

    private void PruneHoldFire(IReadOnlyList<NpcShip>? npcs)
    {
        if (_holdFireShips.Count == 0)
            return;

        List<NpcShip>? stale = null;
        foreach (NpcShip ship in _holdFireShips)
        {
            if (ship.IsDestroyed ||
                (npcs != null && !Contains(npcs, ship)) ||
                !ship.IsLawfulStopHoldFire ||
                ship.PlayerTargetReason != NpcPlayerTargetReason.ContrabandEnforcement)
            {
                (stale ??= new List<NpcShip>()).Add(ship);
            }
        }

        if (stale == null)
            return;

        foreach (NpcShip ship in stale)
        {
            _holdFireShips.Remove(ship);
            if (!ship.IsDestroyed && ship.IsLawfulStopHoldFire &&
                ship.PlayerTargetReason != NpcPlayerTargetReason.ContrabandEnforcement)
                ship.SetLawfulStopHoldFire(false);
        }
    }

    private void ClearHoldFireExcept(NpcShip? keep)
    {
        foreach (NpcShip ship in _holdFireShips)
        {
            if (ship != null && !ship.IsDestroyed && !ReferenceEquals(ship, keep))
                ship.SetLawfulStopHoldFire(false);
        }

        _holdFireShips.Clear();
        if (keep != null)
            _holdFireShips.Add(keep);
    }

    private static NpcShip? SelectNearestEnforcer(Ship player, IReadOnlyList<NpcShip> enforcers)
    {
        NpcShip? nearest = null;
        float nearestSq = float.PositiveInfinity;
        string nearestName = string.Empty;
        string nearestIdentity = string.Empty;

        for (int i = 0; i < enforcers.Count; i++)
        {
            NpcShip candidate = enforcers[i];
            if (candidate == null || candidate.IsDestroyed)
                continue;

            float distanceSq = Vector3.DistanceSquared(candidate.Position, player.Position);
            string candidateName = candidate.Name ?? string.Empty;
            string candidateIdentity = NpcIdentity.GetStableIdentity(candidate);
            bool closer = distanceSq < nearestSq;
            bool nameTie = Math.Abs(distanceSq - nearestSq) <= 0.0001f &&
                (nearest == null || string.CompareOrdinal(candidateName, nearestName) < 0);
            bool identityTie = Math.Abs(distanceSq - nearestSq) <= 0.0001f &&
                string.Equals(candidateName, nearestName, StringComparison.Ordinal) &&
                string.CompareOrdinal(candidateIdentity, nearestIdentity) < 0;

            if (closer || nameTie || identityTie)
            {
                nearestSq = distanceSq;
                nearest = candidate;
                nearestName = candidateName;
                nearestIdentity = candidateIdentity;
            }
        }

        return nearest;
    }

    private static bool Contains(IReadOnlyList<NpcShip> npcs, NpcShip ship)
    {
        for (int i = 0; i < npcs.Count; i++)
        {
            if (ReferenceEquals(npcs[i], ship))
                return true;
        }

        return false;
    }
}
