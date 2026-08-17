using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Roguelancer;

/// <summary>
/// Developer-only validation harness for the station equipment terminal.
/// </summary>
internal sealed class EquipmentDealerSmokeTest
{
    private readonly EquipmentDealer _equipmentDealer;

    public EquipmentDealerSmokeTest()
    {
        _equipmentDealer = RunSilenced(() => new EquipmentDealer());
    }

    public (int Passed, int Failed) Run()
    {
        int passed = 0;
        int failed = 0;

        RunCase(ValidateDealerInventory, "valid bounded inventory", ref passed, ref failed);
        RunCase(ValidateAffordablePurchase, "affordable purchase", ref passed, ref failed);
        RunCase(ValidateInsufficientFunds, "insufficient funds", ref passed, ref failed);
        RunCase(ValidateIncompatibleEquip, "incompatible equip", ref passed, ref failed);
        RunCase(ValidateValidEquip, "valid equip", ref passed, ref failed);
        RunCase(ValidateValidUnequip, "valid unequip", ref passed, ref failed);
        RunCase(ValidateSafeSell, "safe sell", ref passed, ref failed);
        RunCase(ValidateSellNonexistent, "sell nonexistent", ref passed, ref failed);
        RunCase(ValidateInvalidItemRejected, "invalid item rejected", ref passed, ref failed);
        RunCase(ValidateCurrentShipLoadoutIsUsed, "current ship compatibility", ref passed, ref failed);
        RunCase(ValidateFullHardpointIsSafe, "full hardpoint safety", ref passed, ref failed);
        RunCase(ValidateMissionCargoUnaffected, "mission cargo unaffected", ref passed, ref failed);
        RunCase(ValidateSaveLoadRoundTrip, "equipment save/load", ref passed, ref failed);

        Console.WriteLine($"[EQUIPMENT SMOKE] RESULT: {passed} passed, {failed} failed");
        return (passed, failed);
    }

    private void RunCase(Func<(bool Success, string FailureReason)> test, string label, ref int passed, ref int failed)
    {
        try
        {
            (bool success, string failureReason) = RunSilenced(test);
            if (success)
            {
                passed++;
                Console.WriteLine($"[EQUIPMENT SMOKE] PASS {label}");
            }
            else
            {
                failed++;
                Console.WriteLine($"[EQUIPMENT SMOKE] FAIL {label}: {failureReason}");
            }
        }
        catch (Exception ex)
        {
            failed++;
            Console.WriteLine($"[EQUIPMENT SMOKE] FAIL {label}: {ex.Message}");
        }
    }

    private (bool Success, string FailureReason) ValidateDealerInventory()
    {
        IReadOnlyList<EquipmentDefinition> inventory = _equipmentDealer.AvailableEquipment;
        if (inventory.Count != 5)
        {
            return Fail($"expected five bounded dealer items, found {inventory.Count}");
        }

        if (inventory.Any(equipment => equipment == null || string.IsNullOrWhiteSpace(equipment.Id) || equipment.Price <= 0))
        {
            return Fail("dealer inventory contained an invalid definition or price");
        }

        string[] expectedIds =
        {
            "liberty_pulse_cannon",
            "rogue_blaster",
            "basic_missile_launcher",
            "basic_mine_dropper",
            "basic_countermeasure_dropper"
        };
        if (!expectedIds.All(expected => inventory.Any(equipment => string.Equals(equipment.Id, expected, StringComparison.OrdinalIgnoreCase))))
        {
            return Fail("dealer inventory did not enumerate the expected live flight definitions");
        }

        return Pass();
    }

    private (bool Success, string FailureReason) ValidateAffordablePurchase()
    {
        EquipmentDefinition equipment = EquipmentCatalog.GetById("liberty_pulse_cannon");
        Ship ship = new Ship(Vector3.Zero);
        PlayerCredits credits = new PlayerCredits(equipment.Price + 100);
        int before = credits.Credits;

        if (!_equipmentDealer.TryBuyEquipment(equipment, credits, ship, out string message))
        {
            return Fail($"purchase failed: {message}");
        }

        if (credits.Credits != before - equipment.Price || ship.Loadout.GetOwnedCount(equipment.Id) != 1)
        {
            return Fail("purchase did not deduct once and add exactly one owned item");
        }

        return Pass();
    }

    private (bool Success, string FailureReason) ValidateInsufficientFunds()
    {
        EquipmentDefinition equipment = EquipmentCatalog.GetById("liberty_pulse_cannon");
        Ship ship = new Ship(Vector3.Zero);
        PlayerCredits credits = new PlayerCredits(equipment.Price - 1);
        int before = credits.Credits;

        if (_equipmentDealer.TryBuyEquipment(equipment, credits, ship, out _))
        {
            return Fail("unaffordable equipment unexpectedly purchased");
        }

        if (credits.Credits != before || ship.Loadout.GetOwnedCount(equipment.Id) != 0)
        {
            return Fail("unaffordable purchase changed credits or ownership");
        }

        return Pass();
    }

    private (bool Success, string FailureReason) ValidateIncompatibleEquip()
    {
        EquipmentDefinition gun = EquipmentCatalog.GetById("liberty_pulse_cannon");
        ShipLoadout loadout = new ShipLoadout(new[]
        {
            new ShipHardpoint { Id = "MissileOnly", AllowedEquipmentTypes = new List<EquipmentType> { EquipmentType.MissileLauncher } }
        });
        loadout.AddOwnedEquipment(gun, 1);

        if (_equipmentDealer.TryMountEquipment(gun, loadout, out string message))
        {
            return Fail("gun mounted on an incompatible hardpoint");
        }

        if (loadout.GetMountedCount(gun.Id) != 0)
        {
            return Fail($"incompatible equip changed mount state: {message}");
        }

        return Pass();
    }

    private (bool Success, string FailureReason) ValidateValidEquip()
    {
        EquipmentDefinition gun = EquipmentCatalog.GetById("liberty_pulse_cannon");
        ShipLoadout loadout = ShipLoadout.CreateStarterLoadout(false);
        loadout.AddOwnedEquipment(gun, 1);

        if (!_equipmentDealer.TryMountEquipment(gun, loadout, out string message))
        {
            return Fail($"valid gun equip failed: {message}");
        }

        if (loadout.GetMountedCount(gun.Id) != 1 || loadout.Hardpoints.All(hardpoint => !string.Equals(hardpoint.MountedEquipmentId, gun.Id, StringComparison.OrdinalIgnoreCase)))
        {
            return Fail("valid equip did not update the authoritative hardpoint");
        }

        return Pass();
    }

    private (bool Success, string FailureReason) ValidateValidUnequip()
    {
        EquipmentDefinition gun = EquipmentCatalog.GetById("liberty_pulse_cannon");
        ShipLoadout loadout = ShipLoadout.CreateStarterLoadout(false);
        loadout.AddOwnedEquipment(gun, 1);
        if (!_equipmentDealer.TryMountEquipment(gun, loadout, out _))
        {
            return Fail("could not stage gun for unequip");
        }

        if (!_equipmentDealer.TryUnmountEquipment(gun, loadout, out string message))
        {
            return Fail($"valid unequip failed: {message}");
        }

        if (loadout.GetMountedCount(gun.Id) != 0 || loadout.GetOwnedCount(gun.Id) != 1)
        {
            return Fail("unequip destroyed ownership or left the hardpoint mounted");
        }

        return Pass();
    }

    private (bool Success, string FailureReason) ValidateSafeSell()
    {
        EquipmentDefinition gun = EquipmentCatalog.GetById("liberty_pulse_cannon");
        ShipLoadout loadout = ShipLoadout.CreateStarterLoadout(false);
        PlayerCredits credits = new PlayerCredits(0);
        loadout.AddOwnedEquipment(gun, 1);
        int resale = _equipmentDealer.GetResaleValue(gun);

        if (!_equipmentDealer.TrySellUnequippedEquipment(gun, credits, loadout, out string message))
        {
            return Fail($"unequipped sale failed: {message}");
        }

        if (loadout.GetOwnedCount(gun.Id) != 0 || credits.Credits != resale)
        {
            return Fail("sale did not remove one owned item and add one resale payment");
        }

        loadout.AddOwnedEquipment(gun, 1);
        if (!_equipmentDealer.TryMountEquipment(gun, loadout, out _))
        {
            return Fail("could not stage equipped sale guard");
        }

        int creditsBeforeRejectedSale = credits.Credits;
        if (_equipmentDealer.TrySellUnequippedEquipment(gun, credits, loadout, out _))
        {
            return Fail("equipped equipment was sold without unequipping");
        }

        if (credits.Credits != creditsBeforeRejectedSale || loadout.GetMountedCount(gun.Id) != 1)
        {
            return Fail("rejected equipped sale changed credits or hardpoint state");
        }

        return Pass();
    }

    private (bool Success, string FailureReason) ValidateInvalidItemRejected()
    {
        Ship ship = new Ship(Vector3.Zero);
        PlayerCredits credits = new PlayerCredits(10_000);
        EquipmentDefinition fake = new EquipmentDefinition { Id = "fake-equipment", Name = "Fake", Price = 1, EquipmentType = EquipmentType.Gun };
        int creditsBefore = credits.Credits;

        if (_equipmentDealer.TryBuyEquipment(fake, credits, ship, out _))
        {
            return Fail("definition outside the dealer inventory was purchased");
        }

        if (credits.Credits != creditsBefore || ship.Loadout.GetOwnedCount(fake.Id) != 0)
        {
            return Fail("invalid item rejection changed state");
        }

        return Pass();
    }

    private (bool Success, string FailureReason) ValidateSellNonexistent()
    {
        EquipmentDefinition gun = EquipmentCatalog.GetById("liberty_pulse_cannon");
        ShipLoadout loadout = ShipLoadout.CreateStarterLoadout(false);
        PlayerCredits credits = new PlayerCredits(123);

        if (_equipmentDealer.TrySellUnequippedEquipment(gun, credits, loadout, out _))
        {
            return Fail("nonexistent owned equipment unexpectedly sold");
        }

        if (credits.Credits != 123 || loadout.GetOwnedCount(gun.Id) != 0)
        {
            return Fail("rejected nonexistent sale changed state");
        }

        return Pass();
    }

    private (bool Success, string FailureReason) ValidateCurrentShipLoadoutIsUsed()
    {
        ShipDealer shipDealer = new ShipDealer();
        Ship playerShip = new Ship(Vector3.Zero);
        EquipmentDefinition gun = EquipmentCatalog.GetById("liberty_pulse_cannon");
        EquipmentDefinition launcher = EquipmentCatalog.GetById("basic_missile_launcher");
        ShipLoadout currentShipLoadout = new ShipLoadout(new[]
        {
            new ShipHardpoint { Id = "CurrentShipMissile", AllowedEquipmentTypes = new List<EquipmentType> { EquipmentType.MissileLauncher } }
        });
        currentShipLoadout.AddOwnedEquipment(launcher, 1);
        playerShip.SetLoadout(currentShipLoadout);

        ShipDefinition transport = shipDealer.GetShipByName("Pirate Transport");
        PlayerCredits credits = new PlayerCredits(shipDealer.GetTotalCost(transport));
        if (!shipDealer.TryPurchaseShip(transport, credits, playerShip, out string purchaseMessage))
        {
            return Fail($"ship purchase failed before compatibility check: {purchaseMessage}");
        }

        if (_equipmentDealer.CanEquipEquipment(gun, playerShip, out _))
        {
            return Fail("compatibility incorrectly used a cached/default gun slot after ship purchase");
        }

        if (!_equipmentDealer.CanEquipEquipment(launcher, playerShip, out string message))
        {
            return Fail($"current ship loadout was not used after ship purchase: {message}");
        }

        return Pass();
    }

    private (bool Success, string FailureReason) ValidateFullHardpointIsSafe()
    {
        EquipmentDefinition gun = EquipmentCatalog.GetById("liberty_pulse_cannon");
        ShipLoadout loadout = new ShipLoadout(new[]
        {
            new ShipHardpoint
            {
                Id = "OnlyGun",
                AllowedEquipmentTypes = new List<EquipmentType> { EquipmentType.Gun },
                MountedEquipmentId = gun.Id
            }
        });
        loadout.AddOwnedEquipment(gun, 1);
        PlayerCredits credits = new PlayerCredits(0);

        if (_equipmentDealer.TryMountEquipment(gun, loadout, out _))
        {
            return Fail("equipment mounted twice on a full hardpoint");
        }

        if (credits.Credits != 0 || loadout.GetMountedCount(gun.Id) != 1 || loadout.GetOwnedCount(gun.Id) != 1)
        {
            return Fail("full hardpoint rejection changed authoritative state");
        }

        return Pass();
    }

    private (bool Success, string FailureReason) ValidateMissionCargoUnaffected()
    {
        Ship ship = new Ship(Vector3.Zero);
        ship.SetLoadout(ShipLoadout.CreateStarterLoadout(false));
        Commodity package = CommodityCatalog.GetById("sealed-data-package");
        EquipmentDefinition gun = EquipmentCatalog.GetById("liberty_pulse_cannon");
        if (package == null || gun == null || !ship.CargoHold.AddMissionCargo(7101, package, 1))
        {
            return Fail("could not stage equipment/courier regression state");
        }

        int usedBefore = ship.CargoHold.UsedCapacity;
        if (!_equipmentDealer.TryBuyEquipment(gun, new PlayerCredits(gun.Price + 100), ship, out string purchaseMessage))
        {
            return Fail($"equipment purchase failed beside courier cargo: {purchaseMessage}");
        }
        if (!_equipmentDealer.TryMountEquipment(gun, ship, out string equipMessage))
        {
            return Fail($"equipment equip failed beside courier cargo: {equipMessage}");
        }

        if (ship.CargoHold.UsedCapacity != usedBefore ||
            !ship.CargoHold.HasMissionCargo(7101, package.Id, 1) ||
            ship.CargoHold.GetMissionCargoReservations().Count != 1)
        {
            return Fail("equipment transaction changed mission cargo");
        }

        return Pass();
    }

    private (bool Success, string FailureReason) ValidateSaveLoadRoundTrip()
    {
        string directory = Path.Combine(Path.GetTempPath(), "Roguelancer_EquipmentSmoke_" + Guid.NewGuid().ToString("N"));
        string savePath = Path.Combine(directory, "equipment.json");
        try
        {
            EquipmentDefinition gun = EquipmentCatalog.GetById("liberty_pulse_cannon");
            Ship ship = new Ship(Vector3.Zero);
            ship.SetLoadout(ShipLoadout.CreateStarterLoadout(false));
            PlayerCredits credits = new PlayerCredits(gun.Price + 2_000);
            if (!_equipmentDealer.TryBuyEquipment(gun, credits, ship, out string purchaseMessage))
            {
                return Fail($"purchase failed before persistence: {purchaseMessage}");
            }

            if (!_equipmentDealer.TryMountEquipment(gun, ship, out string equipMessage))
            {
                return Fail($"equip failed before persistence: {equipMessage}");
            }

            SaveGameManager saveManager = new SaveGameManager(savePath);
            SaveGameData data = new SaveGameData
            {
                PlayerCredits = credits.Credits,
                CurrentShipName = "Pirate Transport",
                OwnedEquipment = saveManager.CaptureOwnedEquipment(ship.Loadout),
                MountedEquipment = saveManager.CaptureMountedEquipment(ship.Loadout)
            };

            if (!saveManager.TrySave(data, out string saveFailure))
            {
                return Fail($"save failed: {saveFailure}");
            }

            if (!saveManager.TryLoad(out SaveGameData loaded, out string loadFailure))
            {
                return Fail($"load failed: {loadFailure}");
            }

            ShipLoadout restored = saveManager.BuildLoadout(loaded, out List<string> warnings);
            if (loaded.PlayerCredits != credits.Credits || !string.Equals(loaded.CurrentShipName, "Pirate Transport", StringComparison.OrdinalIgnoreCase))
            {
                return Fail("credits or current ship did not persist");
            }

            if (restored.GetOwnedCount(gun.Id) != 1 || restored.GetMountedCount(gun.Id) != 1)
            {
                return Fail($"equipment ownership/loadout did not persist: {string.Join("; ", warnings)}");
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
        TextWriter previous = Console.Out;
        try
        {
            Console.SetOut(TextWriter.Null);
            return action();
        }
        finally
        {
            Console.SetOut(previous);
        }
    }
}
