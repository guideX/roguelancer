using System;
using System.Collections.Generic;
using System.Linq;

namespace Roguelancer
{
    /// <summary>
    /// Authoritative station transaction boundary for ship equipment.
    /// Equipment ownership and mounting remain on Ship.Loadout; this class only
    /// validates dealer inventory and applies atomic economy/loadout changes.
    /// </summary>
    public class EquipmentDealer
    {
        /// <summary>
        /// Phase 8 prototype resale rule. Definitions do not currently carry a
        /// separate resale field, so every valid dealer item trades back at 50%.
        /// </summary>
        public const float ResaleRate = 0.50f;

        private Station _currentStation;

        public Station CurrentStation => _currentStation;

        public IReadOnlyList<EquipmentDefinition> AvailableEquipment => EquipmentCatalog.GetDealerInventory();

        public void SetDockedStation(Station station)
        {
            _currentStation = station;
            if (station != null)
            {
                Console.WriteLine($"[EQUIPMENT] Docked at {station.Name} using live flight-equipment catalog");
            }
        }

        public void ClearDockedStation()
        {
            if (_currentStation != null)
            {
                Console.WriteLine($"[EQUIPMENT] Undocked from {_currentStation.Name}");
            }

            _currentStation = null;
        }

        public EquipmentDefinition GetEquipmentByIndex(int index)
        {
            IReadOnlyList<EquipmentDefinition> inventory = AvailableEquipment;
            return index >= 0 && index < inventory.Count ? inventory[index] : null;
        }

        public int GetResaleValue(EquipmentDefinition equipment)
        {
            EquipmentDefinition canonical = ResolveSoldEquipment(equipment);
            if (canonical == null || canonical.Price <= 0) return 0;

            return Math.Max(1, (int)MathF.Floor(canonical.Price * ResaleRate));
        }

        public bool CanBuyEquipment(EquipmentDefinition equipment, PlayerCredits credits, ShipLoadout loadout, out string message)
        {
            message = string.Empty;
            EquipmentDefinition canonical = ResolveSoldEquipment(equipment);
            if (canonical == null)
            {
                message = "That equipment is not sold at this terminal.";
                return false;
            }

            if (canonical.Price <= 0)
            {
                message = $"{canonical.Name} has invalid pricing.";
                return false;
            }

            if (credits == null || loadout == null)
            {
                message = "Equipment dealer transaction is unavailable.";
                return false;
            }

            // Ownership is kept in ShipLoadout rather than cargo, so there is no
            // separate cargo-capacity gate. A type must still have at least one
            // compatible hardpoint on the current ship to be useful equipment.
            if (!loadout.GetCompatibleHardpoints(canonical).Any())
            {
                message = $"{canonical.Name} is incompatible with the current ship.";
                return false;
            }

            if (!credits.CanAfford(canonical.Price))
            {
                message = $"Not enough credits for {canonical.Name}.";
                return false;
            }

            return true;
        }

        public bool CanBuyEquipment(EquipmentDefinition equipment, PlayerCredits credits, Ship playerShip, out string message)
        {
            return CanBuyEquipment(equipment, credits, playerShip?.Loadout, out message);
        }

        public bool CanEquipEquipment(EquipmentDefinition equipment, ShipLoadout loadout, out string message)
        {
            message = string.Empty;
            EquipmentDefinition canonical = ResolveSoldEquipment(equipment);
            if (canonical == null)
            {
                message = "That equipment is not sold at this terminal.";
                return false;
            }

            if (loadout == null)
            {
                message = "Loadout unavailable.";
                return false;
            }

            if (loadout.GetAvailableToMountCount(canonical.Id) <= 0)
            {
                message = $"No spare {canonical.Name} is owned to equip.";
                return false;
            }

            if (!loadout.GetCompatibleHardpoints(canonical).Any())
            {
                message = $"{canonical.Name} is incompatible with the current ship.";
                return false;
            }

            if (loadout.FindFirstCompatibleEmptyHardpoint(canonical) == null)
            {
                message = $"No empty compatible hardpoint for {canonical.Name}.";
                return false;
            }

            return true;
        }

        public bool CanEquipEquipment(EquipmentDefinition equipment, Ship playerShip, out string message)
        {
            return CanEquipEquipment(equipment, playerShip?.Loadout, out message);
        }

        public bool TryBuyEquipment(EquipmentDefinition equipment, PlayerCredits credits, ShipLoadout loadout, out string message)
        {
            if (!CanBuyEquipment(equipment, credits, loadout, out message))
            {
                Console.WriteLine($"[EQUIPMENT][FAIL] {message}");
                return false;
            }

            EquipmentDefinition canonical = ResolveSoldEquipment(equipment);

            // Stage ownership first, then deduct credits. If the second step
            // fails, remove exactly the staged stack so the transaction is
            // atomic from the caller's perspective.
            if (!loadout.AddOwnedEquipment(canonical, 1))
            {
                message = $"Could not add {canonical.Name} to owned equipment.";
                Console.WriteLine($"[EQUIPMENT][FAIL] {message}");
                return false;
            }

            if (!credits.RemoveCredits(canonical.Price))
            {
                loadout.RemoveOwnedEquipment(canonical.Id, 1);
                message = "Credit transfer failed; purchase was cancelled.";
                Console.WriteLine($"[EQUIPMENT][FAIL] {message}");
                return false;
            }

            message = $"Purchased {canonical.Name} for {canonical.Price:N0} CR.";
            Console.WriteLine($"[EQUIPMENT] {message}");
            return true;
        }

        public bool TryBuyEquipment(EquipmentDefinition equipment, PlayerCredits credits, Ship playerShip, out string message)
        {
            return TryBuyEquipment(equipment, credits, playerShip?.Loadout, out message);
        }

        public bool TryMountEquipment(EquipmentDefinition equipment, ShipLoadout loadout, out string message)
        {
            if (!CanEquipEquipment(equipment, loadout, out message))
            {
                Console.WriteLine($"[EQUIPMENT][FAIL] {message}");
                return false;
            }

            EquipmentDefinition canonical = ResolveSoldEquipment(equipment);
            bool success = loadout.TryMountEquipment(canonical, out message);
            Console.WriteLine(success ? $"[EQUIPMENT] {message}" : $"[EQUIPMENT][FAIL] {message}");
            return success;
        }

        public bool TryMountEquipment(EquipmentDefinition equipment, Ship playerShip, out string message)
        {
            return TryMountEquipment(equipment, playerShip?.Loadout, out message);
        }

        public bool TryUnmountEquipment(EquipmentDefinition equipment, ShipLoadout loadout, out string message)
        {
            message = string.Empty;
            EquipmentDefinition canonical = ResolveSoldEquipment(equipment);
            if (canonical == null)
            {
                message = "That equipment is not sold at this terminal.";
                return false;
            }

            if (loadout == null)
            {
                message = "Loadout unavailable.";
                return false;
            }

            bool success = loadout.TryUnmountEquipment(canonical.Id, out message);
            Console.WriteLine(success ? $"[EQUIPMENT] {message}" : $"[EQUIPMENT][FAIL] {message}");
            return success;
        }

        public bool TryUnmountEquipment(EquipmentDefinition equipment, Ship playerShip, out string message)
        {
            return TryUnmountEquipment(equipment, playerShip?.Loadout, out message);
        }

        public bool TrySellUnequippedEquipment(EquipmentDefinition equipment, PlayerCredits credits, ShipLoadout loadout, out string message)
        {
            message = string.Empty;
            EquipmentDefinition canonical = ResolveSoldEquipment(equipment);
            if (canonical == null)
            {
                message = "That equipment is not bought back at this terminal.";
                return false;
            }

            if (credits == null || loadout == null)
            {
                message = "Equipment dealer transaction is unavailable.";
                return false;
            }

            int saleValue = GetResaleValue(canonical);
            if (saleValue <= 0)
            {
                message = $"{canonical.Name} has no valid resale value.";
                return false;
            }

            if (loadout.GetAvailableToSellCount(canonical.Id) <= 0)
            {
                message = $"{canonical.Name} is mounted or unavailable to sell. Unequip it first.";
                Console.WriteLine($"[EQUIPMENT][FAIL] {message}");
                return false;
            }

            // The mounted-count guard above ensures a sale can never delete an
            // equipment id still referenced by a hardpoint.
            if (!loadout.RemoveOwnedEquipment(canonical.Id, 1))
            {
                message = $"Could not sell {canonical.Name}.";
                Console.WriteLine($"[EQUIPMENT][FAIL] {message}");
                return false;
            }

            credits.AddCredits(saleValue);
            message = $"Sold {canonical.Name} for {saleValue:N0} CR.";
            Console.WriteLine($"[EQUIPMENT] {message}");
            return true;
        }

        public bool TrySellUnequippedEquipment(EquipmentDefinition equipment, PlayerCredits credits, Ship playerShip, out string message)
        {
            return TrySellUnequippedEquipment(equipment, credits, playerShip?.Loadout, out message);
        }

        private EquipmentDefinition ResolveSoldEquipment(EquipmentDefinition equipment)
        {
            if (equipment == null || string.IsNullOrWhiteSpace(equipment.Id)) return null;

            return AvailableEquipment.FirstOrDefault(candidate =>
                string.Equals(candidate.Id, equipment.Id, StringComparison.OrdinalIgnoreCase));
        }
    }
}
