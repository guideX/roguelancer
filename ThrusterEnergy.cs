using Microsoft.Xna.Framework;
using System;

namespace Roguelancer
{
    /// <summary>
    /// Bounded per-ship thruster-energy runtime. A missing or invalid
    /// thruster intentionally provides zero capacity, no regeneration, and no
    /// afterburn. Charge is transient combat state and restores to full when a
    /// valid thruster is mounted or a loadout is restored.
    /// </summary>
    public sealed class ThrusterEnergy
    {
        public string MountedThrusterId { get; private set; } = string.Empty;
        public ThrusterEquipmentDefinition Definition { get; private set; }
        public float MaxEnergy { get; private set; }
        public float CurrentEnergy { get; private set; }
        public float RegenRate { get; private set; }
        public float DrainRate { get; private set; }
        public float AfterburnSpeedMultiplier { get; private set; } = 2f;

        public bool HasMountedThruster => Definition != null &&
            Definition.IsValid &&
            EquipmentCatalog.GetById(Definition.Id) == Definition &&
            MaxEnergy > 0f &&
            RegenRate > 0f &&
            DrainRate > 0f &&
            string.Equals(MountedThrusterId, Definition.Id, StringComparison.OrdinalIgnoreCase);

        public bool IsDepleted => HasMountedThruster && CurrentEnergy <= 0f;

        public bool CanAfterburn(bool shipAlive = true) =>
            shipAlive && HasMountedThruster && IsFinitePositive(CurrentEnergy);

        public float EnergyPercentage => HasMountedThruster && MaxEnergy > 0f
            ? MathHelper.Clamp(CurrentEnergy / MaxEnergy, 0f, 1f)
            : 0f;

        public ThrusterEnergy()
        {
            Clear();
        }

        public ThrusterEnergy(ThrusterEquipmentDefinition definition)
        {
            Configure(definition, restoreFull: true);
        }

        /// <summary>
        /// Binds the runtime to a canonical mounted thruster. Invalid
        /// definitions become an unmounted zero-capacity state safely.
        /// </summary>
        public void Configure(ThrusterEquipmentDefinition definition, bool restoreFull = true)
        {
            if (definition == null || !definition.IsValid ||
                EquipmentCatalog.GetById(definition.Id) != definition ||
                string.IsNullOrWhiteSpace(definition.Id))
            {
                Clear();
                return;
            }

            MountedThrusterId = definition.Id;
            Definition = definition;
            MaxEnergy = SanitizePositive(definition.EnergyCapacity);
            RegenRate = SanitizePositive(definition.EnergyRegenerationRate);
            DrainRate = SanitizePositive(definition.AfterburnDrainRate);
            AfterburnSpeedMultiplier = SanitizeMultiplier(definition.AfterburnSpeedMultiplier);
            if (MaxEnergy <= 0f || RegenRate <= 0f || DrainRate <= 0f || AfterburnSpeedMultiplier <= 0f)
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
            CurrentEnergy = HasMountedThruster ? MaxEnergy : 0f;
            NormalizeState();
        }

        /// <summary>
        /// Test/debug and load-recovery seam that clamps all input.
        /// Gameplay normally advances charge through Advance.
        /// </summary>
        public void SetCurrentEnergy(float value)
        {
            CurrentEnergy = SanitizeCharge(value, MaxEnergy);
            NormalizeState();
        }

        /// <summary>
        /// Advances one authoritative charge step. When requested afterburn
        /// cannot actually consume charge (including at zero), the resource
        /// is considered idle and regenerates. The return value is whether
        /// afterburn remains usable after this step.
        /// </summary>
        public bool Advance(GameTime gameTime, bool afterburnRequested, bool shipAlive = true)
        {
            float elapsedSeconds = gameTime == null
                ? 0f
                : (float)gameTime.ElapsedGameTime.TotalSeconds;
            return Advance(elapsedSeconds, afterburnRequested, shipAlive);
        }

        public bool Advance(float elapsedSeconds, bool afterburnRequested, bool shipAlive = true)
        {
            if (!shipAlive || !HasMountedThruster)
            {
                NormalizeState();
                return false;
            }

            if (!IsFinitePositive(elapsedSeconds))
            {
                NormalizeState();
                return afterburnRequested && CanAfterburn(shipAlive);
            }

            if (afterburnRequested && CanAfterburn(shipAlive))
            {
                double drain = (double)DrainRate * elapsedSeconds;
                if (!double.IsNaN(drain) && !double.IsInfinity(drain) && drain > 0d)
                {
                    CurrentEnergy = (float)Math.Max(0d, (double)CurrentEnergy - drain);
                    NormalizeState();
                    return CanAfterburn(shipAlive);
                }
            }

            Regenerate(elapsedSeconds);
            return false;
        }

        public void Update(GameTime gameTime, bool afterburnActive = false, bool shipAlive = true)
        {
            Advance(gameTime, afterburnActive, shipAlive);
        }

        public void Regenerate(float elapsedSeconds)
        {
            if (!HasMountedThruster || !IsFinitePositive(elapsedSeconds))
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
            return $"THRUSTER {CurrentEnergy:F0}/{MaxEnergy:F0}";
        }

        private void Clear()
        {
            MountedThrusterId = string.Empty;
            Definition = null;
            MaxEnergy = 0f;
            RegenRate = 0f;
            DrainRate = 0f;
            AfterburnSpeedMultiplier = 2f;
            CurrentEnergy = 0f;
        }

        private void NormalizeState()
        {
            if (!HasMountedThruster)
            {
                CurrentEnergy = 0f;
                MaxEnergy = 0f;
                RegenRate = 0f;
                DrainRate = 0f;
                AfterburnSpeedMultiplier = 2f;
                MountedThrusterId = string.Empty;
                Definition = null;
                return;
            }

            MaxEnergy = SanitizePositive(MaxEnergy);
            RegenRate = SanitizePositive(RegenRate);
            DrainRate = SanitizePositive(DrainRate);
            AfterburnSpeedMultiplier = SanitizeMultiplier(AfterburnSpeedMultiplier);
            CurrentEnergy = SanitizeCharge(CurrentEnergy, MaxEnergy);
        }

        private static float SanitizePositive(float value) => IsFinitePositive(value) ? value : 0f;

        private static float SanitizeMultiplier(float value) =>
            !float.IsNaN(value) && !float.IsInfinity(value) && value >= 1f && value <= 3f
                ? value
                : 2f;

        private static float SanitizeCharge(float value, float capacity)
        {
            if (!IsFinitePositive(capacity) || !IsFiniteNonNegative(value))
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
