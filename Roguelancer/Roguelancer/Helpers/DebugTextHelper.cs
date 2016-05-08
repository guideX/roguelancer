// Roguelancer 0.1 Pre Alpha by Leon Aiossa
// http://team-nexgen.com
namespace Roguelancer.Helpers {
    /// <summary>
    /// Debug Text Helper
    /// </summary>
    public static class DebugTextHelper {
        /// <summary>
        /// Set Text
        /// </summary>
        /// <param name="game"></param>
        /// <param name="value"></param>
        public static void SetText(RoguelancerGame game, string value, bool timerEnabled) {
            game.DebugText.Model.TimerEnabled = timerEnabled;
            if (value != null) {
                game.DebugText.Model.CurrentShowTime = 0;
                game.DebugText.Model.ShowEnabled = true;
                game.DebugText.Model.Text = value;
            }
        }
    }
}
