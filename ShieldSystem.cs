using Microsoft.Xna.Framework;
using System;

namespace Roguelancer
{
    /// <summary>
    /// Freelancer-style shield system that absorbs damage before hull.
    /// Shields regenerate over time after taking damage.
    /// </summary>
    public class ShieldSystem
    {
        public float MaxShields { get; private set; }
        public float CurrentShields { get; private set; }
        public bool IsDown => CurrentShields <= 0f;
        public float ShieldPercentage => MaxShields > 0 ? MathHelper.Clamp(CurrentShields / MaxShields, 0f, 1f) : 0f;

        // Regeneration settings
        public float RegenRate { get; set; } = 15f;
        public float RegenDelay { get; set; } = 3f;

        // State tracking
        private float _regenDelayTimer = 0f;
        private bool _isRecharging = false;

        public event Action OnShieldsDown;
        public event Action OnShieldsRestored;

        public ShieldSystem(float maxShields, float regenRate = 15f, float regenDelay = 3f)
        {
            MaxShields = maxShields;
            CurrentShields = maxShields;
            RegenRate = regenRate;
            RegenDelay = regenDelay;
        }

        /// <summary>
        /// Absorb incoming damage. Returns the amount of damage that bleeds through to hull.
        /// </summary>
        public float AbsorbDamage(float damage)
        {
            if (damage <= 0f) return 0f;

            _regenDelayTimer = RegenDelay;
            _isRecharging = false;

            if (CurrentShields <= 0f)
            {
                // Shields already down, all damage passes through
                return damage;
            }

            if (CurrentShields >= damage)
            {
                // Shields absorb all damage
                CurrentShields -= damage;
                Console.WriteLine($"??? Shields absorbed {damage:F1} damage (Shields: {CurrentShields:F1}/{MaxShields:F1} = {ShieldPercentage * 100:F0}%)");

                if (CurrentShields <= 0f)
                {
                    CurrentShields = 0f;
                    Console.WriteLine("??? SHIELDS DOWN!");
                    OnShieldsDown?.Invoke();
                }

                return 0f;
            }
            else
            {
                // Shields partially absorb, rest bleeds through to hull
                float absorbed = CurrentShields;
                float bleedThrough = damage - absorbed;
                CurrentShields = 0f;

                Console.WriteLine($"??? Shields absorbed {absorbed:F1}, {bleedThrough:F1} bleeds through to hull!");
                Console.WriteLine("??? SHIELDS DOWN!");
                OnShieldsDown?.Invoke();

                return bleedThrough;
            }
        }

        /// <summary>
        /// Update shield regeneration
        /// </summary>
        public void Update(GameTime gameTime)
        {
            float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;

            // Handle regen delay after taking damage
            if (_regenDelayTimer > 0f)
            {
                _regenDelayTimer -= dt;
                if (_regenDelayTimer <= 0f)
                {
                    _regenDelayTimer = 0f;
                    if (CurrentShields < MaxShields)
                    {
                        _isRecharging = true;
                        Console.WriteLine("??? Shield recharge starting...");
                    }
                }
                return;
            }

            // Regenerate shields
            if (CurrentShields < MaxShields)
            {
                bool wasDown = CurrentShields <= 0f;
                CurrentShields += RegenRate * dt;

                if (CurrentShields >= MaxShields)
                {
                    CurrentShields = MaxShields;
                    _isRecharging = false;
                }

                if (wasDown && CurrentShields > 0f)
                {
                    OnShieldsRestored?.Invoke();
                }
            }
        }

        /// <summary>
        /// Fully restore shields (for respawns, docking, etc.)
        /// </summary>
        public void FullRestore()
        {
            CurrentShields = MaxShields;
            _regenDelayTimer = 0f;
            _isRecharging = false;
        }

        /// <summary>
        /// Get shield bar color (always blue-themed like Freelancer)
        /// </summary>
        public Color GetShieldColor()
        {
            float t = ShieldPercentage;

            if (t > 0.5f)
            {
                // Bright blue to medium blue
                return Color.Lerp(new Color(80, 140, 255), new Color(120, 180, 255), (t - 0.5f) * 2f);
            }
            else if (t > 0.2f)
            {
                // Dim blue to bright blue
                return Color.Lerp(new Color(40, 80, 180), new Color(80, 140, 255), (t - 0.2f) / 0.3f);
            }
            else
            {
                // Very dim / flickering when almost gone
                return Color.Lerp(new Color(20, 40, 100), new Color(40, 80, 180), t / 0.2f);
            }
        }
    }
}
