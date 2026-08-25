using Microsoft.Xna.Framework;
using System;

namespace Roguelancer
{
    /// <summary>
    /// The one authoritative transient cruise-drive state machine used by
    /// player and NPC ships. Cruise is intrinsic flight behavior; it does not
    /// consume weapon or thruster energy.
    /// </summary>
    public sealed class CruiseDrive
    {
        public const float DefaultChargeDuration = 3f;
        public const float DefaultCooldownDuration = 5f;
        public const float DefaultSpeedMultiplier = 3.5f;
        public const float DefaultAccelerationMultiplier = 4f;
        private const float MaximumAcceptedElapsedSeconds = 60f;

        private readonly float _chargeDuration;
        private readonly float _cooldownDuration;
        private readonly float _speedMultiplier;
        private readonly float _accelerationMultiplier;
        private float _chargeElapsed;
        private float _cooldownRemaining;

        public CruiseDrive(
            float chargeDuration = DefaultChargeDuration,
            float cooldownDuration = DefaultCooldownDuration,
            float speedMultiplier = DefaultSpeedMultiplier,
            float accelerationMultiplier = DefaultAccelerationMultiplier)
        {
            _chargeDuration = SanitizePositive(chargeDuration, DefaultChargeDuration);
            _cooldownDuration = SanitizePositive(cooldownDuration, DefaultCooldownDuration);
            _speedMultiplier = SanitizePositive(speedMultiplier, DefaultSpeedMultiplier);
            _accelerationMultiplier = SanitizePositive(accelerationMultiplier, DefaultAccelerationMultiplier);
            Reset();
        }

        public CruiseDriveState State { get; private set; } = CruiseDriveState.Inactive;
        public float ChargeDuration => _chargeDuration;
        public float CooldownDuration => _cooldownDuration;
        public float SpeedMultiplier => _speedMultiplier;
        public float AccelerationMultiplier => _accelerationMultiplier;
        public float ChargeElapsed => SanitizeTimer(_chargeElapsed);
        public float CooldownRemaining => SanitizeTimer(_cooldownRemaining);
        public bool IsCharging => State == CruiseDriveState.Charging;
        public bool IsActive => State == CruiseDriveState.Active;
        public bool IsCoolingDown => State == CruiseDriveState.Cooldown;
        public bool IsChargingOrActive => IsCharging || IsActive;

        /// <summary>
        /// Guns are blocked only after cruise becomes active. Charging remains
        /// combat-capable, which keeps charge-up behavior minimally invasive.
        /// </summary>
        public bool BlocksStandardWeapons => IsActive;

        public float ChargeProgress
        {
            get
            {
                if (IsActive) return 1f;
                if (!IsCharging || _chargeDuration <= 0f) return 0f;
                return MathHelper.Clamp(ChargeElapsed / _chargeDuration, 0f, 1f);
            }
        }

        public bool CanActivate(bool shipAlive = true, bool docked = false, bool validMovement = true)
        {
            NormalizeState();
            return State == CruiseDriveState.Inactive && shipAlive && !docked && validMovement;
        }

        /// <summary>
        /// Starts a fresh deterministic charge. Repeated requests do not reset
        /// or accelerate a charge, and cooldown cannot be bypassed.
        /// </summary>
        public bool TryActivate(bool shipAlive = true, bool docked = false, bool validMovement = true)
        {
            if (!CanActivate(shipAlive, docked, validMovement))
                return false;

            State = CruiseDriveState.Charging;
            _chargeElapsed = 0f;
            _cooldownRemaining = 0f;
            return true;
        }

        public bool ToggleActivation(bool shipAlive = true, bool docked = false, bool validMovement = true)
        {
            NormalizeState();
            if (IsChargingOrActive)
            {
                return Cancel(CruiseCancellationReason.Manual);
            }

            return TryActivate(shipAlive, docked, validMovement);
        }

        /// <summary>
        /// Cancels charge or active cruise and applies the standard cooldown.
        /// Harmless duplicate cancellation does not reset or extend cooldown.
        /// </summary>
        public bool Cancel(CruiseCancellationReason reason = CruiseCancellationReason.Manual)
        {
            NormalizeState();
            if (!IsChargingOrActive)
                return false;

            State = CruiseDriveState.Cooldown;
            _chargeElapsed = 0f;
            _cooldownRemaining = _cooldownDuration;
            return true;
        }

        /// <summary>
        /// A positive hostile combat result disrupts both charging and active
        /// cruise, including shield-only hits. Non-hostile, zero, NaN, and
        /// infinite results are intentionally ignored.
        /// </summary>
        public bool DisruptByDamage(float damage, bool hostile = true)
        {
            if (!hostile || !IsFinitePositive(damage))
                return false;

            return Cancel(CruiseCancellationReason.HostileDamage);
        }

        public void Advance(GameTime gameTime, bool shipAlive = true, bool docked = false, bool validMovement = true)
        {
            if (gameTime == null)
            {
                return;
            }

            Advance((float)gameTime.ElapsedGameTime.TotalSeconds, shipAlive, docked, validMovement);
        }

        public void Advance(float elapsedSeconds, bool shipAlive = true, bool docked = false, bool validMovement = true)
        {
            NormalizeState();

            if (!shipAlive)
            {
                Reset();
                return;
            }

            float deltaTime = SanitizeElapsed(elapsedSeconds);
            if (IsCoolingDown)
            {
                _cooldownRemaining = Math.Max(0f, CooldownRemaining - deltaTime);
                if (_cooldownRemaining <= 0f)
                {
                    State = CruiseDriveState.Inactive;
                    _cooldownRemaining = 0f;
                }
                return;
            }

            if (docked || !validMovement)
            {
                Cancel(CruiseCancellationReason.IncompatibleFlight);
                return;
            }

            if (!IsCharging)
            {
                return;
            }

            _chargeElapsed = Math.Min(_chargeDuration, ChargeElapsed + deltaTime);
            if (_chargeElapsed >= _chargeDuration)
            {
                State = CruiseDriveState.Active;
                _chargeElapsed = _chargeDuration;
            }
        }

        public float GetEffectiveSpeed(float normalMaxSpeed, float configuredCruiseSpeed = 0f, float validationMultiplier = 1f)
        {
            float normalSpeed = SanitizeNonNegative(normalMaxSpeed);
            float configuredSpeed = SanitizeNonNegative(configuredCruiseSpeed);
            float policySpeed = Math.Max(normalSpeed * _speedMultiplier, configuredSpeed);
            return policySpeed * MathHelper.Clamp(SanitizePositive(validationMultiplier, 1f), 1f, 20f);
        }

        public string GetHudText()
        {
            NormalizeState();
            return State switch
            {
                CruiseDriveState.Charging => $"CRUISE CHARGING {ChargeProgress * 100f:F0}%",
                CruiseDriveState.Active => "CRUISE ACTIVE",
                CruiseDriveState.Cooldown => $"CRUISE COOLDOWN {CooldownRemaining:F1}",
                _ => "CRUISE READY"
            };
        }

        public void Reset()
        {
            State = CruiseDriveState.Inactive;
            _chargeElapsed = 0f;
            _cooldownRemaining = 0f;
        }

        private void NormalizeState()
        {
            if (!Enum.IsDefined(typeof(CruiseDriveState), State))
            {
                Reset();
                return;
            }

            _chargeElapsed = SanitizeTimer(_chargeElapsed);
            _cooldownRemaining = SanitizeTimer(_cooldownRemaining);

            if (State == CruiseDriveState.Charging && _chargeElapsed >= _chargeDuration)
            {
                State = CruiseDriveState.Active;
                _chargeElapsed = _chargeDuration;
            }
            else if (State == CruiseDriveState.Cooldown && _cooldownRemaining <= 0f)
            {
                State = CruiseDriveState.Inactive;
            }
        }

        private static float SanitizeElapsed(float value)
        {
            if (float.IsNaN(value) || value < 0f) return 0f;
            if (float.IsInfinity(value)) return MaximumAcceptedElapsedSeconds;
            return Math.Min(value, MaximumAcceptedElapsedSeconds);
        }

        private static float SanitizeTimer(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value) || value < 0f) return 0f;
            return Math.Min(value, MaximumAcceptedElapsedSeconds);
        }

        private static float SanitizePositive(float value, float fallback)
        {
            return IsFinitePositive(value) ? value : fallback;
        }

        private static float SanitizeNonNegative(float value)
        {
            return float.IsNaN(value) || float.IsInfinity(value) || value < 0f ? 0f : value;
        }

        private static bool IsFinitePositive(float value) =>
            !float.IsNaN(value) && !float.IsInfinity(value) && value > 0f;
    }

    public enum CruiseDriveState
    {
        Inactive,
        Charging,
        Active,
        Cooldown
    }

    public enum CruiseCancellationReason
    {
        Manual,
        HostileDamage,
        AfterburnerActivation,
        IncompatibleFlight,
        Docking,
        Destruction,
        TargetInvalid,
        Reset
    }
}
