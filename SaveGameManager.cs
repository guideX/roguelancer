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
            warnings = new List<string>();
            ShipLoadout loadout = ShipLoadout.CreateStarterLoadout(false);

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

                var hardpoint = loadout.GetHardpointById(mounted.HardpointId);
                if (hardpoint == null)
                {
                    warnings.Add($"skipped missing hardpoint '{mounted.HardpointId}'");
                    continue;
                }

                EquipmentDefinition definition = ResolveEquipmentDefinition(mounted.EquipmentId, mounted.EquipmentType, warnings, allowHardpointFallback: true, hardpoint: hardpoint);
                if (definition == null)
                {
                    warnings.Add($"skipped unknown mounted equipment '{mounted.EquipmentId}' on {mounted.HardpointId}");
                    continue;
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

                if (!cargoHold.AddCommodity(commodity, item.Quantity))
                {
                    warnings.Add($"could not fit {item.Quantity}x {commodity.Name} in cargo hold");
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

                standings[FactionManager.NormalizeFactionId(entry.FactionId)] = NormalizeStanding(entry.Standing);
            }

            reputationManager.LoadStandings(standings);
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

                mission.Status = MissionStatus.Active;
                active.Add(mission);
            }

            foreach (var missionData in data.CompletedMissions ?? new List<SaveMissionData>())
            {
                var mission = CreateMissionFromSave(missionData, warnings);
                if (mission == null)
                {
                    continue;
                }

                if (mission.Status == MissionStatus.Available)
                {
                    mission.Status = MissionStatus.Completed;
                }

                completed.Add(mission);
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
                result.Add(new SaveOwnedEquipmentData
                {
                    EquipmentId = definition?.Id ?? kvp.Key,
                    EquipmentType = definition?.EquipmentType ?? ResolveOwnedEquipmentType(loadout, kvp.Key),
                    Quantity = kvp.Value
                });
            }

            return result;
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
                if (kvp.Value <= 0)
                {
                    continue;
                }

                Commodity commodity = CommodityCatalog.GetByName(kvp.Key) ?? CommodityCatalog.GetById(kvp.Key);
                result.Add(new SaveCargoItemData
                {
                    CommodityId = commodity?.Id ?? kvp.Key,
                    Quantity = kvp.Value
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
                    ObjectiveComplete = mission.ObjectiveComplete
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

            return Mission.CreateRestored(
                data.MissionId,
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
                data.ObjectiveComplete);
        }

        private static float NormalizeStanding(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
            {
                return 0f;
            }

            return Math.Clamp(value, -1f, 1f);
        }
    }
}
