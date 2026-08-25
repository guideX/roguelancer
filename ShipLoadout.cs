using System;
using System.Collections.Generic;
using System.Linq;

namespace Roguelancer
{
    /// <summary>
    /// Player ship loadout, including mounted hardpoints and owned equipment.
    /// </summary>
    public class ShipLoadout
    {
        public const int MaximumOwnedEquipmentCount = 24;

        private readonly List<ShipHardpoint> _hardpoints = new List<ShipHardpoint>();
        private readonly Dictionary<string, int> _ownedEquipment = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        private readonly CombatConsumableInventory _combatConsumables = new CombatConsumableInventory();
        private readonly bool _usesGenericFallbackLayout;

        public IReadOnlyList<ShipHardpoint> Hardpoints => _hardpoints;

        public IReadOnlyDictionary<string, int> OwnedEquipment => _ownedEquipment;

        public CombatConsumableInventory CombatConsumables => _combatConsumables;

        public int OwnedEquipmentCount
        {
            get
            {
                int total = 0;
                foreach (KeyValuePair<string, int> entry in _ownedEquipment)
                {
                    if (IsConsumableId(entry.Key))
                    {
                        continue;
                    }

                    total = Math.Min(MaximumOwnedEquipmentCount, total + Math.Max(0, entry.Value));
                }

                return total;
            }
        }

        public int AvailableOwnedEquipmentCapacity => Math.Max(0, MaximumOwnedEquipmentCount - OwnedEquipmentCount);

        public bool UsesGenericFallbackLayout => _usesGenericFallbackLayout;

        public ShipLoadout()
        {
            _usesGenericFallbackLayout = false;
        }

        public ShipLoadout(IEnumerable<ShipHardpoint> hardpoints, bool usesGenericFallbackLayout = false)
        {
            _usesGenericFallbackLayout = usesGenericFallbackLayout;
            if (hardpoints == null)
            {
                return;
            }

            foreach (var hardpoint in hardpoints)
            {
                if (hardpoint != null)
                {
                    _hardpoints.Add(hardpoint.Clone());
                }
            }
        }

        public static ShipLoadout CreateStarterLoadout(bool includeStarterEquipment = true)
        {
            return CreateGenericStarterLoadout(includeStarterEquipment);
        }

        /// <summary>
        /// Builds a loadout from ship-definition metadata. A missing metadata
        /// list intentionally uses the historical generic layout so future
        /// ships can be added without breaking equipment gameplay.
        /// </summary>
        public static ShipLoadout CreateForShip(ShipDefinition shipDefinition, bool includeStarterEquipment = false)
        {
            if (shipDefinition?.HasExplicitHardpointMetadata == true)
            {
                var explicitLoadout = new ShipLoadout(
                    shipDefinition.HardpointDefinitions
                        .Where(definition => definition != null)
                        .Select(definition => definition.ToRuntimeHardpoint()));

                if (includeStarterEquipment)
                {
                    MountStarterEquipment(explicitLoadout, "PrimaryGunLeft", "liberty_light_laser", EquipmentType.Gun);
                    MountStarterEquipment(explicitLoadout, "PrimaryGunRight", "rogue_blaster", EquipmentType.Gun);
                    MountStarterEquipment(explicitLoadout, "MissileRack", "basic_missile_launcher", EquipmentType.MissileLauncher);
                    MountStarterEquipment(explicitLoadout, "ShieldGenerator", "civilian_shield_generator", EquipmentType.ShieldGenerator);
                    MountStarterEquipment(explicitLoadout, "Thruster", "light_thruster", EquipmentType.Thruster);
                    MountStarterEquipment(explicitLoadout, "Scanner", "basic_scanner", EquipmentType.Scanner);
                    MountStarterEquipment(explicitLoadout, "CountermeasureRack", "basic_countermeasure_dropper", EquipmentType.CountermeasureDropper);
                    MountStarterEquipment(explicitLoadout, "Powerplant", "civilian_powerplant", EquipmentType.Powerplant);
                    AddStarterConsumables(explicitLoadout);
                }

                return explicitLoadout;
            }

            return CreateGenericStarterLoadout(includeStarterEquipment);
        }

        private static ShipLoadout CreateGenericStarterLoadout(bool includeStarterEquipment)
        {
            var loadout = new ShipLoadout(new[]
            {
                new ShipHardpoint { Id = "PrimaryGunLeft", AllowedEquipmentTypes = new List<EquipmentType> { EquipmentType.Gun } },
                new ShipHardpoint { Id = "PrimaryGunRight", AllowedEquipmentTypes = new List<EquipmentType> { EquipmentType.Gun } },
                new ShipHardpoint { Id = "MissileRack", AllowedEquipmentTypes = new List<EquipmentType> { EquipmentType.MissileLauncher } },
                new ShipHardpoint { Id = "MineRack", AllowedEquipmentTypes = new List<EquipmentType> { EquipmentType.MineDropper } },
                new ShipHardpoint { Id = "CountermeasureRack", AllowedEquipmentTypes = new List<EquipmentType> { EquipmentType.CountermeasureDropper } },
                new ShipHardpoint { Id = "ShieldGenerator", AllowedEquipmentTypes = new List<EquipmentType> { EquipmentType.ShieldGenerator } },
                new ShipHardpoint { Id = "Powerplant", AllowedEquipmentTypes = new List<EquipmentType> { EquipmentType.Powerplant } },
                new ShipHardpoint { Id = "Thruster", AllowedEquipmentTypes = new List<EquipmentType> { EquipmentType.Thruster } },
                new ShipHardpoint { Id = "Scanner", AllowedEquipmentTypes = new List<EquipmentType> { EquipmentType.Scanner } },
                new ShipHardpoint { Id = "TractorBeam", AllowedEquipmentTypes = new List<EquipmentType> { EquipmentType.TractorBeam } }
            }, usesGenericFallbackLayout: true);

            if (includeStarterEquipment)
            {
                MountStarterEquipment(loadout, "PrimaryGunLeft", "liberty_light_laser", EquipmentType.Gun);
                MountStarterEquipment(loadout, "PrimaryGunRight", "rogue_blaster", EquipmentType.Gun);
                MountStarterEquipment(loadout, "MissileRack", "basic_missile_launcher", EquipmentType.MissileLauncher);
                MountStarterEquipment(loadout, "ShieldGenerator", "civilian_shield_generator", EquipmentType.ShieldGenerator);
                MountStarterEquipment(loadout, "Thruster", "light_thruster", EquipmentType.Thruster);
                MountStarterEquipment(loadout, "Scanner", "basic_scanner", EquipmentType.Scanner);
                MountStarterEquipment(loadout, "CountermeasureRack", "basic_countermeasure_dropper", EquipmentType.CountermeasureDropper);
                MountStarterEquipment(loadout, "Powerplant", "civilian_powerplant", EquipmentType.Powerplant);
                AddStarterConsumables(loadout);
            }

            return loadout;
        }

        /// <summary>
        /// Rebuilds only the hardpoint layout while preserving every owned
        /// equipment stack. Existing stable IDs are kept first; remaining
        /// mounted items are placed into the first compatible free target
        /// hardpoint. Items that cannot fit remain owned and are unmounted.
        /// </summary>
        public ShipLoadout ReconfigureHardpoints(IEnumerable<ShipHardpoint> targetHardpoints, out List<string> warnings)
        {
            warnings = new List<string>();
            bool useFallback = targetHardpoints == null;
            IEnumerable<ShipHardpoint> definitions = targetHardpoints ?? CreateGenericFallbackHardpoints();
            ShipLoadout result = new ShipLoadout(definitions, useFallback);

            foreach (var owned in _ownedEquipment)
            {
                if (owned.Value > 0)
                {
                    result._ownedEquipment[owned.Key] = owned.Value;
                }
            }
            result._combatConsumables.CopyFrom(_combatConsumables);

            List<(ShipHardpoint Source, EquipmentDefinition Equipment)> mounted = _hardpoints
                .Where(hardpoint => hardpoint != null && !hardpoint.IsEmpty)
                .Select(hardpoint => (hardpoint, EquipmentCatalog.GetById(hardpoint.MountedEquipmentId)))
                .ToList();

            HashSet<string> placedSourceIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            bool shieldPlaced = false;
            bool powerplantPlaced = false;
            foreach (var entry in mounted)
            {
                if (entry.Equipment == null)
                {
                    warnings.Add($"kept unknown owned equipment '{entry.Source.MountedEquipmentId}' unmounted");
                    continue;
                }

                ShipHardpoint sameId = result.GetHardpointById(entry.Source.Id);
                bool isShield = entry.Equipment is ShieldEquipmentDefinition;
                bool isPowerplant = entry.Equipment is PowerplantEquipmentDefinition;
                if (sameId != null && sameId.IsEmpty && CanMount(entry.Equipment) &&
                    (!isShield || !shieldPlaced) && (!isPowerplant || !powerplantPlaced) &&
                    sameId.CanAccept(entry.Equipment))
                {
                    sameId.MountedEquipmentId = entry.Equipment.Id;
                    placedSourceIds.Add(entry.Source.Id);
                    shieldPlaced |= isShield;
                    powerplantPlaced |= isPowerplant;
                }
            }

            foreach (var entry in mounted)
            {
                if (entry.Equipment == null || placedSourceIds.Contains(entry.Source.Id))
                {
                    continue;
                }

                if (!CanMount(entry.Equipment))
                {
                    warnings.Add($"kept invalid equipment '{entry.Source.MountedEquipmentId}' unmounted");
                    continue;
                }

                bool isShield = entry.Equipment is ShieldEquipmentDefinition;
                bool isPowerplant = entry.Equipment is PowerplantEquipmentDefinition;
                if (isShield && shieldPlaced)
                {
                    warnings.Add($"unmounted {entry.Equipment.Id} from {entry.Source.Id}; only one shield hardpoint is supported");
                    continue;
                }

                if (isPowerplant && powerplantPlaced)
                {
                    warnings.Add($"unmounted {entry.Equipment.Id} from {entry.Source.Id}; only one powerplant is supported");
                    continue;
                }

                ShipHardpoint remapped = result.FindFirstCompatibleEmptyHardpoint(entry.Equipment);
                if (remapped != null)
                {
                    remapped.MountedEquipmentId = entry.Equipment.Id;
                    shieldPlaced |= isShield;
                    powerplantPlaced |= isPowerplant;
                    warnings.Add($"remapped {entry.Equipment.Id} from {entry.Source.Id} to {remapped.Id}");
                }
                else
                {
                    warnings.Add($"unmounted {entry.Equipment.Id} from {entry.Source.Id}; no compatible hardpoint on the new ship");
                }
            }

            return result;
        }

        private static IEnumerable<ShipHardpoint> CreateGenericFallbackHardpoints()
        {
            return new[]
            {
                new ShipHardpoint { Id = "PrimaryGunLeft", AllowedEquipmentTypes = new List<EquipmentType> { EquipmentType.Gun } },
                new ShipHardpoint { Id = "PrimaryGunRight", AllowedEquipmentTypes = new List<EquipmentType> { EquipmentType.Gun } },
                new ShipHardpoint { Id = "MissileRack", AllowedEquipmentTypes = new List<EquipmentType> { EquipmentType.MissileLauncher } },
                new ShipHardpoint { Id = "MineRack", AllowedEquipmentTypes = new List<EquipmentType> { EquipmentType.MineDropper } },
                new ShipHardpoint { Id = "CountermeasureRack", AllowedEquipmentTypes = new List<EquipmentType> { EquipmentType.CountermeasureDropper } },
                new ShipHardpoint { Id = "ShieldGenerator", AllowedEquipmentTypes = new List<EquipmentType> { EquipmentType.ShieldGenerator } },
                new ShipHardpoint { Id = "Powerplant", AllowedEquipmentTypes = new List<EquipmentType> { EquipmentType.Powerplant } },
                new ShipHardpoint { Id = "Thruster", AllowedEquipmentTypes = new List<EquipmentType> { EquipmentType.Thruster } },
                new ShipHardpoint { Id = "Scanner", AllowedEquipmentTypes = new List<EquipmentType> { EquipmentType.Scanner } },
                new ShipHardpoint { Id = "TractorBeam", AllowedEquipmentTypes = new List<EquipmentType> { EquipmentType.TractorBeam } }
            };
        }

        public IEnumerable<EquipmentDefinition> GetMountedEquipment()
        {
            foreach (var hardpoint in _hardpoints)
            {
                if (hardpoint == null || string.IsNullOrWhiteSpace(hardpoint.MountedEquipmentId))
                {
                    continue;
                }

                var definition = EquipmentCatalog.GetById(hardpoint.MountedEquipmentId);
                if (definition != null)
                {
                    yield return definition;
                }
            }
        }

        public IEnumerable<WeaponEquipmentDefinition> GetMountedGuns()
        {
            foreach (var equipment in GetMountedEquipment())
            {
                if (equipment is WeaponEquipmentDefinition weaponEquipment && weaponEquipment.EquipmentType == EquipmentType.Gun)
                {
                    yield return weaponEquipment;
                }
            }
        }

        public bool HasMountedGun()
        {
            return GetMountedGuns().Any();
        }

        public WeaponEquipmentDefinition GetPrimaryMountedGun()
        {
            return GetMountedGuns().FirstOrDefault();
        }

        public ShieldEquipmentDefinition GetMountedShield()
        {
            foreach (var hardpoint in _hardpoints)
            {
                if (hardpoint == null || string.IsNullOrWhiteSpace(hardpoint.MountedEquipmentId))
                {
                    continue;
                }

                EquipmentDefinition definition = EquipmentCatalog.GetById(hardpoint.MountedEquipmentId);
                if (definition is ShieldEquipmentDefinition shield && shield.IsValid &&
                    definition.EquipmentType == EquipmentType.ShieldGenerator)
                {
                    return shield;
                }
            }

            return null;
        }

        public bool HasMountedShield() => GetMountedShield() != null;

        public PowerplantEquipmentDefinition GetMountedPowerplant()
        {
            foreach (var hardpoint in _hardpoints)
            {
                if (hardpoint == null || string.IsNullOrWhiteSpace(hardpoint.MountedEquipmentId))
                {
                    continue;
                }

                EquipmentDefinition definition = EquipmentCatalog.GetById(hardpoint.MountedEquipmentId);
                if (definition is PowerplantEquipmentDefinition powerplant &&
                    powerplant.IsValid && definition.EquipmentType == EquipmentType.Powerplant)
                {
                    return powerplant;
                }
            }

            return null;
        }

        public bool HasMountedPowerplant() => GetMountedPowerplant() != null;

        public IEnumerable<EquipmentDefinition> GetMountedMissileLaunchers()
        {
            foreach (var equipment in GetMountedEquipment())
            {
                if (equipment != null && equipment.EquipmentType == EquipmentType.MissileLauncher)
                {
                    yield return equipment;
                }
            }
        }

        public bool HasMountedMissileLauncher()
        {
            return GetMountedMissileLaunchers().Any();
        }

        public EquipmentDefinition GetPrimaryMountedMissileLauncher()
        {
            return GetMountedMissileLaunchers().FirstOrDefault();
        }

        public IEnumerable<EquipmentDefinition> GetMountedMineDroppers()
        {
            foreach (var equipment in GetMountedEquipment())
            {
                if (equipment != null && equipment.EquipmentType == EquipmentType.MineDropper)
                {
                    yield return equipment;
                }
            }
        }

        public bool HasMountedMineDropper()
        {
            return GetMountedMineDroppers().Any();
        }

        public EquipmentDefinition GetPrimaryMountedMineDropper()
        {
            return GetMountedMineDroppers().FirstOrDefault();
        }

        public IEnumerable<EquipmentDefinition> GetMountedCountermeasureDroppers()
        {
            foreach (var equipment in GetMountedEquipment())
            {
                if (equipment != null && equipment.EquipmentType == EquipmentType.CountermeasureDropper)
                {
                    yield return equipment;
                }
            }
        }

        public bool HasMountedCountermeasureDropper()
        {
            return GetMountedCountermeasureDroppers().Any();
        }

        public EquipmentDefinition GetPrimaryMountedCountermeasureDropper()
        {
            return GetMountedCountermeasureDroppers().FirstOrDefault();
        }

        public ShipHardpoint GetHardpointById(string hardpointId)
        {
            if (string.IsNullOrWhiteSpace(hardpointId))
            {
                return null;
            }

            return _hardpoints.FirstOrDefault(h => string.Equals(h.Id, hardpointId, StringComparison.OrdinalIgnoreCase));
        }

        public int GetOwnedCount(string equipmentId)
        {
            if (string.IsNullOrWhiteSpace(equipmentId))
            {
                return 0;
            }

            if (IsConsumableId(equipmentId))
            {
                return _combatConsumables.GetQuantity(equipmentId);
            }

            return _ownedEquipment.TryGetValue(equipmentId, out int count) ? Math.Max(0, count) : 0;
        }

        public int GetMountedCount(string equipmentId)
        {
            if (string.IsNullOrWhiteSpace(equipmentId))
            {
                return 0;
            }

            return _hardpoints.Count(h => string.Equals(h.MountedEquipmentId, equipmentId, StringComparison.OrdinalIgnoreCase));
        }

        public int GetAvailableToMountCount(string equipmentId)
        {
            if (IsConsumableId(equipmentId))
            {
                return 0;
            }

            return Math.Max(0, GetOwnedCount(equipmentId) - GetMountedCount(equipmentId));
        }

        public int GetAvailableToSellCount(string equipmentId)
        {
            if (IsConsumableId(equipmentId))
            {
                return GetOwnedCount(equipmentId);
            }

            return GetAvailableToMountCount(equipmentId);
        }

        public bool IsEquipmentMounted(string equipmentId)
        {
            return GetMountedCount(equipmentId) > 0;
        }

        public bool AddOwnedEquipment(EquipmentDefinition equipment, int quantity = 1)
        {
            if (equipment == null || quantity <= 0)
            {
                return false;
            }

            if (equipment is ConsumableEquipmentDefinition consumable)
            {
                if (!consumable.IsValid ||
                    !_combatConsumables.TryAdd(consumable.Id, quantity))
                {
                    return false;
                }

                Console.WriteLine($"[LOADOUT] Added {quantity}x {equipment.Name} to carried supplies");
                return true;
            }

            if (quantity > AvailableOwnedEquipmentCapacity)
            {
                return false;
            }

            if (_ownedEquipment.TryGetValue(equipment.Id, out int currentQuantity))
            {
                if (currentQuantity < 0 || quantity > int.MaxValue - currentQuantity)
                {
                    return false;
                }

                _ownedEquipment[equipment.Id] = currentQuantity + quantity;
            }
            else
            {
                _ownedEquipment[equipment.Id] = quantity;
            }

            Console.WriteLine($"[LOADOUT] Added {quantity}x {equipment.Name} to owned equipment");
            return true;
        }

        /// <summary>
        /// Adds as much of a carried consumable stack as its per-type capacity
        /// permits. Mountable equipment remains all-or-nothing.
        /// </summary>
        public bool TryAddOwnedEquipmentPartial(EquipmentDefinition equipment, int requestedQuantity, out int addedQuantity)
        {
            addedQuantity = 0;
            if (equipment == null || requestedQuantity <= 0)
            {
                return false;
            }

            if (equipment is ConsumableEquipmentDefinition consumable)
            {
                return consumable.IsValid &&
                    _combatConsumables.TryAddPartial(consumable.Id, requestedQuantity, out addedQuantity);
            }

            if (!AddOwnedEquipment(equipment, requestedQuantity))
            {
                return false;
            }

            addedQuantity = requestedQuantity;
            return true;
        }

        public bool RemoveOwnedEquipment(string equipmentId, int quantity = 1)
        {
            if (string.IsNullOrWhiteSpace(equipmentId) || quantity <= 0)
            {
                return false;
            }

            if (IsConsumableId(equipmentId))
            {
                bool removedConsumable = _combatConsumables.TryRemove(equipmentId, quantity);
                if (removedConsumable)
                {
                    Console.WriteLine($"[LOADOUT] Removed {quantity}x {equipmentId} from carried supplies");
                }

                return removedConsumable;
            }

            int ownedCount = GetOwnedCount(equipmentId);
            int mountedCount = GetMountedCount(equipmentId);
            int availableToSell = Math.Max(0, ownedCount - mountedCount);
            if (availableToSell < quantity)
            {
                return false;
            }

            int newCount = ownedCount - quantity;
            if (newCount <= 0)
            {
                _ownedEquipment.Remove(equipmentId);
            }
            else
            {
                _ownedEquipment[equipmentId] = newCount;
            }

            Console.WriteLine($"[LOADOUT] Removed {quantity}x {equipmentId} from owned equipment");
            return true;
        }

        public IEnumerable<ShipHardpoint> GetCompatibleHardpoints(EquipmentDefinition equipment)
        {
            if (equipment == null)
            {
                yield break;
            }

            foreach (var hardpoint in _hardpoints)
            {
                if (hardpoint != null && CanMount(equipment) && hardpoint.CanAccept(equipment))
                {
                    yield return hardpoint;
                }
            }
        }

        public ShipHardpoint FindFirstCompatibleEmptyHardpoint(EquipmentDefinition equipment)
        {
            return GetCompatibleHardpoints(equipment).FirstOrDefault(h => h.IsEmpty);
        }

        public bool TryMountEquipment(EquipmentDefinition equipment, out string message)
        {
            message = string.Empty;

            if (equipment == null)
            {
                message = "No equipment selected.";
                return false;
            }

            if (!CanMount(equipment))
            {
                message = $"{equipment.Name} is not a valid canonical equipment definition.";
                return false;
            }

            if (equipment is ShieldEquipmentDefinition && HasMountedShield())
            {
                message = "Only one shield can be mounted at a time.";
                return false;
            }

            if (equipment is PowerplantEquipmentDefinition && HasMountedPowerplant())
            {
                message = "Only one powerplant can be mounted at a time.";
                return false;
            }

            int availableToMount = GetAvailableToMountCount(equipment.Id);
            if (availableToMount <= 0)
            {
                message = $"No spare {equipment.Name} owned to mount.";
                return false;
            }

            var hardpoint = FindFirstCompatibleEmptyHardpoint(equipment);
            if (hardpoint == null)
            {
                string allowedTypes = string.Join(", ", _hardpoints
                    .Where(h => h != null && h.CanAccept(equipment))
                    .Select(h => h.Id));
                message = string.IsNullOrWhiteSpace(allowedTypes)
                    ? $"{equipment.Name} cannot be mounted on this ship."
                    : $"No empty compatible hardpoint for {equipment.Name}. Compatible: {allowedTypes}";
                return false;
            }

            hardpoint.MountedEquipmentId = equipment.Id;
            message = $"Mounted {equipment.Name} to {hardpoint.Id}.";
            Console.WriteLine($"[LOADOUT] Mounted {equipment.Name} -> {hardpoint.Id}");
            return true;
        }

        public bool TryMountEquipment(string hardpointId, EquipmentDefinition equipment, out string message)
        {
            message = string.Empty;

            if (equipment == null)
            {
                message = "No equipment selected.";
                return false;
            }

            if (!CanMount(equipment))
            {
                message = $"{equipment.Name} is not a valid canonical equipment definition.";
                return false;
            }

            if (equipment is ShieldEquipmentDefinition && HasMountedShield())
            {
                message = "Only one shield can be mounted at a time.";
                return false;
            }

            if (equipment is PowerplantEquipmentDefinition && HasMountedPowerplant())
            {
                message = "Only one powerplant can be mounted at a time.";
                return false;
            }

            var hardpoint = GetHardpointById(hardpointId);
            if (hardpoint == null)
            {
                message = $"Hardpoint '{hardpointId}' does not exist.";
                return false;
            }

            if (!hardpoint.CanAccept(equipment))
            {
                message = $"{equipment.Name} cannot mount on {hardpoint.Id}.";
                return false;
            }

            if (!hardpoint.IsEmpty)
            {
                message = $"{hardpoint.Id} is already occupied by {hardpoint.MountedEquipmentId}.";
                return false;
            }

            if (GetAvailableToMountCount(equipment.Id) <= 0)
            {
                message = $"No spare {equipment.Name} owned to mount.";
                return false;
            }

            hardpoint.MountedEquipmentId = equipment.Id;
            message = $"Mounted {equipment.Name} to {hardpoint.Id}.";
            Console.WriteLine($"[LOADOUT] Mounted {equipment.Name} -> {hardpoint.Id}");
            return true;
        }

        public bool TryUnmountEquipment(string equipmentId, out string message)
        {
            message = string.Empty;

            if (string.IsNullOrWhiteSpace(equipmentId))
            {
                message = "No equipment selected.";
                return false;
            }

            var hardpoint = _hardpoints.FirstOrDefault(h => string.Equals(h.MountedEquipmentId, equipmentId, StringComparison.OrdinalIgnoreCase));
            if (hardpoint == null)
            {
                message = $"{equipmentId} is not currently mounted.";
                return false;
            }

            hardpoint.MountedEquipmentId = string.Empty;
            message = $"Unmounted {equipmentId} from {hardpoint.Id}.";
            Console.WriteLine($"[LOADOUT] Unmounted {equipmentId} from {hardpoint.Id}");
            return true;
        }

        public bool TryUnmountHardpoint(string hardpointId, out string message)
        {
            message = string.Empty;

            var hardpoint = GetHardpointById(hardpointId);
            if (hardpoint == null)
            {
                message = $"Hardpoint '{hardpointId}' does not exist.";
                return false;
            }

            if (hardpoint.IsEmpty)
            {
                message = $"{hardpoint.Id} is already empty.";
                return false;
            }

            string equipmentId = hardpoint.MountedEquipmentId;
            hardpoint.MountedEquipmentId = string.Empty;
            message = $"Unmounted {equipmentId} from {hardpoint.Id}.";
            Console.WriteLine($"[LOADOUT] Unmounted {equipmentId} from {hardpoint.Id}");
            return true;
        }

        public string GetMountedSummary()
        {
            var parts = _hardpoints
                .Select(h =>
                {
                    string mounted = string.IsNullOrWhiteSpace(h.MountedEquipmentId) ? "(empty)" : h.MountedEquipmentId;
                    return $"{h.Id}: {mounted}";
                });

            return string.Join(" | ", parts);
        }

        private static void MountStarterEquipment(ShipLoadout loadout, string hardpointId, string equipmentId, EquipmentType equipmentType)
        {
            var equipment = EquipmentCatalog.GetById(equipmentId);
            if (equipment == null)
            {
                equipment = EquipmentCatalog.GetFallbackForType(equipmentType);
                if (equipment == null)
                {
                    Console.WriteLine($"[LOADOUT] Missing starter equipment '{equipmentId}' and no fallback exists for {equipmentType}");
                    return;
                }

                Console.WriteLine($"[LOADOUT] Missing starter equipment '{equipmentId}', using fallback '{equipment.Name}'");
            }

            loadout.AddOwnedEquipment(equipment, 1);
            if (!loadout.TryMountEquipment(hardpointId, equipment, out string message))
            {
                Console.WriteLine($"[LOADOUT] Failed to mount starter equipment: {message}");
            }
        }

        private static void AddStarterConsumables(ShipLoadout loadout)
        {
            ConsumableEquipmentDefinition nanobots = EquipmentCatalog.GetById(CombatConsumableIds.Nanobots) as ConsumableEquipmentDefinition;
            ConsumableEquipmentDefinition batteries = EquipmentCatalog.GetById(CombatConsumableIds.ShieldBatteries) as ConsumableEquipmentDefinition;
            loadout?.AddOwnedEquipment(nanobots, 5);
            loadout?.AddOwnedEquipment(batteries, 5);
        }

        private static bool CanMount(EquipmentDefinition equipment)
        {
            if (equipment == null)
            {
                return false;
            }

            if (equipment is ConsumableEquipmentDefinition || equipment.EquipmentType == EquipmentType.Consumable)
            {
                return false;
            }

            if (equipment.EquipmentType == EquipmentType.ShieldGenerator)
            {
                return equipment is ShieldEquipmentDefinition shield && shield.IsValid;
            }

            if (equipment.EquipmentType == EquipmentType.Powerplant)
            {
                return equipment is PowerplantEquipmentDefinition powerplant && powerplant.IsValid &&
                       EquipmentCatalog.GetById(powerplant.Id) == powerplant;
            }

            return true;
        }

        private static bool IsConsumableId(string equipmentId)
        {
            return string.Equals(equipmentId, CombatConsumableIds.Nanobots, StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(equipmentId, CombatConsumableIds.ShieldBatteries, StringComparison.OrdinalIgnoreCase);
        }
    }
}
