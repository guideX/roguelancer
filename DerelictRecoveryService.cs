using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Roguelancer
{
    public enum DerelictPresentation
    {
        Hidden,
        UnidentifiedSignal,
        IdentifiedWreck,
        ReadyToInvestigate,
        RecoveredWreck,
    }

    public sealed class DerelictRecoveryResult
    {
        public bool Success { get; init; }
        public string FailureReason { get; init; } = string.Empty;
        public IReadOnlyList<CargoPod> SpawnedPods { get; init; } = Array.Empty<CargoPod>();
    }

    /// <summary>
    /// Bounded authority for discoverable derelict investigation and one-time
    /// salvage release. Discovery/presentation is a pure function of distance;
    /// recovery is an explicit edge-triggered transaction that releases finite
    /// physical pods through <see cref="LootManager"/> and then marks the
    /// stable site id recovered. The service owns no inventory, economy, or
    /// mission state.
    ///
    /// Recovery commit point: the site is marked recovered only after
    /// <see cref="LootManager.SpawnDerelictSalvage"/> reports full success
    /// for every defined salvage line. Spawn failure (live-object cap or
    /// blocked coordinates) leaves the site unrecovered so the player can
    /// retry later; outstanding pods then follow normal CargoPod persistence.
    /// </summary>
    public sealed class DerelictRecoveryService
    {
        public const Keys InvestigateKey = Keys.F;
        public const string InvestigateKeyLabel = "F";

        private readonly HashSet<string> _recoveredSiteIds = new(StringComparer.OrdinalIgnoreCase);
        private bool _recoveryInProgress;

        public int RecoveredCount => _recoveredSiteIds.Count;

        public bool IsRecovered(string siteId)
        {
            return !string.IsNullOrWhiteSpace(siteId) && _recoveredSiteIds.Contains(siteId.Trim());
        }

        public List<string> CaptureState()
        {
            return _recoveredSiteIds
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .OrderBy(id => id, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        /// <summary>
        /// Restores durable recovery state. Null (legacy saves pre-Phase 87)
        /// means no site recovered; unknown ids are preserved verbatim so
        /// future curated sites stay forward-compatible.
        /// </summary>
        public void RestoreState(IEnumerable<string> recoveredSiteIds)
        {
            _recoveredSiteIds.Clear();
            _recoveryInProgress = false;
            if (recoveredSiteIds == null)
                return;
            foreach (string id in recoveredSiteIds)
            {
                if (!string.IsNullOrWhiteSpace(id))
                    _recoveredSiteIds.Add(id.Trim());
            }
        }

        /// <summary>
        /// Clears transient interaction state only. Durable recovery survives
        /// system transitions, docking, death, and save/load.
        /// </summary>
        public void ResetTransient()
        {
            _recoveryInProgress = false;
        }

        public DerelictPresentation GetPresentation(
            DerelictSite site,
            Vector3 playerPosition,
            int currentSystemIndex,
            bool playerAlive,
            bool docked)
        {
            if (site == null || playerAlive == false || docked || currentSystemIndex != site.SystemIndex)
                return DerelictPresentation.Hidden;

            float distance = Vector3.Distance(playerPosition, site.Position);
            if (distance > site.DetectionRange)
                return DerelictPresentation.Hidden;
            if (IsRecovered(site.SiteId))
                return distance <= site.IdentificationRange
                    ? DerelictPresentation.RecoveredWreck
                    : DerelictPresentation.UnidentifiedSignal;
            if (distance <= site.RecoveryRange)
                return DerelictPresentation.ReadyToInvestigate;
            if (distance <= site.IdentificationRange)
                return DerelictPresentation.IdentifiedWreck;
            return DerelictPresentation.UnidentifiedSignal;
        }

        /// <summary>
        /// Live-only HUD projection. Returns null when nothing should render.
        /// Side-effect free; safe to call every frame.
        /// </summary>
        public string GetHudText(
            DerelictSite site,
            Vector3 playerPosition,
            int currentSystemIndex,
            bool playerAlive,
            bool docked)
        {
            return GetPresentation(site, playerPosition, currentSystemIndex, playerAlive, docked) switch
            {
                DerelictPresentation.UnidentifiedSignal => "Unidentified signal detected",
                DerelictPresentation.IdentifiedWreck => site.DisplayName,
                DerelictPresentation.ReadyToInvestigate => $"{site.DisplayName} - Press {InvestigateKeyLabel} to investigate",
                DerelictPresentation.RecoveredWreck => $"{site.DisplayName} (salvaged)",
                _ => null,
            };
        }

        public bool CanRecover(
            DerelictSite site,
            Vector3 playerPosition,
            int currentSystemIndex,
            bool playerAlive,
            bool docked,
            bool inTransit,
            out string failureReason)
        {
            failureReason = string.Empty;
            if (site == null)
            {
                failureReason = "no derelict in range";
                return false;
            }
            if (!playerAlive)
            {
                failureReason = "player ship is invalid";
                return false;
            }
            if (docked)
            {
                failureReason = "cannot investigate while docked";
                return false;
            }
            if (inTransit)
            {
                failureReason = "cannot investigate while in transit";
                return false;
            }
            if (currentSystemIndex != site.SystemIndex)
            {
                failureReason = "derelict is in another system";
                return false;
            }
            if (IsRecovered(site.SiteId))
            {
                failureReason = "derelict already salvaged";
                return false;
            }
            if (Vector3.Distance(playerPosition, site.Position) > site.RecoveryRange)
            {
                failureReason = "derelict out of range";
                return false;
            }
            if (_recoveryInProgress)
            {
                failureReason = "recovery already in progress";
                return false;
            }
            return true;
        }

        public DerelictRecoveryResult TryRecover(
            DerelictSite site,
            Vector3 playerPosition,
            int currentSystemIndex,
            bool playerAlive,
            bool docked,
            bool inTransit,
            LootManager loot)
        {
            if (!CanRecover(site, playerPosition, currentSystemIndex, playerAlive, docked, inTransit, out string gateReason))
                return new DerelictRecoveryResult { Success = false, FailureReason = gateReason };
            if (loot == null)
                return new DerelictRecoveryResult { Success = false, FailureReason = "loot system unavailable" };

            // Same-frame reentrancy guard: holding the key or dispatching two
            // attempts in one frame cannot release loot twice.
            if (_recoveryInProgress)
                return new DerelictRecoveryResult { Success = false, FailureReason = "recovery already in progress" };
            _recoveryInProgress = true;
            try
            {
                // Re-check under guard so two same-frame attempts cannot both
                // pass the gate before either commits.
                if (IsRecovered(site.SiteId))
                    return new DerelictRecoveryResult { Success = false, FailureReason = "derelict already salvaged" };

                if (!loot.SpawnDerelictSalvage(site.Position, site.Salvage, out IReadOnlyList<CargoPod> spawned, out string spawnFailure))
                    return new DerelictRecoveryResult { Success = false, FailureReason = spawnFailure };

                _recoveredSiteIds.Add(site.SiteId);
                return new DerelictRecoveryResult { Success = true, SpawnedPods = spawned };
            }
            finally
            {
                _recoveryInProgress = false;
            }
        }
    }
}
