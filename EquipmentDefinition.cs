using System;
using Microsoft.Xna.Framework;

namespace Roguelancer
{
    /// <summary>
    /// Small authored family labels used by NPC loadout policy. These are
    /// canonical weapon metadata, not a separate NPC item database.
    /// </summary>
    public enum WeaponFamily
    {
        Liberty,
        Rogue,
        Mixed
    }

    /// <summary>
    /// Bounded progression bands for canonical gun selection.
    /// </summary>
    public enum WeaponProgressionTier
    {
        Low,
        Standard,
        High
    }

    /// <summary>
    /// Bounded progression bands for canonical shield selection.
    /// </summary>
    public enum ShieldProgressionTier
    {
        Low,
        Standard,
        High
    }

    /// <summary>
    /// Authored shield family metadata used by deterministic NPC loadout policy.
    /// The combat runtime reads only the canonical shield stats below.
    /// </summary>
    public enum ShieldFamily
    {
        Liberty,
        Rogue,
        Professional,
        Mixed
    }

    /// <summary>
    /// Authored powerplant family metadata used by deterministic NPC loadout
    /// policy. Runtime charge belongs to WeaponEnergy, not this definition.
    /// </summary>
    public enum PowerplantFamily
    {
        Civilian,
        Liberty,
        Rogue,
        Professional
    }

    /// <summary>
    /// Bounded progression bands for canonical powerplant selection.
    /// </summary>
    public enum PowerplantProgressionTier
    {
        Low,
        Standard,
        High
    }

    public enum CombatConsumableType
    {
        None,
        Nanobots,
        ShieldBattery
    }

    /// <summary>
    /// Base definition for a piece of ship equipment.
    /// </summary>
    public class EquipmentDefinition
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public EquipmentType EquipmentType { get; set; }
        public int Price { get; set; }

        /// <summary>
        /// Optional standalone equipment model. Current Phase 9 catalog
        /// entries intentionally leave this empty because the repository has
        /// projectile effects and ship meshes, but no standalone gun/launcher
        /// art. The shared mounted-equipment renderer skips empty paths.
        /// </summary>
        public string VisualModelPath { get; set; } = string.Empty;

        /// <summary>Correction applied only to the optional equipment model.</summary>
        public Vector3 VisualRotationDegrees { get; set; } = Vector3.Zero;

        /// <summary>Scale correction for the optional equipment model.</summary>
        public float VisualModelScale { get; set; } = 1f;

        /// <summary>
        /// Missile damage for launcher equipment.
        /// </summary>
        public float MissileDamage { get; set; }

        /// <summary>
        /// Missile flight speed for launcher equipment.
        /// </summary>
        public float MissileSpeed { get; set; }

        /// <summary>
        /// Missile turn rate for launcher equipment.
        /// </summary>
        public float MissileTurnRate { get; set; }

        /// <summary>
        /// Missile lifetime for launcher equipment.
        /// </summary>
        public float MissileLifetime { get; set; }

        /// <summary>
        /// Countermeasure lifetime for countermeasure equipment.
        /// </summary>
        public float CountermeasureLife { get; set; }

        /// <summary>
        /// Countermeasure attraction radius for countermeasure equipment.
        /// </summary>
        public float CountermeasureAttractionRadius { get; set; }

        /// <summary>
        /// Countermeasure strength for countermeasure equipment.
        /// </summary>
        public float CountermeasureStrength { get; set; }

        /// <summary>
        /// Countermeasure cooldown for countermeasure equipment.
        /// </summary>
        public float CountermeasureCooldown { get; set; }

        /// <summary>
        /// Mine damage for mine dropper equipment.
        /// </summary>
        public float MineDamage { get; set; }

        /// <summary>
        /// Mine trigger radius for mine dropper equipment.
        /// </summary>
        public float MineTriggerRadius { get; set; }

        /// <summary>
        /// Mine blast radius for mine dropper equipment.
        /// </summary>
        public float MineBlastRadius { get; set; }

        /// <summary>
        /// Mine lifetime for mine dropper equipment.
        /// </summary>
        public float MineLifetime { get; set; }

        /// <summary>
        /// Mine cooldown for mine dropper equipment.
        /// </summary>
        public float MineCooldown { get; set; }

        /// <summary>
        /// Mine arm delay for mine dropper equipment.
        /// </summary>
        public float MineArmDelay { get; set; }

        /// <summary>
        /// Future-safe ammo cost per missile fired.
        /// </summary>
        public float MissileAmmoCost { get; set; }

        /// <summary>
        /// Optional gating for future progression systems.
        /// </summary>
        public int? RequiredLevel { get; set; }

        /// <summary>
        /// Optional faction reputation gate for future progression systems.
        /// </summary>
        public string RequiredReputationFactionId { get; set; } = string.Empty;

        /// <summary>
        /// Optional minimum reputation value for acquiring this equipment.
        /// The controlling faction is resolved from RequiredReputationFactionId
        /// when present, otherwise from the live dealer/station offer context.
        /// </summary>
        public int? RequiredReputation { get; set; }

        /// <summary>
        /// Fractional minimum standing for the live equipment access gate.
        /// This remains static definition metadata; ownership never rechecks it.
        /// </summary>
        public float? MinimumReputation { get; set; }

        /// <summary>
        /// Optional contraband flag for future law enforcement systems.
        /// </summary>
        public bool IsContraband { get; set; }

        public FactionReputationRequirement GetReputationRequirement(string controllingFactionId)
        {
            float? minimumStanding = MinimumReputation ??
                (RequiredReputation.HasValue ? RequiredReputation.Value : (float?)null);
            if (!minimumStanding.HasValue)
                return null;

            string factionId = FactionManager.CoalesceFactionId(
                RequiredReputationFactionId,
                controllingFactionId);
            return new FactionReputationRequirement(factionId, minimumStanding.Value);
        }

        public virtual string GetStatsSummary()
        {
            string requirement = string.Empty;
            if (RequiredLevel.HasValue)
            {
                requirement += $"Level {RequiredLevel.Value}";
            }

            float? minimumReputation = MinimumReputation ??
                (RequiredReputation.HasValue ? RequiredReputation.Value : (float?)null);
            if (!string.IsNullOrWhiteSpace(RequiredReputationFactionId) && minimumReputation.HasValue)
            {
                if (requirement.Length > 0)
                {
                    requirement += " | ";
                }

                requirement += $"Rep {RequiredReputationFactionId}:{ReputationManager.FormatStanding(minimumReputation.Value)}";
            }
            else if (minimumReputation.HasValue)
            {
                if (requirement.Length > 0)
                {
                    requirement += " | ";
                }

                requirement += $"Minimum standing {ReputationManager.FormatStanding(minimumReputation.Value)}";
            }

            if (string.IsNullOrWhiteSpace(requirement))
            {
                requirement = "No restrictions";
            }

            string missileStats = string.Empty;
            if (MissileDamage > 0f || MissileSpeed > 0f || MissileTurnRate > 0f || MissileLifetime > 0f || MissileAmmoCost > 0f)
            {
                missileStats = $" | MISSILE DMG {MissileDamage:F0} | SPD {MissileSpeed:F0} | TURN {MissileTurnRate:F2} | LIFE {MissileLifetime:F1}s | AMMO {MissileAmmoCost:F0}";
            }

            string countermeasureStats = string.Empty;
            if (CountermeasureLife > 0f || CountermeasureAttractionRadius > 0f || CountermeasureStrength > 0f || CountermeasureCooldown > 0f)
            {
                countermeasureStats = $" | CM LIFE {CountermeasureLife:F1}s | RAD {CountermeasureAttractionRadius:F0} | STR {CountermeasureStrength:F1} | CD {CountermeasureCooldown:F1}s";
            }

            string mineStats = string.Empty;
            if (MineDamage > 0f || MineTriggerRadius > 0f || MineBlastRadius > 0f || MineLifetime > 0f || MineCooldown > 0f || MineArmDelay > 0f)
            {
                mineStats = $" | MINE DMG {MineDamage:F0} | TRIG {MineTriggerRadius:F0} | BLAST {MineBlastRadius:F0} | LIFE {MineLifetime:F1}s | ARM {MineArmDelay:F1}s | CD {MineCooldown:F1}s";
            }

            return $"{EquipmentType} | {Price:N0} CR | {requirement}{missileStats}{countermeasureStats}{mineStats}";
        }

        public override string ToString()
        {
            return Name;
        }
    }

    /// <summary>
    /// Canonical carried combat supply. These definitions intentionally have
    /// no mount behavior; runtime quantities live on a ship's loadout.
    /// </summary>
    public sealed class ConsumableEquipmentDefinition : EquipmentDefinition
    {
        public CombatConsumableType ConsumableType { get; set; } = CombatConsumableType.None;
        public int MaximumCarryQuantity { get; set; }
        public float RestorationAmount { get; set; }

        public bool IsValid => EquipmentType == EquipmentType.Consumable &&
            Price > 0 &&
            MaximumCarryQuantity > 0 &&
            IsFinitePositive(RestorationAmount) &&
            (ConsumableType == CombatConsumableType.Nanobots ||
             ConsumableType == CombatConsumableType.ShieldBattery);

        public override string GetStatsSummary()
        {
            return $"{base.GetStatsSummary()} | RESTORE {RestorationAmount:F0} | CARRY {MaximumCarryQuantity}";
        }

        private static bool IsFinitePositive(float value) =>
            !float.IsNaN(value) && !float.IsInfinity(value) && value > 0f;
    }

    /// <summary>
    /// Canonical defensive equipment definition. Mutable runtime charge never
    /// belongs here; each mounted ship owns its own ShieldSystem state.
    /// </summary>
    public sealed class ShieldEquipmentDefinition : EquipmentDefinition
    {
        public ShieldFamily Family { get; set; } = ShieldFamily.Mixed;
        public ShieldProgressionTier ProgressionTier { get; set; } = ShieldProgressionTier.Standard;
        public float Capacity { get; set; }
        public float RegenerationRate { get; set; }
        public float RegenerationDelay { get; set; }

        public bool IsValid => EquipmentType == EquipmentType.ShieldGenerator &&
            Price > 0 &&
            IsFinitePositive(Capacity) &&
            IsFinitePositive(RegenerationRate) &&
            IsFiniteNonNegative(RegenerationDelay) &&
            Enum.IsDefined(typeof(ShieldFamily), Family) &&
            Enum.IsDefined(typeof(ShieldProgressionTier), ProgressionTier);

        public override string GetStatsSummary()
        {
            return $"{base.GetStatsSummary()} | {Family} {ProgressionTier} | CAP {Capacity:F0} | REGEN {RegenerationRate:F1}/s | DELAY {RegenerationDelay:F1}s";
        }

        private static bool IsFinitePositive(float value) =>
            !float.IsNaN(value) && !float.IsInfinity(value) && value > 0f;

        private static bool IsFiniteNonNegative(float value) =>
            !float.IsNaN(value) && !float.IsInfinity(value) && value >= 0f;
    }

    /// <summary>
    /// Canonical ship powerplant metadata. The per-ship weapon-energy charge
    /// is deliberately kept in WeaponEnergy so definitions remain immutable
    /// catalog data.
    /// </summary>
    public sealed class PowerplantEquipmentDefinition : EquipmentDefinition
    {
        public PowerplantFamily Family { get; set; } = PowerplantFamily.Civilian;
        public PowerplantProgressionTier ProgressionTier { get; set; } = PowerplantProgressionTier.Low;
        public float EnergyCapacity { get; set; }
        public float EnergyRegenerationRate { get; set; }

        public bool IsValid => EquipmentType == EquipmentType.Powerplant &&
            Price > 0 &&
            IsFinitePositive(EnergyCapacity) &&
            IsFinitePositive(EnergyRegenerationRate) &&
            Enum.IsDefined(typeof(PowerplantFamily), Family) &&
            Enum.IsDefined(typeof(PowerplantProgressionTier), ProgressionTier);

        public override string GetStatsSummary()
        {
            return $"{base.GetStatsSummary()} | {Family} {ProgressionTier} | CAP {EnergyCapacity:F0} | REGEN {EnergyRegenerationRate:F1}/s";
        }

        private static bool IsFinitePositive(float value) =>
            !float.IsNaN(value) && !float.IsInfinity(value) && value > 0f;
    }

    /// <summary>
    /// Definition for gun-like equipment with weapon system stats.
    /// </summary>
    public class WeaponEquipmentDefinition : EquipmentDefinition
    {
        public WeaponType WeaponType { get; set; }
        public WeaponFamily Family { get; set; } = WeaponFamily.Mixed;
        public WeaponProgressionTier ProgressionTier { get; set; } = WeaponProgressionTier.Standard;
        public float Damage { get; set; }
        public float ProjectileSpeed { get; set; }
        public float RefireRate { get; set; }
        public float EnergyCost { get; set; }
        public float Range { get; set; }

        public override string GetStatsSummary()
        {
            return $"{base.GetStatsSummary()} | {Family} {ProgressionTier} | Weapon {WeaponType} | DMG {Damage:F0} | SPD {ProjectileSpeed:F0} | ROF {RefireRate:F2}s | EN {EnergyCost:F0} | RNG {Range:F0}";
        }
    }
}
