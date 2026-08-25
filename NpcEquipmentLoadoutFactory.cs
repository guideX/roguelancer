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
                    "liberty_light_laser",
                    "liberty_sentry_blaster",
                    "liberty_ranger_laser",
                    "liberty_pulse_cannon",
                    "liberty_heavy_pulse"),
                [FactionManager.LibertyNavy] = new NpcLoadoutPolicyProfile(
                    FactionManager.LibertyNavy,
                    isCivilian: false,
                    preferMatchedWeapons: true,
                    "liberty_sentry_blaster",
                    "liberty_pulse_cannon",
                    "liberty_ranger_laser",
                    "liberty_heavy_pulse"),
                [FactionManager.LibertyRogues] = new NpcLoadoutPolicyProfile(
                    FactionManager.LibertyRogues,
                    isCivilian: false,
                    preferMatchedWeapons: false,
                    "rogue_needle_blaster",
                    "rogue_blaster",
                    "rogue_scattergun",
                    "rogue_rail_cannon"),
                [FactionManager.LibertyCorporations] = new NpcLoadoutPolicyProfile(
                    FactionManager.LibertyCorporations,
                    isCivilian: false,
                    preferMatchedWeapons: true,
                    "liberty_light_laser",
                    "liberty_sentry_blaster",
                    "liberty_pulse_cannon",
                    "liberty_ranger_laser",
                    "liberty_heavy_pulse"),
                [FactionManager.BountyHunters] = new NpcLoadoutPolicyProfile(
                    FactionManager.BountyHunters,
                    isCivilian: false,
                    preferMatchedWeapons: false,
                    "liberty_light_laser",
                    "rogue_blaster",
                    "liberty_pulse_cannon",
                    "rogue_needle_blaster",
                    "liberty_ranger_laser",
                    "rogue_scattergun",
                    "liberty_heavy_pulse",
                    "rogue_rail_cannon"),
                [FactionManager.Junkers] = new NpcLoadoutPolicyProfile(
                    FactionManager.Junkers,
                    isCivilian: false,
                    preferMatchedWeapons: false,
                    "rogue_blaster",
                    "liberty_light_laser",
                    "liberty_sentry_blaster",
                    "rogue_needle_blaster",
                    "rogue_scattergun"),
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
            bool heavy = IsHeavy(archetypeName, modelPath);
            string identity = BuildIdentity(archetypeName, factionId, modelPath, combatRole, tier);

            // Every normal NPC receives one deterministic thruster and plant
            // before its weapons are selected, so movement and weapon budgets
            // are authored as one coherent loadout snapshot.
            ThrusterEquipmentDefinition thruster = SelectThruster(profile, tier, heavy, combatRole);
            TryAddAndMountThruster(loadout, thruster);

            PowerplantEquipmentDefinition powerplant = SelectPowerplant(profile, tier, heavy, combatRole);
            TryAddAndMountPowerplant(loadout, powerplant);

            // Defensive equipment is independent from offensive capability:
            // civilian and trader traffic may receive a modest shield while
            // remaining completely unarmed.
            List<ShieldEquipmentDefinition> eligibleShields = GetEligibleShields(
                profile,
                loadout,
                tier);
            if (eligibleShields.Count > 0)
            {
                int shieldIndex = SelectShieldIndex(eligibleShields, combatRole, tier, heavy);
                TryAddAndMountShield(loadout, eligibleShields[shieldIndex]);
            }

            AddCombatConsumables(loadout, profile, archetypeName, modelPath, combatRole, tier, heavy);

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

            List<WeaponEquipmentDefinition> eligibleWeapons = GetEligibleWeapons(profile, loadout, tier);
            if (eligibleWeapons.Count == 0)
            {
                eligibleWeapons = GetEligibleWeapons(FallbackProfile, loadout, tier);
            }

            if (eligibleWeapons.Count == 0)
            {
                return loadout;
            }

            int primaryIndex = SelectPrimaryIndex(
                profile,
                eligibleWeapons,
                identity,
                combatRole,
                tier);

            for (int slot = 0; slot < weaponCount; slot++)
            {
                int weaponIndex = profile.PreferMatchedWeapons || tier == NpcLoadoutTier.High
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
                   IsValidPositive(weapon.Range) &&
                   Enum.IsDefined(typeof(WeaponFamily), weapon.Family) &&
                   Enum.IsDefined(typeof(WeaponProgressionTier), weapon.ProgressionTier);
        }

        public static bool IsValidNpcShield(EquipmentDefinition equipment)
        {
            return equipment is ShieldEquipmentDefinition shield && shield.IsValid &&
                   EquipmentCatalog.GetById(shield.Id) == shield;
        }

        public static bool IsValidNpcPowerplant(EquipmentDefinition equipment)
        {
            return equipment is PowerplantEquipmentDefinition powerplant && powerplant.IsValid &&
                   EquipmentCatalog.GetById(powerplant.Id) == powerplant;
        }

        public static bool IsValidNpcThruster(EquipmentDefinition equipment)
        {
            return equipment is ThrusterEquipmentDefinition thruster && thruster.IsValid &&
                   EquipmentCatalog.GetById(thruster.Id) == thruster;
        }

        public static ThrusterEquipmentDefinition GetNpcThruster(ShipLoadout loadout)
        {
            ThrusterEquipmentDefinition thruster = loadout?.GetMountedThruster();
            return IsValidNpcThruster(thruster) ? thruster : null;
        }

        public static PowerplantEquipmentDefinition GetNpcPowerplant(ShipLoadout loadout)
        {
            PowerplantEquipmentDefinition powerplant = loadout?.GetMountedPowerplant();
            return IsValidNpcPowerplant(powerplant) ? powerplant : null;
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
            ShipLoadout loadout,
            NpcLoadoutTier tier)
        {
            List<WeaponEquipmentDefinition> eligible = new();
            foreach (string weaponId in profile?.PreferredWeaponIds ?? Array.Empty<string>())
            {
                EquipmentDefinition definition = EquipmentCatalog.GetById(weaponId);
                if (!IsValidNpcWeapon(definition) ||
                    !IsAvailableAtTier((WeaponEquipmentDefinition)definition, tier) ||
                    !loadout.GetCompatibleHardpoints(definition).Any(hardpoint => hardpoint.IsEmpty) ||
                    eligible.Any(existing => string.Equals(existing.Id, definition.Id, StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                eligible.Add((WeaponEquipmentDefinition)definition);
            }

            return eligible;
        }

        private static List<ShieldEquipmentDefinition> GetEligibleShields(
            NpcLoadoutPolicyProfile profile,
            ShipLoadout loadout,
            NpcLoadoutTier tier)
        {
            List<ShieldEquipmentDefinition> eligible = new();
            foreach (string shieldId in GetPreferredShieldIds(profile?.FactionId))
            {
                EquipmentDefinition definition = EquipmentCatalog.GetById(shieldId);
                if (!IsValidNpcShield(definition) ||
                    !IsShieldAvailableAtTier((ShieldEquipmentDefinition)definition, tier) ||
                    !loadout.GetCompatibleHardpoints(definition).Any())
                {
                    continue;
                }

                eligible.Add((ShieldEquipmentDefinition)definition);
            }

            return eligible;
        }

        private static IReadOnlyList<string> GetPreferredShieldIds(string factionId)
        {
            string normalized = FactionManager.NormalizeFactionId(factionId);
            if (string.Equals(normalized, FactionManager.LibertyPolice, StringComparison.OrdinalIgnoreCase))
                return new[] { "civilian_shield_generator", "liberty_patrol_shield", "liberty_military_shield", "liberty_heavy_shield" };
            if (string.Equals(normalized, FactionManager.LibertyNavy, StringComparison.OrdinalIgnoreCase))
                return new[] { "liberty_patrol_shield", "professional_deflector", "liberty_military_shield", "liberty_heavy_shield" };
            if (string.Equals(normalized, FactionManager.LibertyRogues, StringComparison.OrdinalIgnoreCase))
                return new[] { "rogue_scrap_shield", "rogue_combat_shield" };
            if (string.Equals(normalized, FactionManager.LibertyCorporations, StringComparison.OrdinalIgnoreCase))
                return new[] { "civilian_shield_generator", "liberty_patrol_shield", "professional_deflector" };
            if (string.Equals(normalized, FactionManager.BountyHunters, StringComparison.OrdinalIgnoreCase))
                return new[] { "professional_deflector", "liberty_patrol_shield", "liberty_military_shield", "rogue_combat_shield" };
            if (string.Equals(normalized, FactionManager.Junkers, StringComparison.OrdinalIgnoreCase))
                return new[] { "rogue_scrap_shield", "civilian_shield_generator", "liberty_patrol_shield" };
            if (string.Equals(normalized, FactionManager.NeutralCivilians, StringComparison.OrdinalIgnoreCase))
                return new[] { "civilian_shield_generator" };

            return new[] { "civilian_shield_generator" };
        }

        private static bool IsShieldAvailableAtTier(ShieldEquipmentDefinition shield, NpcLoadoutTier tier)
        {
            if (shield == null)
                return false;

            return shield.ProgressionTier switch
            {
                ShieldProgressionTier.Low => true,
                ShieldProgressionTier.Standard => tier != NpcLoadoutTier.Low,
                ShieldProgressionTier.High => tier == NpcLoadoutTier.High,
                _ => false
            };
        }

        private static int SelectShieldIndex(
            IReadOnlyList<ShieldEquipmentDefinition> eligible,
            TrafficZoneBehaviorType combatRole,
            NpcLoadoutTier tier,
            bool heavy)
        {
            if (eligible == null || eligible.Count <= 1 || tier == NpcLoadoutTier.Low)
                return 0;

            bool supportRole = combatRole == TrafficZoneBehaviorType.TraderRoute ||
                               combatRole == TrafficZoneBehaviorType.StationTraffic;
            if (supportRole)
                return 0;

            int preferredIndex = tier == NpcLoadoutTier.High
                ? eligible.Count - 1
                : Math.Min(heavy ? 2 : 1, eligible.Count - 1);

            if (tier == NpcLoadoutTier.Standard)
            {
                for (int i = 0; i < eligible.Count; i++)
                {
                    if (eligible[i].Family == ShieldFamily.Professional)
                    {
                        return i;
                    }
                }
            }

            return preferredIndex;
        }

        private static bool TryAddAndMountShield(ShipLoadout loadout, ShieldEquipmentDefinition shield)
        {
            if (!IsValidNpcShield(shield) || !loadout.AddOwnedEquipment(shield, 1))
                return false;

            if (loadout.TryMountEquipment(shield, out _))
                return true;

            loadout.RemoveOwnedEquipment(shield.Id, 1);
            return false;
        }

        private static PowerplantEquipmentDefinition SelectPowerplant(
            NpcLoadoutPolicyProfile profile,
            NpcLoadoutTier tier,
            bool heavy,
            TrafficZoneBehaviorType combatRole)
        {
            string factionId = FactionManager.NormalizeFactionId(profile?.FactionId);
            bool supportRole = combatRole == TrafficZoneBehaviorType.TraderRoute ||
                               combatRole == TrafficZoneBehaviorType.StationTraffic;

            string id;
            if (profile?.IsCivilian == true || supportRole && string.Equals(factionId, FactionManager.NeutralCivilians, StringComparison.OrdinalIgnoreCase))
            {
                id = "civilian_powerplant";
            }
            else if (string.Equals(factionId, FactionManager.LibertyPolice, StringComparison.OrdinalIgnoreCase))
            {
                id = tier == NpcLoadoutTier.High || heavy
                    ? "liberty_heavy_powerplant"
                    : "liberty_patrol_powerplant";
            }
            else if (string.Equals(factionId, FactionManager.LibertyNavy, StringComparison.OrdinalIgnoreCase))
            {
                id = tier == NpcLoadoutTier.High || heavy
                    ? "liberty_heavy_powerplant"
                    : "liberty_military_powerplant";
            }
            else if (string.Equals(factionId, FactionManager.LibertyRogues, StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(factionId, FactionManager.Junkers, StringComparison.OrdinalIgnoreCase))
            {
                id = tier == NpcLoadoutTier.Low && !heavy
                    ? "rogue_scrap_powerplant"
                    : "rogue_combat_powerplant";
            }
            else if (string.Equals(factionId, FactionManager.LibertyCorporations, StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(factionId, FactionManager.BountyHunters, StringComparison.OrdinalIgnoreCase))
            {
                id = tier == NpcLoadoutTier.Low && !heavy
                    ? "liberty_patrol_powerplant"
                    : "professional_powerplant";
            }
            else
            {
                id = "civilian_powerplant";
            }

            return EquipmentCatalog.GetById(id) as PowerplantEquipmentDefinition ??
                   EquipmentCatalog.GetFallbackForType(EquipmentType.Powerplant) as PowerplantEquipmentDefinition;
        }

        private static ThrusterEquipmentDefinition SelectThruster(
            NpcLoadoutPolicyProfile profile,
            NpcLoadoutTier tier,
            bool heavy,
            TrafficZoneBehaviorType combatRole)
        {
            string factionId = FactionManager.NormalizeFactionId(profile?.FactionId);
            bool supportRole = combatRole == TrafficZoneBehaviorType.TraderRoute ||
                               combatRole == TrafficZoneBehaviorType.StationTraffic;

            string id;
            if (profile?.IsCivilian == true || supportRole && string.Equals(factionId, FactionManager.NeutralCivilians, StringComparison.OrdinalIgnoreCase))
            {
                id = "civilian_thruster";
            }
            else if (string.Equals(factionId, FactionManager.LibertyPolice, StringComparison.OrdinalIgnoreCase))
            {
                id = tier == NpcLoadoutTier.High || heavy
                    ? "liberty_heavy_thruster"
                    : "liberty_patrol_thruster";
            }
            else if (string.Equals(factionId, FactionManager.LibertyNavy, StringComparison.OrdinalIgnoreCase))
            {
                id = tier == NpcLoadoutTier.High || heavy
                    ? "liberty_heavy_thruster"
                    : "liberty_military_thruster";
            }
            else if (string.Equals(factionId, FactionManager.LibertyRogues, StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(factionId, FactionManager.Junkers, StringComparison.OrdinalIgnoreCase))
            {
                id = tier == NpcLoadoutTier.Low && !heavy
                    ? "rogue_scrap_thruster"
                    : "rogue_combat_thruster";
            }
            else if (string.Equals(factionId, FactionManager.LibertyCorporations, StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(factionId, FactionManager.BountyHunters, StringComparison.OrdinalIgnoreCase))
            {
                id = tier == NpcLoadoutTier.Low && !heavy
                    ? "liberty_patrol_thruster"
                    : "professional_thruster";
            }
            else
            {
                id = "civilian_thruster";
            }

            return EquipmentCatalog.GetById(id) as ThrusterEquipmentDefinition ??
                   EquipmentCatalog.GetFallbackForType(EquipmentType.Thruster) as ThrusterEquipmentDefinition;
        }

        private static bool TryAddAndMountThruster(ShipLoadout loadout, ThrusterEquipmentDefinition thruster)
        {
            if (!IsValidNpcThruster(thruster) || !loadout.AddOwnedEquipment(thruster, 1))
            {
                return false;
            }

            if (loadout.TryMountEquipment(thruster, out _))
            {
                return true;
            }

            loadout.RemoveOwnedEquipment(thruster.Id, 1);
            return false;
        }

        private static bool TryAddAndMountPowerplant(ShipLoadout loadout, PowerplantEquipmentDefinition powerplant)
        {
            if (!IsValidNpcPowerplant(powerplant) || !loadout.AddOwnedEquipment(powerplant, 1))
            {
                return false;
            }

            if (loadout.TryMountEquipment(powerplant, out _))
            {
                return true;
            }

            loadout.RemoveOwnedEquipment(powerplant.Id, 1);
            return false;
        }

        private static void AddCombatConsumables(
            ShipLoadout loadout,
            NpcLoadoutPolicyProfile profile,
            string archetypeName,
            string modelPath,
            TrafficZoneBehaviorType combatRole,
            NpcLoadoutTier tier,
            bool heavy)
        {
            if (loadout == null)
            {
                return;
            }

            string factionId = FactionManager.NormalizeFactionId(profile?.FactionId);
            int nanobots;
            int batteries;
            if (profile?.IsCivilian == true || combatRole == TrafficZoneBehaviorType.TraderRoute)
            {
                nanobots = 1;
                batteries = 1;
            }
            else if (string.Equals(factionId, FactionManager.LibertyNavy, StringComparison.OrdinalIgnoreCase))
            {
                nanobots = 3;
                batteries = 3;
            }
            else if (string.Equals(factionId, FactionManager.LibertyPolice, StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(factionId, FactionManager.LibertyCorporations, StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(factionId, FactionManager.BountyHunters, StringComparison.OrdinalIgnoreCase))
            {
                nanobots = 2;
                batteries = 2;
            }
            else
            {
                nanobots = 1;
                batteries = 1;
            }

            if (tier == NpcLoadoutTier.High)
            {
                nanobots++;
                batteries++;
            }

            if (heavy || IsHeavy(archetypeName, modelPath))
            {
                nanobots++;
                batteries++;
            }

            if (string.Equals(factionId, FactionManager.LibertyRogues, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(factionId, FactionManager.Junkers, StringComparison.OrdinalIgnoreCase))
            {
                nanobots = Math.Max(1, nanobots - 1);
                batteries = Math.Max(1, batteries - 1);
            }

            ConsumableEquipmentDefinition nanobotDefinition = EquipmentCatalog.GetById(CombatConsumableIds.Nanobots) as ConsumableEquipmentDefinition;
            ConsumableEquipmentDefinition batteryDefinition = EquipmentCatalog.GetById(CombatConsumableIds.ShieldBatteries) as ConsumableEquipmentDefinition;
            loadout.AddOwnedEquipment(nanobotDefinition, Math.Min(CombatConsumableInventory.MaximumNanobots, nanobots));
            loadout.AddOwnedEquipment(batteryDefinition, Math.Min(CombatConsumableInventory.MaximumShieldBatteries, batteries));
        }

        private static bool IsAvailableAtTier(WeaponEquipmentDefinition weapon, NpcLoadoutTier tier)
        {
            if (weapon == null)
            {
                return false;
            }

            return weapon.ProgressionTier switch
            {
                WeaponProgressionTier.Low => true,
                WeaponProgressionTier.Standard => tier != NpcLoadoutTier.Low,
                WeaponProgressionTier.High => tier == NpcLoadoutTier.High,
                _ => false
            };
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

        private static int SelectPrimaryIndex(
            NpcLoadoutPolicyProfile profile,
            IReadOnlyList<WeaponEquipmentDefinition> eligibleWeapons,
            string identity,
            TrafficZoneBehaviorType combatRole,
            NpcLoadoutTier tier)
        {
            if (eligibleWeapons == null || eligibleWeapons.Count <= 1)
            {
                return 0;
            }

            bool supportRole = combatRole == TrafficZoneBehaviorType.TraderRoute ||
                               combatRole == TrafficZoneBehaviorType.StationTraffic;
            if (supportRole && tier != NpcLoadoutTier.High)
            {
                return 0;
            }

            // Matched professional factions use the strongest item permitted
            // by the canonical tier. High-tier mixed factions also guarantee
            // access to their strongest bounded option, while standard rogue
            // identities preserve the Phase 40 deterministic mixed behavior.
            if (profile?.PreferMatchedWeapons == true || tier == NpcLoadoutTier.High)
            {
                return eligibleWeapons.Count - 1;
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
