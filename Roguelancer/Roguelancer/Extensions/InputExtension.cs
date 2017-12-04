using Roguelancer;
using Roguelancer.Enum;
using Roguelancer.Functionality;
using Roguelancer.Helpers;
using Roguelancer.Models;
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
    public static KeyboardKeyStatusModel FindKeyBoardStatus(this string str, KeyInputModel keys) {
        switch (str) {
            case "F10":
                return keys.F10;
            case "F9":
                return keys.F9;
            default:
                return null;
        }
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