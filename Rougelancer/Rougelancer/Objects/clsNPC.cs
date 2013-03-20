// Rougelancer 0.1 Pre Alpha by Leon Aiossa
// http://www.team-nexgen.org
using Rougelancer.Functionality;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Content;
namespace Rougelancer.Objects {
    class clsNPC {
        public Vector3 lPosition;
        public Vector3 lDirection;
        public Vector3 lUp;
        public Vector3 lVelocity;
        public Matrix lWorld;
        private clsModel lModel;
        private float lUpdateDirectionX = 2.0f;
        private float lUpdateDirectionY = 2.0f;
        private float lRotationXLeftAdd = 1.0f;
        private float lRotationXRightAdd = -1.0f;
        private float lRotationYUpAdd = -1.0f;
        private float lRotationYDownAdd = 1.0f;
        private const float lMinimumAltitude = 350.0f;
        private const float lRotationRate = 1.5f;
        private const float lMass = 1.0f;
        private const float lThrustForce = 24000.0f;
        private const float lDragFactor = 0.97f;
        private float lMaxThrustAmount = 0.5f;
        private float lMaxThrustAfterburnerAmount = 1.0f;
        private float lThrustAddSpeed = 0.007f;
        private float lThrustAfterBurnerAddAmount = 0.2f;
        private float lThrustSlowDownSpeed = 0.003f;
        private float lCurrentThrust = 0.0f;
        public clsNPC()
        {
            Reset();
        }
        public void LoadContent(string _Path, ContentManager _Content) {
            lModel.Init(_Path, _Content);
        }
        public void Update(GameTime _GameTime, clsGraphics _Graphics, clsInputItems _InputItems, clsDebugText _DebugText) {
            int w = _Graphics.ReturnBackBufferWidth();
            int h = _Graphics.ReturnBackBufferHeight();
            float elapsed = (float)_GameTime.ElapsedGameTime.TotalSeconds;
            Vector2 _RotationAmount = new Vector2();
            float w2 = (float)w / lUpdateDirectionX;
            float h2 = (float)h / lUpdateDirectionY;
            if (_InputItems.lMouse.lLeftButton == true) {
                _RotationAmount.X = (_InputItems.lMouse.lVector.X - w2) / -w2;
                _RotationAmount.Y = (_InputItems.lMouse.lVector.Y - h2) / -h2;
            }
            if (_InputItems.lKeys.lLeft == true) {
                _RotationAmount.X = lRotationXLeftAdd;
            }
            if (_InputItems.lKeys.lRight == true) {
                _RotationAmount.X = lRotationXRightAdd;
            }
            if (_InputItems.lKeys.lUp == true) {
                _RotationAmount.Y = lRotationYUpAdd;
            }
            if (_InputItems.lKeys.lDown == true) {
                _RotationAmount.Y = lRotationYDownAdd;
            }
            _RotationAmount = _RotationAmount * lRotationRate * elapsed;
            if (lUp.Y < 0) {
                _RotationAmount.X = -_RotationAmount.X;
            }
            Matrix rotationMatrix = Matrix.CreateFromAxisAngle(lRight, _RotationAmount.Y) * Matrix.CreateRotationY(_RotationAmount.X);
            lDirection = Vector3.TransformNormal(lDirection, rotationMatrix);
            lUp = Vector3.TransformNormal(lUp, rotationMatrix);
            lDirection.Normalize();
            lUp.Normalize();
            lRight = Vector3.Cross(lDirection, lUp);
            lUp = Vector3.Cross(lRight, lDirection);
            
            if (_InputItems.lKeys.lTab == true) {
                if (lCurrentThrust == lMaxThrustAfterburnerAmount) {
                    lCurrentThrust = lMaxThrustAfterburnerAmount;
                }
                else if (lCurrentThrust < lMaxThrustAfterburnerAmount) {
                    lCurrentThrust = lCurrentThrust + lThrustAfterBurnerAddAmount;
                } else {
                    lCurrentThrust = lMaxThrustAfterburnerAmount;
                }
            } else {
                if (lCurrentThrust > lMaxThrustAmount) {
                    lCurrentThrust = lCurrentThrust - lThrustSlowDownSpeed;
                } else {
                    if (_InputItems.lKeys.lW == true) {
                        if (lCurrentThrust == lMaxThrustAmount) {
                            lCurrentThrust = lMaxThrustAmount;
                        } else if (lCurrentThrust < lMaxThrustAmount) {
                            lCurrentThrust = lCurrentThrust + lThrustAddSpeed;
                        } else {
                            lCurrentThrust = lMaxThrustAmount;
                        }
                    }
                }
            }
            if (_InputItems.lKeys.lS == true) {
                if (lCurrentThrust == 0) {
                } else if (lCurrentThrust > lMaxThrustAmount || lCurrentThrust > -.0001) {
                    lCurrentThrust = lCurrentThrust - lThrustSlowDownSpeed;
                } if (lCurrentThrust < -.00001f) {
                    lCurrentThrust = 0;
                } if (lCurrentThrust == 0) {
                    lCurrentThrust = 0;
                } else {
                    lCurrentThrust = lCurrentThrust - lThrustSlowDownSpeed;
                }
            }
            lText = lCurrentThrust.ToString();
            Vector3 _Force = lDirection * lCurrentThrust * lThrustForce;
            Vector3 _Acceleration = _Force / lMass;
            lVelocity += _Acceleration * elapsed;
            lVelocity *= lDragFactor;
            lPosition += lVelocity * elapsed;
            lPosition.Y = Math.Max(lPosition.Y, lMinimumAltitude);
            lWorld = Matrix.Identity;
            lWorld.Forward = lDirection;
            lWorld.Up = lUp;
            lWorld.Right = lRight;
            lWorld.Translation = lPosition;
        }
        public void Draw(clsCamera _Camera) {
            lModel.Draw(lWorld, _Camera);
        }
        public void Reset() {
            lPosition = new Vector3(0, lMinimumAltitude, 0);
            lDirection = Vector3.Forward;
            lUp = Vector3.Up;
            lRight = Vector3.Right;
            lVelocity = Vector3.Zero;
            lModel = new clsModel();
        }



        public void LoadContent(string _Path, ContentManager _Content)
        {
            lModel.Init(_Path, _Content);
        }
        public void Update(GameTime _GameTime, clsGraphics _Graphics, clsInputItems _InputItems, clsDebugText _DebugText)
        {
            int w = _Graphics.ReturnBackBufferWidth();
            int h = _Graphics.ReturnBackBufferHeight();
            float elapsed = (float)_GameTime.ElapsedGameTime.TotalSeconds;
            Vector2 _RotationAmount = new Vector2();
            float w2 = (float)w / lUpdateDirectionX;
            float h2 = (float)h / lUpdateDirectionY;
            if (_InputItems.lMouse.lLeftButton == true)
            {
                _RotationAmount.X = (_InputItems.lMouse.lVector.X - w2) / -w2;
                _RotationAmount.Y = (_InputItems.lMouse.lVector.Y - h2) / -h2;
            }
            if (_InputItems.lKeys.lLeft == true)
            {
                _RotationAmount.X = lRotationXLeftAdd;
            }
            if (_InputItems.lKeys.lRight == true)
            {
                _RotationAmount.X = lRotationXRightAdd;
            }
            if (_InputItems.lKeys.lUp == true)
            {
                _RotationAmount.Y = lRotationYUpAdd;
            }
            if (_InputItems.lKeys.lDown == true)
            {
                _RotationAmount.Y = lRotationYDownAdd;
            }
            _RotationAmount = _RotationAmount * lRotationRate * elapsed;
            if (lUp.Y < 0)
            {
                _RotationAmount.X = -_RotationAmount.X;
            }
            Matrix rotationMatrix = Matrix.CreateFromAxisAngle(lRight, _RotationAmount.Y) * Matrix.CreateRotationY(_RotationAmount.X);
            lDirection = Vector3.TransformNormal(lDirection, rotationMatrix);
            lUp = Vector3.TransformNormal(lUp, rotationMatrix);
            lDirection.Normalize();
            lUp.Normalize();
            lRight = Vector3.Cross(lDirection, lUp);
            lUp = Vector3.Cross(lRight, lDirection);

            if (_InputItems.lKeys.lTab == true)
            {
                if (lCurrentThrust == lMaxThrustAfterburnerAmount)
                {
                    lCurrentThrust = lMaxThrustAfterburnerAmount;
                }
                else if (lCurrentThrust < lMaxThrustAfterburnerAmount)
                {
                    lCurrentThrust = lCurrentThrust + lThrustAfterBurnerAddAmount;
                }
                else
                {
                    lCurrentThrust = lMaxThrustAfterburnerAmount;
                }
            }
            else
            {
                if (lCurrentThrust > lMaxThrustAmount)
                {
                    lCurrentThrust = lCurrentThrust - lThrustSlowDownSpeed;
                }
                else
                {
                    if (_InputItems.lKeys.lW == true)
                    {
                        if (lCurrentThrust == lMaxThrustAmount)
                        {
                            lCurrentThrust = lMaxThrustAmount;
                        }
                        else if (lCurrentThrust < lMaxThrustAmount)
                        {
                            lCurrentThrust = lCurrentThrust + lThrustAddSpeed;
                        }
                        else
                        {
                            lCurrentThrust = lMaxThrustAmount;
                        }
                    }
                }
            }
            if (_InputItems.lKeys.lS == true)
            {
                if (lCurrentThrust == 0)
                {
                }
                else if (lCurrentThrust > lMaxThrustAmount || lCurrentThrust > -.0001)
                {
                    lCurrentThrust = lCurrentThrust - lThrustSlowDownSpeed;
                } if (lCurrentThrust < -.00001f)
                {
                    lCurrentThrust = 0;
                } if (lCurrentThrust == 0)
                {
                    lCurrentThrust = 0;
                }
                else
                {
                    lCurrentThrust = lCurrentThrust - lThrustSlowDownSpeed;
                }
            }
            lText = lCurrentThrust.ToString();
            Vector3 _Force = lDirection * lCurrentThrust * lThrustForce;
            Vector3 _Acceleration = _Force / lMass;
            lVelocity += _Acceleration * elapsed;
            lVelocity *= lDragFactor;
            lPosition += lVelocity * elapsed;
            lPosition.Y = Math.Max(lPosition.Y, lMinimumAltitude);
            lWorld = Matrix.Identity;
            lWorld.Forward = lDirection;
            lWorld.Up = lUp;
            lWorld.Right = lRight;
            lWorld.Translation = lPosition;
        }
        public void Draw(clsCamera _Camera)
        {
            lModel.Draw(lWorld, _Camera);
        }
        public void Reset()
        {
            lPosition = new Vector3(0, lMinimumAltitude, 0);
            lDirection = Vector3.Forward;
            lUp = Vector3.Up;
            lRight = Vector3.Right;
            lVelocity = Vector3.Zero;
            lModel = new clsModel();
        }
       
        
        
        
        
        public void Init()
        {
            lModel = new clsModel();
        }
        public void LoadContent(Vector2 _StartupPosition) {
            
        }
        public void Draw(Matrix _World, clsCamera _Camera) {
            lModel.Draw(_World, _Camera);
        }
    }
}