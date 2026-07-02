using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Roguelancer
{
    /// <summary>
    /// Developer-only validation harness for ship dealer pricing and purchase safety.
    /// </summary>
    internal sealed class ShipDealerSmokeTest
    {
        private readonly ShipDealer _shipDealer;

        public ShipDealerSmokeTest()
        {
            _shipDealer = RunSilenced(() => new ShipDealer());
        }

        public (int Passed, int Failed) Run()
        {
            int passed = 0;
            int failed = 0;

            RunCase(ValidateBalanceAnchors, "balance anchors", ref passed, ref failed);
            RunCase(ValidateUnaffordablePurchaseFailsSafely, "unaffordable purchase", ref passed, ref failed);
            RunCase(ValidateCargoGateFailsSafely, "cargo gate", ref passed, ref failed);
            RunCase(ValidateAffordablePurchaseSucceeds, "affordable purchase", ref passed, ref failed);

            Console.WriteLine($"[SHIP SMOKE] RESULT: {passed} passed, {failed} failed");
            return (passed, failed);
        }

        private void RunCase(Func<(bool Success, string FailureReason)> test, string label, ref int passed, ref int failed)
        {
            try
            {
                var result = RunSilenced(test);
                if (result.Success)
                {
                    passed++;
                    Console.WriteLine($"[SHIP SMOKE] PASS {label}");
                    return;
                }

                failed++;
                Console.WriteLine($"[SHIP SMOKE] FAIL {label}: {result.FailureReason}");
            }
            catch (Exception ex)
            {
                failed++;
                Console.WriteLine($"[SHIP SMOKE] FAIL {label}: {ex.Message}");
            }
        }

        private (bool Success, string FailureReason) ValidateBalanceAnchors()
        {
            ShipDefinition starter = _shipDealer.GetShipByName("Scimitar");
            ShipDefinition upgrade = _shipDealer.GetShipByName("Pirate Transport");
            int startingCredits = RoguelancerGame.StartingPlayerCredits;

            if (starter == null || upgrade == null)
            {
                return Fail("expected Scimitar and Pirate Transport to be available");
            }

            if (startingCredits <= 0)
            {
                return Fail("starting credits were not positive");
            }

            if (starter.Price <= 0 || upgrade.Price <= 0)
            {
                return Fail("ship prices must stay positive");
            }

            if (starter.TradeInValue <= 0 || upgrade.TradeInValue <= 0)
            {
                return Fail("ship trade-in values must stay positive");
            }

            if (starter.TradeInValue >= starter.Price || upgrade.TradeInValue >= upgrade.Price)
            {
                return Fail("trade-in value must stay below purchase price");
            }

            if (upgrade.Price <= starter.Price)
            {
                return Fail("expected a higher-tier ship above the starter tier");
            }

            int upgradeCost = _shipDealer.GetTotalCost(upgrade);
            if (startingCredits >= upgradeCost)
            {
                return Fail($"starting credits ({startingCredits:N0}) should stay below the first ship upgrade cost ({upgradeCost:N0})");
            }

            if (startingCredits < 2000 || startingCredits > 5000)
            {
                return Fail($"starting credits ({startingCredits:N0}) are outside the intended early-game range");
            }

            return Pass();
        }

        private (bool Success, string FailureReason) ValidateUnaffordablePurchaseFailsSafely()
        {
            ShipDefinition upgrade = _shipDealer.GetShipByName("Pirate Transport");
            if (upgrade == null)
            {
                return Fail("Pirate Transport was not available");
            }

            Ship playerShip = new Ship(Vector3.Zero);
            PlayerCredits credits = new PlayerCredits(_shipDealer.GetTotalCost(upgrade) - 1);
            string loadoutBefore = playerShip.Loadout.GetMountedSummary();
            int creditsBefore = credits.Credits;
            int cargoBefore = playerShip.CargoHold.MaxCapacity;

            bool success = _shipDealer.PurchaseShip(upgrade, credits, playerShip, null);
            if (success)
            {
                return Fail("purchase succeeded even though credits were insufficient");
            }

            if (!string.Equals(_shipDealer.CurrentPlayerShip.Name, "Scimitar", StringComparison.OrdinalIgnoreCase))
            {
                return Fail("failed purchase should not change the current ship");
            }

            if (credits.Credits != creditsBefore)
            {
                return Fail("failed purchase should not change credits");
            }

            if (!string.Equals(playerShip.Loadout.GetMountedSummary(), loadoutBefore, StringComparison.Ordinal))
            {
                return Fail("failed purchase should not change loadout state");
            }

            if (playerShip.CargoHold.MaxCapacity != cargoBefore)
            {
                return Fail("failed purchase should not change cargo capacity");
            }

            return Pass();
        }

        private (bool Success, string FailureReason) ValidateCargoGateFailsSafely()
        {
            ShipDefinition cargoShip = new ShipDefinition(
                "Tiny Hauler",
                "Test ship with a smaller hold than the current cargo load",
                "SHIPS/scimitar/Scimitar2",
                12000)
            {
                CargoCapacity = 10,
                MaxSpeed = 140f,
                MaxReverseSpeed = 90f,
                CruiseSpeed = 300f,
                AfterburnerSpeed = 220f,
                Acceleration = 90f,
                TurnSpeed = 1.0f,
                MaxHull = 80f,
                MaxEnergy = 120f,
                MaxShields = 20f
            };

            Ship playerShip = new Ship(Vector3.Zero);
            Commodity food = CommodityCatalog.GetById("food-rations");
            if (food == null)
            {
                return Fail("food rations were not available for cargo setup");
            }

            if (!playerShip.CargoHold.AddCommodity(food, 20))
            {
                return Fail("could not stage cargo for the cargo gate test");
            }

            PlayerCredits credits = new PlayerCredits(_shipDealer.GetTotalCost(cargoShip));
            int creditsBefore = credits.Credits;
            int cargoBefore = playerShip.CargoHold.UsedCapacity;

            bool success = _shipDealer.PurchaseShip(cargoShip, credits, playerShip, null);
            if (success)
            {
                return Fail("purchase succeeded even though the cargo did not fit");
            }

            if (credits.Credits != creditsBefore)
            {
                return Fail("cargo-gated failure should not change credits");
            }

            if (playerShip.CargoHold.UsedCapacity != cargoBefore)
            {
                return Fail("cargo-gated failure should not move cargo");
            }

            if (!string.Equals(_shipDealer.CurrentPlayerShip.Name, "Scimitar", StringComparison.OrdinalIgnoreCase))
            {
                return Fail("cargo-gated failure should not change the current ship");
            }

            return Pass();
        }

        private (bool Success, string FailureReason) ValidateAffordablePurchaseSucceeds()
        {
            ShipDefinition upgrade = _shipDealer.GetShipByName("Pirate Transport");
            if (upgrade == null)
            {
                return Fail("Pirate Transport was not available");
            }

            Ship playerShip = new Ship(Vector3.Zero);
            Commodity water = CommodityCatalog.GetById("water");
            if (water == null)
            {
                return Fail("water was not available for cargo staging");
            }

            if (!playerShip.CargoHold.AddCommodity(water, 5))
            {
                return Fail("could not stage cargo for the successful purchase test");
            }

            EquipmentDefinition spareGun = EquipmentCatalog.GetById("liberty_pulse_cannon");
            if (spareGun == null)
            {
                return Fail("expected the pulse cannon to exist for spare equipment staging");
            }

            if (!playerShip.Loadout.AddOwnedEquipment(spareGun, 1))
            {
                return Fail("could not stage spare equipment for the successful purchase test");
            }

            string loadoutBefore = playerShip.Loadout.GetMountedSummary();
            int spareBefore = playerShip.Loadout.GetOwnedCount(spareGun.Id);
            int creditsBefore = _shipDealer.GetTotalCost(upgrade);
            PlayerCredits credits = new PlayerCredits(creditsBefore);

            bool success = _shipDealer.PurchaseShip(upgrade, credits, playerShip, null);
            if (!success)
            {
                return Fail("affordable purchase failed unexpectedly");
            }

            if (!string.Equals(_shipDealer.CurrentPlayerShip.Name, upgrade.Name, StringComparison.OrdinalIgnoreCase))
            {
                return Fail("successful purchase did not update the dealer's current ship");
            }

            if (credits.Credits != 0)
            {
                return Fail("successful purchase did not deduct the expected credits");
            }

            if (playerShip.CargoHold.MaxCapacity != upgrade.CargoCapacity)
            {
                return Fail("successful purchase did not apply the new cargo capacity");
            }

            if (playerShip.CargoHold.UsedCapacity != 5)
            {
                return Fail("successful purchase changed the carried cargo");
            }

            if (!string.Equals(playerShip.Loadout.GetMountedSummary(), loadoutBefore, StringComparison.Ordinal))
            {
                return Fail("successful purchase changed the mounted loadout");
            }

            if (playerShip.Loadout.GetOwnedCount(spareGun.Id) != spareBefore)
            {
                return Fail("successful purchase changed spare equipment");
            }

            if (playerShip.MaxSpeed != upgrade.MaxSpeed ||
                playerShip.Hull.MaxHull != upgrade.MaxHull ||
                playerShip.Shields.MaxShields != upgrade.MaxShields ||
                playerShip.Energy.MaxEnergy != upgrade.MaxEnergy)
            {
                return Fail("successful purchase did not apply the new ship stats");
            }

            return Pass();
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
    }
}
