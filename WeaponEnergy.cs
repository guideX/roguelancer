using Microsoft.Xna.Framework;
using System;

namespace Roguelancer
{
    /// <summary>
    /// Bounded per-ship weapon-energy runtime. A missing or invalid
    /// powerplant intentionally provides zero capacity and no regeneration.
    /// Charge is transient combat state and is restored to full whenever a
    /// valid plant is mounted or a loadout is restored.
    /// </summary>
    public sealed class WeaponEnergy
    {
        public string MountedPowerplantId { get; private set; } = string.Empty;
        public PowerplantEquipmentDefinition Definition { get; private set; }
        public float MaxEnergy { get; private set; }
        public float CurrentEnergy { get; private set; }
        public float RegenRate { get; private set; }

        public bool HasMountedPowerplant => Definition != null &&
            Definition.IsValid &&
            EquipmentCatalog.GetById(Definition.Id) == Definition &&
            MaxEnergy > 0f &&
            RegenRate > 0f &&
            string.Equals(MountedPowerplantId, Definition.Id, StringComparison.OrdinalIgnoreCase);

        public bool IsDepleted => HasMountedPowerplant && CurrentEnergy <= 0f;

        public float EnergyPercentage => HasMountedPowerplant && MaxEnergy > 0f
            ? MathHelper.Clamp(CurrentEnergy / MaxEnergy, 0f, 1f)
            : 0f;

        public WeaponEnergy()
        {
            Clear();
        }

        public WeaponEnergy(PowerplantEquipmentDefinition definition)
        {
            Configure(definition, restoreFull: true);
        }

        /// <summary>
        /// Binds the runtime to a canonical powerplant. Invalid definitions
        /// become an unmounted zero-capacity state without throwing.
        /// </summary>
        public void Configure(PowerplantEquipmentDefinition definition, bool restoreFull = true)
        {
            if (definition == null || !definition.IsValid ||
                EquipmentCatalog.GetById(definition.Id) != definition ||
                string.IsNullOrWhiteSpace(definition.Id))
            {
                Clear();
                return;
            }

            MountedPowerplantId = definition.Id;
            Definition = definition;
            MaxEnergy = SanitizePositive(definition.EnergyCapacity);
            RegenRate = SanitizePositive(definition.EnergyRegenerationRate);
            if (MaxEnergy <= 0f || RegenRate <= 0f)
            {
                Clear();
                return;
            }

            CurrentEnergy = restoreFull
                ? MaxEnergy
                : SanitizeCharge(CurrentEnergy, MaxEnergy);
            NormalizeState();
        }

        public void Unmount()
        {
            Clear();
        }

        public void FullRestore()
        {
            CurrentEnergy = HasMountedPowerplant ? MaxEnergy : 0f;
            NormalizeState();
        }

        /// <summary>
        /// Test/debug and load-recovery seam that still clamps all input.
        /// Gameplay should normally spend and regenerate through this class.
        /// </summary>
        public void SetCurrentEnergy(float value)
        {
            CurrentEnergy = SanitizeCharge(value, MaxEnergy);
            NormalizeState();
        }

        public bool TrySpend(float amount)
        {
            if (!HasMountedPowerplant || !IsFinitePositive(amount) ||
                !IsFiniteNonNegative(CurrentEnergy) || amount > CurrentEnergy)
            {
                NormalizeState();
                return false;
            }

            CurrentEnergy = Math.Max(0f, CurrentEnergy - amount);
            NormalizeState();
            return true;
        }

        public void Update(GameTime gameTime, bool shipAlive = true)
        {
            if (!shipAlive || !HasMountedPowerplant || gameTime == null)
            {
                NormalizeState();
                return;
            }

            float elapsedSeconds = (float)gameTime.ElapsedGameTime.TotalSeconds;
            Regenerate(elapsedSeconds);
        }

        public void Regenerate(float elapsedSeconds)
        {
            if (!HasMountedPowerplant || !IsFinitePositive(elapsedSeconds))
            {
                NormalizeState();
                return;
            }

            double recharge = (double)RegenRate * elapsedSeconds;
            if (double.IsNaN(recharge) || double.IsInfinity(recharge) || recharge <= 0d)
            {
                NormalizeState();
                return;
            }

            CurrentEnergy = (float)Math.Min(MaxEnergy, (double)CurrentEnergy + recharge);
            NormalizeState();
        }

        public string GetHudText()
        {
            return $"ENERGY {CurrentEnergy:F0}/{MaxEnergy:F0}";
        }

        private void Clear()
        {
            MountedPowerplantId = string.Empty;
            Definition = null;
            MaxEnergy = 0f;
            RegenRate = 0f;
            CurrentEnergy = 0f;
        }

        private void NormalizeState()
        {
            if (!HasMountedPowerplant)
            {
                CurrentEnergy = 0f;
                MaxEnergy = 0f;
                RegenRate = 0f;
                MountedPowerplantId = string.Empty;
                Definition = null;
                return;
            }

            MaxEnergy = SanitizePositive(MaxEnergy);
            RegenRate = SanitizePositive(RegenRate);
            CurrentEnergy = SanitizeCharge(CurrentEnergy, MaxEnergy);
        }

        private static float SanitizePositive(float value) => IsFinitePositive(value) ? value : 0f;

        private static float SanitizeCharge(float value, float capacity)
        {
            if (!IsFiniteNonNegative(capacity) || capacity <= 0f ||
                !IsFiniteNonNegative(value))
            {
                return 0f;
            }

            return MathHelper.Clamp(value, 0f, capacity);
        }

        private static bool IsFinitePositive(float value) =>
            !float.IsNaN(value) && !float.IsInfinity(value) && value > 0f;

        private static bool IsFiniteNonNegative(float value) =>
            !float.IsNaN(value) && !float.IsInfinity(value) && value >= 0f;
    }
}
