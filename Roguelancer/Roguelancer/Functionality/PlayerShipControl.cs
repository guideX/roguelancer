using Roguelancer.Interfaces;
using Microsoft.Xna.Framework;
using System;
using Roguelancer.Models;
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
        private const float thrustForce = 44000.0f;
        private const float dragFactor = 0.97f;
        private float maxThrustAmount = 0.3f;
        private float maxThrustAfterburnerAmount = 1.0f;
        private float thrustAddSpeed = 0.006f;
        private float thrustAfterBurnerAddAmount = 0.1f;
        private float thrustSlowDownSpeed = 0.005f;
        private float thrustReverseSpeed = -0.009f;
        private float maxThrustReverse = -0.10f;
        private float maxCruiseSpeed = 2.0f;
        private bool limitAltitude = false;
        private float thrustMinNotZero = .00001f;
        public bool useInput = true;
        public void Initialize(RoguelancerGame _Game) {
        }
        public void LoadContent(RoguelancerGame _Game) {
        }
        public void UpdateModel(GameModel model, RoguelancerGame _Game) {
            Vector3 force, acceleration;
            float elapsed = (float)_Game.GameTime.ElapsedGameTime.TotalSeconds;
            Vector2 rotationAmount = new Vector2();
            float w2 = (float)_Game.Graphics.ReturnBackBufferWidth() / updateDirectionX;
            float h2 = (float)_Game.Graphics.ReturnBackBufferHeight() / updateDirectionY;
            if(useInput == true) {
                //if (_game.Input.lInputItems.keys.lControlLeft || _game.Input.lInputItems.mouse.lRightButton) { // SHOOT!
                    //_Game.objects.Bullets.Shoot(_Game);
                //}
                if(_Game.Input.lInputItems.toggles.mouseMode == true && _Game.Input.lInputItems.toggles.freeMouseMode == false) {
                    if(_Game.Input.lInputItems.mouse.lLeftButton == true) {
                        rotationAmount.X = (_Game.Input.lInputItems.mouse.lVector.X - w2) / -w2;
                        rotationAmount.Y = (_Game.Input.lInputItems.mouse.lVector.Y - h2) / -h2;
                    }
                } else if(_Game.Input.lInputItems.toggles.mouseMode == false && _Game.Input.lInputItems.toggles.freeMouseMode == true) {
                    rotationAmount.X = (_Game.Input.lInputItems.mouse.lVector.X - w2) / -w2;
                    rotationAmount.Y = (_Game.Input.lInputItems.mouse.lVector.Y - h2) / -h2;
                }
                if(_Game.Input.lInputItems.keys.lLeft == true) {
                    rotationAmount.X = rotationXLeftAdd;
                }
                if(_Game.Input.lInputItems.keys.lRight == true) {
                    rotationAmount.X = rotationXRightAdd;
                }
                if(_Game.Input.lInputItems.keys.lUp == true) {
                    rotationAmount.Y = rotationYUpAdd;
                }
                if(_Game.Input.lInputItems.keys.lDown == true) {
                    rotationAmount.Y = rotationYDownAdd;
                }
                rotationAmount = rotationAmount * rotationRate * elapsed;
                if(model.Up.Y < 0) {
                    rotationAmount.X = -rotationAmount.X;
                }
            }
            model.Rotation = rotationAmount;
            model.UpdatePosition();
            if(useInput == true) {
                if(_Game.Input.lInputItems.keys.lW == true) {
                    _Game.Camera.Shake(.8f, 0f, false);
                    if(model.CurrentThrust == maxThrustAmount) {
                        model.CurrentThrust = maxThrustAmount;
                    } else if(model.CurrentThrust < maxThrustAmount) {
                        model.CurrentThrust = model.CurrentThrust + thrustAddSpeed;
                    } else {
                        model.CurrentThrust = maxThrustAmount;
                    }
                } else {
                    _Game.Camera.StopShaking();
                }
                if(_Game.Input.lInputItems.toggles.cruise == true) {
                    if(_Game.Input.lInputItems.keys.lS == true) {
                        model.CurrentThrust = maxThrustAmount;
                        _Game.Input.lInputItems.toggles.cruise = false;
                    } else {
                        model.CurrentThrust = maxCruiseSpeed;
                    }
                } else {
                    if(_Game.Input.lInputItems.keys.lTab == true) {
                        _Game.Camera.Shake(10f, 0f, false);
                        if(model.CurrentThrust == maxThrustAfterburnerAmount) {
                            model.CurrentThrust = maxThrustAfterburnerAmount;
                        } else if(model.CurrentThrust < maxThrustAfterburnerAmount) {
                            model.CurrentThrust = model.CurrentThrust + thrustAfterBurnerAddAmount;
                        } else {
                            model.CurrentThrust = maxThrustAfterburnerAmount;
                        }
                    } else {
                        if(model.CurrentThrust > maxThrustAmount) {
                            model.CurrentThrust = model.CurrentThrust - thrustSlowDownSpeed;
                            _Game.Camera.Shake(.8f, 0f, false);
                        } else {
                            if(_Game.Input.lInputItems.keys.lW == true) {
                                _Game.Camera.Shake(.8f, 0f, false);
                                if(model.CurrentThrust == maxThrustAmount) {
                                    model.CurrentThrust = maxThrustAmount;
                                } else if(model.CurrentThrust < maxThrustAmount) {
                                    model.CurrentThrust = model.CurrentThrust + thrustAddSpeed;
                                } else {
                                    model.CurrentThrust = maxThrustAmount;
                                }
                            } else {
                                _Game.Camera.StopShaking();
                            }
                        }
                    }
                    if(_Game.Input.lInputItems.keys.lX == true) {
                        _Game.Camera.Shake(1f, 0f, false);
                        if(model.CurrentThrust > maxThrustReverse) {
                            model.CurrentThrust = model.CurrentThrust + thrustReverseSpeed;
                        }
                    }
                    if(_Game.Input.lInputItems.keys.lS == true) {
                        _Game.Camera.StopShaking();
                        if(model.CurrentThrust == 0) {
                        } else if(model.CurrentThrust > maxThrustAmount || model.CurrentThrust > -.0001) {
                            model.CurrentThrust = model.CurrentThrust - thrustSlowDownSpeed;
                        }
                        if(model.CurrentThrust < thrustMinNotZero) {
                            model.CurrentThrust = 0;
                        }
                        if(model.CurrentThrust == 0) {
                            model.CurrentThrust = 0;
                        } else {
                            model.CurrentThrust = model.CurrentThrust - thrustSlowDownSpeed;
                        }
                    }
                }
            } else {
                if(_Game.Input.lInputItems.keys.lL == true) {
                    if(model.CurrentThrust == maxThrustAfterburnerAmount) {
                        model.CurrentThrust = maxThrustAfterburnerAmount;
                    } else if(model.CurrentThrust < maxThrustAfterburnerAmount) {
                        model.CurrentThrust = model.CurrentThrust + thrustAfterBurnerAddAmount;
                    } else {
                        model.CurrentThrust = maxThrustAfterburnerAmount;
                    }
                } else {
                    if(model.CurrentThrust > maxThrustAmount) {
                        model.CurrentThrust = model.CurrentThrust - thrustSlowDownSpeed;
                    } else {
                        if(_Game.Input.lInputItems.keys.lP == true) {
                            if(model.CurrentThrust == maxThrustAmount) {
                                model.CurrentThrust = maxThrustAmount;
                            } else if(model.CurrentThrust < maxThrustAmount) {
                                model.CurrentThrust = model.CurrentThrust + thrustAddSpeed;
                            } else {
                                model.CurrentThrust = maxThrustAmount;
                            }
                        }
                    }
                }
                if(_Game.Input.lInputItems.keys.lK == true) {
                    if(model.CurrentThrust == 0) {
                    } else if(model.CurrentThrust > maxThrustAmount || model.CurrentThrust > -.0001) {
                        model.CurrentThrust = model.CurrentThrust - thrustSlowDownSpeed;
                    }
                    if(model.CurrentThrust < -.00001f) {
                        model.CurrentThrust = 0;
                    }
                    if(model.CurrentThrust == 0) {
                        model.CurrentThrust = 0;
                    } else {
                        model.CurrentThrust = model.CurrentThrust - thrustSlowDownSpeed;
                    }
                }
            }
            if(_Game.Input.lInputItems.toggles.toggleCamera == false) {
                force = model.Direction * model.CurrentThrust * thrustForce;
                acceleration = force / mass;
                model.Velocity += acceleration * elapsed;
                model.Velocity *= dragFactor;
                model.Position += model.Velocity * elapsed;
                if(limitAltitude == true) {
                    model.Position.Y = Math.Max(model.Position.Y, model.MinimumAltitude);
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