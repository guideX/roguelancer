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
            "basic_countermeasure_dropper",
            "civilian_shield_generator",
            "liberty_patrol_shield",
            "liberty_military_shield",
            "liberty_heavy_shield",
            "rogue_scrap_shield",
            "rogue_combat_shield",
            "professional_deflector",
            "civilian_powerplant",
            "liberty_patrol_powerplant",
            "liberty_military_powerplant",
            "liberty_heavy_powerplant",
            "rogue_scrap_powerplant",
            "rogue_combat_powerplant",
            "professional_powerplant",
            "civilian_thruster",
            "liberty_patrol_thruster",
            "liberty_military_thruster",
            "liberty_heavy_thruster",
            "rogue_scrap_thruster",
            "rogue_combat_thruster",
            "professional_thruster",
            CombatConsumableIds.Nanobots,
            CombatConsumableIds.ShieldBatteries
        };
        private static readonly Dictionary<EquipmentType, string> _fallbackByType = new Dictionary<EquipmentType, string>
        {
            { EquipmentType.Gun, "liberty_light_laser" },
            { EquipmentType.MissileLauncher, "basic_missile_launcher" },
            { EquipmentType.MineDropper, "basic_mine_dropper" },
            { EquipmentType.CountermeasureDropper, "basic_countermeasure_dropper" },
            { EquipmentType.ShieldGenerator, "civilian_shield_generator" },
            { EquipmentType.Powerplant, "civilian_powerplant" },
            { EquipmentType.Thruster, "civilian_thruster" },
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

            Register(new ShieldEquipmentDefinition
            {
                Id = "civilian_shield_generator",
                Name = "Civilian Shield Generator",
                Description = "A modest shield generator suitable for standard patrol and trade runs.",
                EquipmentType = EquipmentType.ShieldGenerator,
                Price = 4200,
                Family = ShieldFamily.Liberty,
                ProgressionTier = ShieldProgressionTier.Low,
                Capacity = 50f,
                RegenerationRate = 15f,
                RegenerationDelay = 3f
            });

            Register(new ShieldEquipmentDefinition
            {
                Id = "liberty_patrol_shield",
                Name = "Liberty Patrol Shield",
                Description = "A dependable patrol-grade deflector with balanced recovery.",
                EquipmentType = EquipmentType.ShieldGenerator,
                Price = 6500,
                Family = ShieldFamily.Liberty,
                ProgressionTier = ShieldProgressionTier.Standard,
                Capacity = 65f,
                RegenerationRate = 18f,
                RegenerationDelay = 2.5f
            });

            Register(new ConsumableEquipmentDefinition
            {
                Id = CombatConsumableIds.Nanobots,
                Name = "Nanobots",
                Description = "A sealed repair swarm that restores damaged hull plating in flight.",
                EquipmentType = EquipmentType.Consumable,
                Price = 600,
                ConsumableType = CombatConsumableType.Nanobots,
                MaximumCarryQuantity = CombatConsumableInventory.MaximumNanobots,
                RestorationAmount = 25f
            });

            Register(new ConsumableEquipmentDefinition
            {
                Id = CombatConsumableIds.ShieldBatteries,
                Name = "Shield Batteries",
                Description = "A disposable shield cell that restores charge to the mounted generator.",
                EquipmentType = EquipmentType.Consumable,
                Price = 700,
                ConsumableType = CombatConsumableType.ShieldBattery,
                MaximumCarryQuantity = CombatConsumableInventory.MaximumShieldBatteries,
                RestorationAmount = 30f
            });

            Register(new ShieldEquipmentDefinition
            {
                Id = "liberty_military_shield",
                Name = "Liberty Military Shield",
                Description = "A durable military deflector that trades recharge speed for capacity.",
                EquipmentType = EquipmentType.ShieldGenerator,
                Price = 10000,
                Family = ShieldFamily.Liberty,
                ProgressionTier = ShieldProgressionTier.Standard,
                Capacity = 90f,
                RegenerationRate = 14f,
                RegenerationDelay = 4f
            });

            Register(new ShieldEquipmentDefinition
            {
                Id = "liberty_heavy_shield",
                Name = "Liberty Heavy Shield",
                Description = "A heavy military generator for large hulls and hard patrol work.",
                EquipmentType = EquipmentType.ShieldGenerator,
                Price = 16000,
                Family = ShieldFamily.Liberty,
                ProgressionTier = ShieldProgressionTier.High,
                Capacity = 125f,
                RegenerationRate = 12f,
                RegenerationDelay = 5f
            });

            Register(new ShieldEquipmentDefinition
            {
                Id = "rogue_scrap_shield",
                Name = "Rogue Scrap Shield",
                Description = "An improvised shield assembled from salvaged emitters.",
                EquipmentType = EquipmentType.ShieldGenerator,
                Price = 2800,
                Family = ShieldFamily.Rogue,
                ProgressionTier = ShieldProgressionTier.Low,
                Capacity = 45f,
                RegenerationRate = 8f,
                RegenerationDelay = 5f
            });

            Register(new ShieldEquipmentDefinition
            {
                Id = "rogue_combat_shield",
                Name = "Rogue Combat Shield",
                Description = "A reinforced raider shield with respectable capacity and slow recovery.",
                EquipmentType = EquipmentType.ShieldGenerator,
                Price = 8500,
                Family = ShieldFamily.Rogue,
                ProgressionTier = ShieldProgressionTier.Standard,
                Capacity = 80f,
                RegenerationRate = 11f,
                RegenerationDelay = 4f
            });

            Register(new ShieldEquipmentDefinition
            {
                Id = "professional_deflector",
                Name = "Professional Deflector",
                Description = "A commercial-grade deflector favored by corporations and bounty hunters.",
                EquipmentType = EquipmentType.ShieldGenerator,
                Price = 9000,
                Family = ShieldFamily.Professional,
                ProgressionTier = ShieldProgressionTier.Standard,
                Capacity = 75f,
                RegenerationRate = 16f,
                RegenerationDelay = 2.5f
            });

            Register(new PowerplantEquipmentDefinition
            {
                Id = "civilian_powerplant",
                Name = "Civilian Powerplant",
                Description = "A modest, dependable plant for civilian and starter craft.",
                EquipmentType = EquipmentType.Powerplant,
                Price = 3500,
                Family = PowerplantFamily.Civilian,
                ProgressionTier = PowerplantProgressionTier.Low,
                EnergyCapacity = 180f,
                EnergyRegenerationRate = 30f
            });

            Register(new PowerplantEquipmentDefinition
            {
                Id = "liberty_patrol_powerplant",
                Name = "Liberty Patrol Powerplant",
                Description = "A balanced patrol plant tuned for reliable sustained fire.",
                EquipmentType = EquipmentType.Powerplant,
                Price = 5500,
                Family = PowerplantFamily.Liberty,
                ProgressionTier = PowerplantProgressionTier.Standard,
                EnergyCapacity = 220f,
                EnergyRegenerationRate = 36f
            });

            Register(new PowerplantEquipmentDefinition
            {
                Id = "liberty_military_powerplant",
                Name = "Liberty Military Powerplant",
                Description = "A military-grade plant with stronger combat reserves.",
                EquipmentType = EquipmentType.Powerplant,
                Price = 9000,
                Family = PowerplantFamily.Liberty,
                ProgressionTier = PowerplantProgressionTier.Standard,
                EnergyCapacity = 280f,
                EnergyRegenerationRate = 44f
            });

            Register(new PowerplantEquipmentDefinition
            {
                Id = "liberty_heavy_powerplant",
                Name = "Liberty Heavy Powerplant",
                Description = "A high-output plant for heavy military hulls and weapons.",
                EquipmentType = EquipmentType.Powerplant,
                Price = 14500,
                Family = PowerplantFamily.Liberty,
                ProgressionTier = PowerplantProgressionTier.High,
                EnergyCapacity = 360f,
                EnergyRegenerationRate = 52f
            });

            Register(new PowerplantEquipmentDefinition
            {
                Id = "rogue_scrap_powerplant",
                Name = "Rogue Scrap Powerplant",
                Description = "An improvised plant that is cheap, rough, and serviceable.",
                EquipmentType = EquipmentType.Powerplant,
                Price = 2600,
                Family = PowerplantFamily.Rogue,
                ProgressionTier = PowerplantProgressionTier.Low,
                EnergyCapacity = 160f,
                EnergyRegenerationRate = 24f
            });

            Register(new PowerplantEquipmentDefinition
            {
                Id = "rogue_combat_powerplant",
                Name = "Rogue Combat Powerplant",
                Description = "A scavenged combat plant rebuilt for aggressive raider craft.",
                EquipmentType = EquipmentType.Powerplant,
                Price = 7500,
                Family = PowerplantFamily.Rogue,
                ProgressionTier = PowerplantProgressionTier.Standard,
                EnergyCapacity = 260f,
                EnergyRegenerationRate = 38f
            });

            Register(new PowerplantEquipmentDefinition
            {
                Id = "professional_powerplant",
                Name = "Professional Powerplant",
                Description = "A refined commercial plant with efficient combat recovery.",
                EquipmentType = EquipmentType.Powerplant,
                Price = 12000,
                Family = PowerplantFamily.Professional,
                ProgressionTier = PowerplantProgressionTier.High,
                EnergyCapacity = 320f,
                EnergyRegenerationRate = 50f
            });

            Register(new ThrusterEquipmentDefinition
            {
                Id = "civilian_thruster",
                Name = "Civilian Thruster",
                Description = "A dependable entry-level thruster with finite afterburn reserves.",
                EquipmentType = EquipmentType.Thruster,
                Price = 6000,
                Family = ThrusterFamily.Civilian,
                ProgressionTier = ThrusterProgressionTier.Low,
                EnergyCapacity = 240f,
                EnergyRegenerationRate = 36f,
                AfterburnDrainRate = 40f,
                AfterburnSpeedMultiplier = 2.00f
            });

            Register(new ThrusterEquipmentDefinition
            {
                Id = "liberty_patrol_thruster",
                Name = "Liberty Patrol Thruster",
                Description = "A balanced patrol thruster tuned for reliable pursuit bursts.",
                EquipmentType = EquipmentType.Thruster,
                Price = 8500,
                Family = ThrusterFamily.Liberty,
                ProgressionTier = ThrusterProgressionTier.Standard,
                EnergyCapacity = 280f,
                EnergyRegenerationRate = 44f,
                AfterburnDrainRate = 50f,
                AfterburnSpeedMultiplier = 2.05f
            });

            Register(new ThrusterEquipmentDefinition
            {
                Id = "liberty_military_thruster",
                Name = "Liberty Military Thruster",
                Description = "A military thruster with stronger reserves for combat pilots.",
                EquipmentType = EquipmentType.Thruster,
                Price = 12500,
                Family = ThrusterFamily.Liberty,
                ProgressionTier = ThrusterProgressionTier.Standard,
                EnergyCapacity = 340f,
                EnergyRegenerationRate = 52f,
                AfterburnDrainRate = 58f,
                AfterburnSpeedMultiplier = 2.10f
            });

            Register(new ThrusterEquipmentDefinition
            {
                Id = "liberty_heavy_thruster",
                Name = "Liberty Heavy Thruster",
                Description = "A high-capacity military thruster for heavy hulls.",
                EquipmentType = EquipmentType.Thruster,
                Price = 19000,
                Family = ThrusterFamily.Liberty,
                ProgressionTier = ThrusterProgressionTier.High,
                EnergyCapacity = 420f,
                EnergyRegenerationRate = 60f,
                AfterburnDrainRate = 66f,
                AfterburnSpeedMultiplier = 2.15f
            });

            Register(new ThrusterEquipmentDefinition
            {
                Id = "rogue_scrap_thruster",
                Name = "Rogue Scrap Thruster",
                Description = "An improvised thruster assembled from salvaged parts.",
                EquipmentType = EquipmentType.Thruster,
                Price = 4200,
                Family = ThrusterFamily.Rogue,
                ProgressionTier = ThrusterProgressionTier.Low,
                EnergyCapacity = 220f,
                EnergyRegenerationRate = 32f,
                AfterburnDrainRate = 44f,
                AfterburnSpeedMultiplier = 1.95f
            });

            Register(new ThrusterEquipmentDefinition
            {
                Id = "rogue_combat_thruster",
                Name = "Rogue Combat Thruster",
                Description = "A rebuilt raider thruster for aggressive short bursts.",
                EquipmentType = EquipmentType.Thruster,
                Price = 10500,
                Family = ThrusterFamily.Rogue,
                ProgressionTier = ThrusterProgressionTier.Standard,
                EnergyCapacity = 300f,
                EnergyRegenerationRate = 46f,
                AfterburnDrainRate = 52f,
                AfterburnSpeedMultiplier = 2.10f
            });

            Register(new ThrusterEquipmentDefinition
            {
                Id = "professional_thruster",
                Name = "Professional Thruster",
                Description = "An efficient commercial thruster favored by specialists.",
                EquipmentType = EquipmentType.Thruster,
                Price = 16000,
                Family = ThrusterFamily.Professional,
                ProgressionTier = ThrusterProgressionTier.High,
                EnergyCapacity = 360f,
                EnergyRegenerationRate = 58f,
                AfterburnDrainRate = 65f,
                AfterburnSpeedMultiplier = 2.20f
            });

            Register(new ThrusterEquipmentDefinition
            {
                Id = "light_thruster",
                Name = "Light Thruster",
                Description = "Legacy starter thruster retained for older schema-10 saves.",
                EquipmentType = EquipmentType.Thruster,
                Price = 6000,
                Family = ThrusterFamily.Civilian,
                ProgressionTier = ThrusterProgressionTier.Low,
                EnergyCapacity = 240f,
                EnergyRegenerationRate = 36f,
                AfterburnDrainRate = 40f,
                AfterburnSpeedMultiplier = 2.00f
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
