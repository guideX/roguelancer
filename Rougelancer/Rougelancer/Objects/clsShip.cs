// Rougelancer 0.1 Pre Alpha by Leon Aiossa
// http://www.team-nexgen.org
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Content;
using System;
using Rougelancer.Functionality;
namespace Rougelancer.Objects {
    public class clsShip {
        public string lText;
        public Vector3 lPosition;
        public Vector3 lDirection;
        public Vector3 lUp;
        public Vector3 lVelocity;
        public clsModel lModel;
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
        public Matrix lRotationMatrix;
        //private float lCurrentScrollValue = 0.0f;
        //private float lPreviousScrollValue = 0.0f;
        private float lMaxThrustAmount = 0.2f;
        private float lMaxThrustAfterburnerAmount = 0.4f;
        private float lThrustAddSpeed = 0.006f;
        private float lThrustAfterBurnerAddAmount = 0.1f;
        private float lThrustSlowDownSpeed = 0.005f;
        public float lCurrentThrust = 0.0f;
        private float lThrustReverseSpeed = -0.009f;
        private float lMaxThrustReverse = -0.10f;
        private float lMaxCruiseSpeed = 1.3f;
        private bool lLimitAltitude = false;
        private float lThrustMinNotZero = .00001f;
        private Vector3 lRight;
        private bool lUseInput = true;
        public clsShip(bool _UseInput) {
            lUseInput = _UseInput;
            lPosition = new Vector3(0, lMinimumAltitude, 0);
            lDirection = Vector3.Forward;
            lUp = Vector3.Up;
            lRight = Vector3.Right;
            lVelocity = Vector3.Zero;
            lModel = new clsModel();
        }
        public void LoadContent(string _Path, ContentManager _Content, clsCamera _Camera) {
            lModel.Init(_Path, _Content);
        }
        public void Update(GameTime _GameTime, clsGraphics _Graphics, clsInputItems _InputItems, clsDebugText _DebugText, clsCamera _Camera) {
            Vector3 _Force, _Acceleration;
            float elapsed = (float)_GameTime.ElapsedGameTime.TotalSeconds;
            Vector2 _RotationAmount = new Vector2();
            float w2 = (float)_Graphics.ReturnBackBufferWidth() / lUpdateDirectionX;
            float h2 = (float)_Graphics.ReturnBackBufferHeight() / lUpdateDirectionY;
            if (lUseInput == true) {
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
            }
            Matrix _RotationMatrix = Matrix.CreateFromAxisAngle(lRight, _RotationAmount.Y) * Matrix.CreateRotationY(_RotationAmount.X);
            lDirection = Vector3.TransformNormal(lDirection, _RotationMatrix);
            lUp = Vector3.TransformNormal(lUp, _RotationMatrix);
            lRotationMatrix = _RotationMatrix;
            lDirection.Normalize();
            lUp.Normalize();
            lRight = Vector3.Cross(lDirection, lUp);
            lUp = Vector3.Cross(lRight, lDirection);
            _DebugText.Update("D: " + lDirection.X.ToString() + "-" + lDirection.Y.ToString() + "-" + lDirection.Z.ToString() + Environment.NewLine + "P: " + lPosition.X.ToString() + "-" + lPosition.Y.ToString() + "-" + lPosition.Z.ToString() + Environment.NewLine + "R: " + lRotationMatrix.Forward.X.ToString() + "-" + lRotationMatrix.Forward.Y.ToString() + lRotationMatrix.Forward.Z.ToString() + Environment.NewLine + "T: " + lCurrentThrust.ToString(), _Graphics.lSpriteBatch);
            if (lUseInput == true) {
                //lPreviousScrollValue = lCurrentScrollValue;
                //lCurrentScrollValue = _InputItems.lMouse.lScrollWheel;
                if(_InputItems.lKeys.lW == true) {
                    _Camera.Shake(.8f, 0f, false);
                    if(lCurrentThrust == lMaxThrustAmount) {
                        lCurrentThrust = lMaxThrustAmount;
                    } else if(lCurrentThrust < lMaxThrustAmount) {
                        lCurrentThrust = lCurrentThrust + lThrustAddSpeed;
                    } else {
                        lCurrentThrust = lMaxThrustAmount;
                    }
                } else {
                    _Camera.StopShaking();
                }
                if (_InputItems.lToggles.lCruise == true) {
                    if (_InputItems.lKeys.lS == true) {
                        lCurrentThrust = lMaxThrustAmount;
                        _InputItems.lToggles.lCruise = false;
                    } else {
                        lCurrentThrust = lMaxCruiseSpeed;
                    }
                } else {
                    if (_InputItems.lKeys.lTab == true) {
                        _Camera.Shake(10f, 0f, false);
                        if (lCurrentThrust == lMaxThrustAfterburnerAmount) {
                            lCurrentThrust = lMaxThrustAfterburnerAmount;
                        } else if (lCurrentThrust < lMaxThrustAfterburnerAmount) {
                            lCurrentThrust = lCurrentThrust + lThrustAfterBurnerAddAmount;
                        } else {
                            lCurrentThrust = lMaxThrustAfterburnerAmount;
                        }
                    } else {
                        if(lCurrentThrust > lMaxThrustAmount) {
                            lCurrentThrust = lCurrentThrust - lThrustSlowDownSpeed;
                            _Camera.Shake(.8f, 0f, false);
                        } else {
                            if(_InputItems.lKeys.lW == true) {
                                _Camera.Shake(.8f, 0f, false);
                                if(lCurrentThrust == lMaxThrustAmount) {
                                    lCurrentThrust = lMaxThrustAmount;
                                } else if(lCurrentThrust < lMaxThrustAmount) {
                                    lCurrentThrust = lCurrentThrust + lThrustAddSpeed;
                                } else {
                                    lCurrentThrust = lMaxThrustAmount;
                                }
                            } else {
                                _Camera.StopShaking();
                            }
                        }
                    }
                    if (_InputItems.lKeys.lX == true) {
                        _Camera.Shake(1f, 0f, false);
                        if (lCurrentThrust > lMaxThrustReverse) {
                            lCurrentThrust = lCurrentThrust + lThrustReverseSpeed;
                        }
                    }
                    if (_InputItems.lKeys.lS == true) {
                        _Camera.StopShaking();
                        if (lCurrentThrust == 0) {
                        } else if (lCurrentThrust > lMaxThrustAmount || lCurrentThrust > -.0001) {
                            lCurrentThrust = lCurrentThrust - lThrustSlowDownSpeed;
                        } if (lCurrentThrust < lThrustMinNotZero) {
                            lCurrentThrust = 0;
                        } if (lCurrentThrust == 0) {
                            lCurrentThrust = 0;
                        } else {
                            lCurrentThrust = lCurrentThrust - lThrustSlowDownSpeed;
                        }
                    }
                }
            } else {
                // Test Controls Begin
                if (_InputItems.lKeys.lL == true) {
                    if (lCurrentThrust == lMaxThrustAfterburnerAmount) {
                        lCurrentThrust = lMaxThrustAfterburnerAmount;
                    } else if (lCurrentThrust < lMaxThrustAfterburnerAmount) {
                        lCurrentThrust = lCurrentThrust + lThrustAfterBurnerAddAmount;
                    } else {
                        lCurrentThrust = lMaxThrustAfterburnerAmount;
                    }
                } else {
                    if (lCurrentThrust > lMaxThrustAmount) {
                        lCurrentThrust = lCurrentThrust - lThrustSlowDownSpeed;
                    } else {
                        if (_InputItems.lKeys.lP == true) {
                            if (lCurrentThrust == lMaxThrustAmount) {
                                lCurrentThrust = lMaxThrustAmount;
                            } else if (lCurrentThrust < lMaxThrustAmount) {
                                lCurrentThrust = lCurrentThrust + lThrustAddSpeed;
                            } else {
                                lCurrentThrust = lMaxThrustAmount;
                            }
                        }
                    }
                } if (_InputItems.lKeys.lK == true) {
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
                // Test Controls End
            }
            if (_InputItems.lToggles.lToggleCamera == false) {
                _Force = lDirection * lCurrentThrust * lThrustForce;
                _Acceleration = _Force / lMass;
                lVelocity += _Acceleration * elapsed;
                lVelocity *= lDragFactor;
                lPosition += lVelocity * elapsed;
                if (lLimitAltitude == true) {
                    lPosition.Y = Math.Max(lPosition.Y, lMinimumAltitude);
                }
                lModel.lWorld = Matrix.Identity;
                lModel.lWorld.Forward = lDirection;
                lModel.lWorld.Up = lUp;
                lModel.lWorld.Right = lRight;
                lModel.lWorld.Translation = lPosition;
            }
        }
        public void Draw(clsCamera _Camera) {
            lModel.Draw(_Camera);
        }
    }
}