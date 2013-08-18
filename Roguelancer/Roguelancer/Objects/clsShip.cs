// Roguelancer 0.1 Pre Alpha by Leon Aiossa
// http://www.team-nexgen.org
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Content;
using System;
using Roguelancer.Functionality;
namespace Roguelancer.Objects {
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
        public void Initialize(clsGame _Game) {

        }
        public void LoadContent(clsGame _Game) {
            lModel.drawMode = clsModel.DrawMode.mainModel;
            lModel.Initialize(_Game);
        }
        public void Update(GameTime _GameTime, clsGame _Game) {
            Vector3 _Force, _Acceleration;
            float elapsed = (float)_GameTime.ElapsedGameTime.TotalSeconds;
            Vector2 _RotationAmount = new Vector2();
            float w2 = (float)_Game.lGraphics.ReturnBackBufferWidth() / lUpdateDirectionX;
            float h2 = (float)_Game.lGraphics.ReturnBackBufferHeight() / lUpdateDirectionY;
            if (lUseInput == true) {
                if (_Game.lInput.lInputItems.lMouse.lLeftButton == true) {
                    _RotationAmount.X = (_Game.lInput.lInputItems.lMouse.lVector.X - w2) / -w2;
                    _RotationAmount.Y = (_Game.lInput.lInputItems.lMouse.lVector.Y - h2) / -h2;
                }
                if (_Game.lInput.lInputItems.lKeys.lLeft == true) {
                    _RotationAmount.X = lRotationXLeftAdd;
                }
                if (_Game.lInput.lInputItems.lKeys.lRight == true) {
                    _RotationAmount.X = lRotationXRightAdd;
                }
                if (_Game.lInput.lInputItems.lKeys.lUp == true) {
                    _RotationAmount.Y = lRotationYUpAdd;
                }
                if (_Game.lInput.lInputItems.lKeys.lDown == true) {
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
            _Game.lDebugText.Update(_Game);
            if (lUseInput == true) {
                if(_Game.lInput.lInputItems.lKeys.lW == true) {
                    _Game.lCamera.Shake(.8f, 0f, false);
                    if(lCurrentThrust == lMaxThrustAmount) {
                        lCurrentThrust = lMaxThrustAmount;
                    } else if(lCurrentThrust < lMaxThrustAmount) {
                        lCurrentThrust = lCurrentThrust + lThrustAddSpeed;
                    } else {
                        lCurrentThrust = lMaxThrustAmount;
                    }
                } else {
                    _Game.lCamera.StopShaking();
                }
                if (_Game.lInput.lInputItems.lToggles.lCruise == true) {
                    if (_Game.lInput.lInputItems.lKeys.lS == true) {
                        lCurrentThrust = lMaxThrustAmount;
                        _Game.lInput.lInputItems.lToggles.lCruise = false;
                    } else {
                        lCurrentThrust = lMaxCruiseSpeed;
                    }
                } else {
                    if (_Game.lInput.lInputItems.lKeys.lTab == true) {
                        _Game.lCamera.Shake(10f, 0f, false);
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
                            _Game.lCamera.Shake(.8f, 0f, false);
                        } else {
                            if(_Game.lInput.lInputItems.lKeys.lW == true) {
                                _Game.lCamera.Shake(.8f, 0f, false);
                                if(lCurrentThrust == lMaxThrustAmount) {
                                    lCurrentThrust = lMaxThrustAmount;
                                } else if(lCurrentThrust < lMaxThrustAmount) {
                                    lCurrentThrust = lCurrentThrust + lThrustAddSpeed;
                                } else {
                                    lCurrentThrust = lMaxThrustAmount;
                                }
                            } else {
                                _Game.lCamera.StopShaking();
                            }
                        }
                    }
                    if (_Game.lInput.lInputItems.lKeys.lX == true) {
                        _Game.lCamera.Shake(1f, 0f, false);
                        if (lCurrentThrust > lMaxThrustReverse) {
                            lCurrentThrust = lCurrentThrust + lThrustReverseSpeed;
                        }
                    }
                    if (_Game.lInput.lInputItems.lKeys.lS == true) {
                        _Game.lCamera.StopShaking();
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
                if (_Game.lInput.lInputItems.lKeys.lL == true) {
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
                        if (_Game.lInput.lInputItems.lKeys.lP == true) {
                            if (lCurrentThrust == lMaxThrustAmount) {
                                lCurrentThrust = lMaxThrustAmount;
                            } else if (lCurrentThrust < lMaxThrustAmount) {
                                lCurrentThrust = lCurrentThrust + lThrustAddSpeed;
                            } else {
                                lCurrentThrust = lMaxThrustAmount;
                            }
                        }
                    }
                } if (_Game.lInput.lInputItems.lKeys.lK == true) {
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
            }
            if (_Game.lInput.lInputItems.lToggles.lToggleCamera == false) {
                _Force = lDirection * lCurrentThrust * lThrustForce;
                _Acceleration = _Force / lMass;
                lVelocity += _Acceleration * elapsed;
                lVelocity *= lDragFactor;
                lPosition += lVelocity * elapsed;
                if (lLimitAltitude == true) {
                    lPosition.Y = Math.Max(lPosition.Y, lMinimumAltitude);
                }
                lModel.world = Matrix.Identity;
                lModel.world.Forward = lDirection;
                lModel.world.Up = lUp;
                lModel.world.Right = lRight;
                lModel.world.Translation = lPosition;
            }
        }
        public void Draw(clsGame _Game) {
            lModel.Draw(_Game);
        }
    }
}