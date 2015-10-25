// Roguelancer 0.1 Pre Alpha by Leon Aiossa
// http://www.team-nexgen.org
namespace Roguelancer.Interfaces {
    public interface IDebugText : IGame {
        #region "public variables"
        /// <summary>
        /// Text
        /// </summary>
        //string Text { get; set; }
        string GetText();
        void SetText(RoguelancerGame game, string value, bool timerEnabled);
        #endregion
    }
}