using Microsoft.Xna.Framework;
using System;

namespace Roguelancer
{
    /// <summary>
    /// Freelancer-style chase camera that follows the player ship
    /// </summary>
    public class Camera
    {
        public Matrix View { get; private set; }
        public Matrix Projection { get; private set; }
        public Vector3 Position { get; private set; }

        // Camera views
        private enum CameraView { Cockpit, Turret, Rear }
        private CameraView _currentView = CameraView.Cockpit;

        // Turret view state
        private float _turretYaw = 0f;
        private float _turretPitch = 0f;
        private const float MaxTurretPitch = MathHelper.PiOver2 - 0.1f;

        // Shake effect state
        private float _shakeMagnitude = 0f;
        private float _shakeFrequency = 0f;
        private float _shakeTimer = 0f;

        public bool IsTurretViewActive => _currentView == CameraView.Turret;

        public Camera(float aspectRatio)
        {
            Projection = Matrix.CreatePerspectiveFieldOfView(
                MathHelper.ToRadians(60), // 60-degree FOV
                aspectRatio,
                0.1f,       // Near clip plane
                100000f);   // Far clip plane (very long for space)
        }

        public void Follow(Vector3 targetPosition, Vector3 targetForward, Vector3 targetUp, float followSpeed)
        {
            // Base cockpit view position (behind and slightly above the ship)
            Vector3 desiredPosition = targetPosition - targetForward * 12f + targetUp * 4f;

            // Apply shake offset
            Vector3 shakeOffset = Vector3.Zero;
            if (_shakeMagnitude > 0)
            {
                shakeOffset = new Vector3(
                    (float)(Math.Sin(_shakeTimer * _shakeFrequency) * _shakeMagnitude),
                    (float)(Math.Cos(_shakeTimer * _shakeFrequency * 1.2) * _shakeMagnitude),
                    (float)(Math.Sin(_shakeTimer * _shakeFrequency * 0.8) * _shakeMagnitude)
                );
            }

            Position = Vector3.Lerp(Position, desiredPosition, followSpeed) + shakeOffset;

            // Determine look-at target and up direction based on view mode
            Vector3 lookAtTarget;
            Vector3 upDirection = targetUp;

            switch (_currentView)
            {
                case CameraView.Turret:
                    // Turret view: Look in a direction controlled by mouse/keys
                    Matrix turretRotation = Matrix.CreateFromYawPitchRoll(_turretYaw, _turretPitch, 0);
                    Vector3 lookDirection = Vector3.Transform(targetForward, turretRotation);
                    lookAtTarget = Position + lookDirection;
                    break;

                case CameraView.Rear:
                    // Rear view: Look back at the ship from in front of it
                    Position = targetPosition + targetForward * 25f + targetUp * 6f; // Position in front of the ship
                    lookAtTarget = targetPosition; // Look back at the ship
                    break;

                case CameraView.Cockpit:
                default:
                    // Standard cockpit view: Look forward from the camera position
                    lookAtTarget = Position + targetForward;
                    break;
            }

            View = Matrix.CreateLookAt(Position, lookAtTarget, upDirection);
        }

        public void UpdateShake(float deltaTime)
        {
            if (_shakeMagnitude > 0)
            {
                _shakeTimer += deltaTime;
            }
        }

        public void AddShake(float magnitude, float frequency)
        {
            _shakeMagnitude = Math.Max(_shakeMagnitude, magnitude);
            _shakeFrequency = Math.Max(_shakeFrequency, frequency);
        }

        public void ToggleTurretView()
        {
            if (_currentView == CameraView.Turret)
            {
                _currentView = CameraView.Cockpit;
            }
            else
            {
                _currentView = CameraView.Turret;
                _turretYaw = 0;
                _turretPitch = 0;
            }
        }

        public void SetRearView()
        {
            _currentView = CameraView.Rear;
        }

        public void CycleView()
        {
            _currentView = (CameraView)(((int)_currentView + 1) % 3); // Cycles through Cockpit, Turret, Rear
            if (_currentView == CameraView.Turret)
            {
                _turretYaw = 0;
                _turretPitch = 0;
            }
        }

        public string GetCurrentViewName()
        {
            switch (_currentView)
            {
                case CameraView.Cockpit: return "Cockpit View";
                case CameraView.Turret: return "Turret View";
                case CameraView.Rear: return "Rear View";
                default: return "Unknown View";
            }
        }

        public void UpdateTurretView(float yawDelta, float pitchDelta)
        {
            if (IsTurretViewActive)
            {
                _turretYaw += yawDelta;
                _turretPitch = MathHelper.Clamp(_turretPitch + pitchDelta, -MaxTurretPitch, MaxTurretPitch);
            }
        }
    }
}
