using Microsoft.Xna.Framework;
using System;

namespace Roguelancer
{
    /// <summary>
    /// Manages hull integrity (health) for ships
    /// </summary>
    public class HullIntegrity
    {
        public float MaxHull { get; private set; }
        public float CurrentHull { get; private set; }
        public bool IsDestroyed => CurrentHull <= 0f;
        public float HullPercentage => MaxHull > 0 ? CurrentHull / MaxHull : 0f;

        public event Action<float> OnDamaged; // Passes damage amount
        public event Action OnDestroyed;

        public HullIntegrity(float maxHull)
        {
            MaxHull = maxHull;
            CurrentHull = maxHull;
        }

        /// <summary>
        /// Apply damage to the hull
        /// </summary>
        /// <param name="damage">Amount of damage to apply</param>
        /// <returns>True if ship was destroyed by this damage</returns>
        public bool TakeDamage(float damage)
        {
            if (IsDestroyed) return false;

            float previousHull = CurrentHull;
            CurrentHull = Math.Max(0f, CurrentHull - damage);

            Console.WriteLine($"?? Hull damaged: {damage:F1} (Hull: {CurrentHull:F1}/{MaxHull:F1} = {HullPercentage * 100:F0}%)");

            OnDamaged?.Invoke(damage);

            if (CurrentHull <= 0f && previousHull > 0f)
            {
                Console.WriteLine($"?? SHIP DESTROYED!");
                OnDestroyed?.Invoke();
                return true;
            }

            return false;
        }

        /// <summary>
        /// Repair hull by specified amount
        /// </summary>
        public void Repair(float amount)
        {
            if (IsDestroyed) return;
            CurrentHull = Math.Min(MaxHull, CurrentHull + amount);
        }

        /// <summary>
        /// Fully repair hull to maximum
        /// </summary>
        public void FullRepair()
        {
            CurrentHull = MaxHull;
        }

        /// <summary>
        /// Get color based on hull percentage (green -> yellow -> red)
        /// </summary>
        public Color GetHullColor()
        {
            float percent = HullPercentage;
            if (percent > 0.66f)
                return Color.Lerp(Color.Yellow, Color.Lime, (percent - 0.66f) / 0.34f);
            else if (percent > 0.33f)
                return Color.Lerp(Color.Orange, Color.Yellow, (percent - 0.33f) / 0.33f);
            else
                return Color.Lerp(Color.Red, Color.Orange, percent / 0.33f);
        }
    }
}
