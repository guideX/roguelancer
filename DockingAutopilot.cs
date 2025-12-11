using Microsoft.Xna.Framework;
using System;

namespace Roguelancer
{
    /// <summary>
    /// Handles cinematic autopilot docking sequence
    /// </summary>
    public class DockingAutopilot
    {
        public enum DockingPhase
        {
            Inactive,
            Approaching,      // Moving to approach point
            Aligning,         // Rotating to face docking port
            FinalApproach,    // Moving into docking port
            Docked            // Docking complete
        }

        private DockingPhase _phase = DockingPhase.Inactive;
        private Station _targetStation = null;
        private Ship _ship = null;
        private Camera _camera = null;
        
        // Autopilot parameters
        private float _approachSpeed = 150f;
        private float _alignmentSpeed = 100f;
        private float _finalApproachSpeed = 50f;
        private float _rotationSpeed = 1.5f;
        
        // Camera parameters
        private Vector3 _cinematicCameraOffset = new Vector3(0, 50, 150);
        private Vector3 _originalCameraPosition;
        private bool _cinematicMode = false;
        private float _cinematicBlend = 0f;
        private float _cinematicBlendSpeed = 1.0f;

        public bool IsActive => _phase != DockingPhase.Inactive;
        public bool IsCinematicMode => _cinematicMode;
        public DockingPhase CurrentPhase => _phase;

        /// <summary>
        /// Event fired when docking is complete
        /// </summary>
        public event Action OnDockingComplete;

        public DockingAutopilot()
        {
        }

        /// <summary>
        /// Start the cinematic docking sequence
        /// </summary>
        public void StartDocking(Ship ship, Station station, Camera camera)
        {
            if (_phase != DockingPhase.Inactive) return;

            _ship = ship;
            _targetStation = station;
            _camera = camera;
            _phase = DockingPhase.Approaching;
            _cinematicMode = true;
            _cinematicBlend = 0f;
            _originalCameraPosition = camera.Position;

            Console.WriteLine($"[DOCK AUTOPILOT] Starting cinematic docking sequence to {station.Name}");
            Console.WriteLine($"[DOCK AUTOPILOT] Ship at: {ship.Position}");
            Console.WriteLine($"[DOCK AUTOPILOT] Station at: {station.Position}");
            Console.WriteLine($"[DOCK AUTOPILOT] Docking point: {station.GetDockingPoint()}");
        }

        /// <summary>
        /// Cancel the docking sequence
        /// </summary>
        public void Cancel()
        {
            if (_phase == DockingPhase.Inactive) return;

            Console.WriteLine("[DOCK AUTOPILOT] Docking cancelled");
            _phase = DockingPhase.Inactive;
            _cinematicMode = false;
            _cinematicBlend = 0f;
            _targetStation = null;
        }

        /// <summary>
        /// Update the autopilot docking sequence
        /// </summary>
        public void Update(GameTime gameTime)
        {
            if (_phase == DockingPhase.Inactive || _ship == null || _targetStation == null)
                return;

            float deltaTime = (float)gameTime.ElapsedGameTime.TotalSeconds;

            // Blend into cinematic camera
            if (_cinematicMode && _cinematicBlend < 1.0f)
            {
                _cinematicBlend = MathHelper.Clamp(_cinematicBlend + _cinematicBlendSpeed * deltaTime, 0f, 1f);
            }

            switch (_phase)
            {
                case DockingPhase.Approaching:
                    UpdateApproaching(deltaTime);
                    break;
                case DockingPhase.Aligning:
                    UpdateAligning(deltaTime);
                    break;
                case DockingPhase.FinalApproach:
                    UpdateFinalApproach(deltaTime);
                    break;
                case DockingPhase.Docked:
                    CompleteDocking();
                    break;
            }

            // Update cinematic camera
            if (_cinematicMode)
            {
                UpdateCinematicCamera(deltaTime);
            }
        }

        private void UpdateApproaching(float deltaTime)
        {
            Vector3 approachPoint = _targetStation.GetApproachPoint();
            Vector3 toApproach = approachPoint - _ship.Position;
            float distanceToApproach = toApproach.Length();

            if (distanceToApproach < 50f)
            {
                // Arrived at approach point, start alignment
                _phase = DockingPhase.Aligning;
                Console.WriteLine("[DOCK AUTOPILOT] Phase: Aligning with docking port");
                return;
            }

            // Move towards approach point
            Vector3 direction = Vector3.Normalize(toApproach);
            float speed = MathHelper.Clamp(_approachSpeed, 0, distanceToApproach / deltaTime);
            _ship.Position += direction * speed * deltaTime;

            // Gradually rotate to face approach point
            RotateShipTowards(direction, _rotationSpeed * 0.5f * deltaTime);
        }

        private void UpdateAligning(float deltaTime)
        {
            Vector3 dockingPoint = _targetStation.GetDockingPoint();
            Vector3 toDocking = dockingPoint - _ship.Position;
            Vector3 dockingDirection = Vector3.Normalize(toDocking);

            // Check if aligned (ship is facing the docking point)
            float alignment = Vector3.Dot(_ship.Forward, dockingDirection);
            
            if (alignment > 0.98f) // Within 11 degrees
            {
                // Aligned, begin final approach
                _phase = DockingPhase.FinalApproach;
                Console.WriteLine("[DOCK AUTOPILOT] Phase: Final approach to docking port");
                return;
            }

            // Rotate ship to face docking point
            RotateShipTowards(dockingDirection, _rotationSpeed * deltaTime);

            // Hold position with slight drift towards docking point
            _ship.Position += dockingDirection * _alignmentSpeed * 0.2f * deltaTime;
        }

        private void UpdateFinalApproach(float deltaTime)
        {
            Vector3 dockingPoint = _targetStation.GetDockingPoint();
            Vector3 toDocking = dockingPoint - _ship.Position;
            float distanceToDocking = toDocking.Length();

            if (distanceToDocking < 10f)
            {
                // Docking complete!
                _ship.Position = dockingPoint;
                _phase = DockingPhase.Docked;
                Console.WriteLine("[DOCK AUTOPILOT] Docking complete!");
                return;
            }

            // Move slowly towards docking point
            Vector3 direction = Vector3.Normalize(toDocking);
            float speed = MathHelper.Clamp(_finalApproachSpeed, 0, distanceToDocking / deltaTime);
            _ship.Position += direction * speed * deltaTime;

            // Fine-tune alignment
            RotateShipTowards(direction, _rotationSpeed * 0.5f * deltaTime);
        }

        private void CompleteDocking()
        {
            // Trigger docking complete event
            OnDockingComplete?.Invoke();
            
            // Reset state
            _phase = DockingPhase.Inactive;
            _cinematicMode = false;
            _cinematicBlend = 0f;
            _targetStation = null;
        }

        private void RotateShipTowards(Vector3 targetDirection, float rotationAmount)
        {
            // This is a simplified version - in full implementation,
            // you would call ship's rotation methods or set velocity towards target
            // For now, we just move the ship towards the target
            // The actual rotation can be handled by the existing ship control system
        }

        private void UpdateCinematicCamera(float deltaTime)
        {
            if (_camera == null || _ship == null) return;

            // Note: Camera update will be handled by the game's camera system
            // The camera will automatically follow the ship during docking
            // This method is kept for future cinematic camera enhancement
        }

        /// <summary>
        /// Get a message describing the current phase for HUD display
        /// </summary>
        public string GetPhaseMessage()
        {
            return _phase switch
            {
                DockingPhase.Approaching => "AUTOPILOT: Approaching station...",
                DockingPhase.Aligning => "AUTOPILOT: Aligning with docking port...",
                DockingPhase.FinalApproach => "AUTOPILOT: Final approach...",
                DockingPhase.Docked => "AUTOPILOT: Docking complete",
                _ => ""
            };
        }
    }
}
