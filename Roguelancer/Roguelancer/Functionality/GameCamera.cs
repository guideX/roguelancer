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
    public class GameCamera : IGame {
        public Matrix projection;
        public Matrix view;
        private int mode = 2;
        private Vector3 chasePosition;
        private Vector3 chaseDirection;
        private Vector3 lookAt;
        private Vector3 position;
        private Vector3 up = Vector3.Up;
        private Vector3 desiredPosition;
        private Vector3 velocity;
        private Vector3 shakeOffset;
        private float shakeMagnitude;
        private float shakeDuration;
        private float shakeTimer;
        private bool shaking;
        private bool shakeUseDuration;
        private static readonly Random shakeRandom = new Random();
        private float NextShakeFloat() {
            return (float)shakeRandom.NextDouble() * 2f - 1f;
        }
        public void StopShaking() {
            shaking = false;
        }
        public void Shake(float magnitude, float duration, bool _shakeUseDuration) {
            shaking = true;
            shakeMagnitude = magnitude;
            shakeUseDuration = _shakeUseDuration;
            if(shakeUseDuration == true) {
                shakeDuration = duration;
                shakeTimer = 0f;
            }
        }
        public void Initialize(RoguelancerGame _Game) {
            _Game.Settings.cameraSettings.aspectRatio = _Game.Graphics.ScreenDimensions();
            UpdateCameraChaseTarget(_Game);
            Reset(_Game);
        }
        public void UpdateWorldPositions(RoguelancerGame _Game) {
            Matrix transform = Matrix.Identity;
            transform.Forward = chaseDirection;
            transform.Up = up;
            transform.Right = Vector3.Cross(up, chaseDirection);
            if (mode == 0) {
                desiredPosition = chasePosition + Vector3.TransformNormal(_Game.Settings.cameraSettings.desiredPositionOffset, transform);
                lookAt = chasePosition + Vector3.TransformNormal(_Game.Settings.cameraSettings.lookAtOffset, transform);
            }
            if (mode == 2) {
                desiredPosition = chasePosition + Vector3.TransformNormal(_Game.Settings.cameraSettings.desiredPositionOffset, transform);
                lookAt = chasePosition + _Game.Settings.cameraSettings.thrustViewAmount * chaseDirection;
            }
        }
        public void LoadContent(RoguelancerGame _Game) { 
        }
        public void UpdateNewCamera(float _X, float _Y, RoguelancerGame _Game) {
            if (mode != 1) {
                return;
            }
            Matrix _Transform = Matrix.Identity;
            _Transform.Forward = chaseDirection;
            _Transform.Up = up;
            _Transform.Right = Vector3.Cross(up, chaseDirection);
            Vector3 o = chasePosition + Vector3.TransformNormal(_Game.Settings.cameraSettings.desiredPositionOffset, _Transform);
            Vector3 d = chasePosition + Vector3.TransformNormal(_Game.Settings.cameraSettings.lookAtOffset, _Transform);
            view = Matrix.CreateLookAt(o, d, up);
            projection = Matrix.CreatePerspectiveFieldOfView(_Game.Settings.cameraSettings.fieldOfView, _Game.Settings.cameraSettings.aspectRatio, _Game.Settings.cameraSettings.nearPlaneDistance, _Game.Settings.cameraSettings.clippingDistance); //Builds a perspective projection matrix based on a field of view
            BoundingFrustum f = new BoundingFrustum(this.view * this.projection);
            Vector3[] _Fcorners = new Vector3[8];
            f.GetCorners(_Fcorners);
            Vector3 _Fnx = _Fcorners[1] - _Fcorners[0];
            Vector3 _Fny = _Fcorners[3] - _Fcorners[0];
            Vector3 _MousePos = _Fcorners[0] + (_X * _Fnx) + (_Y * _Fny);
            Vector3 _MouseDir = _MousePos - o;
            desiredPosition = o;
            lookAt = d;
            Vector3 v, v2 = (d - o);
            Vector3.Normalize(ref v2, out v);
            Plane p = new Plane(v, -Vector3.Dot(v, d));
            Ray r = new Ray(o, _MouseDir);
            float? t = r.Intersects(p);
            if (t != null) {
                v = o + (float)t * _MouseDir;
                v = (v - d);
                lookAt = d + v / _Game.Settings.cameraSettings.lookAtDivideBy;
            }
        }
        public void UpdateMatrices(RoguelancerGame _Game) {
            view = Matrix.CreateLookAt(position, lookAt, up);
            projection = Matrix.CreatePerspectiveFieldOfView(_Game.Settings.cameraSettings.fieldOfView, _Game.Settings.cameraSettings.aspectRatio, _Game.Settings.cameraSettings.nearPlaneDistance, _Game.Settings.cameraSettings.clippingDistance);
        }
        public void Reset(RoguelancerGame _Game) {
            UpdateWorldPositions(_Game);
            velocity = Vector3.Zero;
            position = desiredPosition;
            UpdateMatrices(_Game);
        }
        public void Update(RoguelancerGame _Game) {
            if(_Game.GameTime == null) {
                throw new ArgumentNullException("gameTime");
            }
            if(shaking) {
                float magnitude;
                if(shakeUseDuration == true) {
                    shakeTimer += (float)_Game.GameTime.ElapsedGameTime.TotalSeconds;
                    if(shakeTimer >= shakeDuration) {
                        shaking = false;
                        shakeTimer = shakeDuration;
                        float lProgress = shakeTimer / shakeDuration;
                        magnitude = shakeMagnitude * (1f - (lProgress * lProgress));
                    }
                } else {
                    magnitude = shakeMagnitude * (1f - (2f));
                    shakeOffset = new Vector3(NextShakeFloat(), NextShakeFloat(), NextShakeFloat()) * magnitude;
                    position += shakeOffset;
                }
            }
            UpdateWorldPositions(_Game);
            float elapsed = (float)_Game.GameTime.ElapsedGameTime.TotalSeconds;
            Vector3 _Stretch = position - desiredPosition;
            Vector3 _Force = -_Game.Settings.cameraSettings.stiffness * _Stretch - _Game.Settings.cameraSettings.damping * velocity;
            Vector3 _Acceleration = _Force / _Game.Settings.cameraSettings.mass;
            velocity += _Acceleration * elapsed;
            position += velocity * elapsed;
            UpdateMatrices(_Game);
        }
        public void UpdateCameraChaseTarget(RoguelancerGame _Game) {
            Ship _ship = _Game.Objects.ships.GetPlayerShip(_Game);
            GraphicsDevice _GraphicsDevice = _Game.Graphics.graphicsDeviceManager.GraphicsDevice;
            MouseState _MouseState = Mouse.GetState();
            chasePosition = _ship.model.Position;
            chaseDirection = _ship.model.Direction;
            up = _ship.model.Up;
            if (mode == 1) {
                if (_MouseState.LeftButton == ButtonState.Pressed) {
                    UpdateNewCamera((float)_MouseState.X / (float)_GraphicsDevice.Viewport.Width, (float)_MouseState.Y / (float)_GraphicsDevice.Viewport.Height, _Game);
                } else {
                    UpdateNewCamera(_Game.Settings.cameraSettings.newCameraX, _Game.Settings.cameraSettings.newCameraY, _Game);
                }
            }
        }
        public void Draw(RoguelancerGame _Game) {
        }
    }
}