using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;

namespace Roguelancer
{
    public enum PoliceScanState
    {
        Idle,
        Scanning,
        ContrabandDetected,
        Cleared,
        Enforcement
    }

    /// <summary>
    /// Real-time cargo inspection lifecycle for ordinary Liberty Police
    /// patrols. The scan is read-only until completion; detection applies one
    /// bounded standing/hostility consequence and never confiscates cargo.
    /// </summary>
    public sealed class PoliceScanSystem
    {
        public const int FineAmount = PoliceEnforcementService.BaseFineCredits;
        public const float ScanDurationSeconds = 3.5f;
        public const float ScanRange = 3200f;
        public const float CancelRange = 3800f;
        public const float ResultHoldSeconds = 2f;
        public const float RetryCooldownSeconds = 25f;
        public const float DetectionReputationPenalty = -0.03f;

        private readonly PoliceEnforcementService _enforcementService;
        private MissionManager _missionManager;
        private NpcShip _activeScanner;
        private PoliceEnforcementOffer _enforcementOffer;
        private float _scanTimer;
        private float _resultTimer;
        private float _cooldownTimer;

        public PoliceScanState State { get; private set; } = PoliceScanState.Idle;
        public PoliceEnforcementOffer CurrentOffer => _enforcementOffer;
        public PoliceEnforcementService EnforcementService => _enforcementService;
        public NpcShip ActiveScanner => _activeScanner;
        public float CooldownRemaining => Math.Max(0f, _cooldownTimer);
        public int DetectionCount { get; private set; }
        public int LastJettisonedQuantity { get; private set; }
        public float ScanProgress => State == PoliceScanState.Scanning
            ? MathHelper.Clamp(_scanTimer / ScanDurationSeconds, 0f, 1f)
            : 0f;
        public string ActiveScannerFactionId => _activeScanner?.FactionId ?? string.Empty;
        public string StatusText => State switch
        {
            PoliceScanState.Scanning =>
                $"LIBERTY POLICE CARGO SCAN | Inspection {ScanProgress * 100f:0}% ({_scanTimer:0.0}/{ScanDurationSeconds:0.0}s)",
            PoliceScanState.ContrabandDetected => "CONTRABAND DETECTED | POLICE ALERT ACTIVE",
            PoliceScanState.Cleared => "CARGO INSPECTION COMPLETE | HOLD CLEAR",
            PoliceScanState.Enforcement => "POLICE HOSTILE",
            _ => string.Empty
        };

        public PoliceScanSystem(PoliceEnforcementService enforcementService = null)
        {
            _enforcementService = enforcementService ?? new PoliceEnforcementService();
        }

        public void SetMissionManager(MissionManager missionManager) => _missionManager = missionManager;

        public bool IsLawfulScannerFaction(string factionId) => _enforcementService.IsPolicingFaction(factionId);

        public void Update(
            GameTime gameTime,
            Ship playerShip,
            IReadOnlyList<NpcShip> npcs,
            PlayerCredits playerCredits,
            ReputationManager reputationManager,
            NotificationManager notificationManager = null,
            Action<string> log = null)
        {
            Update(gameTime, playerShip, npcs, playerCredits, reputationManager,
                playerDocked: false, playerInTradeLaneTransit: false, notificationManager, log);
        }

        public void Update(
            GameTime gameTime,
            Ship playerShip,
            IReadOnlyList<NpcShip> npcs,
            PlayerCredits playerCredits,
            ReputationManager reputationManager,
            bool playerDocked,
            bool playerInTradeLaneTransit,
            NotificationManager notificationManager = null,
            Action<string> log = null)
        {
            if (gameTime == null || playerShip == null || playerCredits == null || reputationManager == null)
                return;

            float deltaTime = Math.Max(0f, (float)gameTime.ElapsedGameTime.TotalSeconds);
            _cooldownTimer = Math.Max(0f, _cooldownTimer - deltaTime);

            if (playerDocked || playerInTradeLaneTransit || playerShip.Hull?.IsDestroyed == true)
            {
                if (State == PoliceScanState.Scanning)
                    CancelScan("inspection interrupted by flight-state transition", notificationManager, log, applyCooldown: false);
                else if (State is PoliceScanState.Cleared or PoliceScanState.ContrabandDetected or PoliceScanState.Enforcement)
                    ClearResultState();
                return;
            }

            if (State is PoliceScanState.Cleared or PoliceScanState.Enforcement or PoliceScanState.ContrabandDetected)
            {
                _resultTimer = Math.Max(0f, _resultTimer - deltaTime);
                if (_resultTimer <= 0f)
                    ClearResultState();
                return;
            }

            if (State == PoliceScanState.Scanning)
            {
                if (!IsScannerValid(playerShip, reputationManager))
                {
                    CancelScan("scanner lost the inspection lock", notificationManager, log, applyCooldown: true);
                    return;
                }

                _scanTimer += deltaTime;
                if (_scanTimer >= ScanDurationSeconds)
                    CompleteScan(playerShip, playerCredits, reputationManager, notificationManager, log);
                return;
            }

            if (_cooldownTimer > 0f || playerShip.CargoHold == null || npcs == null || npcs.Count == 0 ||
                reputationManager.IsFactionCurrentlyHostile(FactionManager.LibertyPolice))
            {
                return;
            }

            NpcShip candidate = FindNearestLawfulScanner(playerShip, npcs, reputationManager);
            if (candidate != null)
                StartScan(candidate, notificationManager, log);
        }

        public void Reset()
        {
            State = PoliceScanState.Idle;
            _activeScanner = null;
            _enforcementOffer = null;
            _scanTimer = 0f;
            _resultTimer = 0f;
            _cooldownTimer = 0f;
            LastJettisonedQuantity = 0;
        }

        /// <summary>
        /// Compatibility seam for the pre-Phase-52 enforcement UI. Automatic
        /// scans never call this path; Phase 52 detection is consequence-only.
        /// </summary>
        public bool TryAcceptEnforcement(
            Ship playerShip,
            PlayerCredits playerCredits,
            ReputationManager reputationManager,
            NotificationManager notificationManager = null,
            Action<string> log = null) => TryResolveEnforcement(
                PoliceEnforcementResolution.Comply, playerShip, playerCredits, reputationManager, notificationManager, log);

        public bool TryRefuseEnforcement(
            Ship playerShip,
            PlayerCredits playerCredits,
            ReputationManager reputationManager,
            NotificationManager notificationManager = null,
            Action<string> log = null) => TryResolveEnforcement(
                PoliceEnforcementResolution.Refuse, playerShip, playerCredits, reputationManager, notificationManager, log);

        public bool TryJettisonContraband(
            Ship playerShip,
            LootManager lootManager,
            NotificationManager notificationManager = null,
            Action<string> log = null)
        {
            if (playerShip?.CargoHold == null || lootManager == null)
                return false;

            int removed = lootManager.TryJettisonContraband(
                playerShip.CargoHold,
                playerShip.Position + playerShip.Forward * 120f,
                playerShip.Velocity,
                out int podCount);
            if (removed <= 0)
                return false;

            LastJettisonedQuantity = removed;
            log?.Invoke($"[POLICE SCAN] Jettisoned {removed} contraband units into {podCount} physical pod(s).");
            notificationManager?.ShowMessage($"Contraband jettisoned: {removed} units", 2f);
            if (State == PoliceScanState.ContrabandDetected && !HasContraband(playerShip.CargoHold))
            {
                _enforcementOffer = null;
                _resultTimer = ResultHoldSeconds;
                State = PoliceScanState.Cleared;
                notificationManager?.ShowMessage("Cargo clear — recoverable pods deployed", 2f);
                log?.Invoke("[POLICE SCAN] Detection cleared by physical jettison; pods remain recoverable.");
            }
            return true;
        }

        public bool TryJettisonContraband(Ship playerShip, NotificationManager notificationManager = null, Action<string> log = null)
        {
            // Legacy callers do not have a loot authority. Keep the old seam
            // non-destructive to mission cargo and report that physical pods
            // are required for gameplay jettison.
            if (playerShip?.CargoHold == null || !HasContraband(playerShip.CargoHold))
                return false;
            notificationManager?.ShowMessage("Jettison requires the physical cargo-pod system", 2f);
            log?.Invoke("[POLICE SCAN] Jettison rejected: no LootManager authority was supplied.");
            return false;
        }

        private void StartScan(NpcShip scanner, NotificationManager notificationManager, Action<string> log)
        {
            _activeScanner = scanner;
            _scanTimer = 0f;
            _enforcementOffer = null;
            State = PoliceScanState.Scanning;
            notificationManager?.ShowMessage("Liberty Police inspection initiated", 2f);
            log?.Invoke($"[POLICE SCAN] Scan initiated by {scanner.Name} ({scanner.FactionId}).");
        }

        private void CompleteScan(
            Ship playerShip,
            PlayerCredits playerCredits,
            ReputationManager reputationManager,
            NotificationManager notificationManager,
            Action<string> log)
        {
            _enforcementOffer = _enforcementService.Evaluate(
                ActiveScannerFactionId,
                playerShip.CargoHold,
                playerCredits);
            _scanTimer = 0f;
            _cooldownTimer = RetryCooldownSeconds;

            if (!_enforcementOffer.HasContraband)
            {
                State = PoliceScanState.Cleared;
                _resultTimer = ResultHoldSeconds;
                notificationManager?.ShowMessage("Inspection complete: cargo clean", 2f);
                log?.Invoke("[POLICE SCAN] Inspection complete: cargo clean.");
                return;
            }

            State = PoliceScanState.ContrabandDetected;
            _resultTimer = ResultHoldSeconds;
            DetectionCount++;
            reputationManager.TemporaryHostility.RecordHostileAction(
                FactionManager.LibertyPolice,
                "contraband detected during cargo scan",
                PoliceEnforcementService.TemporaryHostilityDurationSeconds,
                causedByPlayerAggression: false);
            reputationManager.AdjustReputationDirect(
                FactionManager.LibertyPolice,
                DetectionReputationPenalty,
                ReputationChangeReason.PoliceScan);
            if (_missionManager?.ActiveMission?.Type == MissionType.ContrabandSmuggling)
                _missionManager.ActiveMission.SmugglingPoliceDetected = true;
            _activeScanner?.SetPlayerTarget(playerShip.Position, NpcPlayerTargetReason.FactionDisposition);
            notificationManager?.ShowMessage("Contraband detected — Liberty Police alerted", 3f);
            log?.Invoke($"[POLICE SCAN] Contraband detected: {_enforcementOffer.TotalContrabandQuantity} units; temporary hostility applied.");
        }

        private void CancelScan(
            string reason,
            NotificationManager notificationManager,
            Action<string> log,
            bool applyCooldown)
        {
            _scanTimer = 0f;
            _activeScanner = null;
            _enforcementOffer = null;
            State = PoliceScanState.Idle;
            if (applyCooldown)
                _cooldownTimer = Math.Max(_cooldownTimer, 5f);
            notificationManager?.ShowMessage("Police inspection interrupted", 2f);
            log?.Invoke($"[POLICE SCAN] Scan canceled: {reason}.");
        }

        private void ClearResultState()
        {
            State = PoliceScanState.Idle;
            _activeScanner = null;
            _enforcementOffer = null;
            _scanTimer = 0f;
            _resultTimer = 0f;
        }

        private bool TryResolveEnforcement(
            PoliceEnforcementResolution resolution,
            Ship playerShip,
            PlayerCredits playerCredits,
            ReputationManager reputationManager,
            NotificationManager notificationManager,
            Action<string> log)
        {
            if (State != PoliceScanState.ContrabandDetected || _enforcementOffer == null)
                return false;

            if (!_enforcementService.TryResolve(
                    _enforcementOffer,
                    resolution,
                    playerShip?.CargoHold,
                    playerCredits,
                    reputationManager,
                    out PoliceEnforcementResult result,
                    out string failureReason))
            {
                notificationManager?.ShowMessage(failureReason, 2f);
                log?.Invoke($"[POLICE SCAN] Legacy enforcement resolution failed: {failureReason}.");
                return false;
            }

            State = result.Outcome == PoliceEnforcementOutcome.Refused ||
                    result.Outcome == PoliceEnforcementOutcome.ConfiscatedUnpaid
                ? PoliceScanState.Enforcement
                : PoliceScanState.Cleared;
            _resultTimer = ResultHoldSeconds;
            _enforcementOffer = null;
            notificationManager?.ShowMessage(result.Message, 3f);
            log?.Invoke($"[POLICE SCAN] Legacy enforcement resolution: {result.Message}.");
            return true;
        }

        private bool IsScannerValid(Ship playerShip, ReputationManager reputationManager)
        {
            if (_activeScanner == null || _activeScanner.IsDestroyed ||
                !IsLawfulScannerFaction(_activeScanner.FactionId) ||
                _activeScanner.IsTrafficEngaged || _activeScanner.IsTradeLaneTransit ||
                _activeScanner.IsMissionHoldPosition ||
                reputationManager.IsFactionCurrentlyHostile(_activeScanner.FactionId))
            {
                return false;
            }

            return Vector3.DistanceSquared(playerShip.Position, _activeScanner.Position) <= CancelRange * CancelRange;
        }

        private NpcShip FindNearestLawfulScanner(
            Ship playerShip,
            IReadOnlyList<NpcShip> npcs,
            ReputationManager reputationManager)
        {
            NpcShip nearest = null;
            float nearestDistanceSq = ScanRange * ScanRange;
            int nearestStableKey = int.MaxValue;

            for (int i = 0; i < npcs.Count; i++)
            {
                NpcShip npc = npcs[i];
                if (npc == null || npc.IsDestroyed || !IsLawfulScannerFaction(npc.FactionId) ||
                    npc.TrafficBehavior != TrafficZoneBehaviorType.LawfulPatrol ||
                    npc.IsTrafficEngaged || npc.IsTradeLaneTransit || npc.IsMissionHoldPosition ||
                    npc.HasPlayerTarget || reputationManager.IsFactionCurrentlyHostile(npc.FactionId))
                {
                    continue;
                }

                float distanceSq = Vector3.DistanceSquared(playerShip.Position, npc.Position);
                int stableKey = GetStableScannerKey(npc);
                if (distanceSq < nearestDistanceSq ||
                    (Math.Abs(distanceSq - nearestDistanceSq) < 0.01f && stableKey < nearestStableKey))
                {
                    nearestDistanceSq = distanceSq;
                    nearestStableKey = stableKey;
                    nearest = npc;
                }
            }

            return nearest;
        }

        private static int GetStableScannerKey(NpcShip npc)
        {
            unchecked
            {
                int hash = 17;
                foreach (char character in (npc?.FactionId ?? string.Empty).ToUpperInvariant())
                    hash = hash * 31 + character;
                foreach (char character in (npc?.Name ?? string.Empty).ToUpperInvariant())
                    hash = hash * 31 + character;
                Vector3 position = npc?.Position ?? Vector3.Zero;
                hash = hash * 31 + BitConverter.SingleToInt32Bits(position.X);
                hash = hash * 31 + BitConverter.SingleToInt32Bits(position.Y);
                hash = hash * 31 + BitConverter.SingleToInt32Bits(position.Z);
                return hash;
            }
        }

        private static bool HasContraband(CargoHold cargoHold)
        {
            if (cargoHold == null)
                return false;

            foreach (KeyValuePair<string, int> entry in cargoHold.GetAllCommodities())
            {
                Commodity commodity = CommodityCatalog.GetByIdOrName(entry.Key);
                if (commodity?.IsContraband == true && entry.Value > 0)
                    return true;
            }

            return false;
        }
    }
}
