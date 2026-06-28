using System;
using System.Collections.Generic;

namespace Roguelancer
{
    /// <summary>
    /// Dealer that sells and manages ship equipment.
    /// </summary>
    public class EquipmentDealer
    {
        private Station _currentStation;

        public Station CurrentStation => _currentStation;

        public IReadOnlyList<EquipmentDefinition> AvailableEquipment => EquipmentCatalog.GetAll();

        public void SetDockedStation(Station station)
        {
            _currentStation = station;
            if (station != null)
            {
                Console.WriteLine($"[EQUIPMENT] Docked at {station.Name} using starter catalog");
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
            return EquipmentCatalog.GetByIndex(index);
        }

        public bool TryBuyEquipment(EquipmentDefinition equipment, PlayerCredits credits, ShipLoadout loadout, out string message)
        {
            message = string.Empty;

            if (equipment == null)
            {
                message = "No equipment selected.";
                return false;
            }

            if (credits == null || loadout == null)
            {
                message = "Equipment dealer is unavailable.";
                return false;
            }

            if (!credits.CanAfford(equipment.Price))
            {
                message = $"Not enough credits for {equipment.Name}.";
                Console.WriteLine($"[EQUIPMENT][FAIL] {message}");
                return false;
            }

            if (!credits.RemoveCredits(equipment.Price))
            {
                message = "Credit transfer failed.";
                Console.WriteLine($"[EQUIPMENT][FAIL] {message}");
                return false;
            }

            if (!loadout.AddOwnedEquipment(equipment, 1))
            {
                credits.AddCredits(equipment.Price);
                message = $"Could not add {equipment.Name} to inventory.";
                Console.WriteLine($"[EQUIPMENT][FAIL] {message}");
                return false;
            }

            message = $"Purchased {equipment.Name} for {equipment.Price:N0} CR.";
            Console.WriteLine($"[EQUIPMENT] Purchased {equipment.Name} for {equipment.Price:N0} CR");
            return true;
        }

        public bool TryMountEquipment(EquipmentDefinition equipment, ShipLoadout loadout, out string message)
        {
            message = string.Empty;

            if (equipment == null)
            {
                message = "No equipment selected.";
                return false;
            }

            if (loadout == null)
            {
                message = "Loadout unavailable.";
                return false;
            }

            bool success = loadout.TryMountEquipment(equipment, out message);
            if (success)
            {
                Console.WriteLine($"[EQUIPMENT] {message}");
            }
            else
            {
                Console.WriteLine($"[EQUIPMENT][FAIL] {message}");
            }

            return success;
        }

        public bool TryUnmountEquipment(EquipmentDefinition equipment, ShipLoadout loadout, out string message)
        {
            message = string.Empty;

            if (equipment == null)
            {
                message = "No equipment selected.";
                return false;
            }

            if (loadout == null)
            {
                message = "Loadout unavailable.";
                return false;
            }

            bool success = loadout.TryUnmountEquipment(equipment.Id, out message);
            if (success)
            {
                Console.WriteLine($"[EQUIPMENT] {message}");
            }
            else
            {
                Console.WriteLine($"[EQUIPMENT][FAIL] {message}");
            }

            return success;
        }

        public bool TrySellUnequippedEquipment(EquipmentDefinition equipment, PlayerCredits credits, ShipLoadout loadout, out string message)
        {
            message = string.Empty;

            if (equipment == null)
            {
                message = "No equipment selected.";
                return false;
            }

            if (credits == null || loadout == null)
            {
                message = "Equipment dealer is unavailable.";
                return false;
            }

            if (loadout.GetAvailableToSellCount(equipment.Id) <= 0)
            {
                message = $"{equipment.Name} is mounted or unavailable to sell.";
                Console.WriteLine($"[EQUIPMENT][FAIL] {message}");
                return false;
            }

            int saleValue = Math.Max(1, equipment.Price / 2);
            if (!loadout.RemoveOwnedEquipment(equipment.Id, 1))
            {
                message = $"Could not sell {equipment.Name}.";
                Console.WriteLine($"[EQUIPMENT][FAIL] {message}");
                return false;
            }

            credits.AddCredits(saleValue);
            message = $"Sold {equipment.Name} for {saleValue:N0} CR.";
            Console.WriteLine($"[EQUIPMENT] Sold {equipment.Name} for {saleValue:N0} CR");
            return true;
        }
    }
}
