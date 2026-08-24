using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;

namespace Roguelancer
{
    /// <summary>
    /// Small authored progression tiers used by NPC loadout policy. This is
    /// intentionally not a second difficulty system; callers map their
    /// existing encounter or mission signal into one of these bounded tiers.
    /// </summary>
    public enum NpcLoadoutTier
    {
        Low,
        Standard,
        High
    }

    /// <summary>
    /// Data-only faction preference for canonical NPC guns.
    /// </summary>
    public sealed class NpcLoadoutPolicyProfile
    {
        internal NpcLoadoutPolicyProfile(
            string factionId,
            bool isCivilian,
            bool preferMatchedWeapons,
            params string[] preferredWeaponIds)
        {
            FactionId = factionId ?? string.Empty;
            IsCivilian = isCivilian;
            PreferMatchedWeapons = preferMatchedWeapons;
            PreferredWeaponIds = Array.AsReadOnly(preferredWeaponIds ?? Array.Empty<string>());
        }

        public string FactionId { get; }
        public bool IsCivilian { get; }
        public bool PreferMatchedWeapons { get; }
        public IReadOnlyList<string> PreferredWeaponIds { get; }
    }

    /// <summary>
    /// Central deterministic policy for durable NPC weapon snapshots.
    /// Profiles contain only canonical catalog IDs. The resulting snapshot is
    /// also the source consumed by equipment salvage when the NPC is destroyed.
    /// </summary>
    public static class NpcEquipmentLoadoutFactory
    {
        private static readonly NpcLoadoutPolicyProfile FallbackProfile =
            new NpcLoadoutPolicyProfile(
                "fallback",
                isCivilian: false,
                preferMatchedWeapons: false,
                "liberty_light_laser",
                "rogue_blaster");

        private static readonly Dictionary<string, NpcLoadoutPolicyProfile> Profiles =
            new Dictionary<string, NpcLoadoutPolicyProfile>(StringComparer.OrdinalIgnoreCase)
            {
                [FactionManager.LibertyPolice] = new NpcLoadoutPolicyProfile(
                    FactionManager.LibertyPolice,
                    isCivilian: false,
                    preferMatchedWeapons: true,
                    "liberty_pulse_cannon",
                    "liberty_light_laser"),
                [FactionManager.LibertyNavy] = new NpcLoadoutPolicyProfile(
                    FactionManager.LibertyNavy,
                    isCivilian: false,
                    preferMatchedWeapons: true,
                    "liberty_pulse_cannon",
                    "liberty_light_laser"),
                [FactionManager.LibertyRogues] = new NpcLoadoutPolicyProfile(
                    FactionManager.LibertyRogues,
                    isCivilian: false,
                    preferMatchedWeapons: false,
                    "rogue_blaster",
                    "liberty_light_laser"),
                [FactionManager.LibertyCorporations] = new NpcLoadoutPolicyProfile(
                    FactionManager.LibertyCorporations,
                    isCivilian: false,
                    preferMatchedWeapons: true,
                    "liberty_pulse_cannon",
                    "liberty_light_laser"),
                [FactionManager.BountyHunters] = new NpcLoadoutPolicyProfile(
                    FactionManager.BountyHunters,
                    isCivilian: false,
                    preferMatchedWeapons: true,
                    "liberty_light_laser",
                    "liberty_pulse_cannon"),
                [FactionManager.Junkers] = new NpcLoadoutPolicyProfile(
                    FactionManager.Junkers,
                    isCivilian: false,
                    preferMatchedWeapons: false,
                    "rogue_blaster",
                    "liberty_light_laser"),
                [FactionManager.NeutralCivilians] = new NpcLoadoutPolicyProfile(
                    FactionManager.NeutralCivilians,
                    isCivilian: true,
                    preferMatchedWeapons: true,
                    "liberty_light_laser")
            };

        /// <summary>
        /// Preserves the Phase 39 call shape for existing callers. Ambient
        /// patrols use the standard tier and their existing archetype text.
        /// </summary>
        public static ShipLoadout CreateForNpc(string archetypeName, string factionId, string modelPath = null)
        {
            return CreateForNpc(
                archetypeName,
                factionId,
                modelPath,
                TrafficZoneBehaviorType.LawfulPatrol,
                NpcLoadoutTier.Standard);
        }

        public static ShipLoadout CreateForNpc(
            string archetypeName,
            string factionId,
            string modelPath,
            TrafficZoneBehaviorType combatRole,
            NpcLoadoutTier tier = NpcLoadoutTier.Standard)
        {
            ShipLoadout loadout = ShipLoadout.CreateStarterLoadout(false);
            NpcLoadoutPolicyProfile profile = GetProfileForFaction(factionId);
            bool isFighter = IsFighter(archetypeName, modelPath);

            // Existing traffic behavior is the mature role signal. Trader and
            // station traffic remain unarmed unless their archetype explicitly
            // identifies a fighter, in which case they get one defensive gun.
            bool combatCapable = combatRole == TrafficZoneBehaviorType.PirateAmbush
                ? isFighter || !profile.IsCivilian
                : isFighter;
            if (!combatCapable)
            {
                return loadout;
            }

            int hardpointCapacity = GetGunHardpointCapacity(loadout);
            int weaponCount = GetWeaponCount(
                archetypeName,
                modelPath,
                combatRole,
                tier,
                hardpointCapacity);
            if (weaponCount <= 0)
            {
                return loadout;
            }

            List<WeaponEquipmentDefinition> eligibleWeapons = GetEligibleWeapons(profile, loadout);
            if (eligibleWeapons.Count == 0)
            {
                eligibleWeapons = GetEligibleWeapons(FallbackProfile, loadout);
            }

            if (eligibleWeapons.Count == 0)
            {
                return loadout;
            }

            string identity = BuildIdentity(archetypeName, factionId, modelPath, combatRole, tier);
            int primaryIndex = profile.PreferMatchedWeapons
                ? tier == NpcLoadoutTier.Low ? eligibleWeapons.Count - 1 : 0
                : SelectMixedPrimaryIndex(identity, eligibleWeapons.Count);

            for (int slot = 0; slot < weaponCount; slot++)
            {
                int weaponIndex = profile.PreferMatchedWeapons
                    ? primaryIndex
                    : (primaryIndex + slot) % eligibleWeapons.Count;
                WeaponEquipmentDefinition weapon = eligibleWeapons[weaponIndex];
                if (!TryAddAndMount(loadout, weapon))
                {
                    // A malformed catalog entry or an unexpected hardpoint
                    // layout can never produce an invalid mounted snapshot.
                    continue;
                }
            }

            return loadout;
        }

        public static NpcLoadoutPolicyProfile GetProfileForFaction(string factionId)
        {
            string normalized = FactionManager.NormalizeFactionId(factionId);
            return Profiles.TryGetValue(normalized, out NpcLoadoutPolicyProfile profile)
                ? profile
                : FallbackProfile;
        }

        public static bool IsValidNpcWeapon(EquipmentDefinition equipment)
        {
            if (equipment is not WeaponEquipmentDefinition weapon ||
                weapon.EquipmentType != EquipmentType.Gun ||
                !Enum.IsDefined(typeof(WeaponType), weapon.WeaponType))
            {
                return false;
            }

            return IsValidPositive(weapon.Damage) &&
                   IsValidPositive(weapon.ProjectileSpeed) &&
                   IsValidPositive(weapon.RefireRate) &&
                   IsValidPositive(weapon.EnergyCost) &&
                   IsValidPositive(weapon.Range);
        }

        /// <summary>
        /// Uses the shortest valid mounted-gun range so every gun in a volley
        /// can reach the target. The result is bounded for safe combat use.
        /// </summary>
        public static float GetFiringRange(ShipLoadout loadout)
        {
            float range = float.MaxValue;
            foreach (WeaponEquipmentDefinition weapon in loadout?.GetMountedGuns() ?? Array.Empty<WeaponEquipmentDefinition>())
            {
                EquipmentDefinition canonical = EquipmentCatalog.GetById(weapon?.Id);
                if (canonical is not WeaponEquipmentDefinition canonicalWeapon || !IsValidNpcWeapon(canonicalWeapon))
                {
                    continue;
                }

                range = Math.Min(range, canonicalWeapon.Range);
            }

            return range == float.MaxValue ? 0f : MathHelper.Clamp(range, 600f, 12000f);
        }

        public static float GetRefireRate(ShipLoadout loadout)
        {
            float refireRate = float.MaxValue;
            foreach (WeaponEquipmentDefinition weapon in loadout?.GetMountedGuns() ?? Array.Empty<WeaponEquipmentDefinition>())
            {
                EquipmentDefinition canonical = EquipmentCatalog.GetById(weapon?.Id);
                if (canonical is not WeaponEquipmentDefinition canonicalWeapon || !IsValidNpcWeapon(canonicalWeapon))
                {
                    continue;
                }

                refireRate = Math.Min(refireRate, canonicalWeapon.RefireRate);
            }

            return refireRate == float.MaxValue ? 0f : MathHelper.Clamp(refireRate, 0.08f, 5f);
        }

        private static List<WeaponEquipmentDefinition> GetEligibleWeapons(
            NpcLoadoutPolicyProfile profile,
            ShipLoadout loadout)
        {
            List<WeaponEquipmentDefinition> eligible = new();
            foreach (string weaponId in profile?.PreferredWeaponIds ?? Array.Empty<string>())
            {
                EquipmentDefinition definition = EquipmentCatalog.GetById(weaponId);
                if (!IsValidNpcWeapon(definition) ||
                    !loadout.GetCompatibleHardpoints(definition).Any(hardpoint => hardpoint.IsEmpty) ||
                    eligible.Any(existing => string.Equals(existing.Id, definition.Id, StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                eligible.Add((WeaponEquipmentDefinition)definition);
            }

            return eligible;
        }

        private static bool TryAddAndMount(ShipLoadout loadout, WeaponEquipmentDefinition weapon)
        {
            if (!IsValidNpcWeapon(weapon) || !loadout.AddOwnedEquipment(weapon, 1))
            {
                return false;
            }

            if (loadout.TryMountEquipment(weapon, out _))
            {
                return true;
            }

            // The item was never mounted, so it is still sellable/owned and
            // can be safely removed from this transient NPC snapshot.
            loadout.RemoveOwnedEquipment(weapon.Id, 1);
            return false;
        }

        private static int GetWeaponCount(
            string archetypeName,
            string modelPath,
            TrafficZoneBehaviorType combatRole,
            NpcLoadoutTier tier,
            int hardpointCapacity)
        {
            if (hardpointCapacity <= 0)
            {
                return 0;
            }

            bool heavy = IsHeavy(archetypeName, modelPath);
            int count = heavy ? hardpointCapacity : Math.Min(2, hardpointCapacity);

            if (tier == NpcLoadoutTier.Low)
            {
                count = heavy ? Math.Min(2, hardpointCapacity) : 1;
            }

            if (combatRole == TrafficZoneBehaviorType.TraderRoute ||
                combatRole == TrafficZoneBehaviorType.StationTraffic)
            {
                count = Math.Min(1, count);
            }

            return count;
        }

        private static int GetGunHardpointCapacity(ShipLoadout loadout)
        {
            return loadout?.Hardpoints.Count(hardpoint =>
                hardpoint?.AllowedEquipmentTypes?.Contains(EquipmentType.Gun) == true) ?? 0;
        }

        private static int SelectMixedPrimaryIndex(string identity, int weaponCount)
        {
            if (weaponCount <= 1)
            {
                return 0;
            }

            return Hash(identity, "mixed-primary") % 4u == 0u ? 1 : 0;
        }

        private static string BuildIdentity(
            string archetypeName,
            string factionId,
            string modelPath,
            TrafficZoneBehaviorType combatRole,
            NpcLoadoutTier tier)
        {
            return string.Join(
                "|",
                archetypeName ?? string.Empty,
                FactionManager.NormalizeFactionId(factionId),
                modelPath ?? string.Empty,
                combatRole,
                tier);
        }

        private static bool IsFighter(string archetypeName, string modelPath)
        {
            string descriptor = $"{archetypeName} {modelPath}";
            return descriptor.IndexOf("fighter", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   descriptor.IndexOf("warthog", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   descriptor.IndexOf("heavy", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool IsHeavy(string archetypeName, string modelPath)
        {
            string descriptor = $"{archetypeName} {modelPath}";
            return descriptor.IndexOf("warthog", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   descriptor.IndexOf("heavy", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool IsValidPositive(float value) =>
            !float.IsNaN(value) && !float.IsInfinity(value) && value > 0f;

        private static uint Hash(string value, string salt)
        {
            unchecked
            {
                uint hash = 2166136261u;
                string input = $"{value ?? string.Empty}|{salt ?? string.Empty}";
                foreach (char character in input)
                {
                    hash ^= character;
                    hash *= 16777619u;
                }

                return hash;
            }
        }
    }
}
