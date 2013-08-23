using Roguelancer.Interfaces;
using Microsoft.Xna.Framework;
using System;
namespace Roguelancer.Functionality {
    public class clsPlayerShipControl : IGame {
        private float lUpdateDirectionX = 2.0f;
        private float lUpdateDirectionY = 2.0f;
        private float lRotationXLeftAdd = 1.0f;
        private float lRotationXRightAdd = -1.0f;
        private float lRotationYUpAdd = -1.0f;
        private float lRotationYDownAdd = 1.0f;
        private const float lRotationRate = 1.5f;
        private const float lMass = 1.0f;
        private const float lThrustForce = 24000.0f;
        private const float lDragFactor = 0.97f;
        private float lMaxThrustAmount = 0.2f;
        private float lMaxThrustAfterburnerAmount = 0.4f;
        private float lThrustAddSpeed = 0.006f;
        private float lThrustAfterBurnerAddAmount = 0.1f;
        private float lThrustSlowDownSpeed = 0.005f;
        private float lThrustReverseSpeed = -0.009f;
        private float lMaxThrustReverse = -0.10f;
        private float lMaxCruiseSpeed = 1.3f;
        private bool lLimitAltitude = false;
        private float lThrustMinNotZero = .00001f;
        public bool useInput = true;
        public clsModel model { get; set; }
        public void Initialize(clsGame _Game) {
        }
        public void LoadContent(clsGame _Game) {
        }
        public void Update(clsGame _Game) {
            Vector3 _Force, _Acceleration;
            float elapsed = (float)_Game.gameTime.ElapsedGameTime.TotalSeconds;
            Vector2 rotationAmount = new Vector2();
            float w2 = (float)_Game.graphics.ReturnBackBufferWidth() / lUpdateDirectionX;
            float h2 = (float)_Game.graphics.ReturnBackBufferHeight() / lUpdateDirectionY;
            if(useInput == true) {
                if(_Game.input.lInputItems.lMouse.lLeftButton == true) {
                    rotationAmount.X = (_Game.input.lInputItems.lMouse.lVector.X - w2) / -w2;
                    rotationAmount.Y = (_Game.input.lInputItems.lMouse.lVector.Y - h2) / -h2;
                }
                if(_Game.input.lInputItems.lKeys.lLeft == true) {
                    rotationAmount.X = lRotationXLeftAdd;
                }
                if(_Game.input.lInputItems.lKeys.lRight == true) {
                    rotationAmount.X = lRotationXRightAdd;
                }
                if(_Game.input.lInputItems.lKeys.lUp == true) {
                    rotationAmount.Y = lRotationYUpAdd;
                }
                if(_Game.input.lInputItems.lKeys.lDown == true) {
                    rotationAmount.Y = lRotationYDownAdd;
                }
                rotationAmount = rotationAmount * lRotationRate * elapsed;
                if(model.up.Y < 0) {
                    rotationAmount.X = -rotationAmount.X;
                }
            }
            model.rotationAmount = rotationAmount;
            //model.UpdatePosition();
            if(useInput == true) {
                if(_Game.input.lInputItems.lKeys.lW == true) {
                    _Game.camera.Shake(.8f, 0f, false);
                    if(model.currentThrust == lMaxThrustAmount) {
                        model.currentThrust = lMaxThrustAmount;
                    } else if(model.currentThrust < lMaxThrustAmount) {
                        model.currentThrust = model.currentThrust + lThrustAddSpeed;
                    } else {
                        model.currentThrust = lMaxThrustAmount;
                    }
                } else {
                    _Game.camera.StopShaking();
                }
                if(_Game.input.lInputItems.lToggles.lCruise == true) {
                    if(_Game.input.lInputItems.lKeys.lS == true) {
                        model.currentThrust = lMaxThrustAmount;
                        _Game.input.lInputItems.lToggles.lCruise = false;
                    } else {
                        model.currentThrust = lMaxCruiseSpeed;
                    }
                } else {
                    if(_Game.input.lInputItems.lKeys.lTab == true) {
                        _Game.camera.Shake(10f, 0f, false);
                        if(model.currentThrust == lMaxThrustAfterburnerAmount) {
                            model.currentThrust = lMaxThrustAfterburnerAmount;
                        } else if(model.currentThrust < lMaxThrustAfterburnerAmount) {
                            model.currentThrust = model.currentThrust + lThrustAfterBurnerAddAmount;
                        } else {
                            model.currentThrust = lMaxThrustAfterburnerAmount;
                        }
                    } else {
                        if(model.currentThrust > lMaxThrustAmount) {
                            model.currentThrust = model.currentThrust - lThrustSlowDownSpeed;
                            _Game.camera.Shake(.8f, 0f, false);
                        } else {
                            if(_Game.input.lInputItems.lKeys.lW == true) {
                                _Game.camera.Shake(.8f, 0f, false);
                                if(model.currentThrust == lMaxThrustAmount) {
                                    model.currentThrust = lMaxThrustAmount;
                                } else if(model.currentThrust < lMaxThrustAmount) {
                                    model.currentThrust = model.currentThrust + lThrustAddSpeed;
                                } else {
                                    model.currentThrust = lMaxThrustAmount;
                                }
                            } else {
                                _Game.camera.StopShaking();
                            }
                        }
                    }
                    if(_Game.input.lInputItems.lKeys.lX == true) {
                        _Game.camera.Shake(1f, 0f, false);
                        if(model.currentThrust > lMaxThrustReverse) {
                            model.currentThrust = model.currentThrust + lThrustReverseSpeed;
                        }
                    }
                    if(_Game.input.lInputItems.lKeys.lS == true) {
                        _Game.camera.StopShaking();
                        if(model.currentThrust == 0) {
                        } else if(model.currentThrust > lMaxThrustAmount || model.currentThrust > -.0001) {
                            model.currentThrust = model.currentThrust - lThrustSlowDownSpeed;
                        }
                        if(model.currentThrust < lThrustMinNotZero) {
                            model.currentThrust = 0;
                        }
                        if(model.currentThrust == 0) {
                            model.currentThrust = 0;
                        } else {
                            model.currentThrust = model.currentThrust - lThrustSlowDownSpeed;
                        }
                    }
                }
            } else {
                if(_Game.input.lInputItems.lKeys.lL == true) {
                    if(model.currentThrust == lMaxThrustAfterburnerAmount) {
                        model.currentThrust = lMaxThrustAfterburnerAmount;
                    } else if(model.currentThrust < lMaxThrustAfterburnerAmount) {
                        model.currentThrust = model.currentThrust + lThrustAfterBurnerAddAmount;
                    } else {
                        model.currentThrust = lMaxThrustAfterburnerAmount;
                    }
                } else {
                    if(model.currentThrust > lMaxThrustAmount) {
                        model.currentThrust = model.currentThrust - lThrustSlowDownSpeed;
                    } else {
                        if(_Game.input.lInputItems.lKeys.lP == true) {
                            if(model.currentThrust == lMaxThrustAmount) {
                                model.currentThrust = lMaxThrustAmount;
                            } else if(model.currentThrust < lMaxThrustAmount) {
                                model.currentThrust = model.currentThrust + lThrustAddSpeed;
                            } else {
                                model.currentThrust = lMaxThrustAmount;
                            }
                        }
                    }
                }
                if(_Game.input.lInputItems.lKeys.lK == true) {
                    if(model.currentThrust == 0) {
                    } else if(model.currentThrust > lMaxThrustAmount || model.currentThrust > -.0001) {
                        model.currentThrust = model.currentThrust - lThrustSlowDownSpeed;
                    }
                    if(model.currentThrust < -.00001f) {
                        model.currentThrust = 0;
                    }
                    if(model.currentThrust == 0) {
                        model.currentThrust = 0;
                    } else {
                        model.currentThrust = model.currentThrust - lThrustSlowDownSpeed;
                    }
                }
            }
            if(_Game.input.lInputItems.lToggles.lToggleCamera == false) {
                _Force = model.direction * model.currentThrust * lThrustForce;
                _Acceleration = _Force / lMass;
                model.velocity += _Acceleration * elapsed;
                model.velocity *= lDragFactor;
                model.position += model.velocity * elapsed;
                if(lLimitAltitude == true) {
                    model.position.Y = Math.Max(model.position.Y, model.minimumAltitude);
                }
                //model.Update(_Game);
            }
        }
        public void Draw(clsGame _Game) {

        }
    }
}