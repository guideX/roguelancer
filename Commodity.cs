using Microsoft.Xna.Framework;

namespace Roguelancer
{
    /// <summary>
    /// Represents a tradeable commodity
    /// </summary>
    public class Commodity
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public int BasePrice { get; set; }
        public int VolumePerUnit { get; set; } // Cargo space per unit
        public Color DisplayColor { get; set; }
        
        public Commodity(string name, string description, int basePrice, int volumePerUnit, Color displayColor)
        {
            Name = name;
            Description = description;
            BasePrice = basePrice;
            VolumePerUnit = volumePerUnit;
            DisplayColor = displayColor;
        }

        /// <summary>
        /// Get a formatted info card text
        /// </summary>
        public string GetInfoCard()
        {
            return $"{Name}\n\n{Description}\n\nPrice: {BasePrice:N0} CR per unit\nCargo Space: {VolumePerUnit} per unit";
        }

        /// <summary>
        /// Create Diamonds commodity
        /// </summary>
        public static Commodity CreateDiamonds()
        {
            return new Commodity(
                "Diamonds",
                "Precious crystalline carbon formations. Highly valued across all known systems for industrial applications and luxury goods. Diamonds are compact and extremely valuable, making them ideal for high-profit trading runs.",
                5000,
                1, // 1 cargo space per unit
                Color.Cyan
            );
        }

        /// <summary>
        /// Create Alien Organisms commodity
        /// </summary>
        public static Commodity CreateAlienOrganisms()
        {
            return new Commodity(
                "Alien Organisms",
                "Exotic life forms from deep space. These rare biological specimens are highly sought after by research facilities and bio-tech corporations. Requires specialized containment - each unit occupies significant cargo space.",
                3000,
                5, // 5 cargo spaces per unit
                Color.LimeGreen
            );
        }
    }
}
