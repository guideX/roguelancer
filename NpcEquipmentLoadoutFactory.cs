using System;

namespace Roguelancer
{
    /// <summary>
    /// Defines the small set of existing catalog weapons carried by ambient
    /// fighter archetypes. The runtime NPC weapon simulation remains separate;
    /// this loadout is the authoritative durable-definition snapshot used by
    /// equipment salvage.
    /// </summary>
    public static class NpcEquipmentLoadoutFactory
    {
        public static ShipLoadout CreateForNpc(string archetypeName, string factionId, string modelPath = null)
        {
            ShipLoadout loadout = ShipLoadout.CreateStarterLoadout(false);
            if (!IsFighter(archetypeName, modelPath))
            {
                return loadout;
            }

            string weaponId = IsRogue(factionId) ? "rogue_blaster" : "liberty_pulse_cannon";
            EquipmentDefinition weapon = EquipmentCatalog.GetById(weaponId);
            if (weapon is not WeaponEquipmentDefinition || weapon.EquipmentType != EquipmentType.Gun)
            {
                return loadout;
            }

            int weaponCount = IsHeavy(archetypeName, modelPath) ? 2 : 1;
            if (!loadout.AddOwnedEquipment(weapon, weaponCount))
            {
                return loadout;
            }

            for (int i = 0; i < weaponCount; i++)
            {
                if (!loadout.TryMountEquipment(weapon, out _))
                {
                    break;
                }
            }

            return loadout;
        }

        private static bool IsFighter(string archetypeName, string modelPath)
        {
            string descriptor = $"{archetypeName} {modelPath}";
            return descriptor.IndexOf("fighter", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   descriptor.IndexOf("warthog", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool IsHeavy(string archetypeName, string modelPath)
        {
            string descriptor = $"{archetypeName} {modelPath}";
            return descriptor.IndexOf("warthog", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   descriptor.IndexOf("heavy", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool IsRogue(string factionId)
        {
            string normalized = FactionManager.NormalizeFactionId(factionId);
            return normalized.IndexOf("rogue", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   normalized.IndexOf("pirate", StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
