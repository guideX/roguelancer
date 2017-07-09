using System;
using Microsoft.Xna.Framework;
using Roguelancer.Interfaces;
using Roguelancer.Models;
namespace Roguelancer.Functionality {
    /// <summary>
    /// Player Ship Control
    /// </summary>
    public class PlayerShipControl : IPlayerShipControl {
        #region "public properties"
        /// <summary>
        /// Player Ship Model
        /// </summary>
        public PlayerShipControlModel Model { get; set; }
        #endregion
        #region "public methods"
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
            //var elapsed = (float)game.GameTime.ElapsedGameTime.TotalSeconds; // Elapsed time
            var rotationAmount = new Vector2(); // Create Vector for Rotation Amount
            if (Model.UseInput) { // This model is using input
                rotationAmount = this.CalculateRotationAmount(game); // Calculate Rotation Amount
                if (game.Input.InputItems.Keys.Left) rotationAmount.X = this.GetShipRotationXLeft(); // Left
                if (game.Input.InputItems.Keys.Right) rotationAmount.X = this.GetShipRotationXRight(); // Right
                if (game.Input.InputItems.Keys.Up) rotationAmount.Y = this.GetShipRotationYUp(); // Up
                if (game.Input.InputItems.Keys.Down) rotationAmount.Y = this.GetShipRotationYDown(); // Down
                rotationAmount = this.GetRotationAmount(rotationAmount, model, game);
            }
            model.Rotation = rotationAmount;
            model.UpdatePosition();
            if (Model.UseInput) {
                if (game.Input.InputItems.Keys.W) {
                    this.MoveForward(game, model);
                } else {
                    this.StopShaking(game);
                }
                if (game.Input.InputItems.Toggles.Cruise) { // Cruising
                    if (game.Input.InputItems.Keys.S) {
                        model.CurrentThrust = PlayerShipControlModel.MaxThrustAmount;
                        game.Input.InputItems.Toggles.Cruise = false;
                    } else {
                        model.CurrentThrust = PlayerShipControlModel.MaxCruiseSpeed;
                    }
                } else {
                    if (game.Input.InputItems.Keys.Tab) { // Tab
                        this.UseAfterBurnThrust(game, model); // Use Afterburn Thrust
                    } else {
                        if (model.CurrentThrust > PlayerShipControlModel.MaxThrustAmount) {
                            model.CurrentThrust = model.CurrentThrust - PlayerShipControlModel.ThrustSlowDownSpeed;
                            game.Camera.Shake(Model.ShakeValue, 0f, false);
                        }
                    }
                    if (game.Input.InputItems.Keys.X) {
                        game.Camera.Shake(1f, 0f, false);
                        if (model.CurrentThrust > PlayerShipControlModel.MaxThrustReverse) {
                            model.CurrentThrust = model.CurrentThrust + PlayerShipControlModel.ThrustReverseSpeed;
                        }
                    }
                    if (game.Input.InputItems.Keys.S) {
                        this.StopShaking(game);
                        if (model.CurrentThrust == 0) {
                        } else if (model.CurrentThrust > PlayerShipControlModel.MaxThrustAmount || model.CurrentThrust > -.0001) {
                            model.CurrentThrust = model.CurrentThrust - PlayerShipControlModel.ThrustSlowDownSpeed;
                        }
                        if (model.CurrentThrust < PlayerShipControlModel.ThrustMinNotZero) {
                            model.CurrentThrust = 0;
                        }
                        if (model.CurrentThrust != 0) model.CurrentThrust = model.CurrentThrust - PlayerShipControlModel.ThrustSlowDownSpeed;
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
            Vector3 force, acceleration;
            if (!game.Input.InputItems.Toggles.ToggleCamera) {
                force = model.Direction * model.CurrentThrust * Model.ThrustForce;
                acceleration = force / Model.Mass;
                model.Velocity += acceleration * (float)game.GameTime.ElapsedGameTime.TotalSeconds;
                model.Velocity *= PlayerShipControlModel.DragFactor;
                model.Position += model.Velocity * (float)game.GameTime.ElapsedGameTime.TotalSeconds;
                if (PlayerShipControlModel.LimitAltitude) {
                    model.Position.Y = Math.Max(model.Position.Y, model.MinimumAltitude);
                }
                model.Update(game);
            }
        }
        #endregion
    }
}