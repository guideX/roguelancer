using Microsoft.Xna.Framework;
using Roguelancer.Configuration;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Roguelancer
{
    /// <summary>
    /// Developer-only validation harness for save/load persistence.
    /// </summary>
    internal sealed class SaveSmokeTest
    {
        private readonly SaveGameManager _saveGameManager;
        private readonly string _tempDirectory;

        public SaveSmokeTest()
        {
            _tempDirectory = Path.Combine(Path.GetTempPath(), "Roguelancer_SaveSmoke_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_tempDirectory);
            _saveGameManager = new SaveGameManager(Path.Combine(_tempDirectory, "player_save.json"));
        }

        public (int Passed, int Failed) Run()
        {
            int passed = 0;
            int failed = 0;

            RunCase(ValidateSaveFileWritten, "save file written", ref passed, ref failed);
            RunCase(ValidateLoadRoundTrip, "load round-trip", ref passed, ref failed);
            RunCase(ValidateCorruptSaveSafety, "corrupt save safety", ref passed, ref failed);
            RunCase(ValidateMissingContentSafety, "missing content safety", ref passed, ref failed);

            Console.WriteLine($"[SAVE SMOKE] RESULT: {passed} passed, {failed} failed");
            TryCleanupTempDirectory();
            return (passed, failed);
        }

        private void RunCase(Func<(bool Success, string FailureReason)> test, string label, ref int passed, ref int failed)
        {
            try
            {
                var result = test();
                if (result.Success)
                {
                    passed++;
                    Console.WriteLine($"[SAVE SMOKE] PASS {label}");
                }
                else
                {
                    failed++;
                    Console.WriteLine($"[SAVE SMOKE] FAIL {label}: {result.FailureReason}");
                }
            }
            catch (Exception ex)
            {
                failed++;
                Console.WriteLine($"[SAVE SMOKE] FAIL {label}: {ex.Message}");
            }
        }

        private (bool Success, string FailureReason) ValidateSaveFileWritten()
        {
            SaveGameData saveData = CreateRoundTripSaveData();
            string saveFailure = string.Empty;
            bool saved = RunSilenced(() => _saveGameManager.TrySave(saveData, out saveFailure));
            if (!saved)
            {
                return Fail($"save failed unexpectedly: {saveFailure}");
            }

            if (!File.Exists(_saveGameManager.SavePath))
            {
                return Fail("save file was not written");
            }

            return Pass();
        }

        private (bool Success, string FailureReason) ValidateLoadRoundTrip()
        {
            SaveGameData original = CreateRoundTripSaveData();
            string saveFailure = string.Empty;
            bool saved = RunSilenced(() => _saveGameManager.TrySave(original, out saveFailure));
            if (!saved)
            {
                return Fail($"save failed unexpectedly: {saveFailure}");
            }

            string loadFailure = string.Empty;
            SaveGameData loaded = null;
            bool loadedResult = RunSilenced(() => _saveGameManager.TryLoad(out loaded, out loadFailure));
            if (!loadedResult || loaded == null)
            {
                return Fail($"load failed unexpectedly: {loadFailure}");
            }

            if (!CompareRoundTrip(original, loaded, out string comparisonFailure))
            {
                return Fail(comparisonFailure);
            }

            return Pass();
        }

        private (bool Success, string FailureReason) ValidateCorruptSaveSafety()
        {
            File.WriteAllText(_saveGameManager.SavePath, "{ this is not valid json");

            string failureReason = string.Empty;
            SaveGameData loadedData = null;
            bool loaded = RunSilenced(() => _saveGameManager.TryLoad(out loadedData, out failureReason));
            if (loaded)
            {
                return Fail("corrupt save unexpectedly loaded");
            }

            if (string.IsNullOrWhiteSpace(failureReason))
            {
                return Fail("corrupt save did not report a failure reason");
            }

            return Pass();
        }

        private (bool Success, string FailureReason) ValidateMissingContentSafety()
        {
            SaveGameData missingContent = CreateMissingContentSaveData();
            string saveFailure = string.Empty;
            bool saved = RunSilenced(() => _saveGameManager.TrySave(missingContent, out saveFailure));
            if (!saved)
            {
                return Fail($"save failed unexpectedly: {saveFailure}");
            }

            string loadFailure = string.Empty;
            SaveGameData loaded = null;
            bool loadedResult = RunSilenced(() => _saveGameManager.TryLoad(out loaded, out loadFailure));
            if (!loadedResult || loaded == null)
            {
                return Fail($"load failed unexpectedly: {loadFailure}");
            }

            List<string> warnings = new();
            ShipLoadout loadout = RunSilenced(() => _saveGameManager.BuildLoadout(loaded, out warnings));
            EquipmentDefinition mountedGun = loadout.GetPrimaryMountedGun();
            if (mountedGun == null)
            {
                return Fail("missing equipment did not fall back to starter gear");
            }

            if (!string.Equals(mountedGun.Id, "liberty_light_laser", StringComparison.OrdinalIgnoreCase))
            {
                return Fail($"unexpected fallback gun: {mountedGun.Id}");
            }

            CargoHold cargoHold = new CargoHold(50);
            RunSilenced(() => _saveGameManager.ApplyCargo(cargoHold, loaded, out warnings));
            if (cargoHold.GetCommodityQuantity("missing-commodity") != 0)
            {
                return Fail("missing commodity was not skipped");
            }

            Commodity validCommodity = CommodityCatalog.GetById("food-rations");
            if (validCommodity == null)
            {
                return Fail("valid commodity catalog entry was not found");
            }

            if (cargoHold.GetCommodityQuantity(validCommodity.Name) <= 0)
            {
                return Fail("valid commodity did not survive missing-content load");
            }

            var reputationManager = RunSilenced(() => new ReputationManager(new FactionManager()));
            RunSilenced(() => _saveGameManager.ApplyReputation(reputationManager, loaded));
            if (Math.Abs(reputationManager.GetStanding("mystery_faction") - 0.42f) > 0.01f)
            {
                return Fail("unknown faction standing did not round-trip safely");
            }

            var commodityDealer = RunSilenced(() => new CommodityDealer());
            var station = new Station(new StationConfig
            {
                Description = "Newark Station",
                FactionId = FactionManager.LibertyPolice
            }, null);
            RunSilenced(() => commodityDealer.SetDockedStation(station));
            RunSilenced(() => commodityDealer.RestoreMarketState(loaded.StationMarkets));

            var listings = RunSilenced(() => commodityDealer.CurrentMarketListings);
            if (listings == null || listings.Count == 0)
            {
                return Fail("restored market snapshot was empty");
            }

            if (listings.Any(listing => string.Equals(listing.Commodity?.Id, "missing-commodity", StringComparison.OrdinalIgnoreCase)))
            {
                return Fail("missing commodity unexpectedly survived market restore");
            }

            var validListing = listings.FirstOrDefault(listing => string.Equals(listing.Commodity?.Id, "food-rations", StringComparison.OrdinalIgnoreCase));
            if (validListing == null || validListing.Stock != 10)
            {
                return Fail("valid market listing did not round-trip");
            }

            return Pass();
        }

        private SaveGameData CreateRoundTripSaveData()
        {
            return new SaveGameData
            {
                SchemaVersion = SaveGameData.CurrentSchemaVersion,
                PlayerCredits = 54_321,
                CurrentSystemIndex = 2,
                CurrentShipName = "Scimitar",
                PlayerPosition = new SaveVector3Data(123.5f, -45.25f, 678.75f),
                PlayerVelocity = new SaveVector3Data(7.5f, 0.5f, -3.25f),
                PlayerForward = new SaveVector3Data(0f, 0f, -1f),
                OwnedEquipment = new List<SaveOwnedEquipmentData>
                {
                    new SaveOwnedEquipmentData { EquipmentId = "liberty_light_laser", EquipmentType = EquipmentType.Gun, Quantity = 2 },
                    new SaveOwnedEquipmentData { EquipmentId = "basic_missile_launcher", EquipmentType = EquipmentType.MissileLauncher, Quantity = 1 }
                },
                MountedEquipment = new List<SaveMountedEquipmentData>
                {
                    new SaveMountedEquipmentData { HardpointId = "PrimaryGunLeft", EquipmentId = "liberty_light_laser", EquipmentType = EquipmentType.Gun },
                    new SaveMountedEquipmentData { HardpointId = "PrimaryGunRight", EquipmentId = "rogue_blaster", EquipmentType = EquipmentType.Gun },
                    new SaveMountedEquipmentData { HardpointId = "MissileRack", EquipmentId = "basic_missile_launcher", EquipmentType = EquipmentType.MissileLauncher }
                },
                Cargo = new List<SaveCargoItemData>
                {
                    new SaveCargoItemData { CommodityId = "food-rations", Quantity = 8 },
                    new SaveCargoItemData { CommodityId = "h-fuel", Quantity = 4 }
                },
                FactionReputation = new List<SaveFactionReputationData>
                {
                    new SaveFactionReputationData { FactionId = FactionManager.LibertyPolice, Standing = 0.65f },
                    new SaveFactionReputationData { FactionId = FactionManager.LibertyRogues, Standing = -0.45f }
                },
                ActiveMissions = new List<SaveMissionData>
                {
                    new SaveMissionData
                    {
                        MissionId = 41,
                        Type = MissionType.Bounty,
                        Difficulty = MissionDifficulty.Hard,
                        Status = MissionStatus.Active,
                        Target = "Test Pirate",
                        Destination = "Last seen near Manhattan",
                        Reward = 12_500,
                        TimeLimit = 180f,
                        ElapsedTime = 44.5f,
                        Description = "Destroy Test Pirate",
                        OfferedBy = "Smoke Tester",
                        FactionId = FactionManager.BountyHunters,
                        ObjectiveComplete = false
                    }
                },
                CompletedMissions = new List<SaveMissionData>
                {
                    new SaveMissionData
                    {
                        MissionId = 42,
                        Type = MissionType.Delivery,
                        Difficulty = MissionDifficulty.Easy,
                        Status = MissionStatus.Completed,
                        Target = "Medical Supplies",
                        Destination = "Manhattan",
                        Reward = 2_500,
                        TimeLimit = 0f,
                        ElapsedTime = 0f,
                        Description = "Deliver Medical Supplies to Manhattan",
                        OfferedBy = "Smoke Tester",
                        FactionId = FactionManager.LibertyCorporations,
                        ObjectiveComplete = true
                    }
                },
                StationMarkets = new List<SaveMarketStateData>
                {
                    new SaveMarketStateData
                    {
                        StationKey = "newarkstation",
                        StationName = "Newark Station",
                        Listings = new List<SaveMarketListingData>
                        {
                            new SaveMarketListingData
                            {
                                CommodityId = "food-rations",
                                BuyPrice = 150,
                                SellPrice = 90,
                                Stock = 42,
                                DemandLevel = 3,
                                IsAvailable = true
                            }
                        }
                    }
                }
            };
        }

        private SaveGameData CreateMissingContentSaveData()
        {
            return new SaveGameData
            {
                SchemaVersion = SaveGameData.CurrentSchemaVersion,
                PlayerCredits = 9_999,
                CurrentSystemIndex = 1,
                CurrentShipName = "Scimitar",
                PlayerPosition = new SaveVector3Data(1f, 2f, 3f),
                PlayerVelocity = new SaveVector3Data(0f, 0f, 0f),
                PlayerForward = new SaveVector3Data(0f, 0f, -1f),
                OwnedEquipment = new List<SaveOwnedEquipmentData>
                {
                    new SaveOwnedEquipmentData { EquipmentId = "missing_owned_gun", EquipmentType = EquipmentType.Gun, Quantity = 1 }
                },
                MountedEquipment = new List<SaveMountedEquipmentData>
                {
                    new SaveMountedEquipmentData { HardpointId = "PrimaryGunLeft", EquipmentId = "missing_mounted_gun", EquipmentType = EquipmentType.Gun },
                    new SaveMountedEquipmentData { HardpointId = "MissileRack", EquipmentId = "missing_missile_launcher", EquipmentType = EquipmentType.MissileLauncher }
                },
                Cargo = new List<SaveCargoItemData>
                {
                    new SaveCargoItemData { CommodityId = "missing-commodity", Quantity = 4 },
                    new SaveCargoItemData { CommodityId = "food-rations", Quantity = 2 }
                },
                FactionReputation = new List<SaveFactionReputationData>
                {
                    new SaveFactionReputationData { FactionId = "mystery_faction", Standing = 0.42f },
                    new SaveFactionReputationData { FactionId = string.Empty, Standing = 0.99f }
                },
                StationMarkets = new List<SaveMarketStateData>
                {
                    new SaveMarketStateData
                    {
                        StationKey = "newarkstation",
                        StationName = "Newark Station",
                        Listings = new List<SaveMarketListingData>
                        {
                            new SaveMarketListingData { CommodityId = "missing-commodity", BuyPrice = 999, SellPrice = 900, Stock = 12, DemandLevel = 1, IsAvailable = true },
                            new SaveMarketListingData { CommodityId = "food-rations", BuyPrice = 155, SellPrice = 95, Stock = 10, DemandLevel = 2, IsAvailable = true }
                        }
                    }
                }
            };
        }

        private bool CompareRoundTrip(SaveGameData expected, SaveGameData actual, out string failureReason)
        {
            failureReason = string.Empty;

            if (actual == null)
            {
                failureReason = "loaded save data was null";
                return false;
            }

            if (expected.SchemaVersion != actual.SchemaVersion)
            {
                failureReason = $"schema version mismatch: {expected.SchemaVersion} != {actual.SchemaVersion}";
                return false;
            }

            if (expected.PlayerCredits != actual.PlayerCredits)
            {
                failureReason = $"credits mismatch: {expected.PlayerCredits} != {actual.PlayerCredits}";
                return false;
            }

            if (expected.CurrentSystemIndex != actual.CurrentSystemIndex)
            {
                failureReason = $"system index mismatch: {expected.CurrentSystemIndex} != {actual.CurrentSystemIndex}";
                return false;
            }

            if (!string.Equals(expected.CurrentShipName, actual.CurrentShipName, StringComparison.OrdinalIgnoreCase))
            {
                failureReason = $"ship name mismatch: {expected.CurrentShipName} != {actual.CurrentShipName}";
                return false;
            }

            if (!AreClose(expected.PlayerPosition, actual.PlayerPosition) ||
                !AreClose(expected.PlayerVelocity, actual.PlayerVelocity) ||
                !AreClose(expected.PlayerForward, actual.PlayerForward))
            {
                failureReason = "vector state did not round-trip";
                return false;
            }

            if (!CompareCargo(expected.Cargo, actual.Cargo, out failureReason))
            {
                return false;
            }

            if (!CompareReputation(expected.FactionReputation, actual.FactionReputation, out failureReason))
            {
                return false;
            }

            if (!CompareMissions(expected.ActiveMissions, actual.ActiveMissions, "active", out failureReason))
            {
                return false;
            }

            if (!CompareMissions(expected.CompletedMissions, actual.CompletedMissions, "completed", out failureReason))
            {
                return false;
            }

            if (!CompareMarkets(expected.StationMarkets, actual.StationMarkets, out failureReason))
            {
                return false;
            }

            return true;
        }

        private static bool CompareCargo(IReadOnlyList<SaveCargoItemData> expected, IReadOnlyList<SaveCargoItemData> actual, out string failureReason)
        {
            failureReason = string.Empty;
            if (expected.Count != actual.Count)
            {
                failureReason = $"cargo count mismatch: {expected.Count} != {actual.Count}";
                return false;
            }

            for (int i = 0; i < expected.Count; i++)
            {
                if (!string.Equals(expected[i].CommodityId, actual[i].CommodityId, StringComparison.OrdinalIgnoreCase) ||
                    expected[i].Quantity != actual[i].Quantity)
                {
                    failureReason = $"cargo entry {i} mismatch";
                    return false;
                }
            }

            return true;
        }

        private static bool CompareReputation(IReadOnlyList<SaveFactionReputationData> expected, IReadOnlyList<SaveFactionReputationData> actual, out string failureReason)
        {
            failureReason = string.Empty;
            if (expected.Count != actual.Count)
            {
                failureReason = $"reputation count mismatch: {expected.Count} != {actual.Count}";
                return false;
            }

            for (int i = 0; i < expected.Count; i++)
            {
                if (!string.Equals(expected[i].FactionId, actual[i].FactionId, StringComparison.OrdinalIgnoreCase) ||
                    Math.Abs(expected[i].Standing - actual[i].Standing) > 0.0001f)
                {
                    failureReason = $"reputation entry {i} mismatch";
                    return false;
                }
            }

            return true;
        }

        private static bool CompareMissions(IReadOnlyList<SaveMissionData> expected, IReadOnlyList<SaveMissionData> actual, string label, out string failureReason)
        {
            failureReason = string.Empty;
            if (expected.Count != actual.Count)
            {
                failureReason = $"{label} mission count mismatch: {expected.Count} != {actual.Count}";
                return false;
            }

            for (int i = 0; i < expected.Count; i++)
            {
                if (!CompareMission(expected[i], actual[i]))
                {
                    failureReason = $"{label} mission entry {i} mismatch";
                    return false;
                }
            }

            return true;
        }

        private static bool CompareMission(SaveMissionData expected, SaveMissionData actual)
        {
            return expected.MissionId == actual.MissionId &&
                   expected.Type == actual.Type &&
                   expected.Difficulty == actual.Difficulty &&
                   expected.Status == actual.Status &&
                   string.Equals(expected.Target, actual.Target, StringComparison.OrdinalIgnoreCase) &&
                   string.Equals(expected.Destination, actual.Destination, StringComparison.OrdinalIgnoreCase) &&
                   expected.Reward == actual.Reward &&
                   Math.Abs(expected.TimeLimit - actual.TimeLimit) <= 0.0001f &&
                   Math.Abs(expected.ElapsedTime - actual.ElapsedTime) <= 0.0001f &&
                   string.Equals(expected.Description, actual.Description, StringComparison.OrdinalIgnoreCase) &&
                   string.Equals(expected.OfferedBy, actual.OfferedBy, StringComparison.OrdinalIgnoreCase) &&
                   string.Equals(expected.FactionId, actual.FactionId, StringComparison.OrdinalIgnoreCase) &&
                   expected.ObjectiveComplete == actual.ObjectiveComplete;
        }

        private static bool CompareMarkets(IReadOnlyList<SaveMarketStateData> expected, IReadOnlyList<SaveMarketStateData> actual, out string failureReason)
        {
            failureReason = string.Empty;
            if (expected.Count != actual.Count)
            {
                failureReason = $"market snapshot count mismatch: {expected.Count} != {actual.Count}";
                return false;
            }

            for (int i = 0; i < expected.Count; i++)
            {
                if (!CompareMarketState(expected[i], actual[i]))
                {
                    failureReason = $"market snapshot {i} mismatch";
                    return false;
                }
            }

            return true;
        }

        private static bool CompareMarketState(SaveMarketStateData expected, SaveMarketStateData actual)
        {
            if (!string.Equals(expected.StationKey, actual.StationKey, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(expected.StationName, actual.StationName, StringComparison.OrdinalIgnoreCase) ||
                expected.Listings.Count != actual.Listings.Count)
            {
                return false;
            }

            for (int i = 0; i < expected.Listings.Count; i++)
            {
                var expectedListing = expected.Listings[i];
                var actualListing = actual.Listings[i];
                if (!string.Equals(expectedListing.CommodityId, actualListing.CommodityId, StringComparison.OrdinalIgnoreCase) ||
                    expectedListing.BuyPrice != actualListing.BuyPrice ||
                    expectedListing.SellPrice != actualListing.SellPrice ||
                    expectedListing.Stock != actualListing.Stock ||
                    expectedListing.DemandLevel != actualListing.DemandLevel ||
                    expectedListing.IsAvailable != actualListing.IsAvailable)
                {
                    return false;
                }
            }

            return true;
        }

        private static bool AreClose(SaveVector3Data expected, SaveVector3Data actual)
        {
            return Math.Abs(expected.X - actual.X) <= 0.0001f &&
                   Math.Abs(expected.Y - actual.Y) <= 0.0001f &&
                   Math.Abs(expected.Z - actual.Z) <= 0.0001f;
        }

        private static (bool Success, string FailureReason) Pass()
        {
            return (true, string.Empty);
        }

        private static (bool Success, string FailureReason) Fail(string reason)
        {
            return (false, reason);
        }

        private static void RunSilenced(Action action)
        {
            RunSilenced(() =>
            {
                action();
                return true;
            });
        }

        private static T RunSilenced<T>(Func<T> action)
        {
            TextWriter originalOut = Console.Out;
            try
            {
                Console.SetOut(TextWriter.Null);
                return action();
            }
            finally
            {
                Console.SetOut(originalOut);
            }
        }

        private void TryCleanupTempDirectory()
        {
            try
            {
                if (Directory.Exists(_tempDirectory))
                {
                    Directory.Delete(_tempDirectory, true);
                }
            }
            catch
            {
                // Best-effort cleanup only.
            }
        }
    }
}
