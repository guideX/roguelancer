using System;
using System.Collections.Generic;
using System.Linq;

namespace Roguelancer
{
    /// <summary>
    /// Authoritative canonical equipment catalog with safe fallback behavior.
    /// </summary>
    public static class EquipmentCatalog
    {
        private static readonly Dictionary<string, EquipmentDefinition> _definitions = new Dictionary<string, EquipmentDefinition>(StringComparer.OrdinalIgnoreCase);
        private static readonly string[] _dealerInventoryIds =
        {
            // All canonical flight guns are sold through the same bounded
            // dealer path used by the original equipment definitions.
            "liberty_pulse_cannon",
            "rogue_blaster",
            "liberty_light_laser",
            "liberty_sentry_blaster",
            "liberty_ranger_laser",
            "liberty_heavy_pulse",
            "rogue_needle_blaster",
            "rogue_scattergun",
            "rogue_rail_cannon",
            "basic_missile_launcher",
            "basic_mine_dropper",
            "basic_countermeasure_dropper"
        };
        private static readonly Dictionary<EquipmentType, string> _fallbackByType = new Dictionary<EquipmentType, string>
        {
            { EquipmentType.Gun, "liberty_light_laser" },
            { EquipmentType.MissileLauncher, "basic_missile_launcher" },
            { EquipmentType.MineDropper, "basic_mine_dropper" },
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
                Price = 3200,
                WeaponType = WeaponType.LaserBolt,
                Family = WeaponFamily.Liberty,
                ProgressionTier = WeaponProgressionTier.Low,
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
                Price = 6500,
                WeaponType = WeaponType.BlueDonut,
                Family = WeaponFamily.Liberty,
                ProgressionTier = WeaponProgressionTier.Standard,
                Damage = 24f,
                ProjectileSpeed = 1500f,
                RefireRate = 0.28f,
                EnergyCost = 18f,
                Range = 4500f,
                RequiredReputationFactionId = FactionManager.LibertyPolice,
                MinimumReputation = ReputationManager.FriendlyThreshold
            });

            Register(new WeaponEquipmentDefinition
            {
                Id = "rogue_blaster",
                Name = "Rogue Blaster",
                Description = "A pirate-friendly blaster tuned for fast, dirty fights.",
                EquipmentType = EquipmentType.Gun,
                Price = 2400,
                WeaponType = WeaponType.BlueDonut,
                Family = WeaponFamily.Rogue,
                ProgressionTier = WeaponProgressionTier.Low,
                Damage = 8f,
                ProjectileSpeed = 1500f,
                RefireRate = 0.10f,
                EnergyCost = 12f,
                Range = 3800f,
                RequiredReputationFactionId = FactionManager.LibertyRogues,
                MinimumReputation = ReputationManager.FriendlyThreshold
            });

            Register(new WeaponEquipmentDefinition
            {
                Id = "liberty_sentry_blaster",
                Name = "Liberty Sentry Blaster",
                Description = "An efficient security blaster built for steady patrol duty.",
                EquipmentType = EquipmentType.Gun,
                Price = 3900,
                WeaponType = WeaponType.QuickBlaster,
                Family = WeaponFamily.Liberty,
                ProgressionTier = WeaponProgressionTier.Low,
                Damage = 12f,
                ProjectileSpeed = 1900f,
                RefireRate = 0.12f,
                EnergyCost = 7f,
                Range = 4200f
            });

            Register(new WeaponEquipmentDefinition
            {
                Id = "liberty_ranger_laser",
                Name = "Liberty Ranger Laser",
                Description = "A long-range laser favored by patrol marksmen and escorts.",
                EquipmentType = EquipmentType.Gun,
                Price = 8200,
                WeaponType = WeaponType.LaserBolt,
                Family = WeaponFamily.Liberty,
                ProgressionTier = WeaponProgressionTier.Standard,
                Damage = 22f,
                ProjectileSpeed = 2800f,
                RefireRate = 0.32f,
                EnergyCost = 14f,
                Range = 7200f
            });

            Register(new WeaponEquipmentDefinition
            {
                Id = "liberty_heavy_pulse",
                Name = "Liberty Heavy Pulse",
                Description = "A hard-hitting military pulse cannon with deliberate recovery time.",
                EquipmentType = EquipmentType.Gun,
                Price = 12500,
                WeaponType = WeaponType.Fireball,
                Family = WeaponFamily.Liberty,
                ProgressionTier = WeaponProgressionTier.High,
                Damage = 36f,
                ProjectileSpeed = 1250f,
                RefireRate = 0.45f,
                EnergyCost = 28f,
                Range = 5200f
            });

            Register(new WeaponEquipmentDefinition
            {
                Id = "rogue_needle_blaster",
                Name = "Rogue Needle Blaster",
                Description = "A compact pirate repeater that trades impact for relentless fire.",
                EquipmentType = EquipmentType.Gun,
                Price = 4300,
                WeaponType = WeaponType.QuickBlaster,
                Family = WeaponFamily.Rogue,
                ProgressionTier = WeaponProgressionTier.Standard,
                Damage = 11f,
                ProjectileSpeed = 2500f,
                RefireRate = 0.09f,
                EnergyCost = 10f,
                Range = 4000f
            });

            Register(new WeaponEquipmentDefinition
            {
                Id = "rogue_scattergun",
                Name = "Rogue Scattergun",
                Description = "A short-range raider cannon that delivers a brutal close pass.",
                EquipmentType = EquipmentType.Gun,
                Price = 7000,
                WeaponType = WeaponType.Fireball,
                Family = WeaponFamily.Rogue,
                ProgressionTier = WeaponProgressionTier.Standard,
                Damage = 28f,
                ProjectileSpeed = 1150f,
                RefireRate = 0.36f,
                EnergyCost = 24f,
                Range = 3000f
            });

            Register(new WeaponEquipmentDefinition
            {
                Id = "rogue_rail_cannon",
                Name = "Rogue Rail Cannon",
                Description = "A costly pirate siege gun with extreme impact and a slow cycle.",
                EquipmentType = EquipmentType.Gun,
                Price = 13500,
                WeaponType = WeaponType.Fireball,
                Family = WeaponFamily.Rogue,
                ProgressionTier = WeaponProgressionTier.High,
                Damage = 40f,
                ProjectileSpeed = 1100f,
                RefireRate = 0.52f,
                EnergyCost = 34f,
                Range = 5400f
            });

            Register(new EquipmentDefinition
            {
                Id = "basic_missile_launcher",
                Name = "Basic Missile Launcher",
                Description = "A simple launcher for basic homing missiles.",
                EquipmentType = EquipmentType.MissileLauncher,
                Price = 5500,
                MissileDamage = 48f,
                MissileSpeed = 900f,
                MissileTurnRate = 2.2f,
                MissileLifetime = 5.0f,
                MissileAmmoCost = 1f
            });

            Register(new EquipmentDefinition
            {
                Id = "basic_mine_dropper",
                Name = "Basic Mine Dropper",
                Description = "Drops a compact proximity mine that detonates on nearby hostiles.",
                EquipmentType = EquipmentType.MineDropper,
                Price = 2600,
                MineDamage = 110f,
                MineTriggerRadius = 90f,
                MineBlastRadius = 240f,
                MineLifetime = 18f,
                MineCooldown = 6f,
                MineArmDelay = 0.8f
            });

            Register(new EquipmentDefinition
            {
                Id = "basic_countermeasure_dropper",
                Name = "Basic Countermeasure Dropper",
                Description = "Launches defensive flares and chaff to confuse hostile targeting.",
                EquipmentType = EquipmentType.CountermeasureDropper,
                Price = 1800,
                CountermeasureLife = 4.5f,
                CountermeasureAttractionRadius = 1400f,
                CountermeasureStrength = 3f,
                CountermeasureCooldown = 6f
            });

            Register(new EquipmentDefinition
            {
                Id = "civilian_shield_generator",
                Name = "Civilian Shield Generator",
                Description = "A modest shield generator suitable for standard patrol and trade runs.",
                EquipmentType = EquipmentType.ShieldGenerator,
                Price = 4200
            });

            Register(new EquipmentDefinition
            {
                Id = "light_thruster",
                Name = "Light Thruster",
                Description = "A lightweight thruster package for improved responsiveness.",
                EquipmentType = EquipmentType.Thruster,
                Price = 6000
            });

            Register(new EquipmentDefinition
            {
                Id = "basic_scanner",
                Name = "Basic Scanner",
                Description = "A compact scanner for identifying ships, cargo, and contacts.",
                EquipmentType = EquipmentType.Scanner,
                Price = 1200
            });
        }

        public static IReadOnlyList<EquipmentDefinition> GetAll()
        {
            return _definitions.Values
                .OrderBy(definition => definition.EquipmentType)
                .ThenBy(definition => definition.Price)
                .ToList();
        }

        public static IReadOnlyList<EquipmentDefinition> GetDealerInventory()
        {
            return _dealerInventoryIds
                .Select(GetById)
                .Where(definition => definition != null)
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
