namespace Roguelancer.Models {
    /// <summary>
    /// Star Settings Model
    /// </summary>
    public class StarSettingsModel  {
        /// <summary>
        /// Star Settings Model
        /// </summary>
        /// <param name="starsEnabled"></param>
        /// <param name="amountOfStarsPerSheet"></param>
        /// <param name="maxPositionX"></param>
        /// <param name="maxPositionY"></param>
        /// <param name="maxSize"></param>
        /// <param name="maxPositionIncrementY"></param>
        /// <param name="maxPositionStartingY"></param>
        /// <param name="numberOfStarSheets"></param>
        public StarSettingsModel(
                bool starsEnabled,
                int amountOfStarsPerSheet,
                int maxPositionX,
                int maxPositionY,
                int maxSize,
                int maxPositionIncrementY,
                int maxPositionStartingY,
                int numberOfStarSheets) {
            StarsEnabled = starsEnabled;
            AmountOfStarsPerSheet = amountOfStarsPerSheet;
            MaxPositionX = maxPositionX;
            MaxPositionY = maxPositionY;
        }
        /// <summary>
        /// Stars Enabled
        /// </summary>
        public bool StarsEnabled { get; set; }
        /// <summary>
        /// Amount of Stars Per Sheet
        /// </summary>
        public int AmountOfStarsPerSheet { get; set; }
        /// <summary>
        /// Max Position X
        /// </summary>
        public int MaxPositionX { get; set; }
        /// <summary>
        /// Max Position Y
        /// </summary>
        public int MaxPositionY { get; set; }
        /// <summary>
        /// Max Size
        /// </summary>
        public int MaxSize { get; set; }
        /// <summary>
        /// Max Position Increment Y
        /// </summary>
        public int MaxPositionIncrementY { get; set; }
        /// <summary>
        /// Max Position Starting Y
        /// </summary>
        public int MaxPositionStartingY { get; set; }
        /// <summary>
        /// Number of Star Sheets
        /// </summary>
        public int NumberOfStarSheets { get; set; }
    }
}