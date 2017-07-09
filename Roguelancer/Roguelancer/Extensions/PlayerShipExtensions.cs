using Roguelancer;
using Roguelancer.Functionality;
using Roguelancer.Helpers;
using Roguelancer.Models;
using Microsoft.Xna.Framework;
/// <summary>
/// Player Ship Extensions
/// </summary>
public static class PlayerShipExtensions {
    /// <summary>
    /// Get Ship Rotation X Left 
    /// </summary>
    /// <param name="playerShipControl"></param>
    /// <param name="game"></param>
    /// <returns></returns>
    public static float GetShipRotationXLeft(this PlayerShipControl playerShipControl) {
        return playerShipControl.Model.RotationXLeftAdd;
    }
    /// <summary>
    /// Get Ship Rotation Y Up
    /// </summary>
    /// <param name="playerShipControl"></param>
    /// <param name="game"></param>
    /// <returns></returns>
    public static float GetShipRotationYUp(this PlayerShipControl playerShipControl) {
        return playerShipControl.Model.RotationYUpAdd;
    }
    /// <summary>
    /// Get Ship Rotation Y Down
    /// </summary>
    /// <param name="playerShipControl"></param>
    /// <param name="game"></param>
    /// <returns></returns>
    public static float GetShipRotationYDown(this PlayerShipControl playerShipControl) {
        return playerShipControl.Model.RotationYDownAdd;
    }
    /// <summary>
    /// Calculate Rotation Amount
    /// </summary>
    /// <param name="playerShipControl"></param>
    /// <param name="game"></param>
    /// <returns></returns>
    public static Vector2 CalculateRotationAmount(this PlayerShipControl playerShipControl, RoguelancerGame game) {
        var result = new Vector2();
        if (game.Input.InputItems.Mouse.LeftButton && game.Input.InputItems.Toggles.MouseMode && !game.Input.InputItems.Toggles.FreeMouseMode) {
            result = GetMouseRotationAmount(playerShipControl, game, playerShipControl.Model);
        } else if (!game.Input.InputItems.Toggles.MouseMode && game.Input.InputItems.Toggles.FreeMouseMode) {
            result = GetMouseRotationAmount(playerShipControl, game, playerShipControl.Model);
        }
        return result;
    }
    /// <summary>
    /// Get Mouse Rotation Amount
    /// </summary>
    /// <param name="playerShipControl"></param>
    /// <param name="game"></param>
    /// <param name="w"></param>
    /// <param name="y"></param>
    /// <returns></returns>
    private static Vector2 GetMouseRotationAmount(this PlayerShipControl playerShipControl, RoguelancerGame game, PlayerShipControlModel model) {
        var w = game.Graphics.Model.GraphicsDeviceManager.PreferredBackBufferWidth / model.UpdateDirectionX;
        var y = game.Graphics.Model.GraphicsDeviceManager.PreferredBackBufferHeight / model.UpdateDirectionY;
        return new Vector2() {
            X = (game.Input.InputItems.Mouse.Vector.X - w) / -w,
            Y = (game.Input.InputItems.Mouse.Vector.Y - y) / -y
        };
    }
    /// <summary>
    /// Get Ship Rotation X Right
    /// </summary>
    /// <param name="playerShipControl"></param>
    /// <param name="game"></param>
    /// <returns></returns>
    public static float GetShipRotationXRight(this PlayerShipControl playerShipControl) {
        return playerShipControl.Model.RotationXRightAdd; // Add Rotation Right
    }
    /// <summary>
    /// Move Forward
    /// </summary>
    /// <param name="game"></param>
    public static void MoveForward(this PlayerShipControl playerShipControl, RoguelancerGame game, GameModel model) {
        var playerShip = ShipHelper.GetPlayerShip(game);
        game.Camera.Shake(playerShip.ShipModel.PlayerShipControl.Model.ShakeValue, 0f, false);
        if (model.CurrentThrust == PlayerShipControlModel.MaxThrustAmount) {
            model.CurrentThrust = PlayerShipControlModel.MaxThrustAmount;
        } else if (model.CurrentThrust < PlayerShipControlModel.MaxThrustAmount) {
            model.CurrentThrust = model.CurrentThrust + PlayerShipControlModel.ThrustAddSpeed;
        } else {
            model.CurrentThrust = PlayerShipControlModel.MaxThrustAmount;
        }
    }
    /// <summary>
    /// Stop Shaking
    /// </summary>
    public static void StopShaking(this PlayerShipControl playerShipControl, RoguelancerGame game) {
        game.Camera.Model.Shaking = false;
    }
    /// <summary>
    /// Use After Burn Thrust
    /// </summary>
    /// <param name="playerShipControl"></param>
    /// <param name="game"></param>
    /// <param name="model"></param>
    public static void UseAfterBurnThrust(this PlayerShipControl playerShipControl, RoguelancerGame game, GameModel model) {
        game.Camera.Shake(playerShipControl.Model.ShakeValue, 0f, false);
        if (model.CurrentThrust == PlayerShipControlModel.MaxThrustAfterburnerAmount) {
            model.CurrentThrust = PlayerShipControlModel.MaxThrustAfterburnerAmount;
        } else if (model.CurrentThrust < PlayerShipControlModel.MaxThrustAfterburnerAmount) {
            model.CurrentThrust = model.CurrentThrust + PlayerShipControlModel.ThrustAfterBurnerAddAmount;
        } else {
            model.CurrentThrust = PlayerShipControlModel.MaxThrustAfterburnerAmount;
        }
    }
    /// <summary>
    /// Get Rotation Amount
    /// </summary>
    /// <param name="playerShipControl"></param>
    /// <param name="rotationAmount"></param>
    /// <param name="game"></param>
    /// <returns></returns>
    public static Vector2 GetRotationAmount(this PlayerShipControl playerShipControl, Vector2 rotationAmount, GameModel model, RoguelancerGame game) {
        var obj = rotationAmount * playerShipControl.Model.RotationRate * (float)game.GameTime.ElapsedGameTime.TotalSeconds; // Slow Rotation Amount
        if (model.Up.Y < 0) obj.X = -obj.X;
        return obj;
    }
}