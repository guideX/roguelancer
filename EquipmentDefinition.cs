using System;

namespace Roguelancer
{
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
        /// Optional minimum reputation value for future progression systems.
        /// </summary>
        public int? RequiredReputation { get; set; }

        /// <summary>
        /// Optional contraband flag for future law enforcement systems.
        /// </summary>
        public bool IsContraband { get; set; }

        public virtual string GetStatsSummary()
        {
            string requirement = string.Empty;
            if (RequiredLevel.HasValue)
            {
                requirement += $"Level {RequiredLevel.Value}";
            }

            if (!string.IsNullOrWhiteSpace(RequiredReputationFactionId) && RequiredReputation.HasValue)
            {
                if (requirement.Length > 0)
                {
                    requirement += " | ";
                }

                requirement += $"Rep {RequiredReputationFactionId}:{RequiredReputation.Value}";
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
    /// Definition for gun-like equipment with weapon system stats.
    /// </summary>
    public class WeaponEquipmentDefinition : EquipmentDefinition
    {
        public WeaponType WeaponType { get; set; }
        public float Damage { get; set; }
        public float ProjectileSpeed { get; set; }
        public float RefireRate { get; set; }
        public float EnergyCost { get; set; }
        public float Range { get; set; }

        public override string GetStatsSummary()
        {
            return $"{base.GetStatsSummary()} | Weapon {WeaponType} | DMG {Damage:F0} | SPD {ProjectileSpeed:F0} | ROF {RefireRate:F2}s | EN {EnergyCost:F0} | RNG {Range:F0}";
        }
    }
}
