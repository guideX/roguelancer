// Roguelancer 0.1 Pre Alpha by Leon Aiossa
// http://www.team-nexgen.org
using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Graphics;
using Roguelancer.Objects;
using Roguelancer.Interfaces;
namespace Roguelancer.Functionality {
    /// <summary>
    /// Game Camera
    /// </summary>
    public class GameCamera : IGame {

        public Matrix Projection;
        public Matrix View;
        private int Mode = 2;
        private Vector3 ChasePosition;
        private Vector3 ChaseDirection;
        private Vector3 LookAt;
        private Vector3 Position;
        private Vector3 Up = Vector3.Up;
        private Vector3 DesiredPosition;
        private Vector3 Velocity;
        private Vector3 ShakeOffset;
        private float ShakeMagnitude;
        private float ShakeDuration;
        private float ShakeTimer;
        private bool Shaking;
        private bool ShakeUseDuration;
        private static readonly Random shakeRandom = new Random();
        private float NextShakeFloat() {
            return (float)shakeRandom.NextDouble() * 2f - 1f;
        }
        public void StopShaking() {
            Shaking = false;
        }
        public void Shake(float magnitude, float duration, bool _shakeUseDuration) {
            Shaking = true;
            ShakeMagnitude = magnitude;
            ShakeUseDuration = _shakeUseDuration;
            if(ShakeUseDuration == true) {
                ShakeDuration = duration;
                ShakeTimer = 0f;
            }
        }
        public void Initialize(RoguelancerGame _Game) {
            _Game.Settings.cameraSettings.aspectRatio = _Game.Graphics.ScreenDimensions();
            UpdateCameraChaseTarget(_Game);
            Reset(_Game);
        }
        public void UpdateWorldPositions(RoguelancerGame _Game) {
            Matrix transform = Matrix.Identity;
            transform.Forward = ChaseDirection;
            transform.Up = Up;
            transform.Right = Vector3.Cross(Up, ChaseDirection);
            if (Mode == 0) {
                DesiredPosition = ChasePosition + Vector3.TransformNormal(_Game.Settings.cameraSettings.desiredPositionOffset, transform);
                LookAt = ChasePosition + Vector3.TransformNormal(_Game.Settings.cameraSettings.lookAtOffset, transform);
            }
            if (Mode == 2) {
                DesiredPosition = ChasePosition + Vector3.TransformNormal(_Game.Settings.cameraSettings.desiredPositionOffset, transform);
                LookAt = ChasePosition + _Game.Settings.cameraSettings.thrustViewAmount * ChaseDirection;
            }
        }
        public void LoadContent(RoguelancerGame _Game) { 
        }
        public void UpdateNewCamera(float _X, float _Y, RoguelancerGame _Game) {
            if (Mode != 1) {
                return;
            }
            Matrix _Transform = Matrix.Identity;
            _Transform.Forward = ChaseDirection;
            _Transform.Up = Up;
            _Transform.Right = Vector3.Cross(Up, ChaseDirection);
            var o = ChasePosition + Vector3.TransformNormal(_Game.Settings.cameraSettings.desiredPositionOffset, _Transform);
            var d = ChasePosition + Vector3.TransformNormal(_Game.Settings.cameraSettings.lookAtOffset, _Transform);
            View = Matrix.CreateLookAt(o, d, Up);
            Projection = Matrix.CreatePerspectiveFieldOfView(_Game.Settings.cameraSettings.fieldOfView, _Game.Settings.cameraSettings.aspectRatio, _Game.Settings.cameraSettings.nearPlaneDistance, _Game.Settings.cameraSettings.clippingDistance); //Builds a perspective projection matrix based on a field of view
            var f = new BoundingFrustum(this.View * this.Projection);
            Vector3[] _Fcorners = new Vector3[8];
            f.GetCorners(_Fcorners);
            Vector3 _Fnx = _Fcorners[1] - _Fcorners[0];
            Vector3 _Fny = _Fcorners[3] - _Fcorners[0];
            Vector3 _MousePos = _Fcorners[0] + (_X * _Fnx) + (_Y * _Fny);
            Vector3 _MouseDir = _MousePos - o;
            DesiredPosition = o;
            LookAt = d;
            Vector3 v, v2 = (d - o);
            Vector3.Normalize(ref v2, out v);
            Plane p = new Plane(v, -Vector3.Dot(v, d));
            Ray r = new Ray(o, _MouseDir);
            float? t = r.Intersects(p);
            if (t != null) {
                v = o + (float)t * _MouseDir;
                v = (v - d);
                LookAt = d + v / _Game.Settings.cameraSettings.lookAtDivideBy;
            }
        }
        public void UpdateMatrices(RoguelancerGame _Game) {
            View = Matrix.CreateLookAt(Position, LookAt, Up);
            Projection = Matrix.CreatePerspectiveFieldOfView(_Game.Settings.cameraSettings.fieldOfView, _Game.Settings.cameraSettings.aspectRatio, _Game.Settings.cameraSettings.nearPlaneDistance, _Game.Settings.cameraSettings.clippingDistance);
        }
        public void Reset(RoguelancerGame _Game) {
            UpdateWorldPositions(_Game);
            Velocity = Vector3.Zero;
            Position = DesiredPosition;
            UpdateMatrices(_Game);
        }
        public void Update(RoguelancerGame _Game) {
            if(_Game.GameTime == null) {
                throw new ArgumentNullException("gameTime");
            }
            if(Shaking) {
                float magnitude;
                if(ShakeUseDuration == true) {
                    ShakeTimer += (float)_Game.GameTime.ElapsedGameTime.TotalSeconds;
                    if(ShakeTimer >= ShakeDuration) {
                        Shaking = false;
                        ShakeTimer = ShakeDuration;
                        float lProgress = ShakeTimer / ShakeDuration;
                        magnitude = ShakeMagnitude * (1f - (lProgress * lProgress));
                    }
                } else {
                    magnitude = ShakeMagnitude * (1f - (2f));
                    ShakeOffset = new Vector3(NextShakeFloat(), NextShakeFloat(), NextShakeFloat()) * magnitude;
                    Position += ShakeOffset;
                }
            }
            UpdateWorldPositions(_Game);
            float elapsed = (float)_Game.GameTime.ElapsedGameTime.TotalSeconds;
            var _Stretch = Position - DesiredPosition;
            var _Force = -_Game.Settings.cameraSettings.stiffness * _Stretch - _Game.Settings.cameraSettings.damping * Velocity;
            var _Acceleration = _Force / _Game.Settings.cameraSettings.mass;
            Velocity += _Acceleration * elapsed;
            Position += Velocity * elapsed;
            UpdateMatrices(_Game);
        }
        public void UpdateCameraChaseTarget(RoguelancerGame _Game) {
            var ship = _Game.Objects.ships.GetPlayerShip(_Game);
            var gd = _Game.Graphics.graphicsDeviceManager.GraphicsDevice;
            MouseState _MouseState = Mouse.GetState();
            ChasePosition = ship.model.Position;
            ChaseDirection = ship.model.Direction;
            Up = ship.model.Up;
            if (Mode == 1) {
                if (_MouseState.LeftButton == ButtonState.Pressed) {
                    UpdateNewCamera((float)_MouseState.X / (float)gd.Viewport.Width, (float)_MouseState.Y / (float)gd.Viewport.Height, _Game);
                } else {
                    UpdateNewCamera(_Game.Settings.cameraSettings.newCameraX, _Game.Settings.cameraSettings.newCameraY, _Game);
                }
            }
        }
        public void Draw(RoguelancerGame _Game) {
        }
    }
}