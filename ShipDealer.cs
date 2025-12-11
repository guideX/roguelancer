using Microsoft.Xna.Framework.Content;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Roguelancer
{
    /// <summary>
    /// Manages ship purchasing and selling at stations
    /// </summary>
    public class ShipDealer
    {
        private List<ShipDefinition> _availableShips = new List<ShipDefinition>();
        private ShipDefinition _currentPlayerShip;
        
        public IReadOnlyList<ShipDefinition> AvailableShips => _availableShips;
        public ShipDefinition CurrentPlayerShip => _currentPlayerShip;

        public ShipDealer()
        {
            InitializeShipInventory();
        }

        /// <summary>
        /// Initialize the available ships for purchase
        /// </summary>
        private void InitializeShipInventory()
        {
            _availableShips.Add(ShipDefinition.CreateScimitar());
            _availableShips.Add(ShipDefinition.CreateTransport());
            
            // Set default player ship to Scimitar
            _currentPlayerShip = ShipDefinition.CreateScimitar();
        }

        /// <summary>
        /// Load models for all available ships
        /// </summary>
        public void LoadShipModels(ContentManager content)
        {
            foreach (var ship in _availableShips)
            {
                try
                {
                    ship.Model = content.Load<Microsoft.Xna.Framework.Graphics.Model>(ship.ModelPath);
                    Console.WriteLine($"[SHIP DEALER] Loaded model for {ship.Name}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[SHIP DEALER] Failed to load model for {ship.Name}: {ex.Message}");
                }
            }
            
            // Load current player ship model too
            try
            {
                _currentPlayerShip.Model = content.Load<Microsoft.Xna.Framework.Graphics.Model>(_currentPlayerShip.ModelPath);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SHIP DEALER] Failed to load current player ship model: {ex.Message}");
            }
        }

        /// <summary>
        /// Check if player can afford a ship (with trade-in)
        /// </summary>
        public bool CanAffordShip(ShipDefinition ship, PlayerCredits credits)
        {
            int totalCost = GetTotalCost(ship);
            return credits.CanAfford(totalCost);
        }

        /// <summary>
        /// Get the total cost of a ship after trade-in discount
        /// </summary>
        public int GetTotalCost(ShipDefinition ship)
        {
            if (ship.Name == _currentPlayerShip.Name)
            {
                return 0; // Already own this ship
            }
            
            // New ship price minus trade-in value of current ship
            return Math.Max(0, ship.Price - _currentPlayerShip.TradeInValue);
        }

        /// <summary>
        /// Purchase a new ship
        /// </summary>
        public bool PurchaseShip(ShipDefinition ship, PlayerCredits credits, Ship playerShip, CommodityDealer commodityDealer)
        {
            // Check if already own this ship
            if (ship.Name == _currentPlayerShip.Name)
            {
                Console.WriteLine($"[SHIP DEALER] Already own {ship.Name}");
                return false;
            }

            // Check if can afford
            int totalCost = GetTotalCost(ship);
            if (!credits.CanAfford(totalCost))
            {
                Console.WriteLine($"[SHIP DEALER] Cannot afford {ship.Name} - need {totalCost} CR");
                return false;
            }

            // Check if cargo fits in new ship
            int currentCargoUsed = playerShip.CargoHold.UsedCapacity;
            if (currentCargoUsed > ship.CargoCapacity)
            {
                Console.WriteLine($"[SHIP DEALER] Cargo doesn't fit! Current: {currentCargoUsed}, New ship capacity: {ship.CargoCapacity}");
                return false;
            }

            // Process purchase
            if (credits.RemoveCredits(totalCost))
            {
                Console.WriteLine($"[SHIP DEALER] Purchased {ship.Name} for {totalCost} CR");
                
                // Apply new ship stats (this will update cargo capacity)
                ship.ApplyToShip(playerShip);
                
                // Update current player ship
                _currentPlayerShip = ship;
                
                return true;
            }

            return false;
        }

        /// <summary>
        /// Get a ship by name
        /// </summary>
        public ShipDefinition GetShipByName(string name)
        {
            return _availableShips.FirstOrDefault(s => s.Name == name);
        }

        /// <summary>
        /// Sell current ship and get credits (only if have another ship to switch to)
        /// </summary>
        public int SellCurrentShip(PlayerCredits credits)
        {
            int value = _currentPlayerShip.TradeInValue;
            credits.AddCredits(value);
            Console.WriteLine($"[SHIP DEALER] Sold {_currentPlayerShip.Name} for {value} credits");
            return value;
        }

        /// <summary>
        /// Set the current player ship (for initialization)
        /// </summary>
        public void SetCurrentShip(ShipDefinition ship)
        {
            _currentPlayerShip = ship;
        }

        /// <summary>
        /// Get ship by index
        /// </summary>
        public ShipDefinition GetShipByIndex(int index)
        {
            if (index >= 0 && index < _availableShips.Count)
            {
                return _availableShips[index];
            }
            return null;
        }
    }
}
