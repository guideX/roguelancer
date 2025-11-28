using Microsoft.Xna.Framework;
using System;

namespace Roguelancer
{
    /// <summary>
    /// Manages ship energy for weapons and systems
    /// </summary>
    public class ShipEnergy
    {
        // Energy pool
        public float MaxEnergy { get; set; }
        public float CurrentEnergy { get; private set; }
        
        // Regeneration settings
        public float RegenRate { get; set; } = 50f; // Energy per second when regenerating
        public float RegenDelay { get; set; } = 2f; // Delay in seconds after depleting before regen starts
        
        // State tracking
        private float _regenDelayTimer = 0f;
        private bool _isDepleted = false;
        
        public bool IsDepleted => _isDepleted;
        public float EnergyPercentage => MaxEnergy > 0 ? MathHelper.Clamp(CurrentEnergy / MaxEnergy, 0f, 1f) : 0f;
        
        public ShipEnergy(float maxEnergy, float regenRate = 50f, float regenDelay = 2f)
        {
            MaxEnergy = maxEnergy;
            CurrentEnergy = maxEnergy;
            RegenRate = regenRate;
            RegenDelay = regenDelay;
        }
        
        /// <summary>
        /// Try to consume energy. Returns true if enough energy was available.
        /// </summary>
        public bool TryConsume(float amount)
        {
            if (CurrentEnergy < amount)
            {
                return false;
            }
            
            CurrentEnergy -= amount;
            
            // Check if we just depleted
            if (CurrentEnergy <= 0f)
            {
                CurrentEnergy = 0f;
                _isDepleted = true;
                _regenDelayTimer = RegenDelay;
                Console.WriteLine($"? ENERGY DEPLETED! Recharge delay: {RegenDelay:F1}s");
            }
            
            return true;
        }
        
        /// <summary>
        /// Update energy regeneration
        /// </summary>
        public void Update(GameTime gameTime)
        {
            float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
            
            // Handle depletion delay
            if (_isDepleted)
            {
                _regenDelayTimer -= dt;
                if (_regenDelayTimer <= 0f)
                {
                    _isDepleted = false;
                    Console.WriteLine("? Energy recharge starting...");
                }
                return; // Don't regenerate during delay
            }
            
            // Regenerate energy
            if (CurrentEnergy < MaxEnergy)
            {
                CurrentEnergy += RegenRate * dt;
                if (CurrentEnergy > MaxEnergy)
                {
                    CurrentEnergy = MaxEnergy;
                }
            }
        }
        
        /// <summary>
        /// Fully restore energy (for respawns, docking, etc.)
        /// </summary>
        public void FullRestore()
        {
            CurrentEnergy = MaxEnergy;
            _isDepleted = false;
            _regenDelayTimer = 0f;
        }
        
        /// <summary>
        /// Get color based on energy percentage (green -> yellow -> red)
        /// </summary>
        public Color GetEnergyColor()
        {
            float t = EnergyPercentage;
            
            if (t > 0.5f)
            {
                // Green to Yellow (100% to 50%)
                float blend = (t - 0.5f) * 2f;
                return Color.Lerp(Color.Yellow, Color.Green, blend);
            }
            else
            {
                // Yellow to Red (50% to 0%)
                float blend = t * 2f;
                return Color.Lerp(Color.Red, Color.Yellow, blend);
            }
        }
    }
}
