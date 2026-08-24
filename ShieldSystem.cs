using Microsoft.Xna.Framework;
using System;

namespace Roguelancer
{
    /// <summary>
    /// Per-ship shield runtime state. Canonical shield definitions remain
    /// immutable catalog data; current charge and regeneration timing live on
    /// this instance only.
    /// </summary>
    public class ShieldSystem
    {
        private float _regenRate;
        private float _regenDelay;
        private float _regenDelayTimer;

        public string MountedShieldId { get; private set; } = string.Empty;
        public ShieldEquipmentDefinition Definition { get; private set; }
        public float MaxShields { get; private set; }
        public float CurrentShields { get; private set; }
        public bool HasMountedShield => MaxShields > 0f &&
            (Definition != null || string.IsNullOrWhiteSpace(MountedShieldId));
        public bool IsDown => !HasMountedShield || CurrentShields <= 0f;
        public float ShieldPercentage => MaxShields > 0f
            ? MathHelper.Clamp(CurrentShields / MaxShields, 0f, 1f)
            : 0f;

        public float RegenRate
        {
            get => _regenRate;
            set => _regenRate = IsFiniteNonNegative(value) ? value : 0f;
        }

        public float RegenDelay
        {
            get => _regenDelay;
            set => _regenDelay = IsFiniteNonNegative(value) ? value : 0f;
        }

        public event Action OnShieldsDown;
        public event Action OnShieldsRestored;

        public ShieldSystem() : this(0f, 0f, 0f)
        {
        }

        /// <summary>
        /// Legacy numeric construction remains source-compatible for existing
        /// combat harnesses. Ships using equipment bind through Configure.
        /// </summary>
        public ShieldSystem(float maxShields, float regenRate = 15f, float regenDelay = 3f)
        {
            MountedShieldId = string.Empty;
            Definition = null;
            MaxShields = SanitizeCapacity(maxShields);
            RegenRate = regenRate;
            RegenDelay = regenDelay;
            CurrentShields = MaxShields;
        }

        public ShieldSystem(ShieldEquipmentDefinition definition)
        {
            Configure(definition, restoreFull: true);
        }

        /// <summary>
        /// Binds this runtime state to a canonical mounted shield. Invalid or
        /// missing definitions safely become an unmounted zero-capacity state.
        /// </summary>
        public void Configure(ShieldEquipmentDefinition definition, bool restoreFull = true)
        {
            if (definition == null || !definition.IsValid)
            {
                MountedShieldId = string.Empty;
                Definition = null;
                MaxShields = 0f;
                RegenRate = 0f;
                RegenDelay = 0f;
                CurrentShields = 0f;
                _regenDelayTimer = 0f;
                return;
            }

            MountedShieldId = definition.Id ?? string.Empty;
            Definition = definition;
            MaxShields = definition.Capacity;
            RegenRate = definition.RegenerationRate;
            RegenDelay = definition.RegenerationDelay;
            if (restoreFull)
            {
                FullRestore();
            }
            else
            {
                CurrentShields = MathHelper.Clamp(CurrentShields, 0f, MaxShields);
            }
        }

        /// <summary>
        /// Absorbs finite positive damage and returns only non-negative hull
        /// overflow. Shield hits always reset the regeneration delay, even if
        /// the shield is already depleted.
        /// </summary>
        public float AbsorbDamage(float damage)
        {
            if (!IsFinitePositive(damage))
            {
                return 0f;
            }

            _regenDelayTimer = HasMountedShield ? RegenDelay : 0f;
            if (!HasMountedShield || CurrentShields <= 0f)
            {
                return damage;
            }

            float absorbed = Math.Min(CurrentShields, damage);
            CurrentShields = MathHelper.Clamp(CurrentShields - absorbed, 0f, MaxShields);
            float overflow = Math.Max(0f, damage - absorbed);

            if (CurrentShields <= 0f && absorbed > 0f)
            {
                CurrentShields = 0f;
                OnShieldsDown?.Invoke();
            }

            return IsFiniteNonNegative(overflow) ? overflow : damage;
        }

        /// <summary>
        /// Advances regeneration from simulation elapsed time, respecting the
        /// canonical delay and clamping at maximum capacity.
        /// </summary>
        public void Update(GameTime gameTime)
        {
            if (!HasMountedShield || gameTime == null || CurrentShields >= MaxShields)
            {
                return;
            }

            float deltaTime = (float)gameTime.ElapsedGameTime.TotalSeconds;
            if (!IsFinitePositive(deltaTime))
            {
                return;
            }

            if (_regenDelayTimer > 0f)
            {
                _regenDelayTimer = Math.Max(0f, _regenDelayTimer - deltaTime);
                if (_regenDelayTimer > 0f)
                {
                    return;
                }
            }

            if (!IsFinitePositive(RegenRate))
            {
                return;
            }

            bool wasDown = CurrentShields <= 0f;
            float recharge = RegenRate * deltaTime;
            if (!IsFinitePositive(recharge))
            {
                return;
            }

            CurrentShields = MathHelper.Clamp(CurrentShields + recharge, 0f, MaxShields);
            if (wasDown && CurrentShields > 0f)
            {
                OnShieldsRestored?.Invoke();
            }
        }

        public void FullRestore()
        {
            CurrentShields = MaxShields;
            _regenDelayTimer = 0f;
        }

        public Color GetShieldColor()
        {
            float t = ShieldPercentage;
            if (t > 0.5f)
            {
                return Color.Lerp(new Color(80, 140, 255), new Color(120, 180, 255), (t - 0.5f) * 2f);
            }

            if (t > 0.2f)
            {
                return Color.Lerp(new Color(40, 80, 180), new Color(80, 140, 255), (t - 0.2f) / 0.3f);
            }

            return Color.Lerp(new Color(20, 40, 100), new Color(40, 80, 180), t / 0.2f);
        }

        private static float SanitizeCapacity(float value) => IsFinitePositive(value) ? value : 0f;

        private static bool IsFinitePositive(float value) =>
            !float.IsNaN(value) && !float.IsInfinity(value) && value > 0f;

        private static bool IsFiniteNonNegative(float value) =>
            !float.IsNaN(value) && !float.IsInfinity(value) && value >= 0f;
    }
}
