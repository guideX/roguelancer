using Microsoft.Xna.Framework.Input;
using Roguelancer;
/// <summary>
/// Input Extensions
/// </summary>
public static class InputExtensions {
    /// <summary>
    /// Was Key Pressed
    /// </summary>
    /// <param name="k"></param>
    /// <param name="game"></param>
    /// <returns></returns>
    public static bool WasKeypressed(this Keys k, RoguelancerGame game) {
        var result = false;
        if (game.Input.CurrentKeyboardState.IsKeyDown(k) && game.Input.LastKeyboardState.IsKeyUp(k)) result = true;
        return result;
    }
}