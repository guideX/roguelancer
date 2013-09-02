using Roguelancer.Interfaces;
using Microsoft.Xna.Framework;
using System;
namespace Roguelancer.Functionality {
    public class PlayerShipControl : IGame {
        private float updateDirectionX = 2.0f;
        private float updateDirectionY = 2.0f;
        private float rotationXLeftAdd = 1.0f;
        private float rotationXRightAdd = -1.0f;
        private float rotationYUpAdd = -1.0f;
        private float rotationYDownAdd = 1.0f;
        private const float rotationRate = 1.5f;
        private const float mass = 1.0f;
        private const float thrustForce = 24000.0f;
        private const float dragFactor = 0.97f;
        private float maxThrustAmount = 0.2f;
        private float maxThrustAfterburnerAmount = 0.4f;
        private float thrustAddSpeed = 0.006f;
        private float thrustAfterBurnerAddAmount = 0.1f;
        private float thrustSlowDownSpeed = 0.005f;
        private float thrustReverseSpeed = -0.009f;
        private float maxThrustReverse = -0.10f;
        private float maxCruiseSpeed = 1.3f;
        private bool limitAltitude = false;
        private float thrustMinNotZero = .00001f;
        public bool useInput = true;
        public void Initialize(RoguelancerGame _Game) {
        }
        public void LoadContent(RoguelancerGame _Game) {
        }
        public void UpdateModel(GameModel model, RoguelancerGame _Game) {
            Vector3 force, acceleration;
            float elapsed = (float)_Game.gameTime.ElapsedGameTime.TotalSeconds;
            Vector2 rotationAmount = new Vector2();
            float w2 = (float)_Game.graphics.ReturnBackBufferWidth() / updateDirectionX;
            float h2 = (float)_Game.graphics.ReturnBackBufferHeight() / updateDirectionY;
            if(useInput == true) {
                if(_Game.input.lInputItems.toggles.mouseMode == true && _Game.input.lInputItems.toggles.freeMouseMode == false) {
                    if(_Game.input.lInputItems.mouse.lLeftButton == true) {
                        rotationAmount.X = (_Game.input.lInputItems.mouse.lVector.X - w2) / -w2;
                        rotationAmount.Y = (_Game.input.lInputItems.mouse.lVector.Y - h2) / -h2;
                    }
                } else if(_Game.input.lInputItems.toggles.mouseMode == false && _Game.input.lInputItems.toggles.freeMouseMode == true) {
                    rotationAmount.X = (_Game.input.lInputItems.mouse.lVector.X - w2) / -w2;
                    rotationAmount.Y = (_Game.input.lInputItems.mouse.lVector.Y - h2) / -h2;
                }
                if(_Game.input.lInputItems.keys.lLeft == true) {
                    rotationAmount.X = rotationXLeftAdd;
                }
                if(_Game.input.lInputItems.keys.lRight == true) {
                    rotationAmount.X = rotationXRightAdd;
                }
                if(_Game.input.lInputItems.keys.lUp == true) {
                    rotationAmount.Y = rotationYUpAdd;
                }
                if(_Game.input.lInputItems.keys.lDown == true) {
                    rotationAmount.Y = rotationYDownAdd;
                }
                rotationAmount = rotationAmount * rotationRate * elapsed;
                if(model.up.Y < 0) {
                    rotationAmount.X = -rotationAmount.X;
                }
            }
            model.rotationAmount = rotationAmount;
            model.UpdatePosition();
            if(useInput == true) {
                if(_Game.input.lInputItems.keys.lW == true) {
                    _Game.camera.Shake(.8f, 0f, false);
                    if(model.currentThrust == maxThrustAmount) {
                        model.currentThrust = maxThrustAmount;
                    } else if(model.currentThrust < maxThrustAmount) {
                        model.currentThrust = model.currentThrust + thrustAddSpeed;
                    } else {
                        model.currentThrust = maxThrustAmount;
                    }
                } else {
                    _Game.camera.StopShaking();
                }
                if(_Game.input.lInputItems.toggles.cruise == true) {
                    if(_Game.input.lInputItems.keys.lS == true) {
                        model.currentThrust = maxThrustAmount;
                        _Game.input.lInputItems.toggles.cruise = false;
                    } else {
                        model.currentThrust = maxCruiseSpeed;
                    }
                } else {
                    if(_Game.input.lInputItems.keys.lTab == true) {
                        _Game.camera.Shake(10f, 0f, false);
                        if(model.currentThrust == maxThrustAfterburnerAmount) {
                            model.currentThrust = maxThrustAfterburnerAmount;
                        } else if(model.currentThrust < maxThrustAfterburnerAmount) {
                            model.currentThrust = model.currentThrust + thrustAfterBurnerAddAmount;
                        } else {
                            model.currentThrust = maxThrustAfterburnerAmount;
                        }
                    } else {
                        if(model.currentThrust > maxThrustAmount) {
                            model.currentThrust = model.currentThrust - thrustSlowDownSpeed;
                            _Game.camera.Shake(.8f, 0f, false);
                        } else {
                            if(_Game.input.lInputItems.keys.lW == true) {
                                _Game.camera.Shake(.8f, 0f, false);
                                if(model.currentThrust == maxThrustAmount) {
                                    model.currentThrust = maxThrustAmount;
                                } else if(model.currentThrust < maxThrustAmount) {
                                    model.currentThrust = model.currentThrust + thrustAddSpeed;
                                } else {
                                    model.currentThrust = maxThrustAmount;
                                }
                            } else {
                                _Game.camera.StopShaking();
                            }
                        }
                    }
                    if(_Game.input.lInputItems.keys.lX == true) {
                        _Game.camera.Shake(1f, 0f, false);
                        if(model.currentThrust > maxThrustReverse) {
                            model.currentThrust = model.currentThrust + thrustReverseSpeed;
                        }
                    }
                    if(_Game.input.lInputItems.keys.lS == true) {
                        _Game.camera.StopShaking();
                        if(model.currentThrust == 0) {
                        } else if(model.currentThrust > maxThrustAmount || model.currentThrust > -.0001) {
                            model.currentThrust = model.currentThrust - thrustSlowDownSpeed;
                        }
                        if(model.currentThrust < thrustMinNotZero) {
                            model.currentThrust = 0;
                        }
                        if(model.currentThrust == 0) {
                            model.currentThrust = 0;
                        } else {
                            model.currentThrust = model.currentThrust - thrustSlowDownSpeed;
                        }
                    }
                }
            } else {
                if(_Game.input.lInputItems.keys.lL == true) {
                    if(model.currentThrust == maxThrustAfterburnerAmount) {
                        model.currentThrust = maxThrustAfterburnerAmount;
                    } else if(model.currentThrust < maxThrustAfterburnerAmount) {
                        model.currentThrust = model.currentThrust + thrustAfterBurnerAddAmount;
                    } else {
                        model.currentThrust = maxThrustAfterburnerAmount;
                    }
                } else {
                    if(model.currentThrust > maxThrustAmount) {
                        model.currentThrust = model.currentThrust - thrustSlowDownSpeed;
                    } else {
                        if(_Game.input.lInputItems.keys.lP == true) {
                            if(model.currentThrust == maxThrustAmount) {
                                model.currentThrust = maxThrustAmount;
                            } else if(model.currentThrust < maxThrustAmount) {
                                model.currentThrust = model.currentThrust + thrustAddSpeed;
                            } else {
                                model.currentThrust = maxThrustAmount;
                            }
                        }
                    }
                }
                if(_Game.input.lInputItems.keys.lK == true) {
                    if(model.currentThrust == 0) {
                    } else if(model.currentThrust > maxThrustAmount || model.currentThrust > -.0001) {
                        model.currentThrust = model.currentThrust - thrustSlowDownSpeed;
                    }
                    if(model.currentThrust < -.00001f) {
                        model.currentThrust = 0;
                    }
                    if(model.currentThrust == 0) {
                        model.currentThrust = 0;
                    } else {
                        model.currentThrust = model.currentThrust - thrustSlowDownSpeed;
                    }
                }
            }
            if(_Game.input.lInputItems.toggles.toggleCamera == false) {
                force = model.direction * model.currentThrust * thrustForce;
                acceleration = force / mass;
                model.velocity += acceleration * elapsed;
                model.velocity *= dragFactor;
                model.position += model.velocity * elapsed;
                if(limitAltitude == true) {
                    model.position.Y = Math.Max(model.position.Y, model.minimumAltitude);
                }
                model.Update(_Game);
            }
        }
        public void Update(RoguelancerGame _Game) {

        }
        public void Draw(RoguelancerGame _Game) {

        }
    }
}