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

            return $"{EquipmentType} | {Price:N0} CR | {requirement}";
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
