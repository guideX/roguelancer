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
    public class clsCamera : IGame {
        public bool lCameraSpringEnabled = true;
        public int lMode = 2;
        public float lAspectRatio = 4.0f / 3.0f; // Screen Dimensions
        public Matrix lProjection;
        public Matrix lView;
        public Vector3 lChasePosition;
        public Vector3 lChaseDirection;
        public Vector3 lLookAt;
        public Vector3 lPosition;
        public GameTime gameTime;
        public Vector3 lUp = Vector3.Up;
        private Vector3 lDesiredPositionOffset = new Vector3(0, 400.0f, 1830.0f);
        private Vector3 lDesiredPosition;
        private Vector3 lLookAtOffset = new Vector3(0, 2.8f, 0);
        private Vector3 lVelocity;
        private Vector3 lShakeOffset;
        private float lLookAtDivideBy = 2.0f;
        private float lStiffness = 1800.0f; // How loosely the ship can be positioned
        private float lDamping = 600.0f; // Weird Back and fourth thingy
        private float lMass = 50.0f; // Too little and ship is all over the place, too much and it sways back and fourth
        public float lFieldofView = MathHelper.ToRadians(45.0f); // Builds a perspective projection matrix based on a field of view
        public float lNearPlaneDistance = 1.0f; // How close the camera can get
        public float lClippingPlane = 1000000.0f; // Distance for the clipping plane
        private float lNewCameraX = 0.5f;
        private float lNewCameraY = 0.5f;
        private float lShakeMagnitude;
        private float lShakeDuration;
        private float lShakeTimer;
        private bool lShaking;
        private bool lShakeUseDuration;
        private int lThrustViewAmount = 10000;
        private static readonly Random lShakeRandom = new Random();
        private float NextShakeFloat() {
            return (float)lShakeRandom.NextDouble() * 2f - 1f;
        }
        public void StopShaking() {
            lShaking = false;
        }
        public void Shake(float _Magnitude, float _Duration, bool _ShakeUseDuration) {
            lShaking = true;
            lShakeMagnitude = _Magnitude;
            lShakeUseDuration = _ShakeUseDuration;
            if(lShakeUseDuration == true) {
                lShakeDuration = _Duration;
                lShakeTimer = 0f;
            }
        }
        public void Initialize(clsGame _Game) {
            lAspectRatio = _Game.graphics.ScreenDimensions();
            UpdateCameraChaseTarget(_Game.graphics, _Game.ships.GetPlayerShip());
            Reset();
        }
        private void UpdateWorldPositions() {
            Matrix _Transform = Matrix.Identity;
            _Transform.Forward = lChaseDirection;
            _Transform.Up = lUp;
            _Transform.Right = Vector3.Cross(lUp, lChaseDirection);
            if (lMode == 0) {
                lDesiredPosition = lChasePosition + Vector3.TransformNormal(lDesiredPositionOffset, _Transform);
                lLookAt = lChasePosition + Vector3.TransformNormal(lLookAtOffset, _Transform);
            }
            if (lMode == 2) {
                lDesiredPosition = lChasePosition + Vector3.TransformNormal(lDesiredPositionOffset, _Transform);
                lLookAt = lChasePosition + lThrustViewAmount * lChaseDirection;
            }
        }
        public void LoadContent(clsGame _Game) { }
        private void UpdateNewCamera(float _X, float _Y) {
            if (lMode != 1) {
                return;
            }
            Matrix _Transform = Matrix.Identity;
            _Transform.Forward = lChaseDirection;
            _Transform.Up = lUp;
            _Transform.Right = Vector3.Cross(lUp, lChaseDirection);
            Vector3 o = lChasePosition + Vector3.TransformNormal(lDesiredPositionOffset, _Transform);
            Vector3 d = lChasePosition + Vector3.TransformNormal(lLookAtOffset, _Transform);
            lView = Matrix.CreateLookAt(o, d, lUp);
            lProjection = Matrix.CreatePerspectiveFieldOfView(lFieldofView, lAspectRatio, lNearPlaneDistance, lClippingPlane); //Builds a perspective projection matrix based on a field of view
            BoundingFrustum f = new BoundingFrustum(this.lView * this.lProjection);
            Vector3[] _Fcorners = new Vector3[8];
            f.GetCorners(_Fcorners);
            Vector3 _Fnx = _Fcorners[1] - _Fcorners[0];
            Vector3 _Fny = _Fcorners[3] - _Fcorners[0];
            Vector3 _MousePos = _Fcorners[0] + (_X * _Fnx) + (_Y * _Fny);
            Vector3 _MouseDir = _MousePos - o;
            lDesiredPosition = o;
            lLookAt = d;
            Vector3 v, v2 = (d - o);
            Vector3.Normalize(ref v2, out v);
            Plane p = new Plane(v, -Vector3.Dot(v, d));
            Ray r = new Ray(o, _MouseDir);
            float? t = r.Intersects(p);
            if (t != null) {
                v = o + (float)t * _MouseDir;
                v = (v - d);
                lLookAt = d + v / lLookAtDivideBy;
            }
        }
        private void UpdateMatrices() {
            lView = Matrix.CreateLookAt(lPosition, lLookAt, lUp);
            lProjection = Matrix.CreatePerspectiveFieldOfView(lFieldofView, lAspectRatio, lNearPlaneDistance, lClippingPlane);
        }
        public void Reset() {
            UpdateWorldPositions();
            lVelocity = Vector3.Zero;
            lPosition = lDesiredPosition;
            UpdateMatrices();
        }
        public void Update(clsGame _Game) {
            if (gameTime == null) {
                throw new ArgumentNullException("gameTime");
            }
            if(lShaking) {
                float lMagnitude;
                if(lShakeUseDuration == true) {
                    lShakeTimer += (float)gameTime.ElapsedGameTime.TotalSeconds;
                    if(lShakeTimer >= lShakeDuration) {
                        lShaking = false;
                        lShakeTimer = lShakeDuration;
                        float lProgress = lShakeTimer / lShakeDuration;
                        lMagnitude = lShakeMagnitude * (1f - (lProgress * lProgress));
                    }
                } else {
                    lMagnitude = lShakeMagnitude * (1f - (2f));
                    lShakeOffset = new Vector3(NextShakeFloat(), NextShakeFloat(), NextShakeFloat()) * lMagnitude;
                    lPosition += lShakeOffset;
                }
            }
            UpdateWorldPositions();
            float elapsed = (float)gameTime.ElapsedGameTime.TotalSeconds;
            Vector3 _Stretch = lPosition - lDesiredPosition;
            Vector3 _Force = -lStiffness * _Stretch - lDamping * lVelocity;
            Vector3 _Acceleration = _Force / lMass;
            lVelocity += _Acceleration * elapsed;
            lPosition += lVelocity * elapsed;
            UpdateMatrices();
        }
        public void UpdateCameraChaseTarget(clsGraphics _Graphics, ship _Ship) {
            GraphicsDevice _GraphicsDevice = _Graphics.lGDM.GraphicsDevice;
            MouseState _MouseState = Mouse.GetState();
            lChasePosition = _Ship.model.position;
            lChaseDirection = _Ship.model.direction;
            lUp = _Ship.model.up;
            if (lMode == 1) {
                if (_MouseState.LeftButton == ButtonState.Pressed) {
                    UpdateNewCamera((float)_MouseState.X / (float)_GraphicsDevice.Viewport.Width, (float)_MouseState.Y / (float)_GraphicsDevice.Viewport.Height);
                } else {
                    UpdateNewCamera(lNewCameraX, lNewCameraY);
                }
            }
        }
        public void ExCameraSpringEnabled() {
            lCameraSpringEnabled = !lCameraSpringEnabled;
        }
        public void Draw(clsGame _Game) {
        }
    }
}