using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Roguelancer
{
    public enum CombatSalvageTier
    {
        None,
        Standard,
        Heavy
    }

    public enum SalvagePayloadType
    {
        Commodity,
        Equipment,
        Consumable
    }

    /// <summary>
    /// Immutable policy output for one physical salvage object. The object is
    /// not player property until a successful pickup transfers its payload.
    /// </summary>
    public sealed class SalvageDrop
    {
        public SalvagePayloadType PayloadType { get; }
        public bool IsEquipment => PayloadType == SalvagePayloadType.Equipment;
        public bool IsCommodity => PayloadType == SalvagePayloadType.Commodity;
        public bool IsConsumable => PayloadType == SalvagePayloadType.Consumable;
        public string CommodityId { get; }
        public string EquipmentId { get; }
        public string ConsumableId { get; }
        public int Quantity { get; }
        public int StackIndex { get; }
        public CombatSalvageTier Tier { get; }

        public SalvageDrop(string commodityId, int quantity, int stackIndex, CombatSalvageTier tier)
        {
            PayloadType = SalvagePayloadType.Commodity;
            CommodityId = commodityId ?? string.Empty;
            EquipmentId = string.Empty;
            ConsumableId = string.Empty;
            Quantity = Math.Max(0, quantity);
            StackIndex = Math.Max(0, stackIndex);
            Tier = tier;
        }

        public static SalvageDrop ForEquipment(string equipmentId, int stackIndex, CombatSalvageTier tier)
        {
            return new SalvageDrop(
                SalvagePayloadType.Equipment,
                equipmentId,
                string.Empty,
                1,
                stackIndex,
                tier);
        }

        public static SalvageDrop ForConsumable(string consumableId, int quantity, int stackIndex, CombatSalvageTier tier)
        {
            return new SalvageDrop(
                SalvagePayloadType.Consumable,
                string.Empty,
                consumableId,
                quantity,
                stackIndex,
                tier);
        }

        private SalvageDrop(
            SalvagePayloadType payloadType,
            string equipmentId,
            string consumableId,
            int quantity,
            int stackIndex,
            CombatSalvageTier tier)
        {
            PayloadType = payloadType;
            CommodityId = string.Empty;
            EquipmentId = equipmentId ?? string.Empty;
            ConsumableId = consumableId ?? string.Empty;
            Quantity = Math.Max(0, quantity);
            StackIndex = Math.Max(0, stackIndex);
            Tier = tier;
        }
    }

    /// <summary>
    /// Deterministic, bounded policy for commodity salvage created by a real
    /// authoritative NPC destruction event. Bounty and reputation authorities
    /// are intentionally not referenced here.
    /// </summary>
    public sealed class CombatSalvageService
    {
        public const int MaxLiveSalvageObjects = 32;
        public const int MaximumRememberedDestructions = 256;
        public const double SalvageLifetimeSeconds = 120d;
        public const float PickupRadius = 200f;
        public const float MinimumSpawnOffset = 75f;
        public const float MaximumSpawnOffset = 200f;
        public const int StandardMaximumObjectsPerDestruction = 1;
        public const int HeavyMaximumObjectsPerDestruction = 2;
        public const int StandardMaximumEquipmentObjectsPerDestruction = 1;
        public const int HeavyMaximumEquipmentObjectsPerDestruction = 2;
        public const int StandardMinimumQuantity = 1;
        public const int StandardMaximumQuantity = 3;
        public const int HeavyMinimumQuantity = 2;
        public const int HeavyMaximumQuantity = 5;
        public const int StandardMaximumConsumableQuantity = 3;
        public const int HeavyMaximumConsumableQuantity = 3;

        private static readonly string[] RogueCommodityPool =
        {
            "side-arms",
            "construction-materials",
            "boron",
            "water",
            "consumer-goods",
            "food-rations"
        };

        private static readonly string[] LawfulCommodityPool =
        {
            "h-fuel",
            "medical-supplies",
            "construction-materials",
            "consumer-goods",
            "water"
        };

        private static readonly string[] TraderCommodityPool =
        {
            "food-rations",
            "water",
            "consumer-goods",
            "boron"
        };

        private readonly HashSet<NpcShip> _processedDestructions = new();
        private readonly Queue<NpcShip> _processedDestructionOrder = new();

        public int ProcessedDestructionCount => _processedDestructions.Count;

        /// <summary>
        /// Returns deterministic policy output and remembers a valid death so
        /// duplicate event subscriptions cannot create duplicate drops.
        /// </summary>
        public IReadOnlyList<SalvageDrop> EvaluateDestruction(NpcShip destroyedShip)
        {
            if (!IsEligibleDestruction(destroyedShip) || !RememberDestruction(destroyedShip))
            {
                return Array.Empty<SalvageDrop>();
            }

            CombatSalvageTier tier = DetermineTier(destroyedShip);
            if (tier == CombatSalvageTier.None)
            {
                return Array.Empty<SalvageDrop>();
            }

            List<SalvageDrop> drops = new();
            string[] pool = GetCommodityPool(destroyedShip);
            if (pool.Length > 0 && ShouldDrop(destroyedShip, tier))
            {
                AddCommodityDrops(drops, destroyedShip, tier, pool);
            }

            if (ShouldDropEquipment(destroyedShip, tier))
            {
                AddEquipmentDrops(drops, destroyedShip, tier);
            }

            if (ShouldDropShield(destroyedShip, tier))
            {
                AddShieldDrop(drops, destroyedShip, tier);
            }

            if (ShouldDropPowerplant(destroyedShip, tier))
            {
                AddPowerplantDrop(drops, destroyedShip, tier);
            }

            AddConsumableDrops(drops, destroyedShip, tier);

            return drops.Count == 0 ? Array.Empty<SalvageDrop>() : drops;
        }

        private void AddCommodityDrops(List<SalvageDrop> drops, NpcShip destroyedShip, CombatSalvageTier tier, string[] pool)
        {
            uint identityHash = GetIdentityHash(destroyedShip, tier);
            if (tier == CombatSalvageTier.Standard)
            {
                // Keep the pre-Phase-38 trader-route loop's compact 1-2
                // balance while combat fighters use the Phase 38 1-3 bound.
                int maximumQuantity = destroyedShip.TrafficBehavior == TrafficZoneBehaviorType.TraderRoute
                    ? 2
                    : StandardMaximumQuantity;
                int quantity = StandardMinimumQuantity + (int)(Hash(identityHash, "quantity") %
                    (maximumQuantity - StandardMinimumQuantity + 1));
                drops.Add(new SalvageDrop(
                    pool[(int)(Hash(identityHash, "commodity|0") % (uint)pool.Length)],
                    quantity,
                    0,
                    tier));
                return;
            }

            int totalQuantity = HeavyMinimumQuantity + (int)(Hash(identityHash, "quantity") %
                (HeavyMaximumQuantity - HeavyMinimumQuantity + 1));
            int objectCount = Hash(identityHash, "objects") % 2u == 0u ? 1 : 2;
            if (objectCount == 1)
            {
                drops.Add(new SalvageDrop(
                    pool[(int)(Hash(identityHash, "commodity|0") % (uint)pool.Length)],
                    totalQuantity,
                    0,
                    tier));
                return;
            }

            int firstQuantity = 1 + (int)(Hash(identityHash, "split") % (uint)(totalQuantity - 1));
            drops.Add(new SalvageDrop(
                pool[(int)(Hash(identityHash, "commodity|0") % (uint)pool.Length)],
                firstQuantity,
                0,
                tier));
            drops.Add(new SalvageDrop(
                pool[(int)(Hash(identityHash, "commodity|1") % (uint)pool.Length)],
                totalQuantity - firstQuantity,
                1,
                tier));
        }

        private void AddEquipmentDrops(List<SalvageDrop> drops, NpcShip destroyedShip, CombatSalvageTier tier)
        {
            List<WeaponEquipmentDefinition> eligible = new();
            foreach (WeaponEquipmentDefinition weapon in destroyedShip.Loadout?.GetMountedGuns() ?? Array.Empty<WeaponEquipmentDefinition>())
            {
                EquipmentDefinition canonical = EquipmentCatalog.GetById(weapon?.Id);
                if (canonical is WeaponEquipmentDefinition canonicalWeapon &&
                    canonicalWeapon.EquipmentType == EquipmentType.Gun)
                {
                    eligible.Add(canonicalWeapon);
                }
            }

            if (eligible.Count == 0)
            {
                return;
            }

            int maximumObjects = tier == CombatSalvageTier.Heavy
                ? HeavyMaximumEquipmentObjectsPerDestruction
                : StandardMaximumEquipmentObjectsPerDestruction;
            int objectCount = Math.Min(maximumObjects, eligible.Count);
            uint identityHash = GetIdentityHash(destroyedShip, tier);
            int startIndex = (int)(Hash(identityHash, "equipment-slot") % (uint)eligible.Count);
            for (int i = 0; i < objectCount; i++)
            {
                WeaponEquipmentDefinition weapon = eligible[(startIndex + i) % eligible.Count];
                drops.Add(SalvageDrop.ForEquipment(weapon.Id, i, tier));
            }
        }

        private static void AddShieldDrop(List<SalvageDrop> drops, NpcShip destroyedShip, CombatSalvageTier tier)
        {
            ShieldEquipmentDefinition shield = destroyedShip?.Loadout?.GetMountedShield();
            if (shield == null || !shield.IsValid)
            {
                return;
            }

            int maximumObjects = tier == CombatSalvageTier.Heavy
                ? HeavyMaximumEquipmentObjectsPerDestruction
                : StandardMaximumEquipmentObjectsPerDestruction;
            if (drops.Count(drop => drop != null && drop.IsEquipment) >= maximumObjects)
            {
                return;
            }

            // There is one logical shield slot, so one destruction can create
            // at most one shield pod and it preserves the exact mounted ID.
            drops.Add(SalvageDrop.ForEquipment(shield.Id, drops.Count, tier));
        }

        private static void AddPowerplantDrop(List<SalvageDrop> drops, NpcShip destroyedShip, CombatSalvageTier tier)
        {
            PowerplantEquipmentDefinition powerplant = destroyedShip?.Loadout?.GetMountedPowerplant();
            if (powerplant == null || !powerplant.IsValid)
            {
                return;
            }

            int maximumObjects = tier == CombatSalvageTier.Heavy
                ? HeavyMaximumEquipmentObjectsPerDestruction
                : StandardMaximumEquipmentObjectsPerDestruction;
            if (drops.Count(drop => drop != null && drop.IsEquipment) >= maximumObjects)
            {
                return;
            }

            // The exact mounted canonical ID is preserved. The existing
            // per-destruction equipment bound still applies, and the shared
            // LootManager live-object cap remains the final world bound.
            drops.Add(SalvageDrop.ForEquipment(powerplant.Id, drops.Count, tier));
        }

        private void AddConsumableDrops(List<SalvageDrop> drops, NpcShip destroyedShip, CombatSalvageTier tier)
        {
            if (drops == null || destroyedShip?.Loadout == null)
            {
                return;
            }

            string[] ids = { CombatConsumableIds.Nanobots, CombatConsumableIds.ShieldBatteries };
            uint identityHash = GetIdentityHash(destroyedShip, tier);
            int maximumQuantity = tier == CombatSalvageTier.Heavy
                ? HeavyMaximumConsumableQuantity
                : StandardMaximumConsumableQuantity;

            for (int i = 0; i < ids.Length; i++)
            {
                string id = ids[i];
                int remaining = Math.Max(0, destroyedShip.Loadout.GetOwnedCount(id));
                if (remaining <= 0 || !ShouldDropConsumable(destroyedShip, tier, id))
                {
                    continue;
                }

                int desired = 1 + (int)(Hash(identityHash, $"consumable-quantity|{id}") % (uint)maximumQuantity);
                int quantity = Math.Min(remaining, desired);
                if (quantity > 0)
                {
                    drops.Add(SalvageDrop.ForConsumable(id, quantity, drops.Count, tier));
                }
            }
        }

        public IReadOnlyList<SalvageDrop> ProcessDestruction(NpcShip destroyedShip) =>
            EvaluateDestruction(destroyedShip);

        public bool IsEligibleDestruction(NpcShip destroyedShip)
        {
            if (destroyedShip == null || !destroyedShip.IsDestroyed ||
                !destroyedShip.WasAliveImmediatelyBeforeDestruction)
            {
                return false;
            }

            string factionId = FactionManager.NormalizeFactionId(destroyedShip.FactionId);
            bool lawful = IsLawfulFaction(factionId);
            bool rogue = IsRogueFaction(factionId);
            bool trader = string.Equals(factionId, FactionManager.NeutralCivilians, StringComparison.OrdinalIgnoreCase) ||
                          destroyedShip.TrafficBehavior == TrafficZoneBehaviorType.TraderRoute;

            // Station traffic and unknown/non-combat ships do not create
            // salvage. Trader-route ships remain eligible for the existing
            // early-game cargo loop; combat factions are the Phase 38 core.
            return lawful || rogue || trader && destroyedShip.TrafficBehavior == TrafficZoneBehaviorType.TraderRoute;
        }

        public CombatSalvageTier DetermineTier(NpcShip destroyedShip)
        {
            if (!IsEligibleIdentity(destroyedShip))
            {
                return CombatSalvageTier.None;
            }

            string descriptor = $"{destroyedShip.Name} {destroyedShip.ModelPath}";
            return descriptor.IndexOf("warthog", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   descriptor.IndexOf("heavy", StringComparison.OrdinalIgnoreCase) >= 0
                ? CombatSalvageTier.Heavy
                : CombatSalvageTier.Standard;
        }

        public bool ShouldDrop(NpcShip destroyedShip)
        {
            CombatSalvageTier tier = DetermineTier(destroyedShip);
            return tier != CombatSalvageTier.None && ShouldDrop(destroyedShip, tier);
        }

        public bool ShouldDropEquipment(NpcShip destroyedShip)
        {
            CombatSalvageTier tier = DetermineTier(destroyedShip);
            return tier != CombatSalvageTier.None && ShouldDropEquipment(destroyedShip, tier);
        }

        public bool ShouldDropShield(NpcShip destroyedShip)
        {
            CombatSalvageTier tier = DetermineTier(destroyedShip);
            return tier != CombatSalvageTier.None && ShouldDropShield(destroyedShip, tier);
        }

        public bool ShouldDropConsumable(NpcShip destroyedShip, string consumableId)
        {
            CombatSalvageTier tier = DetermineTier(destroyedShip);
            return tier != CombatSalvageTier.None && ShouldDropConsumable(destroyedShip, tier, consumableId);
        }

        public bool ShouldDropPowerplant(NpcShip destroyedShip)
        {
            CombatSalvageTier tier = DetermineTier(destroyedShip);
            return tier != CombatSalvageTier.None && ShouldDropPowerplant(destroyedShip, tier);
        }

        public Vector3 GetSpawnOffset(NpcShip destroyedShip, int stackIndex)
        {
            CombatSalvageTier tier = DetermineTier(destroyedShip);
            uint hash = Hash(GetIdentityHash(destroyedShip, tier), $"spawn|{Math.Max(0, stackIndex)}");
            float distance = MinimumSpawnOffset + (hash % (uint)(MaximumSpawnOffset - MinimumSpawnOffset + 1f));
            float y = ((hash >> 8) % 101u) / 100f * 2f - 1f;
            double angle = ((hash >> 16) % 360u) * Math.PI / 180d;
            float radial = (float)Math.Sqrt(Math.Max(0d, 1d - y * y));
            return new Vector3(
                (float)(Math.Cos(angle) * radial) * distance,
                y * distance,
                (float)(Math.Sin(angle) * radial) * distance);
        }

        public void Reset()
        {
            _processedDestructions.Clear();
            _processedDestructionOrder.Clear();
        }

        private bool ShouldDrop(NpcShip destroyedShip, CombatSalvageTier tier)
        {
            uint roll = Hash(GetIdentityHash(destroyedShip, tier), "drop");
            // Stable modulo policy: standard fighters are 50%; heavy/Warthog
            // fighters are 75%. No wall-clock or frame-dependent randomness.
            return tier == CombatSalvageTier.Heavy ? roll % 4u != 0u : roll % 2u == 0u;
        }

        private bool ShouldDropEquipment(NpcShip destroyedShip, CombatSalvageTier tier)
        {
            uint roll = Hash(GetIdentityHash(destroyedShip, tier), "equipment-drop");
            // Equipment uses an independent deterministic roll: 25% for
            // standard fighters and 50% for heavy/Warthog fighters.
            return tier == CombatSalvageTier.Heavy ? roll % 2u == 0u : roll % 4u == 0u;
        }

        private bool ShouldDropShield(NpcShip destroyedShip, CombatSalvageTier tier)
        {
            if (destroyedShip?.Loadout?.GetMountedShield() == null)
            {
                return false;
            }

            uint roll = Hash(GetIdentityHash(destroyedShip, tier), "shield-drop");
            // Independent bounded policy: 20% standard, 33% heavy.
            return tier == CombatSalvageTier.Heavy ? roll % 3u == 0u : roll % 5u == 0u;
        }

        private bool ShouldDropPowerplant(NpcShip destroyedShip, CombatSalvageTier tier)
        {
            PowerplantEquipmentDefinition powerplant = destroyedShip?.Loadout?.GetMountedPowerplant();
            if (powerplant == null || !powerplant.IsValid || EquipmentCatalog.GetById(powerplant.Id) != powerplant)
            {
                return false;
            }

            uint roll = Hash(GetIdentityHash(destroyedShip, tier), "powerplant-drop");
            // Independent conservative policy: 20% standard and 30% heavy.
            return tier == CombatSalvageTier.Heavy ? roll % 10u < 3u : roll % 10u < 2u;
        }

        private bool ShouldDropConsumable(NpcShip destroyedShip, CombatSalvageTier tier, string consumableId)
        {
            if (destroyedShip?.Loadout == null ||
                !string.Equals(consumableId, CombatConsumableIds.Nanobots, StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(consumableId, CombatConsumableIds.ShieldBatteries, StringComparison.OrdinalIgnoreCase) ||
                destroyedShip.Loadout.GetOwnedCount(consumableId) <= 0)
            {
                return false;
            }

            uint roll = Hash(GetIdentityHash(destroyedShip, tier), $"consumable-drop|{consumableId}");
            return tier == CombatSalvageTier.Heavy ? roll % 3u != 0u : roll % 2u == 0u;
        }

        private string[] GetCommodityPool(NpcShip destroyedShip)
        {
            string factionId = FactionManager.NormalizeFactionId(destroyedShip.FactionId);
            if (IsRogueFaction(factionId))
            {
                return RogueCommodityPool;
            }

            if (IsLawfulFaction(factionId))
            {
                return LawfulCommodityPool;
            }

            return TraderCommodityPool;
        }

        private bool RememberDestruction(NpcShip destroyedShip)
        {
            if (!_processedDestructions.Add(destroyedShip))
            {
                return false;
            }

            _processedDestructionOrder.Enqueue(destroyedShip);
            while (_processedDestructionOrder.Count > MaximumRememberedDestructions)
            {
                _processedDestructions.Remove(_processedDestructionOrder.Dequeue());
            }

            return true;
        }

        private static bool IsEligibleIdentity(NpcShip ship) => ship != null;

        private static bool IsRogueFaction(string factionId)
        {
            return factionId.IndexOf("rogue", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   factionId.IndexOf("pirate", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool IsLawfulFaction(string factionId)
        {
            return string.Equals(factionId, FactionManager.LibertyPolice, StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(factionId, FactionManager.LibertyNavy, StringComparison.OrdinalIgnoreCase);
        }

        private static uint GetIdentityHash(NpcShip ship, CombatSalvageTier tier)
        {
            string identity = string.Join("|",
                FactionManager.NormalizeFactionId(ship?.FactionId),
                ship?.Name ?? string.Empty,
                ship?.ModelPath ?? string.Empty,
                ship?.TrafficBehavior.ToString() ?? string.Empty,
                tier.ToString());
            return StableHash(identity);
        }

        private static uint Hash(uint seed, string suffix)
        {
            return StableHash($"{seed:X8}|{suffix}");
        }

        private static uint StableHash(string value)
        {
            unchecked
            {
                uint hash = 2166136261u;
                for (int i = 0; i < (value?.Length ?? 0); i++)
                {
                    hash ^= value[i];
                    hash *= 16777619u;
                }

                return hash;
            }
        }
    }
}
