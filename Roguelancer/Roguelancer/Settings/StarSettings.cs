// Roguelancer 0.1 Pre Alpha by Leon Aiossa
// http://www.team-nexgen.com
namespace Roguelancer.Settings {
    public class StarSettings {
        public bool starsEnabled { get; set; }
        public int amountOfStarsPerSheet { get; set; }
        public int maxPositionX { get; set; }
        public int maxPositionY { get; set; }
        public int maxSize { get; set; }
        public int maxPositionIncrementY { get; set; }
        public int maxPositionStartingY { get; set; }
        public int numberOfStarSheets { get; set; }
        public StarSettings
            (
                bool _starsEnabled,
                int _amountOfStarsPerSheet,
                int _maxPositionX,
                int _maxPositionY,
                int _maxSize,
                int _maxPositionIncrementY,
                int _maxPositionStartingY,
                int _numberOfStarSheets
            ) {
            starsEnabled = _starsEnabled;
            amountOfStarsPerSheet = _amountOfStarsPerSheet;
            maxPositionX = _maxPositionX;
            maxPositionY = _maxPositionY;
            maxSize = _maxSize;
            maxPositionIncrementY = _maxPositionIncrementY;
            maxPositionStartingY = _maxPositionStartingY;
            numberOfStarSheets = _numberOfStarSheets;
        }
    }
}