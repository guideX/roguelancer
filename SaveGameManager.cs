using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Roguelancer
{
    /// <summary>
    /// Reads and writes versioned local save files.
    /// </summary>
    public sealed class SaveGameManager
    {
        private readonly string _savePath;

        private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();

        public SaveGameManager(string savePath = null)
        {
            _savePath = string.IsNullOrWhiteSpace(savePath) ? GetDefaultSavePath() : savePath;
        }

        public string SavePath => _savePath;

        public static string GetDefaultSavePath()
        {
            string root = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            if (string.IsNullOrWhiteSpace(root))
            {
                root = AppContext.BaseDirectory;
            }

            return Path.Combine(root, "Roguelancer", "Saves", "player_save.json");
        }

        public bool HasSaveFile()
        {
            return File.Exists(_savePath);
        }

        public bool TrySave(SaveGameData data, out string failureReason)
        {
            failureReason = string.Empty;

            if (data == null)
            {
                failureReason = "save data was null";
                Console.WriteLine("[SAVE] Save failed: save data was null");
                return false;
            }

            try
            {
                data.SchemaVersion = SaveGameData.CurrentSchemaVersion;
                EnsureSaveDirectoryExists();

                string json = JsonSerializer.Serialize(data, JsonOptions);
                string directory = Path.GetDirectoryName(_savePath) ?? string.Empty;
                string tempPath = Path.Combine(directory, $"{Path.GetFileNameWithoutExtension(_savePath)}_{Guid.NewGuid():N}.tmp");

                File.WriteAllText(tempPath, json);
                File.Move(tempPath, _savePath, true);

                Console.WriteLine($"[SAVE] Saved game to {_savePath}");
                return true;
            }
            catch (Exception ex)
            {
                failureReason = ex.Message;
                Console.WriteLine($"[SAVE] Save failed: {ex.Message}");
                return false;
            }
        }

        public bool TryLoad(out SaveGameData data, out string failureReason)
        {
            data = null;
            failureReason = string.Empty;

            if (!File.Exists(_savePath))
            {
                failureReason = "save file not found";
                return false;
            }

            try
            {
                string json = File.ReadAllText(_savePath);
                data = JsonSerializer.Deserialize<SaveGameData>(json, JsonOptions);
                if (data == null)
                {
                    failureReason = "save file did not contain valid save data";
                    Console.WriteLine($"[SAVE] Load failed: {failureReason}");
                    return false;
                }

                if (data.SchemaVersion > SaveGameData.CurrentSchemaVersion)
                {
                    failureReason = $"save file version {data.SchemaVersion} is newer than supported version {SaveGameData.CurrentSchemaVersion}";
                    Console.WriteLine($"[SAVE] Load failed: {failureReason}");
                    data = null;
                    return false;
                }

                if (data.SchemaVersion <= 0)
                {
                    failureReason = "save file schema version was missing or invalid";
                    Console.WriteLine($"[SAVE] Load failed: {failureReason}");
                    data = null;
                    return false;
                }

                Console.WriteLine($"[SAVE] Loaded game from {_savePath}");
                return true;
            }
            catch (JsonException ex)
            {
                failureReason = $"invalid JSON: {ex.Message}";
                Console.WriteLine($"[SAVE] Load failed: {failureReason}");
                return false;
            }
            catch (Exception ex)
            {
                failureReason = ex.Message;
                Console.WriteLine($"[SAVE] Load failed: {ex.Message}");
                return false;
            }
        }

        public ShipLoadout BuildLoadout(SaveGameData data, out List<string> warnings)
        {
            return BuildLoadout(data, null, out warnings);
        }

        /// <summary>
        /// Builds a saved loadout against the current ship's hardpoint
        /// metadata. The legacy overload above intentionally retains the
        /// generic layout for older smoke callers and generic future ships.
        /// </summary>
        public ShipLoadout BuildLoadout(SaveGameData data, ShipDefinition shipDefinition, out List<string> warnings)
        {
            warnings = new List<string>();
            ShipLoadout loadout = shipDefinition == null
                ? ShipLoadout.CreateStarterLoadout(false)
                : ShipLoadout.CreateForShip(shipDefinition, false);

            if (data == null)
            {
                warnings.Add("save data was null");
                return loadout;
            }

            foreach (var owned in data.OwnedEquipment ?? new List<SaveOwnedEquipmentData>())
            {
                if (owned == null || owned.Quantity <= 0)
                {
                    continue;
                }

                EquipmentDefinition definition = ResolveEquipmentDefinition(owned.EquipmentId, owned.EquipmentType, warnings, allowHardpointFallback: false);
                if (definition == null)
                {
                    warnings.Add($"skipped unknown owned equipment '{owned.EquipmentId}'");
                    continue;
                }

                loadout.AddOwnedEquipment(definition, owned.Quantity);
            }

            foreach (var mounted in data.MountedEquipment ?? new List<SaveMountedEquipmentData>())
            {
                if (mounted == null || string.IsNullOrWhiteSpace(mounted.HardpointId))
                {
                    continue;
                }

                EquipmentDefinition definition = ResolveEquipmentDefinition(mounted.EquipmentId, mounted.EquipmentType, warnings, allowHardpointFallback: true);
                if (definition == null)
                {
                    warnings.Add($"skipped unknown mounted equipment '{mounted.EquipmentId}' on {mounted.HardpointId}");
                    continue;
                }

                ShipHardpoint hardpoint = loadout.GetHardpointById(mounted.HardpointId);
                if (hardpoint == null)
                {
                    hardpoint = loadout.FindFirstCompatibleEmptyHardpoint(definition);
                    if (hardpoint != null)
                    {
                        warnings.Add($"remapped saved hardpoint '{mounted.HardpointId}' to '{hardpoint.Id}' for {definition.Id}");
                    }
                    else
                    {
                        warnings.Add($"skipped missing hardpoint '{mounted.HardpointId}' for incompatible {definition.Id}");
                        continue;
                    }
                }

                if (loadout.GetOwnedCount(definition.Id) <= 0)
                {
                    loadout.AddOwnedEquipment(definition, 1);
                }

                if (!hardpoint.IsEmpty)
                {
                    warnings.Add($"hardpoint '{mounted.HardpointId}' was already occupied");
                    continue;
                }

                if (!loadout.TryMountEquipment(hardpoint.Id, definition, out string message))
                {
                    warnings.Add(message);
                }
            }

            // These optional Phase 43 fields default to zero for older saves;
            // CombatConsumableInventory clamps malformed values to its finite
            // per-type limits.
            loadout.CombatConsumables.SetQuantities(data.Nanobots, data.ShieldBatteries);

            // Phase 44 intentionally keeps schema 10. Older schema-10 saves
            // have no powerplant fields because mounted/owned equipment is
            // catalog-backed. If a malformed/old loadout has a valid owned
            // plant but no active mount, use that existing item first; only
            // add the deterministic civilian fallback when none is owned.
            if (loadout.GetMountedPowerplant() == null)
            {
                PowerplantEquipmentDefinition existing = loadout.OwnedEquipment.Keys
                    .Select(id => EquipmentCatalog.GetById(id))
                    .OfType<PowerplantEquipmentDefinition>()
                    .OrderBy(plant => plant.Id, StringComparer.OrdinalIgnoreCase)
                    .FirstOrDefault();
                PowerplantEquipmentDefinition starter = existing ??
                    EquipmentCatalog.GetById("civilian_powerplant") as PowerplantEquipmentDefinition ??
                    EquipmentCatalog.GetFallbackForType(EquipmentType.Powerplant) as PowerplantEquipmentDefinition;
                if (starter != null && (existing != null || loadout.AddOwnedEquipment(starter, 1)) &&
                    !loadout.TryMountEquipment(starter, out string powerplantMessage))
                {
                    warnings.Add(powerplantMessage);
                }
            }

            // Phase 45 keeps schema 10. Thruster ownership and mounting are
            // already represented by the catalog-backed equipment lists, so
            // schema-10 saves from before this feature simply have no
            // thruster entries. Preserve any valid owned thruster first;
            // otherwise add exactly one deterministic civilian fallback.
            if (loadout.GetMountedThruster() == null)
            {
                ThrusterEquipmentDefinition existing = loadout.OwnedEquipment.Keys
                    .Select(id => EquipmentCatalog.GetById(id))
                    .OfType<ThrusterEquipmentDefinition>()
                    .Where(thruster => thruster.IsValid)
                    .OrderBy(thruster => thruster.Id, StringComparer.OrdinalIgnoreCase)
                    .FirstOrDefault();
                ThrusterEquipmentDefinition starter = existing ??
                    EquipmentCatalog.GetById("civilian_thruster") as ThrusterEquipmentDefinition ??
                    EquipmentCatalog.GetFallbackForType(EquipmentType.Thruster) as ThrusterEquipmentDefinition;
                if (starter != null && (existing != null || loadout.AddOwnedEquipment(starter, 1)) &&
                    !loadout.TryMountEquipment(starter, out string thrusterMessage))
                {
                    warnings.Add(thrusterMessage);
                }
            }

            return loadout;
        }

        public void ApplyCargo(CargoHold cargoHold, SaveGameData data, out List<string> warnings)
        {
            warnings = new List<string>();

            if (cargoHold == null || data == null)
            {
                warnings.Add("cargo hold or save data was null");
                return;
            }

            cargoHold.Clear();

            foreach (var item in data.Cargo ?? new List<SaveCargoItemData>())
            {
                if (item == null || item.Quantity <= 0)
                {
                    continue;
                }

                Commodity commodity = CommodityCatalog.GetByIdOrName(item.CommodityId);
                if (commodity == null)
                {
                    warnings.Add($"skipped unknown commodity '{item.CommodityId}'");
                    continue;
                }

                CargoProvenance provenance = item.IsStolen
                    ? CargoProvenance.Stolen
                    : CargoProvenance.Clean;
                bool added = item.MissionBound || item.MissionId > 0
                    ? cargoHold.AddMissionCargo(item.MissionId, commodity, item.Quantity, provenance)
                    : cargoHold.AddCommodity(commodity, item.Quantity, provenance);
                if (!added)
                {
                    warnings.Add(item.MissionBound || item.MissionId > 0
                        ? $"could not restore mission cargo {item.MissionId}:{item.Quantity}x {commodity.Name}"
                        : $"could not fit {item.Quantity}x {commodity.Name} in cargo hold");
                }
            }
        }

        public void ApplyReputation(ReputationManager reputationManager, SaveGameData data)
        {
            if (reputationManager == null || data == null)
            {
                return;
            }

            Dictionary<string, float> standings = new(StringComparer.OrdinalIgnoreCase);
            foreach (var entry in data.FactionReputation ?? new List<SaveFactionReputationData>())
            {
                if (entry == null || string.IsNullOrWhiteSpace(entry.FactionId))
                {
                    continue;
                }

                standings[FactionManager.NormalizeFactionId(entry.FactionId)] = entry.Standing;
            }

            reputationManager.LoadStandings(standings);
        }

        public void ApplyTemporaryHostility(ReputationManager reputationManager, SaveGameData data)
        {
            if (reputationManager == null || data == null)
                return;

            List<TemporaryHostilitySnapshot> snapshots = new();
            foreach (SaveTemporaryHostilityData entry in data.TemporaryHostility ?? new List<SaveTemporaryHostilityData>())
            {
                if (entry == null || string.IsNullOrWhiteSpace(entry.FactionId) ||
                    string.Equals(entry.Reason, TemporaryHostilityManager.FugitivePursuitReason, StringComparison.OrdinalIgnoreCase) ||
                    float.IsNaN(entry.RemainingSeconds) || float.IsInfinity(entry.RemainingSeconds) ||
                    entry.RemainingSeconds <= 0f)
                    continue;

                snapshots.Add(new TemporaryHostilitySnapshot(
                    FactionManager.NormalizeFactionId(entry.FactionId),
                    entry.Reason ?? string.Empty,
                    entry.RemainingSeconds));
            }

            reputationManager.TemporaryHostility.RestoreSnapshot(snapshots);
        }

        public void ApplyMissions(MissionManager missionManager, SaveGameData data, out List<string> warnings)
        {
            warnings = new List<string>();

            if (missionManager == null || data == null)
            {
                warnings.Add("mission manager or save data was null");
                return;
            }

            List<Mission> active = new();
            List<Mission> completed = new();

            foreach (var missionData in data.ActiveMissions ?? new List<SaveMissionData>())
            {
                var mission = CreateMissionFromSave(missionData, warnings);
                if (mission == null)
                {
                    continue;
                }

                mission.Status = mission.Status is MissionStatus.Accepted or MissionStatus.InProgress
                    ? MissionStatus.InProgress
                    : MissionStatus.Active;
                active.Add(mission);
            }

            foreach (var missionData in data.CompletedMissions ?? new List<SaveMissionData>())
            {
                var mission = CreateMissionFromSave(missionData, warnings);
                if (mission == null)
                {
                    continue;
                }

                if (mission.Status == MissionStatus.Available || mission.Status == MissionStatus.InProgress)
                {
                    mission.Status = MissionStatus.Completed;
                }

                if (!mission.RewardPaid && mission.Status != MissionStatus.Rewarded)
                {
                    completed.Add(mission);
                }
            }

            missionManager.RestoreState(active, completed);
        }

        public List<SaveOwnedEquipmentData> CaptureOwnedEquipment(ShipLoadout loadout)
        {
            var result = new List<SaveOwnedEquipmentData>();
            if (loadout == null)
            {
                return result;
            }

            foreach (var kvp in loadout.OwnedEquipment)
            {
                if (kvp.Value <= 0)
                {
                    continue;
                }

                EquipmentDefinition definition = EquipmentCatalog.GetById(kvp.Key);
                if (definition is ConsumableEquipmentDefinition)
                {
                    continue;
                }

                result.Add(new SaveOwnedEquipmentData
                {
                    EquipmentId = definition?.Id ?? kvp.Key,
                    EquipmentType = definition?.EquipmentType ?? ResolveOwnedEquipmentType(loadout, kvp.Key),
                    Quantity = kvp.Value
                });
            }

            return result;
        }

        public (int Nanobots, int ShieldBatteries) CaptureConsumables(ShipLoadout loadout)
        {
            return loadout == null
                ? (0, 0)
                : (loadout.CombatConsumables.Nanobots, loadout.CombatConsumables.ShieldBatteries);
        }

        public List<SaveMountedEquipmentData> CaptureMountedEquipment(ShipLoadout loadout)
        {
            var result = new List<SaveMountedEquipmentData>();
            if (loadout == null)
            {
                return result;
            }

            foreach (var hardpoint in loadout.Hardpoints)
            {
                if (hardpoint == null || hardpoint.IsEmpty)
                {
                    continue;
                }

                EquipmentDefinition definition = EquipmentCatalog.GetById(hardpoint.MountedEquipmentId);
                result.Add(new SaveMountedEquipmentData
                {
                    HardpointId = hardpoint.Id ?? string.Empty,
                    EquipmentId = definition?.Id ?? hardpoint.MountedEquipmentId,
                    EquipmentType = definition?.EquipmentType ?? ResolveHardpointFallbackType(hardpoint)
                });
            }

            return result;
        }

        public List<SaveCargoItemData> CaptureCargo(CargoHold cargoHold)
        {
            var result = new List<SaveCargoItemData>();
            if (cargoHold == null)
            {
                return result;
            }

            foreach (var kvp in cargoHold.GetAllCommodities())
            {
                Commodity commodity = CommodityCatalog.GetByName(kvp.Key) ?? CommodityCatalog.GetById(kvp.Key);
                int cleanQuantity = cargoHold.GetSellableCleanCommodityQuantity(kvp.Key);
                if (cleanQuantity > 0)
                {
                    result.Add(new SaveCargoItemData
                    {
                        CommodityId = commodity?.Id ?? kvp.Key,
                        Quantity = cleanQuantity,
                        IsStolen = false
                    });
                }

                int stolenQuantity = cargoHold.GetSellableStolenCommodityQuantity(kvp.Key);
                if (stolenQuantity > 0)
                {
                    result.Add(new SaveCargoItemData
                    {
                        CommodityId = commodity?.Id ?? kvp.Key,
                        Quantity = stolenQuantity,
                        IsStolen = true
                    });
                }
            }

            foreach (MissionCargoReservation reservation in cargoHold.GetMissionCargoReservations())
            {
                if (reservation.MissionId <= 0 || reservation.Quantity <= 0)
                {
                    continue;
                }

                result.Add(new SaveCargoItemData
                {
                    CommodityId = reservation.CommodityId ?? reservation.CommodityName,
                    Quantity = reservation.Quantity,
                    MissionId = reservation.MissionId,
                    MissionBound = true,
                    IsStolen = reservation.IsStolen
                });
            }

            return result;
        }

        public List<SaveFactionReputationData> CaptureReputation(ReputationManager reputationManager)
        {
            var result = new List<SaveFactionReputationData>();
            if (reputationManager == null)
            {
                return result;
            }

            foreach (var kvp in reputationManager.GetStandingsSnapshot())
            {
                result.Add(new SaveFactionReputationData
                {
                    FactionId = kvp.Key,
                    Standing = NormalizeStanding(kvp.Value)
                });
            }

            return result;
        }

        public List<SaveMissionData> CaptureMissions(IEnumerable<Mission> missions)
        {
            var result = new List<SaveMissionData>();
            if (missions == null)
            {
                return result;
            }

            foreach (var mission in missions)
            {
                if (mission == null)
                {
                    continue;
                }

                result.Add(new SaveMissionData
                {
                    DefinitionId = mission.DefinitionId ?? string.Empty,
                    Title = mission.Title ?? string.Empty,
                    MissionId = mission.Id,
                    Type = mission.Type,
                    Difficulty = mission.Difficulty,
                    Status = mission.Status,
                    Target = mission.Target ?? string.Empty,
                    Destination = mission.Destination ?? string.Empty,
                    Reward = mission.Reward,
                    TimeLimit = mission.TimeLimit,
                    ElapsedTime = mission.ElapsedTime,
                    Description = mission.Description ?? string.Empty,
                    OfferedBy = mission.OfferedBy ?? string.Empty,
                    FactionId = FactionManager.NormalizeFactionId(mission.FactionId),
                    MinimumEmployerReputation = NormalizeOptionalStanding(mission.MinimumEmployerReputation),
                    MaximumEmployerReputation = NormalizeOptionalStanding(mission.MaximumEmployerReputation),
                    ObjectiveComplete = mission.ObjectiveComplete,
                    TargetLocation = mission.TargetLocation ?? string.Empty,
                    TargetSystemIndex = mission.TargetSystemIndex,
                    TargetCount = mission.TargetCount,
                    CurrentProgress = mission.CurrentProgress,
                    RequiredProgress = mission.RequiredProgress,
                    ObjectiveRadius = mission.ObjectiveRadius,
                    OriginStationId = mission.OriginStationId ?? string.Empty,
                    OriginStationName = mission.OriginStationName ?? string.Empty,
                    OriginSystemIndex = mission.OriginSystemIndex,
                    AcceptedAtUtc = mission.AcceptedAtUtc == DateTime.MinValue ? string.Empty : mission.AcceptedAtUtc.ToString("O"),
                    RewardPaid = mission.RewardPaid,
                    ReputationReward = NormalizeStanding(mission.ReputationReward),
                    ReputationRewardApplied = mission.ReputationRewardApplied,
                    TargetPosition = mission.TargetPosition.HasValue ? SaveVector3Data.From(mission.TargetPosition.Value) : null,
                    SourceStationName = mission.SourceStationName ?? string.Empty,
                    DestinationStationId = mission.DestinationStationId ?? string.Empty,
                    PackageId = mission.PackageId ?? string.Empty,
                    PackageQuantity = mission.PackageQuantity,
                    PackageVolume = mission.PackageVolume,
                    MissionCargoLoaded = mission.MissionCargoLoaded,
                    DeliveredQuantity = mission.DeliveredQuantity,
                     CommodityId = mission.CommodityId ?? string.Empty,
                     RequiredQuantity = mission.RequiredQuantity,
                     IssuedCargoQuantity = mission.IssuedCargoQuantity,
                     SmugglingStage = mission.SmugglingStage,
                     SmugglingPoliceDetected = mission.SmugglingPoliceDetected,
                     SmugglingJettisonedQuantity = mission.SmugglingJettisonedQuantity,
                     TargetLaneId = mission.TargetLaneId ?? string.Empty,
                    TargetSegmentId = mission.TargetSegmentId ?? string.Empty,
                    TargetRingIndex = mission.TargetRingIndex,
                    HoldDurationSeconds = mission.HoldDurationSeconds,
                    HoldProgressSeconds = mission.HoldProgressSeconds,
                    PlayerDisruptionObserved = mission.PlayerDisruptionObserved,
                    LastQualifiedDisruptionAtSeconds = mission.LastQualifiedDisruptionAtSeconds,
                    PoliceReputationPenalty = mission.PoliceReputationPenalty,
                    PoliceConsequenceApplied = mission.PoliceConsequenceApplied,
                    SecurityResponseTriggered = mission.SecurityResponseTriggered,
                    FailureReason = mission.FailureReason ?? string.Empty,
                    DefenseStage = mission.DefenseStage,
                    DefenseAttackForceSize = mission.DefenseAttackForceSize,
                    DefenseAttackersRemaining = mission.DefenseAttackersRemaining,
                    DefenseActivationRadius = mission.DefenseActivationRadius,
                    DefenseFailureHoldSeconds = mission.DefenseFailureHoldSeconds,
                    DefenseFailureHoldProgressSeconds = mission.DefenseFailureHoldProgressSeconds,
                    DefenseActivationStarted = mission.DefenseActivationStarted,
                    ConvoyRouteId = mission.ConvoyRouteId ?? string.Empty,
                    ConvoyRouteLaneId = mission.ConvoyRouteLaneId ?? string.Empty,
                    ConvoyRouteSegmentId = mission.ConvoyRouteSegmentId ?? string.Empty,
                    ConvoyRouteDirection = mission.ConvoyRouteDirection,
                    ConvoyFactionId = mission.ConvoyFactionId ?? string.Empty,
                    ConvoyHostileFactionId = mission.ConvoyHostileFactionId ?? string.Empty,
                    ConvoyShipArchetype = mission.ConvoyShipArchetype ?? string.Empty,
                    ConvoyRouteRingIndex = mission.ConvoyRouteRingIndex,
                    ConvoyEncounterRingIndex = mission.ConvoyEncounterRingIndex,
                    ConvoyShipCount = mission.ConvoyShipCount,
                    ConvoyRequiredSurvivors = mission.ConvoyRequiredSurvivors,
                    ConvoySurvivors = mission.ConvoySurvivors,
                    ConvoyDestroyedCount = mission.ConvoyDestroyedCount,
                    ConvoyDestroyedMask = mission.ConvoyDestroyedMask,
                    ConvoyArrivedCount = mission.ConvoyArrivedCount,
                    ConvoyArrivedMask = mission.ConvoyArrivedMask,
                    ConvoyAttackForceSize = mission.ConvoyAttackForceSize,
                    ConvoyAttackersRemaining = mission.ConvoyAttackersRemaining,
                    ConvoyStage = mission.ConvoyStage,
                    ConvoyRendezvousRadius = mission.ConvoyRendezvousRadius,
                    ConvoyAbandonmentRadius = mission.ConvoyAbandonmentRadius,
                    ConvoyAbandonmentGraceSeconds = mission.ConvoyAbandonmentGraceSeconds,
                    ConvoyAbandonmentProgressSeconds = mission.ConvoyAbandonmentProgressSeconds,
                    ConvoyArrivalRadius = mission.ConvoyArrivalRadius,
                    ConvoyRouteStarted = mission.ConvoyRouteStarted,
                    ConvoyEncounterActivated = mission.ConvoyEncounterActivated,
                    ConvoyEncounterResolved = mission.ConvoyEncounterResolved,
                    ConvoyEncounterSpawnAttempted = mission.ConvoyEncounterSpawnAttempted,
                    ConvoyRendezvousPosition = mission.ConvoyRendezvousPosition.HasValue ? SaveVector3Data.From(mission.ConvoyRendezvousPosition.Value) : null,
                    ConvoyEncounterPosition = mission.ConvoyEncounterPosition.HasValue ? SaveVector3Data.From(mission.ConvoyEncounterPosition.Value) : null,
                     ConvoyDestinationPosition = mission.ConvoyDestinationPosition.HasValue ? SaveVector3Data.From(mission.ConvoyDestinationPosition.Value) : null,
                     RaidRouteId = mission.RaidRouteId ?? string.Empty,
                     RaidRouteLaneId = mission.RaidRouteLaneId ?? string.Empty,
                     RaidRouteSegmentId = mission.RaidRouteSegmentId ?? string.Empty,
                     RaidRouteDirection = mission.RaidRouteDirection,
                     RaidConvoyFactionId = mission.RaidConvoyFactionId ?? string.Empty,
                     RaidShipArchetype = mission.RaidShipArchetype ?? string.Empty,
                     RaidRouteRingIndex = mission.RaidRouteRingIndex,
                     RaidInterceptionRingIndex = mission.RaidInterceptionRingIndex,
                     RaidShipCount = mission.RaidShipCount,
                     RaidDestroyedCount = mission.RaidDestroyedCount,
                     RaidEscapedCount = mission.RaidEscapedCount,
                     RaidDestroyedMask = mission.RaidDestroyedMask,
                     RaidEscapedMask = mission.RaidEscapedMask,
                     RaidCargoReleasedMask = mission.RaidCargoReleasedMask,
                     RaidCargoLostQuantity = mission.RaidCargoLostQuantity,
                     RaidCargoReleasedQuantity = mission.RaidCargoReleasedQuantity,
                     RaidCargoRecoveredQuantity = mission.RaidCargoRecoveredQuantity,
                     RaidCommodityId = mission.RaidCommodityId ?? string.Empty,
                     RaidRequiredQuantity = mission.RaidRequiredQuantity,
                     RaidTotalAllocatedQuantity = mission.RaidTotalAllocatedQuantity,
                     RaidRemainingPossibleQuantity = mission.RaidRemainingPossibleQuantity,
                     RaidCargoAllocation = mission.RaidCargoAllocation?.ToList() ?? new List<int>(),
                     RaidStage = mission.RaidStage,
                     RaidRouteStarted = mission.RaidRouteStarted,
                     RaidInterceptionActivated = mission.RaidInterceptionActivated,
                     RaidInterceptionPosition = mission.RaidInterceptionPosition.HasValue ? SaveVector3Data.From(mission.RaidInterceptionPosition.Value) : null,
                     RaidDestinationPosition = mission.RaidDestinationPosition.HasValue ? SaveVector3Data.From(mission.RaidDestinationPosition.Value) : null
                });
            }

            return result;
        }

        public List<SaveTemporaryHostilityData> CaptureTemporaryHostility(ReputationManager reputationManager)
        {
            List<SaveTemporaryHostilityData> result = new();
            if (reputationManager == null)
                return result;

            foreach (TemporaryHostilitySnapshot snapshot in reputationManager.TemporaryHostility.GetActiveSnapshot())
            {
                if (snapshot.RemainingSeconds <= 0f ||
                    string.Equals(snapshot.Reason, TemporaryHostilityManager.FugitivePursuitReason, StringComparison.OrdinalIgnoreCase) ||
                    float.IsNaN(snapshot.RemainingSeconds) || float.IsInfinity(snapshot.RemainingSeconds))
                    continue;

                result.Add(new SaveTemporaryHostilityData
                {
                    FactionId = FactionManager.NormalizeFactionId(snapshot.FactionId),
                    Reason = snapshot.Reason ?? string.Empty,
                    RemainingSeconds = Math.Clamp(snapshot.RemainingSeconds, 0f, TemporaryHostilityManager.MaximumDurationSeconds)
                });
            }

            return result;
        }

        public List<Mission> BuildMissionList(IEnumerable<SaveMissionData> missions, out List<string> warnings)
        {
            warnings = new List<string>();
            var result = new List<Mission>();
            if (missions == null)
            {
                return result;
            }

            foreach (var missionData in missions)
            {
                var mission = CreateMissionFromSave(missionData, warnings);
                if (mission != null)
                {
                    result.Add(mission);
                }
            }

            return result;
        }

        private void EnsureSaveDirectoryExists()
        {
            string directory = Path.GetDirectoryName(_savePath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }
        }

        private static JsonSerializerOptions CreateJsonOptions()
        {
            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNameCaseInsensitive = true,
                AllowTrailingCommas = true,
                ReadCommentHandling = JsonCommentHandling.Skip
            };
            options.Converters.Add(new JsonStringEnumConverter());
            return options;
        }

        private static EquipmentDefinition ResolveEquipmentDefinition(
            string equipmentId,
            EquipmentType equipmentType,
            List<string> warnings,
            bool allowHardpointFallback,
            ShipHardpoint hardpoint = null)
        {
            EquipmentDefinition definition = EquipmentCatalog.GetById(equipmentId);
            if (definition != null)
            {
                if (definition.EquipmentType != equipmentType)
                {
                    warnings?.Add($"skipped equipment '{equipmentId}' because its saved type did not match the canonical definition");
                    return null;
                }

                return definition;
            }

            EquipmentDefinition fallback = EquipmentCatalog.GetFallbackForType(equipmentType);
            if (fallback != null)
            {
                if (allowHardpointFallback && hardpoint != null && !hardpoint.CanAccept(fallback))
                {
                    foreach (var fallbackType in hardpoint.AllowedEquipmentTypes ?? new List<EquipmentType>())
                    {
                        fallback = EquipmentCatalog.GetFallbackForType(fallbackType);
                        if (fallback != null && hardpoint.CanAccept(fallback))
                        {
                            warnings?.Add($"replaced missing equipment '{equipmentId}' with fallback '{fallback.Id}'");
                            return fallback;
                        }
                    }

                    return null;
                }

                warnings?.Add($"replaced missing equipment '{equipmentId}' with fallback '{fallback.Id}'");
                return fallback;
            }

            if (allowHardpointFallback && hardpoint != null)
            {
                foreach (var fallbackType in hardpoint.AllowedEquipmentTypes ?? new List<EquipmentType>())
                {
                    fallback = EquipmentCatalog.GetFallbackForType(fallbackType);
                    if (fallback != null)
                    {
                        warnings?.Add($"replaced missing equipment '{equipmentId}' with fallback '{fallback.Id}'");
                        return fallback;
                    }
                }
            }

            return null;
        }

        private static EquipmentType ResolveOwnedEquipmentType(ShipLoadout loadout, string equipmentId)
        {
            if (loadout == null || string.IsNullOrWhiteSpace(equipmentId))
            {
                return EquipmentType.Gun;
            }

            var hardpoint = loadout.Hardpoints.FirstOrDefault(h => string.Equals(h.MountedEquipmentId, equipmentId, StringComparison.OrdinalIgnoreCase));
            if (hardpoint != null && hardpoint.AllowedEquipmentTypes != null && hardpoint.AllowedEquipmentTypes.Count > 0)
            {
                return hardpoint.AllowedEquipmentTypes[0];
            }

            var definition = EquipmentCatalog.GetById(equipmentId);
            return definition?.EquipmentType ?? EquipmentType.Gun;
        }

        private static EquipmentType ResolveHardpointFallbackType(ShipHardpoint hardpoint)
        {
            if (hardpoint?.AllowedEquipmentTypes != null && hardpoint.AllowedEquipmentTypes.Count > 0)
            {
                return hardpoint.AllowedEquipmentTypes[0];
            }

            return EquipmentType.Gun;
        }

        private static Mission CreateMissionFromSave(SaveMissionData data, List<string> warnings)
        {
            if (data == null)
            {
                return null;
            }

            if (data.MissionId <= 0)
            {
                warnings?.Add("skipped mission with invalid id");
                return null;
            }

            DateTime acceptedAtUtc = DateTime.MinValue;
            if (!string.IsNullOrWhiteSpace(data.AcceptedAtUtc) &&
                DateTime.TryParse(data.AcceptedAtUtc, null, System.Globalization.DateTimeStyles.RoundtripKind, out DateTime parsed))
            {
                acceptedAtUtc = parsed;
            }

            return Mission.CreateRestored(
                data.MissionId,
                data.DefinitionId,
                data.Title,
                data.Type,
                data.Difficulty,
                data.Status,
                data.Target,
                data.Destination,
                data.Reward,
                data.TimeLimit,
                data.Description,
                data.OfferedBy,
                data.FactionId,
                data.ElapsedTime,
                data.ObjectiveComplete,
                data.TargetLocation,
                data.TargetSystemIndex,
                data.TargetCount,
                data.CurrentProgress,
                data.RequiredProgress,
                data.ObjectiveRadius,
                data.OriginStationId,
                data.OriginStationName,
                data.OriginSystemIndex,
                acceptedAtUtc,
                data.RewardPaid,
                data.TargetPosition,
                data.SourceStationName,
                data.DestinationStationId,
                data.PackageId,
                data.PackageQuantity,
                data.PackageVolume,
                data.MissionCargoLoaded,
                data.DeliveredQuantity,
                data.CommodityId,
                data.RequiredQuantity,
                data.IssuedCargoQuantity,
                data.ReputationReward,
                data.ReputationRewardApplied,
                data.MinimumEmployerReputation,
                data.MaximumEmployerReputation,
                data.TargetLaneId,
                data.TargetSegmentId,
                data.TargetRingIndex,
                data.HoldDurationSeconds,
                data.HoldProgressSeconds,
                data.PlayerDisruptionObserved,
                data.LastQualifiedDisruptionAtSeconds,
                data.PoliceReputationPenalty,
                data.PoliceConsequenceApplied,
                data.SecurityResponseTriggered,
                data.FailureReason,
                data.DefenseStage,
                data.DefenseAttackForceSize,
                data.DefenseAttackersRemaining,
                data.DefenseActivationRadius,
                data.DefenseFailureHoldSeconds,
                data.DefenseFailureHoldProgressSeconds,
                data.DefenseActivationStarted,
                data.ConvoyRouteId,
                data.ConvoyRouteLaneId,
                data.ConvoyRouteSegmentId,
                data.ConvoyRouteDirection,
                data.ConvoyFactionId,
                data.ConvoyHostileFactionId,
                data.ConvoyShipArchetype,
                data.ConvoyRouteRingIndex,
                data.ConvoyEncounterRingIndex,
                data.ConvoyShipCount,
                data.ConvoyRequiredSurvivors,
                data.ConvoySurvivors,
                data.ConvoyDestroyedCount,
                data.ConvoyDestroyedMask,
                data.ConvoyArrivedCount,
                data.ConvoyArrivedMask,
                data.ConvoyAttackForceSize,
                data.ConvoyAttackersRemaining,
                data.ConvoyStage,
                data.ConvoyRendezvousRadius,
                data.ConvoyAbandonmentRadius,
                data.ConvoyAbandonmentGraceSeconds,
                data.ConvoyAbandonmentProgressSeconds,
                data.ConvoyArrivalRadius,
                data.ConvoyRouteStarted,
                data.ConvoyEncounterActivated,
                data.ConvoyEncounterResolved,
                data.ConvoyEncounterSpawnAttempted,
                data.ConvoyRendezvousPosition,
                data.ConvoyEncounterPosition,
                 data.ConvoyDestinationPosition,
                 data.RaidRouteId,
                 data.RaidRouteLaneId,
                 data.RaidRouteSegmentId,
                 data.RaidRouteDirection,
                 data.RaidConvoyFactionId,
                 data.RaidShipArchetype,
                 data.RaidRouteRingIndex,
                 data.RaidInterceptionRingIndex,
                 data.RaidShipCount,
                 data.RaidDestroyedCount,
                 data.RaidEscapedCount,
                 data.RaidDestroyedMask,
                 data.RaidEscapedMask,
                 data.RaidCargoReleasedMask,
                 data.RaidCargoLostQuantity,
                 data.RaidCargoReleasedQuantity,
                 data.RaidCargoRecoveredQuantity,
                 data.RaidCommodityId,
                 data.RaidRequiredQuantity,
                 data.RaidTotalAllocatedQuantity,
                 data.RaidRemainingPossibleQuantity,
                 data.RaidStage,
                 data.RaidRouteStarted,
                 data.RaidInterceptionActivated,
                 data.RaidInterceptionPosition,
                 data.RaidDestinationPosition,
                  data.RaidCargoAllocation,
                  data.SmugglingStage,
                  data.SmugglingPoliceDetected,
                  data.SmugglingJettisonedQuantity);
        }

        private static float NormalizeStanding(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
            {
                return 0f;
            }

            return Math.Clamp(value, -1f, 1f);
        }

        private static float? NormalizeOptionalStanding(float? value)
        {
            if (!value.HasValue || float.IsNaN(value.Value) || float.IsInfinity(value.Value))
                return null;

            return Math.Clamp(value.Value, -1f, 1f);
        }
    }
}
