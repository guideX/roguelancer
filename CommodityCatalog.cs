using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Roguelancer
{
    /// <summary>
    /// Static catalog of starter commodities used by markets and fallback trading.
    /// </summary>
    public static class CommodityCatalog
    {
        private static readonly List<Commodity> _all = new()
        {
            new Commodity("food-rations", "Food Rations", "Basic packaged rations for long-haul crews and station dwellers.", 120, 1, false, "Food", Color.SandyBrown),
            new Commodity("water", "Water", "Purified water in sealed ration packs and tanked bulk form.", 80, 1, false, "Basic Supplies", Color.SkyBlue),
            new Commodity("h-fuel", "H-Fuel", "Hydrogen-based reactor fuel for drives, generators, and station systems.", 300, 1, false, "Fuel", Color.OrangeRed),
            new Commodity("engine-components", "Engine Components", "Precision drive parts used in shipyard repairs and retrofits.", 900, 2, false, "Industrial", Color.LightSteelBlue),
            new Commodity("construction-materials", "Construction Materials", "Structural alloys, panels, and bulk building supplies.", 450, 3, false, "Industrial", Color.Silver),
            new Commodity("luxury-goods", "Luxury Goods", "High-end apparel, spirits, and premium consumer imports.", 1200, 1, false, "Luxury", Color.MediumPurple),
            new Commodity("medical-supplies", "Medical Supplies", "Field medicine, medkits, and emergency treatment stock.", 650, 1, false, "Medical", Color.LightCoral),
            new Commodity("side-arms", "Side Arms", "Compact personal weapons for security, bodyguards, and smugglers.", 1500, 1, true, "Military", Color.IndianRed),
            new Commodity("diamonds", "Diamonds", "Precious crystalline carbon formations prized by industry and the wealthy.", 5000, 1, false, "Precious", Color.Cyan),
            new Commodity("alien-organisms", "Alien Organisms", "Rare biological specimens of uncertain origin and questionable legality.", 3000, 5, true, "Biological", Color.LimeGreen),
            new Commodity("boron", "Boron", "Industrial boron used in alloys, reactors, and advanced manufacturing.", 220, 1, false, "Raw Materials", Color.Khaki),
            new Commodity("consumer-goods", "Consumer Goods", "Everyday appliances, electronics, and station market staples.", 250, 1, false, "Consumer Goods", Color.Gold)
        };

        public static IReadOnlyList<Commodity> All => _all;

        public static Commodity GetById(string commodityId)
        {
            if (string.IsNullOrWhiteSpace(commodityId))
            {
                return null;
            }

            return _all.FirstOrDefault(c => string.Equals(c.Id, commodityId.Trim(), StringComparison.OrdinalIgnoreCase));
        }

        public static Commodity GetByName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return null;
            }

            return _all.FirstOrDefault(c => string.Equals(c.Name, name.Trim(), StringComparison.OrdinalIgnoreCase));
        }

        public static Commodity GetByIdOrName(string commodityIdOrName)
        {
            return GetById(commodityIdOrName) ?? GetByName(commodityIdOrName);
        }

        public static Dictionary<Commodity, int> BuildRegistry()
        {
            var registry = new Dictionary<Commodity, int>();
            for (int i = 0; i < _all.Count; i++)
            {
                registry[_all[i]] = i;
            }

            return registry;
        }
    }
}
