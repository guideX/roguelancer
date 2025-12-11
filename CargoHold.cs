using System.Collections.Generic;
using System.Linq;

namespace Roguelancer
{
    /// <summary>
    /// Manages a ship's cargo hold and commodity inventory
    /// </summary>
    public class CargoHold
    {
        private Dictionary<string, int> _commodities = new Dictionary<string, int>();
        
        public int MaxCapacity { get; private set; }
        public int UsedCapacity { get; private set; }
        public int AvailableCapacity => MaxCapacity - UsedCapacity;

        public CargoHold(int maxCapacity)
        {
            MaxCapacity = maxCapacity;
            UsedCapacity = 0;
        }

        /// <summary>
        /// Get the quantity of a specific commodity
        /// </summary>
        public int GetCommodityQuantity(string commodityName)
        {
            return _commodities.TryGetValue(commodityName, out int quantity) ? quantity : 0;
        }

        /// <summary>
        /// Get all commodities in cargo hold
        /// </summary>
        public Dictionary<string, int> GetAllCommodities()
        {
            return new Dictionary<string, int>(_commodities);
        }

        /// <summary>
        /// Check if cargo hold can accommodate additional units of a commodity
        /// </summary>
        public bool CanFit(Commodity commodity, int quantity)
        {
            int requiredSpace = commodity.VolumePerUnit * quantity;
            return AvailableCapacity >= requiredSpace;
        }

        /// <summary>
        /// Add commodity to cargo hold
        /// </summary>
        public bool AddCommodity(Commodity commodity, int quantity)
        {
            if (!CanFit(commodity, quantity))
                return false;

            if (_commodities.ContainsKey(commodity.Name))
            {
                _commodities[commodity.Name] += quantity;
            }
            else
            {
                _commodities[commodity.Name] = quantity;
            }

            UsedCapacity += commodity.VolumePerUnit * quantity;
            return true;
        }

        /// <summary>
        /// Remove commodity from cargo hold
        /// </summary>
        public bool RemoveCommodity(Commodity commodity, int quantity)
        {
            if (!_commodities.ContainsKey(commodity.Name))
                return false;

            int currentQuantity = _commodities[commodity.Name];
            if (currentQuantity < quantity)
                return false;

            _commodities[commodity.Name] -= quantity;
            if (_commodities[commodity.Name] == 0)
            {
                _commodities.Remove(commodity.Name);
            }

            UsedCapacity -= commodity.VolumePerUnit * quantity;
            return true;
        }

        /// <summary>
        /// Clear all commodities (used when selling all cargo)
        /// </summary>
        public void Clear()
        {
            _commodities.Clear();
            UsedCapacity = 0;
        }

        /// <summary>
        /// Transfer cargo to a new cargo hold (for ship changes)
        /// Returns false if cargo doesn't fit
        /// </summary>
        public bool TransferTo(CargoHold newCargoHold, Dictionary<Commodity, int> commodityRegistry)
        {
            // Check if everything fits
            int totalRequiredSpace = 0;
            foreach (var kvp in _commodities)
            {
                var commodity = commodityRegistry.FirstOrDefault(c => c.Key.Name == kvp.Key).Key;
                if (commodity != null)
                {
                    totalRequiredSpace += commodity.VolumePerUnit * kvp.Value;
                }
            }

            if (totalRequiredSpace > newCargoHold.MaxCapacity)
                return false;

            // Transfer all commodities
            foreach (var kvp in _commodities)
            {
                var commodity = commodityRegistry.FirstOrDefault(c => c.Key.Name == kvp.Key).Key;
                if (commodity != null)
                {
                    newCargoHold.AddCommodity(commodity, kvp.Value);
                }
            }

            return true;
        }

        /// <summary>
        /// Get total value of all cargo
        /// </summary>
        public int GetTotalValue(Dictionary<Commodity, int> commodityRegistry)
        {
            int totalValue = 0;
            foreach (var kvp in _commodities)
            {
                var commodity = commodityRegistry.FirstOrDefault(c => c.Key.Name == kvp.Key).Key;
                if (commodity != null)
                {
                    totalValue += commodity.BasePrice * kvp.Value;
                }
            }
            return totalValue;
        }

        /// <summary>
        /// Update max capacity (for ship changes)
        /// </summary>
        public void SetMaxCapacity(int newMaxCapacity)
        {
            MaxCapacity = newMaxCapacity;
        }
    }
}
