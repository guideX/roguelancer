// Roguelancer 0.1 Pre Alpha by Leon Aiossa
// http://www.team-nexgen.org
using System;
using Microsoft.Xna.Framework;
using Roguelancer.Interfaces;
using Roguelancer.Models;
namespace Roguelancer.Functionality {
    /// <summary>
    /// Player Ship Control
    /// </summary>
    public class PlayerShipControl : IPlayerShipControl {
        /// <summary>
        /// Player Ship Model
        /// </summary>
        public PlayerShipControlModel Model { get; set; }
        /// <summary>
        /// Use Input
        /// </summary>
        public bool UseInput { get; set; }
        #region "public functions"
        /// <summary>
        /// Player Ship Control
        /// </summary>
        public PlayerShipControl() {
            Model = new PlayerShipControlModel();
            Model.ShakeValue = .8f;
            UseInput = true;
        }
        /// <summary>
        /// Initialize
        /// </summary>
        /// <param name="game"></param>
        public void Initialize(RoguelancerGame game) { }
        /// <summary>
        /// Load Content
        /// </summary>
        /// <param name="game"></param>
        public void LoadContent(RoguelancerGame game) { }
        /// <summary>
        /// Update
        /// </summary>
        /// <param name="game"></param>
        public void Update(RoguelancerGame game) { }
        /// <summary>
        /// Draw
        /// </summary>
        /// <param name="game"></param>
        public void Draw(RoguelancerGame game) { }
        /// <summary>
        /// Update Model
        /// </summary>
        /// <param name="model"></param>
        /// <param name="game"></param>
        public void UpdateModel(GameModel model, RoguelancerGame game) {
            Vector3 force, acceleration;
            var elapsed = (float)game.GameTime.ElapsedGameTime.TotalSeconds; // Elapsed time
            var rotationAmount = new Vector2();
            var w2 = (float)game.Graphics.GraphicsDeviceManager.PreferredBackBufferWidth / PlayerShipControlModel.UpdateDirectionX;
            var h2 = (float)game.Graphics.GraphicsDeviceManager.PreferredBackBufferHeight / PlayerShipControlModel.UpdateDirectionY;
            if (UseInput) {
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
                    rotationAmount.X = PlayerShipControlModel.RotationXLeftAdd;
                }
                if (game.Input.InputItems.Keys.Right == true) {
                    rotationAmount.X = PlayerShipControlModel.RotationXRightAdd;
                }
                if (game.Input.InputItems.Keys.Up == true) {
                    rotationAmount.Y = PlayerShipControlModel.RotationYUpAdd;
                }
                if (game.Input.InputItems.Keys.Down == true) {
                    rotationAmount.Y = PlayerShipControlModel.RotationYDownAdd;
                }
                if (game.Input.InputItems.Keys.Z) {
                    model.Up.Y = 0f;
                }
                rotationAmount = rotationAmount * PlayerShipControlModel.RotationRate * elapsed;
                if (model.Up.Y < 0) {
                    rotationAmount.X = -rotationAmount.X;
                }
            }
            model.Rotation = rotationAmount;
            model.UpdatePosition();
            if (UseInput) {
                if (game.Input.InputItems.Keys.W == true) {
                    game.Camera.Shake(Model.ShakeValue, 0f, false);
                    if (model.CurrentThrust == PlayerShipControlModel.MaxThrustAmount) {
                        model.CurrentThrust = PlayerShipControlModel.MaxThrustAmount;
                    } else if (model.CurrentThrust < PlayerShipControlModel.MaxThrustAmount) {
                        model.CurrentThrust = model.CurrentThrust + PlayerShipControlModel.ThrustAddSpeed;
                    } else {
                        model.CurrentThrust = PlayerShipControlModel.MaxThrustAmount;
                    }
                } else {
                    game.Camera.StopShaking();
                }
                if (game.Input.InputItems.Toggles.Cruise == true) {
                    if (game.Input.InputItems.Keys.S == true) {
                        model.CurrentThrust = PlayerShipControlModel.MaxThrustAmount;
                        game.Input.InputItems.Toggles.Cruise = false;
                    } else {
                        model.CurrentThrust = PlayerShipControlModel.MaxCruiseSpeed;
                    }
                } else {
                    if (game.Input.InputItems.Keys.Tab == true) {
                        game.Camera.Shake(Model.ShakeValue, 0f, false);
                        if (model.CurrentThrust == PlayerShipControlModel.MaxThrustAfterburnerAmount) {
                            model.CurrentThrust = PlayerShipControlModel.MaxThrustAfterburnerAmount;
                        } else if (model.CurrentThrust < PlayerShipControlModel.MaxThrustAfterburnerAmount) {
                            model.CurrentThrust = model.CurrentThrust + PlayerShipControlModel.ThrustAfterBurnerAddAmount;
                        } else {
                            model.CurrentThrust = PlayerShipControlModel.MaxThrustAfterburnerAmount;
                        }
                    } else {
                        if (model.CurrentThrust > PlayerShipControlModel.MaxThrustAmount) {
                            model.CurrentThrust = model.CurrentThrust - PlayerShipControlModel.ThrustSlowDownSpeed;
                            game.Camera.Shake(Model.ShakeValue, 0f, false);
                        } else {
                            if (game.Input.InputItems.Keys.W == true) {
                                game.Camera.Shake(Model.ShakeValue, 0f, false);
                                if (model.CurrentThrust == PlayerShipControlModel.MaxThrustAmount) {
                                    model.CurrentThrust = PlayerShipControlModel.MaxThrustAmount;
                                } else if (model.CurrentThrust < PlayerShipControlModel.MaxThrustAmount) {
                                    model.CurrentThrust = model.CurrentThrust + PlayerShipControlModel.ThrustAddSpeed;
                                } else {
                                    model.CurrentThrust = PlayerShipControlModel.MaxThrustAmount;
                                }
                            } else {
                                game.Camera.StopShaking();
                            }
                        }
                    }
                    if (game.Input.InputItems.Keys.X == true) {
                        game.Camera.Shake(1f, 0f, false);
                        if (model.CurrentThrust > PlayerShipControlModel.MaxThrustReverse) {
                            model.CurrentThrust = model.CurrentThrust + PlayerShipControlModel.ThrustReverseSpeed;
                        }
                    }
                    if (game.Input.InputItems.Keys.S == true) {
                        game.Camera.StopShaking();
                        if (model.CurrentThrust == 0) {
                        } else if (model.CurrentThrust > PlayerShipControlModel.MaxThrustAmount || model.CurrentThrust > -.0001) {
                            model.CurrentThrust = model.CurrentThrust - PlayerShipControlModel.ThrustSlowDownSpeed;
                        }
                        if (model.CurrentThrust < PlayerShipControlModel.ThrustMinNotZero) {
                            model.CurrentThrust = 0;
                        }
                        if (model.CurrentThrust == 0) {
                            model.CurrentThrust = 0;
                        } else {
                            model.CurrentThrust = model.CurrentThrust - PlayerShipControlModel.ThrustSlowDownSpeed;
                        }
                    }
                }
            } else {
                if (game.Input.InputItems.Keys.L == true) {
                    if (model.CurrentThrust == PlayerShipControlModel.MaxThrustAfterburnerAmount) {
                        model.CurrentThrust = PlayerShipControlModel.MaxThrustAfterburnerAmount;
                    } else if (model.CurrentThrust < PlayerShipControlModel.MaxThrustAfterburnerAmount) {
                        model.CurrentThrust = model.CurrentThrust + PlayerShipControlModel.ThrustAfterBurnerAddAmount;
                    } else {
                        model.CurrentThrust = PlayerShipControlModel.MaxThrustAfterburnerAmount;
                    }
                } else {
                    if (model.CurrentThrust > PlayerShipControlModel.MaxThrustAmount) {
                        model.CurrentThrust = model.CurrentThrust - PlayerShipControlModel.ThrustSlowDownSpeed;
                    } else {
                        if (game.Input.InputItems.Keys.P == true) {
                            if (model.CurrentThrust == PlayerShipControlModel.MaxThrustAmount) {
                                model.CurrentThrust = PlayerShipControlModel.MaxThrustAmount;
                            } else if (model.CurrentThrust < PlayerShipControlModel.MaxThrustAmount) {
                                model.CurrentThrust = model.CurrentThrust + PlayerShipControlModel.ThrustAddSpeed;
                            } else {
                                model.CurrentThrust = PlayerShipControlModel.MaxThrustAmount;
                            }
                        }
                    }
                }
                if (game.Input.InputItems.Keys.K == true) {
                    if (model.CurrentThrust == 0) {
                    } else if (model.CurrentThrust > PlayerShipControlModel.MaxThrustAmount || model.CurrentThrust > -.0001) {
                        model.CurrentThrust = model.CurrentThrust - PlayerShipControlModel.ThrustSlowDownSpeed;
                    }
                    if (model.CurrentThrust < -.00001f) {
                        model.CurrentThrust = 0;
                    }
                    if (model.CurrentThrust == 0) {
                        model.CurrentThrust = 0;
                    } else {
                        model.CurrentThrust = model.CurrentThrust - PlayerShipControlModel.ThrustSlowDownSpeed;
                    }
                }
            }
            if (game.Input.InputItems.Toggles.ToggleCamera == false) {
                force = model.Direction * model.CurrentThrust * PlayerShipControlModel.ThrustForce;
                acceleration = force / PlayerShipControlModel.Mass;
                model.Velocity += acceleration * elapsed;
                model.Velocity *= PlayerShipControlModel.DragFactor;
                model.Position += model.Velocity * elapsed;
                if (PlayerShipControlModel.LimitAltitude) {
                    model.Position.Y = Math.Max(model.Position.Y, model.MinimumAltitude);
                }
                model.Update(game);
            }
        }
        #endregion
    }
}