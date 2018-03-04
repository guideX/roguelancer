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
    /// <summary>
    /// Get Keyboard Status Model
    /// </summary>
    /// <param name="k"></param>
    /// <param name="state"></param>
    /// <param name="lastState"></param>
    /// <returns></returns>
    public static KeyboardKeyStatusModel GetKeyboardKeyStatusModel(this Microsoft.Xna.Framework.Input.Keys k, Microsoft.Xna.Framework.Input.KeyboardState state, Microsoft.Xna.Framework.Input.KeyboardState lastState, KeyboardAssignmentEnum assignment = KeyboardAssignmentEnum.None) {
        return new KeyboardKeyStatusModel() {
            IsKeyDown = state.IsKeyDown(k),
            WasKeyPressed = (state.IsKeyDown(k) && lastState.IsKeyUp(k)),
            Assignment = assignment
        };
    }
    /// <summary>
    /// Update Keys Status
    /// </summary>
    /// <param name="game"></param>
    /// <param name="currentState"></param>
    /// <param name="lastState"></param>
    public static void UpdateKeysStatus(this KeyInputModel keys, KeyboardState currentState, KeyboardState lastState) {
        keys.Tab = Keys.Tab.GetKeyboardKeyStatusModel(currentState, lastState);
        keys.Escape = Keys.Escape.GetKeyboardKeyStatusModel(currentState, lastState);
        keys.Space = Keys.Space.GetKeyboardKeyStatusModel(currentState, lastState);
        keys.Left = Keys.Left.GetKeyboardKeyStatusModel(currentState, lastState);
        keys.Up = Keys.Up.GetKeyboardKeyStatusModel(currentState, lastState);
        keys.Right = Keys.Right.GetKeyboardKeyStatusModel(currentState, lastState);
        keys.Down = Keys.Down.GetKeyboardKeyStatusModel(currentState, lastState);
        keys.D0 = Keys.D0.GetKeyboardKeyStatusModel(currentState, lastState);
        keys.D1 = Keys.D1.GetKeyboardKeyStatusModel(currentState, lastState);
        keys.D2 = Keys.D2.GetKeyboardKeyStatusModel(currentState, lastState);
        keys.D3 = Keys.D3.GetKeyboardKeyStatusModel(currentState, lastState);
        keys.D4 = Keys.D4.GetKeyboardKeyStatusModel(currentState, lastState);
        keys.D5 = Keys.D5.GetKeyboardKeyStatusModel(currentState, lastState);
        keys.D6 = Keys.D6.GetKeyboardKeyStatusModel(currentState, lastState);
        keys.D7 = Keys.D7.GetKeyboardKeyStatusModel(currentState, lastState);
        keys.D8 = Keys.D8.GetKeyboardKeyStatusModel(currentState, lastState);
        keys.D9 = Keys.D9.GetKeyboardKeyStatusModel(currentState, lastState);
        keys.A = Keys.A.GetKeyboardKeyStatusModel(currentState, lastState);
        keys.B = Keys.B.GetKeyboardKeyStatusModel(currentState, lastState);
        keys.C = Keys.C.GetKeyboardKeyStatusModel(currentState, lastState);
        keys.D = Keys.D.GetKeyboardKeyStatusModel(currentState, lastState);
        keys.E = Keys.E.GetKeyboardKeyStatusModel(currentState, lastState);
        keys.F = Keys.F.GetKeyboardKeyStatusModel(currentState, lastState);
        keys.G = Keys.G.GetKeyboardKeyStatusModel(currentState, lastState);
        keys.H = Keys.H.GetKeyboardKeyStatusModel(currentState, lastState);
        keys.I = Keys.I.GetKeyboardKeyStatusModel(currentState, lastState);
        keys.J = Keys.J.GetKeyboardKeyStatusModel(currentState, lastState);
        keys.K = Keys.K.GetKeyboardKeyStatusModel(currentState, lastState);
        keys.L = Keys.L.GetKeyboardKeyStatusModel(currentState, lastState);
        keys.M = Keys.M.GetKeyboardKeyStatusModel(currentState, lastState);
        keys.N = Keys.N.GetKeyboardKeyStatusModel(currentState, lastState);
        keys.O = Keys.O.GetKeyboardKeyStatusModel(currentState, lastState);
        keys.P = Keys.P.GetKeyboardKeyStatusModel(currentState, lastState);
        keys.Q = Keys.Q.GetKeyboardKeyStatusModel(currentState, lastState);
        keys.R = Keys.R.GetKeyboardKeyStatusModel(currentState, lastState);
        keys.S = Keys.S.GetKeyboardKeyStatusModel(currentState, lastState);
        keys.T = Keys.T.GetKeyboardKeyStatusModel(currentState, lastState);
        keys.U = Keys.U.GetKeyboardKeyStatusModel(currentState, lastState);
        keys.V = Keys.V.GetKeyboardKeyStatusModel(currentState, lastState);
        keys.W = Keys.W.GetKeyboardKeyStatusModel(currentState, lastState);
        keys.X = Keys.X.GetKeyboardKeyStatusModel(currentState, lastState);
        keys.Y = Keys.Y.GetKeyboardKeyStatusModel(currentState, lastState);
        keys.Z = Keys.Z.GetKeyboardKeyStatusModel(currentState, lastState);
        keys.NumPad0 = Keys.NumPad0.GetKeyboardKeyStatusModel(currentState, lastState);
        keys.NumPad1 = Keys.NumPad1.GetKeyboardKeyStatusModel(currentState, lastState);
        keys.NumPad2 = Keys.NumPad2.GetKeyboardKeyStatusModel(currentState, lastState);
        keys.NumPad3 = Keys.NumPad3.GetKeyboardKeyStatusModel(currentState, lastState);
        keys.NumPad4 = Keys.NumPad4.GetKeyboardKeyStatusModel(currentState, lastState);
        keys.NumPad5 = Keys.NumPad5.GetKeyboardKeyStatusModel(currentState, lastState);
        keys.NumPad6 = Keys.NumPad6.GetKeyboardKeyStatusModel(currentState, lastState);
        keys.NumPad7 = Keys.NumPad7.GetKeyboardKeyStatusModel(currentState, lastState);
        keys.NumPad8 = Keys.NumPad8.GetKeyboardKeyStatusModel(currentState, lastState);
        keys.NumPad9 = Keys.NumPad9.GetKeyboardKeyStatusModel(currentState, lastState);
        keys.F1 = Keys.F1.GetKeyboardKeyStatusModel(currentState, lastState);
        keys.F2 = Keys.F2.GetKeyboardKeyStatusModel(currentState, lastState);
        keys.F3 = Keys.F3.GetKeyboardKeyStatusModel(currentState, lastState);
        keys.F4 = Keys.F4.GetKeyboardKeyStatusModel(currentState, lastState);
        keys.F5 = Keys.F5.GetKeyboardKeyStatusModel(currentState, lastState);
        keys.F6 = Keys.F6.GetKeyboardKeyStatusModel(currentState, lastState);
        keys.F7 = Keys.F7.GetKeyboardKeyStatusModel(currentState, lastState);
        keys.F8 = Keys.F8.GetKeyboardKeyStatusModel(currentState, lastState);
        keys.F9 = Keys.F9.GetKeyboardKeyStatusModel(currentState, lastState);
        keys.F10 = Keys.F10.GetKeyboardKeyStatusModel(currentState, lastState);
        keys.F11 = Keys.F11.GetKeyboardKeyStatusModel(currentState, lastState);
        keys.F12 = Keys.F12.GetKeyboardKeyStatusModel(currentState, lastState);
        keys.LeftControl = Keys.LeftControl.GetKeyboardKeyStatusModel(currentState, lastState);
        keys.RightControl = Keys.RightControl.GetKeyboardKeyStatusModel(currentState, lastState);
    }
}