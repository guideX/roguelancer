using System;
using Microsoft.Xna.Framework;
using Roguelancer.Interfaces;
using Roguelancer.Models;
namespace Roguelancer.Functionality {
    /// <summary>
    /// Player Ship Control
    /// </summary>
    public class PlayerShipControl : IPlayerShipControl {
        #region "public variables"
        /// <summary>
        /// Shake Value
        /// </summary>
        public float ShakeValue { get; set; }
        /// <summary>
        /// Use Input
        /// </summary>
        public bool UseInput { get; set; }
        #endregion
        #region "private variables"
        /// <summary>
        /// Update Direction X
        /// </summary>
        private float _updateDirectionX = 3.0f;
        private float _updateDirectionY = 3.0f;
        private float _rotationXLeftAdd = 1.0f;
        private float _rotationXRightAdd = -1.0f;
        private float _rotationYUpAdd = -1.0f;
        private float _rotationYDownAdd = 1.0f;
        private const float _rotationRate = 1.5f;
        private const float _mass = 1.0f;
        private const float _thrustForce = 44000.0f;
        private const float _dragFactor = 0.97f;
        private float _maxThrustAmount = 0.3f;
        private float _maxThrustAfterburnerAmount = 1.0f;
        private float _thrustAddSpeed = 0.006f;
        private float _thrustAfterBurnerAddAmount = 0.1f;
        private float _thrustSlowDownSpeed = 0.005f;
        private float _thrustReverseSpeed = -0.009f;
        private float _maxThrustReverse = -0.10f;
        private float _maxCruiseSpeed = 2.0f;
        private bool _limitAltitude = true;
        private float _thrustMinNotZero = .00001f;
        #endregion
        #region "public functions"
        /// <summary>
        /// Player Ship Control
        /// </summary>
        public PlayerShipControl() {
            try {
                ShakeValue = .8f;
                UseInput = true;
            } catch {
                throw;
            }
        }
        /// <summary>
        /// Initialize
        /// </summary>
        /// <param name="game"></param>
        public void Initialize(RoguelancerGame game) {}
        /// <summary>
        /// Load Content
        /// </summary>
        /// <param name="game"></param>
        public void LoadContent(RoguelancerGame game) {}
        /// <summary>
        /// Update
        /// </summary>
        /// <param name="game"></param>
        public void Update(RoguelancerGame game) {}
        /// <summary>
        /// Draw
        /// </summary>
        /// <param name="game"></param>
        public void Draw(RoguelancerGame game) {}
        /// <summary>
        /// Update Model
        /// </summary>
        /// <param name="model"></param>
        /// <param name="game"></param>
        public void UpdateModel(GameModel model, RoguelancerGame game) {
            Vector3 force, acceleration;
            var elapsed = (float)game.GameTime.ElapsedGameTime.TotalSeconds;
            var rotationAmount = new Vector2();
            var w2 = (float)game.Graphics.GraphicsDeviceManager.PreferredBackBufferWidth / _updateDirectionX;
            var h2 = (float)game.Graphics.GraphicsDeviceManager.PreferredBackBufferHeight / _updateDirectionY;
            if (UseInput == true) {
                if (game.Input.InputItems.Toggles.MouseMode == true && game.Input.InputItems.Toggles.FreeMouseMode == false) {
                    if (game.Input.InputItems.Mouse.LeftButton == true) {
                        rotationAmount.X = (game.Input.InputItems.Mouse.Vector.X - w2) / -w2;
                        rotationAmount.Y = (game.Input.InputItems.Mouse.Vector.Y - h2) / -h2;
                    }
                } else if (game.Input.InputItems.Toggles.MouseMode == false && game.Input.InputItems.Toggles.FreeMouseMode == true) {
                    rotationAmount.X = (game.Input.InputItems.Mouse.Vector.X - w2) / -w2;
                    rotationAmount.Y = (game.Input.InputItems.Mouse.Vector.Y - h2) / -h2;
                }
                if (game.Input.InputItems.Keys.Left == true) {
                    rotationAmount.X = _rotationXLeftAdd;
                }
                if (game.Input.InputItems.Keys.Right == true) {
                    rotationAmount.X = _rotationXRightAdd;
                }
                if (game.Input.InputItems.Keys.Up == true) {
                    rotationAmount.Y = _rotationYUpAdd;
                }
                if (game.Input.InputItems.Keys.Down == true) {
                    rotationAmount.Y = _rotationYDownAdd;
                }
                rotationAmount = rotationAmount * _rotationRate * elapsed;
                if (model.Up.Y < 0) {
                    rotationAmount.X = -rotationAmount.X;
                }
            }
            model.Rotation = rotationAmount;
            model.UpdatePosition();
            if (UseInput == true) {
                if (game.Input.InputItems.Keys.W == true) {
                    game.Camera.Shake(ShakeValue, 0f, false);
                    if (model.CurrentThrust == _maxThrustAmount) {
                        model.CurrentThrust = _maxThrustAmount;
                    } else if (model.CurrentThrust < _maxThrustAmount) {
                        model.CurrentThrust = model.CurrentThrust + _thrustAddSpeed;
                    } else {
                        model.CurrentThrust = _maxThrustAmount;
                    }
                } else {
                    game.Camera.StopShaking();
                }
                if (game.Input.InputItems.Toggles.Cruise == true) {
                    if (game.Input.InputItems.Keys.S == true) {
                        model.CurrentThrust = _maxThrustAmount;
                        game.Input.InputItems.Toggles.Cruise = false;
                    } else {
                        model.CurrentThrust = _maxCruiseSpeed;
                    }
                } else {
                    if (game.Input.InputItems.Keys.Tab == true) {
                        game.Camera.Shake(ShakeValue, 0f, false);
                        if (model.CurrentThrust == _maxThrustAfterburnerAmount) {
                            model.CurrentThrust = _maxThrustAfterburnerAmount;
                        } else if (model.CurrentThrust < _maxThrustAfterburnerAmount) {
                            model.CurrentThrust = model.CurrentThrust + _thrustAfterBurnerAddAmount;
                        } else {
                            model.CurrentThrust = _maxThrustAfterburnerAmount;
                        }
                    } else {
                        if (model.CurrentThrust > _maxThrustAmount) {
                            model.CurrentThrust = model.CurrentThrust - _thrustSlowDownSpeed;
                            game.Camera.Shake(ShakeValue, 0f, false);
                        } else {
                            if (game.Input.InputItems.Keys.W == true) {
                                game.Camera.Shake(ShakeValue, 0f, false);
                                if (model.CurrentThrust == _maxThrustAmount) {
                                    model.CurrentThrust = _maxThrustAmount;
                                } else if (model.CurrentThrust < _maxThrustAmount) {
                                    model.CurrentThrust = model.CurrentThrust + _thrustAddSpeed;
                                } else {
                                    model.CurrentThrust = _maxThrustAmount;
                                }
                            } else {
                                game.Camera.StopShaking();
                            }
                        }
                    }
                    if (game.Input.InputItems.Keys.X == true) {
                        game.Camera.Shake(1f, 0f, false);
                        if (model.CurrentThrust > _maxThrustReverse) {
                            model.CurrentThrust = model.CurrentThrust + _thrustReverseSpeed;
                        }
                    }
                    if (game.Input.InputItems.Keys.S == true) {
                        game.Camera.StopShaking();
                        if (model.CurrentThrust == 0) {
                        } else if (model.CurrentThrust > _maxThrustAmount || model.CurrentThrust > -.0001) {
                            model.CurrentThrust = model.CurrentThrust - _thrustSlowDownSpeed;
                        }
                        if (model.CurrentThrust < _thrustMinNotZero) {
                            model.CurrentThrust = 0;
                        }
                        if (model.CurrentThrust == 0) {
                            model.CurrentThrust = 0;
                        } else {
                            model.CurrentThrust = model.CurrentThrust - _thrustSlowDownSpeed;
                        }
                    }
                }
            } else {
                if (game.Input.InputItems.Keys.L == true) {
                    if (model.CurrentThrust == _maxThrustAfterburnerAmount) {
                        model.CurrentThrust = _maxThrustAfterburnerAmount;
                    } else if (model.CurrentThrust < _maxThrustAfterburnerAmount) {
                        model.CurrentThrust = model.CurrentThrust + _thrustAfterBurnerAddAmount;
                    } else {
                        model.CurrentThrust = _maxThrustAfterburnerAmount;
                    }
                } else {
                    if (model.CurrentThrust > _maxThrustAmount) {
                        model.CurrentThrust = model.CurrentThrust - _thrustSlowDownSpeed;
                    } else {
                        if (game.Input.InputItems.Keys.P == true) {
                            if (model.CurrentThrust == _maxThrustAmount) {
                                model.CurrentThrust = _maxThrustAmount;
                            } else if (model.CurrentThrust < _maxThrustAmount) {
                                model.CurrentThrust = model.CurrentThrust + _thrustAddSpeed;
                            } else {
                                model.CurrentThrust = _maxThrustAmount;
                            }
                        }
                    }
                }
                if (game.Input.InputItems.Keys.K == true) {
                    if (model.CurrentThrust == 0) {
                    } else if (model.CurrentThrust > _maxThrustAmount || model.CurrentThrust > -.0001) {
                        model.CurrentThrust = model.CurrentThrust - _thrustSlowDownSpeed;
                    }
                    if (model.CurrentThrust < -.00001f) {
                        model.CurrentThrust = 0;
                    }
                    if (model.CurrentThrust == 0) {
                        model.CurrentThrust = 0;
                    } else {
                        model.CurrentThrust = model.CurrentThrust - _thrustSlowDownSpeed;
                    }
                }
            }
            if (game.Input.InputItems.Toggles.ToggleCamera == false) {
                force = model.Direction * model.CurrentThrust * _thrustForce;
                acceleration = force / _mass;
                model.Velocity += acceleration * elapsed;
                model.Velocity *= _dragFactor;
                model.Position += model.Velocity * elapsed;
                if (_limitAltitude == true) {
                    model.Position.Y = Math.Max(model.Position.Y, model.MinimumAltitude);
                }
                model.Update(game);
            }
        }
        #endregion
    }
}