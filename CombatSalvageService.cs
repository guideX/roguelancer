using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;

namespace Roguelancer
{
    public enum CombatSalvageTier
    {
        None,
        Standard,
        Heavy
    }

    /// <summary>
    /// Immutable policy output for one physical commodity salvage object.
    /// The object is not cargo until a player collects it through CargoHold.
    /// </summary>
    public sealed class SalvageDrop
    {
        public string CommodityId { get; }
        public int Quantity { get; }
        public int StackIndex { get; }
        public CombatSalvageTier Tier { get; }

        public SalvageDrop(string commodityId, int quantity, int stackIndex, CombatSalvageTier tier)
        {
            CommodityId = commodityId ?? string.Empty;
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
        public const int StandardMinimumQuantity = 1;
        public const int StandardMaximumQuantity = 3;
        public const int HeavyMinimumQuantity = 2;
        public const int HeavyMaximumQuantity = 5;

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
            if (tier == CombatSalvageTier.None || !ShouldDrop(destroyedShip, tier))
            {
                return Array.Empty<SalvageDrop>();
            }

            string[] pool = GetCommodityPool(destroyedShip);
            if (pool.Length == 0)
            {
                return Array.Empty<SalvageDrop>();
            }

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
                return new[]
                {
                    new SalvageDrop(
                        pool[(int)(Hash(identityHash, "commodity|0") % (uint)pool.Length)],
                        quantity,
                        0,
                        tier)
                };
            }

            int totalQuantity = HeavyMinimumQuantity + (int)(Hash(identityHash, "quantity") %
                (HeavyMaximumQuantity - HeavyMinimumQuantity + 1));
            int objectCount = Hash(identityHash, "objects") % 2u == 0u ? 1 : 2;
            if (objectCount == 1)
            {
                return new[]
                {
                    new SalvageDrop(
                        pool[(int)(Hash(identityHash, "commodity|0") % (uint)pool.Length)],
                        totalQuantity,
                        0,
                        tier)
                };
            }

            int firstQuantity = 1 + (int)(Hash(identityHash, "split") % (uint)(totalQuantity - 1));
            return new[]
            {
                new SalvageDrop(
                    pool[(int)(Hash(identityHash, "commodity|0") % (uint)pool.Length)],
                    firstQuantity,
                    0,
                    tier),
                new SalvageDrop(
                    pool[(int)(Hash(identityHash, "commodity|1") % (uint)pool.Length)],
                    totalQuantity - firstQuantity,
                    1,
                    tier)
            };
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
