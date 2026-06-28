using System;

namespace Roguelancer
{
    /// <summary>
    /// Player's credit/money system for trading and upgrades
    /// </summary>
    public class PlayerCredits
    {
        private int _credits;
        
        public int Credits 
        { 
            get => _credits;
            private set => _credits = Math.Max(0, value);
        }

        public event Action<int>? OnCreditsChanged;

        public PlayerCredits(int startingCredits = 10000)
        {
            _credits = startingCredits;
        }

        /// <summary>
        /// Set the player's credit balance directly.
        /// </summary>
        public void SetCredits(int amount)
        {
            Credits = amount;
            OnCreditsChanged?.Invoke(Credits);
        }

        /// <summary>
        /// Add credits to player account
        /// </summary>
        public void AddCredits(int amount)
        {
            if (amount <= 0) return;
            
            Credits += amount;
            OnCreditsChanged?.Invoke(Credits);
            Console.WriteLine($"[CREDITS] +{amount} credits. Total: {Credits}");
        }

        /// <summary>
        /// Remove credits from player account
        /// </summary>
        public bool RemoveCredits(int amount)
        {
            if (amount <= 0) return false;
            if (amount > Credits) return false;
            
            Credits -= amount;
            OnCreditsChanged?.Invoke(Credits);
            Console.WriteLine($"[CREDITS] -{amount} credits. Total: {Credits}");
            return true;
        }

        /// <summary>
        /// Check if player can afford something
        /// </summary>
        public bool CanAfford(int amount)
        {
            return Credits >= amount;
        }

        /// <summary>
        /// Format credits with commas for display
        /// </summary>
        public string GetFormattedCredits()
        {
            return Credits.ToString("N0");
        }
    }
}
