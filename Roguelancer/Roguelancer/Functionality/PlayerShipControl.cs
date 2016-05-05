// Roguelancer 0.1 Pre Alpha by Leon Aiossa
// http://team-nexgen.com
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
        /// Player Ship Model
        /// </summary>
        public PlayerShipControlModel Model { get; set; }
        #endregion
        #region "public functions"
        /// <summary>
        /// Player Ship Control
        /// </summary>
        public PlayerShipControl(RoguelancerGame game) {
            Model = new PlayerShipControlModel(game);
            Model.UseInput = true;
        }
        /// <summary>
        /// Update Model
        /// </summary>
        /// <param name="model"></param>
        /// <param name="game"></param>
        public void UpdateModel(GameModel model, RoguelancerGame game) {
            Vector3 force, acceleration;
            var elapsed = (float)game.GameTime.ElapsedGameTime.TotalSeconds; // Elapsed time
            var rotationAmount = new Vector2(); // Create Vector for Rotation Amount
            var w2 = (float)game.Graphics.Model.GraphicsDeviceManager.PreferredBackBufferWidth / Model.UpdateDirectionX;
            var h2 = (float)game.Graphics.Model.GraphicsDeviceManager.PreferredBackBufferHeight / Model.UpdateDirectionY;
            if (Model.UseInput) {
                if (game.Input.InputItems.Toggles.MouseMode && !game.Input.InputItems.Toggles.FreeMouseMode) { // Flying around but must click to adjust direction
                    if (game.Input.InputItems.Mouse.LeftButton) { // Left Button
                        rotationAmount.X = (game.Input.InputItems.Mouse.Vector.X - w2) / -w2; // Adjust X
                        rotationAmount.Y = (game.Input.InputItems.Mouse.Vector.Y - h2) / -h2; // Adjust Y
                    }
                } else if (!game.Input.InputItems.Toggles.MouseMode && game.Input.InputItems.Toggles.FreeMouseMode) { // Flying around
                    rotationAmount.X = (game.Input.InputItems.Mouse.Vector.X - w2) / -w2; // Adjust X
                    rotationAmount.Y = (game.Input.InputItems.Mouse.Vector.Y - h2) / -h2; // Adjust Y
                }
                if (game.Input.InputItems.Keys.Left) { // Keys Left
                    rotationAmount.X = PlayerShipControlModel.RotationXLeftAdd; // Add Rotation Left
                }
                if (game.Input.InputItems.Keys.Right) { // Keys Right
                    rotationAmount.X = PlayerShipControlModel.RotationXRightAdd; // Add Rotation Right
                }
                if (game.Input.InputItems.Keys.Up) { // Keys Up
                    rotationAmount.Y = PlayerShipControlModel.RotationYUpAdd; // Add Rotation Up
                }
                if (game.Input.InputItems.Keys.Down) { // Keys Down
                    rotationAmount.Y = PlayerShipControlModel.RotationYDownAdd; // Add Rotation Down
                }
                if (game.Input.InputItems.Keys.Z) { // Z
                    model.Up.Y = 0f; // Stop
                }
                rotationAmount = rotationAmount * PlayerShipControlModel.RotationRate * elapsed;
                if (model.Up.Y < 0) {
                    rotationAmount.X = -rotationAmount.X;
                }
            }
            model.Rotation = rotationAmount;
            model.UpdatePosition();
            if (Model.UseInput) {
                if (game.Input.InputItems.Keys.W) {
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
                if (game.Input.InputItems.Toggles.Cruise) {
                    if (game.Input.InputItems.Keys.S) {
                        model.CurrentThrust = PlayerShipControlModel.MaxThrustAmount;
                        game.Input.InputItems.Toggles.Cruise = false;
                    } else {
                        model.CurrentThrust = PlayerShipControlModel.MaxCruiseSpeed;
                    }
                } else {
                    if (game.Input.InputItems.Keys.Tab) {
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
                            if (game.Input.InputItems.Keys.W) {
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
                    if (game.Input.InputItems.Keys.X) {
                        game.Camera.Shake(1f, 0f, false);
                        if (model.CurrentThrust > PlayerShipControlModel.MaxThrustReverse) {
                            model.CurrentThrust = model.CurrentThrust + PlayerShipControlModel.ThrustReverseSpeed;
                        }
                    }
                    if (game.Input.InputItems.Keys.S) {
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
                if (game.Input.InputItems.Keys.L) {
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
                        if (game.Input.InputItems.Keys.P) {
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
                if (game.Input.InputItems.Keys.K) {
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
            if (!game.Input.InputItems.Toggles.ToggleCamera) {
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