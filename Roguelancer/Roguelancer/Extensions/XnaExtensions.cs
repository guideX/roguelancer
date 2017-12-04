using Roguelancer.Enum;
using Roguelancer.Models;
    /// <summary>
    /// Xna Extensions
    /// </summary>
    public static class XnaExtensions {
        /// <summary>
        /// Get Keyboard Status Model
        /// </summary>
        /// <param name="k"></param>
        /// <param name="state"></param>
        /// <param name="lastState"></param>
        /// <returns></returns>
        public static KeyboardKeyStatusModel GetKeyboardKeyStatusModel(this Microsoft.Xna.Framework.Input.Keys k, Microsoft.Xna.Framework.Input.KeyboardState state, Microsoft.Xna.Framework.Input.KeyboardState lastState, KeyboardAssignmentEnum assignment = KeyboardAssignmentEnum.None) {
            var result = new KeyboardKeyStatusModel();
            result.IsKeyDown = state.IsKeyDown(k);
            result.WasKeyPressed = (state.IsKeyDown(k) && lastState.IsKeyUp(k));
            result.Assignment = assignment;
            return result;
        }
    }