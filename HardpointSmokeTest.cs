using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Roguelancer
{
    /// <summary>
    /// Focused Phase 9 validation for ship-specific layouts, remapping,
    /// shared attachment state, fallback behavior, and persistence.
    /// </summary>
    internal sealed class HardpointSmokeTest
    {
        public (int Passed, int Failed) Run()
        {
            int passed = 0;
            int failed = 0;

            RunCase(ValidateScimitarLayout, "Scimitar explicit layout", ref passed, ref failed);
            RunCase(ValidateTransportLayoutAndCompatibility, "transport layout and compatibility", ref passed, ref failed);
            RunCase(ValidateShipChangeRemapPreservesOwnership, "ship change remap preserves ownership", ref passed, ref failed);
            RunCase(ValidateGenericFallback, "generic fallback", ref passed, ref failed);
            RunCase(ValidateSharedAttachmentState, "shared station/flight attachment state", ref passed, ref failed);
            RunCase(ValidateShipSpecificSaveRoundTrip, "ship-specific save round-trip", ref passed, ref failed);

            Console.WriteLine($"[HARDPOINT SMOKE] RESULT: {passed} passed, {failed} failed");
            return (passed, failed);
        }

        private static (bool Success, string FailureReason) ValidateScimitarLayout()
        {
            ShipDefinition scimitar = ShipDefinition.CreateScimitar();
            ShipLoadout loadout = ShipLoadout.CreateForShip(scimitar);
            string[] expectedIds = { "PrimaryGunLeft", "PrimaryGunRight", "MissileRack" };

            if (!scimitar.HasExplicitHardpointMetadata || loadout.Hardpoints.Count != 9)
            {
                return Fail($"expected explicit Scimitar metadata with nine hardpoints, found {loadout.Hardpoints.Count}");
            }

            if (!expectedIds.All(id => loadout.GetHardpointById(id)?.CanAccept(EquipmentCatalog.GetById(id == "MissileRack" ? "basic_missile_launcher" : "rogue_blaster")) == true))
            {
                return Fail("Scimitar gun or launcher hardpoint type was incorrect");
            }

            if (loadout.Hardpoints.Select(hardpoint => hardpoint.Id).Distinct(StringComparer.OrdinalIgnoreCase).Count() != loadout.Hardpoints.Count)
            {
                return Fail("Scimitar hardpoint IDs were not stable and unique");
            }

            return Pass();
        }

        private static (bool Success, string FailureReason) ValidateTransportLayoutAndCompatibility()
        {
            ShipDefinition scimitar = ShipDefinition.CreateScimitar();
            ShipDefinition transport = ShipDefinition.CreateTransport();
            ShipLoadout scimitarLoadout = ShipLoadout.CreateForShip(scimitar);
            ShipLoadout transportLoadout = ShipLoadout.CreateForShip(transport);
            EquipmentDefinition gun = EquipmentCatalog.GetById("rogue_blaster");
            EquipmentDefinition launcher = EquipmentCatalog.GetById("basic_missile_launcher");

            int scimitarGuns = scimitarLoadout.GetCompatibleHardpoints(gun).Count();
            int transportGuns = transportLoadout.GetCompatibleHardpoints(gun).Count();
            if (scimitarGuns != 2 || transportGuns != 1)
            {
                return Fail($"expected two Scimitar gun mounts and one transport gun mount, found {scimitarGuns}/{transportGuns}");
            }

            if (!transportLoadout.GetCompatibleHardpoints(launcher).Any() ||
                transportLoadout.GetHardpointById("PrimaryGunLeft") != null)
            {
                return Fail("transport launcher compatibility or distinct hardpoint identity was incorrect");
            }

            return Pass();
        }

        private static (bool Success, string FailureReason) ValidateShipChangeRemapPreservesOwnership()
        {
            ShipDealer dealer = new ShipDealer();
            Ship ship = new Ship(Vector3.Zero);
            ShipDefinition scimitar = dealer.GetShipByName("Scimitar");
            ShipDefinition transport = dealer.GetShipByName("Pirate Transport");
            EquipmentDefinition firstGun = EquipmentCatalog.GetById("liberty_pulse_cannon");
            EquipmentDefinition secondGun = EquipmentCatalog.GetById("rogue_blaster");
            EquipmentDefinition launcher = EquipmentCatalog.GetById("basic_missile_launcher");

            ship.SetLoadout(ShipLoadout.CreateForShip(scimitar));
            ship.Loadout.AddOwnedEquipment(firstGun);
            ship.Loadout.AddOwnedEquipment(secondGun);
            ship.Loadout.AddOwnedEquipment(launcher);
            if (!ship.Loadout.TryMountEquipment("PrimaryGunLeft", firstGun, out _) ||
                !ship.Loadout.TryMountEquipment("PrimaryGunRight", secondGun, out _) ||
                !ship.Loadout.TryMountEquipment("MissileRack", launcher, out _))
            {
                return Fail("could not stage the Scimitar mounted loadout");
            }

            PlayerCredits credits = new PlayerCredits(dealer.GetTotalCost(transport));
            if (!dealer.TryPurchaseShip(transport, credits, ship, out string message))
            {
                return Fail($"transport purchase failed: {message}");
            }

            if (ship.Loadout.GetOwnedCount(firstGun.Id) != 1 ||
                ship.Loadout.GetOwnedCount(secondGun.Id) != 1 ||
                ship.Loadout.GetOwnedCount(launcher.Id) != 1)
            {
                return Fail("ship replacement lost owned equipment");
            }

            if (ship.Loadout.GetMountedCount(firstGun.Id) + ship.Loadout.GetMountedCount(secondGun.Id) != 1 ||
                ship.Loadout.GetMountedCount(launcher.Id) != 1 ||
                ship.Loadout.GetHardpointById("TransportGun")?.IsEmpty == true ||
                ship.Loadout.GetHardpointById("TransportLauncher")?.IsEmpty == true)
            {
                return Fail("ship replacement did not remap the bounded loadout correctly");
            }

            if (!ship.LastHardpointReconfigurationWarnings.Any(warning => warning.Contains("unmounted", StringComparison.OrdinalIgnoreCase)))
            {
                return Fail("ship replacement did not diagnose the unplaceable second gun");
            }

            SaveGameManager saveManager = new SaveGameManager(Path.Combine(Path.GetTempPath(), "Roguelancer_HardpointSmoke_state.json"));
            SaveGameData changedShipState = new SaveGameData
            {
                CurrentShipName = transport.Name,
                OwnedEquipment = saveManager.CaptureOwnedEquipment(ship.Loadout),
                MountedEquipment = saveManager.CaptureMountedEquipment(ship.Loadout)
            };
            ShipLoadout restored = saveManager.BuildLoadout(changedShipState, transport, out List<string> restoreWarnings);
            if (restored.GetOwnedCount(secondGun.Id) != 1 ||
                restored.GetMountedCount(secondGun.Id) != 0 ||
                restored.GetMountedCount(launcher.Id) != 1 ||
                restoreWarnings.Count != 0)
            {
                return Fail($"transport save state was not valid after remap: {string.Join("; ", restoreWarnings)}");
            }

            return Pass();
        }

        private static (bool Success, string FailureReason) ValidateGenericFallback()
        {
            ShipDefinition futureShip = new ShipDefinition("Future Ship", "no metadata", "SHIPS/future", 1000);
            ShipLoadout loadout = ShipLoadout.CreateForShip(futureShip);
            EquipmentDefinition gun = EquipmentCatalog.GetById("rogue_blaster");
            loadout.AddOwnedEquipment(gun);

            if (!loadout.UsesGenericFallbackLayout || !loadout.TryMountEquipment(gun, out _))
            {
                return Fail("generic fallback layout did not preserve existing gun gameplay");
            }

            return Pass();
        }

        private static (bool Success, string FailureReason) ValidateSharedAttachmentState()
        {
            Ship ship = new Ship(Vector3.Zero);
            ship.SetLoadout(ShipLoadout.CreateForShip(ShipDefinition.CreateScimitar(), false));
            EquipmentDefinition gun = EquipmentCatalog.GetById("rogue_blaster");
            ship.Loadout.AddOwnedEquipment(gun);
            if (!ship.Loadout.TryMountEquipment("PrimaryGunLeft", gun, out _))
            {
                return Fail("could not stage a visual attachment");
            }

            IReadOnlyList<MountedEquipmentAttachment> station = MountedEquipmentRenderer.Build(ship, Vector3.Zero, Matrix.Identity);
            IReadOnlyList<MountedEquipmentAttachment> flight = MountedEquipmentRenderer.Build(ship, new Vector3(40f, 12f, -8f), Matrix.Identity);
            if (station.Count != ship.Loadout.GetMountedEquipment().Count() || flight.Count != station.Count)
            {
                return Fail("station and flight attachment lists diverged from the authoritative loadout");
            }

            if (!station.Select(attachment => attachment.HardpointId).SequenceEqual(flight.Select(attachment => attachment.HardpointId)) ||
                station.Any(attachment => attachment.World == Matrix.Identity))
            {
                return Fail("shared attachment transforms were not generated from hardpoint metadata");
            }

            return Pass();
        }

        private static (bool Success, string FailureReason) ValidateShipSpecificSaveRoundTrip()
        {
            string directory = Path.Combine(Path.GetTempPath(), "Roguelancer_HardpointSmoke_" + Guid.NewGuid().ToString("N"));
            string savePath = Path.Combine(directory, "hardpoints.json");
            try
            {
                SaveGameManager manager = new SaveGameManager(savePath);
                ShipDefinition scimitar = ShipDefinition.CreateScimitar();
                ShipLoadout source = ShipLoadout.CreateForShip(scimitar);
                EquipmentDefinition gun = EquipmentCatalog.GetById("rogue_blaster");
                source.AddOwnedEquipment(gun);
                if (!source.TryMountEquipment("PrimaryGunLeft", gun, out _))
                {
                    return Fail("could not stage the Scimitar save assignment");
                }

                SaveGameData data = new SaveGameData
                {
                    CurrentShipName = scimitar.Name,
                    OwnedEquipment = manager.CaptureOwnedEquipment(source),
                    MountedEquipment = manager.CaptureMountedEquipment(source)
                };
                if (!manager.TrySave(data, out string saveFailure))
                {
                    return Fail($"save failed: {saveFailure}");
                }

                if (!manager.TryLoad(out SaveGameData loaded, out string loadFailure))
                {
                    return Fail($"load failed: {loadFailure}");
                }

                ShipLoadout restored = manager.BuildLoadout(loaded, scimitar, out List<string> warnings);
                if (restored.GetHardpointById("PrimaryGunLeft")?.MountedEquipmentId != gun.Id || warnings.Count != 0)
                {
                    return Fail($"Scimitar stable hardpoint assignment did not round-trip: {string.Join("; ", warnings)}");
                }

                return Pass();
            }
            finally
            {
                try
                {
                    if (Directory.Exists(directory)) Directory.Delete(directory, true);
                }
                catch
                {
                    // Smoke cleanup must not hide the transaction result.
                }
            }
        }

        private static (bool Success, string FailureReason) Pass() => (true, string.Empty);
        private static (bool Success, string FailureReason) Fail(string reason) => (false, reason);

        private static void RunCase(Func<(bool Success, string FailureReason)> test, string label, ref int passed, ref int failed)
        {
            try
            {
                (bool success, string failureReason) = RunSilenced(test);
                if (success)
                {
                    passed++;
                    Console.WriteLine($"[HARDPOINT SMOKE] PASS {label}");
                }
                else
                {
                    failed++;
                    Console.WriteLine($"[HARDPOINT SMOKE] FAIL {label}: {failureReason}");
                }
            }
            catch (Exception ex)
            {
                failed++;
                Console.WriteLine($"[HARDPOINT SMOKE] FAIL {label}: {ex.Message}");
            }
        }

        private static T RunSilenced<T>(Func<T> action)
        {
            StringBuilder output = new StringBuilder();
            TextWriter original = Console.Out;
            try
            {
                Console.SetOut(new StringWriter(output));
                return action();
            }
            finally
            {
                Console.SetOut(original);
            }
        }
    }
}
