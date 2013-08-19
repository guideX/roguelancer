﻿// Roguelancer 0.1 Pre Alpha by Leon Aiossa
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
        public Vector3 lVelocity;
        public clsModel model;
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
        //public Matrix lRotationMatrix;
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
        private bool lUseInput = true;
        public clsShip(bool _UseInput) {
            lUseInput = _UseInput;
            model = new clsModel();
            model.direction = Vector3.Forward;
            lVelocity = Vector3.Zero;
        }
        public void Initialize(clsGame _Game) {
            model.drawMode = clsModel.DrawMode.mainModel;
            model.Initialize(_Game);
        }
        public void LoadContent(clsGame _Game) {
            model.LoadContent(_Game);
        }
        public void Update(GameTime _GameTime, clsGame _Game) {
            Vector3 _Force, _Acceleration;
            float elapsed = (float)_GameTime.ElapsedGameTime.TotalSeconds;
            Vector2 rotationAmount = new Vector2();
            float w2 = (float)_Game.graphics.ReturnBackBufferWidth() / lUpdateDirectionX;
            float h2 = (float)_Game.graphics.ReturnBackBufferHeight() / lUpdateDirectionY;
            if (lUseInput == true) {
                if (_Game.input.lInputItems.lMouse.lLeftButton == true) {
                    rotationAmount.X = (_Game.input.lInputItems.lMouse.lVector.X - w2) / -w2;
                    rotationAmount.Y = (_Game.input.lInputItems.lMouse.lVector.Y - h2) / -h2;
                }
                if (_Game.input.lInputItems.lKeys.lLeft == true) {
                    rotationAmount.X = lRotationXLeftAdd;
                }
                if (_Game.input.lInputItems.lKeys.lRight == true) {
                    rotationAmount.X = lRotationXRightAdd;
                }
                if (_Game.input.lInputItems.lKeys.lUp == true) {
                    rotationAmount.Y = lRotationYUpAdd;
                }
                if (_Game.input.lInputItems.lKeys.lDown == true) {
                    rotationAmount.Y = lRotationYDownAdd;
                }
                rotationAmount = rotationAmount * lRotationRate * elapsed;
                if(model.up.Y < 0) {
                    rotationAmount.X = -rotationAmount.X;
                }
            }
            model.rotationAmount = rotationAmount;
            model.UpdatePosition();
            //_Game.lDebugText.Update(_Game);
            if (lUseInput == true) {
                if(_Game.input.lInputItems.lKeys.lW == true) {
                    _Game.camera.Shake(.8f, 0f, false);
                    if(lCurrentThrust == lMaxThrustAmount) {
                        lCurrentThrust = lMaxThrustAmount;
                    } else if(lCurrentThrust < lMaxThrustAmount) {
                        lCurrentThrust = lCurrentThrust + lThrustAddSpeed;
                    } else {
                        lCurrentThrust = lMaxThrustAmount;
                    }
                } else {
                    _Game.camera.StopShaking();
                }
                if (_Game.input.lInputItems.lToggles.lCruise == true) {
                    if (_Game.input.lInputItems.lKeys.lS == true) {
                        lCurrentThrust = lMaxThrustAmount;
                        _Game.input.lInputItems.lToggles.lCruise = false;
                    } else {
                        lCurrentThrust = lMaxCruiseSpeed;
                    }
                } else {
                    if (_Game.input.lInputItems.lKeys.lTab == true) {
                        _Game.camera.Shake(10f, 0f, false);
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
                            _Game.camera.Shake(.8f, 0f, false);
                        } else {
                            if(_Game.input.lInputItems.lKeys.lW == true) {
                                _Game.camera.Shake(.8f, 0f, false);
                                if(lCurrentThrust == lMaxThrustAmount) {
                                    lCurrentThrust = lMaxThrustAmount;
                                } else if(lCurrentThrust < lMaxThrustAmount) {
                                    lCurrentThrust = lCurrentThrust + lThrustAddSpeed;
                                } else {
                                    lCurrentThrust = lMaxThrustAmount;
                                }
                            } else {
                                _Game.camera.StopShaking();
                            }
                        }
                    }
                    if (_Game.input.lInputItems.lKeys.lX == true) {
                        _Game.camera.Shake(1f, 0f, false);
                        if (lCurrentThrust > lMaxThrustReverse) {
                            lCurrentThrust = lCurrentThrust + lThrustReverseSpeed;
                        }
                    }
                    if (_Game.input.lInputItems.lKeys.lS == true) {
                        _Game.camera.StopShaking();
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
                if (_Game.input.lInputItems.lKeys.lL == true) {
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
                        if (_Game.input.lInputItems.lKeys.lP == true) {
                            if (lCurrentThrust == lMaxThrustAmount) {
                                lCurrentThrust = lMaxThrustAmount;
                            } else if (lCurrentThrust < lMaxThrustAmount) {
                                lCurrentThrust = lCurrentThrust + lThrustAddSpeed;
                            } else {
                                lCurrentThrust = lMaxThrustAmount;
                            }
                        }
                    }
                } if (_Game.input.lInputItems.lKeys.lK == true) {
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
            if (_Game.input.lInputItems.lToggles.lToggleCamera == false) {
                _Force = model.direction * lCurrentThrust * lThrustForce;
                _Acceleration = _Force / lMass;
                lVelocity += _Acceleration * elapsed;
                lVelocity *= lDragFactor;
                model.position += lVelocity * elapsed;
                if (lLimitAltitude == true) {
                    model.position.Y = Math.Max(model.position.Y, model.minimumAltitude);
                }
                model.Update(_Game);
            }
        }
        public void Draw(clsGame _Game) {
            model.Draw(_Game);
        }
    }
}