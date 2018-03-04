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
    }
}