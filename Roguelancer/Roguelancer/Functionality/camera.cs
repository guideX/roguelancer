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
    public class camera : IGame {
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
        private Vector3 lShakeOffset;
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
        public void Initialize(clsGame _Game) {
            _Game.settings.cameraSettings.aspectRatio = _Game.graphics.ScreenDimensions();
            UpdateCameraChaseTarget(_Game);
            Reset(_Game);
        }
        private void UpdateWorldPositions(clsGame _Game) {
            Matrix transform = Matrix.Identity;
            transform.Forward = chaseDirection;
            transform.Up = up;
            transform.Right = Vector3.Cross(up, chaseDirection);
            if (mode == 0) {
                desiredPosition = chasePosition + Vector3.TransformNormal(_Game.settings.cameraSettings.desiredPositionOffset, transform);
                lookAt = chasePosition + Vector3.TransformNormal(_Game.settings.cameraSettings.lookAtOffset, transform);
            }
            if (mode == 2) {
                desiredPosition = chasePosition + Vector3.TransformNormal(_Game.settings.cameraSettings.desiredPositionOffset, transform);
                lookAt = chasePosition + _Game.settings.cameraSettings.thrustViewAmount * chaseDirection;
            }
        }
        public void LoadContent(clsGame _Game) { 
        }
        private void UpdateNewCamera(float _X, float _Y, clsGame _Game) {
            if (mode != 1) {
                return;
            }
            Matrix _Transform = Matrix.Identity;
            _Transform.Forward = chaseDirection;
            _Transform.Up = up;
            _Transform.Right = Vector3.Cross(up, chaseDirection);
            Vector3 o = chasePosition + Vector3.TransformNormal(_Game.settings.cameraSettings.desiredPositionOffset, _Transform);
            Vector3 d = chasePosition + Vector3.TransformNormal(_Game.settings.cameraSettings.lookAtOffset, _Transform);
            view = Matrix.CreateLookAt(o, d, up);
            projection = Matrix.CreatePerspectiveFieldOfView(_Game.settings.cameraSettings.fieldOfView, _Game.settings.cameraSettings.aspectRatio, _Game.settings.cameraSettings.nearPlaneDistance, _Game.settings.cameraSettings.clippingDistance); //Builds a perspective projection matrix based on a field of view
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
                lookAt = d + v / _Game.settings.cameraSettings.lookAtDivideBy;
            }
        }
        private void UpdateMatrices(clsGame _Game) {
            view = Matrix.CreateLookAt(position, lookAt, up);
            projection = Matrix.CreatePerspectiveFieldOfView(_Game.settings.cameraSettings.fieldOfView, _Game.settings.cameraSettings.aspectRatio, _Game.settings.cameraSettings.nearPlaneDistance, _Game.settings.cameraSettings.clippingDistance);
        }
        public void Reset(clsGame _Game) {
            UpdateWorldPositions(_Game);
            velocity = Vector3.Zero;
            position = desiredPosition;
            UpdateMatrices(_Game);
        }
        public void Update(clsGame _Game) {
            if(_Game.gameTime == null) {
                throw new ArgumentNullException("gameTime");
            }
            if(shaking) {
                float magnitude;
                if(shakeUseDuration == true) {
                    shakeTimer += (float)_Game.gameTime.ElapsedGameTime.TotalSeconds;
                    if(shakeTimer >= shakeDuration) {
                        shaking = false;
                        shakeTimer = shakeDuration;
                        float lProgress = shakeTimer / shakeDuration;
                        magnitude = shakeMagnitude * (1f - (lProgress * lProgress));
                    }
                } else {
                    magnitude = shakeMagnitude * (1f - (2f));
                    lShakeOffset = new Vector3(NextShakeFloat(), NextShakeFloat(), NextShakeFloat()) * magnitude;
                    position += lShakeOffset;
                }
            }
            UpdateWorldPositions(_Game);
            float elapsed = (float)_Game.gameTime.ElapsedGameTime.TotalSeconds;
            Vector3 _Stretch = position - desiredPosition;
            Vector3 _Force = -_Game.settings.cameraSettings.stiffness * _Stretch - _Game.settings.cameraSettings.damping * velocity;
            Vector3 _Acceleration = _Force / _Game.settings.cameraSettings.mass;
            velocity += _Acceleration * elapsed;
            position += velocity * elapsed;
            UpdateMatrices(_Game);
        }
        public void UpdateCameraChaseTarget(clsGame _Game) {
            ship _ship = _Game.ships.GetPlayerShip();
            GraphicsDevice _GraphicsDevice = _Game.graphics.graphicsDeviceManager.GraphicsDevice;
            MouseState _MouseState = Mouse.GetState();
            chasePosition = _ship.model.position;
            chaseDirection = _ship.model.direction;
            up = _ship.model.up;
            if (mode == 1) {
                if (_MouseState.LeftButton == ButtonState.Pressed) {
                    UpdateNewCamera((float)_MouseState.X / (float)_GraphicsDevice.Viewport.Width, (float)_MouseState.Y / (float)_GraphicsDevice.Viewport.Height, _Game);
                } else {
                    UpdateNewCamera(_Game.settings.cameraSettings.newCameraX, _Game.settings.cameraSettings.newCameraY, _Game);
                }
            }
        }
        public void Draw(clsGame _Game) {
        }
    }
}