using System;
using System.Collections.Generic;
using System.Linq;

namespace Roguelancer
{
    /// <summary>
    /// Small starter equipment catalog with safe fallback behavior.
    /// </summary>
    public static class EquipmentCatalog
    {
        private static readonly Dictionary<string, EquipmentDefinition> _definitions = new Dictionary<string, EquipmentDefinition>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<EquipmentType, string> _fallbackByType = new Dictionary<EquipmentType, string>
        {
            { EquipmentType.Gun, "liberty_light_laser" },
            { EquipmentType.MissileLauncher, "basic_missile_launcher" },
            { EquipmentType.CountermeasureDropper, "basic_countermeasure_dropper" },
            { EquipmentType.ShieldGenerator, "civilian_shield_generator" },
            { EquipmentType.Thruster, "light_thruster" },
            { EquipmentType.Scanner, "basic_scanner" }
        };

        static EquipmentCatalog()
        {
            Register(new WeaponEquipmentDefinition
            {
                Id = "liberty_light_laser",
                Name = "Liberty Light Laser",
                Description = "A dependable light laser for civilian and militia craft.",
                EquipmentType = EquipmentType.Gun,
                Price = 2500,
                WeaponType = WeaponType.LaserBolt,
                Damage = 16f,
                ProjectileSpeed = 2200f,
                RefireRate = 0.18f,
                EnergyCost = 8f,
                Range = 5000f
            });

            Register(new WeaponEquipmentDefinition
            {
                Id = "liberty_pulse_cannon",
                Name = "Liberty Pulse Cannon",
                Description = "A heavier pulse cannon with stronger impact and a slower cadence.",
                EquipmentType = EquipmentType.Gun,
                Price = 5000,
                WeaponType = WeaponType.BlueDonut,
                Damage = 24f,
                ProjectileSpeed = 1500f,
                RefireRate = 0.28f,
                EnergyCost = 18f,
                Range = 4500f
            });

            Register(new WeaponEquipmentDefinition
            {
                Id = "rogue_blaster",
                Name = "Rogue Blaster",
                Description = "A pirate-friendly blaster tuned for fast, dirty fights.",
                EquipmentType = EquipmentType.Gun,
                Price = 1800,
                WeaponType = WeaponType.BlueDonut,
                Damage = 8f,
                ProjectileSpeed = 1500f,
                RefireRate = 0.10f,
                EnergyCost = 12f,
                Range = 3800f
            });

            Register(new EquipmentDefinition
            {
                Id = "basic_missile_launcher",
                Name = "Basic Missile Launcher",
                Description = "A simple launcher for basic homing missiles.",
                EquipmentType = EquipmentType.MissileLauncher,
                Price = 4000,
                MissileDamage = 48f,
                MissileSpeed = 900f,
                MissileTurnRate = 2.2f,
                MissileLifetime = 5.0f,
                MissileAmmoCost = 1f
            });

            Register(new EquipmentDefinition
            {
                Id = "basic_countermeasure_dropper",
                Name = "Basic Countermeasure Dropper",
                Description = "Launches defensive flares and chaff to confuse hostile targeting.",
                EquipmentType = EquipmentType.CountermeasureDropper,
                Price = 1200
            });

            Register(new EquipmentDefinition
            {
                Id = "civilian_shield_generator",
                Name = "Civilian Shield Generator",
                Description = "A modest shield generator suitable for standard patrol and trade runs.",
                EquipmentType = EquipmentType.ShieldGenerator,
                Price = 3000
            });

            Register(new EquipmentDefinition
            {
                Id = "light_thruster",
                Name = "Light Thruster",
                Description = "A lightweight thruster package for improved responsiveness.",
                EquipmentType = EquipmentType.Thruster,
                Price = 4500
            });

            Register(new EquipmentDefinition
            {
                Id = "basic_scanner",
                Name = "Basic Scanner",
                Description = "A compact scanner for identifying ships, cargo, and contacts.",
                EquipmentType = EquipmentType.Scanner,
                Price = 800
            });
        }

        public static IReadOnlyList<EquipmentDefinition> GetAll()
        {
            return _definitions.Values
                .OrderBy(definition => definition.EquipmentType)
                .ThenBy(definition => definition.Price)
                .ToList();
        }

        public static EquipmentDefinition GetById(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                return null;
            }

            return _definitions.TryGetValue(id, out var definition) ? definition : null;
        }

        public static EquipmentDefinition GetByIndex(int index)
        {
            var all = GetAll();
            if (index < 0 || index >= all.Count)
            {
                return null;
            }

            return all[index];
        }

        public static EquipmentDefinition GetFallbackForType(EquipmentType equipmentType)
        {
            if (_fallbackByType.TryGetValue(equipmentType, out var fallbackId))
            {
                return GetById(fallbackId);
            }

            return _definitions.Values.FirstOrDefault(definition => definition.EquipmentType == equipmentType);
        }

        public static EquipmentDefinition GetByIdOrFallback(string id, EquipmentType equipmentType)
        {
            return GetById(id) ?? GetFallbackForType(equipmentType);
        }

        public static void Register(EquipmentDefinition definition)
        {
            if (definition == null || string.IsNullOrWhiteSpace(definition.Id))
            {
                return;
            }

            if (_definitions.ContainsKey(definition.Id))
            {
                Console.WriteLine($"[EQUIPMENT CATALOG] Replacing definition: {definition.Id}");
                _definitions[definition.Id] = definition;
            }
            else
            {
                _definitions.Add(definition.Id, definition);
            }
        }
    }
}
