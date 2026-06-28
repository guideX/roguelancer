using Microsoft.Xna.Framework;

namespace Roguelancer
{
    /// <summary>
    /// Represents a tradeable commodity
    /// </summary>
    public class Commodity : CommodityDefinition
    {
        public Commodity()
        {
        }

        public Commodity(string id, string name, string description, int basePrice, int volumePerUnit, bool isContraband, string category, Color displayColor)
            : base(id, name, description, basePrice, volumePerUnit, isContraband, category, displayColor)
        {
        }

        /// <summary>
        /// Create Diamonds commodity
        /// </summary>
        public static Commodity CreateDiamonds()
        {
            return new Commodity(
                "diamonds",
                "Diamonds",
                "Precious crystalline carbon formations. Highly valued across all known systems for industrial applications and luxury goods. Diamonds are compact and extremely valuable, making them ideal for high-profit trading runs.",
                5000,
                1, // 1 cargo space per unit
                false,
                "Precious",
                Color.Cyan
            );
        }

        /// <summary>
        /// Create Alien Organisms commodity
        /// </summary>
        public static Commodity CreateAlienOrganisms()
        {
            return new Commodity(
                "alien-organisms",
                "Alien Organisms",
                "Exotic life forms from deep space. These rare biological specimens are highly sought after by research facilities and bio-tech corporations. Requires specialized containment - each unit occupies significant cargo space.",
                3000,
                5, // 5 cargo spaces per unit
                true,
                "Biological",
                Color.LimeGreen
            );
        }
    }
}
