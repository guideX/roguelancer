// Roguelancer 0.1 Pre Alpha by Leon Aiossa
// http://team-nexgen.com
using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Roguelancer.Interfaces;
using Roguelancer.Models;
using Roguelancer.Helpers;

namespace Roguelancer.Functionality {
    /// <summary>
    /// Game Camera
    /// </summary>
    public class GameCamera : IGameCamera {
        #region "public variables"
        /// <summary>
        /// Game Camera Model
        /// </summary>
        public GameCameraModel Model { get; set; }
        #endregion
        #region "public functions"
        /// <summary>
        /// Game Camera
        /// </summary>
        public GameCamera() {
            Model = new GameCameraModel();
        }
        /// <summary>
        /// Initialize
        /// </summary>
        /// <param name="game"></param>
        public void Initialize(RoguelancerGame game) {
            game.Settings.CameraSettings.Model.AspectRatio = (float)game.GraphicsDevice.Viewport.Width / game.GraphicsDevice.Viewport.Height;
            UpdateCameraChaseTarget(game);
            Reset(game);
        }
        /// <summary>
        /// Update
        /// </summary>
        /// <param name="game"></param>
        public void Update(RoguelancerGame game) {
            UpdateCameraChaseTarget(game);
            if (game.GameTime == null) {
                throw new ArgumentNullException("gameTime");
            }
            if (Model.Shaking) {
                float magnitude;
                if (Model.ShakeUseDuration) {
                    Model.ShakeTimer += (float)game.GameTime.ElapsedGameTime.TotalSeconds;
                    if (Model.ShakeTimer >= Model.ShakeDuration) {
                        Model.Shaking = false;
                        Model.ShakeTimer = Model.ShakeDuration;
                        var progress = Model.ShakeTimer / Model.ShakeDuration;
                        magnitude = Model.ShakeMagnitude * (1f - (progress * progress));
                    }
                } else {
                    magnitude = Model.ShakeMagnitude * (1f - (2f));
                    Model.ShakeOffset = new Vector3((float)Model.ShakeRandom.NextDouble() * 2f - 1f, (float)Model.ShakeRandom.NextDouble() * 2f - 1f, (float)Model.ShakeRandom.NextDouble() * 2f - 1f) * magnitude;
                    Model.Position += Model.ShakeOffset;
                }
            }
            UpdateWorldPositions(game);
            var elapsed = (float)game.GameTime.ElapsedGameTime.TotalSeconds;
            var stretch = Model.Position - Model.DesiredPosition;
            var force = -game.Settings.CameraSettings.Model.Stiffness * stretch - game.Settings.CameraSettings.Model.Damping * Model.Velocity;
            var acceleration = force / game.Settings.CameraSettings.Model.Mass;
            Model.Velocity += acceleration * elapsed;
            Model.Position += Model.Velocity * elapsed;
            UpdateMatrices(game);
        }
        /// <summary>
        /// Shake
        /// </summary>
        /// <param name="magnitude"></param>
        /// <param name="duration"></param>
        /// <param name="shakeUseDuration"></param>
        public void Shake(float magnitude, float duration, bool shakeUseDuration) {
            Model.Shaking = true;
            Model.ShakeMagnitude = magnitude;
            Model.ShakeUseDuration = shakeUseDuration;
            if (Model.ShakeUseDuration) {
                Model.ShakeDuration = duration;
                Model.ShakeTimer = 0f;
            }
        }
        /// <summary>
        /// Stop Shaking
        /// </summary>
        public void StopShaking() {
            Model.Shaking = false;
        }
        #endregion
        #region "private functions"
        /// <summary>
        /// Update World Positions
        /// </summary>
        /// <param name="game"></param>
        private void UpdateWorldPositions(RoguelancerGame game) {
            Matrix transform = Matrix.Identity;
            transform.Forward = Model.ChaseDirection;
            transform.Up = Model.Up;
            transform.Right = Vector3.Cross(Model.Up, Model.ChaseDirection);
            switch (Model.Mode) {
                case Enum.GameCameraModeEnum.Mode0:
                    Model.DesiredPosition = Model.ChasePosition + Vector3.TransformNormal(game.Settings.CameraSettings.Model.DesiredPositionOffset, transform);
                    Model.LookAt = Model.ChasePosition + Vector3.TransformNormal(game.Settings.CameraSettings.Model.LookAtOffset, transform);
                    break;
                case Enum.GameCameraModeEnum.Mode1:
                    break;
                case Enum.GameCameraModeEnum.Mode2:
                    Model.DesiredPosition = Model.ChasePosition + Vector3.TransformNormal(game.Settings.CameraSettings.Model.DesiredPositionOffset, transform);
                    Model.LookAt = Model.ChasePosition + game.Settings.CameraSettings.Model.ThrustViewAmount * Model.ChaseDirection;
                    break;
            }
        }
        /// <summary>
        /// Update New Camera
        /// </summary>
        /// <param name="_X"></param>
        /// <param name="_Y"></param>
        /// <param name="game"></param>
        private void UpdateNewCamera(float x, float y, RoguelancerGame game) {
            if (Model.Mode != Enum.GameCameraModeEnum.Mode1) {
                return;
            }
            var transform = Matrix.Identity;
            transform.Forward = Model.ChaseDirection;
            transform.Up = Model.Up;
            transform.Right = Vector3.Cross(Model.Up, Model.ChaseDirection);
            var o = Model.ChasePosition + Vector3.TransformNormal(game.Settings.CameraSettings.Model.DesiredPositionOffset, transform);
            var d = Model.ChasePosition + Vector3.TransformNormal(game.Settings.CameraSettings.Model.LookAtOffset, transform);
            Model.View = Matrix.CreateLookAt(o, d, Model.Up);
            Model.Projection = Matrix.CreatePerspectiveFieldOfView(game.Settings.CameraSettings.Model.FieldOfView, game.Settings.CameraSettings.Model.AspectRatio, game.Settings.CameraSettings.Model.NearPlaneDistance, game.Settings.CameraSettings.Model.ClippingDistance); //Builds a perspective projection matrix based on a field of view
            var f = new BoundingFrustum(Model.View * Model.Projection);
            var fcorners = new Vector3[8];
            f.GetCorners(fcorners);
            var _Fnx = fcorners[1] - fcorners[0];
            var _Fny = fcorners[3] - fcorners[0];
            var _MousePos = fcorners[0] + (x * _Fnx) + (y * _Fny);
            var _MouseDir = _MousePos - o;
            Model.DesiredPosition = o;
            Model.LookAt = d;
            Vector3 v, v2 = (d - o);
            Vector3.Normalize(ref v2, out v);
            var p = new Plane(v, -Vector3.Dot(v, d));
            var r = new Ray(o, _MouseDir);
            var t = r.Intersects(p);
            if (t != null) {
                v = o + (float)t * _MouseDir;
                v = (v - d);
                Model.LookAt = d + v / game.Settings.CameraSettings.Model.LookAtDivideBy;
            }
        }
        /// <summary>
        /// Update Matrices
        /// </summary>
        /// <param name="game"></param>
        private void UpdateMatrices(RoguelancerGame game) {
            Model.View = Matrix.CreateLookAt(Model.Position, Model.LookAt, Model.Up);
            Model.Projection = Matrix.CreatePerspectiveFieldOfView(game.Settings.CameraSettings.Model.FieldOfView, game.Settings.CameraSettings.Model.AspectRatio, game.Settings.CameraSettings.Model.NearPlaneDistance, game.Settings.CameraSettings.Model.ClippingDistance);
        }
        /// <summary>
        /// Reset
        /// </summary>
        /// <param name="game"></param>
        private void Reset(RoguelancerGame game) {
            UpdateWorldPositions(game);
            Model.Velocity = Vector3.Zero;
            Model.Position = Model.DesiredPosition;
            UpdateMatrices(game);
        }
        /// <summary>
        /// Update Camera Chase Target
        /// </summary>
        /// <param name="game"></param>
        private void UpdateCameraChaseTarget(RoguelancerGame game) {
            var playerShip = ShipHelper.GetPlayerShip(game); // Get Player Ship
            Model.ChasePosition = playerShip.Model.Position;
            Model.ChaseDirection = playerShip.Model.Direction;
            Model.Up = playerShip.Model.Up;
            if (Model.Mode == Enum.GameCameraModeEnum.Mode1) {
                if (game.Input.InputItems.Mouse.State.LeftButton == ButtonState.Pressed) {
                    UpdateNewCamera((float)game.Input.InputItems.Mouse.State.X / (float)game.GraphicsDevice.Viewport.Width, (float)game.Input.InputItems.Mouse.State.Y / (float)game.Graphics.Model.GraphicsDeviceManager.GraphicsDevice.Viewport.Height, game);
                } else {
                    UpdateNewCamera(game.Settings.CameraSettings.Model.NewCameraX, game.Settings.CameraSettings.Model.NewCameraY, game);
                }
            }
        }
        #endregion
    }
}