using System;
using System.Collections.Generic;

namespace Roguelancer
{
    /// <summary>
    /// Bounded carried supplies for combat consumables. This is deliberately
    /// separate from mountable equipment slots and commodity cargo.
    /// </summary>
    public sealed class CombatConsumableInventory
    {
        public const int MaximumNanobots = 50;
        public const int MaximumShieldBatteries = 50;

        public int Nanobots
        {
            get => _nanobots;
            set => _nanobots = Clamp(value, MaximumNanobots);
        }

        public int ShieldBatteries
        {
            get => _shieldBatteries;
            set => _shieldBatteries = Clamp(value, MaximumShieldBatteries);
        }

        private int _nanobots;
        private int _shieldBatteries;

        public int GetQuantity(string consumableId)
        {
            return TryResolveSlot(consumableId, out bool nanobots)
                ? nanobots ? Nanobots : ShieldBatteries
                : 0;
        }

        public int GetMaximumQuantity(string consumableId)
        {
            return TryResolveSlot(consumableId, out bool nanobots)
                ? nanobots ? MaximumNanobots : MaximumShieldBatteries
                : 0;
        }

        public int GetAvailableCapacity(string consumableId)
        {
            return Math.Max(0, GetMaximumQuantity(consumableId) - GetQuantity(consumableId));
        }

        public bool TryAdd(string consumableId, int quantity)
        {
            return TryAddPartial(consumableId, quantity, out int added) && added == quantity;
        }

        public bool TryAddPartial(string consumableId, int requestedQuantity, out int addedQuantity)
        {
            addedQuantity = 0;
            if (requestedQuantity <= 0 || !TryResolveSlot(consumableId, out bool nanobots))
            {
                return false;
            }

            int capacity = GetAvailableCapacity(consumableId);
            addedQuantity = Math.Min(requestedQuantity, capacity);
            if (addedQuantity <= 0)
            {
                return false;
            }

            if (nanobots)
            {
                Nanobots += addedQuantity;
            }
            else
            {
                ShieldBatteries += addedQuantity;
            }

            return true;
        }

        public bool TryRemove(string consumableId, int quantity)
        {
            if (quantity <= 0 || !TryResolveSlot(consumableId, out bool nanobots) ||
                GetQuantity(consumableId) < quantity)
            {
                return false;
            }

            if (nanobots)
            {
                Nanobots -= quantity;
            }
            else
            {
                ShieldBatteries -= quantity;
            }

            return true;
        }

        public void SetQuantities(int nanobots, int shieldBatteries)
        {
            Nanobots = nanobots;
            ShieldBatteries = shieldBatteries;
        }

        public void CopyFrom(CombatConsumableInventory source)
        {
            SetQuantities(source?.Nanobots ?? 0, source?.ShieldBatteries ?? 0);
        }

        public IReadOnlyDictionary<string, int> Snapshot()
        {
            return new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            {
                [CombatConsumableIds.Nanobots] = Nanobots,
                [CombatConsumableIds.ShieldBatteries] = ShieldBatteries
            };
        }

        private static bool TryResolveSlot(string consumableId, out bool nanobots)
        {
            if (string.Equals(consumableId, CombatConsumableIds.Nanobots, StringComparison.OrdinalIgnoreCase))
            {
                nanobots = true;
                return true;
            }

            if (string.Equals(consumableId, CombatConsumableIds.ShieldBatteries, StringComparison.OrdinalIgnoreCase))
            {
                nanobots = false;
                return true;
            }

            nanobots = false;
            return false;
        }

        private static int Clamp(int value, int maximum)
        {
            return Math.Clamp(value, 0, maximum);
        }
    }

    public static class CombatConsumableIds
    {
        public const string Nanobots = "nanobots";
        public const string ShieldBatteries = "shield_batteries";
    }
}
