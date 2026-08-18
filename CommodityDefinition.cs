using Microsoft.Xna.Framework;
using System;

namespace Roguelancer
{
    /// <summary>
    /// Base commodity definition used by station markets and cargo handling.
    /// </summary>
    public class CommodityDefinition
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public int BasePrice { get; set; }
        public int VolumePerUnit { get; set; }
        public bool IsContraband { get; set; }
        public string Category { get; set; }
        public Color DisplayColor { get; set; }

        /// <summary>
        /// Mission-bound package definitions are catalogued with the same
        /// commodity type as ordinary cargo, but are never valid market goods.
        /// The CargoHold reservation remains the authoritative per-unit guard.
        /// </summary>
        public bool IsMissionCargo => string.Equals(Category, "Mission Cargo", StringComparison.OrdinalIgnoreCase);

        public CommodityDefinition()
        {
            Id = string.Empty;
            Name = string.Empty;
            Description = string.Empty;
            Category = string.Empty;
            DisplayColor = Color.White;
        }

        public CommodityDefinition(
            string id,
            string name,
            string description,
            int basePrice,
            int volumePerUnit,
            bool isContraband,
            string category,
            Color displayColor)
        {
            Id = id;
            Name = name;
            Description = description;
            BasePrice = basePrice;
            VolumePerUnit = volumePerUnit;
            IsContraband = isContraband;
            Category = category;
            DisplayColor = displayColor;
        }

        public virtual string GetInfoCard()
        {
            string contrabandText = IsContraband ? "\nContraband: Yes" : string.Empty;
            return $"{Name}\n\n{Description}\n\nCategory: {Category}\nPrice: {BasePrice:N0} CR per unit\nCargo Space: {VolumePerUnit} per unit{contrabandText}";
        }
    }
}
