using System.Collections.Generic;
using System.Linq;

namespace Roguelancer
{
    /// <summary>
    /// Manages commodity trading at stations
    /// </summary>
    public class CommodityDealer
    {
        private List<Commodity> _availableCommodities = new List<Commodity>();
        
        public IReadOnlyList<Commodity> AvailableCommodities => _availableCommodities;

        public CommodityDealer()
        {
            InitializeCommodityInventory();
        }

        /// <summary>
        /// Initialize the available commodities for trade
        /// </summary>
        private void InitializeCommodityInventory()
        {
            _availableCommodities.Add(Commodity.CreateDiamonds());
            _availableCommodities.Add(Commodity.CreateAlienOrganisms());
        }

        /// <summary>
        /// Check if player can afford to buy a commodity
        /// </summary>
        public bool CanAfford(Commodity commodity, int quantity, PlayerCredits credits)
        {
            int totalCost = commodity.BasePrice * quantity;
            return credits.CanAfford(totalCost);
        }

        /// <summary>
        /// Check if cargo hold has space for commodity
        /// </summary>
        public bool HasSpace(Commodity commodity, int quantity, CargoHold cargoHold)
        {
            return cargoHold.CanFit(commodity, quantity);
        }

        /// <summary>
        /// Purchase commodity
        /// </summary>
        public bool BuyCommodity(Commodity commodity, int quantity, PlayerCredits credits, CargoHold cargoHold)
        {
            int totalCost = commodity.BasePrice * quantity;
            
            // Check if player can afford it
            if (!credits.CanAfford(totalCost))
                return false;

            // Check if cargo hold has space
            if (!cargoHold.CanFit(commodity, quantity))
                return false;

            // Process transaction
            if (credits.RemoveCredits(totalCost))
            {
                cargoHold.AddCommodity(commodity, quantity);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Sell commodity
        /// </summary>
        public bool SellCommodity(Commodity commodity, int quantity, PlayerCredits credits, CargoHold cargoHold)
        {
            // Check if player has enough of the commodity
            if (cargoHold.GetCommodityQuantity(commodity.Name) < quantity)
                return false;

            // Process transaction
            if (cargoHold.RemoveCommodity(commodity, quantity))
            {
                int totalValue = commodity.BasePrice * quantity;
                credits.AddCredits(totalValue);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Get commodity by name
        /// </summary>
        public Commodity GetCommodityByName(string name)
        {
            return _availableCommodities.FirstOrDefault(c => c.Name == name);
        }

        /// <summary>
        /// Get commodity by index
        /// </summary>
        public Commodity GetCommodityByIndex(int index)
        {
            if (index < 0 || index >= _availableCommodities.Count)
                return null;
            return _availableCommodities[index];
        }

        /// <summary>
        /// Get all commodities as a dictionary (for cargo transfer lookups)
        /// </summary>
        public Dictionary<Commodity, int> GetCommodityRegistry()
        {
            var registry = new Dictionary<Commodity, int>();
            for (int i = 0; i < _availableCommodities.Count; i++)
            {
                registry[_availableCommodities[i]] = i;
            }
            return registry;
        }
    }
}
