using Roguelancer;
using Roguelancer.Enum;
using Roguelancer.Functionality;
using Roguelancer.Helpers;
using Roguelancer.Models;
using System.Linq;
/// <summary>
/// Input Extension
/// </summary>
public static class InputExtension {
    /// <summary>
    /// Start Playing Menu
    /// </summary>
    /// <param name="game"></param>
    public static void StartPlayingMenu(this RoguelancerGame game) {
        game.GameState.Model.LastGameState = game.GameState.Model.CurrentGameState;
        game.GameState.Model.CurrentGameState = GameStatesEnum.Playing;
    }
    /// <summary>
    /// Exit Menu
    /// </summary>
    /// <param name="game"></param>
    public static void ExitMenu(this RoguelancerGame game) {
        game.GameState.Model.LastGameState = game.GameState.Model.CurrentGameState;
        game.GameState.Model.CurrentGameState = GameStatesEnum.Menu;
    }
    /// <summary>
    /// Goto Menu
    /// </summary>
    /// <param name="game"></param>
    public static void GotoMenu(this RoguelancerGame game) {
        game.GameState.Model.LastGameState = game.GameState.Model.CurrentGameState;
        game.GameState.Model.CurrentGameState = GameStatesEnum.Menu;
    }
    /// <summary>
    /// Free Mouse Mode
    /// </summary>
    /// <param name="game"></param>
    public static void FreeMouseMode(this RoguelancerGame game) {
        game.Input.InputItems.Toggles.MouseMode = false;
        game.Input.InputItems.Toggles.FreeMouseMode = true;
        DebugTextHelper.SetText(game, "Free Flight Mode Enabled", true);
    }
    /// <summary>
    /// Mouse Mode
    /// </summary>
    /// <param name="game"></param>
    public static void MouseMode(this RoguelancerGame game) {
        game.Input.InputItems.Toggles.MouseMode = true;
        game.Input.InputItems.Toggles.FreeMouseMode = false;
        DebugTextHelper.SetText(game, "Mouse Mode Enabled", true);
    }
    /// <summary>
    /// Toggle Camera
    /// </summary>
    /// <param name="game"></param>
    public static void ToggleCamera(this RoguelancerGame game) {
        if (game.Input.InputItems.Toggles.ToggleCamera) {
            game.Input.InputItems.Toggles.ToggleCamera = false;
            game.Input.InputItems.Toggles.RevertCamera = true;
            DebugTextHelper.SetText(game, "Revert Camera Mode", true);
        } else {
            game.Input.InputItems.Toggles.ToggleCamera = true;
            game.Input.InputItems.Toggles.CameraSnapshot = true;
            DebugTextHelper.SetText(game, "Camera Snapshot Mode", true);
        }
    }
    /// <summary>
    /// Toggle Cruise
    /// </summary>
    /// <param name="game"></param>
    public static void ToggleCruise(this RoguelancerGame game) {
        if (game.Input.InputItems.Toggles.Cruise) {
            game.Input.InputItems.Toggles.Cruise = false;
            DebugTextHelper.SetText(game, "Cruise Mode Off", true);
        } else {
            game.Input.InputItems.Toggles.Cruise = true;
            DebugTextHelper.SetText(game, "Cruise Mode On", true);
        }
    }
    /// <summary>
    /// Find Keyboard Status
    /// </summary>
    /// <param name="str"></param>
    /// <param name="keys"></param>
    /// <returns></returns>
    public static KeyboardKeyStatusModel FindKeyboardStatus(this string str, KeyInputModel keys) {
        var obj = (object)keys;
        KeyboardKeyStatusModel result = null;
        if (str != null) {
            result = (KeyboardKeyStatusModel)obj.GetType().GetProperties()
               .Single(pi => pi.Name == str)
               .GetValue(obj, null);
        }
        if (result != null) return result;
        return null;
    }
    /// <summary>
    /// Find Was Key Pressed
    /// </summary>
    /// <param name="str"></param>
    /// <param name="keys"></param>
    /// <returns></returns>
    public static bool FindWasKeyPressed(this string str, KeyInputModel keys) {
        var obj = (object)keys;
        KeyboardKeyStatusModel result = null;
        if (str != null) {
            result = (KeyboardKeyStatusModel)obj.GetType().GetProperties()
               .Single(pi => pi.Name == str)
               .GetValue(obj, null);
        }
        if (result != null) return result.WasKeyPressed;
        return false;
    }
    /// <summary>
    /// Find Was Key Pressed
    /// </summary>
    /// <param name="str"></param>
    /// <param name="keys"></param>
    /// <returns></returns>
    public static bool FindIsKeyDown(this string str, KeyInputModel keys) {
        var obj = (object)keys;
        KeyboardKeyStatusModel result = null;
        if (str != null) {
            result = (KeyboardKeyStatusModel)obj.GetType().GetProperties()
               .Single(pi => pi.Name == str)
               .GetValue(obj, null);
        }
        if (result != null) return result.IsKeyDown;
        return false;
    }
    /// <summary>
    /// Toggle Mode
    /// </summary>
    /// <param name="obj"></param>
    /// <param name="game"></param>
    public static void ToggleMode(this Input obj, RoguelancerGame game) {
        switch (game.Camera.Model.Mode) {
            case GameCameraModeEnum.DogfightingMode:
                DebugTextHelper.SetText(game, "Switching to Experimental Mode", true);
                game.Camera.Model.Mode = GameCameraModeEnum.ExperimentalMode;
                break;
            case GameCameraModeEnum.ExperimentalMode:
                DebugTextHelper.SetText(game, "Switching to Standard Mode", true);
                game.Camera.Model.Mode = GameCameraModeEnum.StandardMode;
                break;
            case GameCameraModeEnum.StandardMode:
                DebugTextHelper.SetText(game, "Switching to Dogfighting Mode", true);
                game.Camera.Model.Mode = GameCameraModeEnum.DogfightingMode;
                break;
        }
        DebugTextHelper.SetText(game, "Toggle Mode", true);
    }
}