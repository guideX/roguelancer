using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;

namespace Roguelancer
{
    /// <summary>
    /// Small scan loop for lawful patrols checking the player's cargo.
    /// </summary>
    public enum PoliceScanState
    {
        Idle,
        Scanning,
        ContrabandDetected,
        Cleared,
        Enforcement
    }

    /// <summary>
    /// Handles a lightweight police scan and contraband consequence loop.
    /// </summary>
    public sealed class PoliceScanSystem
    {
        public const int FineAmount = 1500;

        private const float ScanDurationSeconds = 3f;
        private const float GracePeriodSeconds = 2.5f;
        private const float ScanRange = 2800f;
        private const float CancelRange = 3400f;
        private const float ResultHoldSeconds = 1.5f;
        private const float RetryCooldownSeconds = 8f;
        private const float PaidReputationPenalty = -0.08f;
        private const float FleeAfterDetectionReputationPenalty = -0.50f;
        private const float EnforcementReputationPenalty = -1.0f;

        private static readonly HashSet<string> LawfulScannerFactions = new(StringComparer.OrdinalIgnoreCase)
        {
            FactionManager.LibertyPolice,
            FactionManager.LibertyNavy
        };

        private NpcShip _activeScanner;
        private float _scanTimer;
        private float _resultTimer;
        private float _cooldownTimer;

        public PoliceScanState State { get; private set; } = PoliceScanState.Idle;
        public float ScanProgress => State == PoliceScanState.Scanning ? MathHelper.Clamp(_scanTimer / ScanDurationSeconds, 0f, 1f) : 0f;
        public string StatusText
        {
            get
            {
                return State switch
                {
                    PoliceScanState.Scanning => $"Police Scan: {ScanProgress * 100f:F0}%",
                    PoliceScanState.ContrabandDetected => _resultTimer > 0f
                        ? $"Contraband detected - jettison or pay fine ({_resultTimer:F1}s)"
                        : "Contraband detected - jettison or pay fine",
                    PoliceScanState.Cleared => "Scan cleared",
                    PoliceScanState.Enforcement => "Police hostile",
                    _ => string.Empty,
                };
            }
        }
        public string ActiveScannerFactionId => _activeScanner?.FactionId ?? string.Empty;

        public bool IsLawfulScannerFaction(string? factionId)
        {
            return LawfulScannerFactions.Contains(FactionManager.NormalizeFactionId(factionId));
        }

        public void Update(
            GameTime gameTime,
            Ship playerShip,
            IReadOnlyList<NpcShip> npcs,
            PlayerCredits playerCredits,
            ReputationManager reputationManager,
            NotificationManager notificationManager = null,
            Action<string> log = null)
        {
            if (gameTime == null || playerShip == null || playerCredits == null || reputationManager == null)
            {
                return;
            }

            float deltaTime = Math.Max(0f, (float)gameTime.ElapsedGameTime.TotalSeconds);

            if (_cooldownTimer > 0f)
            {
                _cooldownTimer = Math.Max(0f, _cooldownTimer - deltaTime);
            }

            if (State == PoliceScanState.Cleared || State == PoliceScanState.Enforcement)
            {
                if (_resultTimer > 0f)
                {
                    _resultTimer = Math.Max(0f, _resultTimer - deltaTime);
                }

                if (_resultTimer <= 0f)
                {
                    Reset();
                }

                return;
            }

            if (State == PoliceScanState.ContrabandDetected)
            {
                UpdateContrabandDetected(playerShip, playerCredits, reputationManager, notificationManager, log, deltaTime);
                return;
            }

            if (State == PoliceScanState.Scanning)
            {
                if (!IsScannerValid(playerShip))
                {
                    CancelScan(log);
                    return;
                }

                _scanTimer += deltaTime;
                if (_scanTimer < ScanDurationSeconds)
                {
                    return;
                }

                CompleteScan(playerShip, playerCredits, reputationManager, notificationManager, log);
                return;
            }

            if (_cooldownTimer > 0f)
            {
                return;
            }

            if (playerShip?.CargoHold == null || npcs == null || npcs.Count == 0)
            {
                return;
            }

            NpcShip candidate = FindNearestLawfulScanner(playerShip, npcs);
            if (candidate != null)
            {
                StartScan(candidate, notificationManager, log);
            }
        }

        public void Reset()
        {
            State = PoliceScanState.Idle;
            _activeScanner = null;
            _scanTimer = 0f;
            _resultTimer = 0f;
        }

        public bool TryJettisonContraband(Ship playerShip, NotificationManager notificationManager = null, Action<string> log = null)
        {
            if (playerShip?.CargoHold == null)
            {
                return false;
            }

            Dictionary<string, int> removedCargo = playerShip.CargoHold.RemoveContraband();
            if (removedCargo.Count == 0)
            {
                return false;
            }

            string removedSummary = BuildRemovedCargoSummary(removedCargo);
            log?.Invoke($"[POLICE SCAN] Jettisoned contraband: {removedSummary}.");

            if (State == PoliceScanState.ContrabandDetected && !HasContraband(playerShip.CargoHold))
            {
                ClearByJettison(notificationManager, log);
                return true;
            }

            notificationManager?.ShowMessage("Contraband jettisoned", 2f);
            return true;
        }

        private void StartScan(NpcShip scanner, NotificationManager notificationManager, Action<string> log)
        {
            _activeScanner = scanner;
            _scanTimer = 0f;
            State = PoliceScanState.Scanning;
            notificationManager?.ShowMessage("Police scan initiated", 2f);
            log?.Invoke($"[POLICE SCAN] Scan initiated by {scanner.Name} ({scanner.FactionId}).");
        }

        private void CompleteScan(Ship playerShip, PlayerCredits playerCredits, ReputationManager reputationManager, NotificationManager notificationManager, Action<string> log)
        {
            bool hasContraband = HasContraband(playerShip.CargoHold);
            _scanTimer = 0f;
            _cooldownTimer = RetryCooldownSeconds;

            if (!hasContraband)
            {
                State = PoliceScanState.Cleared;
                _resultTimer = ResultHoldSeconds;
                notificationManager?.ShowMessage("Scan complete: cargo clean", 2f);
                log?.Invoke("[POLICE SCAN] Scan complete: cargo clean.");
                return;
            }

            State = PoliceScanState.ContrabandDetected;
            _resultTimer = GracePeriodSeconds;
            notificationManager?.ShowMessage("Contraband detected - jettison illegal cargo or pay fine", 2f);
            log?.Invoke("[POLICE SCAN] Contraband detected.");
            log?.Invoke($"[POLICE SCAN] Grace period started: {GracePeriodSeconds:F1}s.");
        }

        private void UpdateContrabandDetected(
            Ship playerShip,
            PlayerCredits playerCredits,
            ReputationManager reputationManager,
            NotificationManager notificationManager,
            Action<string> log,
            float deltaTime)
        {
            if (!HasContraband(playerShip.CargoHold))
            {
                ClearByJettison(notificationManager, log);
                return;
            }

            if (!IsScannerValid(playerShip))
            {
                EscalateAfterDetection(reputationManager, notificationManager, log);
                return;
            }

            if (_resultTimer > 0f)
            {
                _resultTimer = Math.Max(0f, _resultTimer - deltaTime);
                if (_resultTimer > 0f)
                {
                    return;
                }
            }

            if (playerCredits.CanAfford(FineAmount) && playerCredits.RemoveCredits(FineAmount))
            {
                reputationManager.AddReputation(ActiveScannerFactionId, PaidReputationPenalty, "police scan fine");
                State = PoliceScanState.Cleared;
                _resultTimer = ResultHoldSeconds;
                notificationManager?.ShowMessage("Fine paid", 2f);
                log?.Invoke($"[POLICE SCAN] Fine paid: {FineAmount:N0} credits.");
                return;
            }

            EscalateToEnforcement(reputationManager, notificationManager, log);
        }

        private void CancelScan(Action<string> log)
        {
            log?.Invoke("[POLICE SCAN] Scan canceled: out of range.");
            _scanTimer = 0f;
            _activeScanner = null;
            State = PoliceScanState.Idle;
            _cooldownTimer = 1f;
        }

        private void ClearByJettison(NotificationManager notificationManager, Action<string> log)
        {
            _scanTimer = 0f;
            _resultTimer = ResultHoldSeconds;
            _cooldownTimer = RetryCooldownSeconds;
            State = PoliceScanState.Cleared;
            notificationManager?.ShowMessage("Contraband jettisoned - scan cleared", 2f);
            log?.Invoke("[POLICE SCAN] Scan cleared by jettison.");
        }

        private void EscalateAfterDetection(ReputationManager reputationManager, NotificationManager notificationManager, Action<string> log)
        {
            if (reputationManager != null)
            {
                reputationManager.AddReputation(ActiveScannerFactionId, FleeAfterDetectionReputationPenalty, "police scan flee");
            }

            EscalateToEnforcementState(notificationManager, log, "[POLICE SCAN] Fled after contraband detection: enforcement escalated.");
        }

        private void EscalateToEnforcement(ReputationManager reputationManager, NotificationManager notificationManager, Action<string> log)
        {
            if (reputationManager != null)
            {
                reputationManager.AddReputation(ActiveScannerFactionId, EnforcementReputationPenalty, "police scan enforcement");
            }

            EscalateToEnforcementState(notificationManager, log, "[POLICE SCAN] Enforcement triggered: police hostile.");
        }

        private void EscalateToEnforcementState(NotificationManager notificationManager, Action<string> log, string logMessage)
        {
            State = PoliceScanState.Enforcement;
            _resultTimer = ResultHoldSeconds;
            notificationManager?.ShowMessage("Police hostile", 2f);
            log?.Invoke(logMessage);
        }

        private static string BuildRemovedCargoSummary(Dictionary<string, int> removedCargo)
        {
            if (removedCargo == null || removedCargo.Count == 0)
            {
                return string.Empty;
            }

            List<string> parts = new List<string>();
            foreach (var entry in removedCargo)
            {
                if (entry.Value <= 0)
                {
                    continue;
                }

                parts.Add($"{entry.Key} x{entry.Value}");
            }

            return string.Join(", ", parts);
        }

        private bool IsScannerValid(Ship playerShip)
        {
            if (_activeScanner == null || _activeScanner.IsDestroyed || !IsLawfulScannerFaction(_activeScanner.FactionId))
            {
                return false;
            }

            float distance = Vector3.Distance(playerShip.Position, _activeScanner.Position);
            return distance <= CancelRange;
        }

        private NpcShip FindNearestLawfulScanner(Ship playerShip, IReadOnlyList<NpcShip> npcs)
        {
            NpcShip nearest = null;
            float nearestDistanceSq = ScanRange * ScanRange;

            for (int i = 0; i < npcs.Count; i++)
            {
                NpcShip npc = npcs[i];
                if (npc == null || npc.IsDestroyed || !IsLawfulScannerFaction(npc.FactionId))
                {
                    continue;
                }

                float distanceSq = Vector3.DistanceSquared(playerShip.Position, npc.Position);
                if (distanceSq <= nearestDistanceSq)
                {
                    nearestDistanceSq = distanceSq;
                    nearest = npc;
                }
            }

            return nearest;
        }

        private static bool HasContraband(CargoHold cargoHold)
        {
            if (cargoHold == null)
            {
                return false;
            }

            Dictionary<string, int> cargo = cargoHold.GetAllCommodities();
            foreach (var entry in cargo)
            {
                Commodity commodity = CommodityCatalog.GetByName(entry.Key) ?? CommodityCatalog.GetById(entry.Key);
                if (commodity?.IsContraband == true && entry.Value > 0)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
