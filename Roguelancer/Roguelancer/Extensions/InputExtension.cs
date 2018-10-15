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
    //public static KeyboardKeyStatusModel FindKeyboardStatus(this string str) {
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

}