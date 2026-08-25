using System;

namespace Roguelancer
{
    /// <summary>
    /// Authoritative combat-consumable activation and bounded NPC policy.
    /// </summary>
    public static class CombatConsumableService
    {
        public const float NpcNanobotHullThreshold = 0.45f;
        public const float NpcShieldBatteryThreshold = 0.35f;
        public const float NpcUsageCooldownSeconds = 8f;

        public static bool TryUseNanobot(Ship ship, out string message)
        {
            message = string.Empty;
            if (ship == null || ship.Hull == null || ship.Loadout == null || ship.Hull.IsDestroyed)
            {
                message = "Nanobots unavailable while the ship is destroyed.";
                return false;
            }

            ConsumableEquipmentDefinition definition = GetDefinition(CombatConsumableType.Nanobots);
            if (!CanUseDefinition(definition, ship.Loadout, out message))
            {
                return false;
            }

            if (!IsFinitePositive(ship.Hull.MaxHull) || !IsFiniteNonNegative(ship.Hull.CurrentHull) ||
                ship.Hull.CurrentHull >= ship.Hull.MaxHull)
            {
                message = "Hull is already full.";
                return false;
            }

            if (!ship.Loadout.RemoveOwnedEquipment(definition.Id, 1))
            {
                message = "No Nanobots available.";
                return false;
            }

            if (!ship.Hull.TryRepair(definition.RestorationAmount, out float repairedAmount))
            {
                ship.Loadout.AddOwnedEquipment(definition, 1);
                message = "Nanobot repair could not be applied.";
                return false;
            }

            message = $"Nanobots repaired {repairedAmount:F0} hull.";
            return true;
        }

        public static bool TryUseShieldBattery(Ship ship, out string message)
        {
            message = string.Empty;
            if (ship == null || ship.Hull == null || ship.Loadout == null || ship.Shields == null ||
                ship.Hull.IsDestroyed)
            {
                message = "Shield Battery unavailable while the ship is invalid or destroyed.";
                return false;
            }

            ConsumableEquipmentDefinition definition = GetDefinition(CombatConsumableType.ShieldBattery);
            if (!CanUseDefinition(definition, ship.Loadout, out message))
            {
                return false;
            }

            if (ship.Loadout.GetMountedShield() == null || !ship.Shields.HasMountedShield)
            {
                message = "No mounted shield can accept a Shield Battery.";
                return false;
            }

            if (!IsFinitePositive(ship.Shields.MaxShields) ||
                !IsFiniteNonNegative(ship.Shields.CurrentShields) ||
                ship.Shields.CurrentShields >= ship.Shields.MaxShields)
            {
                message = "Shields are already full.";
                return false;
            }

            if (!ship.Loadout.RemoveOwnedEquipment(definition.Id, 1))
            {
                message = "No Shield Batteries available.";
                return false;
            }

            if (!ship.Shields.TryRestore(definition.RestorationAmount, out float restoredAmount))
            {
                ship.Loadout.AddOwnedEquipment(definition, 1);
                message = "Shield Battery restoration could not be applied.";
                return false;
            }

            message = $"Shield Battery restored {restoredAmount:F0} shields.";
            return true;
        }

        public static bool TryUseNanobot(NpcShip ship, out string message)
        {
            message = string.Empty;
            if (ship == null || ship.IsDestroyed || ship.Hull == null || ship.Loadout == null)
            {
                return false;
            }

            ConsumableEquipmentDefinition definition = GetDefinition(CombatConsumableType.Nanobots);
            if (!CanUseDefinition(definition, ship.Loadout, out _))
            {
                return false;
            }

            if (!IsFinitePositive(ship.Hull.MaxHull) || !IsFiniteNonNegative(ship.Hull.CurrentHull) ||
                ship.Hull.CurrentHull <= 0f || ship.Hull.CurrentHull >= ship.Hull.MaxHull ||
                ship.Hull.CurrentHull / ship.Hull.MaxHull > NpcNanobotHullThreshold)
            {
                return false;
            }

            if (!ship.Loadout.RemoveOwnedEquipment(definition.Id, 1))
            {
                return false;
            }

            if (!ship.Hull.TryRepair(definition.RestorationAmount, out _))
            {
                ship.Loadout.AddOwnedEquipment(definition, 1);
                return false;
            }

            message = "NPC used Nanobots.";
            return true;
        }

        public static bool TryUseShieldBattery(NpcShip ship, out string message)
        {
            message = string.Empty;
            if (ship == null || ship.IsDestroyed || ship.Shields == null || ship.Loadout == null ||
                ship.Loadout.GetMountedShield() == null)
            {
                return false;
            }

            ConsumableEquipmentDefinition definition = GetDefinition(CombatConsumableType.ShieldBattery);
            if (!CanUseDefinition(definition, ship.Loadout, out _))
            {
                return false;
            }

            if (!ship.Shields.HasMountedShield || !IsFinitePositive(ship.Shields.MaxShields) ||
                !IsFiniteNonNegative(ship.Shields.CurrentShields) ||
                ship.Shields.CurrentShields / ship.Shields.MaxShields > NpcShieldBatteryThreshold ||
                ship.Shields.CurrentShields >= ship.Shields.MaxShields)
            {
                return false;
            }

            if (!ship.Loadout.RemoveOwnedEquipment(definition.Id, 1))
            {
                return false;
            }

            if (!ship.Shields.TryRestore(definition.RestorationAmount, out _))
            {
                ship.Loadout.AddOwnedEquipment(definition, 1);
                return false;
            }

            message = "NPC used Shield Battery.";
            return true;
        }

        /// <summary>
        /// Uses at most one NPC supply per call. Nanobots take priority when
        /// both thresholds are met; the caller owns the explicit cooldown.
        /// </summary>
        public static bool TryUseNpcConsumable(NpcShip ship, out CombatConsumableType usedType)
        {
            usedType = CombatConsumableType.None;
            if (ship == null || ship.IsDestroyed || ship.CombatConsumableCooldownRemaining > 0f)
            {
                return false;
            }

            if (TryUseNanobot(ship, out _))
            {
                usedType = CombatConsumableType.Nanobots;
                ship.StartCombatConsumableCooldown(NpcUsageCooldownSeconds);
                return true;
            }

            if (TryUseShieldBattery(ship, out _))
            {
                usedType = CombatConsumableType.ShieldBattery;
                ship.StartCombatConsumableCooldown(NpcUsageCooldownSeconds);
                return true;
            }

            return false;
        }

        private static ConsumableEquipmentDefinition GetDefinition(CombatConsumableType type)
        {
            string id = type == CombatConsumableType.Nanobots
                ? CombatConsumableIds.Nanobots
                : type == CombatConsumableType.ShieldBattery
                    ? CombatConsumableIds.ShieldBatteries
                    : string.Empty;
            return EquipmentCatalog.GetById(id) as ConsumableEquipmentDefinition;
        }

        private static bool CanUseDefinition(
            ConsumableEquipmentDefinition definition,
            ShipLoadout loadout,
            out string message)
        {
            message = string.Empty;
            if (definition == null || !definition.IsValid || loadout == null)
            {
                message = "Consumable definition or loadout is invalid.";
                return false;
            }

            if (loadout.GetOwnedCount(definition.Id) <= 0)
            {
                message = $"No {definition.Name} available.";
                return false;
            }

            return true;
        }

        private static bool IsFinitePositive(float value) =>
            !float.IsNaN(value) && !float.IsInfinity(value) && value > 0f;

        private static bool IsFiniteNonNegative(float value) =>
            !float.IsNaN(value) && !float.IsInfinity(value) && value >= 0f;
    }
}
