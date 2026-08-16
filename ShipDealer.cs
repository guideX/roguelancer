using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
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
        private bool _modelsLoaded;
        
        public IReadOnlyList<ShipDefinition> AvailableShips => _availableShips;
        public ShipDefinition CurrentPlayerShip => _currentPlayerShip;
        public bool ModelsLoaded => _modelsLoaded;

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
            
            // The catalog entry is also the authoritative current definition.
            // Keeping one object per model prevents the station UI and save path
            // from drifting apart from the player's actual ship identity.
            _currentPlayerShip = _availableShips[0];
        }

        /// <summary>
        /// Load models for all available ships
        /// </summary>
        public void LoadShipModels(ContentManager content)
        {
            if (content == null)
            {
                _modelsLoaded = false;
                return;
            }

            Dictionary<string, Model> modelCache = new(StringComparer.OrdinalIgnoreCase);
            foreach (ShipDefinition ship in _availableShips)
            {
                if (ship == null || string.IsNullOrWhiteSpace(ship.ModelPath)) continue;
                if (modelCache.TryGetValue(ship.ModelPath, out Model cachedModel))
                {
                    ship.Model = cachedModel;
                    continue;
                }

                try
                {
                    Model model = content.Load<Model>(ship.ModelPath);
                    modelCache[ship.ModelPath] = model;
                    ship.Model = model;
                    Console.WriteLine($"[SHIP DEALER] Loaded model for {ship.Name}");
                }
                catch (Exception ex)
                {
                    ship.Model = null;
                    Console.WriteLine($"[SHIP DEALER] Failed to load model for {ship.Name}: {ex.Message}");
                }
            }

            _modelsLoaded = true;
            _availableShips = _availableShips.Where(ship => ship?.Model != null).ToList();
            if (_currentPlayerShip == null || !_availableShips.Contains(_currentPlayerShip))
            {
                _currentPlayerShip = _availableShips.FirstOrDefault();
            }
        }

        /// <summary>
        /// Check if player can afford a ship (with trade-in)
        /// </summary>
        public bool CanAffordShip(ShipDefinition ship, PlayerCredits credits)
        {
            if (ship == null || credits == null || !_availableShips.Contains(ship)) return false;
            if (_currentPlayerShip != null && string.Equals(ship.Name, _currentPlayerShip.Name, StringComparison.OrdinalIgnoreCase)) return false;
            if (_modelsLoaded && ship.Model == null) return false;
            int totalCost = GetTotalCost(ship);
            return ship.Price > 0 && totalCost >= 0 && credits.CanAfford(totalCost);
        }

        /// <summary>
        /// Get the total cost of a ship after trade-in discount
        /// </summary>
        public int GetTotalCost(ShipDefinition ship)
        {
            if (ship == null) return -1;
            if (_currentPlayerShip != null && string.Equals(ship.Name, _currentPlayerShip.Name, StringComparison.OrdinalIgnoreCase))
            {
                return 0; // Already own this ship
            }
            
            // New ship price minus trade-in value of current ship
            int tradeInValue = _currentPlayerShip?.TradeInValue ?? 0;
            long totalCost = (long)ship.Price - Math.Max(0, tradeInValue);
            return (int)Math.Clamp(totalCost, 0L, int.MaxValue);
        }

        /// <summary>
        /// Purchase a new ship
        /// </summary>
        public bool PurchaseShip(ShipDefinition ship, PlayerCredits credits, Ship playerShip, CommodityDealer commodityDealer)
        {
            return TryPurchaseShip(ship, credits, playerShip, out _);
        }

        /// <summary>
        /// Validate a purchase without changing credits, the player ship, cargo,
        /// loadout, or dealer state. Models are required once content loading has
        /// completed; pure smoke tests can exercise the transaction without a
        /// graphics device before that point.
        /// </summary>
        public bool CanPurchaseShip(ShipDefinition ship, PlayerCredits credits, Ship playerShip, out string message)
        {
            message = string.Empty;
            if (ship == null || !_availableShips.Contains(ship))
            {
                message = "Ship is not available at this dealer.";
                return false;
            }

            if (_currentPlayerShip != null && string.Equals(ship.Name, _currentPlayerShip.Name, StringComparison.OrdinalIgnoreCase))
            {
                message = "That ship is already your current ship.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(ship.Name) || string.IsNullOrWhiteSpace(ship.ModelPath))
            {
                message = "Ship definition is incomplete.";
                return false;
            }

            if (ship.Price <= 0 || ship.CargoCapacity <= 0 || ship.MaxHull <= 0f || ship.MaxEnergy <= 0f || ship.MaxShields < 0f ||
                float.IsNaN(ship.MaxSpeed) || float.IsInfinity(ship.MaxSpeed) || float.IsNaN(ship.TurnSpeed) || float.IsInfinity(ship.TurnSpeed))
            {
                message = "Ship has invalid pricing or flight data.";
                return false;
            }

            if (_modelsLoaded && ship.Model == null)
            {
                message = "That ship's model is unavailable.";
                return false;
            }

            if (credits == null || playerShip?.CargoHold == null)
            {
                message = "Ship dealer transaction is unavailable.";
                return false;
            }

            if (playerShip.CargoHold.UsedCapacity > ship.CargoCapacity)
            {
                message = $"Cargo does not fit in the {ship.Name} hold.";
                return false;
            }

            int purchaseCost = GetTotalCost(ship);
            if (purchaseCost < 0)
            {
                message = "Ship price is invalid.";
                return false;
            }

            if (!credits.CanAfford(purchaseCost))
            {
                message = $"Insufficient credits. Need {purchaseCost:N0} CR.";
                return false;
            }

            return true;
        }

        /// <summary>
        /// Execute one validated ship replacement. The candidate definition is
        /// prepared on an isolated Ship first, so invalid initialization cannot
        /// consume credits or mutate the authoritative player instance.
        /// </summary>
        public bool TryPurchaseShip(ShipDefinition ship, PlayerCredits credits, Ship playerShip, out string message)
        {
            if (!CanPurchaseShip(ship, credits, playerShip, out message))
            {
                Console.WriteLine($"[SHIP DEALER] Purchase rejected: {message}");
                return false;
            }

            try
            {
                Ship preparedShip = new(playerShip.Position);
                ship.ApplyToShip(preparedShip);
                if (preparedShip.ModelPath != ship.ModelPath || preparedShip.CargoHold.MaxCapacity != ship.CargoCapacity)
                {
                    message = "Replacement ship could not be prepared.";
                    return false;
                }
            }
            catch (Exception ex)
            {
                message = $"Replacement ship could not be prepared: {ex.Message}";
                Console.WriteLine($"[SHIP DEALER] {message}");
                return false;
            }

            int purchaseCost = GetTotalCost(ship);
            if (!credits.RemoveCredits(purchaseCost))
            {
                message = "Insufficient credits.";
                return false;
            }

            try
            {
                // Preserve the authoritative Ship object. Flight, mission,
                // station, and network systems all reference this instance.
                // ApplyToShip resets the new ship's hull/energy/shields to full
                // and changes only the ship configuration; cargo and loadout
                // remain on the player-owned state after the capacity gate.
                ship.ApplyToShip(playerShip);
                playerShip.RefreshCollisionRadiusFromModel();
                _currentPlayerShip = ship;
                message = $"Purchased {ship.Name} for {purchaseCost:N0} CR.";
                Console.WriteLine($"[SHIP DEALER] {message}");
                return true;
            }
            catch (Exception ex)
            {
                // ApplyToShip is deliberately simple and preflighted above, but
                // refund if a future definition adds a failing initializer.
                credits.AddCredits(purchaseCost);
                message = $"Replacement failed; credits were restored: {ex.Message}";
                Console.WriteLine($"[SHIP DEALER] {message}");
                return false;
            }
        }

        /// <summary>
        /// Get a ship by name
        /// </summary>
        public ShipDefinition GetShipByName(string name)
        {
            return _availableShips.FirstOrDefault(s => string.Equals(s.Name, name, StringComparison.OrdinalIgnoreCase));
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
            if (ship != null && _availableShips.Contains(ship))
            {
                _currentPlayerShip = ship;
            }
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
