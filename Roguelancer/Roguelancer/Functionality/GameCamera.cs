// Roguelancer 0.1 Pre Alpha by Leon Aiossa
// http://www.team-nexgen.org
using System;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Roguelancer.Interfaces;
namespace Roguelancer.Functionality {
    /// <summary>
    /// Game Camera
    /// </summary>
    public class GameCamera : IGameCamera {
        #region "public variables"
        /// <summary>
        /// Projection
        /// </summary>
        public Matrix Projection { get; set; }
        /// <summary>
        /// View
        /// </summary>
        public Matrix View { get; set; }
        #endregion
        #region "private variables"
        /// <summary>
        /// Mode
        /// </summary>
        private int _mode;
        /// <summary>
        /// Chase Position
        /// </summary>
        private Vector3 _chasePosition;
        /// <summary>
        /// Chase Direction
        /// </summary>
        private Vector3 _chaseDirection;
        /// <summary>
        /// Look At
        /// </summary>
        private Vector3 _lookAt;
        /// <summary>
        /// Position
        /// </summary>
        private Vector3 _position;
        /// <summary>
        /// Up
        /// </summary>
        private Vector3 _up = Vector3.Up;
        /// <summary>
        /// Desired Position
        /// </summary>
        private Vector3 _desiredPosition;
        /// <summary>
        /// Velocity
        /// </summary>
        private Vector3 _velocity;
        /// <summary>
        /// Shake Offset
        /// </summary>
        private Vector3 _shakeOffset;
        /// <summary>
        /// Shake Magnitude
        /// </summary>
        private float _shakeMagnitude;
        /// <summary>
        /// Shake Duration
        /// </summary>
        private float _shakeDuration;
        /// <summary>
        /// Shake Timer
        /// </summary>
        private float _shakeTimer;
        /// <summary>
        /// Shaking
        /// </summary>
        private bool _shaking;
        /// <summary>
        /// Shake Use Duration
        /// </summary>
        private bool _shakeUseDuration;
        /// <summary>
        /// Shake Random
        /// </summary>
        private Random _shakeRandom;
        #endregion
        #region "public functions"
        /// <summary>
        /// Game Camera
        /// </summary>
        public GameCamera() {
            try {
                _mode = 2;
                _shakeRandom = new Random();
            } catch {
                throw;
            }
        }
        /// <summary>
        /// Initialize
        /// </summary>
        /// <param name="game"></param>
        public void Initialize(RoguelancerGame game) {
            try {
                game.Settings.CameraSettings.AspectRatio = (float)game.Graphics.GraphicsDeviceManager.GraphicsDevice.Viewport.Width / game.Graphics.GraphicsDeviceManager.GraphicsDevice.Viewport.Height;
                UpdateCameraChaseTarget(game);
                Reset(game);
            } catch {
                throw;
            }
        }
        /// <summary>
        /// Load Content
        /// </summary>
        /// <param name="game"></param>
        public void LoadContent(RoguelancerGame game) { }
        /// <summary>
        /// Update
        /// </summary>
        /// <param name="game"></param>
        public void Update(RoguelancerGame game) {
            try {
                UpdateCameraChaseTarget(game);
                if (game.GameTime == null) {
                    throw new ArgumentNullException("gameTime");
                }
                if (_shaking) {
                    float magnitude;
                    if (_shakeUseDuration == true) {
                        _shakeTimer += (float)game.GameTime.ElapsedGameTime.TotalSeconds;
                        if (_shakeTimer >= _shakeDuration) {
                            _shaking = false;
                            _shakeTimer = _shakeDuration;
                            float lProgress = _shakeTimer / _shakeDuration;
                            magnitude = _shakeMagnitude * (1f - (lProgress * lProgress));
                        }
                    } else {
                        magnitude = _shakeMagnitude * (1f - (2f));
                        _shakeOffset = new Vector3((float)_shakeRandom.NextDouble() * 2f - 1f, (float)_shakeRandom.NextDouble() * 2f - 1f, (float)_shakeRandom.NextDouble() * 2f - 1f) * magnitude;
                        _position += _shakeOffset;
                    }
                }
                UpdateWorldPositions(game);
                var elapsed = (float)game.GameTime.ElapsedGameTime.TotalSeconds;
                var _Stretch = _position - _desiredPosition;
                var _Force = -game.Settings.CameraSettings.Stiffness * _Stretch - game.Settings.CameraSettings.Damping * _velocity;
                var _Acceleration = _Force / game.Settings.CameraSettings.Mass;
                _velocity += _Acceleration * elapsed;
                _position += _velocity * elapsed;
                UpdateMatrices(game);
            } catch {
                throw;
            }
        }
        /// <summary>
        /// Draw
        /// </summary>
        /// <param name="game"></param>
        public void Draw(RoguelancerGame game) { }
        /// <summary>
        /// Shake
        /// </summary>
        /// <param name="magnitude"></param>
        /// <param name="duration"></param>
        /// <param name="shakeUseDuration"></param>
        public void Shake(float magnitude, float duration, bool shakeUseDuration) {
            try {
                _shaking = true;
                _shakeMagnitude = magnitude;
                _shakeUseDuration = shakeUseDuration;
                if (_shakeUseDuration == true) {
                    _shakeDuration = duration;
                    _shakeTimer = 0f;
                }
            } catch {
                throw;
            }
        }
        /// <summary>
        /// Stop Shaking
        /// </summary>
        public void StopShaking() {
            try {
                _shaking = false;
            } catch {
                throw;
            }
        }
        #endregion
        #region "private functions"
        /// <summary>
        /// Update World Positions
        /// </summary>
        /// <param name="game"></param>
        private void UpdateWorldPositions(RoguelancerGame game) {
            try {
                Matrix transform = Matrix.Identity;
                transform.Forward = _chaseDirection;
                transform.Up = _up;
                transform.Right = Vector3.Cross(_up, _chaseDirection);
                if (_mode == 0) {
                    _desiredPosition = _chasePosition + Vector3.TransformNormal(game.Settings.CameraSettings.DesiredPositionOffset, transform);
                    _lookAt = _chasePosition + Vector3.TransformNormal(game.Settings.CameraSettings.LookAtOffset, transform);
                }
                if (_mode == 2) {
                    _desiredPosition = _chasePosition + Vector3.TransformNormal(game.Settings.CameraSettings.DesiredPositionOffset, transform);
                    _lookAt = _chasePosition + game.Settings.CameraSettings.ThrustViewAmount * _chaseDirection;
                }
            } catch {
                throw;
            }
        }
        /// <summary>
        /// Update New Camera
        /// </summary>
        /// <param name="_X"></param>
        /// <param name="_Y"></param>
        /// <param name="game"></param>
        private void UpdateNewCamera(float _X, float _Y, RoguelancerGame game) {
            try {
                if (_mode != 1) {
                    return;
                }
                Matrix _Transform = Matrix.Identity;
                _Transform.Forward = _chaseDirection;
                _Transform.Up = _up;
                _Transform.Right = Vector3.Cross(_up, _chaseDirection);
                var o = _chasePosition + Vector3.TransformNormal(game.Settings.CameraSettings.DesiredPositionOffset, _Transform);
                var d = _chasePosition + Vector3.TransformNormal(game.Settings.CameraSettings.LookAtOffset, _Transform);
                View = Matrix.CreateLookAt(o, d, _up);
                Projection = Matrix.CreatePerspectiveFieldOfView(game.Settings.CameraSettings.FieldOfView, game.Settings.CameraSettings.AspectRatio, game.Settings.CameraSettings.NearPlaneDistance, game.Settings.CameraSettings.ClippingDistance); //Builds a perspective projection matrix based on a field of view
                var f = new BoundingFrustum(this.View * this.Projection);
                Vector3[] _Fcorners = new Vector3[8];
                f.GetCorners(_Fcorners);
                Vector3 _Fnx = _Fcorners[1] - _Fcorners[0];
                Vector3 _Fny = _Fcorners[3] - _Fcorners[0];
                Vector3 _MousePos = _Fcorners[0] + (_X * _Fnx) + (_Y * _Fny);
                Vector3 _MouseDir = _MousePos - o;
                _desiredPosition = o;
                _lookAt = d;
                Vector3 v, v2 = (d - o);
                Vector3.Normalize(ref v2, out v);
                Plane p = new Plane(v, -Vector3.Dot(v, d));
                Ray r = new Ray(o, _MouseDir);
                float? t = r.Intersects(p);
                if (t != null) {
                    v = o + (float)t * _MouseDir;
                    v = (v - d);
                    _lookAt = d + v / game.Settings.CameraSettings.LookAtDivideBy;
                }
            } catch {
                throw;
            }
        }
        /// <summary>
        /// Update Matrices
        /// </summary>
        /// <param name="game"></param>
        private void UpdateMatrices(RoguelancerGame game) {
            try {
                View = Matrix.CreateLookAt(_position, _lookAt, _up);
                Projection = Matrix.CreatePerspectiveFieldOfView(game.Settings.CameraSettings.FieldOfView, game.Settings.CameraSettings.AspectRatio, game.Settings.CameraSettings.NearPlaneDistance, game.Settings.CameraSettings.ClippingDistance);
            } catch {
                throw;
            }
        }
        /// <summary>
        /// Reset
        /// </summary>
        /// <param name="game"></param>
        private void Reset(RoguelancerGame game) {
            try {
                UpdateWorldPositions(game);
                _velocity = Vector3.Zero;
                _position = _desiredPosition;
                UpdateMatrices(game);
            } catch {
                throw;
            }
        }
        /// <summary>
        /// Update Camera Chase Target
        /// </summary>
        /// <param name="game"></param>
        private void UpdateCameraChaseTarget(RoguelancerGame game) {
            try {
                var ship = game.Objects.Ships.Ships.Where(s => s.PlayerShipControl.UseInput).LastOrDefault();
                //game.DebugText.Text = ship.model.Position.X.ToString() + " - " + ship.model.Position.Y.ToString() + " - " + ship.model.Position.Z.ToString();
                var gd = game.Graphics.GraphicsDeviceManager.GraphicsDevice;
                _chasePosition = ship.Model.Position;
                _chaseDirection = ship.Model.Direction;
                _up = ship.Model.Up;
                if (_mode == 1) {
                    if (game.Input.InputItems.Mouse.State.LeftButton == ButtonState.Pressed) {
                        UpdateNewCamera((float)game.Input.InputItems.Mouse.State.X / (float)gd.Viewport.Width, (float)game.Input.InputItems.Mouse.State.Y / (float)gd.Viewport.Height, game);
                    } else {
                        UpdateNewCamera(game.Settings.CameraSettings.NewCameraX, game.Settings.CameraSettings.NewCameraY, game);
                    }
                }
            } catch {
                throw;
            }
        }
        #endregion
    }
}